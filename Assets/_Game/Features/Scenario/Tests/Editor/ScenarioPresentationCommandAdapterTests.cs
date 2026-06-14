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

    [Test]
    public void BattleParticipantDamageUsesCommandRunner()
    {
        var log = new List<string>();
        var registry = new ActionAdapterRegistry();
        registry.Register(new BattleParticipantDamageActionAdapter());

        var context = new ActionExecutionContext();
        context.SetService<IBattleParticipantCommandRunner>(new LoggingBattleParticipantCommandRunner(log));

        ActionSequenceAsset sequence = MakeSequence(new ScenarioActionData
        {
            ActionId = BattleParticipantDamageActionAdapter.Id,
            ParametersJson = "{\"subject\":\"zev\",\"amount\":25}"
        });

        try
        {
            RunToCompletion(new ActionDirector(registry).Play(sequence, context));

            Assert.That(log, Is.EqualTo(new[] { "damage:zev:25" }));
            Assert.That(context.Handle.Status, Is.EqualTo(ActionExecutionStatus.Succeeded));
        }
        finally
        {
            Object.DestroyImmediate(sequence);
        }
    }

    [Test]
    public void BattleParticipantHealHpFailsWhenRunnerIsMissing()
    {
        var registry = new ActionAdapterRegistry();
        registry.Register(new BattleParticipantHealHpActionAdapter());
        var context = new ActionExecutionContext();
        ActionSequenceAsset sequence = MakeSequence(new ScenarioActionData
        {
            ActionId = BattleParticipantHealHpActionAdapter.Id,
            ParametersJson = "{\"subject\":\"player\",\"amount\":10}"
        });

        try
        {
            RunToCompletion(new ActionDirector(registry).Play(sequence, context));

            Assert.That(context.Handle.Status, Is.EqualTo(ActionExecutionStatus.Failed));
            Assert.That(context.Handle.Result.Message, Does.Contain("IBattleParticipantCommandRunner is missing"));
        }
        finally
        {
            Object.DestroyImmediate(sequence);
        }
    }

    [Test]
    public void BattleParticipantCommandRequiresPositiveIntegerAmount()
    {
        var registry = new ActionAdapterRegistry();
        registry.Register(new BattleParticipantConsumeMpActionAdapter());
        var context = new ActionExecutionContext();
        context.SetService<IBattleParticipantCommandRunner>(new LoggingBattleParticipantCommandRunner(new List<string>()));
        ActionSequenceAsset sequence = MakeSequence(new ScenarioActionData
        {
            ActionId = BattleParticipantConsumeMpActionAdapter.Id,
            ParametersJson = "{\"subject\":\"player\",\"amount\":0}"
        });

        try
        {
            RunToCompletion(new ActionDirector(registry).Play(sequence, context));

            Assert.That(context.Handle.Status, Is.EqualTo(ActionExecutionStatus.Failed));
            Assert.That(context.Handle.Result.Message, Does.Contain("amount"));
            Assert.That(context.Handle.Result.Message, Does.Contain("greater than zero"));
        }
        finally
        {
            Object.DestroyImmediate(sequence);
        }
    }

    [Test]
    public void BattleParticipantCommandPropagatesRunnerFailure()
    {
        var registry = new ActionAdapterRegistry();
        registry.Register(new BattleParticipantHealMpActionAdapter());
        var context = new ActionExecutionContext();
        context.SetService<IBattleParticipantCommandRunner>(new FailingBattleParticipantCommandRunner());
        ActionSequenceAsset sequence = MakeSequence(new ScenarioActionData
        {
            ActionId = BattleParticipantHealMpActionAdapter.Id,
            ParametersJson = "{\"subject\":\"missing\",\"amount\":10}"
        });

        try
        {
            RunToCompletion(new ActionDirector(registry).Play(sequence, context));

            Assert.That(context.Handle.Status, Is.EqualTo(ActionExecutionStatus.Failed));
            Assert.That(context.Handle.Result.Message, Does.Contain("not found"));
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

    private sealed class LoggingBattleParticipantCommandRunner : IBattleParticipantCommandRunner
    {
        private readonly List<string> _log;

        public LoggingBattleParticipantCommandRunner(List<string> log)
        {
            _log = log;
        }

        public BattleParticipantCommandResult ApplyPureDamage(
            string subjectId,
            int amount,
            ActionExecutionContext context)
        {
            _log.Add("damage:" + subjectId + ":" + amount);
            return BattleParticipantCommandResult.Succeeded(subjectId, amount, amount, 100, 100 - amount);
        }

        public BattleParticipantCommandResult HealHp(
            string subjectId,
            int amount,
            ActionExecutionContext context)
        {
            _log.Add("heal_hp:" + subjectId + ":" + amount);
            return BattleParticipantCommandResult.Succeeded(subjectId, amount, amount, 50, 50 + amount);
        }

        public BattleParticipantCommandResult HealMp(
            string subjectId,
            int amount,
            ActionExecutionContext context)
        {
            _log.Add("heal_mp:" + subjectId + ":" + amount);
            return BattleParticipantCommandResult.Succeeded(subjectId, amount, amount, 0, amount);
        }

        public BattleParticipantCommandResult ConsumeMp(
            string subjectId,
            int amount,
            ActionExecutionContext context)
        {
            _log.Add("consume_mp:" + subjectId + ":" + amount);
            return BattleParticipantCommandResult.Succeeded(subjectId, amount, amount, amount, 0);
        }
    }

    private sealed class FailingBattleParticipantCommandRunner : IBattleParticipantCommandRunner
    {
        public BattleParticipantCommandResult ApplyPureDamage(
            string subjectId,
            int amount,
            ActionExecutionContext context)
        {
            return BattleParticipantCommandResult.Failed(subjectId, "Battle participant was not found.");
        }

        public BattleParticipantCommandResult HealHp(
            string subjectId,
            int amount,
            ActionExecutionContext context)
        {
            return BattleParticipantCommandResult.Failed(subjectId, "Battle participant was not found.");
        }

        public BattleParticipantCommandResult HealMp(
            string subjectId,
            int amount,
            ActionExecutionContext context)
        {
            return BattleParticipantCommandResult.Failed(subjectId, "Battle participant was not found.");
        }

        public BattleParticipantCommandResult ConsumeMp(
            string subjectId,
            int amount,
            ActionExecutionContext context)
        {
            return BattleParticipantCommandResult.Failed(subjectId, "Battle participant was not found.");
        }
    }
}
