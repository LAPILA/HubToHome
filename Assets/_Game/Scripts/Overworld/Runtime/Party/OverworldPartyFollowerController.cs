using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>실제 파티의 표시만 소유합니다. 저장·카메라·전투 파티를 변경하지 않습니다.</summary>
[DisallowMultipleComponent]
[AddComponentMenu("Hub To Home/필드/동료 추종 제어")]
public sealed class OverworldPartyFollowerController : MonoBehaviour
{
    [SerializeField, Range(0, 5), LabelText("표시할 최대 동료 수")]
    [Tooltip("기본 2명(리더 포함 전열 3명), 최대 5명(전체 6명)입니다. 실제 저장 파티만 표시합니다.")]
    private int _maxFollowers = 2;
    [SerializeField, Min(0.05f), LabelText("동료 간 경로 간격")]
    [Tooltip("원본 픽셀 단위로 맞춥니다. 기본 1.5는 32 PPU에서 48픽셀 간격입니다.")]
    private float _spacing = 1.5f;
    [SerializeField, Min(0f), LabelText("재출발 여유 거리")]
    [Tooltip("자연스럽게 멈춘 뒤 앞 동료가 이 거리만큼 더 가면 출발합니다. 기본 0.5는 16픽셀이며 0이면 즉시 출발합니다.")]
    private float _restartDistance = 0.5f;
    [SerializeField, Range(1f, 3f), LabelText("따라잡기 속도 상한 배율")]
    private float _catchupMultiplier = 1.5f;
    [SerializeField, Min(0.001f), LabelText("경로 복귀 위치 허용 오차")]
    private float _rejoinTolerance = 0.02f;
    [SerializeField, Min(1f), LabelText("예외적 순간 이동 감지 거리")]
    [Tooltip("정상 방 이동은 도착 API에서 명시적으로 재설정합니다. 이 값은 외부의 큰 좌표 변경을 보호하는 보조 장치입니다.")]
    private float _teleportDistance = 6f;

    private const int MaximumFollowers = 5;
    private readonly OverworldFollowerActor[] _actors = new OverworldFollowerActor[MaximumFollowers];
    private readonly CharacterSaveData[] _observedMembers = new CharacterSaveData[MaximumFollowers + 1];
    private readonly float[] _preparedFollowDistances = new float[MaximumFollowers];
    private readonly OverworldFollowerTrail _trail = new OverworldFollowerTrail(1024);
    private PlayerController _leader;
    private PlayerCharacter _leaderCharacter;
    private GlobalDataManager _global;
    private List<CharacterSaveData> _observedParty;
    private CharacterSaveData _observedLeader;
    private GameObject _visualRoot;
    private int _observedCount = -1;
    private int _observedMaximum = -1;
    private int _followerCount;
    private bool _rosterDirty = true;
    private bool _battleHidden;
    private bool _leaderUnavailable;
    private Vector3 _lastLeaderPosition;

    public PlayerController Leader => _leader;
    public int FollowerCount => _followerCount;
    public int MaxFollowers => _maxFollowers;
    // 좌표 자체를 반올림하면 모서리에서 경로를 벗어납니다. 간격만 격자에 맞춥니다.
    public float EffectiveSpacing
    {
        get
        {
            const int ppu = GameConfigPolicy.WorldPixelsPerUnit;
            double spacing = Finite(_spacing) ? Math.Max(0.05d, _spacing) : 1.5d;
            return (float)(Math.Round(spacing * ppu) / ppu);
        }
    }
    public float EffectiveRestartDistance
    {
        get
        {
            const int ppu = GameConfigPolicy.WorldPixelsPerUnit;
            double distance = Finite(_restartDistance) ? Math.Max(0d, _restartDistance) : 0.5d;
            return Mathf.Min(EffectiveSpacing, (float)(Math.Round(distance * ppu) / ppu));
        }
    }
    public uint TrailEpoch => _trail.Epoch;
    public bool IsBattleHidden => _battleHidden;

    public static OverworldPartyFollowerController For(PlayerController leader)
        => leader != null ? leader.GetComponent<OverworldPartyFollowerController>() : null;

    public static OverworldPartyFollowerController EnsureFor(PlayerController leader)
    {
        if (leader == null || leader.State == PlayerController.PlayerState.InBattle || leader.IsBattleEntryHeld)
            return null;
        // 실험실은 별도 표시·카메라 소유자가 있습니다. 씬 이름을 추측하지 않습니다.
        var labs = UnityEngine.Object.FindObjectsByType<BunnySlimeBattleLabSession>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < labs.Length; i++)
            if (labs[i] != null && labs[i].gameObject.scene == leader.gameObject.scene) return null;
        var existing = For(leader);
        if (existing != null)
        {
            if (!ReferenceEquals(existing._leader, leader)) existing.Configure(leader);
            return existing;
        }
        var controller = leader.gameObject.AddComponent<OverworldPartyFollowerController>();
        controller.Configure(leader);
        return controller;
    }

    /// <summary>기존 씬 참조가 없어도 자동 연결되며, 명시 연결과 테스트에도 사용합니다.</summary>
    public void Configure(PlayerController leader, int maxFollowers = -1)
    {
        UnsubscribeGlobal();
        CancelAllCommands();
        _leader = leader;
        _leaderCharacter = leader != null ? leader.GetComponent<PlayerCharacter>() : null;
        if (maxFollowers >= 0) _maxFollowers = Mathf.Clamp(maxFollowers, 0, MaximumFollowers);
        _leaderUnavailable = leader == null || !leader.isActiveAndEnabled;
        _battleHidden = leader != null && leader.State == PlayerController.PlayerState.InBattle;
        _rosterDirty = true;
        BindGlobal();
        ResetAtLeader();
    }

    private void OnEnable()
    {
        if (_leader == null) return;
        BindGlobal();
        NotifyLeaderEnabled();
    }

    private void LateUpdate() => Tick(Time.deltaTime);

    internal void Tick(float deltaTime)
    {
        if (_leader == null) return;
        if (!_leader.isActiveAndEnabled)
        {
            if (!_leaderUnavailable) NotifyLeaderDisabled();
            return;
        }
        if (_leaderUnavailable) NotifyLeaderEnabled();
        BindGlobal();
        SynchronizeRoster();
        bool battle = _leader.State == PlayerController.PlayerState.InBattle
            || (GameStateManager.Instance != null && GameStateManager.Instance.CurrentState == GameState.Battle);
        if (battle != _battleHidden) SetBattleHidden(battle);
        if (_battleHidden) return;

        Vector3 position = _leader.transform.position;
        if ((position - _lastLeaderPosition).sqrMagnitude > _teleportDistance * _teleportDistance)
        {
            ResetAtLeader();
            return;
        }
        _lastLeaderPosition = position;
        // 정지 중 연출자가 리더를 움직여도 실제 이동점을 보관하여 재개 시 직선으로 잇지 않습니다.
        _trail.Record(position, _leader.FacingDirection);
        bool canFollow = !_leader.IsBattleEntryHeld
            && (GameStateManager.Instance == null || GameStateManager.Instance.CanPlayerMove);
        if (!canFollow)
        {
            for (int i = 0; i < _followerCount; i++) _actors[i]?.StopFollowingAnimation();
            return;
        }

        float delta = Finite(deltaTime) ? Mathf.Max(0f, deltaTime) : 0f;
        float speed = Mathf.Max(0f, _leader.CurrentOverworldMoveSpeed);
        float spacing = EffectiveSpacing;
        float restartDistance = EffectiveRestartDistance;
        float catchup = Finite(_catchupMultiplier) ? Mathf.Min(3f, Mathf.Max(1f, _catchupMultiplier)) : 1.5f;
        for (int i = 0; i < _followerCount; i++)
        {
            OverworldFollowerActor actor = _actors[i];
            if (actor == null) continue;
            // 정상 행렬은 앞 동료의 실제 커서를 따릅니다. 관람/이탈한 동료가
            // 뒤 행렬을 막지 않도록 그 경우에만 기존 리더 기준 슬롯 목표를 씁니다.
            float target = _trail.HeadDistance - spacing * (i + 1);
            OverworldFollowerActor previous = i > 0 ? _actors[i - 1] : null;
            if (previous != null && previous.Mode == OverworldFollowerMode.Follow
                && !previous.IsScriptedMoving && previous.HasTrailCursor && previous.TrailEpoch == _trail.Epoch)
                target = previous.TrailDistance - spacing;
            target = Mathf.Max(_trail.OldestDistance, target);
            float budget = speed * delta;
            float excess = Mathf.Max(0f, target - actor.TrailDistance - budget);
            float multiplier = 1f + (catchup - 1f) * Mathf.Min(1f, excess / spacing);
            actor.TickFollowStaggered(_trail, target, budget * multiplier, _rejoinTolerance, restartDistance);
        }
    }

    public void ResetAtLeader()
    {
        CancelAllCommands();
        if (_leader == null) return;
        _trail.Reset(_leader.transform.position, _leader.FacingDirection);
        _lastLeaderPosition = _leader.transform.position;
        BindGlobal();
        SynchronizeRoster();
        for (int i = 0; i < _followerCount; i++)
        {
            var actor = _actors[i];
            if (actor == null) continue;
            actor.ResetPose(_trail, _leader.transform.position, _leader.FacingDirection);
            actor.SetBattleHidden(_battleHidden);
            actor.SetOwnerHidden(_leaderUnavailable);
        }
    }

    public void SetBattleHidden(bool hidden)
    {
        bool wasHidden = _battleHidden;
        _battleHidden = hidden;
        if (hidden) CancelAllCommands();
        for (int i = 0; i < _followerCount; i++) _actors[i]?.SetBattleHidden(hidden);
        if (wasHidden && !hidden) ResetAtLeader();
    }

    internal void NotifyLeaderDisabled()
    {
        _leaderUnavailable = true;
        CancelAllCommands();
        if (_leader != null) _trail.Reset(_leader.transform.position, _leader.FacingDirection);
        for (int i = 0; i < _followerCount; i++) _actors[i]?.SetOwnerHidden(true);
    }

    internal void NotifyLeaderEnabled()
    {
        _leaderUnavailable = false;
        ResetAtLeader();
    }

    public bool TryGetFollower(int slot, out OverworldFollowerActor actor)
    {
        BindGlobal();
        SynchronizeRoster();
        actor = slot >= 1 && slot <= _followerCount ? _actors[slot - 1] : null;
        return actor != null;
    }

    public bool TrySetMode(int slot, OverworldFollowerMode mode, out string error)
    {
        if (!CanControl(out error) || !ValidateSelection(slot, out error)) return false;
        if (mode != OverworldFollowerMode.Follow && mode != OverworldFollowerMode.Hold && mode != OverworldFollowerMode.Detached)
        { error = "지원하지 않는 동료 이동 상태입니다."; return false; }
        int first = slot == 0 ? 0 : slot - 1;
        int end = slot == 0 ? _followerCount : slot;
        if (mode == OverworldFollowerMode.Follow)
            for (int i = first; i < end; i++)
                if (!_actors[i].TryResolveFollow(_trail, _rejoinTolerance, out _preparedFollowDistances[i], out error))
                    return false;
        for (int i = first; i < end; i++)
            _actors[i].CommitMode(mode, _trail, _preparedFollowDistances[i]);
        error = string.Empty;
        return true;
    }

    public bool TrySetVisible(int slot, bool visible, out string error)
    {
        if (!CanControl(out error) || !ValidateSelection(slot, out error)) return false;
        int first = slot == 0 ? 0 : slot - 1;
        int end = slot == 0 ? _followerCount : slot;
        for (int i = first; i < end; i++) _actors[i].SetVisible(visible);
        error = string.Empty;
        return true;
    }

    /// <summary>암전·도착용 명시 재배치입니다. 일반 재합류 실패의 자동 우회로가 아닙니다.</summary>
    public bool TryGather(out string error)
    {
        if (!CanControl(out error) || !ValidateSelection(0, out error)) return false;
        ResetAtLeader();
        error = string.Empty;
        return true;
    }

    public bool TryBeginMove(int slot, IReadOnlyList<Vector3> waypoints, float duration, bool rejoin,
        out OverworldFollowerMoveCommand command, out string error)
    {
        command = null;
        if (!CanControl(out error)) return false;
        if (slot == 0) { error = "개별 이동에는 전체 선택(0)을 사용할 수 없습니다."; return false; }
        if (!ValidateSelection(slot, out error)) return false;
        return _actors[slot - 1].TryBeginMove(waypoints, duration, rejoin, out command, out error);
    }

    internal bool TryResumeActor(OverworldFollowerActor actor, out string error)
    {
        if (actor == null || _battleHidden || _leaderUnavailable)
        { error = "현재 동료를 필드 경로에 복귀시킬 수 없습니다."; return false; }
        if (!actor.TryResolveFollow(_trail, _rejoinTolerance, out float distance, out error)) return false;
        actor.CommitMode(OverworldFollowerMode.Follow, _trail, distance);
        return true;
    }

    [Button("모두 추종"), Tooltip("보관 경로 위의 동료만 재개합니다. 기록 만료/경로 이탈은 실패하며 순간 이동하지 않습니다.")]
    public void FollowAll() { if (!TrySetMode(0, OverworldFollowerMode.Follow, out string error)) Report(error); }
    [Button("모두 고정")]
    public void HoldAll() { if (!TrySetMode(0, OverworldFollowerMode.Hold, out string error)) Report(error); }
    [Button("모두 임시 이탈"), Tooltip("표시 추종만 제외합니다. 실제 저장/전투 파티에서는 빠지지 않습니다.")]
    public void DetachAll() { if (!TrySetMode(0, OverworldFollowerMode.Detached, out string error)) Report(error); }
    [Button("모두 숨기기")]
    public void HideAll() { if (!TrySetVisible(0, false, out string error)) Report(error); }
    [Button("모두 보이기")]
    public void ShowAll() { if (!TrySetVisible(0, true, out string error)) Report(error); }
    [Button("암전 중 재집결"), Tooltip("연출자가 암전/도착 경계를 보장한 뒤 호출하세요. 리더 위치에 모으고 상태를 기본 추종/표시로 초기화합니다.")]
    public void Gather() { if (!TryGather(out string error)) Report(error); }

    // UnityEvent는 bool/out API 대신 이 단일 번호 인자 진입점을 사용할 수 있습니다.
    public void FollowFollower(int slot) { if (!TrySetMode(slot, OverworldFollowerMode.Follow, out string error)) Report(error); }
    public void HoldFollower(int slot) { if (!TrySetMode(slot, OverworldFollowerMode.Hold, out string error)) Report(error); }
    public void DetachFollower(int slot) { if (!TrySetMode(slot, OverworldFollowerMode.Detached, out string error)) Report(error); }
    public void HideFollower(int slot) { if (!TrySetVisible(slot, false, out string error)) Report(error); }
    public void ShowFollower(int slot) { if (!TrySetVisible(slot, true, out string error)) Report(error); }

    private bool CanControl(out string error)
    {
        if (_leader == null || !_leader.isActiveAndEnabled || _leaderUnavailable)
        { error = "현재 활성 필드 리더가 없습니다."; return false; }
        if (_battleHidden || _leader.State == PlayerController.PlayerState.InBattle)
        { error = "전투 중에는 필드 동료를 제어할 수 없습니다."; return false; }
        BindGlobal();
        SynchronizeRoster();
        error = string.Empty;
        return true;
    }

    private bool ValidateSelection(int slot, out string error)
    {
        if (slot < 0 || slot > MaximumFollowers)
        { error = "동료 번호는 0(전체) 또는 1~5여야 합니다."; return false; }
        if (_followerCount == 0 || (slot > 0 && (slot > _followerCount || _actors[slot - 1] == null)))
        { error = "선택한 번호에 표시할 동료가 없습니다."; return false; }
        if (slot == 0)
            for (int i = 0; i < _followerCount; i++)
                if (_actors[i] == null) { error = "일부 동료의 표시 원본이 없어 전체 명령을 적용하지 않았습니다."; return false; }
        error = string.Empty;
        return true;
    }

    private void BindGlobal()
    {
        GlobalDataManager current = GlobalDataManager.Instance;
        if (ReferenceEquals(current, _global)) return;
        UnsubscribeGlobal();
        _global = current;
        if (_global != null) _global.PartyChanged += HandlePartyChanged;
        _rosterDirty = true;
    }

    private void UnsubscribeGlobal()
    {
        if (_global != null) _global.PartyChanged -= HandlePartyChanged;
        _global = null;
    }

    private void HandlePartyChanged() => _rosterDirty = true;

    private CharacterSaveData ResolveLeaderMember(List<CharacterSaveData> party)
    {
        if (party == null || _leaderCharacter == null) return null;
        for (int i = 0; i < party.Count; i++)
            if (_leaderCharacter.IsBoundToSaveData(party[i])) return party[i];
        string id = _leaderCharacter.CharacterData != null ? _leaderCharacter.CharacterData.CharacterID : null;
        if (string.IsNullOrWhiteSpace(id)) return null;
        for (int i = 0; i < party.Count; i++)
            if (party[i] != null && string.Equals(party[i].CharacterDataID, id, StringComparison.Ordinal)) return party[i];
        return null;
    }

    private void SynchronizeRoster()
    {
        if (_leader == null) return;
        List<CharacterSaveData> party = _global != null ? _global.Party : null;
        int count = party != null ? party.Count : 0;
        CharacterSaveData leaderMember = ResolveLeaderMember(party);
        bool changed = _rosterDirty || !ReferenceEquals(party, _observedParty) || count != _observedCount
            || !ReferenceEquals(leaderMember, _observedLeader) || _observedMaximum != _maxFollowers;
        int tracked = Mathf.Min(count, MaximumFollowers + 1);
        for (int i = 0; i < tracked; i++)
            if (!ReferenceEquals(_observedMembers[i], party[i])) changed = true;
        if (!changed) return;
        _rosterDirty = false;
        _observedParty = party;
        _observedCount = count;
        _observedMaximum = _maxFollowers = Mathf.Clamp(_maxFollowers, 0, MaximumFollowers);
        _observedLeader = leaderMember;
        for (int i = 0; i < _observedMembers.Length; i++) _observedMembers[i] = i < tracked ? party[i] : null;
        CancelAllCommands();
        var previous = new OverworldFollowerActor[MaximumFollowers];
        Array.Copy(_actors, previous, MaximumFollowers);
        Array.Clear(_actors, 0, MaximumFollowers);
        _followerCount = 0;
        if (leaderMember != null)
        {
            for (int i = 0; i < count && _followerCount < _maxFollowers; i++)
            {
                CharacterSaveData member = party[i];
                if (member == null || ReferenceEquals(member, leaderMember)) continue;
                OverworldFollowerActor actor = null;
                for (int j = 0; j < previous.Length; j++)
                    if (previous[j] != null && ReferenceEquals(previous[j].Member, member))
                    { actor = previous[j]; previous[j] = null; break; }
                int slot = _followerCount + 1;
                if (actor == null) actor = CreateActor(member, slot);
                if (actor != null) actor.Slot = slot;
                _actors[_followerCount++] = actor;
            }
        }
        for (int i = 0; i < previous.Length; i++)
            if (previous[i] != null) DestroyOwned(previous[i].gameObject);
    }

    private OverworldFollowerActor CreateActor(CharacterSaveData member, int slot)
    {
        if (_visualRoot == null)
        {
            _visualRoot = new GameObject("Overworld Party Followers - Visual Only");
            if (_leader.gameObject.scene.IsValid()) SceneManager.MoveGameObjectToScene(_visualRoot, _leader.gameObject.scene);
        }
        GameObject actorObject = new GameObject("Follower " + slot);
        actorObject.transform.SetParent(_visualRoot.transform, false);
        var actor = actorObject.AddComponent<OverworldFollowerActor>();
        CharacterData data = CharacterDatabase.FindById(member.CharacterDataID);
        if (!actor.Initialize(this, member, slot, data, _leader, out string error))
        {
            Report(error);
            DestroyOwned(actorObject);
            return null;
        }
        actor.ResetPose(_trail, _leader.transform.position, _leader.FacingDirection);
        actor.SetBattleHidden(_battleHidden);
        actor.SetOwnerHidden(_leaderUnavailable);
        return actor;
    }

    private void CancelAllCommands()
    {
        for (int i = 0; i < _actors.Length; i++) _actors[i]?.CancelMove();
    }

    private void OnDisable()
    {
        UnsubscribeGlobal();
        NotifyLeaderDisabled();
    }

    private void OnDestroy()
    {
        UnsubscribeGlobal();
        CancelAllCommands();
        if (_visualRoot != null) DestroyOwned(_visualRoot);
        _visualRoot = null;
    }

    private void Report(string error) { if (!string.IsNullOrEmpty(error)) Debug.LogWarning("[OverworldPartyFollowers] " + error, this); }
    private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    private static void DestroyOwned(UnityEngine.Object target)
    {
        if (target == null) return;
        if (Application.isPlaying) UnityEngine.Object.Destroy(target);
        else UnityEngine.Object.DestroyImmediate(target);
    }
}
