using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

/// <summary>
/// Room 한 번의 수명에서 지정 동료의 관람·반복 대화·명시 경유점 복귀만 소유합니다.
/// 실제 파티, 추종 경로, 카메라, 다른 소유자의 명령은 변경하지 않습니다.
/// </summary>
[DisallowMultipleComponent, DefaultExecutionOrder(100)]
[AddComponentMenu("Hub To Home/필드/동료 관람")]
public sealed class OverworldFollowerObservation : MonoBehaviour
{
    [SerializeField, Range(1, 5), LabelText("관람 동료 번호")]
    private int _slot = 4;
    [SerializeField, LabelText("없는 동료는 건너뛰기")]
    private bool _optionalMissing = true;
    [SerializeField, LabelText("출발 영역")]
    private BoxCollider2D _departureArea;
    [SerializeField, LabelText("관람 경유점"), ListDrawerSettings(ShowIndexLabels = true)]
    private Transform[] _observationWaypoints = new Transform[0];
    [SerializeField, LabelText("복귀 경유점"), ListDrawerSettings(ShowIndexLabels = true)]
    private Transform[] _returnWaypoints = new Transform[0];
    [SerializeField, LabelText("관람 대화")]
    private DialogueData _dialogue;
    [SerializeField, Min(0f), LabelText("각 이동 시간")]
    private float _moveDuration = 1.5f;
    [SerializeField, LabelText("장애물 레이어")]
    private LayerMask _obstacleMask = 1;
    [SerializeField, LabelText("질문 번역 키")]
    private string _promptKey = "follower_lab.observe.prompt";
    [SerializeField, LabelText("합류 번역 키")]
    private string _joinKey = "follower_lab.observe.join";
    [SerializeField, LabelText("계속 관람 번역 키")]
    private string _stayKey = "follower_lab.observe.stay";
    [SerializeField, LabelText("실패 안내 번역 키")]
    private string _blockedKey = "follower_lab.observe.blocked";
    [SerializeField, LabelText("질문 기본 문구")]
    private string _promptFallback = "이제 같이 갈까요?";
    [SerializeField, LabelText("합류 기본 문구")]
    private string _joinFallback = "같이 가자";
    [SerializeField, LabelText("계속 관람 기본 문구")]
    private string _stayFallback = "더 둘러봐";
    [SerializeField, TextArea, LabelText("실패 안내 기본 문구")]
    private string _blockedFallback = "지금은 안전하게 이동할 수 없어요. 길이 비었을 때 다시 불러 주세요.";

    private enum Phase { Dormant, WaitingForEntry, Departing, Holding, Talking, ClosingTalk, Choosing, ClosingChoice, Returning, Complete }
    private Phase _phase;
    private OverworldPartyFollowerController _controller;
    private PlayerController _leader;
    private Collider2D[] _leaderColliders;
    private OverworldFollowerActor _actor;
    private OverworldFollowerObservationInteraction _interaction;
    private GlobalDataManager _global;
    private List<CharacterSaveData> _party;
    private readonly CharacterSaveData[] _members = new CharacterSaveData[6];
    private int _partyCount;
    private uint _epoch;
    private bool _hasSample;
    private bool _leaderEntered;
    private Vector3 _previousActorPosition;
    private Vector3 _previousLeaderPosition;
    private Vector2 _bodySize;
    private Vector2 _bodyOffset;
    private Vector3[] _departurePath;
    private Vector3[] _returnPath;
    private readonly RaycastHit2D[] _castHits = new RaycastHit2D[16];
    private readonly Collider2D[] _overlapHits = new Collider2D[16];
    private ContactFilter2D _obstacleFilter;
    private readonly string[] _choiceKeys = new string[2];
    private readonly string[] _choiceFallbacks = new string[2];
    private OverworldFollowerMoveCommand _move;
    private Vector3[] _activePath;
    private Vector3 _moveStart;
    private Vector3 _joinDestination;
    private uint _actionGeneration;
    private DialogueManager _dialogueOwner;
    private int _dialogueGeneration;
    private bool _startingDialogue;
    private bool _joinSelected;
    private bool _failurePending;
    private DialogueData _failureDialogue;
    private GameStateManager _stateOwner;
    private bool _ownsJoinLock;
    private bool _resolving;

    public int Slot => _slot;
    public bool IsHolding => _phase == Phase.Holding;
    public bool IsDormant => _phase == Phase.Dormant;
    public string LastError { get; private set; } = string.Empty;

    public void Configure(int slot, BoxCollider2D departureArea, Transform[] observationWaypoints,
        Transform[] returnWaypoints, DialogueData dialogue, LayerMask obstacleMask,
        float moveDuration = 1.5f, bool optionalMissing = true)
    {
        StopOwnedWork();
        _slot = slot;
        _departureArea = departureArea;
        _observationWaypoints = observationWaypoints;
        _returnWaypoints = returnWaypoints;
        _dialogue = dialogue;
        _obstacleMask = obstacleMask;
        _moveDuration = moveDuration;
        _optionalMissing = optionalMissing;
        if (Application.isPlaying && isActiveAndEnabled) BeginRoomLifetime();
    }

    /// <summary>명시 참조 연결용입니다. Room 활성화 시 동일 씬 조회는 한 번만 수행합니다.</summary>
    public void Bind(OverworldPartyFollowerController controller)
    {
        StopOwnedWork();
        _controller = controller;
        _leader = controller != null ? controller.Leader : null;
        if (isActiveAndEnabled) BeginRoomLifetime();
    }

    private void OnEnable()
    {
        if (Application.isPlaying) BeginRoomLifetime();
    }

    private void Start()
    {
        // 초기 씬의 Player.Start/Bootstrap보다 Room.OnEnable이 먼저였을 때만 한 번 재연결합니다.
        if (Application.isPlaying && _controller == null) BeginRoomLifetime();
    }

    private void BeginRoomLifetime()
    {
        StopOwnedWork();
        LastError = string.Empty;
        if (!TryValidateConfiguration(out string error)) { LastError = error; Debug.LogWarning(error, this); return; }
        if (_controller == null)
        {
            var controllers = FindObjectsByType<OverworldPartyFollowerController>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            for (int i = 0; i < controllers.Length; i++)
            {
                if (controllers[i].gameObject.scene != gameObject.scene || controllers[i].Leader == null) continue;
                if (_controller != null) { _controller = null; LastError = "동일 씬의 필드 리더가 둘 이상입니다."; return; }
                _controller = controllers[i];
            }
        }
        _leader = _controller != null ? _controller.Leader : null;
        if (_leader == null) return;
        _leaderColliders = _leader.GetComponentsInChildren<Collider2D>(true);
        bool hasBody = false;
        Bounds body = default;
        for (int i = 0; i < _leaderColliders.Length; i++)
        {
            Collider2D collider = _leaderColliders[i];
            if (collider == null || !collider.enabled || collider.isTrigger) continue;
            if (!hasBody) { body = collider.bounds; hasBody = true; }
            else body.Encapsulate(collider.bounds);
        }
        if (!hasBody || body.size.x <= 0f || body.size.y <= 0f)
        { LastError = "관람 경로 검사에 사용할 리더 몸체 Collider가 없습니다."; return; }
        _bodySize = body.size;
        _bodyOffset = body.center - _leader.transform.position;
        _departurePath = new Vector3[_observationWaypoints.Length];
        _returnPath = new Vector3[_returnWaypoints.Length + 1];
        _obstacleFilter = new ContactFilter2D { useLayerMask = true, layerMask = _obstacleMask, useTriggers = false };
        _choiceKeys[0] = _joinKey;
        _choiceKeys[1] = _stayKey;
        _choiceFallbacks[0] = _joinFallback;
        _choiceFallbacks[1] = _stayFallback;
        if (_interaction == null)
        {
            var proxy = new GameObject("Follower Observation Interaction");
            proxy.transform.SetParent(transform, false);
            _interaction = proxy.AddComponent<OverworldFollowerObservationInteraction>();
        }
        _interaction.Configure(this, _bodySize, _bodyOffset);
        _phase = Phase.WaitingForEntry;
        _hasSample = false;
        _leaderEntered = false;
    }

    public bool TryValidateConfiguration(out string error)
    {
        if (_slot < 1 || _slot > 5) { error = "관람 동료 번호는 1~5여야 합니다."; return false; }
        if (_departureArea == null) { error = "관람 출발 영역이 필요합니다."; return false; }
        if (_observationWaypoints == null || _observationWaypoints.Length == 0)
        { error = "관람 경유점이 하나 이상 필요합니다."; return false; }
        if (_returnWaypoints == null) { error = "복귀 경유점 목록이 필요합니다."; return false; }
        if (!ValidWaypoints(_observationWaypoints) || !ValidWaypoints(_returnWaypoints))
        { error = "관람·복귀 경유점에 누락된 Transform이 있습니다."; return false; }
        if (_dialogue == null) { error = "관람 DialogueData가 필요합니다."; return false; }
        if (!OverworldFollowerObservationRoute.Finite(_moveDuration) || _moveDuration < 0f)
        { error = "이동 시간은 유한한 0 이상이어야 합니다."; return false; }
        error = string.Empty;
        return true;
    }

    private static bool ValidWaypoints(Transform[] points)
    {
        for (int i = 0; i < points.Length; i++) if (points[i] == null) return false;
        return true;
    }

    private void LateUpdate() => Tick();

    private void Tick()
    {
        if (_phase == Phase.Dormant) return;
        if (_actor == null && _phase == Phase.WaitingForEntry)
        {
            if (_controller == null || _leader == null) { StopOwnedWork(); return; }
            // 세션의 파티 준비 전에는 optional slot 누락으로 확정하지 않습니다.
            GlobalDataManager current = GlobalDataManager.Instance;
            if (current == null || current.Party == null || current.Party.Count == 0) return;
            if (!_controller.TryGetFollower(_slot, out _actor))
            {
                if (!_optionalMissing) Debug.LogWarning("관람 대상으로 지정한 동료가 없습니다: " + _slot, this);
                StopOwnedWork();
                return;
            }
            _global = current;
            _party = current.Party;
            _partyCount = _party.Count;
            for (int i = 0; i < _members.Length; i++) _members[i] = i < _partyCount ? _party[i] : null;
            _global.PartyChanged += OnPartyChanged;
            _epoch = _controller.TrailEpoch;
        }
        if (!HasCurrentActor()) { StopOwnedWork(); return; }
        if (_controller.TrailEpoch != _epoch)
        {
            if (_phase != Phase.WaitingForEntry) { StopOwnedWork(); return; }
            _epoch = _controller.TrailEpoch;
            _hasSample = false;
            _leaderEntered = false;
        }
        if (_move == null && _actor.IsScriptedMoving) { StopOwnedWork(); return; }

        if (_phase == Phase.WaitingForEntry) TickEntry();
        else if (_phase == Phase.Departing || _phase == Phase.Returning) TickMove();
        else if (_phase == Phase.ClosingTalk || _phase == Phase.ClosingChoice) TickClosing();
        else if (_phase == Phase.Holding)
        {
            if (_actor.Mode != OverworldFollowerMode.Hold) { StopOwnedWork(); return; }
            if (_failurePending && OverworldActionGate.AllowsWorldActions && CanOpenDialogue()) ShowFailure();
        }
        UpdateProxy();
    }

    private void TickEntry()
    {
        Vector3 position = _actor.transform.position;
        Vector3 leaderPosition = _leader.transform.position;
        if (!_hasSample)
        {
            _previousActorPosition = position;
            _previousLeaderPosition = leaderPosition;
            _hasSample = true;
        }
        _leaderEntered |= CrossesDeparture(_previousLeaderPosition, leaderPosition);
        bool crosses = CrossesDeparture(_previousActorPosition, position);
        _previousActorPosition = position;
        _previousLeaderPosition = leaderPosition;
        if (!_leaderEntered || !crosses || !OverworldActionGate.AllowsWorldActions
            || _actor.Mode != OverworldFollowerMode.Follow || !CanOpenDialogue()) return;
        if (!FillPath(_observationWaypoints, _departurePath, false, out string error)
            || !TryStartMove(_departurePath, false, out error)) RecoverHold(error);
    }

    private bool CrossesDeparture(Vector3 from, Vector3 to)
    {
        if (_departureArea == null) return false;
        Transform area = _departureArea.transform;
        return OverworldFollowerObservationRoute.CrossesBox(area.InverseTransformPoint(from), area.InverseTransformPoint(to),
            _departureArea.offset, _departureArea.size);
    }

    private bool FillPath(Transform[] source, Vector3[] destination, bool appendLeader, out string error)
    {
        if (!ValidWaypoints(source)) { error = "이동 경유점이 사라졌습니다."; return false; }
        for (int i = 0; i < source.Length; i++) destination[i] = source[i].position;
        if (appendLeader) destination[destination.Length - 1] = _leader.transform.position;
        return IsPathClear(_actor.transform.position, destination, out error);
    }

    private bool IsPathClear(Vector3 from, Vector3[] path, out string error)
    {
        if (!Finite(from)) { error = "동료 좌표가 유효하지 않습니다."; return false; }
        Vector2 previous = from;
        for (int i = 0; i < path.Length; i++)
        {
            if (!Finite(path[i])) { error = "경유점 좌표가 유효하지 않습니다."; return false; }
            Vector2 end = path[i];
            if (!IsSegmentClear(previous, end)) { error = "리더 몸체 크기로 검사한 관람 경로가 장애물에 막혔습니다."; return false; }
            previous = end;
        }
        error = string.Empty;
        return true;
    }

    private bool IsSegmentClear(Vector2 from, Vector2 to)
    {
        int overlaps = Physics2D.OverlapBox(from + _bodyOffset, _bodySize, 0f, _obstacleFilter, _overlapHits);
        if (overlaps == _overlapHits.Length) return false;
        for (int i = 0; i < overlaps; i++) if (IsObstacle(_overlapHits[i])) return false;
        Vector2 delta = to - from;
        float distance = delta.magnitude;
        if (distance <= 0.00001f) return true;
        int hits = Physics2D.BoxCast(from + _bodyOffset, _bodySize, 0f, delta / distance, _obstacleFilter, _castHits, distance);
        if (hits == _castHits.Length) return false;
        for (int i = 0; i < hits; i++) if (IsObstacle(_castHits[i].collider)) return false;
        return true;
    }

    private bool IsObstacle(Collider2D collider)
    {
        if (collider == null || collider.isTrigger) return false;
        for (int i = 0; i < _leaderColliders.Length; i++) if (_leaderColliders[i] == collider) return false;
        return true;
    }

    private bool TryStartMove(Vector3[] path, bool rejoin, out string error)
    {
        _phase = rejoin ? Phase.Returning : Phase.Departing;
        _activePath = path;
        _moveStart = _actor.transform.position;
        UpdateProxy();
        if (_controller.TryBeginMove(_slot, path, _moveDuration, rejoin, out _move, out error)) return true;
        _move = null;
        return false;
    }

    private void TickMove()
    {
        if (_move == null) { RecoverHold("관람 이동 명령이 종료되었습니다."); return; }
        if (!_move.IsDone)
        {
            // 명시 이동 중에만 검사합니다. 일반 추종 프레임에는 물리 질의를 하지 않습니다.
            if (!IsPathClear(_moveStart, _activePath, out string error)) RecoverHold(error);
            else if (_phase == Phase.Returning && (!_ownsJoinLock
                || (_leader.transform.position - _joinDestination).sqrMagnitude > 0.000001f))
                RecoverHold("합류 중 리더 위치 또는 입력 소유권이 바뀌었습니다.");
            else if (_phase == Phase.Departing && !OverworldActionGate.AllowsWorldActions)
                RecoverHold("다른 연출이 시작되어 관람 이동을 중단했습니다.");
            return;
        }
        OverworldFollowerMoveCommand finished = _move;
        bool wasReturning = _phase == Phase.Returning;
        uint generation = _actionGeneration;
        _move = null;
        ReleaseJoinLock();
        // 입력 복원 통지가 같은 프레임에 방을 교체할 수 있습니다.
        if (!IsCurrentAction(generation)) return;
        if (_actor.IsScriptedMoving || (!finished.WasCompleted && _actor.Mode != OverworldFollowerMode.Hold))
        { StopOwnedWork(); return; }
        if (!finished.WasCompleted) { RecoverHold(finished.Error); return; }
        _phase = wasReturning ? Phase.Complete : Phase.Holding;
    }

    public bool CanInteract(PlayerController player)
        => player != null && player == _leader && _phase == Phase.Holding && !_failurePending
            && HasCurrentActor() && _actor.Mode == OverworldFollowerMode.Hold && !_actor.IsScriptedMoving
            && OverworldActionGate.AllowsWorldActions && CanOpenDialogue();

    public bool TryInteract(PlayerController player)
    {
        if (!CanInteract(player)) return false;
        uint generation = ++_actionGeneration;
        _phase = Phase.Talking;
        UpdateProxy();
        return StartOwnedDialogue(_dialogue, generation, true);
    }

    private void OnTalkCompleted(uint generation)
    {
        if (!IsCurrentAction(generation)) return;
        _phase = Phase.ClosingTalk;
    }

    private void OnTalkCancelled(uint generation)
    {
        if (!IsCurrentAction(generation)) return;
        _phase = Phase.Holding;
        _dialogueOwner = null;
        _dialogueGeneration = 0;
        UpdateProxy();
    }

    private void TickClosing()
    {
        if (_dialogueOwner == null || _dialogueOwner != DialogueManager.Instance
            || _dialogueOwner.PlaybackGeneration != _dialogueGeneration)
        { CancelOwnedDialogue(); _phase = Phase.Holding; return; }
        if (_dialogueOwner.IsPresentationVisible) return;
        if (!OverworldActionGate.AllowsWorldActions) { CancelOwnedDialogue(); _phase = Phase.Holding; return; }
        if (_phase == Phase.ClosingTalk) OpenChoices();
        else
        {
            _dialogueOwner = null;
            _dialogueGeneration = 0;
            _phase = Phase.Holding;
            if (_joinSelected && !TryJoin(out string error)) RecoverHold(error);
        }
    }

    private void OpenChoices()
    {
        uint generation = _actionGeneration;
        DialogueManager owner = _dialogueOwner;
        if (owner == null) { _phase = Phase.Holding; return; }
        int expectedPlayback = unchecked(owner.PlaybackGeneration + 1);
        _phase = Phase.Choosing;
        _joinSelected = false;
        bool started = false;
        _startingDialogue = true;
        try
        {
            started = owner.TryStartLocalizedChoicePrompt(_promptKey, _promptFallback,
                _choiceKeys, _choiceFallbacks, index => OnChoiceSelected(generation, index), () => OnTalkCancelled(generation));
        }
        finally
        {
            _startingDialogue = false;
            FinishDialogueStart(owner, expectedPlayback, generation, started);
        }
    }

    private void OnChoiceSelected(uint generation, int index)
    {
        if (!IsCurrentAction(generation)) return;
        _joinSelected = index == 0;
        _phase = Phase.ClosingChoice;
    }

    /// <summary>복귀 경유점 끝에 현재 리더 위치를 추가하고, 자기 Cutscene 동안만 명시 이동합니다.</summary>
    public bool TryJoin(out string error)
    {
        if (_phase != Phase.Holding || !HasCurrentActor() || _actor.IsScriptedMoving || !CanOpenDialogue())
        { error = "현재 관람 동료가 합류할 수 없습니다."; return false; }
        if (!FillPath(_returnWaypoints, _returnPath, true, out error)) return false;
        if (!AcquireJoinLock()) { error = "현재 리더 입력을 소유할 수 없습니다."; return false; }
        _joinDestination = _leader.transform.position;
        ++_actionGeneration;
        if (TryStartMove(_returnPath, true, out error)) return true;
        ReleaseJoinLock();
        _phase = Phase.Holding;
        UpdateProxy();
        return false;
    }

    private bool AcquireJoinLock()
    {
        GameStateManager state = GameStateManager.Instance;
        if (state == null || state.CurrentState != GameState.Exploration) return false;
        uint generation = _actionGeneration;
        _stateOwner = state;
        _ownsJoinLock = true;
        state.OnStateChanged += OnStateChanged;
        state.ChangeState(GameState.Cutscene);
        if (!IsCurrentAction(generation) || !_ownsJoinLock || state != GameStateManager.Instance
            || state.CurrentState != GameState.Cutscene)
        {
            ReleaseJoinLock();
            return false;
        }
        _leader.StopOverworldMovement();
        return true;
    }

    private void OnStateChanged(GameState state)
    {
        if (_ownsJoinLock && state != GameState.Cutscene)
        {
            _ownsJoinLock = false;
            RecoverHold("다른 상태 소유자가 합류를 중단했습니다.");
        }
    }

    private void ReleaseJoinLock()
    {
        GameStateManager owner = _stateOwner;
        bool restore = _ownsJoinLock;
        _stateOwner = null;
        _ownsJoinLock = false;
        if (owner == null) return;
        owner.OnStateChanged -= OnStateChanged;
        if (restore && owner == GameStateManager.Instance && owner.CurrentState == GameState.Cutscene)
            owner.ChangeState(GameState.Exploration);
    }

    private void RecoverHold(string error)
    {
        if (_resolving) return;
        _resolving = true;
        uint generation = ++_actionGeneration;
        CancelOwnedDialogue();
        OverworldFollowerMoveCommand owned = _move;
        _move = null;
        owned?.Cancel();
        ReleaseJoinLock();
        if (generation != _actionGeneration) { _resolving = false; return; }
        LastError = string.IsNullOrWhiteSpace(error) ? _blockedFallback : error;
        if (HasCurrentActor() && !_actor.IsScriptedMoving && _controller.TrailEpoch == _epoch)
        {
            if (_actor.Mode == OverworldFollowerMode.Follow)
                _controller.TrySetMode(_slot, OverworldFollowerMode.Hold, out _);
            _phase = Phase.Holding;
            _failurePending = true;
        }
        else _phase = Phase.Dormant;
        _resolving = false;
        UpdateProxy();
    }

    private void ShowFailure()
    {
        DialogueManager manager = DialogueManager.Instance;
        if (manager == null) { _failurePending = false; Debug.LogWarning(LastError, this); return; }
        if (_failureDialogue == null)
        {
            _failureDialogue = ScriptableObject.CreateInstance<DialogueData>();
            _failureDialogue.name = "Follower Observation Failure (runtime)";
            _failureDialogue.hideFlags = HideFlags.HideAndDontSave;
            _failureDialogue.Nodes.Add(new DialogueNode { LocalizationKey = _blockedKey, DefaultText = _blockedFallback });
        }
        uint generation = ++_actionGeneration;
        _phase = Phase.Talking;
        _failurePending = false;
        StartOwnedDialogue(_failureDialogue, generation, false);
    }

    private bool StartOwnedDialogue(DialogueData data, uint generation, bool offerChoices)
    {
        DialogueManager owner = DialogueManager.Instance;
        if (owner == null) { _phase = Phase.Holding; UpdateProxy(); return false; }
        _dialogueOwner = owner;
        int expectedPlayback = unchecked(owner.PlaybackGeneration + 1);
        int playback = expectedPlayback;
        bool started = false;
        bool retained = false;
        _startingDialogue = true;
        try
        {
            started = owner.TryStartDialogue(data,
                () => { if (offerChoices) OnTalkCompleted(generation); else OnChoiceSelected(generation, 1); },
                () => OnTalkCancelled(generation), null, out playback);
        }
        finally
        {
            _startingDialogue = false;
            retained = FinishDialogueStart(owner, playback == 0 ? expectedPlayback : playback, generation, started);
        }
        return retained;
    }

    private bool FinishDialogueStart(DialogueManager owner, int playback, uint generation, bool started)
    {
        bool stillCurrent = IsCurrentAction(generation)
            && (_phase == Phase.Talking || _phase == Phase.ClosingTalk || _phase == Phase.Choosing || _phase == Phase.ClosingChoice);
        if (started && stillCurrent && owner != null && owner == DialogueManager.Instance && owner.PlaybackGeneration == playback)
        {
            _dialogueOwner = owner;
            _dialogueGeneration = playback;
            return true;
        }
        // 시작 호출의 상태 변경/노드 이벤트가 Room을 비활성화할 수 있습니다.
        // Manager의 시작 스택이 끝난 뒤 해당 세대만 닫아 내부 패널 참조와 더 최신 대화를 보존합니다.
        if (owner != null)
        {
            owner.CancelDialogue(playback);
            owner.HideFinishedPresentation(playback);
        }
        if (generation == _actionGeneration)
        {
            _dialogueOwner = null;
            _dialogueGeneration = 0;
            _phase = HasCurrentActor() ? Phase.Holding : Phase.Dormant;
            UpdateProxy();
        }
        return false;
    }

    private bool CanOpenDialogue()
        => DialogueManager.Instance == null || !DialogueManager.Instance.IsPresentationVisible;

    private bool HasCurrentActor()
    {
        if (!isActiveAndEnabled || _controller == null || !_controller.isActiveAndEnabled
            || _leader == null || !_leader.isActiveAndEnabled || _controller.Leader != _leader
            || _controller.IsBattleHidden || _leader.IsBattleEntryHeld
            || _leader.State == PlayerController.PlayerState.InBattle
            || _actor == null || !_actor.isActiveAndEnabled || _actor.Slot != _slot || !OwnsParty()) return false;
        return true;
    }

    private bool OwnsParty()
    {
        if (_global == null || _global != GlobalDataManager.Instance || !ReferenceEquals(_party, _global.Party)
            || _party == null || _party.Count != _partyCount) return false;
        for (int i = 0; i < _members.Length && i < _partyCount; i++)
            if (!ReferenceEquals(_members[i], _party[i])) return false;
        return true;
    }

    private bool IsCurrentAction(uint generation)
        => generation == _actionGeneration && HasCurrentActor() && _controller.TrailEpoch == _epoch;

    private void OnPartyChanged()
    {
        if (!OwnsParty()) StopOwnedWork();
    }

    private void UpdateProxy()
    {
        if (_interaction == null) return;
        bool available = _phase == Phase.Holding && _actor != null && !_actor.IsScriptedMoving;
        _interaction.SetAvailable(available, _actor != null ? _actor.transform.position : transform.position);
    }

    private void CancelOwnedDialogue()
    {
        DialogueManager owner = _dialogueOwner;
        int generation = _dialogueGeneration;
        _dialogueOwner = null;
        _dialogueGeneration = 0;
        // TryStart...는 AcquireDialogueState 직후 같은 내부 패널을 계속 사용합니다.
        // OnDisable 재진입 시 호출 중 취소하지 않고 시작 함수의 finally가 정리합니다.
        if (_startingDialogue || owner == null || generation == 0) return;
        owner.CancelDialogue(generation);
        owner.HideFinishedPresentation(generation);
    }

    /// <summary>외부 Room/세션 종료도 자기 대화·명령만 취소합니다. 도착 재배치는 기존 Map이 담당합니다.</summary>
    public void StopOwnedWork()
    {
        ++_actionGeneration;
        _phase = Phase.Dormant;
        _failurePending = false;
        _joinSelected = false;
        if (_global != null) _global.PartyChanged -= OnPartyChanged;
        CancelOwnedDialogue();
        OverworldFollowerMoveCommand owned = _move;
        _move = null;
        owned?.Cancel();
        ReleaseJoinLock();
        _actor = null;
        _global = null;
        _party = null;
        UpdateProxy();
    }

    private void OnDisable() => StopOwnedWork();
    private void OnDestroy()
    {
        StopOwnedWork();
        if (_failureDialogue != null)
        {
            if (Application.isPlaying) Destroy(_failureDialogue);
            else DestroyImmediate(_failureDialogue);
        }
    }

    private static bool Finite(Vector3 value)
        => OverworldFollowerObservationRoute.Finite(value.x) && OverworldFollowerObservationRoute.Finite(value.y)
            && OverworldFollowerObservationRoute.Finite(value.z);
}
