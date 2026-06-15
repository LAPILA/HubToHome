using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

public class BattleScenarioRuntimeTests
{
    [Test]
    public void DeferredHpCrossingTriggersWhenMatchingTimingIsFlushed()
    {
        BattleScenarioData scenario = MakeScenario(BattleRuleTiming.AfterCurrentSkill);
        var runtime = new BattleScenarioRuntime(scenario);

        try
        {
            List<BattleScenarioTrigger> immediate = runtime.PublishEnemyHpCrossedBelow(
                "zev",
                51,
                49,
                100,
                BattleRuleTiming.AfterCurrentSkill);
            List<BattleScenarioTrigger> flushed = runtime.Flush(BattleRuleTiming.AfterCurrentSkill);

            Assert.That(runtime.HasScenario, Is.True);
            Assert.That(immediate.Count, Is.EqualTo(0));
            Assert.That(flushed.Count, Is.EqualTo(1));
            Assert.That(flushed[0].RuleId, Is.EqualTo("enter_phase2"));
            Assert.That(flushed[0].SequenceId, Is.EqualTo("zev_phase2"));
        }
        finally
        {
            DestroyScenario(scenario);
        }
    }

    [Test]
    public void ImmediateHpCrossingTriggersWithoutQueueing()
    {
        BattleScenarioData scenario = MakeScenario(BattleRuleTiming.Immediate);
        var runtime = new BattleScenarioRuntime(scenario);

        try
        {
            List<BattleScenarioTrigger> immediate = runtime.PublishEnemyHpCrossedBelow(
                "zev",
                51,
                49,
                100,
                BattleRuleTiming.Immediate);
            List<BattleScenarioTrigger> flushed = runtime.Flush(BattleRuleTiming.Immediate);

            Assert.That(immediate.Count, Is.EqualTo(1));
            Assert.That(immediate[0].RuleId, Is.EqualTo("enter_phase2"));
            Assert.That(immediate[0].SequenceId, Is.EqualTo("zev_phase2"));
            Assert.That(flushed.Count, Is.EqualTo(0));
        }
        finally
        {
            DestroyScenario(scenario);
        }
    }

    [Test]
    public void InvalidMaxHpDoesNotPublishTriggers()
    {
        BattleScenarioData scenario = MakeScenario(BattleRuleTiming.Immediate);
        var runtime = new BattleScenarioRuntime(scenario);

        try
        {
            List<BattleScenarioTrigger> triggers = runtime.PublishEnemyHpCrossedBelow(
                "zev",
                51,
                49,
                0,
                BattleRuleTiming.Immediate);

            Assert.That(triggers, Is.Empty);
        }
        finally
        {
            DestroyScenario(scenario);
        }
    }

    [Test]
    public void WrongSubjectDoesNotPublishTriggers()
    {
        BattleScenarioData scenario = MakeScenario(BattleRuleTiming.Immediate);
        var runtime = new BattleScenarioRuntime(scenario);

        try
        {
            List<BattleScenarioTrigger> triggers = runtime.PublishEnemyHpCrossedBelow(
                "other_enemy",
                51,
                49,
                100,
                BattleRuleTiming.Immediate);

            Assert.That(triggers, Is.Empty);
        }
        finally
        {
            DestroyScenario(scenario);
        }
    }

    [Test]
    public void AlreadyBelowThresholdDoesNotPublishTriggers()
    {
        BattleScenarioData scenario = MakeScenario(BattleRuleTiming.Immediate);
        var runtime = new BattleScenarioRuntime(scenario);

        try
        {
            List<BattleScenarioTrigger> triggers = runtime.PublishEnemyHpCrossedBelow(
                "zev",
                49,
                40,
                100,
                BattleRuleTiming.Immediate);

            Assert.That(triggers, Is.Empty);
        }
        finally
        {
            DestroyScenario(scenario);
        }
    }

    [Test]
    public void TryResolveSequenceReturnsFalseWhenMissing()
    {
        BattleScenarioData scenario = MakeScenario(BattleRuleTiming.Immediate);
        var runtime = new BattleScenarioRuntime(scenario);

        try
        {
            bool found = runtime.TryResolveSequence("missing_sequence", out ActionSequenceAsset sequence);

            Assert.That(found, Is.False);
            Assert.That(sequence, Is.Null);
        }
        finally
        {
            DestroyScenario(scenario);
        }
    }

    [Test]
    public void NullScenarioIsSafeNoOp()
    {
        var runtime = new BattleScenarioRuntime(null);

        List<BattleScenarioTrigger> immediate = runtime.PublishEnemyHpCrossedBelow(
            "zev",
            51,
            49,
            100,
            BattleRuleTiming.Immediate);
        List<BattleScenarioTrigger> flushed = runtime.Flush(BattleRuleTiming.Immediate);
        bool found = runtime.TryResolveSequence("zev_phase2", out ActionSequenceAsset sequence);

        Assert.That(runtime.HasScenario, Is.False);
        Assert.That(immediate, Is.Empty);
        Assert.That(flushed, Is.Empty);
        Assert.That(found, Is.False);
        Assert.That(sequence, Is.Null);
    }

    [Test]
    public void CreatesBattleSessionStateFromScenarioOpeningModule()
    {
        BattleScenarioData scenario = MakeScenario(BattleRuleTiming.Immediate);
        scenario.OpeningModule = "aim_shooter";
        var runtime = new BattleScenarioRuntime(scenario);

        try
        {
            Assert.That(runtime.SessionState.ScenarioId, Is.EqualTo("zev_first_battle"));
            Assert.That(runtime.SessionState.PrimaryMode, Is.EqualTo("battle"));
            Assert.That(runtime.SessionState.OpeningModule, Is.EqualTo("aim_shooter"));
            Assert.That(runtime.SessionState.CurrentModuleId, Is.EqualTo("aim_shooter"));
        }
        finally
        {
            DestroyScenario(scenario);
        }
    }

    [Test]
    public void NullScenarioSessionStateDefaultsToTurnQte()
    {
        var runtime = new BattleScenarioRuntime(null);

        Assert.That(runtime.SessionState.PrimaryMode, Is.EqualTo("battle"));
        Assert.That(runtime.SessionState.OpeningModule, Is.EqualTo(BattleTurnQteGameModuleRuntime.Id));
        Assert.That(runtime.SessionState.CurrentModuleId, Is.EqualTo(BattleTurnQteGameModuleRuntime.Id));
    }

    [Test]
    public void SessionStateStoresReadableParticipantSnapshots()
    {
        var runtime = new BattleScenarioRuntime(null);
        var participants = new[]
        {
            new BattleParticipantSnapshot(
                "player_001",
                BattleParticipantKind.Player,
                "플레이어",
                75,
                100,
                20,
                40,
                true,
                false,
                false,
                false,
                false,
                false),
            new BattleParticipantSnapshot(
                "zev",
                BattleParticipantKind.Enemy,
                "ZEV",
                49,
                100,
                0,
                0,
                true,
                false,
                true,
                false,
                false,
                false)
        };

        runtime.SessionState.SetParticipants(participants);

        Assert.That(runtime.SessionState.Participants.Count, Is.EqualTo(2));
        Assert.That(runtime.SessionState.TryGetParticipant("zev", out BattleParticipantSnapshot zev), Is.True);
        Assert.That(zev.Kind, Is.EqualTo(BattleParticipantKind.Enemy));
        Assert.That(zev.DisplayName, Is.EqualTo("ZEV"));
        Assert.That(zev.HpRatio, Is.EqualTo(0.49f).Within(0.001f));
        Assert.That(zev.IsStunned, Is.True);
    }

    [Test]
    public void SessionStateReplacesParticipantsOnUpdate()
    {
        var runtime = new BattleScenarioRuntime(null);

        runtime.SessionState.SetParticipants(new[]
        {
            new BattleParticipantSnapshot("zev", BattleParticipantKind.Enemy, "ZEV", 100, 100, 0, 0, true, false, false, false, false, false)
        });
        runtime.SessionState.SetParticipants(new[]
        {
            new BattleParticipantSnapshot("player_001", BattleParticipantKind.Player, "플레이어", 100, 100, 30, 30, true, false, false, false, false, false)
        });

        Assert.That(runtime.SessionState.Participants.Count, Is.EqualTo(1));
        Assert.That(runtime.SessionState.TryGetParticipant("zev", out _), Is.False);
        Assert.That(runtime.SessionState.TryGetParticipant("player_001", out BattleParticipantSnapshot player), Is.True);
        Assert.That(player.MpRatio, Is.EqualTo(1f));
    }

    [Test]
    public void SessionStateStoresBattleScopedFlags()
    {
        var runtime = new BattleScenarioRuntime(null);

        Assert.That(runtime.SessionState.SetFlag("phase.two", "entered"), Is.True);
        Assert.That(runtime.SessionState.SetFlag("shooter.unlocked", string.Empty), Is.True);

        Assert.That(runtime.SessionState.HasFlag("phase.two"), Is.True);
        Assert.That(runtime.SessionState.TryGetFlagValue("phase.two", out string phaseValue), Is.True);
        Assert.That(phaseValue, Is.EqualTo("entered"));
        Assert.That(runtime.SessionState.TryGetFlagValue("shooter.unlocked", out string shooterValue), Is.True);
        Assert.That(shooterValue, Is.EqualTo("true"));
        Assert.That(runtime.SessionState.Flags.Count, Is.EqualTo(2));
    }

    [Test]
    public void SessionStateCanReplaceAndClearBattleFlags()
    {
        var runtime = new BattleScenarioRuntime(null);

        runtime.SessionState.SetFlag("phase.two", "pending");
        runtime.SessionState.SetFlag("phase.two", "entered");
        bool cleared = runtime.SessionState.ClearFlag("phase.two");

        Assert.That(cleared, Is.True);
        Assert.That(runtime.SessionState.HasFlag("phase.two"), Is.False);
        Assert.That(runtime.SessionState.Flags.Count, Is.EqualTo(0));
    }

    [Test]
    public void ParticipantSnapshotFromEnemyUsesScenarioSubjectId()
    {
        GameObject enemyObject = new GameObject("EnemyRuntimeObject");
        EnemyCharacter enemy = enemyObject.AddComponent<EnemyCharacter>();
        EnemyData data = ScriptableObject.CreateInstance<EnemyData>();
        data.EnemyId = "zev";
        data.EnemyName = "ZEV";
        data.MaxHP = 200;

        try
        {
            enemy.Setup(data);
            enemy.TakePureDamage(50);
            BattleParticipantSnapshot snapshot = BattleParticipantSnapshot.FromEnemy(enemy);

            Assert.That(snapshot.SubjectId, Is.EqualTo("zev"));
            Assert.That(snapshot.Kind, Is.EqualTo(BattleParticipantKind.Enemy));
            Assert.That(snapshot.DisplayName, Is.EqualTo("ZEV"));
            Assert.That(snapshot.CurrentHp, Is.EqualTo(150));
            Assert.That(snapshot.MaxHp, Is.EqualTo(200));
            Assert.That(snapshot.HpRatio, Is.EqualTo(0.75f).Within(0.001f));
        }
        finally
        {
            Object.DestroyImmediate(data);
            Object.DestroyImmediate(enemyObject);
        }
    }

    [Test]
    public void BattleParticipantCommandResultKeepsAppliedAmountAndValues()
    {
        BattleParticipantCommandResult result = BattleParticipantCommandResult.Succeeded(
            "zev",
            30,
            25,
            100,
            75);

        Assert.That(result.Success, Is.True);
        Assert.That(result.SubjectId, Is.EqualTo("zev"));
        Assert.That(result.RequestedAmount, Is.EqualTo(30));
        Assert.That(result.AppliedAmount, Is.EqualTo(25));
        Assert.That(result.PreviousValue, Is.EqualTo(100));
        Assert.That(result.CurrentValue, Is.EqualTo(75));
    }

    [Test]
    public void BattleParticipantCommandResultFailureKeepsMessage()
    {
        BattleParticipantCommandResult result = BattleParticipantCommandResult.Failed(
            "missing",
            "Battle participant was not found.");

        Assert.That(result.Success, Is.False);
        Assert.That(result.SubjectId, Is.EqualTo("missing"));
        Assert.That(result.Message, Is.EqualTo("Battle participant was not found."));
    }

    private static BattleScenarioData MakeScenario(BattleRuleTiming timing)
    {
        BattleScenarioData scenario = ScriptableObject.CreateInstance<BattleScenarioData>();
        scenario.ScenarioId = "zev_first_battle";
        scenario.MemoryKey = "zev";
        scenario.Rules.Add(new BattleEventRuleData
        {
            RuleId = "enter_phase2",
            EventType = BattleEventType.EnemyHpCrossedBelow,
            Timing = timing,
            Once = BattleRuleOnceMode.PerBattle,
            SubjectId = "zev",
            ThresholdRatio = 0.5f,
            SequenceId = "zev_phase2"
        });
        scenario.Sequences.Add(MakeSequence("zev_phase2"));
        return scenario;
    }

    private static ActionSequenceAsset MakeSequence(string sequenceId)
    {
        ActionSequenceAsset sequence = ScriptableObject.CreateInstance<ActionSequenceAsset>();
        sequence.SequenceId = sequenceId;
        return sequence;
    }

    private static void DestroyScenario(BattleScenarioData scenario)
    {
        if (scenario == null)
        {
            return;
        }

        for (int i = 0; i < scenario.Sequences.Count; i++)
        {
            UnityEngine.Object.DestroyImmediate(scenario.Sequences[i]);
        }

        UnityEngine.Object.DestroyImmediate(scenario);
    }
}
