using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

public sealed class RegionEntryCoordinatorTests
{
    private GlobalDataManager _previousGlobal;
    private AudioManager _previousAudio;
    private MapTransitionService _previousTransition;
    private readonly List<UnityEngine.Object> _owned = new List<UnityEngine.Object>();
    private GlobalDataManager _global;
    private RoomContainer _container;
    private PlayerController _player;
    private RegionEntryCoordinator _coordinator;
    private RoomDefinition _defaultRoom;
    private RoomDefinition _secondRoom;
    private AudioManager _audio;

    [SetUp]
    public void SetUp()
    {
        _previousGlobal = GlobalDataManager.Instance;
        _previousAudio = AudioManager.Instance;
        _previousTransition = MapTransitionService.Instance;
        SetStaticInstance(typeof(GlobalDataManager), null);
        SetStaticInstance(typeof(AudioManager), null);
        SetStaticInstance(typeof(MapTransitionService), null);

        _global = Component<GlobalDataManager>("GlobalData_RegionEntryTests");
        SetStaticInstance(typeof(GlobalDataManager), _global);
        _container = Component<RoomContainer>("RoomContainer_RegionEntryTests");
        _player = Component<PlayerController>("Player_RegionEntryTests");
        _coordinator = Component<RegionEntryCoordinator>("Coordinator_RegionEntryTests");
        _audio = Component<AudioManager>("Audio_RegionEntryTests");
        SetPrivateField(_audio, "_bgmSourceA", Component<AudioSource>("BGM_A_RegionEntryTests"));
        SetPrivateField(_audio, "_bgmSourceB", Component<AudioSource>("BGM_B_RegionEntryTests"));
        InvokePrivate(_audio, "InitializeRuntimeState");
        SetStaticInstance(typeof(AudioManager), _audio);

        _defaultRoom = Room("region.default", "default.spawn", new Vector2(1f, 2f));
        _secondRoom = Room("region.second", "second.spawn", new Vector2(7f, 8f));
        _coordinator.Configure(
            _container,
            _player,
            _defaultRoom,
            new[] { _defaultRoom, _secondRoom },
            requireCameraBinding: false);
    }

    [TearDown]
    public void TearDown()
    {
        for (int i = _owned.Count - 1; i >= 0; i--)
        {
            if (_owned[i] != null)
                UnityEngine.Object.DestroyImmediate(_owned[i]);
        }

        SetStaticInstance(typeof(GlobalDataManager), _previousGlobal);
        SetStaticInstance(typeof(AudioManager), _previousAudio);
        SetStaticInstance(typeof(MapTransitionService), _previousTransition);
    }

    [Test]
    public void Prepare_ResolvesRoomThenAppliesSpawnAndConsumesArrivalState()
    {
        _global.CurrentRoomId = "region.second";
        _global.SpawnPointId = "second.spawn";
        _global.LookingDir = (int)FacingDirection.Left;

        bool prepared = _coordinator.TryPrepare(out string error);

        Assert.That(prepared, Is.True, error);
        Assert.That(_coordinator.Status, Is.EqualTo(RegionEntryStatus.Succeeded));
        Assert.That(_coordinator.IsReadyToReveal, Is.True);
        Assert.That(_container.CurrentDefinition, Is.SameAs(_secondRoom));
        Assert.That(_player.transform.position, Is.EqualTo(new Vector3(7f, 8f, 0f)));
        Assert.That(_player.FacingDirection, Is.EqualTo((int)FacingDirection.Left));
        Assert.That(_global.CurrentRoomId, Is.EqualTo("region.second"));
        Assert.That(_global.SpawnPointId, Is.Empty);
    }

    [Test]
    public void Prepare_UnknownSavedRoomFallsBackToDefaultRoom()
    {
        _global.CurrentRoomId = "missing.room";
        _global.SpawnPointId = string.Empty;
        _global.SpawnX = 4f;
        _global.SpawnY = 5f;
        LogAssert.Expect(
            LogType.Warning,
            new System.Text.RegularExpressions.Regex("저장된 Room ID를 찾지 못해 기본 Room"));

        bool prepared = _coordinator.TryPrepare(out string error);

        Assert.That(prepared, Is.True, error);
        Assert.That(_coordinator.UsedDefaultFallback, Is.True);
        Assert.That(_container.CurrentDefinition, Is.SameAs(_defaultRoom));
        Assert.That(_global.CurrentRoomId, Is.EqualTo("region.default"));
        Assert.That(_player.transform.position, Is.EqualTo(new Vector3(4f, 5f, 0f)));
    }

    [Test]
    public void Prepare_MissingSpecifiedSpawnFailsWithoutReplacingCommittedRoom()
    {
        Assert.That(
            _container.TryLoadRoom(_defaultRoom, null, out RoomInstance original, out string loadError),
            Is.True,
            loadError);
        _global.CurrentRoomId = "region.second";
        _global.SpawnPointId = "missing.spawn";
        _global.SpawnFallbackAllowed = false;
        LogAssert.Expect(
            LogType.Error,
            new System.Text.RegularExpressions.Regex("SpawnPoint를 찾지 못했습니다"));

        bool prepared = _coordinator.TryPrepare(out string error);

        Assert.That(prepared, Is.False);
        Assert.That(error, Does.Contain("SpawnPoint"));
        Assert.That(_coordinator.Status, Is.EqualTo(RegionEntryStatus.RoomLoadFailed));
        Assert.That(_coordinator.IsReadyToReveal, Is.False);
        Assert.That(_container.CurrentRoom, Is.SameAs(original));
        Assert.That(_container.CurrentDefinition, Is.SameAs(_defaultRoom));
        Assert.That(original.gameObject.activeSelf, Is.True);
    }

    [Test]
    public void InitialRoomStart_AppliesConfiguredBgm()
    {
        AudioClip clip = SetBgm(_defaultRoom, "InitialRoom");
        SetPrivateField(_container, "_initialRoom", _defaultRoom);
        SetPrivateField(_container, "_loadInitialRoomOnStart", true);

        InvokePrivate(_container, "Start");

        Assert.That(_container.CurrentDefinition, Is.SameAs(_defaultRoom));
        Assert.That(_audio.RequestedBgmClip, Is.SameAs(clip));
    }

    [Test]
    public void PrepareSavedRoom_AppliesBgmAndIsNotReplacedByInitialStart()
    {
        AudioClip clip = SetBgm(_secondRoom, "SavedRoom");
        SetBgm(_defaultRoom, "InitialRoom");
        SetPrivateField(_container, "_initialRoom", _defaultRoom);
        SetPrivateField(_container, "_loadInitialRoomOnStart", true);
        _global.CurrentRoomId = _secondRoom.RoomId;
        _global.SpawnPointId = "second.spawn";

        Assert.That(_coordinator.TryPrepare(out string error), Is.True, error);
        RoomInstance restoredRoom = _container.CurrentRoom;
        InvokePrivate(_container, "Start");

        Assert.That(_container.CurrentRoom, Is.SameAs(restoredRoom));
        Assert.That(_audio.RequestedBgmClip, Is.SameAs(clip));
    }

    [Test]
    public void RoomTransition_AppliesDestinationBgm()
    {
        SetBgm(_defaultRoom, "OriginRoom");
        AudioClip clip = SetBgm(_secondRoom, "DestinationRoom");
        _container.LoadRoom(_defaultRoom, _player);
        MapTransitionService service = Component<MapTransitionService>("Transition_RegionEntryTests");
        SetPrivateField(service, "_roomContainer", _container);
        var request = new MapTransitionRequest
        {
            TransitionType = MapTransitionType.Room,
            TargetRoom = _secondRoom,
            TargetSpawnPointId = "second.spawn",
            FadeDuration = 0f
        };

        object result = InvokePrivate(service, "LoadRoom", request, _player);

        Assert.That(result, Is.EqualTo(SceneLoadResult.Succeeded));
        Assert.That(_container.CurrentDefinition, Is.SameAs(_secondRoom));
        Assert.That(_audio.RequestedBgmClip, Is.SameAs(clip));
    }

    [Test]
    public void PrepareRejectedRoom_DoesNotChangeBgm()
    {
        AudioClip clip = SetBgm(_defaultRoom, "OriginRoom");
        SetBgm(_secondRoom, "RejectedRoom");
        _container.LoadRoom(_defaultRoom, _player);
        _global.CurrentRoomId = _secondRoom.RoomId;
        _global.SpawnPointId = "missing.spawn";
        _global.SpawnFallbackAllowed = false;
        LogAssert.Expect(
            LogType.Error,
            new System.Text.RegularExpressions.Regex("SpawnPoint를 찾지 못했습니다"));

        Assert.That(_coordinator.TryPrepare(out _), Is.False);

        Assert.That(_audio.RequestedBgmClip, Is.SameAs(clip));
        Assert.That(_container.CurrentDefinition, Is.SameAs(_defaultRoom));
    }

    [TestCase(true)]
    [TestCase(false)]
    public void RoomWithoutOverride_HonorsKeepCurrentBgm(bool keepCurrentBgm)
    {
        AudioClip clip = SetBgm(_defaultRoom, "OriginRoom");
        _container.LoadRoom(_defaultRoom, _player);
        SetPrivateField(_secondRoom, "_keepCurrentBgm", keepCurrentBgm);
        SetPrivateField(_secondRoom, "_bgmFadeDuration", 0f);

        _container.LoadRoom(_secondRoom, _player);

        Assert.That(_audio.RequestedBgmClip, Is.EqualTo(keepCurrentBgm ? clip : null));
        Assert.That(_audio.HasRequestedBgm, Is.EqualTo(keepCurrentBgm));
    }

    private AudioClip SetBgm(RoomDefinition room, string clipName)
    {
        AudioClip clip = AudioClip.Create(clipName, 4410, 1, 44100, false);
        _owned.Add(clip);
        SetPrivateField(room, "_bgmOverride", clip);
        SetPrivateField(room, "_bgmFadeDuration", 0f);
        return clip;
    }

    private RoomDefinition Room(string roomId, string spawnId, Vector2 spawnPosition)
    {
        GameObject prefabObject = new GameObject(roomId + "_PrefabSource");
        _owned.Add(prefabObject);
        RoomInstance prefab = prefabObject.AddComponent<RoomInstance>();
        SetPrivateField(prefab, "_roomId", roomId);

        GameObject spawnObject = new GameObject(spawnId);
        spawnObject.transform.SetParent(prefabObject.transform, false);
        spawnObject.transform.localPosition = spawnPosition;
        SpawnPoint spawn = spawnObject.AddComponent<SpawnPoint>();
        SetPrivateField(spawn, "_spawnPointId", spawnId);

        RoomDefinition definition = ScriptableObject.CreateInstance<RoomDefinition>();
        _owned.Add(definition);
        SetPrivateField(definition, "_roomId", roomId);
        SetPrivateField(definition, "_roomPrefab", prefab);
        return definition;
    }

    private T Component<T>(string name) where T : Component
    {
        GameObject gameObject = new GameObject(name);
        _owned.Add(gameObject);
        return gameObject.AddComponent<T>();
    }

    private static void SetPrivateField(object target, string fieldName, object value)
    {
        FieldInfo field = target.GetType().GetField(
            fieldName,
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null, "Missing field: " + fieldName);
        field.SetValue(target, value);
    }

    private static void SetStaticInstance(Type type, object value)
    {
        PropertyInfo property = type.GetProperty(
            "Instance",
            BindingFlags.Public | BindingFlags.Static);
        Assert.That(property, Is.Not.Null);
        property.SetValue(null, value);
    }

    private static object InvokePrivate(object target, string methodName, params object[] arguments)
    {
        MethodInfo method = target.GetType().GetMethod(
            methodName,
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(method, Is.Not.Null, "Missing method: " + methodName);
        return method.Invoke(target, arguments);
    }
}
