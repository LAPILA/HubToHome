using System.Collections.Generic;
using Newtonsoft.Json;
using NUnit.Framework;
using UnityEngine;

public class OverworldEnemySaveTests
{
    private GameObject _globalObject;
    private GlobalDataManager _global;

    [SetUp]
    public void SetUp()
    {
        _globalObject = new GameObject("GlobalDataManager");
        _global = _globalObject.AddComponent<GlobalDataManager>();
    }

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(_globalObject);
    }

    [Test]
    public void DefeatedEnemyStateRoundTripsThroughSaveData()
    {
        _global.MarkOverworldEnemyDefeated("map.enemy.1", "TestMap");

        SaveData save = _global.ToSaveData();
        _global.FromSaveData(save);

        Assert.That(_global.TryGetOverworldEnemyState("map.enemy.1", out OverworldEnemyRuntimeState state), Is.True);
        Assert.That(state.IsDefeated, Is.True);
        Assert.That(state.SceneName, Is.EqualTo("TestMap"));
    }

    [Test]
    public void OldSaveWithoutEnemyDictionaryLoadsSafely()
    {
        SaveData oldSave = new SaveData { OverworldEnemies = null };
        Assert.DoesNotThrow(() => _global.FromSaveData(oldSave));
        Assert.That(_global.TryGetOverworldEnemyState("missing", out _), Is.False);
    }
    [Test]
    public void PartySaveSnapshotDoesNotShareEquippedSkillLists()
    {
        var member = new CharacterSaveData
        {
            CharacterDataID = "player_001",
            EquippedSkillIDs = new List<string> { "player.combo_slash" }
        };
        _global.Party.Add(member);

        SaveData snapshot = _global.ToSaveData();
        member.EquippedSkillIDs[0] = "runtime.changed";

        Assert.That(snapshot.PartyData[0].EquippedSkillIDs[0], Is.EqualTo("player.combo_slash"));

        _global.FromSaveData(snapshot);
        snapshot.PartyData[0].EquippedSkillIDs[0] = "snapshot.changed";
        Assert.That(_global.Party[0].EquippedSkillIDs[0], Is.EqualTo("player.combo_slash"));
    }

    [Test]
    public void JsonRoundTripPersistsEnemyStateInventoryAndPartyProgression()
    {
        _global.MarkOverworldEnemyDefeated("map.enemy.json", "TestMap");
        _global.AddItem("consumable.small_potion", 3);
        _global.Party.Add(new CharacterSaveData
        {
            CharacterDataID = "player_001",
            Level = 4,
            EXP = 25,
            EquippedSkillIDs = new List<string> { "player.crash" }
        });

        string json = JsonConvert.SerializeObject(_global.ToSaveData());
        SaveData restored = JsonConvert.DeserializeObject<SaveData>(json);
        _global.FromSaveData(restored);

        Assert.That(_global.GetItemCount("consumable.small_potion"), Is.EqualTo(3));
        Assert.That(_global.Party[0].Level, Is.EqualTo(4));
        Assert.That(_global.Party[0].EXP, Is.EqualTo(25));
        Assert.That(_global.Party[0].EquippedSkillIDs, Is.EqualTo(new[] { "player.crash" }));
        Assert.That(_global.TryGetOverworldEnemyState("map.enemy.json", out OverworldEnemyRuntimeState state), Is.True);
        Assert.That(state.IsDefeated, Is.True);
    }

    [Test]
    public void GrantedBattleRewardsSurviveJsonSaveRoundTrip()
    {
        EnemyData enemy = ScriptableObject.CreateInstance<EnemyData>();
        try
        {
            enemy.EXPReward = 40;
            enemy.GoldReward = 15;
            _global.Party.Add(new CharacterSaveData
            {
                CharacterDataID = string.Empty,
                CharacterID = "TestPlayer",
                Level = 1,
                EXP = 0,
                HP = 100,
                MaxHP = 100,
                MP = 20,
                MaxMP = 20,
                ATK = 10,
                DEF = 5,
                SPD = 10
            });

            BattleRewardResult result = BattleRewardService.Grant(new[] { enemy }, _global);
            string json = JsonConvert.SerializeObject(_global.ToSaveData());
            _global.FromSaveData(JsonConvert.DeserializeObject<SaveData>(json));

            Assert.That(result.Experience, Is.EqualTo(40));
            Assert.That(result.Gold, Is.EqualTo(15));
            Assert.That(_global.Money, Is.EqualTo(15));
            Assert.That(_global.Party, Has.Count.EqualTo(1));
            Assert.That(_global.Party[0].EXP, Is.EqualTo(40));
        }
        finally
        {
            Object.DestroyImmediate(enemy);
        }
    }
}
