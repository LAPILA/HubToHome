using System.Collections;
using NUnit.Framework;
using UnityEngine;

public class CinematicStageActionAdapterTests
{
    [Test]
    public void ShotPlayPassesStableStageAndShotIdsToRunner()
    {
        var runner = new RecordingStageRunner();
        ActionSequenceAsset sequence = MakeSequence(CinematicShotPlayActionAdapter.Id, "{\"stage\":\"overworld.arrival\",\"shot\":\"overworld.subway_arrival\"}");
        ActionExecutionContext context = SceneActionSequenceContextFactory.Create(sequence, runner);

        try
        {
            RunToCompletion(SceneActionSequenceContextFactory.CreateDirector().Play(sequence, context));

            Assert.That(context.Handle.Status, Is.EqualTo(ActionExecutionStatus.Succeeded));
            Assert.That(runner.LastOperation, Is.EqualTo("play"));
            Assert.That(runner.LastStageId, Is.EqualTo("overworld.arrival"));
            Assert.That(runner.LastShotId, Is.EqualTo("overworld.subway_arrival"));
        }
        finally
        {
            Object.DestroyImmediate(sequence);
        }
    }

    [Test]
    public void StagePrepareFailsClearlyWhenRunnerIsMissing()
    {
        ActionSequenceAsset sequence = MakeSequence(CinematicStagePrepareActionAdapter.Id, "{\"stage\":\"overworld.arrival\",\"shot\":\"overworld.subway_arrival\"}");
        var context = new ActionExecutionContext();
        var registry = new ActionAdapterRegistry();
        registry.Register(new CinematicStagePrepareActionAdapter());

        try
        {
            RunToCompletion(new ActionDirector(registry).Play(sequence, context));

            Assert.That(context.Handle.Status, Is.EqualTo(ActionExecutionStatus.Failed));
            Assert.That(context.Handle.Result.Message, Does.Contain("ICinematicStageRunner is missing"));
        }
        finally
        {
            Object.DestroyImmediate(sequence);
        }
    }

    [Test]
    public void ReleasePassesStageIdToRunner()
    {
        var runner = new RecordingStageRunner();
        ActionSequenceAsset sequence = MakeSequence(CinematicStageReleaseActionAdapter.Id, "{\"stage\":\"overworld.arrival\"}");
        ActionExecutionContext context = SceneActionSequenceContextFactory.Create(sequence, runner);

        try
        {
            RunToCompletion(SceneActionSequenceContextFactory.CreateDirector().Play(sequence, context));

            Assert.That(context.Handle.Status, Is.EqualTo(ActionExecutionStatus.Succeeded));
            Assert.That(runner.LastOperation, Is.EqualTo("release"));
            Assert.That(runner.LastStageId, Is.EqualTo("overworld.arrival"));
        }
        finally
        {
            Object.DestroyImmediate(sequence);
        }
    }

    private static ActionSequenceAsset MakeSequence(string actionId, string parametersJson)
    {
        ActionSequenceAsset sequence = ScriptableObject.CreateInstance<ActionSequenceAsset>();
        sequence.Actions.Add(new ScenarioActionData
        {
            ActionId = actionId,
            ParametersJson = parametersJson
        });
        return sequence;
    }

    private static void RunToCompletion(IEnumerator routine, int maxSteps = 100)
    {
        int steps = 0;
        while (routine.MoveNext())
        {
            steps++;
            if (steps > maxSteps)
            {
                Assert.Fail("Routine did not complete within " + maxSteps + " steps.");
            }
        }
    }

    private sealed class RecordingStageRunner : ICinematicStageRunner
    {
        public string LastOperation = string.Empty;
        public string LastStageId = string.Empty;
        public string LastShotId = string.Empty;

        public IEnumerator PrepareStage(string stageId, string shotId, ActionExecutionContext context)
        {
            LastOperation = "prepare";
            LastStageId = stageId;
            LastShotId = shotId;
            yield break;
        }

        public IEnumerator PlayShot(string stageId, string shotId, ActionExecutionContext context)
        {
            LastOperation = "play";
            LastStageId = stageId;
            LastShotId = shotId;
            yield break;
        }

        public IEnumerator ReleaseStage(string stageId, ActionExecutionContext context)
        {
            LastOperation = "release";
            LastStageId = stageId;
            yield break;
        }
    }
}
