using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

public sealed class GlobalDataRuntimeStateTests
{
    private GlobalDataManager _previousInstance;
    private GameObject _globalObject;
    private GlobalDataManager _global;
    private readonly List<UnityEngine.Object> _createdObjects = new List<UnityEngine.Object>();

    [SetUp]
    public void SetUp()
    {
        _previousInstance = GlobalDataManager.Instance;
        SetGlobalInstance(null);

        _globalObject = new GameObject(nameof(GlobalDataRuntimeStateTests));
        _global = _globalObject.AddComponent<GlobalDataManager>();
    }

    [TearDown]
    public void TearDown()
    {
        for (int i = _createdObjects.Count - 1; i >= 0; i--)
        {
            if (_createdObjects[i] != null)
                UnityEngine.Object.DestroyImmediate(_createdObjects[i]);
        }

        _createdObjects.Clear();
        UnityEngine.Object.DestroyImmediate(_globalObject);
        SetGlobalInstance(_previousInstance);
    }

    [Test]
    public void SetFlag_ValueChanges_RaisesNormalizedOldAndNewValueOnce()
    {
        var changes = new List<(string Key, int OldValue, int NewValue)>();
        _global.FlagChanged += (key, oldValue, newValue) =>
            changes.Add((key, oldValue, newValue));

        _global.SetFlag("  station.power  ", 1);
        _global.SetFlag("station.power", 1);

        Assert.That(
            changes,
            Is.EqualTo(new[] { ("station.power", 0, 1) }));
        Assert.That(_global.GetFlag(" station.power "), Is.EqualTo(1));
    }

    [Test]
    public void SetFlag_WhenSubscriberThrows_NotifiesRemainingSubscribers()
    {
        int notificationCount = 0;
        _global.FlagChanged += (_, _, _) => throw new InvalidOperationException("observer failure");
        _global.FlagChanged += (_, _, _) => notificationCount++;

        LogAssert.Expect(LogType.Exception, new Regex("observer failure"));

        Assert.DoesNotThrow(() => _global.SetFlag("station.power", 1));
        Assert.That(notificationCount, Is.EqualTo(1));
        Assert.That(_global.GetFlag("station.power"), Is.EqualTo(1));
    }

    [Test]
    public void FromSaveData_NotifiesOnlyChangedFlags()
    {
        _global.SetFlag("removed.flag", 1);
        _global.SetFlag("unchanged.flag", 3);
        var changes = new List<(string Key, int OldValue, int NewValue)>();
        _global.FlagChanged += (key, oldValue, newValue) =>
            changes.Add((key, oldValue, newValue));
        var data = new SaveData
        {
            eventFlags = new Dictionary<string, int>
            {
                ["unchanged.flag"] = 3,
                ["added.flag"] = 2
            }
        };

        _global.FromSaveData(data);

        Assert.That(changes, Has.Count.EqualTo(2));
        Assert.That(changes, Does.Contain(("removed.flag", 1, 0)));
        Assert.That(changes, Does.Contain(("added.flag", 0, 2)));
    }
    [Test]
    public void InitializePartyFromScene_NewParty_ReturnsBoundSaveObject()
    {
        PlayerCharacter player = CreatePlayer("player.hero");

        CharacterSaveData saved = _global.InitializePartyFromScene(player);
        player.LoadDataFromGlobal(saved);
        player.TakePureDamage(5);
        player.SaveDataToGlobal();

        Assert.That(saved, Is.SameAs(_global.Party[0]));
        Assert.That(saved.CharacterDataID, Is.EqualTo("player.hero"));
        Assert.That(saved.HP, Is.EqualTo(player.CurrentHP));
        Assert.That(_global.Party, Has.Count.EqualTo(1));
    }

    [Test]
    public void InitializePartyFromScene_StableIdMatch_ReusesExactMember()
    {
        var other = new CharacterSaveData { CharacterDataID = "party.other", HP = 11 };
        var expected = new CharacterSaveData { CharacterDataID = "player.hero", HP = 37 };
        _global.Party.Add(other);
        _global.Party.Add(expected);
        PlayerCharacter player = CreatePlayer("player.hero");

        CharacterSaveData actual = _global.InitializePartyFromScene(player);

        Assert.That(actual, Is.SameAs(expected));
        Assert.That(_global.Party, Has.Count.EqualTo(2));
    }

    [Test]
    public void InitializePartyFromScene_LegacyLeader_MigratesStableIdWithoutDuplication()
    {
        var legacy = new CharacterSaveData
        {
            CharacterDataID = string.Empty,
            CharacterID = "Legacy Hero",
            HP = 42
        };
        _global.Party.Add(legacy);
        PlayerCharacter player = CreatePlayer("player.hero");

        CharacterSaveData actual = _global.InitializePartyFromScene(player);

        Assert.That(actual, Is.SameAs(legacy));
        Assert.That(actual.CharacterDataID, Is.EqualTo("player.hero"));
        Assert.That(_global.Party, Has.Count.EqualTo(1));
    }

    [Test]
    public void InitializePartyFromScene_DifferentStableParty_DoesNotAppendDuplicate()
    {
        _global.Party.Add(new CharacterSaveData { CharacterDataID = "party.other" });
        PlayerCharacter player = CreatePlayer("player.hero");

        LogAssert.Expect(LogType.Warning, new Regex("player.hero"));
        CharacterSaveData actual = _global.InitializePartyFromScene(player);

        Assert.That(actual, Is.Null);
        Assert.That(_global.Party, Has.Count.EqualTo(1));
    }

    private PlayerCharacter CreatePlayer(string characterDataId)
    {
        CharacterData data = ScriptableObject.CreateInstance<CharacterData>();
        data.CharacterID = characterDataId;
        data.DisplayName = "Hero";
        data.BaseMaxHP = 80;
        data.BaseMaxAP = 25;
        data.BaseATK = 9;
        data.BaseDEF = 4;
        data.BaseSPD = 7;
        _createdObjects.Add(data);

        var playerObject = new GameObject("Player");
        PlayerCharacter player = playerObject.AddComponent<PlayerCharacter>();
        player.SetCharacterData(data);
        _createdObjects.Add(playerObject);
        return player;
    }

    private static void SetGlobalInstance(GlobalDataManager instance)
    {
        PropertyInfo property = typeof(GlobalDataManager).GetProperty(
            nameof(GlobalDataManager.Instance),
            BindingFlags.Public | BindingFlags.Static);
        property.SetValue(null, instance);
    }
}
