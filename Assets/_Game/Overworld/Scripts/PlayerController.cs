using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 오버월드 플레이어 이동 및 입력 처리 컨트롤러.
/// 8방향 이동, 가속/감속(Friction), 상호작용 입력을 담당합니다.
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
public class PlayerController : MonoBehaviour
{
    // ── 플레이어 상태 ─────────────────────────────────────────
    public enum PlayerState { Idle, Moving, Interacting, InMenu }
    public PlayerState State { get; private set; } = PlayerState.Idle;

    // ── 이동 설정 ─────────────────────────────────────────────
    [Header("Movement")]
    [SerializeField] private float _moveSpeed       = 5f;
    [SerializeField] private float _acceleration    = 20f;
    [SerializeField] private float _deceleration    = 25f;

    // ── 방향 (0=Down 1=Up 2=Left 3=Right) ────────────────────
    public int FacingDirection { get; private set; } = 0;

    // ── 컴포넌트 캐싱 ─────────────────────────────────────────
    private Rigidbody2D _rb;
    private Animator    _animator;

    // ── 입력 ──────────────────────────────────────────────────
    private InputAction _moveAction;
    private InputAction _confirmAction;   // Z키
    private InputAction _cancelAction;    // X키
    private InputAction _menuAction;      // C키

    private Vector2 _moveInput;
    private Vector2 _currentVelocity;

    // ── Animator 파라미터 해시 (GC 최소화) ───────────────────
    private static readonly int HashMoveX    = Animator.StringToHash("MoveX");
    private static readonly int HashMoveY    = Animator.StringToHash("MoveY");
    private static readonly int HashIsMoving = Animator.StringToHash("IsMoving");

    private void Awake()
    {
        _rb       = GetComponent<Rigidbody2D>();
        _animator = GetComponent<Animator>();

        // InputSystem_Actions 에서 액션 참조
        // InputSystem_Actions는 .inputactions 파일에서 자동 생성된 클래스입니다.
        // Unity 에디터에서 Keyboard/InputSystem_Actions.inputactions 파일을 선택 후
        // Inspector > Generate C# Class 를 활성화하면 클래스가 생성됩니다.
        // 생성 전까지는 직접 InputActionAsset을 참조합니다.
        var inputActions = new UnityEngine.InputSystem.InputActionAsset();
        // TODO: Inspector에서 InputActionAsset을 할당하거나 Generate C# Class 후 아래 코드로 교체:
        // var inputActions = new InputSystem_Actions();
        // _moveAction    = inputActions.Player.Move;
        // _confirmAction = inputActions.Player.Attack;
        // _cancelAction  = inputActions.Player.Cancel;
        // _menuAction    = inputActions.Player.Interact;
        // inputActions.Enable();

        // 임시: Keyboard 직접 입력 (InputSystem_Actions 생성 전까지 사용)
        _moveAction    = new UnityEngine.InputSystem.InputAction("Move",    UnityEngine.InputSystem.InputActionType.Value);
        _confirmAction = new UnityEngine.InputSystem.InputAction("Confirm", UnityEngine.InputSystem.InputActionType.Button);
        _cancelAction  = new UnityEngine.InputSystem.InputAction("Cancel",  UnityEngine.InputSystem.InputActionType.Button);
        _menuAction    = new UnityEngine.InputSystem.InputAction("Menu",    UnityEngine.InputSystem.InputActionType.Button);

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
    }

    private void Update()
    {
        if (State == PlayerState.Interacting || State == PlayerState.InMenu) return;

        ReadInput();
        UpdateFacingDirection();
        UpdateAnimator();

        // 상호작용 입력 (Z키)
        if (_confirmAction.WasPressedThisFrame())
            TryInteract();

        // 메뉴 입력 (C키)
        if (_menuAction.WasPressedThisFrame())
            OpenMenu();
    }

    private void FixedUpdate()
    {
        if (State == PlayerState.Interacting || State == PlayerState.InMenu)
        {
            _rb.linearVelocity = Vector2.zero;
            return;
        }

        ApplyMovement();
    }

    // ── 입력 읽기 ─────────────────────────────────────────────
    private void ReadInput()
    {
        _moveInput = _moveAction.ReadValue<Vector2>();

        // 대각선 이동 시 속도 정규화
        if (_moveInput.sqrMagnitude > 1f)
            _moveInput = _moveInput.normalized;
    }

    // ── 이동 적용 (가속/감속) ─────────────────────────────────
    private void ApplyMovement()
    {
        Vector2 targetVelocity = _moveInput * _moveSpeed;

        if (_moveInput.sqrMagnitude > 0.01f)
        {
            _currentVelocity = Vector2.MoveTowards(
                _currentVelocity, targetVelocity, _acceleration * Time.fixedDeltaTime);
            State = PlayerState.Moving;
        }
        else
        {
            _currentVelocity = Vector2.MoveTowards(
                _currentVelocity, Vector2.zero, _deceleration * Time.fixedDeltaTime);
            State = _currentVelocity.sqrMagnitude < 0.01f ? PlayerState.Idle : PlayerState.Moving;
        }

        _rb.linearVelocity = _currentVelocity;
    }

    // ── 방향 업데이트 ─────────────────────────────────────────
    private void UpdateFacingDirection()
    {
        if (_moveInput.sqrMagnitude < 0.01f) return;

        if (Mathf.Abs(_moveInput.x) >= Mathf.Abs(_moveInput.y))
            FacingDirection = _moveInput.x > 0 ? 3 : 2; // Right / Left
        else
            FacingDirection = _moveInput.y > 0 ? 1 : 0; // Up / Down
    }

    // ── 애니메이터 업데이트 ───────────────────────────────────
    private void UpdateAnimator()
    {
        if (_animator == null) return;
        _animator.SetFloat(HashMoveX, _moveInput.x);
        _animator.SetFloat(HashMoveY, _moveInput.y);
        _animator.SetBool(HashIsMoving, State == PlayerState.Moving);
    }

    // ── 상호작용 ──────────────────────────────────────────────
    private void TryInteract()
    {
        // InteractionSystem이 처리
        InteractionSystem.Instance?.TryInteract(this);
    }

    // ── 메뉴 ──────────────────────────────────────────────────
    private void OpenMenu()
    {
        // TODO: UIManager.Instance.OpenInventory();
        Debug.Log("[PlayerController] Menu opened.");
    }

    // ── 외부에서 상태 잠금/해제 ──────────────────────────────
    public void SetInteracting(bool isInteracting)
    {
        State = isInteracting ? PlayerState.Interacting : PlayerState.Idle;
    }

    // ── GlobalDataManager 연동 ────────────────────────────────
    /// <summary>씬 전환 전 현재 위치/방향을 GlobalDataManager에 저장합니다.</summary>
    public void SavePositionToGlobal()
    {
        if (GlobalDataManager.Instance == null) return;
        GlobalDataManager.Instance.SpawnX      = transform.position.x;
        GlobalDataManager.Instance.SpawnY      = transform.position.y;
        GlobalDataManager.Instance.LookingDir  = FacingDirection;
    }

    /// <summary>씬 로드 후 GlobalDataManager에서 위치를 복원합니다.</summary>
    public void LoadPositionFromGlobal()
    {
        if (GlobalDataManager.Instance == null) return;
        transform.position = new Vector3(
            GlobalDataManager.Instance.SpawnX,
            GlobalDataManager.Instance.SpawnY, 0f);
        FacingDirection = GlobalDataManager.Instance.LookingDir;
    }
}
