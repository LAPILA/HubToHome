using System.Collections.Generic;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using Sirenix.OdinInspector;

/// <summary>
/// 델타룬식 오버월드 적 컨트롤러.
/// - EnemyCharacter가 '적 본체/데이터 소유'
/// - OverworldEnemy는 '오버월드 이동/조우 연출'만 담당
/// </summary>
[RequireComponent(typeof(Collider2D))]
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(EnemyCharacter))]
public class OverworldEnemy : MonoBehaviour, IEncounterSource
{
    private static float s_globalEncounterLockUntil;

    [System.Serializable]
    private enum PersistentEnemyStateHandling
    {
        KeepAlive,
        DefeatOnVictory
    }

    public enum EncounterMode
    {
        PatrolContactBattle,
        CinematicOnly,
        Disabled
    }

    [Header("Encounter")]
    [InfoBox("대화형 NPC로 쓸 오브젝트에는 OverworldEnemy 대신 DialogueBattleNPC를 사용하세요. 같은 오브젝트에 둘을 같이 두면 접촉 전투가 먼저 발동할 수 있습니다.")]
    [SerializeField] private EncounterMode _mode = EncounterMode.PatrolContactBattle;
    [Tooltip("추가 동반 적이 필요할 때만 넣습니다. 첫 번째 적 데이터는 반드시 같은 오브젝트의 EnemyCharacter.Data를 사용합니다.")]
    [SerializeField] private List<EnemyData> _additionalEncounterEnemies = new List<EnemyData>();
    [SerializeField] private AudioClip _overrideBattleBGM;
    [SerializeField] private AudioClip _encounterSFX;
    [SerializeField] private float _encounterDelay = 0.08f;
    [SerializeField] private float _battleFadeDuration = 0.08f;
    [SerializeField] private string _battleSceneName = "BattleScene";
    [SerializeField] private bool _useDedicatedBattleScene = true;
    [SerializeField] private bool _destroyAfterTouch = false;
    [SerializeField] private float _postEscapeAlpha = 0.5f;
    [SerializeField] private float _postBattleGraceDuration = 1f;
    [SerializeField] private float _postBattleNudgeDistance = 0.35f;

    [Header("Persistence")]
    [SerializeField] private string _enemyId;
    [SerializeField] private PersistentEnemyStateHandling _victoryHandling = PersistentEnemyStateHandling.DefeatOnVictory;

    [Header("Patrol")]
    [SerializeField] private Transform[] _waypoints;
    [SerializeField] private float _moveSpeed = 2f;
    [SerializeField] private float _arriveDistance = 0.05f;
    [SerializeField] private float _waitAtPoint = 0.15f;
    [SerializeField] private bool _loop = true;

    [Header("Animation")]
    [SerializeField] private Animator _animator;
    [SerializeField] private bool _usePlayerLikeMoveParams = true;
    [SerializeField] private string _moveXParam = "Horizontal";
    [SerializeField] private string _moveYParam = "Vertical";
    [SerializeField] private string _isMovingParam = "IsMoving";
    [SerializeField] private string _battleIdleParam = "BattleIdle";

    private Rigidbody2D _rb;
    private Collider2D _collider;
    private EnemyCharacter _enemyCharacter;
    private int _waypointIndex;
    private int _direction = 1;
    private float _waitTimer;
    private bool _triggered;
    private int _hashMoveX;
    private int _hashMoveY;
    private int _hashIsMoving;
    private int _hashBattleIdle;
    private bool _hasMoveX;
    private bool _hasMoveY;
    private bool _hasIsMoving;
    private bool _hasBattleIdle;
    private bool _runtimeDisabledForBattle;
    private SpriteRenderer _spriteRenderer;
    private Coroutine _cooldownRoutine;
    private string _sceneName;
    private int _baseSortingOrder;
    private bool _hasSortingBase;
    private float _localEncounterBlockedUntil;
    private bool _waitForPlayerExitBeforeRearm;
    private PlayerController _pendingRearmPlayer;
    private bool _encounterInProgress;

    public string EnemyId => _enemyId;
    public bool DefeatsOnVictory => _victoryHandling == PersistentEnemyStateHandling.DefeatOnVictory;

    private void Awake()
    {
        if (GetComponent<DialogueBattleNPC>() != null)
            _mode = EncounterMode.Disabled;

        _sceneName = SceneManager.GetActiveScene().name;
        _rb = GetComponent<Rigidbody2D>();
        _collider = GetComponent<Collider2D>();
        _enemyCharacter = GetComponent<EnemyCharacter>();
        _spriteRenderer = GetComponent<SpriteRenderer>();
        if (_spriteRenderer != null)
        {
            _baseSortingOrder = _spriteRenderer.sortingOrder;
            _hasSortingBase = true;
        }
        if (_animator == null) _animator = GetComponent<Animator>();
        _hashMoveX = Animator.StringToHash(_moveXParam);
        _hashMoveY = Animator.StringToHash(_moveYParam);
        _hashIsMoving = Animator.StringToHash(_isMovingParam);
        _hashBattleIdle = Animator.StringToHash(_battleIdleParam);
        CacheAnimatorParameters();
        _collider.isTrigger = true;
        _rb.gravityScale = 0f;
        _rb.freezeRotation = true;
        _enemyCharacter?.SetBattleMode(false);

        EnsureEnemyId();
        RestorePersistentState();
    }

    private void FixedUpdate()
    {
        RefreshEncounterExitWait();
        if (!_encounterInProgress
            && _triggered
            && !_waitForPlayerExitBeforeRearm
            && _cooldownRoutine == null
            && Time.unscaledTime >= _localEncounterBlockedUntil)
        {
            _triggered = false;
        }

        if (_runtimeDisabledForBattle) return;
        if (_triggered || _mode != EncounterMode.PatrolContactBattle) return;
        Patrol();
    }

    private void LateUpdate()
    {
        UpdateSortingOrder();
    }

    private void Patrol()
    {
        if (_waypoints == null || _waypoints.Length == 0)
        {
            _rb.linearVelocity = Vector2.zero;
            UpdateMoveAnimation(Vector2.zero);
            return;
        }

        if (_waitTimer > 0f)
        {
            _waitTimer -= Time.fixedDeltaTime;
            _rb.linearVelocity = Vector2.zero;
            UpdateMoveAnimation(Vector2.zero);
            return;
        }

        Transform target = _waypoints[Mathf.Clamp(_waypointIndex, 0, _waypoints.Length - 1)];
        if (target == null) return;

        Vector2 current = _rb.position;
        Vector2 targetPos = target.position;
        Vector2 toTarget = targetPos - current;

        if (toTarget.magnitude <= _arriveDistance)
        {
            AdvanceWaypoint();
            _waitTimer = _waitAtPoint;
            _rb.linearVelocity = Vector2.zero;
            UpdateMoveAnimation(Vector2.zero);
            return;
        }

        Vector2 velocity = toTarget.normalized * _moveSpeed;
        _rb.linearVelocity = velocity;
        UpdateMoveAnimation(velocity);
    }

    private void AdvanceWaypoint()
    {
        if (_waypoints.Length <= 1) return;

        if (_loop)
        {
            _waypointIndex = (_waypointIndex + 1) % _waypoints.Length;
        }
        else
        {
            if (_waypointIndex >= _waypoints.Length - 1) _direction = -1;
            else if (_waypointIndex <= 0) _direction = 1;
            _waypointIndex += _direction;
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (_runtimeDisabledForBattle) return;
        if (EncounterCollisionGuard.IsGloballyBlocked) return;
        if (Time.unscaledTime < s_globalEncounterLockUntil) return;
        if (Time.unscaledTime < _localEncounterBlockedUntil) return;
        if (_waitForPlayerExitBeforeRearm) return;
        if (_triggered) return;
        if (_mode != EncounterMode.PatrolContactBattle) return;
        if (!other.CompareTag("Player")) return;

        PlayerController player = other.GetComponent<PlayerController>();
        if (player == null) return;

        StartCoroutine(StartSceneBattleRoutine(player));
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (!_waitForPlayerExitBeforeRearm) return;
        if (!other.CompareTag("Player")) return;

        PlayerController player = other.GetComponent<PlayerController>() ?? _pendingRearmPlayer;
        if (player == null || !EncounterCollisionGuard.IsPlayerOverlapping(_collider, player))
            ClearExitWaitAndRearm();
    }

    public void DisableForBattleInstance()
    {
        _runtimeDisabledForBattle = true;
        _triggered = true;

        if (_rb != null)
            _rb.linearVelocity = Vector2.zero;

        if (_collider != null)
            _collider.enabled = false;

        enabled = false;
    }

    private IEnumerator StartSceneBattleRoutine(PlayerController player)
    {
        List<EnemyData> resolvedEnemies = ResolveEncounterEnemies();
        if (_enemyCharacter == null || _enemyCharacter.Data == null)
        {
            Debug.LogWarning($"[OverworldEnemy] EnemyCharacter.Data가 비어있어 전투를 시작하지 않습니다. Object={gameObject.name}", this);
            yield break;
        }

        if (resolvedEnemies == null || resolvedEnemies.Count == 0)
        {
            Debug.LogWarning($"[OverworldEnemy] 전투 적 목록이 비어있어 전투를 시작하지 않습니다. Object={gameObject.name}", this);
            yield break;
        }

        _triggered = true;
        _encounterInProgress = true;
        s_globalEncounterLockUntil = Time.unscaledTime + Mathf.Max(0.75f, _encounterDelay + 0.5f);
        _rb.linearVelocity = Vector2.zero;
        UpdateMoveAnimation(Vector2.zero);
        if (_useDedicatedBattleScene && _collider != null)
            _collider.enabled = false;
        player.SetBattleMode(true);

        AudioManager.Instance?.PlaySFX(_encounterSFX);

        if (_encounterDelay > 0f)
            yield return new WaitForSecondsRealtime(_encounterDelay);

        bool started = BattleEncounterService.StartEncounter(
            player,
            resolvedEnemies,
            ResolveBattleBGM(resolvedEnemies),
            _useDedicatedBattleScene,
            _battleSceneName,
            _battleFadeDuration,
            _enemyId,
            DefeatsOnVictory,
            this);

        if (!started)
        {
            _encounterInProgress = false;
            _triggered = false;
        }

        if (_destroyAfterTouch && _useDedicatedBattleScene) Destroy(gameObject);
    }

    private void UpdateMoveAnimation(Vector2 velocity)
    {
        if (_animator == null || !_usePlayerLikeMoveParams) return;

        bool isMoving = velocity.sqrMagnitude > 0.001f;
        _enemyCharacter?.SetOverworldMoving(isMoving ? velocity.normalized : Vector2.zero, isMoving);

        if (_hasBattleIdle) _animator.ResetTrigger(_hashBattleIdle);
        if (isMoving)
        {
            if (_hasMoveX) _animator.SetFloat(_hashMoveX, velocity.normalized.x);
            if (_hasMoveY) _animator.SetFloat(_hashMoveY, velocity.normalized.y);
        }
        if (_hasIsMoving) _animator.SetBool(_hashIsMoving, isMoving);
    }

    private void CacheAnimatorParameters()
    {
        if (_animator == null) return;
        _hasMoveX = HasAnimatorParam(_hashMoveX, AnimatorControllerParameterType.Float);
        _hasMoveY = HasAnimatorParam(_hashMoveY, AnimatorControllerParameterType.Float);
        _hasIsMoving = HasAnimatorParam(_hashIsMoving, AnimatorControllerParameterType.Bool);
        _hasBattleIdle = HasAnimatorParam(_hashBattleIdle, AnimatorControllerParameterType.Trigger);
    }

    private bool HasAnimatorParam(int hash, AnimatorControllerParameterType type)
    {
        if (_animator == null) return false;
        foreach (var p in _animator.parameters)
            if (p.nameHash == hash && p.type == type) return true;
        return false;
    }

    private List<EnemyData> ResolveEncounterEnemies()
    {
        List<EnemyData> resolved = new List<EnemyData>();
        if (_enemyCharacter != null && _enemyCharacter.Data != null)
            resolved.Add(_enemyCharacter.Data);

        if (_additionalEncounterEnemies != null)
        {
            for (int i = 0; i < _additionalEncounterEnemies.Count; i++)
            {
                if (_additionalEncounterEnemies[i] != null)
                    resolved.Add(_additionalEncounterEnemies[i]);
            }
        }

        return resolved;
    }

    private AudioClip ResolveBattleBGM(List<EnemyData> enemies)
    {
        if (_overrideBattleBGM != null) return _overrideBattleBGM;

        for (int i = 0; i < enemies.Count; i++)
        {
            if (enemies[i] != null && enemies[i].BattleBGM != null)
                return enemies[i].BattleBGM;
        }

        return MapSettings.CurrentDefaultBattleBGM;
    }

    private void EnsureEnemyId()
    {
        if (!string.IsNullOrWhiteSpace(_enemyId)) return;
        _enemyId = $"{_sceneName}:{gameObject.name}:{transform.position.x:0.###}:{transform.position.y:0.###}";
    }

    private void RestorePersistentState()
    {
        var global = GlobalDataManager.Instance;
        if (global == null || string.IsNullOrWhiteSpace(_enemyId)) return;

        var state = global.GetOrCreateOverworldEnemyState(_enemyId, _sceneName);
        if (state == null) return;

        if (state.IsDefeated)
        {
            DisablePermanently();
            return;
        }

        float remaining = global.GetOverworldEnemyCooldownRemaining(_enemyId);
        if (remaining > 0f)
        {
            StartCooldown(remaining, state.CooldownAlpha > 0f ? state.CooldownAlpha : _postEscapeAlpha);
        }
        else
        {
            global.ClearOverworldEnemyCooldown(_enemyId);
            RestoreActiveState();
        }
    }

    private void StartCooldown(float duration, float alpha)
    {
        if (_cooldownRoutine != null)
            StopCoroutine(_cooldownRoutine);

        _cooldownRoutine = StartCoroutine(CoPostEscapeDisable(duration, alpha));
    }

    private IEnumerator CoPostEscapeDisable(float duration, float alpha)
    {
        _triggered = true;
        _waitTimer = duration;
        if (_rb != null) _rb.linearVelocity = Vector2.zero;
        UpdateMoveAnimation(Vector2.zero);

        Color original = _spriteRenderer != null ? _spriteRenderer.color : Color.white;
        float elapsed = 0f;
        bool faded = true;
        const float blinkInterval = 0.12f;

        while (elapsed < duration)
        {
            if (_spriteRenderer != null)
            {
                Color c = original;
                c.a = faded ? alpha : 1f;
                _spriteRenderer.color = c;
                faded = !faded;
            }

            yield return new WaitForSecondsRealtime(blinkInterval);
            elapsed += blinkInterval;
        }

        if (_spriteRenderer != null) _spriteRenderer.color = original;
        RestoreActiveState();

        var global = GlobalDataManager.Instance;
        global?.ClearOverworldEnemyCooldown(_enemyId);
        _cooldownRoutine = null;
    }

    private void RestoreActiveState()
    {
        if (_runtimeDisabledForBattle) return;

        if (_collider != null) _collider.enabled = true;
        if (_rb != null) _rb.linearVelocity = Vector2.zero;
        RefreshEncounterExitWait();
        _triggered = _waitForPlayerExitBeforeRearm;
        _waitTimer = 0f;
        UpdateMoveAnimation(Vector2.zero);
    }

    public void OnEncounterResolved(bool victory, PlayerController player)
    {
        _encounterInProgress = false;
        _pendingRearmPlayer = player;
        EncounterCollisionGuard.BlockAll(_postBattleGraceDuration);
        _localEncounterBlockedUntil = Time.unscaledTime + Mathf.Max(0f, _postBattleGraceDuration);
        s_globalEncounterLockUntil = Mathf.Max(s_globalEncounterLockUntil, _localEncounterBlockedUntil);

        EncounterCollisionGuard.NudgePlayerOutOf(_collider, player, _postBattleNudgeDistance);
        _waitForPlayerExitBeforeRearm = EncounterCollisionGuard.IsPlayerOverlapping(_collider, player);

        if (victory && DefeatsOnVictory)
        {
            DisablePermanently();
            return;
        }

        if (victory)
            RestoreActiveState();
        else
            StartCooldown(Mathf.Max(_postBattleGraceDuration, 0.75f), _postEscapeAlpha);
    }

    private void RefreshEncounterExitWait()
    {
        if (!_waitForPlayerExitBeforeRearm) return;
        if (_pendingRearmPlayer == null || !EncounterCollisionGuard.IsPlayerOverlapping(_collider, _pendingRearmPlayer))
            ClearExitWaitAndRearm();
    }

    private void ClearExitWaitAndRearm()
    {
        _waitForPlayerExitBeforeRearm = false;
        _pendingRearmPlayer = null;
        if (!_runtimeDisabledForBattle && Time.unscaledTime >= _localEncounterBlockedUntil)
            _triggered = false;
    }

    private void UpdateSortingOrder()
    {
        if (!_hasSortingBase || _spriteRenderer == null) return;
        _spriteRenderer.sortingOrder = _baseSortingOrder - Mathf.RoundToInt(transform.position.y * 100f);
    }

    private void DisablePermanently()
    {
        _triggered = true;
        _runtimeDisabledForBattle = true;

        if (_cooldownRoutine != null)
        {
            StopCoroutine(_cooldownRoutine);
            _cooldownRoutine = null;
        }

        if (_rb != null) _rb.linearVelocity = Vector2.zero;
        UpdateMoveAnimation(Vector2.zero);
        gameObject.SetActive(false);
    }
}
