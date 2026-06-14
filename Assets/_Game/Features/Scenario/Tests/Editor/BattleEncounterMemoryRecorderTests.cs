using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public class BattleEncounterMemoryRecorderTests
{
    [Test]
    public void RecordBattleStartedIncrementsMeetCountForScenarioMemoryKey()
    {
        var fixture = new GlobalDataFixture();
        BattleScenarioData scenario = MakeScenario(" zev ");
        try
        {
            BattleEncounterMemoryRecorder.RecordBattleStarted(scenario, fixture.Global, null);
            BattleEncounterMemoryRecorder.RecordBattleStarted(scenario, fixture.Global, null);

            Assert.That(fixture.Global.TryGetEncounterMemory("zev", out EncounterMemorySaveData memory), Is.True);
            Assert.That(memory.MeetCount, Is.EqualTo(2));
        }
        finally
        {
            Object.DestroyImmediate(scenario);
            fixture.Dispose();
        }
    }

    [Test]
    public void CreateRuntimeSeedsPerEncounterRulesFromSavedMemory()
    {
        var fixture = new GlobalDataFixture();
        BattleScenarioData scenario = MakeScenario("zev");
        try
        {
            fixture.Global.RememberEncounterBeatIds("zev", new[] { "enter_phase2" });

            BattleScenarioRuntime runtime = BattleEncounterMemoryRecorder.CreateRuntime(scenario, fixture.Global, null);
            FirePhase2Rule(runtime);

            Assert.That(runtime.Flush(BattleRuleTiming.AfterCurrentSkill), Is.Empty);
        }
        finally
        {
            Object.DestroyImmediate(scenario);
            fixture.Dispose();
        }
    }

    [Test]
    public void RecordBattleResultStoresFiredRulesAndDefeatedOnVictory()
    {
        var fixture = new GlobalDataFixture();
        BattleScenarioData scenario = MakeScenario("zev");
        try
        {
            BattleScenarioRuntime runtime = new BattleScenarioRuntime(scenario);
            FirePhase2Rule(runtime);
            Assert.That(runtime.Flush(BattleRuleTiming.AfterCurrentSkill), Has.Count.EqualTo(1));

            BattleEncounterMemoryRecorder.RecordBattleResult(
                scenario,
                runtime,
                fixture.Global,
                null,
                true);

            Assert.That(fixture.Global.TryGetEncounterMemory("zev", out EncounterMemorySaveData memory), Is.True);
            Assert.That(memory.Defeated, Is.True);
            Assert.That(memory.SeenBeatIds, Is.EqualTo(new[] { "enter_phase2" }));
        }
        finally
        {
            Object.DestroyImmediate(scenario);
            fixture.Dispose();
        }
    }

    [Test]
    public void RecordBattleResultCanUseFallbackEncounterIdWhenScenarioHasNoMemoryKey()
    {
        var fixture = new GlobalDataFixture();
        BattleScenarioData scenario = MakeScenario("");
        try
        {
            BattleScenarioRuntime runtime = new BattleScenarioRuntime(scenario);
            FirePhase2Rule(runtime);
            runtime.Flush(BattleRuleTiming.AfterCurrentSkill);

            BattleEncounterMemoryRecorder.RecordBattleResult(
                scenario,
                runtime,
                fixture.Global,
                "overworld_zev",
                false);

            Assert.That(fixture.Global.TryGetEncounterMemory("overworld_zev", out EncounterMemorySaveData memory), Is.True);
            Assert.That(memory.Defeated, Is.False);
            Assert.That(memory.SeenBeatIds, Is.EqualTo(new[] { "enter_phase2" }));
        }
        finally
        {
            Object.DestroyImmediate(scenario);
            fixture.Dispose();
        }
    }

    private static void FirePhase2Rule(BattleScenarioRuntime runtime)
    {
        runtime.PublishEnemyHpCrossedBelow(
            "zev",
            51,
            49,
            100,
            BattleRuleTiming.AfterCurrentSkill);
    }

    private static BattleScenarioData MakeScenario(string memoryKey)
    {
        BattleScenarioData scenario = ScriptableObject.CreateInstance<BattleScenarioData>();
        scenario.ScenarioId = "zev_first_battle";
        scenario.MemoryKey = memoryKey;
        scenario.Rules.Add(new BattleEventRuleData
        {
            RuleId = "enter_phase2",
            EventType = BattleEventType.EnemyHpCrossedBelow,
            Timing = BattleRuleTiming.AfterCurrentSkill,
            Once = BattleRuleOnceMode.PerEncounterMemory,
            SubjectId = "zev",
            ThresholdRatio = 0.5f,
            SequenceId = "zev_phase2"
        });

        return scenario;
    }

    private sealed class GlobalDataFixture
    {
        private readonly GlobalDataManager _previousGlobalDataManagerInstance;
        private readonly GameObject _gameObject;

        public GlobalDataFixture()
        {
            _previousGlobalDataManagerInstance = GlobalDataManager.Instance;
            SetGlobalDataManagerInstance(null);
            _gameObject = new GameObject("GlobalDataManagerTest");
            Global = _gameObject.AddComponent<GlobalDataManager>();
        }

        public GlobalDataManager Global { get; }

        public void Dispose()
        {
            Object.DestroyImmediate(_gameObject);
            SetGlobalDataManagerInstance(_previousGlobalDataManagerInstance);
        }

        private static void SetGlobalDataManagerInstance(GlobalDataManager instance)
        {
            PropertyInfo property = typeof(GlobalDataManager).GetProperty(
                "Instance",
                BindingFlags.Public | BindingFlags.Static);
            property.GetSetMethod(true).Invoke(null, new object[] { instance });
        }
    }
}
