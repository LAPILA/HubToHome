using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Sirenix.OdinInspector;
using Sirenix.OdinInspector.Editor;
using UnityEditor;
using UnityEngine;

public sealed class ScenarioSequenceOdinEditorWindow : OdinEditorWindow
{
    private ActionCatalogAsset _builtinCatalog;
    private ActionCatalogAsset _mergedCatalogCache;

    // Migration-only implementation retained for old draft conversion tests.
    // SequenceMakerWindow is the sole discoverable authoring surface.
    private static void OpenWindow()
    {
        ScenarioSequenceOdinEditorWindow window = GetWindow<ScenarioSequenceOdinEditorWindow>();
        window.titleContent = new GUIContent("Odin 시퀀스 에디터");
        window.minSize = new Vector2(960f, 620f);
        window.Show();
    }

    [BoxGroup("대상")]
    [OnValueChanged(nameof(HandleScenarioChanged))]
    [LabelText("Battle Scenario")]
    public BattleScenarioData Scenario;

    [BoxGroup("대상")]
    [OnValueChanged(nameof(LoadSelectedSequence))]
    [ValueDropdown(nameof(GetSequenceOptions))]
    [LabelText("시퀀스")]
    public string SelectedSequenceId = string.Empty;

    [BoxGroup("대상")]
    [OnValueChanged(nameof(RefreshAllBlockCatalogViews))]
    [LabelText("Action Catalog (선택)")]
    public ActionCatalogAsset Catalog;

    [BoxGroup("상태")]
    [ReadOnly]
    [MultiLineProperty(3)]
    [LabelText("메시지")]
    public string StatusMessage = "Battle Scenario를 선택하세요.";

    [BoxGroup("상태")]
    [ShowInInspector, ReadOnly]
    [LabelText("Catalog 상태")]
    public string CatalogStatus => BuildCatalogStatus();

    [BoxGroup("상태")]
    [ShowInInspector, ReadOnly]
    [MultiLineProperty(2)]
    [LabelText("동기화 규칙")]
    public string SyncRuleNotice => "Scenario YAML이 source of truth입니다. Runtime Asset Only 적용은 YAML에 아직 저장되지 않습니다.";

    [ShowIf(nameof(HasLoadedSequence))]
    [BoxGroup("도구")]
    [Button("Add Action")]
    private void AddRootAction()
    {
        Blocks.Add(CreateDraft(CreateDefaultActionId(), Blocks));
        RefreshAllBlockCatalogViews();
        StatusMessage = "루트 액션 블록을 추가했습니다. Runtime Asset Only 적용 또는 Source 저장/재반영을 실행하세요. YAML에는 아직 저장되지 않았습니다.";
    }

    [ShowIf(nameof(HasLoadedSequence))]
    [BoxGroup("도구")]
    [Button("Validate Sequence")]
    private void ValidateCurrentSequence()
    {
        if (!HasLoadedSequence())
        {
            StatusMessage = "검증할 시퀀스가 없습니다.";
            return;
        }

        ActionSequenceAsset tempSequence = BuildTemporarySequence();
        BattleScenarioData tempScenario = BuildTemporaryScenario(tempSequence);
        ActionCatalogAsset effectiveCatalog = BuildEffectiveCatalog();
        ScenarioValidationResult result = ScenarioCatalogValidator.ValidateBattleScenario(tempScenario, effectiveCatalog);
        AppendEditorOnlyValidation(result, Blocks, SelectedSequenceId);
        DestroyImmediate(tempScenario);
        DestroyImmediate(tempSequence);

        if (result.Messages.Count == 0)
        {
            StatusMessage = "시퀀스 검증 성공: 문제를 찾지 못했습니다.";
            return;
        }

        StatusMessage = BuildValidationSummary(result);
    }

    [ShowIf(nameof(HasLoadedSequence))]
    [BoxGroup("도구")]
    [Button("Apply Runtime Asset Only")]
    private void ApplyRuntimeAssetOnly()
    {
        if (!TryApplyBlocksToSelectedSequence(out ActionSequenceAsset sequence))
        {
            return;
        }

        StatusMessage = "현재 블록 편집 내용을 Runtime Action Sequence 에셋에만 반영했습니다. YAML에는 아직 저장되지 않았습니다.";
    }

    [ShowIf(nameof(HasLoadedSequence))]
    [BoxGroup("도구")]
    [Button("Save Source And Reimport")]
    private void SaveSourceAndReimport()
    {
        if (Scenario == null)
        {
            StatusMessage = "저장할 Battle Scenario를 먼저 선택하세요.";
            return;
        }

        if (!TryApplyBlocksToSelectedSequence(out ActionSequenceAsset sequence))
        {
            return;
        }

        var exportCommand = new ScenarioSourceYamlExportCommand();
        ScenarioSourceYamlExportResult exportResult = exportCommand.ExportToSourcePath(Scenario);
        if (!exportResult.Success)
        {
            StatusMessage = BuildValidationSummary(exportResult.Validation) + "\nYAML 저장에 실패했습니다. Runtime Asset에는 편집 내용이 남아 있을 수 있습니다.";
            return;
        }

        ScenarioSourceMetadataEditorSync.ApplyExportResult(Scenario, exportResult, DateTime.UtcNow);
        EditorUtility.SetDirty(Scenario);
        if (sequence != null)
        {
            EditorUtility.SetDirty(sequence);
        }

        var reimportCommand = new ScenarioSourceRuntimeAssetReimportCommand();
        ScenarioSourceRuntimeAssetReimportResult reimportResult = reimportCommand.ReimportFromSourcePath(
            Scenario,
            BuildEffectiveCatalog(),
            DateTime.UtcNow);

        LoadSelectedSequence();
        if (!reimportResult.Success)
        {
            StatusMessage = BuildValidationSummary(reimportResult.Validation) + "\nSource YAML은 저장했지만 validation-first reimport는 실패했습니다.";
            return;
        }

        StatusMessage = "현재 블록 편집 내용을 Source YAML로 저장하고 validation-first reimport를 완료했습니다.";
    }

    [ShowIf(nameof(HasLoadedSequence))]
    [BoxGroup("도구")]
    [Button("Reload From Asset")]
    private void ReloadFromAsset()
    {
        LoadSelectedSequence();
        StatusMessage = "시퀀스 에셋에서 블록 편집 뷰를 다시 불러왔습니다.";
    }

    [ShowIf(nameof(HasLoadedSequence))]
    [ListDrawerSettings(ShowFoldout = true, DefaultExpandedState = true, DraggableItems = false, ShowIndexLabels = true, ListElementLabelName = nameof(ScenarioActionBlockDraft.BlockHeader))]
    [LabelText("Scenario Blocks")]
    public List<ScenarioActionBlockDraft> Blocks = new List<ScenarioActionBlockDraft>();

    protected override void OnDestroy()
    {
        base.OnDestroy();
        if (_mergedCatalogCache != null)
        {
            DestroyImmediate(_mergedCatalogCache);
            _mergedCatalogCache = null;
        }

        if (_builtinCatalog != null)
        {
            DestroyImmediate(_builtinCatalog);
            _builtinCatalog = null;
        }
    }

    internal ActionCatalogEntry FindCatalogEntry(string actionId)
    {
        ActionCatalogAsset preferredCatalog = GetPreferredCatalogAsset();
        ActionCatalogEntry entry = preferredCatalog != null ? preferredCatalog.FindById(actionId) : null;
        if (entry != null)
        {
            return entry;
        }

        EnsureBuiltinCatalog();
        return _builtinCatalog != null ? _builtinCatalog.FindById(actionId) : null;
    }

    internal ValueDropdownList<string> BuildActionOptions()
    {
        var options = new ValueDropdownList<string>();
        EnsureBuiltinCatalog();

        ActionCatalogAsset preferredCatalog = GetPreferredCatalogAsset();
        if (preferredCatalog != null)
        {
            AddActionOptions(options, preferredCatalog);
        }

        if (_builtinCatalog != null)
        {
            AddActionOptions(options, _builtinCatalog, false);
        }

        if (options.Count == 0)
        {
            options.Add("flow.wait", "flow.wait");
        }

        return options;
    }

    internal void RefreshAllBlockCatalogViews()
    {
        for (int i = 0; i < Blocks.Count; i++)
        {
            Blocks[i].RefreshCatalogViewRecursive(this, Blocks);
        }
    }

    internal void DuplicateBlock(ScenarioActionBlockDraft block)
    {
        if (block == null || block.OwnerList == null)
        {
            return;
        }

        int index = block.OwnerList.IndexOf(block);
        if (index < 0)
        {
            return;
        }

        ScenarioActionBlockDraft clone = block.CloneDeep();
        block.OwnerList.Insert(index + 1, clone);
        clone.RefreshCatalogViewRecursive(this, block.OwnerList);
        StatusMessage = "블록을 복제했습니다. Runtime Asset Only 적용 또는 Source 저장/재반영을 실행하세요. YAML에는 아직 저장되지 않았습니다.";
    }

    internal void RemoveBlock(ScenarioActionBlockDraft block)
    {
        if (block == null || block.OwnerList == null)
        {
            return;
        }

        block.OwnerList.Remove(block);
        StatusMessage = "블록을 삭제했습니다. Runtime Asset Only 적용 또는 Source 저장/재반영을 실행하세요. YAML에는 아직 저장되지 않았습니다.";
    }

    internal void MoveBlock(ScenarioActionBlockDraft block, int direction)
    {
        if (block == null || block.OwnerList == null)
        {
            return;
        }

        int index = block.OwnerList.IndexOf(block);
        int target = index + direction;
        if (index < 0 || target < 0 || target >= block.OwnerList.Count)
        {
            return;
        }

        ScenarioActionBlockDraft temp = block.OwnerList[index];
        block.OwnerList[index] = block.OwnerList[target];
        block.OwnerList[target] = temp;
        StatusMessage = "블록 순서를 변경했습니다. Runtime Asset Only 적용 또는 Source 저장/재반영을 실행하세요. YAML에는 아직 저장되지 않았습니다.";
    }

    internal ScenarioActionBlockDraft CreateDraft(string actionId, List<ScenarioActionBlockDraft> ownerList)
    {
        var draft = new ScenarioActionBlockDraft();
        draft.BlockId = ScenarioBlockIdentity.Create();
        draft.OwnerList = ownerList;
        draft.ActionId = string.IsNullOrWhiteSpace(actionId) ? CreateDefaultActionId() : actionId.Trim();
        draft.RefreshCatalogViewRecursive(this, ownerList);
        return draft;
    }

    private void HandleScenarioChanged()
    {
        if (Scenario == null)
        {
            SelectedSequenceId = string.Empty;
            Blocks.Clear();
            StatusMessage = "Battle Scenario를 선택하세요.";
            return;
        }

        if (string.IsNullOrWhiteSpace(SelectedSequenceId))
        {
            ActionSequenceAsset sequence = GetSelectedSequence();
            if (sequence == null && Scenario.Sequences != null && Scenario.Sequences.Count > 0 && Scenario.Sequences[0] != null)
            {
                SelectedSequenceId = Scenario.Sequences[0].SequenceId;
            }
        }

        LoadSelectedSequence();
    }

    private void LoadSelectedSequence()
    {
        Blocks.Clear();
        ActionSequenceAsset sequence = GetSelectedSequence();
        if (Scenario == null || sequence == null)
        {
            StatusMessage = Scenario == null
                ? "Battle Scenario를 선택하세요."
                : "시퀀스를 선택하세요.";
            return;
        }

        if (sequence.Actions != null)
        {
            for (int i = 0; i < sequence.Actions.Count; i++)
            {
                ScenarioActionBlockDraft draft = ScenarioActionBlockDraft.FromActionData(sequence.Actions[i], this, Blocks);
                Blocks.Add(draft);
            }
        }

        RefreshAllBlockCatalogViews();
        StatusMessage = "선택한 시퀀스를 Odin 블록 편집 뷰로 불러왔습니다.";
    }

    private ActionSequenceAsset GetSelectedSequence()
    {
        if (Scenario == null || Scenario.Sequences == null)
        {
            return null;
        }

        string normalized = Normalize(SelectedSequenceId);
        for (int i = 0; i < Scenario.Sequences.Count; i++)
        {
            ActionSequenceAsset sequence = Scenario.Sequences[i];
            if (sequence != null && Normalize(sequence.SequenceId) == normalized)
            {
                return sequence;
            }
        }

        return null;
    }

    private bool HasLoadedSequence()
    {
        return Scenario != null && GetSelectedSequence() != null;
    }

    private IEnumerable<string> GetSequenceOptions()
    {
        var options = new List<string>();
        if (Scenario == null || Scenario.Sequences == null)
        {
            return options;
        }

        for (int i = 0; i < Scenario.Sequences.Count; i++)
        {
            ActionSequenceAsset sequence = Scenario.Sequences[i];
            if (sequence == null || string.IsNullOrWhiteSpace(sequence.SequenceId))
            {
                continue;
            }

            options.Add(sequence.SequenceId.Trim());
        }

        return options;
    }

    private string CreateDefaultActionId()
    {
        ActionCatalogAsset effectiveCatalog = BuildEffectiveCatalog();
        if (effectiveCatalog != null && effectiveCatalog.Entries != null)
        {
            for (int i = 0; i < effectiveCatalog.Entries.Count; i++)
            {
                ActionCatalogEntry entry = effectiveCatalog.Entries[i];
                if (entry != null && !entry.Disabled && !string.IsNullOrWhiteSpace(entry.ActionId))
                {
                    return entry.ActionId.Trim();
                }
            }
        }

        return FlowWaitActionAdapter.Id;
    }

    private ActionCatalogAsset BuildEffectiveCatalog()
    {
        EnsureBuiltinCatalog();
        ActionCatalogAsset preferredCatalog = GetPreferredCatalogAsset();
        if (preferredCatalog == null)
        {
            return _builtinCatalog;
        }

        if (_mergedCatalogCache != null)
        {
            DestroyImmediate(_mergedCatalogCache);
            _mergedCatalogCache = null;
        }

        _mergedCatalogCache = ScriptableObject.CreateInstance<ActionCatalogAsset>();
        _mergedCatalogCache.CatalogId = string.IsNullOrWhiteSpace(preferredCatalog.CatalogId) ? "merged.editor" : preferredCatalog.CatalogId.Trim() + ".merged";
        _mergedCatalogCache.hideFlags = HideFlags.HideAndDontSave;
        CopyEntries(preferredCatalog, _mergedCatalogCache.Entries);
        CopyEntries(_builtinCatalog, _mergedCatalogCache.Entries, replaceExisting: false);
        return _mergedCatalogCache;
    }

    private ActionCatalogAsset GetPreferredCatalogAsset()
    {
        if (Catalog != null)
        {
            return Catalog;
        }

        return FindAutoCatalogAsset();
    }

    private ActionCatalogAsset FindAutoCatalogAsset()
    {
        string[] catalogGuids = AssetDatabase.FindAssets("t:ActionCatalogAsset");
        if (catalogGuids == null || catalogGuids.Length == 0)
        {
            return null;
        }

        ActionCatalogAsset first = null;
        string normalizedScenarioId = Normalize(Scenario != null ? Scenario.ScenarioId : string.Empty);
        string normalizedSourcePath = Normalize(Scenario != null && Scenario.Source != null ? Scenario.Source.SourcePath : string.Empty);
        for (int i = 0; i < catalogGuids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(catalogGuids[i]);
            ActionCatalogAsset asset = AssetDatabase.LoadAssetAtPath<ActionCatalogAsset>(path);
            if (asset == null)
            {
                continue;
            }

            if (first == null)
            {
                first = asset;
            }

            string catalogId = Normalize(asset.CatalogId);
            string assetName = Normalize(asset.name);
            if (!string.IsNullOrEmpty(normalizedScenarioId)
                && (catalogId == normalizedScenarioId
                    || normalizedScenarioId.Contains(catalogId)
                    || assetName.Contains(normalizedScenarioId)))
            {
                return asset;
            }

            if (!string.IsNullOrEmpty(normalizedSourcePath)
                && !string.IsNullOrEmpty(catalogId)
                && normalizedSourcePath.Contains(catalogId))
            {
                return asset;
            }
        }

        return catalogGuids.Length == 1 ? first : null;
    }

    private string BuildCatalogStatus()
    {
        if (Catalog != null)
        {
            return "명시적 ActionCatalogAsset 사용 중: " + Normalize(Catalog.CatalogId);
        }

        ActionCatalogAsset autoCatalog = FindAutoCatalogAsset();
        if (autoCatalog != null)
        {
            return "프로젝트 ActionCatalogAsset 자동 감지 사용 중: " + Normalize(autoCatalog.CatalogId) + " / built-in catalog는 fallback입니다.";
        }

        return "실제 ActionCatalogAsset을 찾지 못했습니다. built-in catalog fallback만 사용 중입니다.";
    }

    private void EnsureBuiltinCatalog()
    {
        if (_builtinCatalog != null)
        {
            return;
        }

        _builtinCatalog = ScriptableObject.CreateInstance<ActionCatalogAsset>();
        _builtinCatalog.hideFlags = HideFlags.HideAndDontSave;
        _builtinCatalog.CatalogId = "builtin.sequence.editor";
        _builtinCatalog.Entries.Add(CreateEntry(
            FlowWaitActionAdapter.Id,
            "flow",
            "기다리기",
            nameof(FlowWaitActionAdapter),
            "flow.wait:\n  duration: 0.5",
            CreateParameter("duration", "float", "대기 시간", "초 단위 대기 시간", false, "0")));
        _builtinCatalog.Entries.Add(CreateEntry(
            DialogueWaitActionAdapter.Id,
            "dialogue",
            "대사 표시 후 대기",
            nameof(DialogueWaitActionAdapter),
            "dialogue.wait:\n  id: zev.phase2_intro",
            CreateParameter("id", "string", "대사 ID", "Dialogue ID", true, string.Empty)));
        _builtinCatalog.Entries.Add(CreateEntry(
            BgmCrossfadeActionAdapter.Id,
            "audio",
            "BGM 크로스페이드",
            nameof(BgmCrossfadeActionAdapter),
            "bgm.crossfade:\n  clip: zev_phase2\n  duration: 0.8",
            CreateParameter("clip", "string", "오디오 ID", "BGM clip id", true, string.Empty),
            CreateParameter("duration", "float", "전환 시간", "초 단위 전환 시간", false, "0")));
        _builtinCatalog.Entries.Add(CreateEntry(
            ScreenFadeActionAdapter.Id,
            "screen",
            "화면 페이드",
            nameof(ScreenFadeActionAdapter),
            "screen.fade:\n  mode: out\n  color: black\n  duration: 0.4",
            CreateParameter("mode", "string", "모드", "in / out / reveal / cover", true, "out"),
            CreateParameter("color", "string", "색상", "black / white / html", false, "black"),
            CreateParameter("duration", "float", "시간", "초 단위 페이드 시간", false, "0")));
        _builtinCatalog.Entries.Add(CreateEntry(
            BattleCameraFocusActionAdapter.Id,
            "camera",
            "전투 카메라 포커스",
            nameof(BattleCameraFocusActionAdapter),
            "battle.camera.focus:\n  subject: zev\n  zoom: 3.2\n  duration: 0.2\n  style: dynamic",
            CreateParameter("subject", "actorId", "대상 ActorKey", "PartyIds/EnemyIds 기준 subject", true, string.Empty),
            CreateParameter("zoom", "float", "줌", "Orthographic size", false, "3.2"),
            CreateParameter("duration", "float", "시간", "포커스 시간", false, "0.2"),
            CreateParameter("style", "string", "연출 스타일", "static / dynamic / gameplay_safe", false, "dynamic")));
        _builtinCatalog.Entries.Add(CreateEntry(
            BattleCameraResetActionAdapter.Id,
            "camera",
            "전투 카메라 리셋",
            nameof(BattleCameraResetActionAdapter),
            "battle.camera.reset:\n  duration: 0.35\n  style: gameplay_safe",
            CreateParameter("duration", "float", "시간", "리셋 시간", false, "0.35"),
            CreateParameter("style", "string", "연출 스타일", "static / dynamic / gameplay_safe", false, "gameplay_safe")));
        _builtinCatalog.Entries.Add(CreateEntry(
            BattleCameraShakeActionAdapter.Id,
            "camera",
            "전투 카메라 흔들림",
            nameof(BattleCameraShakeActionAdapter),
            "battle.camera.shake:\n  direction: right\n  intensity: 0.55\n  duration: 0.12\n  safety: gameplay_safe",
            CreateParameter("direction", "string", "방향", "left / right / up / down", true, "right"),
            CreateParameter("intensity", "float", "세기", "Cinemachine Impulse 세기", false, "0.5"),
            CreateParameter("duration", "float", "시간", "Impulse 지속 시간", false, "0.12"),
            CreateParameter("safety", "string", "안전 모드", "gameplay_safe / cinematic", false, "gameplay_safe")));
        _builtinCatalog.Entries.Add(CreateEntry(
            BattleActorPoseActionAdapter.Id,
            "battle",
            "전투 액터 포즈",
            nameof(BattleActorPoseActionAdapter),
            "battle.actor.pose:\n  actor: zev\n  pose: attack\n  duration: 0.25",
            CreateParameter("actor", "actorId", "ActorKey", "PartyIds/EnemyIds 기준 actor", true, string.Empty),
            CreateParameter("pose", "string", "포즈", "idle / move / attack / hurt", true, "idle"),
            CreateParameter("duration", "float", "시간", "포즈 유지 시간", false, "0.25"),
            CreateParameter("impact", "float", "임팩트", "카메라 임팩트 세기", false, "0")));
        _builtinCatalog.Entries.Add(CreateEntry(
            BattleActorFlipActionAdapter.Id,
            "battle",
            "전투 액터 좌우 반전",
            nameof(BattleActorFlipActionAdapter),
            "battle.actor.flip:\n  actor: zev\n  mode: invert",
            CreateParameter("actor", "actorId", "ActorKey", "PartyIds/EnemyIds 기준 actor", true, string.Empty),
            CreateParameter("mode", "string", "모드", "default / invert / toggle", false, "default")));
        _builtinCatalog.Entries.Add(CreateEntry(
            BattleActorMoveActionAdapter.Id,
            "battle",
            "전투 액터 이동",
            nameof(BattleActorMoveActionAdapter),
            "battle.actor.move_to:\n  actor: zev\n  anchor: center\n  duration: 0.25",
            CreateParameter("actor", "actorId", "ActorKey", "PartyIds/EnemyIds 기준 actor", true, string.Empty),
            CreateParameter("anchor", "string", "앵커", "current / center / player_slot / enemy_slot", false, "current"),
            CreateParameter("x", "float", "추가 X", "앵커에서 추가 X 오프셋", false, "0"),
            CreateParameter("y", "float", "추가 Y", "앵커에서 추가 Y 오프셋", false, "0"),
            CreateParameter("duration", "float", "시간", "이동 시간", false, "0.25"),
            CreateParameter("pose", "string", "포즈", "move / idle", false, "move"),
            CreateParameter("impact", "float", "임팩트", "카메라 임팩트 세기", false, "0")));
        _builtinCatalog.Entries.Add(CreateEntry(
            BattleActorDropInActionAdapter.Id,
            "battle",
            "전투 액터 드롭인",
            nameof(BattleActorDropInActionAdapter),
            "battle.actor.drop_in:\n  actor: zev\n  height: 3.5",
            CreateParameter("actor", "actorId", "ActorKey", "PartyIds/EnemyIds 기준 actor", true, string.Empty),
            CreateParameter("height", "float", "낙하 높이", "드롭인 높이", false, "3.5"),
            CreateParameter("hang", "float", "정지 시간", "공중 정지 시간", false, "0.18"),
            CreateParameter("fall", "float", "낙하 시간", "낙하 시간", false, "0.22"),
            CreateParameter("settle", "float", "착지 시간", "착지 후 정리 시간", false, "0.12"),
            CreateParameter("impact", "float", "임팩트", "카메라 임팩트 세기", false, "1.1")));
        _builtinCatalog.Entries.Add(CreateEntry(
            BattleActorFakeAttackActionAdapter.Id,
            "battle",
            "전투 액터 페이크 어택",
            nameof(BattleActorFakeAttackActionAdapter),
            "battle.actor.fake_attack:\n  actor: zev\n  target: player",
            CreateParameter("actor", "actorId", "공격자 ActorKey", "PartyIds/EnemyIds 기준 actor", true, string.Empty),
            CreateParameter("target", "actorId", "대상 ActorKey", "PartyIds/EnemyIds 기준 target", true, string.Empty),
            CreateParameter("targetPose", "string", "타겟 포즈", "hurt / idle", false, "hurt"),
            CreateParameter("approach", "float", "접근 거리", "타겟 앞 거리", false, "0.85"),
            CreateParameter("lunge", "float", "찌르기 시간", "전진 시간", false, "0.12"),
            CreateParameter("hold", "float", "정지 시간", "타격 유지 시간", false, "0.05"),
            CreateParameter("recover", "float", "복귀 시간", "복귀 시간", false, "0.18"),
            CreateParameter("impact", "float", "임팩트", "카메라 임팩트 세기", false, "0.6")));
        _builtinCatalog.Entries.Add(CreateEntry(
            BattleActorReturnSlotsActionAdapter.Id,
            "battle",
            "전투 액터 슬롯 복귀",
            nameof(BattleActorReturnSlotsActionAdapter),
            "battle.actor.return_slots:\n  duration: 0.28",
            CreateParameter("duration", "float", "시간", "슬롯 복귀 시간", false, "0.28")));
        _builtinCatalog.Entries.Add(CreateEntry(
            ModuleSwitchActionAdapter.Id,
            "module",
            "전투 모듈 전환",
            nameof(ModuleSwitchActionAdapter),
            "module.switch:\n  to: aim_shooter",
            CreateParameter("to", "string", "목표 모듈", "등록된 module id", true, string.Empty)));
        _builtinCatalog.Entries.Add(CreateEntry(
            ModuleStartActionAdapter.Id,
            "module",
            "전투 모듈 시작",
            nameof(ModuleStartActionAdapter),
            "module.start:\n  module: aim_shooter",
            CreateParameter("module", "string", "모듈", "등록된 module id", true, string.Empty)));
        _builtinCatalog.Entries.Add(CreateEntry(
            BattleSkillTimelineActionAdapter.Id,
            "battle",
            "기존 스킬 타임라인 실행",
            nameof(BattleSkillTimelineActionAdapter),
            "battle.skill.timeline:\n  skill: zev_crosscut\n  actor: zev\n  targets: [player]",
            CreateParameter("skill", "string", "스킬 ID", "SkillData.SkillID", true, string.Empty),
            CreateParameter("actor", "actorId", "ActorKey", "PartyIds/EnemyIds 기준 actor", true, string.Empty),
            CreateParameter("targets", "string[]", "타겟들", "쉼표로 구분한 subject id 목록", false, string.Empty)));
        _builtinCatalog.Entries.Add(CreateEntry(
            BattleParticipantDamageActionAdapter.Id,
            "battle",
            "전투 참가자 피해",
            nameof(BattleParticipantDamageActionAdapter),
            "battle.participant.damage:\n  subject: zev\n  amount: 25",
            CreateParameter("subject", "actorId", "대상 ActorKey", "PartyIds/EnemyIds 기준 subject", true, string.Empty),
            CreateParameter("amount", "int", "수치", "피해량", true, "1")));
        _builtinCatalog.Entries.Add(CreateEntry(
            BattleParticipantHealHpActionAdapter.Id,
            "battle",
            "전투 참가자 HP 회복",
            nameof(BattleParticipantHealHpActionAdapter),
            "battle.participant.heal_hp:\n  subject: player\n  amount: 20",
            CreateParameter("subject", "actorId", "대상 ActorKey", "PartyIds/EnemyIds 기준 subject", true, string.Empty),
            CreateParameter("amount", "int", "수치", "회복량", true, "1")));
        _builtinCatalog.Entries.Add(CreateEntry(
            BattleParticipantHealMpActionAdapter.Id,
            "battle",
            "전투 참가자 MP 회복",
            nameof(BattleParticipantHealMpActionAdapter),
            "battle.participant.heal_mp:\n  subject: player\n  amount: 10",
            CreateParameter("subject", "actorId", "대상 ActorKey", "PartyIds/EnemyIds 기준 subject", true, string.Empty),
            CreateParameter("amount", "int", "수치", "회복량", true, "1")));
        _builtinCatalog.Entries.Add(CreateEntry(
            BattleParticipantConsumeMpActionAdapter.Id,
            "battle",
            "전투 참가자 MP 소비",
            nameof(BattleParticipantConsumeMpActionAdapter),
            "battle.participant.consume_mp:\n  subject: player\n  amount: 5",
            CreateParameter("subject", "actorId", "대상 ActorKey", "PartyIds/EnemyIds 기준 subject", true, string.Empty),
            CreateParameter("amount", "int", "수치", "소비량", true, "1")));
        _builtinCatalog.Entries.Add(CreateEntry(
            BattleFlagSetActionAdapter.Id,
            "battle",
            "전투 플래그 설정",
            nameof(BattleFlagSetActionAdapter),
            "battle.flag.set:\n  flag: phase.two\n  value: entered",
            CreateParameter("flag", "string", "플래그", "전투 세션 플래그 ID", true, string.Empty),
            CreateParameter("value", "string", "값", "문자열 값", false, "true")));
        _builtinCatalog.Entries.Add(CreateEntry(
            BattleFlagClearActionAdapter.Id,
            "battle",
            "전투 플래그 해제",
            nameof(BattleFlagClearActionAdapter),
            "battle.flag.clear:\n  flag: phase.two",
            CreateParameter("flag", "string", "플래그", "전투 세션 플래그 ID", true, string.Empty)));
        _builtinCatalog.Entries.Add(CreateEntry(
            TimelinePlayActionAdapter.Id,
            "timeline",
            "타임라인 컷신 재생",
            nameof(TimelinePlayActionAdapter),
            "timeline.play:\n  cutsceneId: zev_intro_clash\n  waitForComplete: true\n  lockInput: true\n  restoreCamera: true\n  skipIfMissing: false",
            CreateParameter("cutsceneId", "string", "컷신 ID", "TimelineCutsceneCatalog의 cutsceneId", true, string.Empty),
            CreateParameter("waitForComplete", "bool", "완료 대기", "재생 종료까지 시퀀스를 대기시킵니다.", false, "true"),
            CreateParameter("lockInput", "bool", "입력 잠금", "재생 중 GameState를 Cutscene으로 잠급니다.", false, "true"),
            CreateParameter("restoreCamera", "bool", "카메라 복구", "재생 후 CameraController를 리셋합니다.", false, "true"),
            CreateParameter("skipIfMissing", "bool", "누락 시 스킵", "컷신/바인딩 누락 시 실패 대신 경고 후 스킵합니다.", false, "false")));
    }

    private static void CopyEntries(ActionCatalogAsset source, List<ActionCatalogEntry> destination, bool replaceExisting = false)
    {
        if (source == null || source.Entries == null)
        {
            return;
        }

        for (int i = 0; i < source.Entries.Count; i++)
        {
            ActionCatalogEntry entry = source.Entries[i];
            if (entry == null || string.IsNullOrWhiteSpace(entry.ActionId))
            {
                continue;
            }

            int existingIndex = FindEntryIndex(destination, entry.ActionId);
            ActionCatalogEntry clone = CloneEntry(entry);
            if (existingIndex >= 0)
            {
                if (replaceExisting)
                {
                    destination[existingIndex] = clone;
                }
            }
            else
            {
                destination.Add(clone);
            }
        }
    }

    private static void AddActionOptions(ValueDropdownList<string> options, ActionCatalogAsset catalog, bool replaceExisting = false)
    {
        if (catalog == null || catalog.Entries == null)
        {
            return;
        }

        for (int i = 0; i < catalog.Entries.Count; i++)
        {
            ActionCatalogEntry entry = catalog.Entries[i];
            if (entry == null || entry.Disabled || string.IsNullOrWhiteSpace(entry.ActionId))
            {
                continue;
            }

            string actionId = entry.ActionId.Trim();
            string displayName = string.IsNullOrWhiteSpace(entry.DisplayNameKo)
                ? actionId
                : entry.DisplayNameKo.Trim() + " (" + actionId + ")";

            int existingIndex = FindOptionIndex(options, actionId);
            if (existingIndex >= 0)
            {
                if (replaceExisting)
                {
                    options[existingIndex] = new ValueDropdownItem<string>(displayName, actionId);
                }

                continue;
            }

            options.Add(displayName, actionId);
        }
    }

    private static int FindOptionIndex(ValueDropdownList<string> options, string actionId)
    {
        for (int i = 0; i < options.Count; i++)
        {
            if (Normalize(options[i].Value) == Normalize(actionId))
            {
                return i;
            }
        }

        return -1;
    }

    private static int FindEntryIndex(List<ActionCatalogEntry> entries, string actionId)
    {
        string normalized = Normalize(actionId);
        for (int i = 0; i < entries.Count; i++)
        {
            ActionCatalogEntry entry = entries[i];
            if (entry != null && Normalize(entry.ActionId) == normalized)
            {
                return i;
            }
        }

        return -1;
    }

    private static ActionCatalogEntry CloneEntry(ActionCatalogEntry source)
    {
        return ActionCatalogContractCopy.Entry(source);
    }

    private static ActionCatalogEntry CreateEntry(
        string actionId,
        string category,
        string displayNameKo,
        string runtimeAdapterId,
        string exampleYaml,
        params ActionCatalogParameter[] parameters)
    {
        var entry = new ActionCatalogEntry
        {
            ActionId = actionId,
            Category = category,
            DisplayNameKo = displayNameKo,
            RuntimeAdapterId = runtimeAdapterId,
            ExampleYaml = exampleYaml,
            DescriptionKo = displayNameKo
        };

        if (parameters != null)
        {
            entry.Parameters.AddRange(parameters);
        }

        return entry;
    }

    private static ActionCatalogParameter CreateParameter(
        string name,
        string type,
        string displayNameKo,
        string descriptionKo,
        bool required,
        string defaultValue)
    {
        return new ActionCatalogParameter
        {
            Name = name,
            Type = type,
            DisplayNameKo = displayNameKo,
            DescriptionKo = descriptionKo,
            Required = required,
            DefaultValue = defaultValue
        };
    }

    private void AppendEditorOnlyValidation(
        ScenarioValidationResult result,
        List<ScenarioActionBlockDraft> blocks,
        string objectIdPrefix)
    {
        if (result == null || blocks == null)
        {
            return;
        }

        for (int i = 0; i < blocks.Count; i++)
        {
            AppendEditorOnlyValidation(result, blocks[i], objectIdPrefix + ".actions[" + i + "]");
        }
    }

    private void AppendEditorOnlyValidation(
        ScenarioValidationResult result,
        ScenarioActionBlockDraft block,
        string objectId)
    {
        if (block == null)
        {
            return;
        }

        string actionId = Normalize(block.ActionId);
        if (actionId == BattleSkillTimelineActionAdapter.Id)
        {
            ValidateActorKey(result, block, objectId, "actor", block.GetParameterString("actor"));
            ValidateSkillId(result, block.GetParameterString("skill"), objectId);
            List<string> targets = block.GetParameterStringList("targets");
            for (int i = 0; i < targets.Count; i++)
            {
                ValidateActorKey(result, block, objectId, "targets[" + i + "]", targets[i]);
            }
        }
        else if (actionId == BattleCameraFocusActionAdapter.Id)
        {
            ValidateActorKey(result, block, objectId, "subject", block.GetParameterString("subject"));
        }
        else if (actionId == BattleActorPoseActionAdapter.Id
            || actionId == BattleActorFlipActionAdapter.Id
            || actionId == BattleActorMoveActionAdapter.Id
            || actionId == BattleActorDropInActionAdapter.Id)
        {
            ValidateActorKey(result, block, objectId, "actor", block.GetParameterString("actor"));
        }
        else if (actionId == BattleActorFakeAttackActionAdapter.Id)
        {
            ValidateActorKey(result, block, objectId, "actor", block.GetParameterString("actor"));
            ValidateActorKey(result, block, objectId, "target", block.GetParameterString("target"));
        }

        for (int i = 0; i < block.Children.Count; i++)
        {
            AppendEditorOnlyValidation(result, block.Children[i], objectId + ".actions[" + i + "]");
        }
    }

    private void ValidateActorKey(
        ScenarioValidationResult result,
        ScenarioActionBlockDraft block,
        string objectId,
        string parameterName,
        string actorKey)
    {
        string normalized = Normalize(actorKey);
        if (string.IsNullOrEmpty(normalized))
        {
            return;
        }

        if (!HasScenarioSubject(normalized))
        {
            result.AddError(
                "scenario.actor.unknown",
                "존재하지 않는 actorKey 입니다: " + parameterName + " = " + normalized,
                objectId);
        }
    }

    private bool HasScenarioSubject(string subjectId)
    {
        if (Scenario == null)
        {
            return false;
        }

        string normalized = Normalize(subjectId);
        if (normalized == "player")
        {
            return true;
        }

        if (HasString(Scenario.PartyIds, normalized) || HasString(Scenario.EnemyIds, normalized))
        {
            return true;
        }

        return false;
    }

    private void ValidateSkillId(ScenarioValidationResult result, string skillId, string objectId)
    {
        string normalized = Normalize(skillId);
        if (string.IsNullOrEmpty(normalized))
        {
            return;
        }

        string[] skillGuids = AssetDatabase.FindAssets("t:SkillData");
        for (int i = 0; i < skillGuids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(skillGuids[i]);
            SkillData skill = AssetDatabase.LoadAssetAtPath<SkillData>(path);
            if (skill != null && (Normalize(skill.SkillID) == normalized || Normalize(skill.SkillName) == normalized || Normalize(skill.name) == normalized))
            {
                return;
            }
        }

        result.AddError("scenario.skill.unknown", "존재하지 않는 skillId 입니다: " + normalized, objectId);
    }

    private static bool HasString(List<string> values, string target)
    {
        if (values == null)
        {
            return false;
        }

        for (int i = 0; i < values.Count; i++)
        {
            if (Normalize(values[i]) == target)
            {
                return true;
            }
        }

        return false;
    }

    private ActionSequenceAsset BuildTemporarySequence()
    {
        ActionSequenceAsset sequence = ScriptableObject.CreateInstance<ActionSequenceAsset>();
        sequence.SequenceId = Normalize(SelectedSequenceId);
        for (int i = 0; i < Blocks.Count; i++)
        {
            sequence.Actions.Add(Blocks[i].ToActionData());
        }

        return sequence;
    }

    private BattleScenarioData BuildTemporaryScenario(ActionSequenceAsset sequence)
    {
        BattleScenarioData tempScenario = ScriptableObject.CreateInstance<BattleScenarioData>();
        if (Scenario != null)
        {
            tempScenario.ScenarioId = Scenario.ScenarioId;
            tempScenario.TitleKo = Scenario.TitleKo;
            tempScenario.PrimaryMode = Scenario.PrimaryMode;
            tempScenario.OpeningModule = Scenario.OpeningModule;
            tempScenario.MemoryKey = Scenario.MemoryKey;
            tempScenario.TimelineCutsceneCatalog = Scenario.TimelineCutsceneCatalog;
            tempScenario.PartyIds.AddRange(Scenario.PartyIds);
            tempScenario.EnemyIds.AddRange(Scenario.EnemyIds);
            tempScenario.Dialogues.AddRange(Scenario.Dialogues);
            tempScenario.AudioClips.AddRange(Scenario.AudioClips);
        }

        if (sequence != null)
        {
            tempScenario.Sequences.Add(sequence);
        }

        return tempScenario;
    }

    private bool TryApplyBlocksToSelectedSequence(out ActionSequenceAsset sequence)
    {
        sequence = GetSelectedSequence();
        if (sequence == null)
        {
            StatusMessage = "적용할 시퀀스를 찾지 못했습니다.";
            return false;
        }

        Undo.RecordObject(sequence, "시퀀스 런타임 반영");
        if (Scenario != null)
        {
            Undo.RecordObject(Scenario, "시퀀스 런타임 반영");
        }

        sequence.Actions.Clear();
        for (int i = 0; i < Blocks.Count; i++)
        {
            sequence.Actions.Add(Blocks[i].ToActionData());
        }

        EditorUtility.SetDirty(sequence);
        if (Scenario != null)
        {
            EditorUtility.SetDirty(Scenario);
        }

        return true;
    }

    private static string BuildValidationSummary(ScenarioValidationResult result)
    {
        if (result == null || result.Messages.Count == 0)
        {
            return "검증 메시지가 없습니다.";
        }

        List<string> lines = new List<string>();
        int count = Mathf.Min(8, result.Messages.Count);
        for (int i = 0; i < count; i++)
        {
            ScenarioValidationMessage message = result.Messages[i];
            lines.Add("- [" + message.Severity + "] " + message.Code + " / " + message.Message);
        }

        if (result.Messages.Count > count)
        {
            lines.Add("- ... 추가 메시지 " + (result.Messages.Count - count) + "개");
        }

        return string.Join("\n", lines.ToArray());
    }

    internal static string Normalize(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
    }
}

[Serializable]
public sealed class ScenarioActionBlockDraft
{
    [NonSerialized] internal List<ScenarioActionBlockDraft> OwnerList;

    [NonSerialized] private ScenarioSequenceOdinEditorWindow _owner;

    [HideInInspector]
    public string BlockId = string.Empty;

    [ShowInInspector]
    [ReadOnly]
    [PropertyOrder(-30)]
    [GUIColor(nameof(GetHeaderColor))]
    [LabelText("구분")]
    public string BlockHeader => BuildHeader();

    [LabelText("Enabled")]
    public bool Enabled = true;

    [LabelText("Designer Label")]
    public string DesignerLabel = string.Empty;

    [TextArea(1, 4)]
    [LabelText("Note")]
    public string Note = string.Empty;

    [ValueDropdown(nameof(GetActionOptions))]
    [OnValueChanged(nameof(HandleActionIdChanged))]
    [LabelText("Action Id")]
    public string ActionId = string.Empty;

    [ShowIf(nameof(HasKnownAction))]
    [ListDrawerSettings(ShowFoldout = true, DefaultExpandedState = true, DraggableItems = false, ShowIndexLabels = true)]
    [LabelText("Parameters")]
    public List<ScenarioActionParameterDraft> Parameters = new List<ScenarioActionParameterDraft>();

    [ShowIf(nameof(HasUnknownAction))]
    [TextArea(3, 10)]
    [LabelText("Raw JSON Fallback")]
    public string RawJson = "{}";

    [ListDrawerSettings(ShowFoldout = true, DefaultExpandedState = false, DraggableItems = false, ShowIndexLabels = true, ListElementLabelName = nameof(BlockHeader))]
    [LabelText("Children")]
    public List<ScenarioActionBlockDraft> Children = new List<ScenarioActionBlockDraft>();

    [ButtonGroup("조작")]
    private void AddChild()
    {
        if (_owner == null)
        {
            return;
        }

        Children.Add(_owner.CreateDraft(_owner.BuildActionOptions().Count > 0 ? _owner.BuildActionOptions()[0].Value : FlowWaitActionAdapter.Id, Children));
        _owner.StatusMessage = "자식 블록을 추가했습니다. Runtime Asset Only 적용 또는 Source 저장/재반영을 실행하세요. YAML에는 아직 저장되지 않았습니다.";
    }

    [ButtonGroup("조작")]
    private void Duplicate()
    {
        _owner?.DuplicateBlock(this);
    }

    [ButtonGroup("조작")]
    private void MoveUp()
    {
        _owner?.MoveBlock(this, -1);
    }

    [ButtonGroup("조작")]
    private void MoveDown()
    {
        _owner?.MoveBlock(this, 1);
    }

    [ButtonGroup("조작")]
    private void Remove()
    {
        _owner?.RemoveBlock(this);
    }

    internal static ScenarioActionBlockDraft FromActionData(
        ScenarioActionData action,
        ScenarioSequenceOdinEditorWindow owner,
        List<ScenarioActionBlockDraft> ownerList)
    {
        var draft = new ScenarioActionBlockDraft();
        draft.BlockId = action != null && !string.IsNullOrWhiteSpace(action.BlockId)
            ? action.BlockId.Trim()
            : ScenarioBlockIdentity.Create();
        draft.Enabled = action == null || !action.Disabled;
        draft.DesignerLabel = action != null ? action.DesignerLabel : string.Empty;
        draft.Note = action != null ? action.Note : string.Empty;
        draft.ActionId = action != null ? action.ActionId : string.Empty;
        draft.RawJson = action != null ? action.ParametersJson : "{}";
        draft.OwnerList = ownerList;
        draft.RefreshCatalogViewRecursive(owner, ownerList);

        if (action != null && action.Children != null)
        {
            for (int i = 0; i < action.Children.Count; i++)
            {
                draft.Children.Add(FromActionData(action.Children[i], owner, draft.Children));
            }
        }

        draft.RefreshCatalogViewRecursive(owner, ownerList);
        return draft;
    }

    internal void RefreshCatalogViewRecursive(ScenarioSequenceOdinEditorWindow owner, List<ScenarioActionBlockDraft> ownerList)
    {
        _owner = owner;
        OwnerList = ownerList;
        RefreshParameterDrafts();
        for (int i = 0; i < Children.Count; i++)
        {
            Children[i].RefreshCatalogViewRecursive(owner, Children);
        }
    }

    internal ScenarioActionData ToActionData()
    {
        var action = new ScenarioActionData
        {
            BlockId = string.IsNullOrWhiteSpace(BlockId) ? ScenarioBlockIdentity.Create() : BlockId.Trim(),
            DesignerLabel = DesignerLabel ?? string.Empty,
            ActionId = ScenarioSequenceOdinEditorWindow.Normalize(ActionId),
            ParametersJson = BuildParametersJson(),
            Note = Note ?? string.Empty,
            Disabled = !Enabled
        };

        for (int i = 0; i < Children.Count; i++)
        {
            action.Children.Add(Children[i].ToActionData());
        }

        return action;
    }

    internal ScenarioActionBlockDraft CloneDeep()
    {
        var clone = new ScenarioActionBlockDraft
        {
            BlockId = ScenarioBlockIdentity.Create(),
            Enabled = Enabled,
            DesignerLabel = DesignerLabel,
            Note = Note,
            ActionId = ActionId,
            RawJson = RawJson
        };

        for (int i = 0; i < Parameters.Count; i++)
        {
            clone.Parameters.Add(Parameters[i].Clone());
        }

        for (int i = 0; i < Children.Count; i++)
        {
            clone.Children.Add(Children[i].CloneDeep());
        }

        return clone;
    }

    internal string GetParameterString(string parameterName)
    {
        for (int i = 0; i < Parameters.Count; i++)
        {
            if (ScenarioSequenceOdinEditorWindow.Normalize(Parameters[i].Name) == ScenarioSequenceOdinEditorWindow.Normalize(parameterName))
            {
                return Parameters[i].GetStringValue();
            }
        }

        try
        {
            JObject json = string.IsNullOrWhiteSpace(RawJson) ? new JObject() : JObject.Parse(RawJson);
            JToken token;
            return json.TryGetValue(parameterName, out token) && token != null ? token.ToString(Formatting.None) : string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }

    internal List<string> GetParameterStringList(string parameterName)
    {
        for (int i = 0; i < Parameters.Count; i++)
        {
            if (ScenarioSequenceOdinEditorWindow.Normalize(Parameters[i].Name) == ScenarioSequenceOdinEditorWindow.Normalize(parameterName))
            {
                return Parameters[i].GetStringListValue();
            }
        }

        var values = new List<string>();
        string raw = GetParameterString(parameterName);
        if (!string.IsNullOrWhiteSpace(raw))
        {
            string[] parts = raw.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < parts.Length; i++)
            {
                values.Add(parts[i].Trim());
            }
        }

        return values;
    }

    private void HandleActionIdChanged()
    {
        RefreshParameterDrafts();
    }

    private void RefreshParameterDrafts()
    {
        ActionCatalogEntry entry = _owner != null ? _owner.FindCatalogEntry(ActionId) : null;
        if (entry == null)
        {
            if (string.IsNullOrWhiteSpace(RawJson))
            {
                RawJson = "{}";
            }

            return;
        }

        JObject current = ParseOrNew(RawJson);
        var newParameters = new List<ScenarioActionParameterDraft>();
        for (int i = 0; i < entry.Parameters.Count; i++)
        {
            ActionCatalogParameter parameter = entry.Parameters[i];
            if (parameter == null || string.IsNullOrWhiteSpace(parameter.Name))
            {
                continue;
            }

            JToken token;
            if (!current.TryGetValue(parameter.Name.Trim(), out token) || token == null)
            {
                token = CreateDefaultToken(parameter.DefaultValue, parameter.Type);
            }

            newParameters.Add(ScenarioActionParameterDraft.FromCatalog(parameter, token));
        }

        foreach (JProperty property in current.Properties())
        {
            if (ContainsParameter(newParameters, property.Name))
            {
                continue;
            }

            newParameters.Add(ScenarioActionParameterDraft.FromUnknown(property.Name, property.Value));
        }

        Parameters = newParameters;
    }

    private ValueDropdownList<string> GetActionOptions()
    {
        return _owner != null ? _owner.BuildActionOptions() : new ValueDropdownList<string>();
    }

    private bool HasKnownAction()
    {
        return _owner != null && _owner.FindCatalogEntry(ActionId) != null;
    }

    private bool HasUnknownAction()
    {
        return !HasKnownAction();
    }

    private string BuildParametersJson()
    {
        if (Parameters == null || Parameters.Count == 0)
        {
            try
            {
                return string.IsNullOrWhiteSpace(RawJson)
                    ? "{}"
                    : JObject.Parse(RawJson).ToString(Formatting.None);
            }
            catch
            {
                return "{}";
            }
        }

        var json = new JObject();
        for (int i = 0; i < Parameters.Count; i++)
        {
            Parameters[i].WriteTo(json);
        }

        return json.ToString(Formatting.None);
    }

    private string BuildHeader()
    {
        string label = string.IsNullOrWhiteSpace(DesignerLabel)
            ? ScenarioSequenceOdinEditorWindow.Normalize(ActionId)
            : DesignerLabel.Trim();
        string category = ResolveCategory();
        string icon = ResolveCategoryIcon(category);
        return (Enabled ? string.Empty : "[비활성] ") + icon + " " + category + " / " + label;
    }

    private Color GetHeaderColor()
    {
        string category = ResolveCategory();
        switch (category)
        {
            case "Dialogue":
                return new Color(0.52f, 0.83f, 1f);
            case "Battle":
                return new Color(1f, 0.55f, 0.55f);
            case "Timeline":
                return new Color(0.87f, 0.62f, 1f);
            case "Flow":
                return new Color(0.76f, 0.94f, 0.60f);
            case "Camera":
                return new Color(1f, 0.84f, 0.48f);
            default:
                return Color.white;
        }
    }

    private string ResolveCategory()
    {
        ActionCatalogEntry entry = _owner != null ? _owner.FindCatalogEntry(ActionId) : null;
        string category = entry != null ? ScenarioSequenceOdinEditorWindow.Normalize(entry.Category) : string.Empty;
        if (string.IsNullOrEmpty(category))
        {
            category = ScenarioSequenceOdinEditorWindow.Normalize(ActionId).Split('.')[0];
        }

        switch (category.ToLowerInvariant())
        {
            case "dialogue": return "Dialogue";
            case "battle": return "Battle";
            case "timeline": return "Timeline";
            case "flow": return "Flow";
            case "camera": return "Camera";
            default: return string.IsNullOrEmpty(category) ? "Unknown" : category;
        }
    }

    private static string ResolveCategoryIcon(string category)
    {
        switch (category)
        {
            case "Dialogue": return "💬";
            case "Battle": return "⚔";
            case "Timeline": return "🎬";
            case "Flow": return "🔀";
            case "Camera": return "🎥";
            default: return "•";
        }
    }

    private static bool ContainsParameter(List<ScenarioActionParameterDraft> parameters, string parameterName)
    {
        for (int i = 0; i < parameters.Count; i++)
        {
            if (ScenarioSequenceOdinEditorWindow.Normalize(parameters[i].Name) == ScenarioSequenceOdinEditorWindow.Normalize(parameterName))
            {
                return true;
            }
        }

        return false;
    }

    private static JObject ParseOrNew(string rawJson)
    {
        try
        {
            return string.IsNullOrWhiteSpace(rawJson) ? new JObject() : JObject.Parse(rawJson);
        }
        catch
        {
            return new JObject();
        }
    }

    private static JToken CreateDefaultToken(string defaultValue, string typeHint)
    {
        string normalizedType = ScenarioSequenceOdinEditorWindow.Normalize(typeHint).ToLowerInvariant();
        string normalizedValue = defaultValue ?? string.Empty;
        if (string.IsNullOrWhiteSpace(normalizedValue))
        {
            return null;
        }

        if (normalizedType.Contains("bool"))
        {
            bool boolValue;
            return bool.TryParse(normalizedValue, out boolValue) ? (JToken)new JValue(boolValue) : new JValue(false);
        }

        if (normalizedType.Contains("int"))
        {
            int intValue;
            return int.TryParse(normalizedValue, out intValue) ? (JToken)new JValue(intValue) : new JValue(0);
        }

        if (normalizedType.Contains("float") || normalizedType.Contains("number"))
        {
            float floatValue;
            return float.TryParse(normalizedValue, out floatValue) ? (JToken)new JValue(floatValue) : new JValue(0f);
        }

        if (normalizedType.Contains("[]"))
        {
            var array = new JArray();
            string[] parts = normalizedValue.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < parts.Length; i++)
            {
                array.Add(parts[i].Trim());
            }

            return array;
        }

        return new JValue(normalizedValue);
    }
}

[Serializable]
public sealed class ScenarioActionParameterDraft
{
    [HideInInspector] public string Name;
    [HideInInspector] public string TypeHint;
    [HideInInspector] public string DisplayNameKo;
    [HideInInspector] public string DescriptionKo;
    [HideInInspector] public bool Required;

    [LabelText("문자열")]
    [ShowIf(nameof(IsStringLike))]
    public string StringValue = string.Empty;

    [LabelText("정수")]
    [ShowIf(nameof(IsInt))]
    public int IntValue;

    [LabelText("숫자")]
    [ShowIf(nameof(IsFloat))]
    public float FloatValue;

    [LabelText("체크")]
    [ShowIf(nameof(IsBool))]
    public bool BoolValue;

    [LabelText("쉼표 구분 목록")]
    [ShowIf(nameof(IsArray))]
    public string CsvValue = string.Empty;

    [ShowInInspector]
    [ReadOnly]
    [PropertyOrder(-20)]
    [LabelText("파라미터")]
    public string Header => BuildHeader();

    [ShowInInspector]
    [ReadOnly]
    [PropertyOrder(-19)]
    [LabelText("설명")]
    [ShowIf(nameof(HasDescription))]
    public string Description => DescriptionKo;

    internal static ScenarioActionParameterDraft FromCatalog(ActionCatalogParameter parameter, JToken token)
    {
        var draft = new ScenarioActionParameterDraft
        {
            Name = parameter.Name != null ? parameter.Name.Trim() : string.Empty,
            TypeHint = parameter.Type != null ? parameter.Type.Trim() : string.Empty,
            DisplayNameKo = parameter.DisplayNameKo ?? string.Empty,
            DescriptionKo = parameter.DescriptionKo ?? string.Empty,
            Required = parameter.Required
        };
        draft.LoadToken(token);
        return draft;
    }

    internal static ScenarioActionParameterDraft FromUnknown(string name, JToken token)
    {
        var draft = new ScenarioActionParameterDraft
        {
            Name = name != null ? name.Trim() : string.Empty,
            TypeHint = InferType(token),
            DisplayNameKo = string.Empty,
            DescriptionKo = "카탈로그 정의가 없어 raw JSON 값에서 추론한 파라미터입니다.",
            Required = false
        };
        draft.LoadToken(token);
        return draft;
    }

    internal ScenarioActionParameterDraft Clone()
    {
        return new ScenarioActionParameterDraft
        {
            Name = Name,
            TypeHint = TypeHint,
            DisplayNameKo = DisplayNameKo,
            DescriptionKo = DescriptionKo,
            Required = Required,
            StringValue = StringValue,
            IntValue = IntValue,
            FloatValue = FloatValue,
            BoolValue = BoolValue,
            CsvValue = CsvValue
        };
    }

    internal void WriteTo(JObject root)
    {
        if (root == null || string.IsNullOrWhiteSpace(Name))
        {
            return;
        }

        string normalizedType = ScenarioSequenceOdinEditorWindow.Normalize(TypeHint).ToLowerInvariant();
        if (IsArray())
        {
            var array = new JArray();
            string[] parts = CsvValue.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < parts.Length; i++)
            {
                array.Add(parts[i].Trim());
            }

            root[Name] = array;
            return;
        }

        if (IsBool())
        {
            root[Name] = BoolValue;
            return;
        }

        if (IsInt())
        {
            root[Name] = IntValue;
            return;
        }

        if (IsFloat())
        {
            root[Name] = FloatValue;
            return;
        }

        root[Name] = StringValue ?? string.Empty;
    }

    internal string GetStringValue()
    {
        if (IsArray())
        {
            return CsvValue;
        }

        if (IsBool())
        {
            return BoolValue.ToString();
        }

        if (IsInt())
        {
            return IntValue.ToString();
        }

        if (IsFloat())
        {
            return FloatValue.ToString();
        }

        return StringValue ?? string.Empty;
    }

    internal List<string> GetStringListValue()
    {
        var values = new List<string>();
        string[] parts = CsvValue.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
        for (int i = 0; i < parts.Length; i++)
        {
            values.Add(parts[i].Trim());
        }

        return values;
    }

    private void LoadToken(JToken token)
    {
        if (token == null || token.Type == JTokenType.Null)
        {
            return;
        }

        if (IsArray())
        {
            CsvValue = token.Type == JTokenType.Array
                ? string.Join(", ", token.Values<string>())
                : token.ToString(Formatting.None);
            return;
        }

        if (IsBool())
        {
            BoolValue = token.Type == JTokenType.Boolean && token.Value<bool>();
            return;
        }

        if (IsInt())
        {
            IntValue = token.Type == JTokenType.Integer ? token.Value<int>() : 0;
            return;
        }

        if (IsFloat())
        {
            FloatValue = token.Type == JTokenType.Float || token.Type == JTokenType.Integer ? token.Value<float>() : 0f;
            return;
        }

        StringValue = token.Type == JTokenType.String ? token.Value<string>() : token.ToString(Formatting.None);
    }

    private bool IsBool()
    {
        return ScenarioSequenceOdinEditorWindow.Normalize(TypeHint).ToLowerInvariant().Contains("bool");
    }

    private bool IsInt()
    {
        string type = ScenarioSequenceOdinEditorWindow.Normalize(TypeHint).ToLowerInvariant();
        return type.Contains("int") && !type.Contains("[]");
    }

    private bool IsFloat()
    {
        string type = ScenarioSequenceOdinEditorWindow.Normalize(TypeHint).ToLowerInvariant();
        return (type.Contains("float") || type.Contains("number")) && !type.Contains("[]");
    }

    private bool IsArray()
    {
        return ScenarioSequenceOdinEditorWindow.Normalize(TypeHint).Contains("[]");
    }

    private bool IsStringLike()
    {
        return !IsBool() && !IsInt() && !IsFloat() && !IsArray();
    }

    private bool HasDescription()
    {
        return !string.IsNullOrWhiteSpace(DescriptionKo);
    }

    private string BuildHeader()
    {
        string display = string.IsNullOrWhiteSpace(DisplayNameKo) ? Name : DisplayNameKo.Trim() + " (" + Name + ")";
        return (Required ? "[필수] " : string.Empty) + display;
    }

    private static string InferType(JToken token)
    {
        if (token == null)
        {
            return "string";
        }

        switch (token.Type)
        {
            case JTokenType.Boolean:
                return "bool";
            case JTokenType.Integer:
                return "int";
            case JTokenType.Float:
                return "float";
            case JTokenType.Array:
                return "string[]";
            default:
                return "string";
        }
    }
}
