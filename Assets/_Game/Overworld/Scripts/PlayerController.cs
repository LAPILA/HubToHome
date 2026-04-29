using UnityEngine;
using UnityEngine.InputSystem;
using DG.Tweening;
using Sirenix.OdinInspector;

/// <summary>
/// 오버월드 플레이어 이동 및 입력 처리 컨트롤러.
/// 픽셀 게임 스타일: 즉각 반응 이동 (가속/감속 없음).
/// 반대 방향 동시 입력 시 마지막으로 누른 방향 우선 처리.
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Animator))]
public class PlayerController : MonoBehaviour
{
    // ── 플레이어 상태 ─────────────────────────────────────────
    public enum PlayerState { Idle, Moving, Interacting, InMenu, InBattle }
    public PlayerState State { get; private set; } = PlayerState.Idle;

    /// <summary>
    /// 전투 씬에서 이동/상호작용 입력을 완전히 잠급니다.
    /// BattleManager.Start()에서 호출하세요.
    /// </summary>
    public void SetBattleMode(bool active)
    {
        if (_rb == null) _rb = GetComponent<Rigidbody2D>();

        if (active)
        {
            State = PlayerState.InBattle;
            _rb.bodyType = RigidbodyType2D.Kinematic;
            _rb.linearVelocity = Vector2.zero;
            
            if (_anim != null) _anim.SetTrigger(HashBattleIdle);
        }
        else
        {
            State = PlayerState.Idle;
            _rb.bodyType = RigidbodyType2D.Dynamic;
        }
    }

    // ── 이동 설정 ─────────────────────────────────────────────
    [Header("Movement")]
    [SerializeField] private float _moveSpeed = 5f;

    // ── 방향 (0=Down 1=Up 2=Left 3=Right) ────────────────────
    public int FacingDirection { get; private set; } = 0;

    // ── 컴포넌트 캐싱 ─────────────────────────────────────────
    private Rigidbody2D    _rb;
    private Animator       _anim;
    private Animator Animator 
    {
        get 
        {
            // 캐싱된 게 없으면 가져오고, 있으면 그대로 반환
            if (_anim == null) _anim = GetComponent<Animator>();
            return _anim;
        }
    }
    private SpriteRenderer _spriteRenderer;
    private Vector3 _originalLocalPos;
    // ── 입력 ──────────────────────────────────────────────────
    private InputAction _moveAction;
    private InputAction _confirmAction;
    private InputAction _cancelAction;
    private InputAction _menuAction;

    // ── 반대 방향 동시 입력 처리용 (Last-Input Priority) ─────
    private bool _keyLeft, _keyRight, _keyUp, _keyDown;
    private bool _prevLeft, _prevRight, _prevUp, _prevDown;
    private int  _lastHorizontal = 0;
    private int  _lastVertical   = 0;

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

    // ── DOTween 연출 설정 ─────────────────────────────────────
    [Header("VFX Settings")]
    [SerializeField] private float _parryFlashDuration = 0.08f;
    [SerializeField] private Color _parryFlashColor    = Color.cyan;
    [SerializeField] private float _hurtFlashDuration  = 0.05f;
    [SerializeField] private float _hurtShakeDuration  = 0.3f;
    [SerializeField] private float _hurtShakeStrength  = 0.15f;
    [SerializeField] private Color _hurtFlashColor     = Color.red;
    [SerializeField] private float _dieFlashDuration   = 0.12f;
    [SerializeField] private Color _dieFlashColor      = Color.white;

    // ─────────────────────────────────────────────────────────
    private void Awake()
    {
        _rb             = GetComponent<Rigidbody2D>();
        _anim           = GetComponent<Animator>();
        _spriteRenderer = GetComponent<SpriteRenderer>();

        _moveAction    = new InputAction("Move",    InputActionType.Value);
        _confirmAction = new InputAction("Confirm", InputActionType.Button);
        _cancelAction  = new InputAction("Cancel",  InputActionType.Button);
        _menuAction    = new InputAction("Menu",    InputActionType.Button);

        _moveAction.AddCompositeBinding("2DVector")
            .With("Up",    "<Keyboard>/upArrow").With("Up",    "<Keyboard>/w")
            .With("Down",  "<Keyboard>/downArrow").With("Down",  "<Keyboard>/s")
            .With("Left",  "<Keyboard>/leftArrow").With("Left",  "<Keyboard>/a")
            .With("Right", "<Keyboard>/rightArrow").With("Right", "<Keyboard>/d");
        _confirmAction.AddBinding("<Keyboard>/z");
        _cancelAction.AddBinding("<Keyboard>/x");
        _menuAction.AddBinding("<Keyboard>/c");

        _moveAction.Enable();
        _confirmAction.Enable();
        _cancelAction.Enable();
        _menuAction.Enable();
        _originalLocalPos = transform.localPosition;
    }

    private void Start()
    {
        LoadPositionFromGlobal();
        UpdateAnimator(); 
    }

    private void Update()
    {
        if (State == PlayerState.Interacting || State == PlayerState.InMenu) return;

        ReadInput();
        UpdateFacingDirection();
        UpdateAnimator();

        if (_confirmAction.WasPressedThisFrame()) TryInteract();
        if (_menuAction.WasPressedThisFrame())    OpenMenu();
    }

    private void FixedUpdate()
    {
        if (State == PlayerState.Interacting || State == PlayerState.InMenu || State == PlayerState.InBattle)
        {
            _rb.linearVelocity = Vector2.zero;
            return;
        }
        ApplyMovement();
    }

    // ── 입력 읽기 (Last-Input Priority) ──────────────────────
    private void ReadInput()
    {
        // Keyboard.current로 직접 읽어 Composite 간섭 없이 개별 키 상태 파악
        var kb = Keyboard.current;
        if (kb == null) return;

        _keyLeft  = kb.leftArrowKey.isPressed  || kb.aKey.isPressed;
        _keyRight = kb.rightArrowKey.isPressed || kb.dKey.isPressed;
        _keyUp    = kb.upArrowKey.isPressed    || kb.wKey.isPressed;
        _keyDown  = kb.downArrowKey.isPressed  || kb.sKey.isPressed;

        // ── 수평 Last-Input Priority ──────────────────────────
        if (_keyLeft && _keyRight)
        {
            if (!_prevLeft  && _keyLeft)  _lastHorizontal = -1;
            else if (!_prevRight && _keyRight) _lastHorizontal =  1;
            // 둘 다 이전부터 눌려 있으면 유지 (멈추지 않음)
        }
        else if (_keyLeft)  _lastHorizontal = -1;
        else if (_keyRight) _lastHorizontal =  1;
        else                _lastHorizontal =  0;

        // ── 수직 Last-Input Priority ──────────────────────────
        if (_keyUp && _keyDown)
        {
            if (!_prevUp   && _keyUp)   _lastVertical =  1;
            else if (!_prevDown && _keyDown) _lastVertical = -1;
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

    // ── 애니메이터 업데이트 ───────────────────────────────────
    private void UpdateAnimator()
    {
        if (_anim == null) return;
        
        if (State == PlayerState.Moving)
        {
            _anim.SetFloat(HashMoveX, _moveInput.x);
            _anim.SetFloat(HashMoveY, _moveInput.y);
        }
        _anim.SetBool(HashIsMoving, State == PlayerState.Moving);
    }

    // ── 전투 애니메이션 + DOTween 연출 ───────────────────────
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

    // ── DOTween 연출 ──────────────────────────────────────────

    /// <summary>사망: 흰 플래시 → 아래로 가라앉으며 페이드 아웃</summary>
    private void PlayDieEffect()
    {
        if (_spriteRenderer == null) return;
        DOTween.Kill(transform);
        DOTween.Kill(_spriteRenderer);

        Sequence seq = DOTween.Sequence();
        seq.Append(_spriteRenderer.DOColor(_dieFlashColor, _dieFlashDuration)
            .SetLoops(6, LoopType.Yoyo).SetEase(Ease.Linear));
        seq.Append(transform.DOMoveY(transform.position.y - 0.3f, 0.6f).SetEase(Ease.InQuad));
        seq.Join(_spriteRenderer.DOFade(0f, 0.6f).SetEase(Ease.InQuad));
    }

    /// <summary>공격: 앞으로 찌르기</summary>
    private void PlayAttackEffect()
    {
        DOTween.Kill(transform);
        transform.DOPunchPosition(Vector3.right * 0.2f, 0.15f, 1, 0.3f);
    }

    /// <summary>승리: 위아래 바운스</summary>
    private void PlayVictoryEffect()
    {
        DOTween.Kill(transform);
        transform.DOPunchPosition(Vector3.up * 0.25f, 0.4f, 2, 0.5f);
    }

    // ── 상호작용 ──────────────────────────────────────────────
    private void TryInteract()
    {
        InteractionSystem.Instance?.TryInteract(this);
    }

    private void OpenMenu()
    {
        Debug.Log("[PlayerController] Menu opened.");
    }

    public void SetInteracting(bool isInteracting)
    {
        State = isInteracting ? PlayerState.Interacting : PlayerState.Idle;
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
    // ── 전투 전용 액션 (DOTween) ──────────────────────────────

    /// <summary>회피 (C키): 뒤로 빠르게 물러났다 돌아옴</summary>
    public void ExecuteDodge()
{
    DOTween.Kill(transform);
    transform.DOLocalMoveX(transform.localPosition.x - 1.5f, 0.15f)
        .SetEase(Ease.OutExpo)
        .SetLoops(2, LoopType.Yoyo);
}

    /// <summary>점프 (Space키): 애니메이션 없이 Y축 포물선</summary>
    public void ExecuteJump()
    {
    DOTween.Kill(transform);
    // 현재 위치에서 위쪽(+Y)으로 이동했다가 복귀
    transform.DOLocalMoveY(transform.localPosition.y + 2.0f, 0.2f)
        .SetEase(Ease.OutQuad)
        .SetLoops(2, LoopType.Yoyo);
    }

    public void ExecuteParry()
    {
        PlayBattleAnim(HashParry); 
    }

    /// <summary>패링 성공 연출</summary>
    public void PlayParryEffect()
    {
        if (_spriteRenderer == null) return;
        _spriteRenderer.DOKill();
        // 청록색 플래시
        _spriteRenderer.DOColor(Color.cyan, 0.05f).SetLoops(2, LoopType.Yoyo);
        // 앞으로 짧고 강하게 툭!
        transform.DOPunchPosition(Vector3.right * 0.3f, 0.2f, 10, 1f);
    }

    /// <summary>피격 연출: 빨간색 플래시 + 움찔(Shake)</summary>
    public void PlayHurtEffect()
    {
        if (_spriteRenderer == null) return;
        _spriteRenderer.DOKill();
        transform.DOKill();

        // 빨간색으로 깜빡임
        _spriteRenderer.DOColor(Color.red, 0.05f).SetLoops(4, LoopType.Yoyo);
        // 움찔거리는 쉐이크 효과
        transform.DOShakePosition(0.2f, 0.2f, 30, 90f);
    }
    // ═══════════════════════════════════════════════════════════
    // ── Odin Inspector 애니메이션 테스트 (에디터 전용) ────────
    // ═══════════════════════════════════════════════════════════
#if UNITY_EDITOR
    [Title("Animation Test (No Parameters)")]
    [InfoBox("에디터 모드 오류 방지를 위해 파라미터 대신 상태(State)를 직접 재생합니다.")]

    // ✅ 이동은 제외하고 상태 확인용으로만 구성
    [BoxGroup("Overworld Look"), Button("기본 대기 (Down)", ButtonSizes.Medium)]
    private void TestIdleLook()
    {
        if (Animator == null) return;
        // 상태 이름을 직접 호출 (컨트롤러 내의 State 이름을 적어주세요)
        Animator.Play("Idle_Down"); 
    }

    [BoxGroup("Battle"), Button("Battle Idle", ButtonSizes.Medium)]
    private void TestBattleIdle() { PlayBattleAnim(HashBattleIdle); }

    [BoxGroup("Battle"), Button("Battle Move", ButtonSizes.Medium)]
    private void TestBattleMove() { PlayBattleAnim(HashBattleMove); }

    [BoxGroup("Battle"), Button("Parry ✦", ButtonSizes.Medium)]
    private void TestParry()      { PlayBattleAnim(HashParry); }

    [BoxGroup("Battle"), Button("Attack ✦", ButtonSizes.Medium)]
    private void TestAttack()     { PlayBattleAnim(HashAttack); }

    [BoxGroup("Battle"), Button("Hurt ✦", ButtonSizes.Medium)]
    private void TestHurt()       { PlayBattleAnim(HashHurt); }

    [BoxGroup("Battle"), Button("Die ✦", ButtonSizes.Medium)]
    private void TestDie()        { PlayBattleAnim(HashDie); }

    [BoxGroup("Battle"), Button("Victory ✦", ButtonSizes.Medium)]
    private void TestVictory()    { PlayBattleAnim(HashVictory); }
#endif
}
