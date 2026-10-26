using System.Reflection;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

public class GlobalDataManagerSaveCompatibilityTests
{
    [Test]
    public void RecruitmentPersistsOnceWithSharedProgressAndIndependentPoints()
    {
        FieldInfo cache = typeof(CharacterDatabase).GetField("_cache", BindingFlags.NonPublic | BindingFlags.Static);
        object previousCache = cache.GetValue(null);
        CharacterData leader = ScriptableObject.CreateInstance<CharacterData>();
        CharacterData ally = ScriptableObject.CreateInstance<CharacterData>();
        leader.CharacterID = "test.leader";
        ally.CharacterID = "test.ally";
        cache.SetValue(null, new Dictionary<string, CharacterData> { [leader.CharacterID] = leader, [ally.CharacterID] = ally });
        try
        {
            var save = new SaveData();
            save.PartyData.Add(new CharacterSaveData { CharacterDataID = leader.CharacterID, Level = 5, EXP = 17, HP = 2, AP = 1 });
            _global.FromSaveData(save);
            Assert.That(_global.TryRecruitPartyMember(ally, out var member, out var error), Is.True, error);
            int points = member.Growth.AvailableSkillPoints;
            Assert.That(_global.TryRecruitPartyMember(ally, out var duplicate, out error), Is.True, error);
            Assert.That(duplicate, Is.SameAs(member));
            Assert.That(_global.Party.Count, Is.EqualTo(2));
            Assert.That(member.Level, Is.EqualTo(5));
            Assert.That(member.EXP, Is.EqualTo(17));
            Assert.That(member.Growth.AvailableSkillPoints, Is.EqualTo(points));
            Assert.That(_global.Party[0].HP, Is.EqualTo(2));
            SaveData snapshot = _global.ToSaveData();
            _global.FromSaveData(snapshot);
            Assert.That(_global.Party.Count, Is.EqualTo(2));
            Assert.That(_global.Party[1].CharacterDataID, Is.EqualTo(ally.CharacterID));
            Assert.That(_global.Party[1].Growth.AvailableSkillPoints, Is.EqualTo(points));
        }
        finally
        {
            cache.SetValue(null, previousCache);
            Object.DestroyImmediate(leader);
            Object.DestroyImmediate(ally);
        }
    }

    [Test]
    public void UnregisteredCompanionCannotCreateAnUnresolvableSaveMember()
    {
        var data = ScriptableObject.CreateInstance<CharacterData>();
        data.CharacterID = "unregistered.recruit.test";
        _global.Party.Add(new CharacterSaveData { CharacterDataID = "test.leader" });
        try
        {
            Assert.That(_global.TryRecruitPartyMember(data, out _, out _), Is.False);
            Assert.That(_global.Party.Count, Is.EqualTo(1));
        }
        finally { Object.DestroyImmediate(data); }
    }

    [Test]
    public void ToSaveDataNormalizesOnlyDetachedSnapshot()
    {
        var member = new CharacterSaveData
        {
            CharacterDataID = "unregistered.snapshot.test",
            EquippedEquipmentIDs = null,
            HP = 150,
            MaxHP = 100
        };
        _global.Party.Add(member);
        SaveData snapshot = _global.ToSaveData();
        Assert.That(snapshot.PartyData[0], Is.Not.SameAs(member));
        Assert.That(snapshot.PartyData[0].EquippedEquipmentIDs.Count, Is.EqualTo(6));
        Assert.That(snapshot.PartyData[0].HP, Is.EqualTo(100));
        Assert.That(member.EquippedEquipmentIDs, Is.Null);
        Assert.That(member.HP, Is.EqualTo(150));
    }

    private GlobalDataManager _previousInstance;
    private GameObject _gameObject;
    private GlobalDataManager _global;

    [SetUp]
    public void SetUp()
    {
        _previousInstance = GlobalDataManager.Instance;
        SetInstance(null);
        _gameObject = new GameObject("GlobalDataManagerSaveCompatibilityTests");
        _global = _gameObject.AddComponent<GlobalDataManager>();
    }

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(_gameObject);
        SetInstance(_previousInstance);
    }

    [Test]
    public void FromSaveDataAcceptsMissingOptionalCollections()
    {
        var data = new SaveData
        {
            currentScene = " ",
            InventoryDict = null,
            eventFlags = null,
            EncounterMemory = null,
            PartyData = null
        };

        Assert.DoesNotThrow(() => _global.FromSaveData(data));
        Assert.That(_global.SpawnScene, Is.EqualTo(SceneName.Overworld));
        Assert.That(_global.GetInventory(), Is.Empty);
        Assert.That(_global.Party, Is.Empty);
    }

    [Test]
    public void FromSaveDataIgnoresNullInput()
    {
        string originalScene = _global.SpawnScene;

        Assert.DoesNotThrow(() => _global.FromSaveData(null));
        Assert.That(_global.SpawnScene, Is.EqualTo(originalScene));
    }

    [TestCase("옛 플레이어 이름")]
    [TestCase("WIZZEL")]
    [TestCase("")]
    [TestCase(null)]
    public void LegacySavedNameCannotOverrideDefaultIdentityOrMutateSource(string legacyName)
    {
        var data = new SaveData { playerName = legacyName };
        data.PartyData.Add(new CharacterSaveData
        {
            CharacterDataID = "legacy.companion",
            CharacterID = "동료의 이름"
        });

        _global.FromSaveData(data);

        Assert.That(_global.PlayerName, Is.EqualTo(GlobalDataManager.DefaultPlayerName));
        Assert.That(_global.ToSaveData().playerName, Is.EqualTo(GlobalDataManager.DefaultPlayerName));
        Assert.That(data.playerName, Is.EqualTo(legacyName), "옛 저장 형식을 파괴하거나 원본을 수정하지 않습니다.");
        Assert.That(_global.Party[0].CharacterID, Is.EqualTo("동료의 이름"), "다른 동료의 이름은 일괄 치환하지 않습니다.");
    }

    private static void SetInstance(GlobalDataManager instance)
    {
        PropertyInfo property = typeof(GlobalDataManager).GetProperty(
            nameof(GlobalDataManager.Instance),
            BindingFlags.Public | BindingFlags.Static);
        property.SetValue(null, instance);
    }
}

