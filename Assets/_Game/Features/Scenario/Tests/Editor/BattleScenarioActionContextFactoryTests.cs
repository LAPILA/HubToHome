using NUnit.Framework;
using UnityEngine;

public class BattleScenarioActionContextFactoryTests
{
    [Test]
    public void CreatesContextWithScenarioFieldsAndRegisteredDialogueRunner()
    {
        BattleScenarioData scenario = ScriptableObject.CreateInstance<BattleScenarioData>();
        DialogueData dialogue = MakeDialogue();
        scenario.ScenarioId = "zev_first_battle";
        scenario.PrimaryMode = "battle";
        scenario.OpeningModule = "turn_qte";
        scenario.Dialogues.Add(new ScenarioDialogueReferenceData
        {
            DialogueId = "zev.phase2_intro",
            Dialogue = dialogue
        });

        try
        {
            ActionExecutionContext context = BattleScenarioActionContextFactory.Create(scenario);

            Assert.That(context.ScenarioId, Is.EqualTo("zev_first_battle"));
            Assert.That(context.PrimaryMode, Is.EqualTo("battle"));
            Assert.That(context.ModuleId, Is.EqualTo("turn_qte"));
            Assert.That(context.Handle.ExecutionId, Is.EqualTo("battle_scenario"));

            IDialogueRunner runner = context.GetService<IDialogueRunner>();
            Assert.That(runner, Is.TypeOf<DialogueManagerRunner>());
            var dialogueRunner = (DialogueManagerRunner)runner;
            Assert.That(dialogueRunner.TryGetRegisteredDialogue("zev.phase2_intro", out DialogueData registered), Is.True);
            Assert.That(registered, Is.SameAs(dialogue));
        }
        finally
        {
            Object.DestroyImmediate(dialogue);
            Object.DestroyImmediate(scenario);
        }
    }

    [Test]
    public void CreatesBattleDefaultContextWhenScenarioIsMissing()
    {
        ActionExecutionContext context = BattleScenarioActionContextFactory.Create(null);

        Assert.That(context.ScenarioId, Is.Empty);
        Assert.That(context.PrimaryMode, Is.EqualTo("battle"));
        Assert.That(context.ModuleId, Is.Empty);
        Assert.That(context.GetService<IDialogueRunner>(), Is.TypeOf<DialogueManagerRunner>());
    }

    [Test]
    public void RegistersSkillTimelineRunnerWhenProvided()
    {
        var runner = new FakeSkillTimelineRunner();

        ActionExecutionContext context = BattleScenarioActionContextFactory.Create(
            null,
            null,
            runner);

        Assert.That(context.GetService<ISkillTimelineRunner>(), Is.SameAs(runner));
    }

    [Test]
    public void RegistersGameModuleActionRunnerWhenProvided()
    {
        var runner = new FakeGameModuleActionRunner();

        ActionExecutionContext context = BattleScenarioActionContextFactory.Create(
            null,
            null,
            null,
            runner);

        Assert.That(context.GetService<IGameModuleActionRunner>(), Is.SameAs(runner));
    }

    [Test]
    public void UsesCurrentGameModuleRunnerIdBeforeScenarioOpeningModule()
    {
        BattleScenarioData scenario = ScriptableObject.CreateInstance<BattleScenarioData>();
        scenario.OpeningModule = "turn_qte";
        var runner = new FakeGameModuleActionRunner
        {
            CurrentModuleId = "aim_shooter"
        };

        try
        {
            ActionExecutionContext context = BattleScenarioActionContextFactory.Create(
                scenario,
                gameModuleActionRunner: runner);

            Assert.That(context.ModuleId, Is.EqualTo("aim_shooter"));
        }
        finally
        {
            Object.DestroyImmediate(scenario);
        }
    }

    [Test]
    public void RegistersBattleSessionStateReaderWhenProvided()
    {
        BattleScenarioData scenario = ScriptableObject.CreateInstance<BattleScenarioData>();
        scenario.ScenarioId = "zev_first_battle";
        scenario.OpeningModule = "turn_qte";
        BattleSessionState sessionState = BattleSessionState.Create(scenario);
        sessionState.SetCurrentModuleId("aim_shooter");

        try
        {
            ActionExecutionContext context = BattleScenarioActionContextFactory.Create(
                scenario,
                battleSessionState: sessionState);

            Assert.That(context.ModuleId, Is.EqualTo("aim_shooter"));
            Assert.That(context.GetService<IBattleSessionStateReader>(), Is.SameAs(sessionState));
            Assert.That(context.GetService<IBattleSessionFlagStore>(), Is.SameAs(sessionState));
        }
        finally
        {
            Object.DestroyImmediate(scenario);
        }
    }

    [Test]
    public void RegistersBattleParticipantCommandRunnerWhenProvided()
    {
        var runner = new FakeBattleParticipantCommandRunner();

        ActionExecutionContext context = BattleScenarioActionContextFactory.Create(
            null,
            battleParticipantCommandRunner: runner);

        Assert.That(context.GetService<IBattleParticipantCommandRunner>(), Is.SameAs(runner));
    }

    [Test]
    public void RunnerCurrentModuleOverridesBattleSessionState()
    {
        BattleScenarioData scenario = ScriptableObject.CreateInstance<BattleScenarioData>();
        scenario.OpeningModule = "turn_qte";
        BattleSessionState sessionState = BattleSessionState.Create(scenario);
        sessionState.SetCurrentModuleId("aim_shooter");
        var runner = new FakeGameModuleActionRunner
        {
            CurrentModuleId = "boxing"
        };

        try
        {
            ActionExecutionContext context = BattleScenarioActionContextFactory.Create(
                scenario,
                gameModuleActionRunner: runner,
                battleSessionState: sessionState);

            Assert.That(context.ModuleId, Is.EqualTo("boxing"));
        }
        finally
        {
            Object.DestroyImmediate(scenario);
        }
    }

    [Test]
    public void RegistersAudioAndScreenRunnersWhenProvided()
    {
        var audioRunner = new FakeAudioActionRunner();
        var screenRunner = new FakeScreenTransitionRunner();

        ActionExecutionContext context = BattleScenarioActionContextFactory.Create(
            null,
            audioActionRunner: audioRunner,
            screenTransitionRunner: screenRunner);

        Assert.That(context.GetService<IAudioActionRunner>(), Is.SameAs(audioRunner));
        Assert.That(context.GetService<IScreenTransitionRunner>(), Is.SameAs(screenRunner));
    }

    private static DialogueData MakeDialogue()
    {
        DialogueData dialogue = ScriptableObject.CreateInstance<DialogueData>();
        dialogue.Nodes.Add(new DialogueNode { DefaultText = "테스트 대사" });
        return dialogue;
    }

    private sealed class FakeSkillTimelineRunner : ISkillTimelineRunner
    {
        public System.Collections.IEnumerator PlaySkillTimeline(
            string skillId,
            string actorId,
            System.Collections.Generic.IReadOnlyList<string> targetIds,
            ActionExecutionContext context)
        {
            yield break;
        }
    }

    private sealed class FakeGameModuleActionRunner : IGameModuleActionRunner
    {
        public string CurrentModuleId { get; set; } = string.Empty;

        public System.Collections.IEnumerator SwitchTo(string moduleId, ActionExecutionContext context)
        {
            yield break;
        }

        public System.Collections.IEnumerator Start(string moduleId, ActionExecutionContext context)
        {
            yield break;
        }
    }

    private sealed class FakeAudioActionRunner : IAudioActionRunner
    {
        public System.Collections.IEnumerator CrossfadeBgm(
            string clipId,
            float duration,
            ActionExecutionHandle handle)
        {
            yield break;
        }
    }

    private sealed class FakeScreenTransitionRunner : IScreenTransitionRunner
    {
        public System.Collections.IEnumerator Fade(
            string mode,
            string color,
            float duration,
            ActionExecutionHandle handle)
        {
            yield break;
        }
    }

    private sealed class FakeBattleParticipantCommandRunner : IBattleParticipantCommandRunner
    {
        public BattleParticipantCommandResult ApplyPureDamage(
            string subjectId,
            int amount,
            ActionExecutionContext context)
        {
            return BattleParticipantCommandResult.Succeeded(subjectId, amount, amount, 10, 0);
        }

        public BattleParticipantCommandResult HealHp(
            string subjectId,
            int amount,
            ActionExecutionContext context)
        {
            return BattleParticipantCommandResult.Succeeded(subjectId, amount, amount, 0, 10);
        }

        public BattleParticipantCommandResult HealMp(
            string subjectId,
            int amount,
            ActionExecutionContext context)
        {
            return BattleParticipantCommandResult.Succeeded(subjectId, amount, amount, 0, 10);
        }

        public BattleParticipantCommandResult ConsumeMp(
            string subjectId,
            int amount,
            ActionExecutionContext context)
        {
            return BattleParticipantCommandResult.Succeeded(subjectId, amount, amount, 10, 0);
        }
    }
}
