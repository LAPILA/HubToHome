using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

public class TimelineCutsceneRunnerTests
{
    [Test]
    public void PlayCutscene_WhenWaitForCompleteIsFalse_DoesNotDestroyDirectorImmediately()
    {
        TimelineAsset timelineAsset = CreateFixedLengthTimeline(1d);
        TimelineCutsceneData cutscene = CreateCutscene("async_cutscene", timelineAsset);
        TimelineCutsceneCatalog catalog = CreateCatalog(cutscene);
        var runner = new TimelineCutsceneRunner(catalog);
        var context = new ActionExecutionContext(new ActionExecutionHandle("timeline_async_test"));

        try
        {
            RunToCompletion(runner.PlayCutscene("async_cutscene", false, false, false, false, context));

            PlayableDirector director = FindDirector("TimelineCutsceneDirector_async_cutscene");
            Assert.That(director, Is.Not.Null);
            Assert.That(director.playableAsset, Is.Not.Null);

            director.Stop();

            Assert.That(FindDirector("TimelineCutsceneDirector_async_cutscene"), Is.Null);
        }
        finally
        {
            DestroyTimelineArtifacts("TimelineCutsceneDirector_async_cutscene", catalog, cutscene, timelineAsset);
        }
    }

    [Test]
    public void PlayCutscene_WhenWaitForCompleteIsTrue_CleansUpAfterDirectorStops()
    {
        TimelineAsset timelineAsset = CreateFixedLengthTimeline(1d);
        TimelineCutsceneData cutscene = CreateCutscene("sync_cutscene", timelineAsset);
        TimelineCutsceneCatalog catalog = CreateCatalog(cutscene);
        var runner = new TimelineCutsceneRunner(catalog);
        var context = new ActionExecutionContext(new ActionExecutionHandle("timeline_sync_test"));

        try
        {
            IEnumerator routine = runner.PlayCutscene("sync_cutscene", true, false, false, false, context);
            Assert.That(routine.MoveNext(), Is.True);

            PlayableDirector director = FindDirector("TimelineCutsceneDirector_sync_cutscene");
            Assert.That(director, Is.Not.Null);

            director.Stop();
            RunToCompletion(routine);

            Assert.That(FindDirector("TimelineCutsceneDirector_sync_cutscene"), Is.Null);
        }
        finally
        {
            DestroyTimelineArtifacts("TimelineCutsceneDirector_sync_cutscene", catalog, cutscene, timelineAsset);
        }
    }

    [Test]
    public void PlayCutscene_WhenCanceledWhileWaiting_CleansUpDirector()
    {
        TimelineAsset timelineAsset = CreateFixedLengthTimeline(1d);
        TimelineCutsceneData cutscene = CreateCutscene("cancel_cutscene", timelineAsset);
        TimelineCutsceneCatalog catalog = CreateCatalog(cutscene);
        var runner = new TimelineCutsceneRunner(catalog);
        var context = new ActionExecutionContext(new ActionExecutionHandle("timeline_cancel_test"));

        try
        {
            IEnumerator routine = runner.PlayCutscene("cancel_cutscene", true, false, false, false, context);
            Assert.That(routine.MoveNext(), Is.True);

            PlayableDirector director = FindDirector("TimelineCutsceneDirector_cancel_cutscene");
            Assert.That(director, Is.Not.Null);

            context.Handle.Cancel("test cancel");
            RunToCompletion(routine);

            Assert.That(FindDirector("TimelineCutsceneDirector_cancel_cutscene"), Is.Null);
        }
        finally
        {
            DestroyTimelineArtifacts("TimelineCutsceneDirector_cancel_cutscene", catalog, cutscene, timelineAsset);
        }
    }

    private static TimelineAsset CreateFixedLengthTimeline(double duration)
    {
        TimelineAsset timeline = ScriptableObject.CreateInstance<TimelineAsset>();
        timeline.name = "TestTimeline";
        timeline.durationMode = TimelineAsset.DurationMode.FixedLength;
        timeline.fixedDuration = duration;
        return timeline;
    }

    private static TimelineCutsceneData CreateCutscene(string cutsceneId, TimelineAsset timelineAsset)
    {
        TimelineCutsceneData cutscene = ScriptableObject.CreateInstance<TimelineCutsceneData>();
        cutscene.CutsceneId = cutsceneId;
        cutscene.TimelineAsset = timelineAsset;
        return cutscene;
    }

    private static TimelineCutsceneCatalog CreateCatalog(TimelineCutsceneData cutscene)
    {
        TimelineCutsceneCatalog catalog = ScriptableObject.CreateInstance<TimelineCutsceneCatalog>();
        catalog.Cutscenes.Add(cutscene);
        return catalog;
    }

    private static PlayableDirector FindDirector(string directorName)
    {
        PlayableDirector[] directors = Resources.FindObjectsOfTypeAll<PlayableDirector>();
        for (int i = 0; i < directors.Length; i++)
        {
            PlayableDirector director = directors[i];
            if (director != null && director.gameObject != null && director.gameObject.name == directorName)
            {
                return director;
            }
        }

        return null;
    }

    private static void DestroyTimelineArtifacts(
        string directorName,
        TimelineCutsceneCatalog catalog,
        TimelineCutsceneData cutscene,
        TimelineAsset timelineAsset)
    {
        PlayableDirector director = FindDirector(directorName);
        if (director != null)
        {
            Object.DestroyImmediate(director.gameObject);
        }

        if (catalog != null)
        {
            Object.DestroyImmediate(catalog);
        }

        if (cutscene != null)
        {
            Object.DestroyImmediate(cutscene);
        }

        if (timelineAsset != null)
        {
            Object.DestroyImmediate(timelineAsset);
        }
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
}