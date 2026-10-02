#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Sirenix.OdinInspector.Editor;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEngine;

namespace HubToHome.EditorTools.SkillMaker
{
    /// <summary>기존 SkillData의 블록만 편집합니다. 자산 생성과 저장은 호출자가 소유합니다.</summary>
    internal sealed class SkillMakerBlockEditor : IDisposable
    {
        private static readonly Type[] BuiltInTypes =
        {
            typeof(Action_Wait), typeof(Action_Move), typeof(Action_PlayAnim),
            typeof(Action_Damage), typeof(Action_ApplyStatus), typeof(Action_QTE),
            typeof(Action_VFX), typeof(Action_DefenseWindow), typeof(Action_Projectile),
            typeof(Action_SequentialMelee), typeof(Action_RapidStrikes), typeof(Action_AerialCrossSlash),
            typeof(Action_EnemyWindup)
        };

        private static readonly IReadOnlyList<Type> CachedBlockTypes = DiscoverBlockTypes();
        private SkillData _skill;
        private PropertyTree _tree;
        private bool _invalidated;
        private bool _drawing;
        private bool _disposed;
        private bool _hasEditabilityCache;
        private bool _cachedCanEdit;
        private bool _cachedPlayMode;
        private string _cachedEditReason;
        private double _nextEditabilityCheck;

        public event Action Changed;

        public static IReadOnlyList<Type> BlockTypes => CachedBlockTypes;

        public void SetSkill(SkillData skill)
        {
            if (_disposed || ReferenceEquals(_skill, skill))
                return;

            _skill = skill;
            Invalidate();
        }

        /// <summary>외부 Inspector 변경이나 Undo/Redo 이후 호출합니다.</summary>
        public void Invalidate()
        {
            _invalidated = true;
            _hasEditabilityCache = false;
            if (!_drawing)
                DisposeTree();
        }

        public void Draw(int index)
        {
            if (_disposed)
                return;
            if (!CanDraw(out string reason))
            {
                // Keep a failed permission check cached too; do not invalidate it on each repaint.
                _invalidated = true;
                DisposeTree();
                EditorGUILayout.HelpBox(reason, MessageType.Info);
                return;
            }
            if (!HasIndex(index))
            {
                EditorGUILayout.HelpBox("목록에서 편집할 실행 블록을 선택하세요.", MessageType.Info);
                return;
            }
            if (_skill.ActionTimeline[index] == null)
            {
                EditorGUILayout.HelpBox("이 블록의 형식 또는 참조가 없습니다. 목록에서 삭제한 뒤 필요한 블록을 추가하세요.", MessageType.Warning);
                return;
            }

            if (_skill.ActionTimeline[index] is Action_DefenseWindow defense)
                EditorGUILayout.HelpBox(GetDefenseAuthoringHelp(defense.Requirement), MessageType.Info);
            else
                EditorGUILayout.HelpBox(GetBlockHelp(_skill.ActionTimeline[index].GetType()), MessageType.Info);

            EnsureTree();
            PropertyTree drawingTree = _tree;
            SkillData drawingSkill = _skill;
            int previousDirtyCount = EditorUtility.GetDirtyCount(drawingSkill);
            bool beganDrawing = false;
            bool guiChanged;
            _drawing = true;
            EditorGUI.BeginChangeCheck();
            try
            {
                // 전체 트리를 그리지 않아 SkillData의 나머지 필드나 리스트 편집 UI가 중복되지 않습니다.
                drawingTree.BeginDraw(true);
                beganDrawing = true;
                InspectorProperty timeline = drawingTree.GetPropertyAtPath(nameof(SkillData.ActionTimeline));
                if (timeline == null || index >= timeline.Children.Count)
                {
                    EditorGUILayout.HelpBox("블록 목록이 변경되었습니다. 목록에서 다시 선택하세요.", MessageType.Info);
                    _invalidated = true;
                }
                else
                {
                    InspectorProperty selected = timeline.Children[index];
                    selected.State.Expanded = true;
                    selected.Draw(GUIContent.none);
                }
            }
            finally
            {
                try
                {
                    if (beganDrawing)
                        drawingTree.EndDraw();
                }
                finally
                {
                    guiChanged = EditorGUI.EndChangeCheck();
                    _drawing = false;
                    if (_invalidated)
                        DisposeTree();
                }
            }

            // Odin이 상세 필드의 Undo와 직렬화를 소유합니다. 값 변경 이후 목록/검사만 알립니다.
            if (drawingSkill != null && (guiChanged
                || EditorUtility.GetDirtyCount(drawingSkill) != previousDirtyCount))
            {
                Changed?.Invoke();
            }
        }

        public int Insert(int index, Type blockType)
        {
            if (!CanMutate() || index < 0 || index > Count || !IsAvailableType(blockType))
                return -1;

            SkillActionBlock block;
            try
            {
                block = (SkillActionBlock)Activator.CreateInstance(blockType, true);
            }
            catch (Exception exception)
            {
                Debug.LogError("[스킬 메이커] 블록을 만들지 못했습니다: " + exception.GetBaseException().Message, _skill);
                return -1;
            }

            int group = BeginMutation("스킬 블록 추가");
            if (_skill.ActionTimeline == null)
                _skill.ActionTimeline = new List<SkillActionBlock>();
            _skill.ActionTimeline.Insert(index, block);
            CompleteMutation(group);
            return index;
        }

        public int Duplicate(int index)
        {
            if (!CanMutate() || !HasIndex(index) || _skill.ActionTimeline[index] == null)
                return -1;

            SkillActionBlock copy;
            try
            {
                // 중첩 managed reference는 분리하고 Unity 자산 참조는 유지하는 Odin의 깊은 복사입니다.
                copy = Sirenix.Serialization.SerializationUtility.CreateCopy(_skill.ActionTimeline[index]) as SkillActionBlock;
            }
            catch (Exception exception)
            {
                Debug.LogError("[스킬 메이커] 블록을 복제하지 못했습니다: " + exception.GetBaseException().Message, _skill);
                return -1;
            }
            if (copy == null || ReferenceEquals(copy, _skill.ActionTimeline[index]))
                return -1;

            int group = BeginMutation("스킬 블록 복제");
            _skill.ActionTimeline.Insert(index + 1, copy);
            CompleteMutation(group);
            return index + 1;
        }

        public int Remove(int index)
        {
            if (!CanMutate() || !HasIndex(index))
                return -1;

            int group = BeginMutation("스킬 블록 삭제");
            _skill.ActionTimeline.RemoveAt(index);
            int selection = Count == 0 ? -1 : Math.Min(index, Count - 1);
            CompleteMutation(group);
            return selection;
        }

        /// <summary>to는 이동이 끝난 목록에서의 인덱스입니다.</summary>
        public int Move(int from, int to)
        {
            if (!CanMutate() || !HasIndex(from) || !HasIndex(to))
                return -1;
            if (from == to)
                return from;

            int group = BeginMutation("스킬 블록 순서 변경");
            SkillActionBlock block = _skill.ActionTimeline[from];
            _skill.ActionTimeline.RemoveAt(from);
            _skill.ActionTimeline.Insert(to, block);
            CompleteMutation(group);
            return to;
        }

        public void Dispose()
        {
            if (_disposed)
                return;
            _disposed = true;
            _skill = null;
            Changed = null;
            Invalidate();
        }

        public static string GetBlockName(Type type)
        {
            // SkillActionBlock.BlockName의 기존 한글 이름을 사용하며 사용자 정의 생성자는 호출하지 않습니다.
            if (type == null) return "누락 블록";
            if (typeof(Action_Wait).IsAssignableFrom(type)) return "대기";
            if (typeof(Action_Move).IsAssignableFrom(type)) return "이동";
            if (typeof(Action_PlayAnim).IsAssignableFrom(type)) return "애니메이션";
            if (typeof(Action_Damage).IsAssignableFrom(type)) return "피해 적용";
            if (typeof(Action_ApplyStatus).IsAssignableFrom(type)) return "상태 효과 부여";
            if (typeof(Action_QTE).IsAssignableFrom(type)) return "실시간 입력 (QTE)";
            if (typeof(Action_VFX).IsAssignableFrom(type)) return "이펙트 재생";
            if (typeof(Action_DefenseWindow).IsAssignableFrom(type)) return "방어 대응";
            if (typeof(Action_Projectile).IsAssignableFrom(type)) return "투사체";
            if (typeof(Action_SequentialMelee).IsAssignableFrom(type)) return "연쇄 근접";
            if (typeof(Action_RapidStrikes).IsAssignableFrom(type)) return "고속 연격 · 독립 QTE";
            if (typeof(Action_AerialCrossSlash).IsAssignableFrom(type)) return "공중 회전 · 교차 베기";
            if (typeof(Action_EnemyWindup).IsAssignableFrom(type)) return "적 공격 준비 · 클로즈업";
            return type.Name.Replace("Action_", string.Empty);
        }

        internal static string GetBlockMenuPath(Type type)
        {
            string category = typeof(Action_QTE).IsAssignableFrom(type)
                || typeof(Action_RapidStrikes).IsAssignableFrom(type)
                || typeof(Action_AerialCrossSlash).IsAssignableFrom(type) ? "아군 전용"
                : typeof(Action_DefenseWindow).IsAssignableFrom(type)
                    || typeof(Action_EnemyWindup).IsAssignableFrom(type) ? "적 공격"
                : typeof(Action_Damage).IsAssignableFrom(type) || typeof(Action_ApplyStatus).IsAssignableFrom(type)
                    || typeof(Action_Projectile).IsAssignableFrom(type) || typeof(Action_SequentialMelee).IsAssignableFrom(type)
                    ? "공통 · 피해와 효과"
                : typeof(Action_Wait).IsAssignableFrom(type) || typeof(Action_Move).IsAssignableFrom(type)
                    || typeof(Action_PlayAnim).IsAssignableFrom(type) || typeof(Action_VFX).IsAssignableFrom(type)
                    ? "공통 · 흐름과 연출" : "사용자 정의";
            return category + "/" + GetBlockName(type);
        }

        internal static string GetBlockHelp(Type type)
        {
            if (typeof(Action_Wait).IsAssignableFrom(type)) return "공통 · 다음 블록으로 넘어가기 전에 기다립니다. 시간은 초 단위입니다.";
            if (typeof(Action_Move).IsAssignableFrom(type)) return "공통 · 시전자를 지정 위치로 이동합니다. 일반 접근은 ‘자동 공격 위치’, 마무리는 ‘원래 자리’를 사용합니다. 이동만 하며 피해는 별도 블록입니다.";
            if (typeof(Action_PlayAnim).IsAssignableFrom(type)) return "공통 · Animator에 등록된 트리거를 실행합니다. 이름은 대소문자까지 일치해야 합니다. 피해는 별도로 적용합니다.";
            if (typeof(Action_Damage).IsAssignableFrom(type)) return "공통 · 선택 대상에 피해를 적용합니다. 배율 1 = 기본 배율, 1.5 = 150%입니다. 적 스킬은 앞에 ‘방어 대응’을 배치하세요.";
            if (typeof(Action_ApplyStatus).IsAssignableFrom(type)) return "공통 · 선택 대상에 상태 효과를 부여합니다. 상태 ID는 표시 이름이 아닌 내부 식별자입니다. 지속 시간은 해당 캐릭터의 턴 기준입니다.";
            if (typeof(Action_QTE).IsAssignableFrom(type)) return "아군 전용 · 입력을 시작한 뒤 이동·애니메이션과 동시에 진행합니다. 피해 블록이 결과를 기다립니다. 적의 Z/X/C 대응은 이 블록이 아닌 ‘방어 대응’입니다.";
            if (typeof(Action_VFX).IsAssignableFrom(type)) return "공통 · 지정 위치에 이펙트 프리팹을 재생합니다. 피해는 발생시키지 않습니다. 적 타격 직전 전조는 ‘방어 대응’의 전조 프리팹으로 지정하세요.";
            if (typeof(Action_DefenseWindow).IsAssignableFrom(type)) return "적 공격 · Z/X/C 방어 입력과 타격 시점을 연결합니다. 바로 뒤에 피해·투사체·연쇄 근접 중 하나를 배치하세요.";
            if (typeof(Action_Projectile).IsAssignableFrom(type)) return "공통 · 투사체 이동과 충돌 피해를 함께 처리합니다. 같은 타격의 ‘피해 적용’을 중복 추가하지 마세요. 적은 앞에 ‘방어 대응’을 배치합니다.";
            if (typeof(Action_SequentialMelee).IsAssignableFrom(type)) return "공통 · 선택한 대상들을 섞인 순서로 찾아가 공격하고 피해를 줍니다. 이동 시간은 초 단위입니다. 적은 앞에 ‘방어 대응’을 배치합니다.";
            if (typeof(Action_RapidStrikes).IsAssignableFrom(type)) return "아군 전용 · 타격 간격마다 공격하며 QTE는 별도 주기로 진행합니다. 이동·입력·피해가 포함되어 같은 타격을 중복 추가할 필요가 없습니다.";
            if (typeof(Action_AerialCrossSlash).IsAssignableFrom(type)) return "아군 전용 · 접근 → 상승 → 카메라 360도 회전 → QTE → 교차 베기 → 복귀를 처리합니다. 캐릭터 자체를 회전시키는 블록이 아닙니다.";
            if (typeof(Action_EnemyWindup).IsAssignableFrom(type)) return "적 전용 · 적을 확대하고 준비 자세를 보여준 뒤 공격 구도로 돌아옵니다. 다음에 ‘방어 대응 → 피해’를 배치하세요. 이 블록은 피해를 주지 않습니다.";
            return "사용자 정의 블록 · 아래 설정과 해당 블록의 제작 규칙을 확인하세요. 저장 전 ‘검사’ 탭에서 결과를 확인하세요.";
        }

        internal static string GetDefenseAuthoringHelp(DefenseRequirement requirement)
        {
            const string timing = "\n판정 시간은 입력 시작부터 타격까지의 시간입니다. 저스트 구간은 공통 설정 또는 개별 판정 구간의 첫 값, 회피/반격 구간은 QTEManager 공통 설정을 사용합니다. 모든 구간은 난도 배율을 적용합니다.";
            if (requirement == DefenseRequirement.Counterable)
                return "연계 반격 공격: Z 가드 불가 / X 회피 / C 연계 반격. 반격 피해 배율은 공격받은 아군 한 명의 ATK에 적용됩니다. 다른 전열과 후열은 참여하지 않습니다."
                    + "\n접근 → 전조 포함 방어 대응 → 피해 → 원위치 복귀 순서로 배치하세요. C 성공 시 남은 타임라인을 생략하고 공격받은 한 명이 접근 → 패링 → 공격한 뒤 양쪽 복귀합니다. 다른 아군은 이동하지 않습니다."
                    + "\n전조와 충분한 판정 시간을 제공하세요. 일반 강공격이 자동으로 반격 공격이 되지는 않습니다." + timing;
            if (requirement == DefenseRequirement.DodgeOnly || requirement == DefenseRequirement.JumpOnly
                || requirement == DefenseRequirement.DodgeOrJump)
                return "현재 방어 모드에서는 X 회피 전용입니다. Z 가드와 C 반격은 불가합니다. 기존 점프 요구 값은 자산 호환을 위해 유지됩니다." + timing;
            return "일반 공격: Z를 누르고 있으면 가드, 타격 직전 새로 누르면 저스트 가드 / X 회피 / C 반격 불가."
                + "\nC 반격 기회를 만들려면 공격 대응 방식에서 ‘반격 가능 특수공격 (회피 / 연계 반격)’을 선택하세요. 기존 BAD/Great/Good 설정은 구형 방어 모드용입니다." + timing;
        }

        private int Count => _skill != null && _skill.ActionTimeline != null ? _skill.ActionTimeline.Count : 0;
        private bool HasIndex(int index) => index >= 0 && index < Count;
        private bool CanMutate() => !_disposed && !_drawing && SkillMakerAssetUtility.CanEdit(_skill, out _);

        private bool CanDraw(out string reason)
        {
            Event current = Event.current;
            bool displayOnly = current != null
                && (current.type == EventType.Layout || current.type == EventType.Repaint);
            bool playing = EditorApplication.isPlayingOrWillChangePlaymode;
            double now = EditorApplication.timeSinceStartup;
            // Layout/Repaint reuse the result for at most one second. Input events always revalidate.
            if (!_hasEditabilityCache || !displayOnly || now >= _nextEditabilityCheck
                || _cachedPlayMode != playing || (_skill == null && _cachedCanEdit))
            {
                _cachedCanEdit = SkillMakerAssetUtility.CanEdit(_skill, out _cachedEditReason);
                _cachedPlayMode = playing;
                _nextEditabilityCheck = now + 1d;
                _hasEditabilityCache = true;
            }
            reason = _cachedEditReason;
            return _cachedCanEdit;
        }

        private void EnsureTree()
        {
            if (_invalidated)
                DisposeTree();
            if (_tree == null)
                _tree = PropertyTree.Create(_skill);
            _invalidated = false;
        }

        private void DisposeTree()
        {
            _tree?.Dispose();
            _tree = null;
        }

        private int BeginMutation(string label)
        {
            Invalidate();
            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName(label);
            Undo.RegisterCompleteObjectUndo(_skill, label);
            return group;
        }

        private void CompleteMutation(int group)
        {
            EditorUtility.SetDirty(_skill);
            Undo.CollapseUndoOperations(group);
            Undo.IncrementCurrentGroup();
            Changed?.Invoke();
        }

        private static bool IsAvailableType(Type type)
        {
            for (int i = 0; i < CachedBlockTypes.Count; i++)
                if (CachedBlockTypes[i] == type)
                    return true;
            return false;
        }

        private static IReadOnlyList<Type> DiscoverBlockTypes()
        {
            var types = new List<Type>(BuiltInTypes);
            var runtimeAssemblies = new HashSet<string>(StringComparer.Ordinal);
            foreach (UnityEditor.Compilation.Assembly assembly in CompilationPipeline.GetAssemblies(AssembliesType.Player))
            {
                runtimeAssemblies.Add(assembly.name);
                // 플레이어용 사전 컴파일 DLL의 사용자 정의 블록도 포함하고 Editor/테스트 전용 형식은 제외합니다.
                foreach (string reference in assembly.compiledAssemblyReferences)
                    runtimeAssemblies.Add(Path.GetFileNameWithoutExtension(reference));
            }

            var customTypes = new List<Type>();
            foreach (Type type in TypeCache.GetTypesDerivedFrom<SkillActionBlock>())
            {
                if (type.IsAbstract || type.IsInterface || type.ContainsGenericParameters
                    || !Attribute.IsDefined(type, typeof(SerializableAttribute), false)
                    || !runtimeAssemblies.Contains(type.Assembly.GetName().Name)
                    || types.Contains(type))
                    continue;
                if (type.GetConstructor(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                    null, Type.EmptyTypes, null) == null)
                    continue;
                customTypes.Add(type);
            }
            customTypes.Sort((left, right) => string.Compare(left.FullName, right.FullName, StringComparison.Ordinal));
            types.AddRange(customTypes);
            return types.AsReadOnly();
        }
    }
}
#endif
