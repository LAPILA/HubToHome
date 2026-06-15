using System.Collections.Generic;
using NUnit.Framework;

public class BattleAimShooterCombatSessionTests
{
    [Test]
    public void FireAtAliveEnemyAppliesDamageAndReportsVictoryOutcome()
    {
        var context = CreateContext();
        var commands = new RecordingBattleParticipantCommandRunner();
        var events = new RecordingGameModuleEventSink();
        context.SetService<IBattleParticipantCommandRunner>(commands);
        context.SetService<IGameModuleEventSink>(events);
        context.SetService<IBattleSessionStateReader>(CreateSessionWithEnemy("zev", true));
        var moduleContext = new GameModuleRuntimeContext(
            context,
            BattleTurnQteGameModuleRuntime.Id,
            BattleAimShooterGameModuleRuntime.Id);
        var session = new BattleAimShooterCombatSession(
            moduleContext,
            new BattleAimShooterModuleSettings(damagePerHit: 7, requiredHits: 1, maxShots: 3));

        BattleAimShooterShotResult result = session.FireAt("zev");

        Assert.That(result.Success, Is.True);
        Assert.That(result.Completed, Is.True);
        Assert.That(result.OutcomeId, Is.EqualTo("victory"));
        Assert.That(result.AppliedDamage, Is.EqualTo(7));
        Assert.That(session.IsCompleted, Is.True);
        Assert.That(commands.Log, Is.EqualTo(new[] { "damage:zev:7" }));
        Assert.That(events.Log, Is.EqualTo(new[] { "completed:aim_shooter:victory:AfterCurrentModule" }));
    }

    [Test]
    public void FireAtRejectsDeadEnemyWithoutApplyingDamage()
    {
        var context = CreateContext();
        var commands = new RecordingBattleParticipantCommandRunner();
        context.SetService<IBattleParticipantCommandRunner>(commands);
        context.SetService<IBattleSessionStateReader>(CreateSessionWithEnemy("zev", false));
        var moduleContext = new GameModuleRuntimeContext(
            context,
            BattleTurnQteGameModuleRuntime.Id,
            BattleAimShooterGameModuleRuntime.Id);
        var session = new BattleAimShooterCombatSession(moduleContext);

        BattleAimShooterShotResult result = session.FireAt("zev");

        Assert.That(result.Success, Is.False);
        Assert.That(result.Message, Does.Contain("not alive"));
        Assert.That(commands.Log, Is.Empty);
        Assert.That(session.IsCompleted, Is.False);
    }

    [Test]
    public void FireAtReportsFailureOutcomeWhenShotsRunOut()
    {
        var context = CreateContext();
        var commands = new RecordingBattleParticipantCommandRunner();
        var events = new RecordingGameModuleEventSink();
        context.SetService<IBattleParticipantCommandRunner>(commands);
        context.SetService<IGameModuleEventSink>(events);
        context.SetService<IBattleSessionStateReader>(CreateSessionWithEnemy("zev", true));
        var moduleContext = new GameModuleRuntimeContext(
            context,
            BattleTurnQteGameModuleRuntime.Id,
            BattleAimShooterGameModuleRuntime.Id);
        var session = new BattleAimShooterCombatSession(
            moduleContext,
            new BattleAimShooterModuleSettings(damagePerHit: 2, requiredHits: 2, maxShots: 1));

        BattleAimShooterShotResult result = session.FireAt("zev");

        Assert.That(result.Success, Is.True);
        Assert.That(result.Completed, Is.True);
        Assert.That(result.OutcomeId, Is.EqualTo("failed"));
        Assert.That(result.Hits, Is.EqualTo(1));
        Assert.That(result.ShotsRemaining, Is.EqualTo(0));
        Assert.That(events.Log, Is.EqualTo(new[] { "completed:aim_shooter:failed:AfterCurrentModule" }));
    }

    private static ActionExecutionContext CreateContext()
    {
        var context = new ActionExecutionContext();
        context.ModuleId = BattleAimShooterGameModuleRuntime.Id;
        return context;
    }

    private static BattleSessionState CreateSessionWithEnemy(string subjectId, bool alive)
    {
        BattleSessionState state = BattleSessionState.Create(null);
        state.SetParticipants(new[]
        {
            new BattleParticipantSnapshot(
                subjectId,
                BattleParticipantKind.Enemy,
                subjectId,
                alive ? 10 : 0,
                10,
                0,
                0,
                alive,
                false,
                false,
                false,
                false,
                false)
        });
        return state;
    }

    private sealed class RecordingBattleParticipantCommandRunner : IBattleParticipantCommandRunner
    {
        public List<string> Log { get; } = new List<string>();

        public BattleParticipantCommandResult ApplyPureDamage(
            string subjectId,
            int amount,
            ActionExecutionContext context)
        {
            Log.Add("damage:" + subjectId + ":" + amount);
            return BattleParticipantCommandResult.Succeeded(subjectId, amount, amount, 10, 10 - amount);
        }

        public BattleParticipantCommandResult HealHp(
            string subjectId,
            int amount,
            ActionExecutionContext context)
        {
            return BattleParticipantCommandResult.Failed(subjectId, "not used");
        }

        public BattleParticipantCommandResult HealMp(
            string subjectId,
            int amount,
            ActionExecutionContext context)
        {
            return BattleParticipantCommandResult.Failed(subjectId, "not used");
        }

        public BattleParticipantCommandResult ConsumeMp(
            string subjectId,
            int amount,
            ActionExecutionContext context)
        {
            return BattleParticipantCommandResult.Failed(subjectId, "not used");
        }
    }

    private sealed class RecordingGameModuleEventSink : IGameModuleEventSink
    {
        public List<string> Log { get; } = new List<string>();

        public void PublishGameModuleCompleted(
            string moduleId,
            string outcomeId = "",
            BattleRuleTiming timing = BattleRuleTiming.AfterCurrentModule)
        {
            Log.Add("completed:" + moduleId + ":" + outcomeId + ":" + timing);
        }
    }
}
