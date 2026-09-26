using System.Collections;
using UnityEngine;
using DG.Tweening;
using Sirenix.OdinInspector;

/// <summary>
/// 오버월드 플레이어 이동 및 입력 처리 컨트롤러.
/// 픽셀 게임 스타일: 즉각 반응 이동 (가속/감속 없음).
/// 반대 방향 동시 입력 시 마지막으로 누른 방향 우선 처리.
/// GameStateManager를 통해 이벤트/대화 중 이동을 완벽하게 통제합니다.
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Animator))]
public class PlayerController : MonoBehaviour, ITimedGuardInputSource
{
    // ── 플레이어 상태 ─────────────────────────────────────────
    public enum PlayerState { Idle, Moving, Interacting, InMenu, InBattle }
    public PlayerState State { get; private set; } = PlayerState.Idle;

    // ── 이동 설정 ─────────────────────────────────────────────
    [Header("Movement")]
    [SerializeField] private float _moveSpeed = 5f;

    // ── 액션 쿨타임 ───────────────────────────────────────────
    [Header("Action Settings")]
    [SerializeField] private float _actionCooldown = 0.4f;
    [SerializeField] private float _attackRange = 1.5f;
    [SerializeField, Min(0f)] private float _attackWidth = 1f;
    [SerializeField] private float _attackDelay = 0.25f;
    [SerializeField] private float _attackRecoverDelay = 0.35f;
    [SerializeField] private LayerMask _enemyLayerMask = ~0;
    [SerializeField] private string _attackTriggerName = "Attack";
    private float _lastActionTime = -999f;

    // ── VFX / DOTween 연출 설정 ───────────────────────────────
    [Header("VFX Settings")]
    [SerializeField] private float _parryFlashDuration = 0.08f;
    [SerializeField] private Color _parryFlashColor    = Color.cyan;
    [SerializeField] private float _hurtFlashDuration  = 0.05f;
    [SerializeField] private float _hurtShakeDuration  = 0.3f;
    [SerializeField] private float _hurtShakeStrength  = 0.15f;
    [SerializeField] private Color _hurtFlashColor     = Color.white;
    [SerializeField, Min(0f)] private float _hurtPopHeight = 0.35f;
    [SerializeField, Min(0.01f)] private float _hurtPopUpDuration = 0.08f;
    [SerializeField, Min(0.01f)] private float _hurtPopReturnDuration = 0.16f;
    [SerializeField] private float _dieFlashDuration   = 0.12f;
    [SerializeField] private Color _dieFlashColor      = Color.white;
    [SerializeField, Min(0.1f), LabelText("전투 회피 후퇴 거리"), Tooltip("전투 회피 시 월드 왼쪽으로 물러나는 거리. 1.5 = 32 PPU에서 48픽셀.")]
    private float _battleDodgeDistance = 1.5f;
    // ── 컴포넌트 캐싱 ─────────────────────────────────────────
    private Rigidbody2D    _rb;
    private Animator       _anim;
    private PlayerCharacter _battleCharacter;
    private uint _battleAnimationVersion;
    private CharacterVFX   _vfx;
    private SpriteRenderer _spriteRenderer;
    private Collider2D[] _colliders;
    private Vector3        _originalLocalPos;
    private Vector3        _originalLocalScale;
    private Vector3 _battleDefenseAnchorPosition;
    private int _baseSortingOrder;
    private bool _hasSortingBase;
    private Tween _defenseVisualTween;
    private int _battleSortingBoost;
    private DefenseInput _bufferedDefenseInput = DefenseInput.None;
    private float _bufferedDefenseInputTime = -999f;
    private const float DefenseInputBufferWindow = 1.25f;
    private DefenseInput _lastPreviewedDefenseInput = DefenseInput.None;
    private DefensePresentationGate _defensePresentationGate;
    private float _lastPreviewedDefenseInputTime = float.NegativeInfinity;
    private const float DefensePreviewDuplicateWindow = 0.05f;
    private Vector3 _hurtReactionOrigin;
    private bool _hurtReactionActive;
    private bool _preemptiveAttackInProgress;
    private bool _preemptiveAttackHitResolved;
    private bool _preemptiveAttackStartedEncounter;
    private readonly Collider2D[] _preemptiveAttackHits = new Collider2D[12];
    private ContactFilter2D _preemptiveAttackContactFilter;
    private IScreenFlashScaleProvider _screenFlashScaleProvider =
        new GameConfigScreenFlashScaleProvider();
    private IScreenShakeScaleProvider _screenShakeScaleProvider =
        new GameConfigScreenShakeScaleProvider();

    private Animator Animator
    {
        get
        {
            if (_anim == null) _anim = GetComponent<Animator>();
            return _anim;
        }
    }

    // ── 방향 (0=Down 1=Up 2=Left 3=Right) ────────────────────
    public int FacingDirection { get; private set; } = 0;

    // ── 반대 방향 동시 입력 처리용 (Last-Input Priority) ─────
    private bool _keyLeft, _keyRight, _keyUp, _keyDown;
    private bool _prevLeft, _prevRight, _prevUp, _prevDown;
    private int  _lastHorizontal;
    private int  _lastVertical;
    private Vector2 _moveInput;

    // ── Animator 파라미터 해시 ────────────────────────────────
    private static readonly int HashMoveX    = Animator.StringToHash("MoveX");
    private static readonly int HashMoveY    = Animator.StringToHash("MoveY");
    private static readonly int HashIsMoving = Animator.StringToHash("IsMoving");

    public static readonly int HashBattleIdle = Animator.StringToHash("BattleIdle");
    public static readonly int HashBattleMove = Animator.StringToHash("BattleMove");
    public static readonly int HashParry      = Animator.StringToHash("Parry");
    public static readonly int HashAttack     = Animator.StringToHash("Attack");
    public static readonly int HashHurt       = Animator.StringToHash("Hurt");
    public static readonly int HashDie        = Animator.StringToHash("Die");
    public static readonly int HashVictory    = Animator.StringToHash("Victory");
    private static readonly int HashParryState = Animator.StringToHash("parry");
    private static readonly int HashHurtState  = Animator.StringToHash("hurt");

    // ─────────────────────────────────────────────────────────
    private void Awake()
    {
        _rb             = GetComponent<Rigidbody2D>();
        _anim           = GetComponent<Animator>();
        _battleCharacter = GetComponent<PlayerCharacter>();
        _spriteRenderer = GetComponent<SpriteRenderer>();
        _vfx            = GetComponent<CharacterVFX>();
        _colliders      = GetComponents<Collider2D>();

        _preemptiveAttackContactFilter = new ContactFilter2D
        {
            useLayerMask = true,
            layerMask = _enemyLayerMask,
            useTriggers = Physics2D.queriesHitTriggers
        };

        if (_spriteRenderer != null)
        {
            _baseSortingOrder = _spriteRenderer.sortingOrder;
            _hasSortingBase = true;
        }

        _originalLocalPos = transform.localPosition;
        _originalLocalScale = transform.localScale;
    }

    private void Start()
    {
        LoadPositionFromGlobal();
        UpdateAnimator(false);

    }

    private void Update()
    {
        // 전투 중에는 GameState가 Cutscene으로 잠겨 있어도 Z/X/C 방어 입력은
        // 먼저 읽어야 합니다. 이동/상호작용은 아래와 같이 완전히 차단합니다.
        if (State == PlayerState.InBattle)
        {
            HandleBattleDefenseInput();
            return;
        }

        // 대화 중이거나 UI가 열려있을 때 오버월드 입력을 완전히 차단합니다.
        if (GameStateManager.Instance != null && !GameStateManager.Instance.CanPlayerMove)
        {
            _moveInput = Vector2.zero;
            UpdateAnimator(false);
            return;
        }

        ReadInput();
        UpdateFacingDirection();
        UpdateAnimator(_moveInput.sqrMagnitude > 0.01f);

        // 상호작용 (캐싱된 타겟을 InteractionSystem을 통해 즉시 실행)
        if (GameInput.ConfirmPressed)
            InteractionSystem.Instance?.TryInteract(this);

        if (GameInput.PreemptiveAttackPressed)
            TryStartPreemptiveAttack();

        // 오버월드 옵션(Config) 호출
        if (GameInput.MenuPressed)
            UIManager.Instance.OpenPanel(UIPanelId.Overworld);
    }

    private void LateUpdate()
    {
        UpdateSortingOrder();
    }

    private void OnDisable()
    {
        ActiveDefensePresentation?.Dispose();
        _defensePresentationGate.Close();
        _bufferedDefenseInput = DefenseInput.None;
        _bufferedDefenseInputTime = -999f;
        _lastPreviewedDefenseInput = DefenseInput.None;
        _lastPreviewedDefenseInputTime = float.NegativeInfinity;
        KillDefenseVisualTween();
        DOTween.Kill(transform);
        if (_spriteRenderer != null)
            _spriteRenderer.DOKill();
        _hurtReactionActive = false;
    }

    private void HandleBattleDefenseInput()
    {
        if (BattleManager.Instance == null)
            return;

        // 플레이어 스킬의 실시간 QTE는 QTEManager가 직접 Z/X/C를 소비합니다.
        // 이때 전투 방어 버퍼까지 채우면 같은 입력이 회피/패링 미리보기와
        // 스킬 노드에 동시에 적용되므로, 적 방어 입력 수집을 잠시 양보합니다.
        if (QTEManager.Instance != null && QTEManager.Instance.IsSkillQteActive)
            return;

        if (!BattleManager.Instance.CanAcceptRealtimeDefenseInput)
            return;

        // 적의 접근·전조·공격·정리 구간 전체에서 입력을 받아 둡니다.
        // 실제 성공/실패는 QTEManager가 충돌 시각에만 판정하므로, 이 버퍼는
        // 조작 유실을 막을 뿐 판정 시간을 앞당기지 않습니다.
        // 적 공격 대응에서는 C를 점프가 아니라 연계 반격으로 보존합니다.
        // 플레이어 스킬 QTE는 위에서 분리했으므로 이 경로는 방어 전용입니다.
        if (GameInput.ReadActiveDefenseInputThisFrame(out DefenseInput input)
            == DefenseInputReadStatus.Valid)
            AttemptDefenseInput(input);
    }

    private void AttemptDefenseInput(DefenseInput input)
    {
        float now = GameInput.GetDefensePressTime(input);
        _bufferedDefenseInput = input;
        _bufferedDefenseInputTime = now;
        PreviewDefenseInputInternal(input, now);
    }

    public bool TryConsumeBufferedDefenseInput(out DefenseInput input)
    {
        return TryConsumeBufferedDefenseInput(out input, out _);
    }

    public bool TryConsumeBufferedDefenseInput(out DefenseInput input, out float inputTime)
    {
        input = DefenseInput.None;
        inputTime = -999f;

        if (_bufferedDefenseInput == DefenseInput.None)
            return false;

        if (Time.realtimeSinceStartup > _bufferedDefenseInputTime + DefenseInputBufferWindow)
        {
            _bufferedDefenseInput = DefenseInput.None;
            _bufferedDefenseInputTime = -999f;
            return false;
        }

        input = _bufferedDefenseInput;
        inputTime = _bufferedDefenseInputTime;
        _bufferedDefenseInput = DefenseInput.None;
        _bufferedDefenseInputTime = -999f;
        return true;
    }

    public void PreviewDefenseInput(DefenseInput input)
    {
        PreviewDefenseInputInternal(input, Time.realtimeSinceStartup);
    }

    public void CommitDefenseAttempt(DefenseInput input, bool acceptedParryFeedback = false)
    {
        // Update 순서와 관계없이 한 번만 미리보기. 버퍼는 이후에도 수집하지만 모션은 잠급니다.
        bool hurtBusy = _hurtReactionActive || (_battleCharacter != null
            && (!_battleCharacter.IsAlive || _battleCharacter.IsHitReactionActive));
        // 조기 Z 대기 중 유효한 X를 누른 경우에는 확정된 X가 한 번만 모션을 인계받습니다.
        if (_lastPreviewedDefenseInput != input && _defensePresentationGate.CanPreview(hurtBusy))
            ResetDefenseVisualStateOnly();
        PreviewDefenseInput(input);
        if (acceptedParryFeedback && input == DefenseInput.Parry && !hurtBusy)
            PlayAcceptedParryFeedback();
        _defensePresentationGate.Commit();
    }

    private void PlayAcceptedParryFeedback()
    {
        // 성공 입력 수락만 즉시 표시합니다. 패링 모션/VFX/보상은 실제 충돌에서 실행합니다.
        KillDefenseVisualTween();
        SpriteRenderer sprite = _spriteRenderer;
        if (sprite == null) return;
        Color baseline = sprite.color;
        sprite.color = Color.Lerp(baseline, ResolveFlashColor(_parryFlashColor), 0.45f);
        Tween feedback = DOTween.To(() => sprite != null ? sprite.color : baseline,
                color => { if (sprite != null) sprite.color = color; }, baseline, 0.12f)
            .SetEase(Ease.OutQuad).SetUpdate(true).SetRecyclable(false)
            .SetLink(gameObject, LinkBehaviour.KillOnDisable);
        _defenseVisualTween = feedback;
        feedback.OnKill(() =>
        {
            if (sprite != null) sprite.color = baseline;
            if (ReferenceEquals(_defenseVisualTween, feedback)) _defenseVisualTween = null;
        });
    }

    private void PreviewDefenseInputInternal(DefenseInput input, float timestamp)
    {
        if (input == DefenseInput.None
            || !_defensePresentationGate.CanPreview(HasActiveDefenseVisualTween() || _hurtReactionActive
                || (_battleCharacter != null && (!_battleCharacter.IsAlive || _battleCharacter.IsHitReactionActive))))
            return;

        // PlayerController.Update와 QTEManager가 같은 프레임의 입력을 모두
        // 관찰할 수 있습니다. 동일 입력의 미리보기는 한 번만 실행해 회피/점프
        // 트윈이 재시작되거나 패링 준비 모션이 깜빡이지 않도록 합니다.
        if (_lastPreviewedDefenseInput == input
            && Mathf.Abs(timestamp - _lastPreviewedDefenseInputTime) <= DefensePreviewDuplicateWindow)
        {
            return;
        }

        _lastPreviewedDefenseInput = input;
        _lastPreviewedDefenseInputTime = timestamp;
        switch (input)
        {
            case DefenseInput.Parry:
                ExecuteParry(true);
                break;
            case DefenseInput.Dodge:
                ExecuteDodge(true);
                break;
            case DefenseInput.Jump:
                ExecuteJump(true);
                break;
            case DefenseInput.Counter:
                // 반격 대기에는 점프 이동을 사용하지 않습니다. 성공 공격은 전투 모듈이 소유합니다.
                ExecuteParry(true);
                break;
        }
    }

    private void FixedUpdate()
    {
        // 상태 잠금 시 물리 이동 즉시 정지 (미끄러짐 방지)
        if (GameStateManager.Instance != null && !GameStateManager.Instance.CanPlayerMove)
        {
            _rb.linearVelocity = Vector2.zero;
            return;
        }

        if (State == PlayerState.InBattle)
        {
            _rb.linearVelocity = Vector2.zero;
            return;
        }

        ApplyMovement();
    }

    // ── 입력 읽기 (Last-Input Priority) ──────────────────────
    private void ReadInput()
    {
        _keyLeft  = GameInput.MoveLeftHeld;
        _keyRight = GameInput.MoveRightHeld;
        _keyUp    = GameInput.MoveUpHeld;
        _keyDown  = GameInput.MoveDownHeld;

        // 수평 Last-Input Priority
        if (_keyLeft && _keyRight)
        {
            if (!_prevLeft  && _keyLeft)       _lastHorizontal = -1;
            else if (!_prevRight && _keyRight) _lastHorizontal =  1;
        }
        else if (_keyLeft)  _lastHorizontal = -1;
        else if (_keyRight) _lastHorizontal =  1;
        else                _lastHorizontal =  0;

        // 수직 Last-Input Priority
        if (_keyUp && _keyDown)
        {
            if (!_prevUp   && _keyUp)         _lastVertical =  1;
            else if (!_prevDown && _keyDown)  _lastVertical = -1;
        }
        else if (_keyUp)   _lastVertical =  1;
        else if (_keyDown) _lastVertical = -1;
        else               _lastVertical =  0;

        _prevLeft  = _keyLeft;
        _prevRight = _keyRight;
        _prevUp    = _keyUp;
        _prevDown  = _keyDown;

        _moveInput = new Vector2(_lastHorizontal, _lastVertical);
        if (_moveInput.sqrMagnitude > 1f)
            _moveInput = _moveInput.normalized;
    }

    // ── 이동 적용 ─────────────────────────────────────────────
    private void ApplyMovement()
    {
        _rb.linearVelocity = _moveInput * _moveSpeed;
        State = _moveInput.sqrMagnitude > 0.01f ? PlayerState.Moving : PlayerState.Idle;
    }

    // ── 방향 업데이트 ─────────────────────────────────────────
    private void UpdateFacingDirection()
    {
        if (_moveInput.sqrMagnitude < 0.01f) return;

        if (Mathf.Abs(_moveInput.x) >= Mathf.Abs(_moveInput.y))
            FacingDirection = _moveInput.x > 0 ? 3 : 2;
        else
            FacingDirection = _moveInput.y > 0 ? 1 : 0;
    }

    /// <summary>캐릭터가 현재 바라보는 방향을 Vector3로 반환합니다.</summary>
    private Vector3 GetFacingVector()
    {
        return FacingDirection switch
        {
            0 => Vector3.down,
            1 => Vector3.up,
            2 => Vector3.left,
            3 => Vector3.right,
            _ => Vector3.down
        };
    }

    public Vector2 GetFacingVector2()
    {
        return GetFacingVector();
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        PreemptiveAttackArea area = PreemptiveAttackGeometry.Create(
            transform.position,
            GetFacingVector2(),
            _attackRange,
            _attackWidth);
        Gizmos.color = new Color(1f, 0.45f, 0.1f, 0.9f);
        Gizmos.DrawWireCube(area.Center, area.Size);
    }
#endif

    public void NudgeFromEncounter(Vector2 direction, float distance)
    {
        if (direction.sqrMagnitude < 0.0001f || distance <= 0f) return;
        if (_rb == null) _rb = GetComponent<Rigidbody2D>();

        Vector2 offset = direction.normalized * distance;
        Vector3 targetPosition = transform.position + new Vector3(offset.x, offset.y, 0f);

        DOTween.Kill(transform);
        if (_rb != null)
        {
            DOTween.Kill(_rb);
            _rb.position = targetPosition;
            _rb.linearVelocity = Vector2.zero;
        }

        transform.position = targetPosition;
        _battleDefenseAnchorPosition = targetPosition;
        _moveInput = Vector2.zero;
        _prevLeft = _prevRight = _prevUp = _prevDown = false;
    }

    // ── 애니메이터 업데이트 ───────────────────────────────────
    private void UpdateAnimator(bool isMoving)
    {
        if (_anim == null) return;

        if (isMoving)
        {
            _anim.SetFloat(HashMoveX, _moveInput.x);
            _anim.SetFloat(HashMoveY, _moveInput.y);
        }
        _anim.SetBool(HashIsMoving, isMoving);
    }

    public void StopOverworldMovement()
    {
        if (_rb == null) _rb = GetComponent<Rigidbody2D>();

        _moveInput = Vector2.zero;
        _prevLeft = _prevRight = _prevUp = _prevDown = false;
        if (_rb != null) _rb.linearVelocity = Vector2.zero;
        UpdateAnimator(false);
        if (State != PlayerState.InBattle)
            State = PlayerState.Idle;
    }

    public bool TryStartPreemptiveAttack()
    {
        if (!isActiveAndEnabled) return false;
        if (_preemptiveAttackInProgress) return false;
        if (State == PlayerState.InBattle) return false;
        if (GameStateManager.Instance != null && !GameStateManager.Instance.CanPlayerMove) return false;
        if (!CanExecuteAction()) return false;

        StartCoroutine(CoPreemptiveAttack());
        return true;
    }

    private IEnumerator CoPreemptiveAttack()
    {
        _preemptiveAttackInProgress = true;
        _preemptiveAttackHitResolved = false;
        _preemptiveAttackStartedEncounter = false;

        GameState previousState = GameStateManager.Instance != null
            ? GameStateManager.Instance.CurrentState
            : GameState.Exploration;

        GameStateManager.Instance?.ChangeState(GameState.Cutscene);
        StopOverworldMovement();
        SyncOverworldAttackDirection();
        TryPlayAnimatorTrigger(_attackTriggerName);

        float fallbackDelay = Mathf.Max(0f, _attackDelay);
        float fallbackAt = Time.unscaledTime + fallbackDelay;
        while (!_preemptiveAttackHitResolved && Time.unscaledTime < fallbackAt)
            yield return null;

        ResolvePreemptiveAttackHit();

        if (_preemptiveAttackStartedEncounter)
            yield break;

        if (_attackRecoverDelay > 0f)
            yield return new WaitForSecondsRealtime(_attackRecoverDelay);

        RestoreAfterFailedPreemptiveAttack(previousState);
    }

    public void ResolvePreemptiveAttackHit()
    {
        if (!_preemptiveAttackInProgress || _preemptiveAttackHitResolved) return;

        _preemptiveAttackHitResolved = true;
        IPreemptiveAttackTarget target = FindPreemptiveAttackTarget();
        if (!IsPreemptiveAttackTargetAlive(target)) return;

        try
        {
            _preemptiveAttackStartedEncounter = target.TryStartPreemptiveAttack(this);
        }
        catch (System.Exception exception)
        {
            _preemptiveAttackStartedEncounter = false;
            Debug.LogException(exception, this);
        }
    }

    private IPreemptiveAttackTarget FindPreemptiveAttackTarget()
    {
        PreemptiveAttackArea area = PreemptiveAttackGeometry.Create(
            transform.position,
            GetFacingVector2(),
            _attackRange,
            _attackWidth);
        if (area.Size.x <= 0f || area.Size.y <= 0f) return null;

        int hitCount = Physics2D.OverlapBox(
            area.Center,
            area.Size,
            0f,
            _preemptiveAttackContactFilter,
            _preemptiveAttackHits);
        IPreemptiveAttackTarget bestTarget = null;
        float bestDistanceSqr = float.MaxValue;

        for (int i = 0; i < hitCount; i++)
        {
            Collider2D hit = _preemptiveAttackHits[i];
            _preemptiveAttackHits[i] = null;
            if (hit == null) continue;

            IPreemptiveAttackTarget candidate = ResolvePreemptiveAttackTarget(hit);
            if (candidate == null) continue;

            Component component = candidate as Component;
            if (component == null) continue;

            Vector2 toTarget = (Vector2)component.transform.position - (Vector2)transform.position;
            if (Vector2.Dot(toTarget, area.Facing) < 0f) continue;
            if (!candidate.CanStartPreemptiveAttack(this)) continue;

            float distanceSqr = toTarget.sqrMagnitude;
            if (distanceSqr >= bestDistanceSqr) continue;

            bestDistanceSqr = distanceSqr;
            bestTarget = candidate;
        }

        return bestTarget;
    }

    private static IPreemptiveAttackTarget ResolvePreemptiveAttackTarget(Collider2D hit)
    {
        if (hit == null) return null;

        MonoBehaviour[] behaviours = hit.GetComponentsInParent<MonoBehaviour>(true);
        for (int i = 0; i < behaviours.Length; i++)
        {
            if (behaviours[i] is IPreemptiveAttackTarget target)
                return target;
        }

        return null;
    }

    private static bool IsPreemptiveAttackTargetAlive(IPreemptiveAttackTarget target)
    {
        if (target == null) return false;
        if (target is Object unityObject) return unityObject != null;
        return true;
    }

    private void SyncOverworldAttackDirection()
    {
        if (_anim == null) return;

        Vector2 facing = GetFacingVector2();
        _anim.SetFloat(HashMoveX, facing.x);
        _anim.SetFloat(HashMoveY, facing.y);
        _anim.SetBool(HashIsMoving, false);
    }

    private void RestoreAfterFailedPreemptiveAttack(GameState previousState)
    {
        GameState restoreState = previousState == GameState.Paused
            ? GameState.Exploration
            : previousState;
        GameStateManager.Instance?.ChangeState(restoreState);
        ResetOverworldAttackAnimation();
        StopOverworldMovement();
        _preemptiveAttackInProgress = false;
        _preemptiveAttackHitResolved = false;
        _preemptiveAttackStartedEncounter = false;
    }

    private void ResetOverworldAttackAnimation()
    {
        if (_anim == null) return;

        int triggerHash = Animator.StringToHash(_attackTriggerName);
        if (HasAnimatorTrigger(triggerHash))
            _anim.ResetTrigger(triggerHash);

        _anim.SetBool(HashIsMoving, false);
        _anim.SetFloat(HashMoveX, GetFacingVector2().x);
        _anim.SetFloat(HashMoveY, GetFacingVector2().y);
        TryCrossFadeOverworldIdle();
    }

    public void CompletePreemptiveAttackWithoutBattle()
    {
        ResetOverworldAttackAnimation();
        StopOverworldMovement();
        _preemptiveAttackInProgress = false;
        _preemptiveAttackHitResolved = false;
        _preemptiveAttackStartedEncounter = false;
        if (State != PlayerState.InBattle)
            State = PlayerState.Idle;
        GameStateManager.Instance?.ChangeState(GameState.Exploration);
    }

    private void TryCrossFadeOverworldIdle()
    {
        if (_anim == null) return;

        const string stateName = "idle";

        int stateHash = Animator.StringToHash(stateName);
        if (_anim.HasState(0, stateHash))
            _anim.Play(stateHash, 0, 0f);
    }

    public void SetFacingDirection(int dir)
    {
        FacingDirection = dir;
        SyncOverworldAttackDirection();
    }

    // ── 전투 모드 전환 ────────────────────────────────────────
    /// <summary>전투 씬에서 이동/상호작용 입력을 완전히 잠급니다.</summary>
    public void SetBattleMode(bool active)
    {
        if (_rb == null) _rb = GetComponent<Rigidbody2D>();

        if (active)
        {
            // 이전 전투/오버월드에서 남은 입력이 새 전투의 첫 방어로
            // 재사용되지 않도록 전투 진입 시 버퍼를 초기화합니다.
            CloseDefenseInputWindow();
            _battleDefenseAnchorPosition = transform.position;
            _moveInput = Vector2.zero;
            _prevLeft = _prevRight = _prevUp = _prevDown = false;
            State = PlayerState.InBattle;
            _rb.bodyType = RigidbodyType2D.Kinematic;
            _rb.linearVelocity = Vector2.zero;
            if (_rb != null) _rb.position = transform.position;
            UpdateAnimator(false);
            if (_anim != null) _anim.SetTrigger(HashBattleIdle);
        }
        else
        {
            CloseDefenseInputWindow();
            ActiveDefensePresentation?.Dispose();
            _preemptiveAttackInProgress = false;
            _preemptiveAttackHitResolved = false;
            _preemptiveAttackStartedEncounter = false;
            _moveInput = Vector2.zero;
            _prevLeft = _prevRight = _prevUp = _prevDown = false;
            State = PlayerState.Idle;
            _rb.bodyType = RigidbodyType2D.Dynamic;
            _rb.linearVelocity = Vector2.zero;
            UpdateAnimator(false);
        }
    }

    // ── GlobalDataManager 연동 ────────────────────────────────
    public void SavePositionToGlobal()
    {
        if (GlobalDataManager.Instance == null) return;
        GlobalDataManager.Instance.SpawnX     = transform.position.x;
        GlobalDataManager.Instance.SpawnY     = transform.position.y;
        GlobalDataManager.Instance.LookingDir = FacingDirection;
    }

    public void LoadPositionFromGlobal()
    {
        GlobalDataManager global = GlobalDataManager.Instance
            ?? FindFirstObjectByType<GlobalDataManager>(FindObjectsInactive.Include);
        if (global == null) return;

        string spawnPointId = global.SpawnPointId;
        if (!string.IsNullOrWhiteSpace(spawnPointId)
            && SpawnPoint.TryFind(spawnPointId, out SpawnPoint spawnPoint)
            && spawnPoint != null)
        {
            transform.position = spawnPoint.transform.position;
        }
        else
        {
            transform.position = new Vector3(global.SpawnX, global.SpawnY, 0f);
        }

        FacingDirection = global.LookingDir;
        global.SpawnPointId = string.Empty;
        global.SpawnFallbackAllowed = false;
    }

    // ── 액션 쿨타임 체크 ──────────────────────────────────────
    private bool CanExecuteAction()
    {
        if (Time.time < _lastActionTime + _actionCooldown) return false;
        _lastActionTime = Time.time;
        return true;
    }
    // ── 전투 액션 실행 ────────────────────────────────────────
    /// <summary>전투 애니메이션 트리거 + 대응 이펙트 재생</summary>
    public void PlayBattleAnim(int triggerHash)
    {
        if (_anim == null) return;
        _battleAnimationVersion++;
        // 같은 Animator를 사용하는 두 컴포넌트의 요청을 한 경로로 모읍니다.
        if (_battleCharacter != null)
            _battleCharacter.PlayBattleAnim(triggerHash);
        else
            _anim.SetTrigger(triggerHash);

        if      (triggerHash == HashParry)   PlayParryEffect();
        else if (triggerHash == HashHurt)    PlayHurtEffect();
        else if (triggerHash == HashDie)     PlayDieEffect();
        else if (triggerHash == HashAttack)  PlayAttackEffect();
        else if (triggerHash == HashVictory) PlayVictoryEffect();
    }

    public bool TryPlayAnimatorTrigger(string triggerName)
    {
        if (_anim == null || string.IsNullOrWhiteSpace(triggerName)) return false;

        int triggerHash = Animator.StringToHash(triggerName);
        if (!HasAnimatorTrigger(triggerHash))
        {
            Debug.LogWarning($"[PlayerController] Animator Trigger '{triggerName}'가 없어 트리거 실행을 건너뜁니다.", this);
            return false;
        }

        _anim.SetTrigger(triggerHash);
        if (triggerHash == HashAttack)
            PlayAttackEffect();

        return true;
    }

    private bool HasAnimatorTrigger(int triggerHash)
    {
        if (_anim == null) return false;
        foreach (AnimatorControllerParameter parameter in _anim.parameters)
        {
            if (parameter.nameHash == triggerHash && parameter.type == AnimatorControllerParameterType.Trigger)
                return true;
        }

        return false;
    }

    public void ExecuteAttack()
    {
        PlayBattleAnim(HashAttack);
    }

    public void ExecuteParry(bool ignoreCooldown = false)
    {
        ResetDefenseVisualStateOnly();
        // 판정 전에는 패링 성공 모션을 재생하지 않습니다. 결과 확정 시에만 재생합니다.
        _defenseVisualTween = DOTween.Sequence().SetRecyclable(false).SetUpdate(true).AppendInterval(0.22f);
    }

    public void ExecuteDodge(bool ignoreCooldown = false)
    {
        ResetDefenseVisualStateOnly();
        PlayDodgeAttempt();
    }

    public void ExecuteJump(bool ignoreCooldown = false)
    {
        ResetDefenseVisualStateOnly();
        PlayJumpAttempt();
    }

    public void ConfirmDefenseSuccess(DefenseInput input)
    {
        // 회피/점프는 입력 직후 시작한 이동 연출을 끝까지 보여줍니다. 패링/반격만
        // 미리보기 트윈을 끊고 결과 모션으로 전환해, 성공 모션이 자리 복귀 뒤에
        // 늦게 재생되거나 같은 프레임에 잘리는 일을 막습니다.
        if (input != DefenseInput.Dodge && input != DefenseInput.Jump)
            KillDefenseVisualTween();

        switch (input)
        {
            case DefenseInput.Parry:
                PlayBattleAnim(HashParry);
                break;
            case DefenseInput.Dodge:
                _vfx?.Play(CharacterVFX.VFXAction.Dodge_Dust);
                break;
            case DefenseInput.Jump:
                _vfx?.Play(CharacterVFX.VFXAction.Jump_Dust);
                break;
            case DefenseInput.Counter:
                PlayParryEffect();
                break;
        }
    }

    public bool IsGuardHeld => GameInput.QTEZHeld;

    public void ConfirmGuardSuccess()
    {
        // 부분 방어도 TakeDamage가 재생한 Hurt를 덮어쓰지 않아야 합니다.
        KillDefenseVisualTween();
    }

    public IEnumerator WaitForDefenseVisualComplete(float fallbackSeconds = 0.45f)
    {
        float started = Time.unscaledTime;
        while (HasActiveDefenseVisualTween()
            && Time.unscaledTime < started + Mathf.Max(0.05f, fallbackSeconds))
            yield return null;
    }

    /// <summary>
    /// 방어 결과의 연출이 끝날 때까지 기다립니다. 입력 판정과 시각 연출의 생명주기를 분리해
    /// 다음 턴의 Idle 리셋이 Parry/BattleMove를 끊지 않도록 합니다.
    /// </summary>
    public IEnumerator WaitForDefenseReactionComplete(
        DefenseInput input,
        bool tookDamage,
        float fallbackSeconds = 1.25f)
    {
        float deadline = Time.unscaledTime + Mathf.Max(0.05f, fallbackSeconds);
        int stateHash = tookDamage
            ? HashHurtState
            : input == DefenseInput.Parry ? HashParryState : 0;
        bool canObserveAnimatorState = stateHash != 0
            && _anim != null
            && _anim.HasState(0, stateHash);
        bool animatorStateSeen = false;
        bool defenseTweenSeen = false;
        float observationGraceUntil = Time.unscaledTime + (canObserveAnimatorState ? 0.05f : 0f);
        PlayerCharacter playerCharacter = tookDamage ? GetComponent<PlayerCharacter>() : null;

        while (Time.unscaledTime < deadline)
        {
            if (this == null || !isActiveAndEnabled || State != PlayerState.InBattle)
                yield break;
            bool defenseTweenActive = HasActiveDefenseVisualTween();
            if (defenseTweenActive)
                defenseTweenSeen = true;

            bool hurtReactionActive = tookDamage && _hurtReactionActive;
            bool characterHitReactionActive = playerCharacter != null
                && playerCharacter.IsHitReactionActive;

            bool animatorStateActive = canObserveAnimatorState
                && IsAnimatorStateActive(stateHash, ref animatorStateSeen);
            bool reactionSeen = defenseTweenSeen || animatorStateSeen
                || hurtReactionActive || characterHitReactionActive;

            if (reactionSeen && !defenseTweenActive && !animatorStateActive
                && (!tookDamage || (!_hurtReactionActive && !characterHitReactionActive)))
                yield break;

            // Trigger가 아직 Animator 업데이트에 반영되지 않은 한 프레임은 관찰합니다.
            if (!reactionSeen && Time.unscaledTime >= observationGraceUntil)
                yield break;

            yield return null;
        }
    }

    private void ResetDefenseVisualStateOnly()
    {
        KillDefenseVisualTween();

        if (State == PlayerState.InBattle)
        {
            if (_rb != null) _rb.position = _battleDefenseAnchorPosition;
            transform.position = _battleDefenseAnchorPosition;
            transform.localScale = _originalLocalScale == Vector3.zero ? transform.localScale : _originalLocalScale;
            if (_rb != null) _rb.linearVelocity = Vector2.zero;
        }
    }

    private void KillDefenseVisualTween()
    {
        Tween ownedTween = _defenseVisualTween;
        _defenseVisualTween = null;
        ownedTween?.Kill();
    }

    private bool HasActiveDefenseVisualTween()
    {
        return _defenseVisualTween != null
            && _defenseVisualTween.IsActive()
            && !_defenseVisualTween.IsComplete();
    }

    private bool IsAnimatorStateActive(int stateHash, ref bool stateSeen)
    {
        if (_anim == null)
            return false;

        AnimatorStateInfo current = _anim.GetCurrentAnimatorStateInfo(0);
        bool isTransitioning = _anim.IsInTransition(0);
        bool currentMatches = current.shortNameHash == stateHash
            || current.fullPathHash == stateHash;
        bool nextMatches = false;
        if (isTransitioning)
        {
            AnimatorStateInfo next = _anim.GetNextAnimatorStateInfo(0);
            nextMatches = next.shortNameHash == stateHash
                || next.fullPathHash == stateHash;
        }

        if (currentMatches || nextMatches)
            stateSeen = true;

        if (currentMatches && !isTransitioning && current.normalizedTime >= 1f)
            return false;

        return currentMatches || nextMatches;
    }

    private void PlayDodgeAttempt()
    {
        Vector3 anchor = _battleDefenseAnchorPosition;
        Vector3 dodgeDir = Vector3.left; // 바라보는 방향/스케일과 무관하게 화면 왼쪽으로 후퇴
        PlayBattleAnim(HashBattleMove);
        uint animationVersion = _battleCharacter != null
            ? _battleCharacter.BattleAnimationVersion : _battleAnimationVersion;
        _vfx?.Play(CharacterVFX.VFXAction.Dodge_Dust);

        float backDistance = Mathf.Max(0.1f, _battleDodgeDistance);
        Vector3 overshoot = anchor + dodgeDir * backDistance;

        // Character의 기존 피격 취소 경로도 이 이동을 중단할 수 있게 타깃을 지정합니다.
        Sequence seq = DOTween.Sequence().SetTarget(transform).SetRecyclable(false);
        seq.Append(transform.DOMove(overshoot, 0.12f).SetEase(Ease.OutCubic));
        seq.AppendInterval(0.12f);
        seq.Append(transform.DOMove(anchor, 0.18f).SetEase(Ease.InOutSine));
        seq.SetUpdate(true);
        seq.OnComplete(() =>
        {
            if (this == null) return;
            if (_rb != null) _rb.position = anchor;
            transform.position = anchor;
            // 정상 복귀는 즉시 Idle. 피격/반격/다른 공격이 포즈를 가져갔다면
            // 오래된 회피 완료 콜백이 그 새 동작을 덮어쓰면 안 됩니다.
            uint currentVersion = _battleCharacter != null
                ? _battleCharacter.BattleAnimationVersion : _battleAnimationVersion;
            if (State == PlayerState.InBattle && animationVersion == currentVersion
                && !_hurtReactionActive
                && (_battleCharacter == null || (_battleCharacter.IsAlive && !_battleCharacter.IsHitReactionActive)))
                PlayBattleAnim(HashBattleIdle);
        });
        seq.OnKill(() =>
        {
            if (this == null) return;
            if (_rb != null) _rb.position = anchor;
            transform.position = anchor;
        });
        _defenseVisualTween = seq;
    }

    private void PlayJumpAttempt()
    {
        Vector3 anchor = _battleDefenseAnchorPosition;
        _vfx?.Play(CharacterVFX.VFXAction.Jump_Dust);

        Vector3 baseScale = _originalLocalScale == Vector3.zero ? transform.localScale : _originalLocalScale;
        Vector3 apex = anchor + Vector3.up * 2.8f;
        Vector3 squash = anchor + Vector3.down * 0.12f;

        Sequence seq = DOTween.Sequence().SetRecyclable(false);
        seq.Append(transform.DOMove(apex, 0.18f).SetEase(Ease.OutCubic));
        seq.Join(transform.DOScale(new Vector3(baseScale.x * 0.92f, baseScale.y * 1.08f, baseScale.z), 0.12f).SetEase(Ease.OutSine));
        seq.Append(transform.DOMove(squash, 0.18f).SetEase(Ease.InCubic));
        seq.Join(transform.DOScale(new Vector3(baseScale.x * 1.08f, baseScale.y * 0.90f, baseScale.z), 0.08f).SetEase(Ease.OutSine));
        seq.Append(transform.DOMove(anchor, 0.08f).SetEase(Ease.OutBack));
        seq.Join(transform.DOScale(baseScale, 0.10f).SetEase(Ease.OutBack));
        seq.SetUpdate(true);
        seq.OnComplete(() =>
        {
            if (_rb != null) _rb.position = anchor;
            transform.position = anchor;
            transform.localScale = baseScale;
        });
        seq.OnKill(() =>
        {
            if (_rb != null) _rb.position = anchor;
            transform.position = anchor;
            transform.localScale = baseScale;
        });
        _defenseVisualTween = seq;
    }

    public void ResetDefenseReactionLock()
    {
        CloseDefenseInputWindow();
        KillDefenseVisualTween();
        if (State == PlayerState.InBattle)
        {
            if (_rb != null) _rb.position = _battleDefenseAnchorPosition;
            transform.position = _battleDefenseAnchorPosition;
            transform.localScale = _originalLocalScale == Vector3.zero ? transform.localScale : _originalLocalScale;
            if (_rb != null) _rb.linearVelocity = Vector2.zero;
            PlayBattleAnim(HashBattleIdle);
        }
    }

    /// <summary>
    /// 적 타격의 입력 창만 닫습니다. 저스트가드/회피 모션은 즉시 Idle로 되돌리지 않아
    /// 피해 적용 및 후속 연출과 같은 프레임에 시작된 상태를 유지합니다.
    /// </summary>
    public void CloseDefenseInputWindow()
    {
        _defensePresentationGate.Close();
        _bufferedDefenseInput = DefenseInput.None;
        _bufferedDefenseInputTime = -999f;
        _lastPreviewedDefenseInput = DefenseInput.None;
        _lastPreviewedDefenseInputTime = float.NegativeInfinity;
    }

    public void PrepareDefenseWindow()
    {
        _defensePresentationGate.Open();
        // 회피 이동 중인 좌표를 복귀 지점으로 채택하지 않습니다.
        // 기준 위치는 전투 배치/명시적 SnapToBattleAnchor에서만 갱신합니다.
        // 적 행동 시작 전에 이미 누른 입력은 버리지 않습니다. 이전 창의
        // CloseDefenseInputWindow가 오래된 입력을 비우며, 버퍼 자체도 만료
        // 시각을 검사하므로 새 공격으로 입력이 새지 않습니다.
        // 준비 단계와 실제 판정 시작이 연달아 호출돼도 진행 중인 회피의 종류를 잊지 않습니다.
        if (!HasActiveDefenseVisualTween())
        {
            _lastPreviewedDefenseInput = DefenseInput.None;
            _lastPreviewedDefenseInputTime = float.NegativeInfinity;
        }
    }

    public Vector3 BattleDefenseAnchorPosition => _battleDefenseAnchorPosition;
    internal BattleDefenderPresentationScope ActiveDefensePresentation { get; set; }

    public void SnapToBattleAnchor(Vector3 worldPosition, bool playIdle = true)
    {
        _battleDefenseAnchorPosition = worldPosition;
        KillDefenseVisualTween();

        if (_rb != null)
        {
            DOTween.Kill(_rb);
            _rb.position = worldPosition;
            _rb.linearVelocity = Vector2.zero;
        }

        transform.position = worldPosition;

        if (playIdle)
            PlayBattleAnim(HashBattleIdle);
    }

    private void UpdateSortingOrder()
    {
        if (!_hasSortingBase || _spriteRenderer == null) return;
        if (State == PlayerState.InBattle) return;

        _spriteRenderer.sortingOrder = _baseSortingOrder - Mathf.RoundToInt(transform.position.y * 100f) + _battleSortingBoost;
    }

    public void SetBattleSortingBoost(int boost)
    {
        _battleSortingBoost = boost;
        if (_spriteRenderer != null && State == PlayerState.InBattle)
            _spriteRenderer.sortingOrder = _baseSortingOrder + boost;
    }

    public void SetScreenFlashScaleProvider(IScreenFlashScaleProvider provider)
    {
        _screenFlashScaleProvider = provider ?? new GameConfigScreenFlashScaleProvider();
    }

    public void SetScreenShakeScaleProvider(IScreenShakeScaleProvider provider)
    {
        _screenShakeScaleProvider = provider ?? new GameConfigScreenShakeScaleProvider();
    }

    private Color ResolveFlashColor(Color authoredColor)
    {
        float scale = VisualAccessibilityPolicy.NormalizeScale(
            _screenFlashScaleProvider?.Scale
            ?? GameConfigManager.DefaultFlashIntensity);
        return VisualAccessibilityPolicy.ScaleFlashColor(
            Color.white,
            authoredColor,
            scale);
    }

    private float ResolveShakeScale()
    {
        return VisualAccessibilityPolicy.NormalizeScale(
            _screenShakeScaleProvider?.Scale
            ?? GameConfigManager.DefaultScreenShake);
    }

    // ── DOTween 이펙트 ────────────────────────────────────────
    public void PlayParryEffect()
    {
        PlayParryEffect(true);
    }

    public void PlayCounterParry()
    {
        if (_anim == null) return;
        _battleAnimationVersion++;
        if (_battleCharacter != null) _battleCharacter.PlayBattleAnim(HashParry);
        else _anim.SetTrigger(HashParry);
        // 반격 서비스가 후퇴/재접근 위치를 소유하므로 색상 트윈은 위치를 복원하지 않습니다.
        PlayParryEffect(false);
    }

    private void PlayParryEffect(bool restoreDefenseAnchor)
    {
        if (_spriteRenderer == null) return;

        KillDefenseVisualTween();
        _spriteRenderer.DOKill(true);
        transform.DOKill(true);

        _vfx?.Play(CharacterVFX.VFXAction.Parry_Success);

        Color restoreColor = _spriteRenderer.color;
        Sequence feedback = DOTween.Sequence()
            .SetUpdate(true)
            .SetRecyclable(false)
            .SetTarget(transform);
        feedback.Join(_spriteRenderer.DOColor(
                ResolveFlashColor(_parryFlashColor),
                Mathf.Max(0.01f, _parryFlashDuration))
            .SetLoops(2, LoopType.Yoyo));
        feedback.OnComplete(() =>
        {
            if (_spriteRenderer != null)
                _spriteRenderer.color = restoreColor;
            if (ReferenceEquals(_defenseVisualTween, feedback))
                _defenseVisualTween = null;
        });
        feedback.OnKill(() =>
        {
            if (_spriteRenderer != null)
                _spriteRenderer.color = restoreColor;
            if (restoreDefenseAnchor && this != null && State == PlayerState.InBattle)
            {
                if (_rb != null) _rb.position = _battleDefenseAnchorPosition;
                transform.position = _battleDefenseAnchorPosition;
            }
            if (ReferenceEquals(_defenseVisualTween, feedback))
                _defenseVisualTween = null;
        });
        _defenseVisualTween = feedback;
    }

    private void PlayAttackEffect()
    {
        // 일반 공격은 타겟 앞에 도착한 뒤 제자리에서 처리합니다. 공격 이펙트가
        // transform을 펀치해 다시 돌진하는 것처럼 보이지 않도록 위치 트윈을 만들지 않습니다.
        _vfx?.Play(CharacterVFX.VFXAction.Attack_Normal);
    }

    public void PlayHurtEffect()
    {
        if (_spriteRenderer == null) return;

        bool hadHitReaction = _hurtReactionActive;
        Vector3 origin = hadHitReaction
            ? _hurtReactionOrigin
            : State == PlayerState.InBattle ? _battleDefenseAnchorPosition : transform.position;

        KillDefenseVisualTween();
        transform.DOKill(false);
        if (!hadHitReaction && State == PlayerState.InBattle)
            origin = _battleDefenseAnchorPosition;

        _hurtReactionOrigin = origin;
        _hurtReactionActive = true;
        transform.position = origin;
        if (State == PlayerState.InBattle && _rb != null)
        {
            _rb.position = origin;
            _rb.linearVelocity = Vector2.zero;
        }

        _spriteRenderer.DOKill();
        Color restoreColor = _spriteRenderer.color;

        _spriteRenderer.DOColor(ResolveFlashColor(_hurtFlashColor), Mathf.Max(0.01f, _hurtFlashDuration))
            .SetUpdate(true)
            .SetLoops(4, LoopType.Yoyo)
            .OnComplete(() => _spriteRenderer.color = restoreColor)
            .OnKill(() => _spriteRenderer.color = restoreColor);

        float popHeight = ResolveHurtPopHeight() * ResolveShakeScale();
        Sequence pop = DOTween.Sequence().SetRecyclable(false).SetUpdate(true);
        pop.SetTarget(transform);
        Vector3 recoil = State == PlayerState.InBattle
            ? origin + Vector3.left * Mathf.Min(0.1875f, popHeight)
            : origin + Vector3.up * popHeight;
        pop.Append(transform.DOMove(recoil, ResolveHurtPopUpDuration())
            .SetEase(Ease.OutQuad));
        pop.Append(transform.DOMove(origin, ResolveHurtPopReturnDuration())
            .SetEase(Ease.InQuad));
        pop.OnComplete(() => CompleteHurtReaction(origin));
        pop.OnKill(() => CompleteHurtReaction(origin));
    }

    private void CompleteHurtReaction(Vector3 origin)
    {
        if (this == null)
            return;

        transform.position = origin;
        if (State == PlayerState.InBattle && _rb != null)
        {
            _rb.position = origin;
            _rb.linearVelocity = Vector2.zero;
        }

        _hurtReactionActive = false;
    }

    private float ResolveHurtPopHeight()
    {
        return _hurtPopHeight > 0f ? _hurtPopHeight : Mathf.Max(0f, _hurtShakeStrength);
    }

    private float ResolveHurtPopUpDuration()
    {
        return _hurtPopUpDuration > 0.01f
            ? _hurtPopUpDuration
            : Mathf.Max(0.01f, _hurtShakeDuration * 0.3f);
    }

    private float ResolveHurtPopReturnDuration()
    {
        return _hurtPopReturnDuration > 0.01f
            ? _hurtPopReturnDuration
            : Mathf.Max(0.01f, _hurtShakeDuration * 0.7f);
    }

    public void PlayDieEffect()
    {
        if (_spriteRenderer == null) return;

        _spriteRenderer.DOKill();
        DOTween.Kill(transform);

        Sequence seq = DOTween.Sequence();
        seq.Append(
            _spriteRenderer.DOColor(ResolveFlashColor(_dieFlashColor), _dieFlashDuration)
                .SetLoops(6, LoopType.Yoyo));
        seq.Append(transform.DOMoveY(transform.position.y - 0.3f, 0.6f).SetEase(Ease.InQuad));
        seq.Join(_spriteRenderer.DOFade(0f, 0.6f).SetEase(Ease.InQuad));
    }

    public void PlayVictoryEffect()
    {
        DOTween.Kill(transform);
        transform.DOPunchPosition(Vector3.up * 0.25f, 0.4f, 2, 0.5f);
    }

    // ═══════════════════════════════════════════════════════════
    // ── Odin Inspector 애니메이션 테스트 (에디터 전용) ────────
    // ═══════════════════════════════════════════════════════════
#if UNITY_EDITOR
    [Title("Animation Test (No Parameters)")]
    [InfoBox("에디터 모드 오류 방지를 위해 파라미터 대신 상태(State)를 직접 재생합니다.")]

    [BoxGroup("Overworld Look"), Button("기본 대기 (Down)", ButtonSizes.Medium)]
    private void TestIdleLook()
    {
        if (Animator == null) return;
        Animator.Play("Idle_Down");
    }

    [BoxGroup("Battle"), Button("Battle Idle",  ButtonSizes.Medium)]
    private void TestBattleIdle() { PlayBattleAnim(HashBattleIdle); }

    [BoxGroup("Battle"), Button("Battle Move",  ButtonSizes.Medium)]
    private void TestBattleMove() { PlayBattleAnim(HashBattleMove); }

    [BoxGroup("Battle"), Button("Parry ✦",    ButtonSizes.Medium)]
    private void TestParry()      { PlayBattleAnim(HashParry); }

    [BoxGroup("Battle"), Button("Attack ✦",   ButtonSizes.Medium)]
    private void TestAttack()     { PlayBattleAnim(HashAttack); }

    [BoxGroup("Battle"), Button("Hurt ✦",     ButtonSizes.Medium)]
    private void TestHurt()       { PlayBattleAnim(HashHurt); }

    [BoxGroup("Battle"), Button("Die ✦",      ButtonSizes.Medium)]
    private void TestDie()        { PlayBattleAnim(HashDie); }

    [BoxGroup("Battle"), Button("Victory ✦",  ButtonSizes.Medium)]
    private void TestVictory()    { PlayBattleAnim(HashVictory); }
#endif
}
