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
        Assert.That(_scenario.PartyIds, Is.EquivalentTo(new[] { "lab.wizzel.front" }));
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

    [TestCase("bunny_slime_lab", "BattleScenario_BunnySlimeLab", 36)]
    [TestCase("zev_lab", "BattleScenario_ZEVLab", 12)]
    public void SavedScenarioMatchesSourceIncludingNestedActions(string sourceName, string assetName, int expectedCount)
    {
        string sourcePath = "Assets/_Game/Content/Scenarios/Source/Battle/BunnySlimeBattleLab/" + sourceName + ".scenario.yaml";
        string source = File.ReadAllText(sourcePath, new UTF8Encoding(false, true));
        var parsed = new ScenarioSourceYamlParser().Parse(source, sourcePath);
        Assert.That(parsed.Success, Is.True, Format(parsed.Validation));
        BattleScenarioData saved = AssetDatabase.LoadAssetAtPath<BattleScenarioData>(
            BunnySlimeBattleLabBuilder.Root + "/Data/Scenario/" + assetName + ".asset");
        Assert.That(saved, Is.Not.Null);
        Assert.That(saved.Source.SourceHash, Is.EqualTo(ScenarioSourceHash.Compute(source)));
        Assert.That(saved.Sequences.Count, Is.EqualTo(parsed.Document.Sequences.Count));
        int actionCount = 0;
        foreach (var sourceSequence in parsed.Document.Sequences)
        {
            ActionSequenceAsset sequence = saved.Sequences.Find(item => item.SequenceId == sourceSequence.SequenceId);
            Assert.That(sequence, Is.Not.Null);
            AssertActionsMatch(sourceSequence.Actions, sequence.Actions);
            foreach (ScenarioActionData unused in Enumerate(sequence.Actions)) actionCount++;
        }
        Assert.That(actionCount, Is.EqualTo(expectedCount));
        var catalog = AssetDatabase.LoadAssetAtPath<ActionCatalogAsset>(ProductionActionLibraryBuildCommand.GeneratedAssetPath);
        ScenarioValidationResult validation = ScenarioCatalogValidator.ValidateBattleScenario(saved, catalog);
        Assert.That(validation.HasErrors, Is.False, Format(validation));
        foreach (var mapping in saved.Dialogues) Assert.That(mapping.Dialogue, Is.Not.Null);
    }

    private static void AssertActionsMatch(List<ScenarioActionData> expected, List<ScenarioActionData> actual)
    {
        Assert.That(actual, Has.Count.EqualTo(expected.Count));
        for (int i = 0; i < expected.Count; i++)
        {
            Assert.That(actual[i].BlockId, Is.EqualTo(expected[i].BlockId));
            Assert.That(actual[i].ActionId, Is.EqualTo(expected[i].ActionId));
            Assert.That(JToken.DeepEquals(JToken.Parse(actual[i].ParametersJson), JToken.Parse(expected[i].ParametersJson)), Is.True);
            AssertActionsMatch(expected[i].Children, actual[i].Children);
        }
    }

    [TestCase("BattleScenario_BunnySlimeLab")]
    [TestCase("BattleScenario_ZEVLab")]
    public void LabDialoguesKeepSharedPanelAndPortraitsWithBattleOnlyColorOverride(string assetName)
    {
        var scenario = AssetDatabase.LoadAssetAtPath<BattleScenarioData>(
            BunnySlimeBattleLabBuilder.Root + "/Data/Scenario/" + assetName + ".asset");
        Assert.That(scenario, Is.Not.Null);
        foreach (var mapping in scenario.Dialogues)
        {
            DialogueData dialogue = mapping.Dialogue;
            Assert.That(dialogue, Is.Not.Null);
            Assert.That(dialogue.Style, Is.EqualTo(DialogueStyle.Overworld),
                dialogue.name + " must retain the shared panel; only the battle layout lease changes its background color.");
            foreach (DialogueNode node in dialogue.Nodes)
            {
                Assert.That(node.Speaker, Is.Not.Null, dialogue.name);
                Assert.That(node.Speaker.GetPortrait(node.Emotion), Is.Not.Null,
                    dialogue.name + ": " + node.Speaker.name + "/" + node.Emotion);
            }
        }
    }


    [Test]
    public void ZevHalfHealthPhase_UsesOneAliveCrossingAndDisplayOnlyAttackBeforeChains()
    {
        var saved = AssetDatabase.LoadAssetAtPath<BattleScenarioData>(
            BunnySlimeBattleLabBuilder.Root + "/Data/Scenario/BattleScenario_ZEVLab.asset");
        Assert.That(saved, Is.Not.Null);
        Assert.That(saved.TriggerRules, Has.Count.EqualTo(3));
        Assert.That(saved.TriggerRules.Find(rule => rule.RuleId == "lab.zev.phase3"), Is.Null);
        Assert.That(saved.Sequences.Find(sequence => sequence.SequenceId == "lab.zev.phase3"), Is.Null);
        ScenarioTriggerRuleData rule = saved.TriggerRules.Find(item => item.RuleId == "lab.zev.phase2");
        Assert.That(rule, Is.Not.Null);
        Assert.That(rule.Timing, Is.EqualTo(ScenarioTriggerTiming.AfterCurrentAction));
        Assert.That(rule.Once, Is.EqualTo(ScenarioTriggerOnceScope.Session));
        var history = new BattleScenarioSession(saved.ScenarioId);
        var evaluator = new ScenarioTriggerEvaluator();
        Assert.That(evaluator.TryEvaluate(rule, HpEvent("lab.wizzel.front", .8f, .49f), history, null, out _, out _), Is.False);
        Assert.That(evaluator.TryEvaluate(rule, HpEvent("lab.zev", .8f, 0f), history, null, out _, out _), Is.False);
        Assert.That(evaluator.TryEvaluate(rule, HpEvent("lab.zev", .8f, .6f), history, null, out _, out _), Is.False);
        Assert.That(evaluator.TryEvaluate(rule, HpEvent("lab.zev", .8f, .49f), history, null, out _, out string error), Is.True, error);
        Assert.That(evaluator.TryEvaluate(rule, HpEvent("lab.zev", .8f, .49f), history, null, out _, out _), Is.False);
        ActionSequenceAsset phase = saved.Sequences.Find(sequence => sequence.SequenceId == "lab.zev.phase2");
        Assert.That(phase, Is.Not.Null);
        CollectionAssert.AreEqual(new[] { "battle.camera.focus", "dialogue.wait", "battle.actor.pose",
            "battle.actor.return_slots", "battle.camera.reset", "battle.flag.set" },
            phase.Actions.ConvertAll(action => action.ActionId));
        JObject attack = JObject.Parse(phase.Actions[2].ParametersJson);
        Assert.That((string)attack["actor"], Is.EqualTo("lab.zev"));
        Assert.That((string)attack["pose"], Is.EqualTo("attack"));
        Assert.That((float)attack["duration"], Is.EqualTo(.3f));
        Assert.That((float)attack["impact"], Is.Zero);
        JObject flag = JObject.Parse(phase.Actions[5].ParametersJson);
        Assert.That((string)flag["flag"], Is.EqualTo("lab.zev.chain_phase"));
        Assert.That(flag["value"].Type, Is.EqualTo(JTokenType.String));
        Assert.That((string)flag["value"], Is.EqualTo("true"));
    }

    [Test]
    public void ZevChainLab_RetainsLocalUltimateAndFourWideSkillPatterns()
    {
        var lab = AssetDatabase.LoadAssetAtPath<BunnySlimeBattleLabData>(BunnySlimeBattleLabBuilder.Root + "/Data/BunnySlimeBattleLab.asset");
        EnemyData zev = lab.Encounters[1].Enemy;
        Assert.That(zev.BaseStats.MaxHP, Is.EqualTo(1800));
        Assert.That(zev.UseOrderedSkills, Is.True);
        Assert.That(zev.SkillList, Has.Count.EqualTo(5));
        Assert.That(lab.Encounters[0].PhaseOverlay, Is.Null);
        Assert.That(lab.Encounters[1].PhaseOverlay, Is.Not.Null);
        var wide = new HashSet<string> { "lab.showcase.zev.blink_slash", "lab.showcase.zev.phantom_arc", "lab.showcase.zev.crash", "lab.showcase.zev.combo_slash" };
        int wideWindows = 0;
        foreach (SkillData skill in zev.SkillList)
        {
            foreach (SkillActionBlock block in skill.ActionTimeline)
            {
                if (!(block is Action_DefenseWindow defense)) continue;
                Assert.That((int)defense.MeleePassExtent, Is.EqualTo(wide.Contains(skill.SkillID) ? 1 : 0), skill.SkillID);
                if (!wide.Contains(skill.SkillID)) continue;
                wideWindows++;
                Assert.That(defense.UseMeleePassThrough, Is.True);
                Assert.That(defense.ScreenPassDuration, Is.EqualTo(.22f));
                Assert.That(defense.ScreenEdgeApproachDuration, Is.EqualTo(.1f));
                Assert.That(defense.ScreenEdgeMarginPixels, Is.EqualTo(12f));
                Assert.That(defense.AttackAnimationLeadTime, Is.EqualTo(.12f));
            }
        }
        Assert.That(wideWindows, Is.EqualTo(7));
    }

    [Test]
    public void ZevSample_HidesOnlyBunnyAndConnectsRecurringPhaseLock()
    {
        var lab = AssetDatabase.LoadAssetAtPath<BunnySlimeBattleLabData>(BunnySlimeBattleLabBuilder.Root + "/Data/BunnySlimeBattleLab.asset");
        Assert.That(lab.Encounters, Has.Length.EqualTo(2));
        Assert.That(lab.Encounters[0].Id, Is.EqualTo("lab.bunny.integrated"));
        Assert.That(lab.Encounters[0].HiddenInMenu, Is.True);
        Assert.That(lab.Encounters[0].Enemy, Is.Not.Null);
        Assert.That(lab.Encounters[0].Scenario, Is.Not.Null);
        Assert.That(lab.Encounters[1].Id, Is.EqualTo("lab.zev.presentation"));
        CollectionAssert.AreEqual(new[] { 1 }, lab.GetVisibleEncounterIndices());
        BattleSkillLockSettings settings = lab.Encounters[1].SkillLock;
        Assert.That(settings, Is.Not.Null);
        Assert.That(settings.ActivationFlag, Is.EqualTo("lab.zev.chain_phase"));
        Assert.That(settings.SkillCount, Is.EqualTo(3));
        Assert.That(settings.RefreshAfterActions, Is.EqualTo(2));

        string root = BunnySlimeBattleLabBuilder.Root + "/Data/Scenario/Dialogue_ZEVLab_";
        var opening = AssetDatabase.LoadAssetAtPath<DialogueData>(root + "opening.asset");
        var phase = AssetDatabase.LoadAssetAtPath<DialogueData>(root + "phase2.asset");
        Assert.That(opening.Nodes, Has.Count.EqualTo(2));
        Assert.That(phase.Nodes, Has.Count.EqualTo(3));
        Assert.That(opening.Nodes[0].DefaultText, Is.EqualTo("전투 실험 계약, 진행하겠습니다."));
        Assert.That(opening.Nodes[1].DefaultText, Is.EqualTo("제가 이길 듯요. ㅎ"));
        Assert.That(phase.Nodes[0].Speaker, Is.SameAs(opening.Nodes[0].Speaker));
        Assert.That(phase.Nodes[1].Speaker, Is.SameAs(opening.Nodes[0].Speaker));
        Assert.That(phase.Nodes[2].Speaker, Is.SameAs(opening.Nodes[1].Speaker));
        Assert.That(phase.Nodes[2].DefaultText, Is.EqualTo("아오, 귀찮아."));
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
