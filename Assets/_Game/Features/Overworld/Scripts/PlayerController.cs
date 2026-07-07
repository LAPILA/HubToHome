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
public class PlayerController : MonoBehaviour
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
    [SerializeField] private float _attackDelay = 0.25f;
    [SerializeField] private float _attackRecoverDelay = 0.35f;
    [SerializeField] private LayerMask _enemyLayerMask = ~0;
    [SerializeField] private string _attackTriggerName = "Attack";
    private float _lastActionTime;

    // ── VFX / DOTween 연출 설정 ───────────────────────────────
    [Header("VFX Settings")]
    [SerializeField] private float _parryFlashDuration = 0.08f;
    [SerializeField] private Color _parryFlashColor    = Color.cyan;
    [SerializeField] private float _hurtFlashDuration  = 0.05f;
    [SerializeField] private float _hurtShakeDuration  = 0.3f;
    [SerializeField] private float _hurtShakeStrength  = 0.15f;
    [SerializeField] private Color _hurtFlashColor     = Color.red;
    [SerializeField] private float _dieFlashDuration   = 0.12f;
    [SerializeField] private Color _dieFlashColor      = Color.white;
    [SerializeField] private float _defenseAttemptCooldown = 0.20f;

    // ── 컴포넌트 캐싱 ─────────────────────────────────────────
    private Rigidbody2D    _rb;
    private Animator       _anim;
    private CharacterVFX   _vfx;
    private SpriteRenderer _spriteRenderer;
    private Collider2D[] _colliders;
    private Vector3        _originalLocalPos;
    private Vector3        _originalLocalScale;
    private bool _defenseReactionLocked;
    private float _lastDefenseAttemptTime = -999f;
    private Vector3 _battleDefenseAnchorPosition;
    private int _baseSortingOrder;
    private bool _hasSortingBase;
    private Tween _defenseVisualTween;
    private int _battleSortingBoost;
    private DefenseInput _bufferedDefenseInput = DefenseInput.None;
    private float _bufferedDefenseInputTime = -999f;
    private const float DefenseInputBufferWindow = 1.25f;
    private bool _defenseInputWindowOpen;
    private bool _preemptiveAttackInProgress;
    private readonly Collider2D[] _preemptiveAttackHits = new Collider2D[12];

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

    // ─────────────────────────────────────────────────────────
    private void Awake()
    {
        _rb             = GetComponent<Rigidbody2D>();
        _anim           = GetComponent<Animator>();
        _spriteRenderer = GetComponent<SpriteRenderer>();
        _vfx            = GetComponent<CharacterVFX>();
        _colliders      = GetComponents<Collider2D>();

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
        // 🚨 1차 방어: 대화 중이거나 UI가 열려있을 때 입력을 완전 차단
        if (GameStateManager.Instance != null && !GameStateManager.Instance.CanPlayerMove)
        {
            _moveInput = Vector2.zero;
            UpdateAnimator(false);
            return;
        }

        // 🚨 2차 방어: 전투 중일 때 이동 차단 (AreaTrigger를 통한 심리스 전투 시)
        if (State == PlayerState.InBattle)
        {
            HandleBattleDefenseInput();
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
            UIManager.Instance.OpenPanel("OverWorldPanel");
    }

    private void LateUpdate()
    {
        UpdateSortingOrder();
    }

    private void HandleBattleDefenseInput()
    {
        if (BattleManager.Instance == null || BattleManager.Instance.CurrentState != BattleState.EnemyAction)
            return;

        if (!_defenseInputWindowOpen)
            return;

        if (GameInput.TryReadDefenseInputThisFrame(out DefenseInput input))
            AttemptDefenseInput(input);
    }

    private void AttemptDefenseInput(DefenseInput input)
    {
        if (Time.unscaledTime < _lastDefenseAttemptTime + _defenseAttemptCooldown)
            return;

        _lastDefenseAttemptTime = Time.unscaledTime;
        _bufferedDefenseInput = input;
        _bufferedDefenseInputTime = Time.unscaledTime;

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
        }
    }

    public bool TryConsumeBufferedDefenseInput(out DefenseInput input)
    {
        input = DefenseInput.None;

        if (_bufferedDefenseInput == DefenseInput.None)
            return false;

        if (Time.unscaledTime > _bufferedDefenseInputTime + DefenseInputBufferWindow)
        {
            _bufferedDefenseInput = DefenseInput.None;
            return false;
        }

        input = _bufferedDefenseInput;
        _bufferedDefenseInput = DefenseInput.None;
        return true;
    }

    public void PreviewDefenseInput(DefenseInput input)
    {
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
        if (_preemptiveAttackInProgress) return false;
        if (!CanExecuteAction()) return false;
        if (State == PlayerState.InBattle) return false;
        if (GameStateManager.Instance != null && !GameStateManager.Instance.CanPlayerMove) return false;

        IPreemptiveAttackTarget target = FindPreemptiveAttackTarget();
        StartCoroutine(CoPreemptiveAttack(target));
        return true;
    }

    private IEnumerator CoPreemptiveAttack(IPreemptiveAttackTarget target)
    {
        _preemptiveAttackInProgress = true;

        GameState previousState = GameStateManager.Instance != null
            ? GameStateManager.Instance.CurrentState
            : GameState.Exploration;

        GameStateManager.Instance?.ChangeState(GameState.Cutscene);
        StopOverworldMovement();
        TryPlayAnimatorTrigger(_attackTriggerName);

        if (_attackDelay > 0f)
            yield return new WaitForSecondsRealtime(_attackDelay);

        bool started = IsPreemptiveAttackTargetAlive(target) && target.TryStartPreemptiveAttack(this);

        if (!started)
        {
            if (_attackRecoverDelay > 0f)
                yield return new WaitForSecondsRealtime(_attackRecoverDelay);

            GameStateManager.Instance?.ChangeState(previousState == GameState.Paused ? GameState.Exploration : previousState);
            ResetOverworldAttackAnimation();
            StopOverworldMovement();
            _preemptiveAttackInProgress = false;
        }
    }

    private IPreemptiveAttackTarget FindPreemptiveAttackTarget()
    {
        int hitCount = Physics2D.OverlapCircleNonAlloc(transform.position, Mathf.Max(0f, _attackRange), _preemptiveAttackHits, _enemyLayerMask);
        IPreemptiveAttackTarget bestTarget = null;
        float bestDistanceSqr = float.MaxValue;

        for (int i = 0; i < hitCount; i++)
        {
            Collider2D hit = _preemptiveAttackHits[i];
            _preemptiveAttackHits[i] = null;
            if (hit == null) continue;

            IPreemptiveAttackTarget candidate = ResolvePreemptiveAttackTarget(hit);
            if (candidate == null || !candidate.CanStartPreemptiveAttack(this)) continue;

            Component component = candidate as Component;
            if (component == null) continue;

            float distanceSqr = ((Vector2)component.transform.position - (Vector2)transform.position).sqrMagnitude;
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
    }

    // ── 전투 모드 전환 ────────────────────────────────────────
    /// <summary>전투 씬에서 이동/상호작용 입력을 완전히 잠급니다.</summary>
    public void SetBattleMode(bool active)
    {
        if (_rb == null) _rb = GetComponent<Rigidbody2D>();

        if (active)
        {
            _defenseReactionLocked = false;
            _lastDefenseAttemptTime = -999f;
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
            _preemptiveAttackInProgress = false;
            _defenseReactionLocked = false;
            _lastDefenseAttemptTime = -999f;
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
        if (GlobalDataManager.Instance == null) return;
        transform.position = new Vector3(
            GlobalDataManager.Instance.SpawnX,
            GlobalDataManager.Instance.SpawnY, 0f);
        FacingDirection = GlobalDataManager.Instance.LookingDir;
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
        _defenseReactionLocked = true;
        TriggerParryAttemptAnim();
        _defenseVisualTween = DOVirtual.DelayedCall(0.22f, () => _defenseReactionLocked = false).SetUpdate(true);
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
        switch (input)
        {
            case DefenseInput.Parry:
                PlayParryEffect();
                break;
            case DefenseInput.Dodge:
                _vfx?.Play(CharacterVFX.VFXAction.Dodge_Dust);
                break;
            case DefenseInput.Jump:
                _vfx?.Play(CharacterVFX.VFXAction.Jump_Dust);
                break;
        }
    }

    public IEnumerator WaitForDefenseVisualComplete(float fallbackSeconds = 0.45f)
    {
        float started = Time.unscaledTime;
        while (_defenseVisualTween != null && _defenseVisualTween.IsActive() && Time.unscaledTime < started + fallbackSeconds)
            yield return null;
    }

    private void ResetDefenseVisualStateOnly()
    {
        _defenseReactionLocked = false;

        _defenseVisualTween?.Kill();
        _defenseVisualTween = null;
        if (_rb != null) DOTween.Kill(_rb);

        if (State == PlayerState.InBattle)
        {
            if (_rb != null) _rb.position = _battleDefenseAnchorPosition;
            transform.position = _battleDefenseAnchorPosition;
            transform.localScale = _originalLocalScale == Vector3.zero ? transform.localScale : _originalLocalScale;
            if (_rb != null) _rb.linearVelocity = Vector2.zero;
        }
    }

    private void TriggerParryAttemptAnim()
    {
        if (_anim == null) return;
        _anim.SetTrigger(HashParry);
    }

    private void PlayDodgeAttempt()
    {
        _defenseReactionLocked = true;
        Vector3 anchor = _battleDefenseAnchorPosition;
        Vector3 dodgeDir = -GetFacingVector(); // 뒤로 빠지기
        _vfx?.Play(CharacterVFX.VFXAction.Dodge_Dust);

        float backDistance = 2.2f;
        Vector3 overshoot = anchor + dodgeDir * backDistance;
        Vector3 rebound = anchor + dodgeDir * 0.35f;

        Sequence seq = DOTween.Sequence();
        seq.Append(transform.DOMove(overshoot, 0.16f).SetEase(Ease.OutCubic));
        seq.Append(transform.DOMove(rebound, 0.12f).SetEase(Ease.InOutSine));
        seq.Append(transform.DOMove(anchor, 0.10f).SetEase(Ease.OutBack));
        seq.SetUpdate(true);
        seq.OnComplete(() =>
        {
            if (_rb != null) _rb.position = anchor;
            transform.position = anchor;
            _defenseReactionLocked = false;
        });
        seq.OnKill(() =>
        {
            if (_rb != null) _rb.position = anchor;
            transform.position = anchor;
            _defenseReactionLocked = false;
        });
        _defenseVisualTween = seq;
    }

    private void PlayJumpAttempt()
    {
        _defenseReactionLocked = true;
        Vector3 anchor = _battleDefenseAnchorPosition;
        _vfx?.Play(CharacterVFX.VFXAction.Jump_Dust);

        Vector3 baseScale = _originalLocalScale == Vector3.zero ? transform.localScale : _originalLocalScale;
        Vector3 apex = anchor + Vector3.up * 2.8f;
        Vector3 squash = anchor + Vector3.down * 0.12f;

        Sequence seq = DOTween.Sequence();
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
            _defenseReactionLocked = false;
        });
        seq.OnKill(() =>
        {
            if (_rb != null) _rb.position = anchor;
            transform.position = anchor;
            transform.localScale = baseScale;
            _defenseReactionLocked = false;
        });
        _defenseVisualTween = seq;
    }

    public void ResetDefenseReactionLock()
    {
        _defenseReactionLocked = false;
        _lastDefenseAttemptTime = -999f;
        _bufferedDefenseInput = DefenseInput.None;
        _defenseInputWindowOpen = false;
        _defenseVisualTween?.Kill();
        _defenseVisualTween = null;
        if (_rb != null) DOTween.Kill(_rb);
        if (State == PlayerState.InBattle)
        {
            if (_rb != null) _rb.position = _battleDefenseAnchorPosition;
            transform.position = _battleDefenseAnchorPosition;
            transform.localScale = _originalLocalScale == Vector3.zero ? transform.localScale : _originalLocalScale;
            if (_rb != null) _rb.linearVelocity = Vector2.zero;
            PlayBattleAnim(HashBattleIdle);
        }
    }

    public void PrepareDefenseWindow()
    {
        _battleDefenseAnchorPosition = transform.position;
        _defenseReactionLocked = false;
        _bufferedDefenseInput = DefenseInput.None;
        _lastDefenseAttemptTime = -999f;
        _defenseInputWindowOpen = true;
    }

    public void SnapToBattleAnchor(Vector3 worldPosition, bool playIdle = true)
    {
        _battleDefenseAnchorPosition = worldPosition;
        _defenseReactionLocked = false;
        _lastDefenseAttemptTime = -999f;
        _defenseVisualTween?.Kill();
        _defenseVisualTween = null;

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
    // ── DOTween 이펙트 ────────────────────────────────────────
    public void PlayParryEffect()
    {
        if (_spriteRenderer == null) return;

        _spriteRenderer.DOKill(true);
        transform.DOKill(true);

        _vfx?.Play(CharacterVFX.VFXAction.Parry_Success);

        _spriteRenderer.DOColor(_parryFlashColor, _parryFlashDuration)
            .SetLoops(2, LoopType.Yoyo)
            .OnComplete(() => _spriteRenderer.color = Color.white)
            .OnKill(()    => _spriteRenderer.color = Color.white);

        transform.DOPunchPosition(GetFacingVector() * 0.3f, 0.2f, 10, 1f);
    }

    private void PlayAttackEffect()
    {
        DOTween.Kill(transform);
        _vfx?.Play(CharacterVFX.VFXAction.Attack_Normal);
        transform.DOPunchPosition(GetFacingVector() * 0.3f, 0.15f, 1, 0.3f);
    }

    public void PlayHurtEffect()
    {
        if (_spriteRenderer == null) return;

        _spriteRenderer.DOKill();
        DOTween.Kill(transform);

        _spriteRenderer.DOColor(_hurtFlashColor, _hurtFlashDuration).SetLoops(4, LoopType.Yoyo);
        transform.DOShakePosition(_hurtShakeDuration, _hurtShakeStrength, 30, 90f);
    }

    public void PlayDieEffect()
    {
        if (_spriteRenderer == null) return;

        _spriteRenderer.DOKill();
        DOTween.Kill(transform);

        Sequence seq = DOTween.Sequence();
        seq.Append(_spriteRenderer.DOColor(_dieFlashColor, _dieFlashDuration).SetLoops(6, LoopType.Yoyo));
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
