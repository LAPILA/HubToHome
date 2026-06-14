using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public class EncounterMemorySaveTests
{
    [Test]
    public void ToSaveDataCopiesEncounterMemoryWithoutSharingLists()
    {
        var fixture = new GlobalDataFixture();
        try
        {
            EncounterMemorySaveData memory = fixture.Global.GetOrCreateEncounterMemory(" zev ");
            memory.MeetCount = 2;
            memory.Defeated = true;
            memory.SeenBeatIds.Add("enter_phase2");

            SaveData saveData = fixture.Global.ToSaveData();

            Assert.That(saveData.EncounterMemory.ContainsKey("zev"), Is.True);
            Assert.That(saveData.EncounterMemory["zev"].MeetCount, Is.EqualTo(2));
            Assert.That(saveData.EncounterMemory["zev"].Defeated, Is.True);
            Assert.That(saveData.EncounterMemory["zev"].SeenBeatIds, Is.EqualTo(new[] { "enter_phase2" }));

            memory.SeenBeatIds.Add("after_save_mutation");
            Assert.That(saveData.EncounterMemory["zev"].SeenBeatIds, Is.EqualTo(new[] { "enter_phase2" }));
        }
        finally
        {
            fixture.Dispose();
        }
    }

    [Test]
    public void FromSaveDataRestoresEncounterMemoryWithoutSharingLists()
    {
        var fixture = new GlobalDataFixture();
        var savedMemory = new EncounterMemorySaveData
        {
            EncounterId = " stale_zev ",
            MeetCount = 3,
            Defeated = true,
            SeenBeatIds = new List<string> { "enter_phase2" }
        };
        var saveData = new SaveData();
        saveData.EncounterMemory["zev"] = savedMemory;

        try
        {
            fixture.Global.FromSaveData(saveData);

            Assert.That(fixture.Global.TryGetEncounterMemory("zev", out EncounterMemorySaveData restored), Is.True);
            Assert.That(restored.EncounterId, Is.EqualTo("zev"));
            Assert.That(restored.MeetCount, Is.EqualTo(3));
            Assert.That(restored.Defeated, Is.True);
            Assert.That(restored.SeenBeatIds, Is.EqualTo(new[] { "enter_phase2" }));

            savedMemory.SeenBeatIds.Add("after_load_mutation");
            Assert.That(restored.SeenBeatIds, Is.EqualTo(new[] { "enter_phase2" }));
        }
        finally
        {
            fixture.Dispose();
        }
    }

    [Test]
    public void GetEncounterMemoryDoesNotExposeMutableRuntimeLists()
    {
        var fixture = new GlobalDataFixture();
        try
        {
            fixture.Global.RememberEncounterBeatIds("zev", new[] { "enter_phase2" });

            IReadOnlyDictionary<string, EncounterMemorySaveData> snapshot = fixture.Global.GetEncounterMemory();
            snapshot["zev"].SeenBeatIds.Add("external_mutation");

            Assert.That(fixture.Global.GetEncounterSeenBeatIds("zev"), Is.EqualTo(new[] { "enter_phase2" }));
        }
        finally
        {
            fixture.Dispose();
        }
    }

    [Test]
    public void BattleScenarioRuntimeUsesRememberedEncounterBeatIds()
    {
        BattleScenarioData scenario = MakeScenario();
        try
        {
            var runtime = new BattleScenarioRuntime(scenario, new[] { "enter_phase2" });

            runtime.PublishEnemyHpCrossedBelow(
                "zev",
                51,
                49,
                100,
                BattleRuleTiming.AfterCurrentSkill);
            List<BattleScenarioTrigger> triggers = runtime.Flush(BattleRuleTiming.AfterCurrentSkill);

            Assert.That(triggers, Is.Empty);
        }
        finally
        {
            Object.DestroyImmediate(scenario);
        }
    }

    [Test]
    public void BattleScenarioRuntimeExportsEncounterBeatIdsAfterRuleFires()
    {
        BattleScenarioData scenario = MakeScenario();
        try
        {
            var runtime = new BattleScenarioRuntime(scenario);

            runtime.PublishEnemyHpCrossedBelow(
                "zev",
                51,
                49,
                100,
                BattleRuleTiming.AfterCurrentSkill);
            List<BattleScenarioTrigger> triggers = runtime.Flush(BattleRuleTiming.AfterCurrentSkill);

            Assert.That(triggers, Has.Count.EqualTo(1));
            Assert.That(runtime.ExportEncounterFiredRuleIds(), Does.Contain("enter_phase2"));
        }
        finally
        {
            Object.DestroyImmediate(scenario);
        }
    }

    private static BattleScenarioData MakeScenario()
    {
        BattleScenarioData scenario = ScriptableObject.CreateInstance<BattleScenarioData>();
        scenario.ScenarioId = "zev_first_battle";
        scenario.MemoryKey = "zev";
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
