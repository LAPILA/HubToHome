#if UNITY_EDITOR
using System.Linq;
using NUnit.Framework;
using UnityEngine;

public sealed class ScenarioReferenceValidationTests
{
    [Test]
    public void ValidatorReportsMissingAndDuplicateScenarioLocalReferences()
    {
        BattleScenarioData scenario = ScriptableObject.CreateInstance<BattleScenarioData>();
        ActionSequenceAsset firstSequence = ScriptableObject.CreateInstance<ActionSequenceAsset>();
        ActionSequenceAsset secondSequence = ScriptableObject.CreateInstance<ActionSequenceAsset>();
        DialogueData dialogue = ScriptableObject.CreateInstance<DialogueData>();
        AudioClip clip = AudioClip.Create("TestClip", 16, 1, 44100, false);
        try
        {
            scenario.ScenarioId = "scenario.references";
            firstSequence.SequenceId = "sequence.same";
            secondSequence.SequenceId = "sequence.same";
            scenario.Sequences.Add(null);
            scenario.Sequences.Add(firstSequence);
            scenario.Sequences.Add(secondSequence);
            scenario.Dialogues.Add(null);
            scenario.Dialogues.Add(new ScenarioDialogueReferenceData
            {
                DialogueId = "dialogue.same",
                Dialogue = dialogue
            });
            scenario.Dialogues.Add(new ScenarioDialogueReferenceData
            {
                DialogueId = "dialogue.same",
                Dialogue = null
            });
            scenario.AudioClips.Add(null);
            scenario.AudioClips.Add(new ScenarioAudioReferenceData
            {
                AudioId = "audio.same",
                Clip = clip
            });
            scenario.AudioClips.Add(new ScenarioAudioReferenceData
            {
                AudioId = "audio.same",
                Clip = null
            });

            var snapshot = new ProjectContentSnapshot();
            snapshot.Scenarios.Add(scenario);

            ContentValidationReport report = ProjectContentValidator.Validate(snapshot);
            string[] codes = report.Issues.Select(issue => issue.Code).ToArray();

            Assert.That(codes, Does.Contain("scenario.sequence.missing"));
            Assert.That(codes, Does.Contain("scenario.sequence.id.duplicate"));
            Assert.That(codes, Does.Contain("scenario.dialogue.reference.missing"));
            Assert.That(codes, Does.Contain("scenario.dialogue.id.duplicate"));
            Assert.That(codes, Does.Contain("scenario.dialogue.asset.missing"));
            Assert.That(codes, Does.Contain("scenario.audio.reference.missing"));
            Assert.That(codes, Does.Contain("scenario.audio.id.duplicate"));
            Assert.That(codes, Does.Contain("scenario.audio.asset.missing"));
        }
        finally
        {
            Object.DestroyImmediate(scenario);
            Object.DestroyImmediate(firstSequence);
            Object.DestroyImmediate(secondSequence);
            Object.DestroyImmediate(dialogue);
            Object.DestroyImmediate(clip);
        }
    }
}
#endif
