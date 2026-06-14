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
        public System.Collections.IEnumerator SwitchTo(string moduleId, ActionExecutionContext context)
        {
            yield break;
        }

        public System.Collections.IEnumerator Start(string moduleId, ActionExecutionContext context)
        {
            yield break;
        }
    }
}
