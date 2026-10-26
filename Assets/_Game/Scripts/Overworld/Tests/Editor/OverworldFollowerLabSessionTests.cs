using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public sealed class OverworldFollowerLabSessionTests
{
    private readonly List<UnityEngine.Object> _owned = new List<UnityEngine.Object>();
    private GlobalDataManager _previousGlobal;
    private GlobalDataManager _global;
    private OverworldFollowerLabSession _session;
    private IDisposable _block;

    [SetUp]
    public void SetUp()
    {
        _previousGlobal = GlobalDataManager.Instance;
        SetStatic(typeof(GlobalDataManager), "<Instance>k__BackingField", null);
        InvokeStatic("ResetRuntimeState");
        _global = Component<GlobalDataManager>("FollowerLabTests Global");
        SetStatic(typeof(GlobalDataManager), "<Instance>k__BackingField", _global);
        _session = Component<OverworldFollowerLabSession>("FollowerLabTests Session");
    }

    [TearDown]
    public void TearDown()
    {
        for (int i = _owned.Count - 1; i >= 0; i--)
            if (_owned[i] != null) UnityEngine.Object.DestroyImmediate(_owned[i]);
        _owned.Clear();
        _block?.Dispose();
        _block = null;
        SetStatic(typeof(GlobalDataManager), "<Instance>k__BackingField", _previousGlobal);
        InvokeStatic("ResetRuntimeState");
    }

    [TestCase(0), TestCase(2), TestCase(5)]
    public void Definition_UsesExactlyAuthoredMembersAndOrder(int companionCount)
    {
        OverworldFollowerLabDefinition definition = Definition(companionCount);
        Assert.That(definition.TryCreateSeed("Lab", new Vector2(2f, 3f), out SaveData seed, out string error), Is.True, error);
        Assert.That(seed.PartyData.Count, Is.EqualTo(companionCount + 1));
        Assert.That(seed.PartyData[0].CharacterDataID, Is.EqualTo(definition.Leader.CharacterID));
        for (int i = 0; i < companionCount; i++)
            Assert.That(seed.PartyData[i + 1].CharacterDataID, Is.EqualTo(definition.Companions[i].CharacterID));
        Assert.That(seed.currentRoomId, Is.EqualTo("follower.lab.tests"));
        Assert.That(seed.spawnPointId, Is.EqualTo("default"));
        Assert.That(seed.Money, Is.Zero);
        Assert.That(seed.eventFlags, Is.Empty);
    }

    [Test]
    public void Definition_RejectsMissingLeaderNullMemberAndExcessWithoutMutatingList()
    {
        var definition = Definition(5);
        definition.Companions.Add(Character("overflow"));
        Assert.That(definition.TryValidateParty(out _), Is.False);
        Assert.That(definition.Companions.Count, Is.EqualTo(6));
        definition.Companions.RemoveAt(5);
        definition.Companions[2] = null;
        Assert.That(definition.TryValidateParty(out _), Is.False);
        Assert.That(definition.Companions[2], Is.Null);
        definition.Companions.Clear();
        definition.Leader = null;
        Assert.That(definition.TryValidateParty(out _), Is.False);
        definition.Companions = null;
        definition.Leader = Character("leader");
        Assert.That(definition.TryValidateParty(out _), Is.False);
    }

    [Test]
    public void Definition_RejectsDuplicateIdentityAndDestroyedUnityReference()
    {
        var definition = Definition(1);
        definition.Companions[0].CharacterID = definition.Leader.CharacterID;
        Assert.That(definition.TryValidateParty(out _), Is.False);
        UnityEngine.Object.DestroyImmediate(definition.Companions[0]);
        Assert.That(definition.TryValidateParty(out _), Is.False);
    }

    [Test]
    public void Cleanup_RestoresOwnedEmptySnapshotAndReleasesOnlyItsOwnBlock()
    {
        ArmOwnedParty(3);
        using (SaveManager.AcquireProgressWriteBlock())
        {
            Invoke("OnDisable");
            Invoke("OnDestroy");
            Assert.That(_global.Party, Is.Empty);
            Assert.That(SaveManager.AreProgressWritesBlocked, Is.True);
            Assert.That(_session.IsRunning, Is.False);
        }
        Assert.That(SaveManager.AreProgressWritesBlocked, Is.False);
    }

    [Test]
    public void ReplacedParty_WithIdenticalIdsIsNeverRestoredOver()
    {
        ArmOwnedParty(3);
        SaveData replacement = _global.ToSaveData();
        replacement.Money = 123;
        _global.FromSaveData(replacement);
        List<CharacterSaveData> actual = _global.Party;
        Invoke("Update");
        Assert.That(SaveManager.AreProgressWritesBlocked, Is.False);
        Invoke("OnDisable");
        Assert.That(_global.Party, Is.SameAs(actual));
        Assert.That(_global.Money, Is.EqualTo(123));
        Assert.That(_session.IsRunning, Is.False);
    }

    [Test]
    public void SameListMemberReplacementAndNewGameClearLoseOwnership()
    {
        ArmOwnedParty(1);
        _global.Party[0] = new CharacterSaveData { CharacterDataID = "member.0" };
        Invoke("Update");
        Assert.That(_session.IsRunning, Is.False);
        Assert.That(_global.Party.Count, Is.EqualTo(1));
        Assert.That(SaveManager.AreProgressWritesBlocked, Is.False);
    }

    [Test]
    public void NewGameClear_DoesNotResurrectLabParty()
    {
        ArmOwnedParty(1);
        _global.ResetForNewGame();
        Invoke("Update");
        Invoke("OnDestroy");
        Assert.That(_global.Party, Is.Empty);
        Assert.That(SaveManager.AreProgressWritesBlocked, Is.False);
    }

    [Test]
    public void FailedPartialInitialization_CleansOwnedSnapshotAndCannotRestart()
    {
        ArmOwnedParty(6);
        object[] args = { "injected initialization failure", null };
        Assert.That(Invoke("Fail", args), Is.False);
        Assert.That(_global.Party, Is.Empty);
        Assert.That(SaveManager.AreProgressWritesBlocked, Is.False);
        object[] restart = { null };
        Assert.That(Invoke("TryInitialize", restart), Is.False);
    }

    [Test]
    public void OldPlayGeneration_DoesNotRestoreOrReleaseAnotherOwner()
    {
        ArmOwnedParty(1);
        List<CharacterSaveData> actual = _global.Party;
        InvokeStatic("ResetRuntimeState");
        using (SaveManager.AcquireProgressWriteBlock())
        {
            Invoke("OnDisable");
            Assert.That(_global.Party, Is.SameAs(actual));
            Assert.That(SaveManager.AreProgressWritesBlocked, Is.True);
        }
        Assert.That(SaveManager.AreProgressWritesBlocked, Is.False);
    }

    [Test]
    public void DestroyedGlobal_ReleasesScopeWithoutTryingToRestore()
    {
        ArmOwnedParty(1);
        UnityEngine.Object.DestroyImmediate(_global.gameObject);
        Invoke("OnDisable");
        Assert.That(SaveManager.AreProgressWritesBlocked, Is.False);
    }

    [Test]
    public void ExistingGlobalAtAwake_RefusesWithoutChangingPartyOrAcquiringBlock()
    {
        _global.Party.Add(new CharacterSaveData());
        List<CharacterSaveData> actual = _global.Party;
        Invoke("Awake");
        object[] args = { null };
        Assert.That(Invoke("TryInitialize", args), Is.False);
        Assert.That(_global.Party, Is.SameAs(actual));
        Assert.That(_global.Party.Count, Is.EqualTo(1));
        Assert.That(SaveManager.AreProgressWritesBlocked, Is.False);
    }

    private void ArmOwnedParty(int count)
    {
        Set(_session, "_global", _global);
        Set(_session, "_emptySession", _global.ToSaveData());
        for (int i = 0; i < count; i++) _global.Party.Add(new CharacterSaveData { CharacterDataID = "member." + i });
        Invoke("CaptureParty", new object[] { _global.Party });
        _block = SaveManager.AcquireProgressWriteBlock();
        Set(_session, "_writeBlock", _block);
        Set(_session, "_finished", false);
        Set(_session, "_generation", typeof(OverworldFollowerLabSession).GetField("_playGeneration", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null));
        Set(_session, "<IsRunning>k__BackingField", true);
    }

    private OverworldFollowerLabDefinition Definition(int count)
    {
        var definition = Asset<OverworldFollowerLabDefinition>();
        definition.Leader = Character("leader");
        for (int i = 0; i < count; i++) definition.Companions.Add(Character("member." + i));
        definition.InitialRoom = Asset<RoomDefinition>();
        definition.InitialArea = Asset<AreaDefinition>();
        Set(definition.InitialRoom, "_roomId", "follower.lab.tests");
        Set(definition.InitialRoom, "_roomPrefab", Component<RoomInstance>("FollowerLabTests Room"));
        Set(definition.InitialRoom, "_areaDefinition", definition.InitialArea);
        Set(definition.InitialArea, "_roomDefinition", definition.InitialRoom);
        return definition;
    }

    private CharacterData Character(string id) { var data = Asset<CharacterData>(); data.CharacterID = id; return data; }
    private T Asset<T>() where T : ScriptableObject { T asset = ScriptableObject.CreateInstance<T>(); _owned.Add(asset); return asset; }
    private T Component<T>(string name) where T : Component
    {
        var go = new GameObject(name);
        go.SetActive(false);
        _owned.Add(go);
        return go.AddComponent<T>();
    }
    private object Invoke(string method, object[] args = null)
        => typeof(OverworldFollowerLabSession).GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(_session, args);
    private static void InvokeStatic(string method)
        => typeof(OverworldFollowerLabSession).GetMethod(method, BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, null);
    private static void Set(object target, string field, object value)
        => target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
    private static void SetStatic(Type type, string field, object value)
        => type.GetField(field, BindingFlags.Static | BindingFlags.NonPublic).SetValue(null, value);
}
