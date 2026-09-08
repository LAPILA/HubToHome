using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace HubToHome.EditorTools.ContentMaker
{
    // Author existing runtime rules; never introduce a second story-state owner.
    internal sealed class ContentMakerDialogueRulesPanel : IDisposable
    {
        private static readonly string[] Comparisons = { "같음 (=)", "다름 (≠)", "이상 (≥)", "이하 (≤)", "초과 (>)", "미만 (<)" };
        private readonly List<FlagDialogueSelector> _selectors = new List<FlagDialogueSelector>();
        private string[] _names = Array.Empty<string>();
        private string _region;
        private bool _listDirty = true;
        private string _newName = "DialogueRules_New";
        private FlagDialogueSelector _selected;
        private SerializedObject _serialized;

        public ContentMakerDialogueRulesPanel() => EditorApplication.projectChanged += Invalidate;

        private void Invalidate() => _listDirty = true;

        public void OnGUI(ContentMakerContext context)
        {
            if (_region != context.RegionPath)
            {
                _region = context.RegionPath;
                _listDirty = true;
                Select(null);
            }
            if (_listDirty) RefreshList();
            using (ContentMakerGUI.Card())
            {
                EditorGUILayout.LabelField("진행 상태별 대화", ContentMakerGUI.SectionTitle);
                EditorGUILayout.LabelField("플래그는 GlobalDataManager의 저장된 정수 상태입니다. 예: ch01.zev.defeated = 1이면 전투 후 대화. 이 화면은 조건을 편집할 뿐 게임 상태를 변경하지 않습니다.", ContentMakerGUI.Muted);
                if (_selectors.Count > 0)
                {
                    int index = _selectors.IndexOf(_selected);
                    int next = EditorGUILayout.Popup("챕터의 조건 목록", index, _names);
                    if (next != index && next >= 0 && next < _selectors.Count) Select(_selectors[next]);
                }
                else EditorGUILayout.LabelField("이 챕터에 조건별 대화 문서가 없습니다.", ContentMakerGUI.Muted);
                FlagDialogueSelector picked = (FlagDialogueSelector)EditorGUILayout.ObjectField("편집할 조건 문서", _selected, typeof(FlagDialogueSelector), false);
                if (picked != _selected) Select(picked);
                using (new EditorGUILayout.HorizontalScope())
                {
                    _newName = EditorGUILayout.TextField("새 조건 문서 이름", _newName);
                    using (new EditorGUI.DisabledScope(string.IsNullOrWhiteSpace(_region) || string.IsNullOrWhiteSpace(_newName) || context.Dialogue == null))
                        if (ContentMakerGUI.SecondaryButton("만들기", GUILayout.Width(70))) Create(context);
                }
                EditorGUILayout.LabelField("새 문서는 선택한 챕터의 Data/Dialogue에 저장합니다. 왼쪽에서 선택한 대화가 기본 대화로 연결됩니다.", ContentMakerGUI.Muted);
            }
            if (_selected != null)
                DrawRules(context);
            DrawNpcLink(context);
        }

        private void RefreshList()
        {
            _listDirty = false;
            _selectors.Clear();
            if (!string.IsNullOrEmpty(_region) && AssetDatabase.IsValidFolder(_region))
                foreach (string guid in AssetDatabase.FindAssets("t:FlagDialogueSelector", new[] { _region }))
                {
                    FlagDialogueSelector selector = AssetDatabase.LoadAssetAtPath<FlagDialogueSelector>(AssetDatabase.GUIDToAssetPath(guid));
                    if (selector != null) _selectors.Add(selector);
                }
            _selectors.Sort((left, right) => string.Compare(left.name, right.name, StringComparison.OrdinalIgnoreCase));
            _names = new string[_selectors.Count];
            for (int index = 0; index < _selectors.Count; index++) _names[index] = _selectors[index].name;
        }

        private void Select(FlagDialogueSelector selector)
        {
            _serialized?.Dispose();
            _selected = selector;
            _serialized = selector != null ? new SerializedObject(selector) : null;
        }

        private void Create(ContentMakerContext context)
        {
            try
            {
                string region = ContentMakerAssetUtility.NormalizeContentPath(context.RegionPath);
                if (!AssetDatabase.IsValidFolder(region) || context.Dialogue == null)
                    throw new InvalidOperationException("저장할 챕터와 기본 대화를 먼저 선택하세요.");
                string path = ContentMakerAssetUtility.UniqueAssetPath(region + "/Data/Dialogue", _newName, ".asset");
                FlagDialogueSelector selector = ScriptableObject.CreateInstance<FlagDialogueSelector>();
                selector.Configure(Array.Empty<FlagDialogueRule>(), context.Dialogue);
                AssetDatabase.CreateAsset(selector, path);
                ContentMakerAssetUtility.Save(selector);
                Select(selector);
                _listDirty = true;
                context.RefreshAssets();
                context.Report("조건 문서를 만들었습니다. 조건을 추가하고 방의 일반 NPC에 연결하세요.");
            }
            catch (ExitGUIException) { throw; }
            catch (Exception error) { context.Report(error.Message, MessageType.Error); }
        }

        private void DrawRules(ContentMakerContext context)
        {
            using (ContentMakerGUI.Card())
            {
                _serialized.Update();
                EditorGUILayout.LabelField(_selected.name + " · 조건 편집", ContentMakerGUI.SectionTitle);
                EditorGUILayout.PropertyField(_serialized.FindProperty("_fallbackDialogue"), new GUIContent("기본 대화", "맞는 조건이 없으면 사용할 대화입니다."));
                EditorGUILayout.HelpBox("조건 하나는 플래그 하나를 비교합니다. 맞는 조건 중 우선순위 숫자가 가장 큰 대화를 사용하고, 동점이면 위쪽 조건을 사용합니다. 여러 조건의 AND/OR, NPC 위치·출현 제어, 메인 연출 실행은 이 문서의 기능이 아닙니다.", MessageType.Info);
                SerializedProperty rules = _serialized.FindProperty("_rules");
                for (int index = 0; index < rules.arraySize; index++)
                {
                    SerializedProperty rule = rules.GetArrayElementAtIndex(index);
                    using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                    {
                        using (new EditorGUILayout.HorizontalScope())
                        {
                            EditorGUILayout.LabelField("조건 " + (index + 1), ContentMakerGUI.SectionTitle);
                            using (new EditorGUI.DisabledScope(index == 0))
                                if (GUILayout.Button("↑", GUILayout.Width(28)))
                                {
                                    rules.MoveArrayElement(index, index - 1);
                                    _serialized.ApplyModifiedProperties();
                                    return;
                                }
                            using (new EditorGUI.DisabledScope(index == rules.arraySize - 1))
                                if (GUILayout.Button("↓", GUILayout.Width(28)))
                                {
                                    rules.MoveArrayElement(index, index + 1);
                                    _serialized.ApplyModifiedProperties();
                                    return;
                                }
                            if (GUILayout.Button("삭제", GUILayout.Width(46)))
                            {
                                rules.DeleteArrayElementAtIndex(index);
                                _serialized.ApplyModifiedProperties();
                                return;
                            }
                        }
                        EditorGUILayout.PropertyField(rule.FindPropertyRelative("_flagKey"), new GUIContent("플래그 키"));
                        SerializedProperty comparison = rule.FindPropertyRelative("_comparison");
                        comparison.enumValueIndex = EditorGUILayout.Popup("비교 방법", comparison.enumValueIndex, Comparisons);
                        EditorGUILayout.PropertyField(rule.FindPropertyRelative("_expectedValue"), new GUIContent("비교 값"));
                        EditorGUILayout.PropertyField(rule.FindPropertyRelative("_priority"), new GUIContent("우선순위"));
                        EditorGUILayout.PropertyField(rule.FindPropertyRelative("_dialogue"), new GUIContent("재생할 대화"));
                    }
                }
                if (ContentMakerGUI.SecondaryButton("+ 조건 추가 · 선택한 대화 연결"))
                {
                    int index = rules.arraySize;
                    rules.InsertArrayElementAtIndex(index);
                    SerializedProperty rule = rules.GetArrayElementAtIndex(index);
                    // Unity may duplicate the previous element, so initialize every field explicitly.
                    rule.FindPropertyRelative("_flagKey").stringValue = string.Empty;
                    rule.FindPropertyRelative("_comparison").enumValueIndex = 0;
                    rule.FindPropertyRelative("_expectedValue").intValue = 1;
                    rule.FindPropertyRelative("_priority").intValue = 0;
                    rule.FindPropertyRelative("_dialogue").objectReferenceValue = context.Dialogue;
                }
                _serialized.ApplyModifiedProperties();
                bool valid = _selected.TryValidate(out string error);
                if (!valid) EditorGUILayout.HelpBox(error, MessageType.Warning);
                using (new EditorGUILayout.HorizontalScope())
                {
                    using (new EditorGUI.DisabledScope(!valid))
                        if (ContentMakerGUI.PrimaryButton("조건 문서 저장"))
                        {
                            ContentMakerAssetUtility.Save(_selected);
                            context.Report("선택한 조건 문서만 저장했습니다.");
                        }
                    if (ContentMakerGUI.SecondaryButton("Project에서 찾기")) EditorGUIUtility.PingObject(_selected);
                }
            }
        }

        private void DrawNpcLink(ContentMakerContext context)
        {
            using (ContentMakerGUI.Card())
            {
                EditorGUILayout.LabelField("방의 일반 NPC에 연결", ContentMakerGUI.SectionTitle);
                EditorGUILayout.LabelField("마커 · NPC에서 방을 선택해 프리팹 편집 모드로 열고, Hierarchy에서 일반 NPC(NPCMarker)를 선택하세요. 다른 씬·방·프로젝트 원본은 이 버튼으로 수정하지 않습니다.", ContentMakerGUI.Muted);
                bool editable = TryGetNpc(context, out NPCMarker npc, out string error);
                EditorGUILayout.LabelField(editable ? "연결 대상: " + npc.name : error, ContentMakerGUI.Muted);
                using (new EditorGUI.DisabledScope(!editable))
                {
                    if (ContentMakerGUI.SecondaryButton("이 NPC의 조건 문서 가져오기"))
                    {
                        using (SerializedObject npcObject = new SerializedObject(npc))
                        {
                            FlagDialogueSelector selector = npcObject.FindProperty("dialogueSelector").objectReferenceValue as FlagDialogueSelector;
                            if (selector != null) Select(selector);
                            else context.Report("선택한 NPC에는 조건 문서가 없습니다.", MessageType.Info);
                        }
                    }
                    using (new EditorGUI.DisabledScope(_selected == null || !_selected.TryValidate(out _)))
                        if (ContentMakerGUI.PrimaryButton("이 조건 문서를 선택한 NPC에 연결")) LinkNpc(context);
                }
                EditorGUILayout.HelpBox("연결 후에는 방 프리팹을 저장하세요. 제브 접근·공격처럼 DialogueBattleNPC가 담당하는 이벤트는 이 일반 NPC 연결의 대상이 아닙니다. 메인 연출은 기존 시나리오 시퀀스에서 관리합니다.", MessageType.Info);
            }
        }

        private static bool TryGetNpc(ContentMakerContext context, out NPCMarker npc, out string error)
        {
            npc = null;
            if (!ContentMakerMarkerService.TryGetEditableRoom(context.Room, out RoomInstance room, out error)) return false;
            GameObject selected = Selection.activeGameObject;
            if (selected == null || selected.scene != room.gameObject.scene || !selected.transform.IsChildOf(room.transform))
            {
                error = "현재 편집하는 방 안의 NPC 오브젝트를 Hierarchy에서 선택하세요.";
                return false;
            }
            npc = selected.GetComponent<NPCMarker>();
            if (npc == null)
            {
                error = "선택한 오브젝트에 일반 NPCMarker가 없습니다. NPC 본체를 선택하세요.";
                return false;
            }
            error = string.Empty;
            return true;
        }

        private void LinkNpc(ContentMakerContext context)
        {
            if (!TryGetNpc(context, out NPCMarker npc, out string error)) { context.Report(error, MessageType.Warning); return; }
            if (_selected == null || !_selected.TryValidate(out error)) { context.Report(error ?? "조건 문서를 선택하세요.", MessageType.Warning); return; }
            using (SerializedObject npcObject = new SerializedObject(npc))
            {
                SerializedProperty property = npcObject.FindProperty("dialogueSelector");
                if (property.objectReferenceValue == _selected) { context.Report("이미 같은 조건 문서가 연결되어 있습니다."); return; }
                if (property.objectReferenceValue != null && !EditorUtility.DisplayDialog("NPC 조건 문서 변경", "기존 조건 연결을 선택한 문서로 바꿉니다. 기존 조건 문서는 삭제하지 않습니다.", "연결 변경", "취소")) return;
                property.objectReferenceValue = _selected;
                npcObject.ApplyModifiedProperties();
                if (PrefabUtility.IsPartOfPrefabInstance(npc)) PrefabUtility.RecordPrefabInstancePropertyModifications(npc);
                EditorSceneManager.MarkSceneDirty(npc.gameObject.scene);
                context.Report("선택한 NPC의 조건 연결을 변경했습니다. 편집 중인 방 프리팹을 저장하세요.");
            }
        }

        public void Dispose()
        {
            EditorApplication.projectChanged -= Invalidate;
            _serialized?.Dispose();
            _serialized = null;
        }
    }
}
