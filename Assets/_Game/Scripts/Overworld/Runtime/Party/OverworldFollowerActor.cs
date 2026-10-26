using System;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

public enum OverworldFollowerMode { Follow, Hold, Detached }

/// <summary>한 동료의 연출 이동 수명. 취소는 이 명령의 Tween만 정리합니다.</summary>
public sealed class OverworldFollowerMoveCommand
{
    private Action _cancel;
    public bool IsDone { get; private set; }
    public bool WasCompleted { get; private set; }
    public bool IsCancelled { get; private set; }
    public string Error { get; private set; } = string.Empty;

    internal void Bind(Action cancel) => _cancel = cancel;
    public void Cancel()
    {
        if (!IsDone) _cancel?.Invoke();
    }

    internal void Finish(bool completed, bool cancelled, string error = null)
    {
        if (IsDone) return;
        IsDone = true;
        WasCompleted = completed;
        IsCancelled = cancelled;
        Error = error ?? string.Empty;
        _cancel = null;
    }
}

/// <summary>입력·충돌·전투 참여가 없는 저장 파티 한 명의 표시입니다.</summary>
[DisallowMultipleComponent]
public sealed class OverworldFollowerActor : MonoBehaviour
{
    private static readonly int MoveX = Animator.StringToHash("MoveX");
    private static readonly int MoveY = Animator.StringToHash("MoveY");
    private static readonly int IsMoving = Animator.StringToHash("IsMoving");
    private readonly List<SpriteRenderer> _renderers = new List<SpriteRenderer>();
    private readonly List<bool> _rendererDefaults = new List<bool>();
    private readonly List<AnimatorBinding> _animators = new List<AnimatorBinding>();
    private OverworldPartyFollowerController _owner;
    private Tween _moveTween;
    private OverworldFollowerMoveCommand _command;
    private uint _commandGeneration;
    private bool _battleHidden;
    private bool _ownerHidden;
    private bool _hasCursor;
    private bool _followInProgress;
    private Vector3 _lastMovePosition;

    public int Slot { get; internal set; }
    public CharacterSaveData Member { get; private set; }
    public OverworldFollowerMode Mode { get; private set; } = OverworldFollowerMode.Follow;
    public bool Visible { get; private set; } = true;
    public int FacingDirection { get; private set; }
    public float TrailDistance { get; private set; }
    public uint TrailEpoch { get; private set; }
    public bool HasTrailCursor => _hasCursor;
    public bool IsScriptedMoving => _command != null && !_command.IsDone;

    internal bool Initialize(OverworldPartyFollowerController owner, CharacterSaveData member,
        int slot, CharacterData data, PlayerController leader, out string error)
    {
        _owner = owner;
        Member = member;
        Slot = slot;
        GameObject source = data != null ? data.OverworldVisualPrefab : null;
        bool explicitSource = source != null;
        if (source == null && data != null) source = data.BattlePrefab;
        if (source == null) source = leader != null ? leader.gameObject : null;
        if (source == null)
        {
            error = "동료 표시 원본과 리더가 없습니다.";
            return false;
        }

        var declaration = explicitSource ? source.GetComponent<OverworldFollowerVisualSource>() : null;
        Transform visualRoot = declaration != null ? declaration.VisualRoot : source.transform;
        if (declaration != null)
        {
            if (!ValidateVisualTree(visualRoot, out error)) return false;
            CopyVisualTree(visualRoot, transform, true);
        }
        else
        {
            SpriteRenderer sprite = source.GetComponent<SpriteRenderer>();
            if (sprite == null && !explicitSource && leader != null)
            {
                source = leader.gameObject;
                sprite = source.GetComponent<SpriteRenderer>();
            }
            if (sprite == null)
            {
                error = "표시 원본 루트의 SpriteRenderer 또는 중첩 표시 계약이 필요합니다.";
                return false;
            }
            if (!ValidateAnimator(source.GetComponent<Animator>(), false, out error)) return false;
            transform.localScale = source.transform.localScale;
            CopyRenderer(sprite, gameObject);
            CopyAnimator(source.GetComponent<Animator>(), gameObject);
        }

        Color tint = data != null && data.BattleSpriteTint.a > 0f ? data.BattleSpriteTint : Color.white;
        for (int i = 0; i < _renderers.Count; i++) _renderers[i].color *= tint;
        error = string.Empty;
        return _renderers.Count > 0;
    }

    private static bool ValidateVisualTree(Transform root, out string error)
    {
        if (root == null) { error = "표시 계층 루트가 없습니다."; return false; }
        var components = root.GetComponentsInChildren<Component>(true);
        bool hasSprite = false;
        for (int i = 0; i < components.Length; i++)
        {
            Component component = components[i];
            if (component is SpriteRenderer) { hasSprite = true; continue; }
            if (component is Transform || component is OverworldFollowerVisualSource) continue;
            if (component is Animator animator)
            {
                if (!ValidateAnimator(animator, true, out error)) return false;
                continue;
            }
            error = "동료 표시 계층에서 허용하지 않는 컴포넌트입니다: "
                + (component != null ? component.GetType().Name : "Missing Script");
            return false;
        }
        error = hasSprite ? string.Empty : "표시 계층에 SpriteRenderer가 없습니다.";
        return hasSprite;
    }

    private static bool ValidateAnimator(Animator animator, bool rejectEvents, out string error)
    {
        error = string.Empty;
        if (animator == null || animator.runtimeAnimatorController == null) return true;
        if (animator.applyRootMotion)
        { error = "동료 표시 Animator의 루트 모션은 지원하지 않습니다."; return false; }
        if (animator.GetBehaviours<StateMachineBehaviour>().Length > 0)
        { error = "동료 표시 Animator의 StateMachineBehaviour는 지원하지 않습니다."; return false; }
#if UNITY_EDITOR
        // 원본 프리팹의 Animator가 아직 평가되지 않았어도 편집기에서는 자산을 직접 검사합니다.
        RuntimeAnimatorController controller = animator.runtimeAnimatorController;
        while (controller is AnimatorOverrideController overrides) controller = overrides.runtimeAnimatorController;
        if (controller is UnityEditor.Animations.AnimatorController authored
            && authored.GetBehaviours<StateMachineBehaviour>().Length > 0)
        { error = "동료 표시 Animator의 StateMachineBehaviour는 지원하지 않습니다."; return false; }
#endif
        if (!rejectEvents) return true; // 루트 fallback 이벤트는 복사 Animator.fireEvents=false로 차단합니다.
        var clips = animator.runtimeAnimatorController.animationClips;
        for (int i = 0; i < clips.Length; i++)
            if (clips[i] != null && clips[i].events.Length > 0)
            { error = "중첩 표시 전용 Animator에는 Animation Event를 둘 수 없습니다."; return false; }
        return true;
    }

    private void CopyVisualTree(Transform source, Transform parent, bool root)
    {
        GameObject target = new GameObject(source.name);
        target.layer = source.gameObject.layer;
        target.transform.SetParent(parent, false);
        target.transform.localPosition = root ? Vector3.zero : source.localPosition;
        target.transform.localRotation = source.localRotation;
        target.transform.localScale = source.localScale;
        CopyRenderer(source.GetComponent<SpriteRenderer>(), target);
        CopyAnimator(source.GetComponent<Animator>(), target);
        for (int i = 0; i < source.childCount; i++) CopyVisualTree(source.GetChild(i), target.transform, false);
        target.SetActive(source.gameObject.activeSelf);
    }

    private void CopyRenderer(SpriteRenderer source, GameObject target)
    {
        if (source == null) return;
        var copy = target.AddComponent<SpriteRenderer>();
        copy.sprite = source.sprite;
        copy.sharedMaterials = source.sharedMaterials;
        copy.color = source.color;
        copy.flipX = source.flipX;
        copy.flipY = source.flipY;
        copy.drawMode = source.drawMode;
        copy.size = source.size;
        copy.maskInteraction = source.maskInteraction;
        copy.sortingLayerID = source.sortingLayerID;
        copy.sortingOrder = 3;
        copy.spriteSortPoint = SpriteSortPoint.Pivot;
        copy.enabled = source.enabled;
        target.layer = source.gameObject.layer;
        _renderers.Add(copy);
        _rendererDefaults.Add(source.enabled);
    }

    private void CopyAnimator(Animator source, GameObject target)
    {
        if (source == null || source.runtimeAnimatorController == null) return;
        var copy = target.AddComponent<Animator>();
        copy.applyRootMotion = false;
        copy.fireEvents = false;
        copy.runtimeAnimatorController = source.runtimeAnimatorController;
        copy.updateMode = source.updateMode;
        copy.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        var binding = new AnimatorBinding { Animator = copy };
        AnimatorControllerParameter[] parameters = source.parameters;
        for (int i = 0; i < parameters.Length; i++)
        {
            var parameter = parameters[i];
            if (parameter.nameHash == MoveX && parameter.type == AnimatorControllerParameterType.Float) binding.MoveX = true;
            if (parameter.nameHash == MoveY && parameter.type == AnimatorControllerParameterType.Float) binding.MoveY = true;
            if (parameter.nameHash == IsMoving && parameter.type == AnimatorControllerParameterType.Bool) binding.IsMoving = true;
        }
        _animators.Add(binding);
    }

    internal void ResetPose(OverworldFollowerTrail trail, Vector3 position, int facing)
    {
        CancelMove();
        Mode = OverworldFollowerMode.Follow;
        Visible = true;
        _ownerHidden = false;
        transform.position = position;
        TrailDistance = trail.HeadDistance;
        TrailEpoch = trail.Epoch;
        _hasCursor = true;
        SetMotion(facing, false);
        ApplyVisibility();
    }

    internal void SetBattleHidden(bool hidden)
    {
        _battleHidden = hidden;
        if (hidden) { CancelMove(); SetMotion(FacingDirection, false); }
        ApplyVisibility();
    }

    internal void SetOwnerHidden(bool hidden)
    {
        _ownerHidden = hidden;
        if (hidden) { CancelMove(); SetMotion(FacingDirection, false); }
        ApplyVisibility();
    }

    internal void SetVisible(bool visible)
    {
        Visible = visible;
        ApplyVisibility();
    }

    private void ApplyVisibility()
    {
        for (int i = 0; i < _renderers.Count; i++)
            if (_renderers[i] != null)
                _renderers[i].enabled = Visible && !_battleHidden && !_ownerHidden && _rendererDefaults[i];
    }

    internal bool TryResolveFollow(OverworldFollowerTrail trail, float tolerance, out float distance, out string error)
    {
        distance = TrailDistance;
        if (IsScriptedMoving) { error = "동료 연출 이동이 진행 중입니다."; return false; }
        if (_hasCursor)
        {
            if (TrailEpoch != trail.Epoch || !trail.TrySample(TrailDistance, TrailEpoch, out OverworldFollowerPose pose))
            { error = "동료의 이전 경로 기록이 만료되었습니다. 경유점 복귀 또는 명시적 재집결이 필요합니다."; return false; }
            if ((pose.Position - transform.position).sqrMagnitude > tolerance * tolerance)
            { error = "동료가 보관 경로 밖에 있습니다. 경유점으로 먼저 복귀하세요."; return false; }
            error = string.Empty;
            return true;
        }
        if (!trail.TryFindDistance(transform.position, tolerance, out distance))
        { error = "동료가 보관 경로 밖에 있습니다. 경유점으로 먼저 복귀하세요."; return false; }
        error = string.Empty;
        return true;
    }

    internal void CommitMode(OverworldFollowerMode mode, OverworldFollowerTrail trail, float followDistance)
    {
        CancelMove();
        Mode = mode;
        if (mode == OverworldFollowerMode.Follow)
        {
            TrailDistance = followDistance;
            TrailEpoch = trail.Epoch;
            _hasCursor = true;
            // 명시 재합류는 자연 정지 뒤 재출발이 아닙니다. 작은 잔여 경로도 마저 소진합니다.
            _followInProgress = true;
        }
        SetMotion(FacingDirection, false);
    }

    internal void TickFollow(OverworldFollowerTrail trail, float targetDistance, float speedBudget, float tolerance)
        => TickFollowStaggered(trail, targetDistance, speedBudget, tolerance, 0f);

    internal void TickFollowStaggered(OverworldFollowerTrail trail, float targetDistance,
        float speedBudget, float tolerance, float restartDistance)
    {
        if (Mode != OverworldFollowerMode.Follow || IsScriptedMoving) return;
        if (!TryResolveFollow(trail, tolerance, out float current, out _))
        { Mode = OverworldFollowerMode.Hold; _followInProgress = false; SetMotion(FacingDirection, false); return; }
        if (!_followInProgress && targetDistance - current < restartDistance)
        { SetMotion(FacingDirection, false); return; }
        float budget = Mathf.Max(0f, speedBudget);
        float next = Mathf.Max(current, Mathf.Min(targetDistance, current + budget));
        // 속도×dt를 누적한 커서와 실제 리더 경로의 float 오차가 쌓이면
        // 정수 픽셀 간격도 1픽셀씩 흔들립니다. 이동 중 수치 오차만 닫고,
        // 정지/대기에서는 커서를 전진시키거나 월드 좌표를 보간하지 않습니다.
        // 긴 탐색의 float 정밀도도 반영하되 원본 1픽셀보다 큰 보정은 허용하지 않습니다.
        float roundingTolerance = Mathf.Min(1f / GameConfigPolicy.WorldPixelsPerUnit,
            Mathf.Max(0.0001f, Mathf.Abs(targetDistance) * 0.00000024f));
        if (budget > 0f && targetDistance >= current && targetDistance - next <= roundingTolerance)
            next = targetDistance;
        if (!trail.TrySample(next, TrailEpoch, out OverworldFollowerPose pose))
        { Mode = OverworldFollowerMode.Hold; _followInProgress = false; SetMotion(FacingDirection, false); return; }
        bool moving = next - current > 0.00001f;
        transform.position = pose.Position;
        TrailDistance = next;
        // 일시 정지/0 delta는 기존 추종 의도를 지우지 않습니다. 실제 목표에서
        // 멈춘 프레임만 다음 출발을 기다리게 하여 메뉴/대화 복귀가 정체되지 않게 합니다.
        if (budget > 0f) _followInProgress = moving;
        SetMotion(moving ? pose.FacingDirection : FacingDirection, moving);
    }

    internal void StopFollowingAnimation()
    {
        if (!IsScriptedMoving) SetMotion(FacingDirection, false);
    }

    internal bool TryBeginMove(IReadOnlyList<Vector3> waypoints, float duration, bool rejoin,
        out OverworldFollowerMoveCommand command, out string error)
    {
        command = null;
        if (waypoints == null || waypoints.Count == 0 || !Finite(duration) || duration < 0f)
        { error = "이동 경유점과 유한한 0 이상 이동 시간이 필요합니다."; return false; }
        float total = 0f;
        Vector3 previous = transform.position;
        for (int i = 0; i < waypoints.Count; i++)
        {
            Vector3 point = waypoints[i];
            if (!Finite(point.x) || !Finite(point.y) || !Finite(point.z))
            { error = "이동 경유점은 유한한 월드 좌표여야 합니다."; return false; }
            total += Vector3.Distance(previous, point);
            previous = point;
        }
        if (!Finite(total)) { error = "이동 경유점 거리가 너무 큽니다."; return false; }

        CancelMove();
        Mode = OverworldFollowerMode.Hold;
        _hasCursor = false;
        var moveCommand = new OverworldFollowerMoveCommand();
        command = moveCommand;
        _command = moveCommand;
        uint generation = ++_commandGeneration;
        moveCommand.Bind(() => CancelOwnedMove(generation, moveCommand));
        _lastMovePosition = transform.position;

        if (duration <= 0f || total <= 0.00001f)
        {
            transform.position = waypoints[waypoints.Count - 1];
            UpdateScriptedMotion();
            FinishMove(generation, moveCommand, rejoin);
            error = string.Empty;
            return true;
        }

        Sequence sequence = DOTween.Sequence();
        sequence.SetRecyclable(false);
        sequence.Pause();
        previous = transform.position;
        for (int i = 0; i < waypoints.Count; i++)
        {
            Vector3 point = waypoints[i];
            float length = Vector3.Distance(previous, point);
            if (length > 0.00001f)
                sequence.Append(transform.DOMove(point, duration * (length / total)).SetEase(Ease.Linear));
            previous = point;
        }
        _moveTween = sequence;
        sequence.OnUpdate(() =>
        {
            if (IsCurrentMove(generation, moveCommand)) UpdateScriptedMotion();
        });
        sequence.OnComplete(() => FinishMove(generation, moveCommand, rejoin));
        sequence.OnKill(() =>
        {
            if (!IsCurrentMove(generation, moveCommand)) return;
            ++_commandGeneration;
            _moveTween = null;
            _command = null;
            moveCommand.Finish(false, true);
            SetMotion(FacingDirection, false);
        });
        sequence.Play();
        error = string.Empty;
        return true;
    }

    private void FinishMove(uint generation, OverworldFollowerMoveCommand command, bool rejoin)
    {
        if (!IsCurrentMove(generation, command)) return;
        _moveTween = null;
        _command = null;
        SetMotion(FacingDirection, false);
        string error = string.Empty;
        if (rejoin && (_owner == null || !_owner.TryResumeActor(this, out error)))
        {
            Mode = OverworldFollowerMode.Hold;
            command.Finish(false, false, _owner == null ? "동료 제어자가 종료되었습니다." : error);
            return;
        }
        command.Finish(true, false);
    }

    private void UpdateScriptedMotion()
    {
        Vector3 delta = transform.position - _lastMovePosition;
        _lastMovePosition = transform.position;
        bool moving = delta.sqrMagnitude > 0.00000001f;
        int facing = FacingDirection;
        if (moving)
            facing = Mathf.Abs(delta.x) >= Mathf.Abs(delta.y)
                ? (delta.x >= 0f ? 3 : 2) : (delta.y >= 0f ? 1 : 0);
        SetMotion(facing, moving);
    }

    private bool IsCurrentMove(uint generation, OverworldFollowerMoveCommand command)
        => this != null && generation == _commandGeneration && ReferenceEquals(_command, command) && !command.IsDone;

    private void CancelOwnedMove(uint generation, OverworldFollowerMoveCommand command)
    {
        if (IsCurrentMove(generation, command)) CancelMove();
        else command.Finish(false, true);
    }

    internal void CancelMove()
    {
        _followInProgress = false;
        ++_commandGeneration;
        Tween tween = _moveTween;
        _moveTween = null;
        var command = _command;
        _command = null;
        if (tween != null && tween.IsActive()) tween.Kill(false);
        command?.Finish(false, true);
        SetMotion(FacingDirection, false);
    }

    private void SetMotion(int facing, bool moving)
    {
        FacingDirection = facing >= 0 && facing <= 3 ? facing : 0;
        float x = FacingDirection == 2 ? -1f : FacingDirection == 3 ? 1f : 0f;
        float y = FacingDirection == 0 ? -1f : FacingDirection == 1 ? 1f : 0f;
        for (int i = 0; i < _animators.Count; i++)
        {
            AnimatorBinding binding = _animators[i];
            if (binding.Animator == null) continue;
            if (binding.MoveX) binding.Animator.SetFloat(MoveX, x);
            if (binding.MoveY) binding.Animator.SetFloat(MoveY, y);
            if (binding.IsMoving) binding.Animator.SetBool(IsMoving, moving);
        }
    }

    private void OnDisable() => CancelMove();
    private void OnDestroy() => CancelMove();
    private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    private struct AnimatorBinding
    {
        public Animator Animator;
        public bool MoveX, MoveY, IsMoving;
    }
}
