using System.Collections.Generic;
using System.Reflection;
using DG.Tweening;
using NUnit.Framework;
using UnityEngine;

public sealed class OverworldFollowerObservationTests
{
    private GameObject _globalObject, _leaderObject, _room, _stateObject, _obstacle;
    private GlobalDataManager _global;
    private GlobalDataManager _previousGlobal;
    private PlayerController _leader;
    private OverworldPartyFollowerController _followers;
    private OverworldFollowerActor _actor;
    private OverworldFollowerObservation _observation;
    private DialogueData _dialogue;
    private CharacterData _data;
    private object _previousCharacterCache;
    private GameStateManager _state;
    private GameStateManager _previousState;
    private GameObject _managerObject, _panelObject;
    private DialogueManager _manager, _previousDialogueManager;
    private bool _createdDialogue;
    private readonly Dictionary<FieldInfo, object> _inputState = new Dictionary<FieldInfo, object>();

    [SetUp]
    public void SetUp()
    {
        // EditMode AddComponent는 일반 MonoBehaviour의 Awake를 호출하지 않습니다.
        // 실제 Play의 Bootstrap 계약만 명시적으로 연결하고, 이전 정적 참조는 복원합니다.
        _previousGlobal = GlobalDataManager.Instance;
        _previousState = GameStateManager.Instance;
        _previousDialogueManager = DialogueManager.Instance;
        SetStaticInstance(typeof(GlobalDataManager), null);
        SetStaticInstance(typeof(GameStateManager), null);
        SetStaticInstance(typeof(DialogueManager), null);
        _createdDialogue = false;
        _previousCharacterCache = typeof(CharacterDatabase).GetField("_cache", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
        _data = ScriptableObject.CreateInstance<CharacterData>();
        _data.CharacterID = "observation.tests";
        typeof(CharacterDatabase).GetField("_cache", BindingFlags.Static | BindingFlags.NonPublic).SetValue(null,
            new Dictionary<string, CharacterData> { { _data.CharacterID, _data } });
        _globalObject = new GameObject("Observation Global");
        _global = _globalObject.AddComponent<GlobalDataManager>();
        SetStaticInstance(typeof(GlobalDataManager), _global);
        _stateObject = new GameObject("Observation State");
        _state = _stateObject.AddComponent<GameStateManager>();
        SetStaticInstance(typeof(GameStateManager), _state);
        _leaderObject = new GameObject("Observation Leader", typeof(SpriteRenderer), typeof(Rigidbody2D), typeof(Animator));
        var body = _leaderObject.AddComponent<BoxCollider2D>();
        body.size = new Vector2(0.7f, 1.3f);
        body.offset = new Vector2(0f, 0.65f);
        var character = _leaderObject.AddComponent<PlayerCharacter>();
        character.SetCharacterData(_data);
        _leader = _leaderObject.AddComponent<PlayerController>();
        var leaderMember = new CharacterSaveData { CharacterDataID = _data.CharacterID };
        _global.Party.Add(leaderMember);
        character.LoadDataFromGlobal(leaderMember);
        _global.Party.Add(new CharacterSaveData { CharacterDataID = _data.CharacterID });
        _global.Party.Add(new CharacterSaveData { CharacterDataID = _data.CharacterID });
        _followers = _leaderObject.AddComponent<OverworldPartyFollowerController>();
        _followers.Configure(_leader, 5);
        Assert.That(_followers.FollowerCount, Is.EqualTo(2), "Fixture: 테스트 파티가 실제 추종 제어기에 연결되어야 합니다.");
        Assert.That(_followers.TryGetFollower(1, out _actor), Is.True, "Fixture: 관람할 실제 동료가 생성되어야 합니다.");
        _room = new GameObject("Observation Room");
        var areaObject = new GameObject("Departure");
        areaObject.transform.SetParent(_room.transform, false);
        var area = areaObject.AddComponent<BoxCollider2D>();
        area.isTrigger = true;
        area.size = Vector2.one;
        var target = new GameObject("View Point");
        target.transform.SetParent(_room.transform, false);
        target.transform.position = new Vector3(2f, 0f);
        _dialogue = ScriptableObject.CreateInstance<DialogueData>();
        _dialogue.Nodes.Add(new DialogueNode { DefaultText = "test" });
        _observation = _room.AddComponent<OverworldFollowerObservation>();
        _observation.Configure(1, area, new[] { target.transform }, new Transform[0], _dialogue, 1, 1f);
        Physics2D.SyncTransforms();
        _observation.Bind(_followers);
        Assert.That(_observation.IsDormant, Is.False, _observation.LastError);
    }

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(_room);
        if (_createdDialogue)
        {
            Object.DestroyImmediate(_managerObject);
            Object.DestroyImmediate(_panelObject);
            foreach (var pair in _inputState) pair.Key.SetValue(null, pair.Value);
            _inputState.Clear();
        }
        Object.DestroyImmediate(_obstacle);
        Object.DestroyImmediate(_leaderObject);
        Object.DestroyImmediate(_globalObject);
        Object.DestroyImmediate(_stateObject);
        Object.DestroyImmediate(_dialogue);
        Object.DestroyImmediate(_data);
        SetStaticInstance(typeof(GlobalDataManager), _previousGlobal);
        SetStaticInstance(typeof(GameStateManager), _previousState);
        SetStaticInstance(typeof(DialogueManager), _previousDialogueManager);
        typeof(CharacterDatabase).GetField("_cache", BindingFlags.Static | BindingFlags.NonPublic).SetValue(null, _previousCharacterCache);
    }

    [TestCase(-5f, 0f, 5f, 0f, true)]
    [TestCase(-5f, 2f, 5f, 2f, false)]
    [TestCase(0f, 0f, 0f, 0f, true)]
    [TestCase(-1f, -1f, 1f, 1f, true)]
    public void Departure_UsesWholePreviousCurrentSegment(float x0, float y0, float x1, float y1, bool expected)
        => Assert.That(OverworldFollowerObservationRoute.CrossesBox(new Vector2(x0, y0), new Vector2(x1, y1),
            Vector2.zero, Vector2.one), Is.EqualTo(expected));

    [Test]
    public void MissingOptionalSlot_DormantWithoutChangingOtherFollowers()
    {
        Set(_observation, "_slot", 5);
        _observation.Bind(_followers);
        Tick();
        Assert.That(_observation.IsDormant, Is.True);
        Assert.That(_followers.FollowerCount, Is.EqualTo(2));
        Assert.That(_actor.Mode, Is.EqualTo(OverworldFollowerMode.Follow));
    }

    [Test]
    public void BodyWidthAndInitialSegment_AreCheckedBeforeDeparture()
    {
        _obstacle = new GameObject("Initial segment obstruction");
        _obstacle.transform.position = new Vector3(1f, 0.65f);
        _obstacle.AddComponent<BoxCollider2D>().size = new Vector2(0.1f, 2f);
        Physics2D.SyncTransforms();
        Tick();
        Assert.That(_observation.IsHolding, Is.True);
        Assert.That(_actor.IsScriptedMoving, Is.False);
        Assert.That(_actor.transform.position, Is.EqualTo(Vector3.zero));
    }

    [Test]
    public void InterruptedDeparture_ProxyFollowsActualPosition()
    {
        Tick();
        _actor.transform.position = new Vector3(0.4f, 0f);
        ((OverworldFollowerMoveCommand)Get(_observation, "_move")).Cancel();
        Tick();
        var proxy = _room.GetComponentInChildren<OverworldFollowerObservationInteraction>(true);
        Assert.That(_observation.IsHolding, Is.True);
        Assert.That(proxy.GetComponent<BoxCollider2D>().enabled, Is.True);
        Assert.That(proxy.transform.position, Is.EqualTo(_actor.transform.position));
    }

    [Test]
    public void Join_UsesCurrentLeaderAndReleasesOwnLock()
    {
        Hold();
        Assert.That(_observation.TryJoin(out string error), Is.True, error);
        Assert.That(_state.CurrentState, Is.EqualTo(GameState.Cutscene));
        FinishMove();
        Assert.That(_actor.Mode, Is.EqualTo(OverworldFollowerMode.Follow));
        Assert.That(_actor.transform.position, Is.EqualTo(_leader.transform.position));
        Assert.That(_state.CurrentState, Is.EqualTo(GameState.Exploration));
    }

    [Test]
    public void ExpiredOriginalTrail_StillRejoinsFreshLeaderEndpoint()
    {
        Hold();
        var trail = (OverworldFollowerTrail)Get(_followers, "_trail");
        for (int i = 1; i < 1200; i++) trail.Record(new Vector3(i * 0.01f, 0f), 3);
        _leader.transform.position = new Vector3(11.99f, 0f);
        Physics2D.SyncTransforms();
        Assert.That(_observation.TryJoin(out string error), Is.True, error);
        FinishMove();
        Assert.That(_actor.Mode, Is.EqualTo(OverworldFollowerMode.Follow));
        Assert.That(_actor.HasTrailCursor, Is.True);
        Assert.That(_actor.transform.position.x, Is.EqualTo(11.99f).Within(0.0001f));
    }

    [Test]
    public void InterruptedJoin_HoldsAndDoesNotOverwriteNewerState()
    {
        Hold();
        Assert.That(_observation.TryJoin(out _), Is.True);
        _state.ChangeState(GameState.Paused);
        Tick();
        Assert.That(_actor.IsScriptedMoving, Is.False);
        Assert.That(_observation.IsHolding, Is.True);
        Assert.That(_state.CurrentState, Is.EqualTo(GameState.Paused));
    }

    [Test]
    public void OtherMoveCommand_CancelsOnlyObservationOwnership()
    {
        Tick();
        Assert.That(_followers.TryBeginMove(1, new[] { Vector3.up * 3f }, 5f, false, out var replacement, out _), Is.True);
        Tick();
        _observation.StopOwnedWork();
        Assert.That(_observation.IsDormant, Is.True);
        Assert.That(replacement.IsDone, Is.False);
    }

    [Test]
    public void RoomDisableDuringJoin_ReleasesInputAndOwnCommand()
    {
        Hold();
        Assert.That(_observation.TryJoin(out _), Is.True);
        DisableRoom();
        Assert.That(_observation.IsDormant, Is.True);
        Assert.That(_actor.IsScriptedMoving, Is.False);
        Assert.That(_state.CurrentState, Is.EqualTo(GameState.Exploration));
    }

    [Test]
    public void SameFrameRoomDisable_RejectsLateDialogueCompletion()
    {
        Hold();
        uint generation = (uint)Get(_observation, "_actionGeneration");
        DisableRoom();
        Invoke(_observation, "OnTalkCompleted", generation);
        Assert.That(_observation.IsDormant, Is.True);
        Assert.That(Get(_observation, "_dialogueOwner"), Is.Null);
    }

    [TestCase(false)]
    [TestCase(true)]
    public void RoomDisableInsideDialogueStart_CancelsAfterManagerReturns(bool failureNotice)
    {
        Hold();
        PrepareDialogueManager();
        _state.OnStateChanged += state => { if (state == GameState.Dialogue) DisableRoom(); };
        if (failureNotice)
        {
            Set(_observation, "_failurePending", true);
            Assert.DoesNotThrow(Tick);
        }
        else Assert.DoesNotThrow(() => _observation.TryInteract(_leader));
        Assert.That(_observation.IsDormant, Is.True);
        Assert.That(_manager.IsPlaying, Is.False);
        Assert.That(_manager.IsPresentationVisible, Is.False);
        Assert.That(_state.CurrentState, Is.EqualTo(GameState.Exploration));
    }

    [Test]
    public void RoomDisableInsidePromptStart_CancelsOnlyJustStartedGeneration()
    {
        Hold();
        PrepareDialogueManager();
        Assert.That(_observation.TryInteract(_leader), Is.True);
        _manager.EndDialogue();
        _manager.HideFinishedPresentation(_manager.PlaybackGeneration);
        _state.OnStateChanged += state => { if (state == GameState.Dialogue) DisableRoom(); };
        Assert.DoesNotThrow(Tick);
        Assert.That(_observation.IsDormant, Is.True);
        Assert.That(_manager.IsPlaying, Is.False);
        Assert.That(_manager.IsPresentationVisible, Is.False);
        Assert.That(_state.CurrentState, Is.EqualTo(GameState.Exploration));
    }

    [Test]
    public void RoomDisableInsideJoinUnlock_DoesNotDereferenceOrChangeOldActor()
    {
        Hold();
        Assert.That(_observation.TryJoin(out _), Is.True);
        _state.OnStateChanged += state => { if (state == GameState.Exploration) DisableRoom(); };
        Assert.DoesNotThrow(FinishMove);
        Assert.That(_observation.IsDormant, Is.True);
        Assert.That(_actor.Mode, Is.EqualTo(OverworldFollowerMode.Follow));
        Assert.That(_state.CurrentState, Is.EqualTo(GameState.Exploration));
    }

    [Test]
    public void PartyReplacementInsideJoinLock_DoesNotMoveNewParty()
    {
        Hold();
        _state.OnStateChanged += state =>
        {
            if (state != GameState.Cutscene) return;
            var replacement = new List<CharacterSaveData>
            {
                new CharacterSaveData { CharacterDataID = _data.CharacterID },
                new CharacterSaveData { CharacterDataID = _data.CharacterID }
            };
            typeof(GlobalDataManager).GetField("<Party>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(_global, replacement);
        };
        Assert.That(_observation.TryJoin(out _), Is.False);
        Assert.That(_followers.TryGetFollower(1, out var actor), Is.True);
        Assert.That(actor.IsScriptedMoving, Is.False);
        Assert.That(_state.CurrentState, Is.EqualTo(GameState.Exploration));
    }

    [Test]
    public void NewPartyOrArrival_DoesNotMutateNewFollowerState()
    {
        Hold();
        Assert.That(_observation.TryJoin(out _), Is.True);
        _followers.ResetAtLeader();
        Tick();
        Assert.That(_observation.IsDormant, Is.True);
        Assert.That(_actor.Mode, Is.EqualTo(OverworldFollowerMode.Follow));
        Assert.That(_state.CurrentState, Is.EqualTo(GameState.Exploration));
    }

    private void Hold()
    {
        Tick();
        FinishMove();
        Assert.That(_observation.IsHolding, Is.True);
    }
    private void DisableRoom()
    {
        _room.SetActive(false);
        // EditMode는 일반 MonoBehaviour의 OnDisable을 자동 호출하지 않습니다.
        // 비활성 상태와 수명 콜백을 함께 재현합니다. 실제 엔진 전달은 Play 통합 검사에서 검증합니다.
        Invoke(_observation, "OnDisable");
    }
    private void PrepareDialogueManager()
    {
        _createdDialogue = true;
        foreach (string name in new[] { "_consumedBattleUIFrame", "_consumedZAt", "_consumedXAt", "_consumedCAt",
            "_suppressPlayerConfirmUntilFrame", "_suppressSharedUIUntilFrame", "_prevBattleNavigate", "_currBattleNavigate" })
        {
            FieldInfo field = typeof(GameInput).GetField(name, BindingFlags.Static | BindingFlags.NonPublic);
            _inputState[field] = field.GetValue(null);
        }
        _panelObject = new GameObject("Observation Dialogue Panel");
        DialogueUI panel = _panelObject.AddComponent<DialogueUI>();
        _panelObject.SetActive(false);
        _managerObject = new GameObject("Observation Dialogue Manager");
        _manager = _managerObject.AddComponent<DialogueManager>();
        SetStaticInstance(typeof(DialogueManager), _manager);
        Set(_manager, "_overworldPanel", panel);
        Set(_manager, "_cinematicPanel", panel);
    }
    private void Tick() => Invoke(_observation, "Tick");
    private void FinishMove()
    {
        ((Tween)Get(_actor, "_moveTween"))?.Complete(true);
        Tick();
    }
    private static object Get(object target, string field)
        => target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target);
    private static void Set(object target, string field, object value)
        => target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
    private static object Invoke(object target, string method, params object[] args)
        => target.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, args);
    private static void SetStaticInstance(System.Type type, object instance)
        => type.GetField("<Instance>k__BackingField", BindingFlags.Static | BindingFlags.NonPublic).SetValue(null, instance);
}
