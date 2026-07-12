using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static class ZevArchitectureCloneSampleBuilder
{
    public const string SourcePath = "Assets/_Game/Content/Scenarios/Source/ZEV/zev_architecture_clone.scenario.yaml";
    public const string ScenarioAssetPath = "Assets/_Game/Content/Scenarios/Generated/ZEV/ZEV_ArchitectureClone_BattleScenario.asset";
    public const string CatalogAssetPath = "Assets/_Game/Content/Scenarios/Catalogs/ScenarioActionCatalog_ZEV_ArchitectureClone.asset";
    public const string EnemyCloneAssetPath = "Assets/_Game/Content/Characters/EnemyDB/ZEV/Enemy_ZEV_ArchitectureClone.asset";
    public const string PrefabCloneAssetPath = "Assets/_Game/Content/Characters/Prefabs/Enemy/ZEV_ArchitectureClone_Prefab.prefab";
    public const string EnemyCloneId = "zev_architecture_clone";

    private const string SourceEnemyAssetPath = "Assets/_Game/Content/Characters/EnemyDB/ZEV/Enemy_ZEV.asset";
    private const string SourcePrefabAssetPath = "Assets/_Game/Content/Characters/Prefabs/Enemy/ZEV_Prefab.prefab";
    private const string DialogueFolderPath = "Assets/_Game/Content/Dialogue/Data/Scenario/ZEV";

    [MenuItem("HubToHome/Scenario/Samples/Rebuild ZEV Architecture Clone")]
    [MenuItem("HubToHome/시나리오/샘플/ZEV 아키텍처 복제 에셋 재생성")]
    public static void RebuildFromMenu()
    {
        ScenarioValidationResult validation = BuildAssets();
        if (validation.HasErrors)
        {
            Debug.LogError("[ZEV Scenario Clone] 에셋 생성 실패:\n" + FormatValidation(validation));
            return;
        }

        Debug.Log("[ZEV Scenario Clone] 에셋 생성을 완료했습니다.");
    }

    [MenuItem("HubToHome/Scenario/Camera/Sync ZEV Camera Scenario")]
    [MenuItem("HubToHome/시나리오/카메라/ZEV 카메라 시나리오 동기화")]
    public static void SyncCameraScenarioAssets()
    {
        ActionCatalogAsset catalog = CreateOrUpdateCatalog();
        BattleScenarioData scenario = AssetDatabase.LoadAssetAtPath<BattleScenarioData>(ScenarioAssetPath);
        if (scenario == null)
        {
            Debug.LogError("[ZEV Camera] Runtime scenario asset is missing: " + ScenarioAssetPath);
            return;
        }

        scenario.Source.SourcePath = SourcePath;
        EditorUtility.SetDirty(scenario);
        var command = new ScenarioSourceRuntimeAssetReimportCommand();
        ScenarioSourceRuntimeAssetReimportResult reimport = command.ReimportFromSourcePath(
            scenario,
            catalog,
            DateTime.UtcNow);
        if (reimport.Validation.HasErrors)
        {
            Debug.LogError("[ZEV Camera] Scenario sync failed:\\n" + FormatValidation(reimport.Validation));
            return;
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.ImportAsset(ScenarioAssetPath);
        Debug.Log("[ZEV Camera] Catalog and runtime scenario synchronized.");
    }
    public static ScenarioValidationResult BuildAssets()
    {
        var result = new ScenarioValidationResult();
        EnsureFolder("Assets/_Game/Content/Scenarios/Generated");
        EnsureFolder("Assets/_Game/Content/Scenarios/Generated/ZEV");
        EnsureFolder("Assets/_Game/Content/Scenarios/Catalogs");
        EnsureFolder("Assets/_Game/Content/Dialogue/Data/Scenario");
        EnsureFolder(DialogueFolderPath);

        AssetDatabase.ImportAsset(SourcePath);
        CreateOrUpdateDialogue(
            DialogueFolderPath + "/ZEV_Clone_OpeningClash.asset",
            "ZEV: 호흡 안정. 합은 확인했습니다.");
        CreateOrUpdateDialogue(
            DialogueFolderPath + "/ZEV_Clone_OpeningAfter.asset",
            "ZEV: 태세 정비. 의뢰 수행 시작하겠습니다.");
        CreateOrUpdateDialogue(
            DialogueFolderPath + "/ZEV_Clone_Phase2Intro.asset",
            "ZEV: 태세를 정비하겠습니다.");
        CreateOrUpdateDialogue(
            DialogueFolderPath + "/ZEV_Clone_ShooterStart.asset",
            "ZEV: 본게임으로 복귀합니다. 조준선을 유지하세요.");
        CreateOrUpdateDialogue(
            DialogueFolderPath + "/ZEV_Clone_ShooterVictory.asset",
            "좋아. 이 방식도 통한다는 건 확인했어.");

        CreateOrUpdateEnemyClone(result);
        ActionCatalogAsset catalog = CreateOrUpdateCatalog();
        BattleScenarioData scenario = CreateOrLoadScenarioAsset();
        if (scenario == null)
        {
            result.AddError(
                "zev.clone.scenario.asset.create.failed",
                "ZEV architecture clone BattleScenarioData asset could not be created.",
                ScenarioAssetPath);
            return result;
        }

        scenario.Source.SourcePath = SourcePath;
        EditorUtility.SetDirty(scenario);

        var command = new ScenarioSourceRuntimeAssetReimportCommand();
        ScenarioSourceRuntimeAssetReimportResult reimport = command.ReimportFromSourcePath(
            scenario,
            catalog,
            DateTime.UtcNow);
        result.Merge(reimport.Validation);

        if (result.HasErrors)
        {
            return result;
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.ImportAsset(ScenarioAssetPath);
        CreateOrUpdatePrefabClone(result);
        AssetDatabase.SaveAssets();
        return result;
    }

    private static BattleScenarioData CreateOrLoadScenarioAsset()
    {
        BattleScenarioData scenario = AssetDatabase.LoadAssetAtPath<BattleScenarioData>(ScenarioAssetPath);
        if (scenario != null)
        {
            return scenario;
        }

        scenario = ScriptableObject.CreateInstance<BattleScenarioData>();
        scenario.name = "ZEV_ArchitectureClone_BattleScenario";
        AssetDatabase.CreateAsset(scenario, ScenarioAssetPath);
        return scenario;
    }

    private static void CreateOrUpdateEnemyClone(ScenarioValidationResult result)
    {
        EnemyData clone = AssetDatabase.LoadAssetAtPath<EnemyData>(EnemyCloneAssetPath);
        if (clone == null)
        {
            if (!AssetDatabase.CopyAsset(SourceEnemyAssetPath, EnemyCloneAssetPath))
            {
                result.AddError(
                    "zev.clone.enemy.copy.failed",
                    "Enemy_ZEV asset could not be duplicated for the architecture clone.",
                    EnemyCloneAssetPath);
                return;
            }

            clone = AssetDatabase.LoadAssetAtPath<EnemyData>(EnemyCloneAssetPath);
        }

        if (clone == null)
        {
            result.AddError(
                "zev.clone.enemy.load.failed",
                "Enemy_ZEV architecture clone asset could not be loaded.",
                EnemyCloneAssetPath);
            return;
        }

        clone.EnemyId = EnemyCloneId;
        clone.EnemyName = "ZEV Architecture Clone";
        EditorUtility.SetDirty(clone);
    }

    private static void CreateOrUpdatePrefabClone(ScenarioValidationResult result)
    {
        EnemyData cloneEnemy = AssetDatabase.LoadAssetAtPath<EnemyData>(EnemyCloneAssetPath);
        BattleScenarioData scenario = AssetDatabase.LoadAssetAtPath<BattleScenarioData>(ScenarioAssetPath);
        if (cloneEnemy == null || scenario == null)
        {
            result.AddError(
                "zev.clone.prefab.dependencies.missing",
                "ZEV architecture clone prefab requires clone enemy and scenario assets.",
                PrefabCloneAssetPath);
            return;
        }

        if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabCloneAssetPath) == null)
        {
            if (!AssetDatabase.CopyAsset(SourcePrefabAssetPath, PrefabCloneAssetPath))
            {
                result.AddError(
                    "zev.clone.prefab.copy.failed",
                    "ZEV prefab could not be duplicated for the architecture clone.",
                    PrefabCloneAssetPath);
                return;
            }
        }

        GameObject root = PrefabUtility.LoadPrefabContents(PrefabCloneAssetPath);
        if (root == null)
        {
            result.AddError(
                "zev.clone.prefab.load.failed",
                "ZEV architecture clone prefab could not be loaded.",
                PrefabCloneAssetPath);
            return;
        }

        try
        {
            root.name = "ZEV_ArchitectureClone_Prefab";

            EnemyCharacter enemyCharacter = root.GetComponent<EnemyCharacter>();
            if (enemyCharacter == null)
            {
                result.AddError(
                    "zev.clone.prefab.enemy_character.missing",
                    "ZEV architecture clone prefab requires EnemyCharacter.",
                    PrefabCloneAssetPath);
            }
            else
            {
                enemyCharacter.Data = cloneEnemy;
                EditorUtility.SetDirty(enemyCharacter);
            }

            DialogueBattleNPC dialogueBattleNpc = root.GetComponent<DialogueBattleNPC>();
            if (dialogueBattleNpc == null)
            {
                result.AddError(
                    "zev.clone.prefab.dialogue_battle_npc.missing",
                    "ZEV architecture clone prefab requires DialogueBattleNPC.",
                    PrefabCloneAssetPath);
            }
            else
            {
                var serialized = new SerializedObject(dialogueBattleNpc);
                SerializedProperty enemies = serialized.FindProperty("_fallbackEncounterEnemies");
                if (enemies != null)
                {
                    enemies.arraySize = 1;
                    enemies.GetArrayElementAtIndex(0).objectReferenceValue = cloneEnemy;
                }

                SerializedProperty scenarioProperty = serialized.FindProperty("_fallbackBattleScenarioData");
                if (scenarioProperty != null)
                {
                    scenarioProperty.objectReferenceValue = scenario;
                }

                serialized.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(dialogueBattleNpc);
            }

            OverworldEnemy overworldEnemy = root.GetComponent<OverworldEnemy>();
            if (overworldEnemy != null)
            {
                var serialized = new SerializedObject(overworldEnemy);
                SerializedProperty scenarioProperty = serialized.FindProperty("_battleScenarioData");
                if (scenarioProperty != null)
                {
                    scenarioProperty.objectReferenceValue = scenario;
                    serialized.ApplyModifiedPropertiesWithoutUndo();
                    EditorUtility.SetDirty(overworldEnemy);
                }
            }

            PrefabUtility.SaveAsPrefabAsset(root, PrefabCloneAssetPath);
            AssetDatabase.ImportAsset(PrefabCloneAssetPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static void CreateOrUpdateDialogue(string assetPath, string text)
    {
        DialogueData dialogue = AssetDatabase.LoadAssetAtPath<DialogueData>(assetPath);
        if (dialogue == null)
        {
            dialogue = ScriptableObject.CreateInstance<DialogueData>();
            dialogue.name = System.IO.Path.GetFileNameWithoutExtension(assetPath);
            AssetDatabase.CreateAsset(dialogue, assetPath);
        }

        dialogue.Style = DialogueStyle.Cinematic;
        dialogue.Nodes.Clear();
        dialogue.Nodes.Add(new DialogueNode
        {
            DefaultText = text,
            IsChoiceNode = false
        });
        EditorUtility.SetDirty(dialogue);
    }

    private static ActionCatalogAsset CreateOrUpdateCatalog()
    {
        ActionCatalogAsset catalog = AssetDatabase.LoadAssetAtPath<ActionCatalogAsset>(CatalogAssetPath);
        if (catalog == null)
        {
            catalog = ScriptableObject.CreateInstance<ActionCatalogAsset>();
            catalog.name = "ScenarioActionCatalog_ZEV_ArchitectureClone";
            AssetDatabase.CreateAsset(catalog, CatalogAssetPath);
        }

        catalog.CatalogId = "zev_architecture_clone";
        catalog.Entries.Clear();
        AddEntry(catalog, "flow.wait", "flow", "기다리기", "FlowWaitActionAdapter", "- flow.wait:\n    duration: 0.1");
        AddEntry(catalog, "dialogue.wait", "dialogue", "대사 표시 후 대기", "DialogueWaitActionAdapter", "- dialogue.wait:\n    id: zev.clone.phase2_intro");
        AddEntry(catalog, "bgm.crossfade", "audio", "BGM 크로스페이드", "BgmCrossfadeActionAdapter", "- bgm.crossfade:\n    clip: zev_clone_phase2\n    duration: 0.8");
        AddEntry(catalog, "screen.fade", "screen", "화면 페이드", "ScreenFadeActionAdapter", "- screen.fade:\n    mode: out\n    color: black\n    duration: 0.25");
        AddEntry(catalog, "cinematic.letterbox", "cinematic", "시네마틱 레터박스", "CinematicLetterboxActionAdapter", "- cinematic.letterbox:\n    mode: show\n    thickness: 0.14\n    duration: 0.18");
        AddEntry(catalog, "battle.camera.focus", "cinematic", "전투 카메라 포커스", "BattleCameraFocusActionAdapter", "- battle.camera.focus:\n    subject: zev_architecture_clone\n    zoom: 4.75\n    duration: 0.42");
        AddEntry(catalog, "battle.camera.reset", "cinematic", "전투 카메라 복귀", "BattleCameraResetActionAdapter", "- battle.camera.reset:\n    duration: 0.25");
        AddEntry(catalog, "battle.camera.shake", "camera", "전투 카메라 흔들림", "BattleCameraShakeActionAdapter", "- battle.camera.shake:\n    direction: right\n    intensity: 0.55\n    duration: 0.12\n    safety: gameplay_safe");
        AddEntry(catalog, "battle.actor.pose", "cinematic", "전투 액터 포즈", "BattleActorPoseActionAdapter", "- battle.actor.pose:\n    actor: zev_architecture_clone\n    pose: strong_skill\n    duration: 0.28\n    impact: 0.6");
        AddEntry(catalog, "battle.actor.flip", "cinematic", "전투 액터 좌우 반전", "BattleActorFlipActionAdapter", "- battle.actor.flip:\n    actor: zev_architecture_clone\n    mode: inverted");
        AddEntry(catalog, "battle.actor.move_to", "cinematic", "전투 액터 이동", "BattleActorMoveActionAdapter", "- battle.actor.move_to:\n    actor: zev_architecture_clone\n    anchor: center\n    x: 0.55\n    y: 0\n    duration: 0.32\n    pose: move");
        AddEntry(catalog, "battle.actor.drop_in", "cinematic", "전투 액터 낙하 착지", "BattleActorDropInActionAdapter", "- battle.actor.drop_in:\n    actor: zev_architecture_clone\n    height: 4.2\n    hang: 0.45\n    fall: 0.42\n    settle: 0.24\n    impact: 1.15");
        AddEntry(catalog, "battle.actor.fake_attack", "cinematic", "연출용 가짜 공격", "BattleActorFakeAttackActionAdapter", "- battle.actor.fake_attack:\n    actor: zev_architecture_clone\n    target: player_001\n    targetPose: parry\n    approach: 0.36\n    lunge: 0.11\n    hold: 0.08\n    recover: 0.16\n    impact: 0.75");
        AddEntry(catalog, "battle.actor.return_slots", "cinematic", "전투 슬롯 복귀", "BattleActorReturnSlotsActionAdapter", "- battle.actor.return_slots:\n    duration: 0.25");
        AddEntry(catalog, "module.switch", "module", "전투 모듈 전환", "ModuleSwitchActionAdapter", "- module.switch:\n    to: aim_shooter");
        AddEntry(catalog, "module.start", "module", "전투 모듈 시작", "ModuleStartActionAdapter", "- module.start:\n    module: aim_shooter");
        AddEntry(catalog, "battle.skill.timeline", "battle", "기존 스킬 타임라인 실행", "BattleSkillTimelineActionAdapter", "- battle.skill.timeline:\n    skill: skill_001\n    actor: zev_architecture_clone\n    targets: [player_001]");
        AddEntry(catalog, "battle.flag.set", "battle", "전투 플래그 설정", "BattleFlagSetActionAdapter", "- battle.flag.set:\n    flag: zev.clone.phase\n    value: shooter");
        AddEntry(catalog, "battle.participant.damage", "battle", "전투 참가자 피해", "BattleParticipantDamageActionAdapter", "- battle.participant.damage:\n    subject: zev_architecture_clone\n    amount: 999");
        EditorUtility.SetDirty(catalog);
        return catalog;
    }

    private static void AddEntry(
        ActionCatalogAsset catalog,
        string actionId,
        string category,
        string displayNameKo,
        string runtimeAdapterId,
        string exampleYaml)
    {
        catalog.Entries.Add(new ActionCatalogEntry
        {
            ActionId = actionId,
            Category = category,
            DisplayNameKo = displayNameKo,
            DescriptionKo = displayNameKo,
            RuntimeAdapterId = runtimeAdapterId,
            ExampleYaml = exampleYaml,
            Parameters = BuildParameters(actionId)
        });
    }

    private static List<ActionCatalogParameter> BuildParameters(string actionId)
    {
        var parameters = new List<ActionCatalogParameter>();
        switch (actionId)
        {
            case "flow.wait":
                parameters.Add(Parameter("duration", "Float", "시간", "기다릴 시간(초)입니다.", false, "0.1"));
                break;
            case "dialogue.wait":
                parameters.Add(Parameter("id", "String", "대화 ID", "Scenario Source dialogues에 등록된 안정적인 대화 ID입니다.", true, string.Empty));
                break;
            case "bgm.crossfade":
                parameters.Add(Parameter("clip", "String", "BGM ID", "Scenario Source audioClips에 등록된 BGM ID입니다.", true, string.Empty));
                parameters.Add(Parameter("duration", "Float", "전환 시간", "크로스페이드 시간(초)입니다.", false, "0.8"));
                break;
            case "screen.fade":
                parameters.Add(Parameter("mode", "String", "방향", "out 또는 in 같은 페이드 방향입니다.", true, "out"));
                parameters.Add(Parameter("color", "String", "색상", "black, white 같은 페이드 색상 ID입니다.", false, "black"));
                parameters.Add(Parameter("duration", "Float", "시간", "페이드 시간(초)입니다.", false, "0.25"));
                break;
            case "cinematic.letterbox":
                parameters.Add(Parameter("mode", "String", "표시 모드", "show 또는 hide입니다.", true, "show"));
                parameters.Add(Parameter("thickness", "Float", "두께", "화면 높이 기준 레터박스 두께 비율입니다.", false, "0.14"));
                parameters.Add(Parameter("duration", "Float", "시간", "레터박스가 열리고 닫히는 시간(초)입니다.", false, "0.18"));
                break;
            case "battle.camera.focus":
                parameters.Add(Parameter("subject", "String", "대상", "카메라가 포커스할 전투 참가자 ID입니다.", true, string.Empty));
                parameters.Add(Parameter("zoom", "Float", "줌", "OrthographicSize 기준 값입니다. 작을수록 더 줌인됩니다.", false, "4.75"));
                parameters.Add(Parameter("duration", "Float", "시간", "포커스 이동 시간(초)입니다.", false, "0.42"));
                parameters.Add(Parameter("style", "String", "연출 스타일", "static, dynamic, gameplay_safe 중 하나입니다.", false, "dynamic"));
                break;
            case "battle.camera.reset":
                parameters.Add(Parameter("duration", "Float", "시간", "기본 전투 카메라로 돌아가는 시간(초)입니다.", false, "0.25"));
                parameters.Add(Parameter("style", "String", "복귀 스타일", "static, dynamic, gameplay_safe 중 하나입니다.", false, "gameplay_safe"));
                break;
            case "battle.camera.shake":
                parameters.Add(Parameter("direction", "String", "방향", "left, right, up, down 중 하나입니다.", true, "right"));
                parameters.Add(Parameter("intensity", "Float", "강도", "0보다 큰 흔들림 강도입니다.", true, "0.55"));
                parameters.Add(Parameter("duration", "Float", "시간", "0보다 큰 흔들림 시간(초)입니다.", true, "0.12"));
                parameters.Add(Parameter("safety", "String", "안전 모드", "gameplay_safe 또는 cinematic입니다.", false, "gameplay_safe"));
                break;
            case "battle.actor.pose":
                parameters.Add(Parameter("actor", "String", "액터", "포즈를 취할 전투 참가자 ID입니다.", true, EnemyCloneId));
                parameters.Add(Parameter("pose", "String", "포즈", "idle, attack, skill, strong_skill 같은 포즈 ID입니다.", false, "strong_skill"));
                parameters.Add(Parameter("duration", "Float", "시간", "포즈 유지 시간(초)입니다.", false, "0.28"));
                parameters.Add(Parameter("impact", "Float", "충격", "카메라/타격감 강도입니다.", false, "0.6"));
                break;
            case "battle.actor.flip":
                parameters.Add(Parameter("actor", "String", "액터", "좌우 반전할 전투 참가자 ID입니다.", true, EnemyCloneId));
                parameters.Add(Parameter("mode", "String", "반전 모드", "default, inverted, toggle 중 하나입니다.", false, "default"));
                break;
            case "battle.actor.move_to":
                parameters.Add(Parameter("actor", "String", "액터", "이동할 전투 참가자 ID입니다.", true, EnemyCloneId));
                parameters.Add(Parameter("anchor", "String", "기준점", "current, center, player_slot, enemy_slot 중 하나입니다.", false, "center"));
                parameters.Add(Parameter("x", "Float", "X 오프셋", "기준점에서의 X 오프셋입니다.", false, "0.55"));
                parameters.Add(Parameter("y", "Float", "Y 오프셋", "기준점에서의 Y 오프셋입니다.", false, "0"));
                parameters.Add(Parameter("duration", "Float", "시간", "이동 시간(초)입니다.", false, "0.32"));
                parameters.Add(Parameter("pose", "String", "포즈", "이동 중 포즈입니다.", false, "move"));
                parameters.Add(Parameter("impact", "Float", "충격", "도착 시 타격감 강도입니다.", false, "0"));
                break;
            case "battle.actor.drop_in":
                parameters.Add(Parameter("actor", "String", "액터", "하늘에서 착지할 전투 참가자 ID입니다.", true, EnemyCloneId));
                parameters.Add(Parameter("height", "Float", "높이", "착지 시작 높이입니다.", false, "4.2"));
                parameters.Add(Parameter("hang", "Float", "공중 정지", "낙하 전 공중에 머무는 시간(초)입니다.", false, "0.45"));
                parameters.Add(Parameter("fall", "Float", "낙하 시간", "착지까지 떨어지는 시간(초)입니다.", false, "0.42"));
                parameters.Add(Parameter("settle", "Float", "착지 정지", "착지 후 충격을 보여줄 시간(초)입니다.", false, "0.24"));
                parameters.Add(Parameter("impact", "Float", "충격", "착지 카메라 흔들림 강도입니다.", false, "1.15"));
                break;
            case "battle.actor.fake_attack":
                parameters.Add(Parameter("actor", "String", "공격자", "연출용 공격을 수행할 전투 참가자 ID입니다.", true, EnemyCloneId));
                parameters.Add(Parameter("target", "String", "대상", "연출용 공격 대상 전투 참가자 ID입니다. 실제 HP는 변경하지 않습니다.", true, "player_001"));
                parameters.Add(Parameter("targetPose", "String", "대상 포즈", "피격 대신 parry, guard, hurt 같은 대상 반응 포즈를 지정합니다.", false, "hurt"));
                parameters.Add(Parameter("approach", "Float", "접근 거리", "대상 앞까지 접근하는 거리입니다.", false, "0.36"));
                parameters.Add(Parameter("lunge", "Float", "돌진 시간", "짧은 돌진 시간(초)입니다.", false, "0.08"));
                parameters.Add(Parameter("hold", "Float", "정지 시간", "히트 스톱처럼 멈추는 시간(초)입니다.", false, "0.04"));
                parameters.Add(Parameter("recover", "Float", "복귀 시간", "공격 후 물러나는 시간(초)입니다.", false, "0.12"));
                parameters.Add(Parameter("impact", "Float", "충격", "타격감 강도입니다. 실제 피해가 아닙니다.", false, "0.75"));
                break;
            case "battle.actor.return_slots":
                parameters.Add(Parameter("duration", "Float", "시간", "모든 전투 참가자가 기본 슬롯으로 복귀하는 시간(초)입니다.", false, "0.25"));
                break;
            case "module.switch":
                parameters.Add(Parameter("to", "String", "전환 대상", "전환할 Game Module ID입니다.", true, "aim_shooter"));
                break;
            case "module.start":
                parameters.Add(Parameter("module", "String", "시작 모듈", "시작할 Game Module ID입니다.", true, "aim_shooter"));
                break;
            case "battle.skill.timeline":
                parameters.Add(Parameter("skill", "String", "스킬 ID", "기존 SkillData.ActionTimeline을 실행할 SkillData ID입니다.", true, "skill_001"));
                parameters.Add(Parameter("actor", "String", "실행자", "스킬 타임라인을 실행할 전투 참가자 ID입니다.", true, EnemyCloneId));
                parameters.Add(Parameter("targets", "String[]", "대상 목록", "대상 전투 참가자 ID 목록입니다.", false, "[player_001]"));
                break;
            case "battle.flag.set":
                parameters.Add(Parameter("flag", "String", "플래그", "전투 중 공유할 battle flag ID입니다.", true, string.Empty));
                parameters.Add(Parameter("value", "String", "값", "저장할 flag 값입니다.", false, "true"));
                break;
            case "battle.participant.damage":
                parameters.Add(Parameter("subject", "String", "대상", "피해를 받을 전투 참가자 ID입니다.", true, string.Empty));
                parameters.Add(Parameter("amount", "Integer", "피해량", "1 이상의 순수 피해량입니다.", true, "1"));
                break;
        }

        return parameters;
    }

    private static ActionCatalogParameter Parameter(
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

    private static void EnsureFolder(string folderPath)
    {
        if (AssetDatabase.IsValidFolder(folderPath))
        {
            return;
        }

        string parent = System.IO.Path.GetDirectoryName(folderPath)?.Replace('\\', '/');
        string name = System.IO.Path.GetFileName(folderPath);
        if (!string.IsNullOrWhiteSpace(parent) && !AssetDatabase.IsValidFolder(parent))
        {
            EnsureFolder(parent);
        }

        if (!string.IsNullOrWhiteSpace(parent) && !string.IsNullOrWhiteSpace(name))
        {
            AssetDatabase.CreateFolder(parent, name);
        }
    }

    private static string FormatValidation(ScenarioValidationResult validation)
    {
        if (validation == null || validation.Messages.Count == 0)
        {
            return string.Empty;
        }

        var lines = new List<string>();
        for (int i = 0; i < validation.Messages.Count; i++)
        {
            ScenarioValidationMessage message = validation.Messages[i];
            lines.Add(message.Severity + " " + message.Code + " " + message.ObjectId + " - " + message.Message);
        }

        return string.Join("\n", lines);
    }
}
