#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public sealed class BunnySlimeLabStoryTests
{
    private BattleScenarioData _scenario;
    private readonly List<DialogueData> _dialogues = new List<DialogueData>();

    [TearDown]
    public void TearDown()
    {
        if (_scenario != null)
        {
            foreach (ActionSequenceAsset sequence in _scenario.Sequences)
                if (sequence != null) Object.DestroyImmediate(sequence);
            Object.DestroyImmediate(_scenario);
        }
        foreach (DialogueData dialogue in _dialogues)
            if (dialogue != null) Object.DestroyImmediate(dialogue);
        _dialogues.Clear();
    }

    [Test]
    public void SourceUsesOfficialActionsAndPreservesSourceAndBlockIdentity()
    {
        string source = ImportSource();
        ActionCatalogAsset catalog = AssetDatabase.LoadAssetAtPath<ActionCatalogAsset>(
            ProductionActionLibraryBuildCommand.GeneratedAssetPath);
        Assert.That(catalog, Is.Not.Null);
        ScenarioValidationResult validation = ScenarioCatalogValidator.ValidateBattleScenario(_scenario, catalog);
        Assert.That(validation.HasErrors, Is.False, Format(validation));
        Assert.That(_scenario.Source.SourcePath, Is.EqualTo(BunnySlimeLabStoryBuilder.SourcePath));
        Assert.That(ScenarioSourcePathPolicy.TryNormalize(BunnySlimeLabStoryBuilder.SourcePath,
            out _, out string sourcePathError), Is.True, sourcePathError);
        Assert.That(_scenario.Source.SourceHash, Is.EqualTo(ScenarioSourceHash.Compute(source)));
        Assert.That(_scenario.PartyIds, Has.Count.EqualTo(6));
        Assert.That(_scenario.EnemyIds, Is.EquivalentTo(new[] { "lab.bunny_slime" }));
        Assert.That(_scenario.Dialogues, Has.Count.EqualTo(5));
        Assert.That(_scenario.AudioClips, Has.Count.EqualTo(4));
        Assert.That(_scenario.Sequences, Has.Count.EqualTo(4));

        int fakeAttackCount = 0;
        foreach (ActionSequenceAsset sequence in _scenario.Sequences)
        {
            Assert.That(ScenarioBlockIdentity.TryValidateUnique(sequence.Actions, out string error), Is.True, error);
            Assert.That(sequence.Source.SourceHash, Is.EqualTo(_scenario.Source.SourceHash));
            foreach (ScenarioActionData action in Enumerate(sequence.Actions))
            {
                Assert.That(action.ActionId, Is.Not.EqualTo("battle.participant.damage"),
                    "시험장 영화 연출은 실제 전투 피해를 적용하지 않아야 합니다.");
                Assert.That(action.ActionId, Is.Not.EqualTo("battle.skill.timeline"));
                if (action.ActionId == "battle.actor.fake_attack") fakeAttackCount++;
            }
        }
        Assert.That(fakeAttackCount, Is.EqualTo(1));
    }

    [Test]
    public void OpeningAndEndingUseSupportedLifecycleCheckpoints()
    {
        ImportSource();
        Assert.That(_scenario.Rules, Has.Count.EqualTo(2));
        BattleEventRuleData opening = _scenario.Rules.Find(rule => rule.EventType == BattleEventType.BattleStarted);
        BattleEventRuleData ending = _scenario.Rules.Find(rule => rule.EventType == BattleEventType.EnemyDefeated);
        Assert.That(opening, Is.Not.Null);
        Assert.That(opening.Timing, Is.EqualTo(BattleRuleTiming.Immediate));
        Assert.That(opening.Once, Is.EqualTo(BattleRuleOnceMode.PerBattle));
        Assert.That(ending, Is.Not.Null);
        Assert.That(ending.SubjectId, Is.EqualTo("lab.bunny_slime"));
        Assert.That(ending.Timing, Is.EqualTo(BattleRuleTiming.AfterCurrentAction));
        Assert.That(ending.Once, Is.EqualTo(BattleRuleOnceMode.PerBattle));
    }

    [TestCase("lab.bunny_slime.phase2", 1f, 0.6f)]
    [TestCase("lab.bunny_slime.phase3", 0.5f, 0.3f)]
    public void PhaseRulesAreEnemyScopedAliveAndOncePerBattle(string id, float previous, float current)
    {
        ImportSource();
        ScenarioTriggerRuleData rule = _scenario.TriggerRules.Find(item => item.RuleId == id);
        Assert.That(rule, Is.Not.Null);
        Assert.That(rule.Timing, Is.EqualTo(ScenarioTriggerTiming.AfterCurrentAction));
        Assert.That(rule.Once, Is.EqualTo(ScenarioTriggerOnceScope.Session));
        var history = new BattleScenarioSession(_scenario.ScenarioId);
        var evaluator = new ScenarioTriggerEvaluator();

        Assert.That(evaluator.TryEvaluate(rule, HpEvent("lab.wizzel.front", previous, current),
            history, null, out _, out string otherError), Is.False);
        Assert.That(otherError, Is.Empty);
        Assert.That(evaluator.TryEvaluate(rule, HpEvent("lab.bunny_slime", previous, 0f),
            history, null, out _, out string deadError), Is.False);
        Assert.That(deadError, Is.Empty);
        Assert.That(evaluator.TryEvaluate(rule, HpEvent("lab.bunny_slime", previous, current),
            history, null, out _, out string validError), Is.True, validError);
        Assert.That(evaluator.TryEvaluate(rule, HpEvent("lab.bunny_slime", previous, current),
            history, null, out _, out _), Is.False);
    }

    private string ImportSource()
    {
        string source = File.ReadAllText(BunnySlimeLabStoryBuilder.SourcePath, new UTF8Encoding(false, true));
        var resolver = new SourceTestResolver(_dialogues);
        ScenarioSourceSyncResult result = new ScenarioSourceImporter(
            new ScenarioSourceYamlParser(), resolver, resolver).Import(source, BunnySlimeLabStoryBuilder.SourcePath);
        _scenario = result.Scenario;
        Assert.That(result.Success, Is.True, Format(result.Validation));
        return source;
    }

    private static ScenarioEventData HpEvent(string subject, float previous, float current)
    {
        var value = new ScenarioEventData(BuiltInScenarioEventIds.ParticipantHpChanged);
        value.SetPayloadValue("subject", new JValue(subject));
        value.SetPayloadValue("previousRatio", new JValue(previous));
        value.SetPayloadValue("currentRatio", new JValue(current));
        return value;
    }

    private static IEnumerable<ScenarioActionData> Enumerate(List<ScenarioActionData> actions)
    {
        foreach (ScenarioActionData action in actions)
        {
            yield return action;
            if (action.Children == null) continue;
            foreach (ScenarioActionData child in Enumerate(action.Children)) yield return child;
        }
    }

    private static string Format(ScenarioValidationResult validation) =>
        string.Join("\n", validation.Messages.ConvertAll(message => message.Code + ": " + message.Message));

    private sealed class SourceTestResolver : IScenarioDialogueReferenceResolver, IScenarioAudioReferenceResolver
    {
        private readonly List<DialogueData> _created;
        public SourceTestResolver(List<DialogueData> created) => _created = created;

        public bool TryResolveDialogue(string id, out DialogueData dialogue)
        {
            dialogue = null;
            if (!id.StartsWith(BunnySlimeLabStoryBuilder.ContentRoot + "/Data/Scenario/Dialogue_BunnyLab_", StringComparison.Ordinal))
                return false;
            dialogue = ScriptableObject.CreateInstance<DialogueData>();
            dialogue.name = Path.GetFileNameWithoutExtension(id);
            _created.Add(dialogue);
            return true;
        }

        public bool TryResolveAudioClip(string id, out AudioClip clip)
        {
            clip = AssetDatabase.LoadAssetAtPath<AudioClip>(id);
            return clip != null;
        }
    }
}
#endif
