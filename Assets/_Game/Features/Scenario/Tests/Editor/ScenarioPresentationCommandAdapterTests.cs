using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using NUnit.Framework;
using UnityEngine;

public class ScenarioPresentationCommandAdapterTests
{
    [Test]
    public void BgmCrossfadeUsesAudioRunnerAndWaitsForCompletion()
    {
        var log = new List<string>();
        var registry = new ActionAdapterRegistry();
        registry.Register(new BgmCrossfadeActionAdapter());

        var context = new ActionExecutionContext();
        context.SetService<IAudioActionRunner>(new LoggingAudioRunner(log));

        ActionSequenceAsset sequence = MakeSequence(new ScenarioActionData
        {
            ActionId = BgmCrossfadeActionAdapter.Id,
            ParametersJson = "{\"clip\":\"zev_phase2\",\"duration\":0.8}"
        });

        try
        {
            RunToCompletion(new ActionDirector(registry).Play(sequence, context));

            Assert.That(log, Is.EqualTo(new[] { "bgm:zev_phase2:0.8" }));
            Assert.That(context.Handle.Status, Is.EqualTo(ActionExecutionStatus.Succeeded));
        }
        finally
        {
            Object.DestroyImmediate(sequence);
        }
    }

    [Test]
    public void ScreenFadeFailsWhenRunnerIsMissing()
    {
        var registry = new ActionAdapterRegistry();
        registry.Register(new ScreenFadeActionAdapter());
        var context = new ActionExecutionContext();
        ActionSequenceAsset sequence = MakeSequence(new ScenarioActionData
        {
            ActionId = ScreenFadeActionAdapter.Id,
            ParametersJson = "{\"mode\":\"out\",\"duration\":0.3}"
        });

        try
        {
            RunToCompletion(new ActionDirector(registry).Play(sequence, context));

            Assert.That(context.Handle.Status, Is.EqualTo(ActionExecutionStatus.Failed));
            Assert.That(context.Handle.Result.Message, Does.Contain("IScreenTransitionRunner is missing"));
        }
        finally
        {
            Object.DestroyImmediate(sequence);
        }
    }

    [Test]
    public void ModuleSwitchUpdatesActionContextAfterRunnerCompletes()
    {
        var log = new List<string>();
        var registry = new ActionAdapterRegistry();
        registry.Register(new ModuleSwitchActionAdapter());

        var context = new ActionExecutionContext();
        context.ModuleId = "turn_qte";
        context.SetService<IGameModuleActionRunner>(new LoggingModuleRunner(log));

        ActionSequenceAsset sequence = MakeSequence(new ScenarioActionData
        {
            ActionId = ModuleSwitchActionAdapter.Id,
            ParametersJson = "{\"to\":\"aim_shooter\"}"
        });

        try
        {
            RunToCompletion(new ActionDirector(registry).Play(sequence, context));

            Assert.That(log, Is.EqualTo(new[] { "switch:aim_shooter" }));
            Assert.That(context.ModuleId, Is.EqualTo("aim_shooter"));
            Assert.That(context.Handle.Status, Is.EqualTo(ActionExecutionStatus.Succeeded));
        }
        finally
        {
            Object.DestroyImmediate(sequence);
        }
    }

    [Test]
    public void ModuleStartRequiresModuleId()
    {
        var registry = new ActionAdapterRegistry();
        registry.Register(new ModuleStartActionAdapter());
        var context = new ActionExecutionContext();
        context.SetService<IGameModuleActionRunner>(new LoggingModuleRunner(new List<string>()));

        ActionSequenceAsset sequence = MakeSequence(new ScenarioActionData
        {
            ActionId = ModuleStartActionAdapter.Id,
            ParametersJson = "{}"
        });

        try
        {
            RunToCompletion(new ActionDirector(registry).Play(sequence, context));

            Assert.That(context.Handle.Status, Is.EqualTo(ActionExecutionStatus.Failed));
            Assert.That(context.Handle.Result.Message, Does.Contain("module.start requires parameter 'module'"));
        }
        finally
        {
            Object.DestroyImmediate(sequence);
        }
    }

    private static ActionSequenceAsset MakeSequence(ScenarioActionData action)
    {
        ActionSequenceAsset sequence = ScriptableObject.CreateInstance<ActionSequenceAsset>();
        sequence.Actions.Add(action);
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

    private sealed class LoggingAudioRunner : IAudioActionRunner
    {
        private readonly List<string> _log;

        public LoggingAudioRunner(List<string> log)
        {
            _log = log;
        }

        public IEnumerator CrossfadeBgm(string clipId, float duration, ActionExecutionHandle handle)
        {
            yield return null;
            _log.Add("bgm:" + clipId + ":" + duration.ToString("0.0", CultureInfo.InvariantCulture));
        }
    }

    private sealed class LoggingModuleRunner : IGameModuleActionRunner
    {
        private readonly List<string> _log;

        public LoggingModuleRunner(List<string> log)
        {
            _log = log;
        }

        public string CurrentModuleId { get; private set; } = string.Empty;

        public IEnumerator SwitchTo(string moduleId, ActionExecutionContext context)
        {
            yield return null;
            _log.Add("switch:" + moduleId);
            CurrentModuleId = moduleId;
        }

        public IEnumerator Start(string moduleId, ActionExecutionContext context)
        {
            yield return null;
            _log.Add("start:" + moduleId);
            CurrentModuleId = moduleId;
        }
    }
}
