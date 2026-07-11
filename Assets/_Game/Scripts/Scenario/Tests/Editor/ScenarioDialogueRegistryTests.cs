using NUnit.Framework;
using UnityEngine;

public class ScenarioDialogueRegistryTests
{
    [Test]
    public void RegistersScenarioDialogueReferencesIntoRunner()
    {
        DialogueData dialogue = MakeDialogue();
        var registry = new ScenarioDialogueRegistry(new[]
        {
            new ScenarioDialogueReferenceData
            {
                DialogueId = " zev.phase2_intro ",
                Dialogue = dialogue
            }
        });

        var runner = new DialogueManagerRunner();

        try
        {
            int registeredCount = registry.RegisterInto(runner);

            Assert.That(registeredCount, Is.EqualTo(1));
            Assert.That(runner.TryGetRegisteredDialogue("zev.phase2_intro", out DialogueData registered), Is.True);
            Assert.That(registered, Is.SameAs(dialogue));
        }
        finally
        {
            Object.DestroyImmediate(dialogue);
        }
    }

    [Test]
    public void IgnoresBlankIdsAndNullDialogues()
    {
        DialogueData dialogue = MakeDialogue();
        var registry = new ScenarioDialogueRegistry(new[]
        {
            null,
            new ScenarioDialogueReferenceData { DialogueId = " ", Dialogue = dialogue },
            new ScenarioDialogueReferenceData { DialogueId = "zev.missing_dialogue", Dialogue = null }
        });

        try
        {
            Assert.That(registry.Count, Is.EqualTo(0));
            Assert.That(registry.TryResolve("zev.missing_dialogue", out DialogueData resolved), Is.False);
            Assert.That(resolved, Is.Null);
        }
        finally
        {
            Object.DestroyImmediate(dialogue);
        }
    }

    [Test]
    public void DuplicateDialogueIdUsesLastValidReference()
    {
        DialogueData first = MakeDialogue();
        DialogueData second = MakeDialogue();
        var registry = new ScenarioDialogueRegistry(new[]
        {
            new ScenarioDialogueReferenceData { DialogueId = "zev.phase2_intro", Dialogue = first },
            new ScenarioDialogueReferenceData { DialogueId = "zev.phase2_intro", Dialogue = second }
        });

        try
        {
            Assert.That(registry.Count, Is.EqualTo(1));
            Assert.That(registry.TryResolve("zev.phase2_intro", out DialogueData resolved), Is.True);
            Assert.That(resolved, Is.SameAs(second));
        }
        finally
        {
            Object.DestroyImmediate(first);
            Object.DestroyImmediate(second);
        }
    }

    private static DialogueData MakeDialogue()
    {
        DialogueData dialogue = ScriptableObject.CreateInstance<DialogueData>();
        dialogue.Nodes.Add(new DialogueNode { DefaultText = "테스트 대사" });
        return dialogue;
    }
}
