using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public sealed class BattlePartyWaveRuntimeTests
{
    private readonly List<Object> _ownedObjects = new List<Object>();
    private BattleManager _manager;
    private PositionManager _positionManager;
    private List<PlayerCharacter> _front;
    private List<PlayerCharacter> _reserve;

    [SetUp]
    public void SetUp()
    {
        _positionManager = CreateComponent<PositionManager>("Party Wave Positions");
        var positions = new List<Transform>();
        for (int i = 0; i < 3; i++)
        {
            GameObject position = CreateObject("Player Slot " + i);
            position.transform.position = new Vector3(-6f + (i * 2f), -1f, 0f);
            positions.Add(position.transform);
        }
        SetPrivateField(_positionManager, "_playerDefaultPos", positions);

        _manager = CreateComponent<BattleManager>("Party Wave Battle Manager");
        _front = new List<PlayerCharacter>();
        _reserve = new List<PlayerCharacter>();
        for (int i = 0; i < 3; i++)
        {
            PlayerCharacter front = CreatePlayer("front." + i, true);
            front.TakePureDamage(front.MaxHP);
            _front.Add(front);
            _manager._playerParty.Add(front);

            PlayerCharacter reserve = CreatePlayer("reserve." + i, false);
            _reserve.Add(reserve);
            GetPrivateList("_reserveParty").Add(reserve);
        }

        List<PlayerCharacter> roster = GetPrivateList("_battlePartyRoster");
        roster.AddRange(_front);
        roster.AddRange(_reserve);
    }

    [TearDown]
    public void TearDown()
    {
        for (int i = _ownedObjects.Count - 1; i >= 0; i--)
        {
            if (_ownedObjects[i] != null)
                Object.DestroyImmediate(_ownedObjects[i]);
        }
        _ownedObjects.Clear();
    }

    [Test]
    public void PromoteReserveWave_DeactivatesDefeatedFrontAndActivatesPreparedReserve()
    {
        GetPrivateTurnQueue().Add(_front[0]);

        bool promoted = InvokePromoteReserveWave();

        Assert.That(promoted, Is.True);
        Assert.That(_manager._playerParty, Is.EqualTo(_reserve));
        Assert.That(GetPrivateList("_reserveParty"), Is.Empty);
        Assert.That(GetPrivateTurnQueue(), Is.Empty);
        for (int i = 0; i < 3; i++)
        {
            Assert.That(_front[i].gameObject.activeSelf, Is.False);
            Assert.That(_reserve[i].gameObject.activeSelf, Is.True);
            Assert.That(
                _reserve[i].transform.position,
                Is.EqualTo(_positionManager.GetPlayerDefaultPos(i)));
        }
    }

    [Test]
    public void PromoteReserveWave_WhenFrontStillAlive_DoesNothing()
    {
        PlayerCharacter aliveFront = CreatePlayer("front.alive", true);
        _manager._playerParty[0] = aliveFront;

        bool promoted = InvokePromoteReserveWave();

        Assert.That(promoted, Is.False);
        Assert.That(_manager._playerParty[0], Is.SameAs(aliveFront));
        Assert.That(GetPrivateList("_reserveParty"), Has.Count.EqualTo(3));
        for (int i = 0; i < _reserve.Count; i++)
            Assert.That(_reserve[i].gameObject.activeSelf, Is.False);
    }

    [Test]
    public void FindUniquePartySave_ReturnsOnlyUnambiguousCharacterId()
    {
        var saves = new List<CharacterSaveData>
        {
            new CharacterSaveData { CharacterDataID = "weasel" },
            new CharacterSaveData { CharacterDataID = "wolf" }
        };

        CharacterSaveData match = InvokeFindUniquePartySave(saves, " wolf ");

        Assert.That(match, Is.SameAs(saves[1]));
    }

    [Test]
    public void FindUniquePartySave_DuplicateCharacterIdReturnsNoMatch()
    {
        var saves = new List<CharacterSaveData>
        {
            new CharacterSaveData { CharacterDataID = "wolf" },
            new CharacterSaveData { CharacterDataID = "wolf" }
        };

        CharacterSaveData match = InvokeFindUniquePartySave(saves, "wolf");

        Assert.That(match, Is.Null);
    }

    private PlayerCharacter CreatePlayer(string characterId, bool active)
    {
        GameObject playerObject = CreateObject(characterId);
        PlayerCharacter player = playerObject.AddComponent<PlayerCharacter>();
        CharacterData data = ScriptableObject.CreateInstance<CharacterData>();
        data.CharacterID = characterId;
        data.DisplayName = characterId;
        data.BaseStats = new StatBlock
        {
            MaxHP = 100,
            MaxAP = 50,
            ATK = 10,
            DEF = 5,
            SPD = 10
        };
        _ownedObjects.Add(data);
        player.SetCharacterData(data);
        playerObject.SetActive(active);
        return player;
    }

    private GameObject CreateObject(string name)
    {
        var gameObject = new GameObject(name);
        _ownedObjects.Add(gameObject);
        return gameObject;
    }

    private T CreateComponent<T>(string name) where T : Component
    {
        return CreateObject(name).AddComponent<T>();
    }

    private List<PlayerCharacter> GetPrivateList(string fieldName)
    {
        FieldInfo field = typeof(BattleManager).GetField(
            fieldName,
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null);
        return (List<PlayerCharacter>)field.GetValue(_manager);
    }

    private List<CharacterBase> GetPrivateTurnQueue()
    {
        FieldInfo field = typeof(BattleManager).GetField(
            "_turnQueue",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null);
        return (List<CharacterBase>)field.GetValue(_manager);
    }

    private bool InvokePromoteReserveWave()
    {
        MethodInfo method = typeof(BattleManager).GetMethod(
            "TryPromoteReservePartyWave",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(method, Is.Not.Null);
        return (bool)method.Invoke(_manager, null);
    }

    private static CharacterSaveData InvokeFindUniquePartySave(
        IReadOnlyList<CharacterSaveData> party,
        string characterId)
    {
        MethodInfo method = typeof(BattleManager).GetMethod(
            "FindUniquePartySave",
            BindingFlags.Static | BindingFlags.NonPublic);
        Assert.That(method, Is.Not.Null);
        return (CharacterSaveData)method.Invoke(null, new object[] { party, characterId });
    }

    private static void SetPrivateField(object target, string fieldName, object value)
    {
        FieldInfo field = target.GetType().GetField(
            fieldName,
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null);
        field.SetValue(target, value);
    }
}
