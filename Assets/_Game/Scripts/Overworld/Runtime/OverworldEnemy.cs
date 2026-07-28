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
public class OverworldEnemy : MonoBehaviour, IEncounterSource, IEncounterOutcomeSource, IPreemptiveAttackTarget
{
    private static float s_globalEncounterLockUntil;

    [System.Serializable]
    private enum PersistentEnemyStateHandling
    {
        KeepAlive,
        DefeatOnVictory
    }

    [System.Serializable]
    private enum InstantVictoryStateHandling
    {
        FollowVictoryHandling,
        KeepAlive,
        DefeatPermanently
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
    [Tooltip("비워두면 BattleManager 기본 시나리오를 사용합니다. 특정 encounter만 Scenario Source 기반 흐름으로 실행할 때 지정합니다.")]
    [SerializeField] private BattleScenarioData _battleScenarioData;
    [SerializeField] private float _encounterDelay = 0.08f;
    [SerializeField] private float _battleFadeDuration = 0.08f;
    [SerializeField] private string _battleSceneName = "BattleScene";
    [SerializeField] private bool _useDedicatedBattleScene = true;
    [SerializeField] private bool _destroyAfterTouch = false;
    [SerializeField] private float _postEscapeAlpha = 0.5f;
    [SerializeField] private float _postBattleGraceDuration = 1f;
    [SerializeField] private float _postBattleNudgeDistance = 0.35f;
    [SerializeField] private bool _canInstantKillLater = true;

    [Header("Persistence")]
    [SerializeField] private string _enemyId;
    [SerializeField] private PersistentEnemyStateHandling _victoryHandling = PersistentEnemyStateHandling.DefeatOnVictory;
    [SerializeField] private InstantVictoryStateHandling _instantVictoryHandling = InstantVictoryStateHandling.FollowVictoryHandling;

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
    private IScreenFlashScaleProvider _screenFlashScaleProvider =
        new GameConfigScreenFlashScaleProvider();

    public string EnemyId => _enemyId;
    public string EncounterMemoryKey => BattleEncounterMemoryRecorder.ResolveMemoryKey(
        _battleScenarioData,
        _enemyId);
    public bool DefeatsOnVictory => _victoryHandling == PersistentEnemyStateHandling.DefeatOnVictory;
    public bool InstantVictoryDefeatsPermanently => _instantVictoryHandling == InstantVictoryStateHandling.DefeatPermanently
        || (_instantVictoryHandling == InstantVictoryStateHandling.FollowVictoryHandling && DefeatsOnVictory);
    public bool CanStartPreemptiveAttack(PlayerController player) => isActiveAndEnabled
        && player != null
        && !_runtimeDisabledForBattle
        && !_encounterInProgress
        && !_triggered
        && !_waitForPlayerExitBeforeRearm
        && _mode == EncounterMode.PatrolContactBattle
        && Time.unscaledTime >= _localEncounterBlockedUntil
        && Time.unscaledTime >= s_globalEncounterLockUntil
        && !EncounterCollisionGuard.IsGloballyBlocked;

    public void SetScreenFlashScaleProvider(IScreenFlashScaleProvider provider)
    {
        _screenFlashScaleProvider = provider ?? new GameConfigScreenFlashScaleProvider();
    }

    private Color ResolveFlashColor(Color safeColor, Color authoredColor)
    {
        float scale = VisualAccessibilityPolicy.NormalizeScale(
            _screenFlashScaleProvider?.Scale
            ?? GameConfigManager.DefaultFlashIntensity);
        return VisualAccessibilityPolicy.ScaleFlashColor(
            safeColor,
            authoredColor,
            scale);
    }

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
        if (!OverworldActionGate.AllowsWorldActions)
        {
            if (_rb != null) _rb.linearVelocity = Vector2.zero;
            UpdateMoveAnimation(Vector2.zero);
            return;
        }

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
        if (GameStateManager.Instance != null && !GameStateManager.Instance.CanPlayerMove) return;
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

    public bool TryStartPreemptiveAttack(PlayerController player)
    {
        if (!CanStartPreemptiveAttack(player)) return false;

        List<EnemyData> resolvedEnemies = ResolveEncounterEnemies();
        if (_enemyCharacter == null || _enemyCharacter.Data == null || resolvedEnemies == null || resolvedEnemies.Count == 0)
        {
            Debug.LogWarning($"[OverworldEnemy] 선공 전투 적 데이터가 비어있습니다. Object={gameObject.name}", this);
            return false;
        }

        GlobalDataManager global = GlobalDataManager.Instance;
        bool previouslyDefeated = HasRecordedEncounterVictory(global);
        FieldEncounterResolution resolution = FieldEncounterPolicy.Evaluate(
            global != null ? global.GetHighestPartyLevel() : 1,
            resolvedEnemies,
            previouslyDefeated,
            _canInstantKillLater);

        if (resolution == FieldEncounterResolution.InstantVictory)
            StartCoroutine(ResolveInstantVictoryRoutine(player, resolvedEnemies));
        else
            StartCoroutine(StartSceneBattleRoutine(player, true));
        return true;
    }


    private IEnumerator ResolveInstantVictoryRoutine(PlayerController player, List<EnemyData> resolvedEnemies)
    {
        _triggered = true;
        _encounterInProgress = true;
        s_globalEncounterLockUntil = Time.unscaledTime + 1f;
        if (_rb != null) _rb.linearVelocity = Vector2.zero;
        UpdateMoveAnimation(Vector2.zero);
        AudioManager.Instance?.PlaySFX(_encounterSFX);

        GlobalDataManager global = GlobalDataManager.Instance;
        BattleEncounterMemoryRecorder.RecordBattleStarted(_battleScenarioData, global, _enemyId);

        if (_spriteRenderer != null)
        {
            Color original = _spriteRenderer.color;
            Color brightFlash = ResolveFlashColor(original, Color.white);
            Color hitFlash = ResolveFlashColor(
                original,
                new Color(1f, 0.35f, 0.35f, original.a));
            for (int i = 0; i < 3; i++)
            {
                _spriteRenderer.color = brightFlash;
                yield return new WaitForSecondsRealtime(0.06f);
                _spriteRenderer.color = hitFlash;
                yield return new WaitForSecondsRealtime(0.06f);
            }
            _spriteRenderer.color = original;
        }

        BattleRewardResult rewards = BattleRewardService.Grant(resolvedEnemies, global);
        BattleEncounterMemoryRecorder.RecordBattleResult(
            _battleScenarioData,
            null,
            global,
            _enemyId,
            true);

        if (global != null && InstantVictoryDefeatsPermanently)
            global.MarkOverworldEnemyDefeated(_enemyId, _sceneName);

        BattleResultUI resultUi = BattleResultUI.EnsureGlobal();
        if (resultUi != null)
            yield return resultUi.Show(rewards, true);

        _encounterInProgress = false;
        player?.CompletePreemptiveAttackWithoutBattle();

        EncounterCollisionGuard.BlockAll(_postBattleGraceDuration);
        _localEncounterBlockedUntil = Time.unscaledTime + Mathf.Max(0f, _postBattleGraceDuration);
        EncounterCollisionGuard.NudgePlayerOutOf(_collider, player, _postBattleNudgeDistance);

        if (InstantVictoryDefeatsPermanently)
        {
            DisablePermanently();
            yield break;
        }

        float cooldown = Mathf.Max(_postBattleGraceDuration, 0.75f);
        global?.MarkOverworldEnemyEscaped(_enemyId, _sceneName, cooldown, _postEscapeAlpha);
        StartCooldown(cooldown, _postEscapeAlpha);
    }

    private IEnumerator StartSceneBattleRoutine(PlayerController player)
    {
        yield return StartSceneBattleRoutine(player, false);
    }

    private IEnumerator StartSceneBattleRoutine(PlayerController player, bool isPreemptiveAttack)
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
        float entryDelay = isPreemptiveAttack ? 0f : _encounterDelay;
        s_globalEncounterLockUntil = Time.unscaledTime + Mathf.Max(0.75f, entryDelay + 0.5f);
        _rb.linearVelocity = Vector2.zero;
        UpdateMoveAnimation(Vector2.zero);
        bool disabledColliderForTransition = _useDedicatedBattleScene
            && _collider != null
            && _collider.enabled;
        if (disabledColliderForTransition)
            _collider.enabled = false;
        player.SetBattleMode(true);

        AudioManager.Instance?.PlaySFX(_encounterSFX);

        if (entryDelay > 0f)
            yield return new WaitForSecondsRealtime(entryDelay);

        bool started = BattleEncounterService.StartEncounter(
            player,
            resolvedEnemies,
            ResolveBattleBGM(resolvedEnemies),
            _useDedicatedBattleScene,
            _battleSceneName,
            _battleFadeDuration,
            _enemyId,
            DefeatsOnVictory,
            this,
            _battleScenarioData,
            isPreemptiveAttack);

        if (!started)
        {
            _encounterInProgress = false;
            _triggered = false;
            if (disabledColliderForTransition && _collider != null)
                _collider.enabled = true;
            player.SetBattleMode(false);
            yield break;
        }

        if (_destroyAfterTouch && _useDedicatedBattleScene)
            Destroy(gameObject);
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

    private bool HasRecordedEncounterVictory(GlobalDataManager global)
    {
        string memoryKey = EncounterMemoryKey;
        return global != null
            && global.TryGetEncounterMemory(memoryKey, out EncounterMemorySaveData memory)
            && memory.Defeated;
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
        OnEncounterResolved(
            victory ? BattleEncounterOutcome.Victory : BattleEncounterOutcome.Escaped,
            player);
    }

    public void OnEncounterResolved(BattleEncounterOutcome outcome, PlayerController player)
    {
        _encounterInProgress = false;
        _pendingRearmPlayer = player;
        EncounterCollisionGuard.BlockAll(_postBattleGraceDuration);
        _localEncounterBlockedUntil = Time.unscaledTime + Mathf.Max(0f, _postBattleGraceDuration);
        s_globalEncounterLockUntil = Mathf.Max(s_globalEncounterLockUntil, _localEncounterBlockedUntil);

        EncounterCollisionGuard.NudgePlayerOutOf(_collider, player, _postBattleNudgeDistance);
        _waitForPlayerExitBeforeRearm = EncounterCollisionGuard.IsPlayerOverlapping(_collider, player);

        if (outcome == BattleEncounterOutcome.Victory && DefeatsOnVictory)
        {
            DisablePermanently();
            return;
        }

        if (outcome == BattleEncounterOutcome.Escaped)
        {
            ResolveEscapeCooldown(out float duration, out float alpha);
            StartCooldown(duration, alpha);
            return;
        }

        RestoreActiveState();
    }

    private void ResolveEscapeCooldown(out float duration, out float alpha)
    {
        duration = Mathf.Max(_postBattleGraceDuration, 0.75f);
        alpha = Mathf.Clamp01(_postEscapeAlpha);

        GlobalDataManager global = GlobalDataManager.Instance;
        if (global == null)
            return;

        float remaining = global.GetOverworldEnemyCooldownRemaining(_enemyId);
        if (remaining <= 0f)
        {
            global.MarkOverworldEnemyEscaped(_enemyId, _sceneName, duration, alpha);
            return;
        }

        duration = remaining;
        if (global.TryGetOverworldEnemyState(_enemyId, out OverworldEnemyRuntimeState state))
            alpha = Mathf.Clamp01(state.CooldownAlpha);
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
