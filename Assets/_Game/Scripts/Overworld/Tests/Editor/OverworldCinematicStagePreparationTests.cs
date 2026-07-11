using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using Unity.Cinemachine;
using UnityEngine;

public class OverworldCinematicStagePreparationTests
{
    [Test]
    public void PreparationRunAppliesShotEndStateAndScopeRestoresSceneState()
    {
        GameObject root = new GameObject("PreviewStage");
        GameObject cameraObject = new GameObject("PreviewCamera");
        GameObject trainObject = new GameObject("Train");
        GameObject railObject = new GameObject("CameraRail");
        cameraObject.transform.SetParent(root.transform);
        trainObject.transform.SetParent(root.transform);
        railObject.transform.SetParent(root.transform);
        CinemachineCamera camera = cameraObject.AddComponent<CinemachineCamera>();
        OverworldCinematicStage stage = root.AddComponent<OverworldCinematicStage>();
        CinematicShotAsset shot = ScriptableObject.CreateInstance<CinematicShotAsset>();
        ActionCatalogAsset catalog = ScriptableObject.CreateInstance<ActionCatalogAsset>();
        ActionSequenceAsset sequence = ScriptableObject.CreateInstance<ActionSequenceAsset>();

        Vector3 originalTrainPosition = new Vector3(-2f, 1f, 0f);
        Vector3 originalRailPosition = new Vector3(-1f, 2f, 0f);
        trainObject.transform.localPosition = originalTrainPosition;
        railObject.transform.localPosition = originalRailPosition;
        camera.Lens.OrthographicSize = 8f;
        cameraObject.SetActive(false);

        shot.StageId = "overworld.arrival";
        shot.ShotId = "subway.arrival";
        shot.CameraRailSubjectId = "camera_rail";
        shot.StartOrthographicSize = 7f;
        shot.EndOrthographicSize = 4f;
        shot.Motions.Add(new CinematicShotMotion
        {
            SubjectId = "train",
            StartLocalPosition = new Vector3(-10f, 0f, 0f),
            EndLocalPosition = new Vector3(12f, 0f, 0f)
        });
        shot.Motions.Add(new CinematicShotMotion
        {
            SubjectId = "camera_rail",
            StartLocalPosition = new Vector3(-8f, 0f, 0f),
            EndLocalPosition = new Vector3(10f, 0f, 0f)
        });

        SetField(stage, "_stageId", "overworld.arrival");
        SetField(stage, "_cinematicCamera", camera);
        SetField(stage, "_subjects", new List<CinematicStageSubjectBinding>
        {
            new CinematicStageSubjectBinding { SubjectId = "train", Target = trainObject.transform },
            new CinematicStageSubjectBinding { SubjectId = "camera_rail", Target = railObject.transform }
        });
        SetField(stage, "_shots", new List<CinematicShotAsset> { shot });

        catalog.Entries.Add(Entry(
            CinematicStagePrepareActionAdapter.Id,
            ActionPreparationPolicy.ApplyFinalState));
        catalog.Entries.Add(Entry(
            CinematicShotPlayActionAdapter.Id,
            ActionPreparationPolicy.ExecuteIsolated));
        catalog.Entries.Add(Entry(
            FlowWaitActionAdapter.Id,
            ActionPreparationPolicy.SkipPresentation));

        sequence.SequenceId = "overworld.preview";
        sequence.Actions.Add(Action(
            "prepare",
            CinematicStagePrepareActionAdapter.Id,
            "{\"stage\":\"overworld.arrival\",\"shot\":\"subway.arrival\"}"));
        sequence.Actions.Add(Action(
            "shot",
            CinematicShotPlayActionAdapter.Id,
            "{\"stage\":\"overworld.arrival\",\"shot\":\"subway.arrival\"}"));
        sequence.Actions.Add(Action(
            "selected",
            FlowWaitActionAdapter.Id,
            "{\"duration\":0}"));

        var sourceContext = new ActionExecutionContext();
        sourceContext.SetService<ICinematicStageRunner>(stage);
        ActionExecutionContext context = PreviewActionExecutionContextFactory.Create(sourceContext);
        var run = new PreparationRun(catalog, ActionPreparationRegistry.CreateDefault());

        try
        {
            using (var scope = new EditorPreviewStateScope())
            {
                RunToCompletion(run.PrepareBefore(sequence, "selected", context, scope));

                Assert.That(run.Result.Status, Is.EqualTo(PreparationRunStatus.Succeeded));
                Assert.That(trainObject.transform.localPosition, Is.EqualTo(new Vector3(12f, 0f, 0f)));
                Assert.That(railObject.transform.localPosition, Is.EqualTo(new Vector3(10f, 0f, 0f)));
                Assert.That(camera.Lens.OrthographicSize, Is.EqualTo(4f).Within(0.001f));
                Assert.That(cameraObject.activeSelf, Is.True);

                scope.Restore();
                Assert.That(trainObject.transform.localPosition, Is.EqualTo(originalTrainPosition));
                Assert.That(railObject.transform.localPosition, Is.EqualTo(originalRailPosition));
                Assert.That(camera.Lens.OrthographicSize, Is.EqualTo(8f).Within(0.001f));
                Assert.That(cameraObject.activeSelf, Is.False);
            }
        }
        finally
        {
            Object.DestroyImmediate(sequence);
            Object.DestroyImmediate(catalog);
            Object.DestroyImmediate(shot);
            Object.DestroyImmediate(root);
        }
    }

    private static ActionCatalogEntry Entry(string actionId, ActionPreparationPolicy policy)
    {
        return new ActionCatalogEntry
        {
            ActionId = actionId,
            DisplayNameKo = actionId,
            PreviewSupport = ActionPreviewSupport.SafePreview,
            PreparationPolicy = policy
        };
    }

    private static ScenarioActionData Action(string blockId, string actionId, string parameters)
    {
        return new ScenarioActionData
        {
            BlockId = blockId,
            ActionId = actionId,
            ParametersJson = parameters
        };
    }

    private static void SetField(object target, string fieldName, object value)
    {
        FieldInfo field = target.GetType().GetField(
            fieldName,
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null, "Missing field: " + fieldName);
        field.SetValue(target, value);
    }

    private static void RunToCompletion(IEnumerator routine)
    {
        int guard = 0;
        while (routine.MoveNext())
        {
            Assert.That(guard++, Is.LessThan(256), "Preparation coroutine did not finish.");
        }
    }
}
