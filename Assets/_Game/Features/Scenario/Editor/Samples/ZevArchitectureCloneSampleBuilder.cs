using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static class ZevArchitectureCloneSampleBuilder
{
    public const string SourcePath = "Assets/_Game/Features/Scenario/Source/ZEV/zev_architecture_clone.scenario.yaml";
    public const string ScenarioAssetPath = "Assets/_Game/Features/Scenario/Generated/ZEV/ZEV_ArchitectureClone_BattleScenario.asset";
    public const string CatalogAssetPath = "Assets/_Game/Features/Scenario/Data/Catalogs/ScenarioActionCatalog_ZEV_ArchitectureClone.asset";
    public const string EnemyCloneAssetPath = "Assets/_Game/Features/Characters/Data/EnemyDB/ZEV/Enemy_ZEV_ArchitectureClone.asset";
    public const string EnemyCloneId = "zev_architecture_clone";

    private const string SourceEnemyAssetPath = "Assets/_Game/Features/Characters/Data/EnemyDB/ZEV/Enemy_ZEV.asset";
    private const string DialogueFolderPath = "Assets/_Game/Features/Dialogue/Data/Scenario/ZEV";

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

    public static ScenarioValidationResult BuildAssets()
    {
        var result = new ScenarioValidationResult();
        EnsureFolder("Assets/_Game/Features/Scenario/Generated");
        EnsureFolder("Assets/_Game/Features/Scenario/Generated/ZEV");
        EnsureFolder("Assets/_Game/Features/Scenario/Data/Catalogs");
        EnsureFolder("Assets/_Game/Features/Dialogue/Data/Scenario");
        EnsureFolder(DialogueFolderPath);

        AssetDatabase.ImportAsset(SourcePath);
        CreateOrUpdateDialogue(
            DialogueFolderPath + "/ZEV_Clone_Phase2Intro.asset",
            "ZEV가 QTE 전투를 거부하고 다른 규칙을 꺼내 든다.");
        CreateOrUpdateDialogue(
            DialogueFolderPath + "/ZEV_Clone_ShooterStart.asset",
            "총구를 맞춰 봐. 이번에는 네 차례가 아니야.");
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
        AddEntry(catalog, "module.switch", "module", "전투 모듈 전환", "ModuleSwitchActionAdapter", "- module.switch:\n    to: aim_shooter");
        AddEntry(catalog, "module.start", "module", "전투 모듈 시작", "ModuleStartActionAdapter", "- module.start:\n    module: aim_shooter");
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
            Parameters = new List<ActionCatalogParameter>()
        });
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
