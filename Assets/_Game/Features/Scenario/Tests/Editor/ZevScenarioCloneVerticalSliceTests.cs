using System.IO;
using NUnit.Framework;
using UnityEditor;

public class ZevScenarioCloneVerticalSliceTests
{
    [Test]
    public void ZevArchitectureCloneSourceImportsWithResolvedReferences()
    {
        string sourceText = File.ReadAllText(ZevArchitectureCloneSampleBuilder.SourcePath);
        var resolver = new AssetDatabaseScenarioDialogueReferenceResolver();
        var importer = new ScenarioSourceImporter(
            new ScenarioSourceYamlParser(),
            resolver,
            resolver);

        ScenarioSourceSyncResult importResult = importer.Import(
            sourceText,
            ZevArchitectureCloneSampleBuilder.SourcePath);

        Assert.That(importResult.Success, Is.True, Describe(importResult.Validation));
        Assert.That(importResult.Scenario.ScenarioId, Is.EqualTo("zev_architecture_clone"));
        Assert.That(importResult.Scenario.EnemyIds, Is.EqualTo(new[] { ZevArchitectureCloneSampleBuilder.EnemyCloneId }));
        Assert.That(importResult.Scenario.Dialogues.Count, Is.EqualTo(3));
        Assert.That(importResult.Scenario.AudioClips.Count, Is.EqualTo(3));
        Assert.That(importResult.Scenario.Rules.Count, Is.EqualTo(2));
        Assert.That(importResult.Scenario.Sequences.Count, Is.EqualTo(2));
        ActionSequenceAsset phase2 = importResult.Scenario.Sequences.Find(
            sequence => sequence != null && sequence.SequenceId == "zev_clone_phase2_transition");
        ActionSequenceAsset victory = importResult.Scenario.Sequences.Find(
            sequence => sequence != null && sequence.SequenceId == "zev_clone_shooter_victory");
        Assert.That(phase2, Is.Not.Null);
        Assert.That(victory, Is.Not.Null);
        AssertParameter(phase2.Actions[0], "clip", "zev_clone_phase2");
        AssertParameter(phase2.Actions[1], "id", "zev.clone.phase2_intro");
        AssertParameter(victory.Actions[0], "id", "zev.clone.shooter_victory");
        AssertParameter(victory.Actions[2], "subject", ZevArchitectureCloneSampleBuilder.EnemyCloneId);

        DestroyImportedScenario(importResult.Scenario);
    }

    [Test]
    public void ZevArchitectureCloneRuntimeAssetMatchesSourceAndCatalog()
    {
        BattleScenarioData scenario = AssetDatabase.LoadAssetAtPath<BattleScenarioData>(
            ZevArchitectureCloneSampleBuilder.ScenarioAssetPath);
        ActionCatalogAsset catalog = AssetDatabase.LoadAssetAtPath<ActionCatalogAsset>(
            ZevArchitectureCloneSampleBuilder.CatalogAssetPath);

        Assert.That(scenario, Is.Not.Null, "Runtime BattleScenarioData asset is missing.");
        Assert.That(catalog, Is.Not.Null, "Scenario ActionCatalogAsset is missing.");
        Assert.That(scenario.Source.SourcePath, Is.EqualTo(ZevArchitectureCloneSampleBuilder.SourcePath));
        Assert.That(scenario.OpeningModule, Is.EqualTo(BattleTurnQteGameModuleRuntime.Id));
        Assert.That(scenario.MemoryKey, Is.EqualTo("zev_architecture_clone"));
        Assert.That(scenario.Sequences.Exists(sequence => sequence != null && sequence.SequenceId == "zev_clone_phase2_transition"), Is.True);
        Assert.That(scenario.Sequences.Exists(sequence => sequence != null && sequence.SequenceId == "zev_clone_shooter_victory"), Is.True);

        ScenarioValidationResult validation = ScenarioCatalogValidator.ValidateBattleScenario(scenario, catalog);
        Assert.That(validation.HasErrors, Is.False, Describe(validation));
    }

    [Test]
    public void ZevArchitectureCloneUsesSeparateEnemyAssetWithStableId()
    {
        EnemyData source = AssetDatabase.LoadAssetAtPath<EnemyData>(
            "Assets/_Game/Features/Characters/Data/EnemyDB/ZEV/Enemy_ZEV.asset");
        EnemyData clone = AssetDatabase.LoadAssetAtPath<EnemyData>(
            ZevArchitectureCloneSampleBuilder.EnemyCloneAssetPath);

        Assert.That(source, Is.Not.Null);
        Assert.That(clone, Is.Not.Null);
        Assert.That(clone, Is.Not.SameAs(source));
        Assert.That(clone.EnemyId, Is.EqualTo(ZevArchitectureCloneSampleBuilder.EnemyCloneId));
        Assert.That(clone.BattlePrefab, Is.SameAs(source.BattlePrefab));
        Assert.That(clone.SkillList.Count, Is.EqualTo(source.SkillList.Count));
        Assert.That(clone.StrongSkillList.Count, Is.EqualTo(source.StrongSkillList.Count));
    }

    private static void DestroyImportedScenario(BattleScenarioData scenario)
    {
        if (scenario == null)
        {
            return;
        }

        for (int i = 0; i < scenario.Sequences.Count; i++)
        {
            UnityEngine.Object.DestroyImmediate(scenario.Sequences[i]);
        }

        UnityEngine.Object.DestroyImmediate(scenario);
    }

    private static void AssertParameter(ScenarioActionData action, string key, string expected)
    {
        string value;
        string error;
        Assert.That(
            ScenarioActionParameterReader.TryGetString(action, key, out value, out error),
            Is.True,
            error);
        Assert.That(value, Is.EqualTo(expected));
    }

    private static string Describe(ScenarioValidationResult validation)
    {
        if (validation == null || validation.Messages.Count == 0)
        {
            return string.Empty;
        }

        var lines = new string[validation.Messages.Count];
        for (int i = 0; i < validation.Messages.Count; i++)
        {
            ScenarioValidationMessage message = validation.Messages[i];
            lines[i] = message.Severity + " " + message.Code + " " + message.ObjectId + " - " + message.Message;
        }

        return string.Join("\n", lines);
    }
}
