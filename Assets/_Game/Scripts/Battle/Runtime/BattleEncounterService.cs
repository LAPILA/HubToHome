using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public interface IEncounterSource
{
    void OnEncounterResolved(bool victory, PlayerController player);
}

public static class EncounterCollisionGuard
{
    private const float NudgePadding = 0.18f;
    private static float s_globalBlockedUntil;

    public static bool IsGloballyBlocked => Time.unscaledTime < s_globalBlockedUntil;

    public static void BlockAll(float seconds)
    {
        s_globalBlockedUntil = Mathf.Max(s_globalBlockedUntil, Time.unscaledTime + Mathf.Max(0f, seconds));
    }

    public static bool IsPlayerOverlapping(Collider2D sourceCollider, PlayerController player)
    {
        if (sourceCollider == null || player == null) return false;

        Collider2D[] playerColliders = player.GetComponents<Collider2D>();
        for (int i = 0; i < playerColliders.Length; i++)
        {
            Collider2D playerCollider = playerColliders[i];
            if (playerCollider == null || !playerCollider.enabled) continue;

            ColliderDistance2D distance = Physics2D.Distance(sourceCollider, playerCollider);
            if (distance.isOverlapped)
                return true;
        }

        return false;
    }

    public static void NudgePlayerOutOf(Collider2D sourceCollider, PlayerController player, float minDistance)
    {
        if (sourceCollider == null || player == null) return;

        Vector2 sourceCenter = sourceCollider.bounds.center;
        Vector2 playerCenter = player.transform.position;
        Vector2 direction = playerCenter - sourceCenter;
        if (direction.sqrMagnitude < 0.0001f)
            direction = player.GetFacingVector2();
        if (direction.sqrMagnitude < 0.0001f)
            direction = Vector2.down;

        float distance = Mathf.Max(0.25f, minDistance);
        Collider2D[] playerColliders = player.GetComponents<Collider2D>();
        for (int i = 0; i < playerColliders.Length; i++)
        {
            Collider2D playerCollider = playerColliders[i];
            if (playerCollider == null || !playerCollider.enabled) continue;

            ColliderDistance2D colliderDistance = Physics2D.Distance(sourceCollider, playerCollider);
            if (colliderDistance.isOverlapped)
                distance = Mathf.Max(distance, Mathf.Abs(colliderDistance.distance) + NudgePadding + minDistance);
        }

        player.NudgeFromEncounter(direction.normalized, distance);
        Physics2D.SyncTransforms();
    }
}

/// <summary>
/// 오버월드/이벤트/대화 등 다양한 진입점에서 전투 시작 준비를 공통 처리합니다.
/// 실제 전투 유닛 생성은 기존 BattleManager + Enemy_Base prefab 파이프라인을 그대로 사용합니다.
/// </summary>
public static class BattleEncounterService
{
    private static int s_activeRequestId;
    private static int s_nextRequestId;

    public static AudioClip ResolveBattleBgm(List<EnemyData> enemies, AudioClip overrideBattleBgm = null)
    {
        if (overrideBattleBgm != null) return overrideBattleBgm;

        if (enemies != null)
        {
            for (int i = 0; i < enemies.Count; i++)
            {
                if (enemies[i] != null && enemies[i].BattleBGM != null)
                    return enemies[i].BattleBGM;
            }
        }

        return MapSettings.CurrentDefaultBattleBGM;
    }

    public static bool StartEncounter(
        PlayerController player,
        List<EnemyData> encounterEnemies,
        AudioClip overrideBattleBgm = null,
        bool useDedicatedBattleScene = false,
        string battleSceneName = SceneName.Battle,
        float battleSceneFadeDuration = 0.08f,
        string encounterId = null,
        bool defeatsOnVictory = false,
        IEncounterSource encounterSource = null,
        BattleScenarioData battleScenarioData = null,
        bool playerPreemptiveAttack = false)
    {
        if (player == null)
        {
            Debug.LogWarning("[BattleEncounterService] PlayerController가 없어 전투를 시작할 수 없습니다.");
            return false;
        }

        if (encounterEnemies == null || encounterEnemies.Count == 0)
        {
            Debug.LogWarning("[BattleEncounterService] EncounterEnemies가 비어있어 전투를 시작할 수 없습니다.");
            return false;
        }

        for (int i = 0; i < encounterEnemies.Count; i++)
        {
            if (encounterEnemies[i] != null) continue;
            Debug.LogWarning($"[BattleEncounterService] EncounterEnemies[{i}]가 비어 있어 전투를 시작할 수 없습니다.");
            return false;
        }

        GlobalDataManager global = GlobalDataManager.Instance;
        if (global == null)
        {
            Debug.LogWarning("[BattleEncounterService] GlobalDataManager가 없어 전투를 시작할 수 없습니다.");
            return false;
        }

        BattleManager seamlessManager = useDedicatedBattleScene ? null : BattleManager.Instance;
        bool useSeamlessBattle = seamlessManager != null;

        if (useSeamlessBattle && !seamlessManager.CanStartSeamlessBattle(encounterEnemies, player, out string seamlessError))
        {
            Debug.LogWarning($"[BattleEncounterService] 심리스 전투 구성이 올바르지 않습니다: {seamlessError}", seamlessManager);
            return false;
        }

        SceneLoader sceneLoader = useSeamlessBattle ? null : SceneLoader.Instance;
        if (!useSeamlessBattle && sceneLoader == null)
        {
            Debug.LogWarning("[BattleEncounterService] 심리스 BattleManager와 SceneLoader가 모두 없어 전투를 시작할 수 없습니다.");
            return false;
        }

        if (!TryAcquireRequest(out int requestId))
        {
            Debug.LogWarning("[BattleEncounterService] 다른 전투 진입 요청을 처리 중입니다.");
            return false;
        }

        GameStateManager gameStateManager = GameStateManager.Instance;
        var transaction = new EncounterStartTransaction(
            requestId,
            global,
            player,
            gameStateManager);

        try
        {
            PrepareEncounterContext(
                global,
                player,
                gameStateManager,
                encounterEnemies,
                overrideBattleBgm,
                encounterId,
                defeatsOnVictory,
                battleScenarioData,
                playerPreemptiveAttack);

            if (useSeamlessBattle)
            {
                seamlessManager.SetBattleScenarioData(battleScenarioData);
                if (seamlessManager.TryStartSeamlessBattle(encounterEnemies, player, encounterSource, out string startError))
                {
                    transaction.Commit();
                    return true;
                }

                Debug.LogWarning($"[BattleEncounterService] 심리스 전투 시작에 실패했습니다: {startError}", seamlessManager);
                transaction.Rollback();
                return false;
            }

            string resolvedBattleScene = string.IsNullOrWhiteSpace(battleSceneName)
                ? SceneName.Battle
                : battleSceneName.Trim();
            SceneLoadOperation operation = sceneLoader.LoadSceneWithResult(
                resolvedBattleScene,
                battleSceneFadeDuration,
                result => CompleteDedicatedRequest(transaction, result));

            if (operation == null)
            {
                Debug.LogWarning("[BattleEncounterService] SceneLoader가 전투 씬 로드 작업을 만들지 못했습니다.");
                transaction.Rollback();
                return false;
            }

            bool accepted = !operation.IsDone
                || SceneLoadResultUtility.WasDestinationActivated(operation.Result);
            if (!accepted)
                transaction.Rollback();

            return accepted;
        }
        catch (Exception exception)
        {
            Debug.LogWarning("[BattleEncounterService] 전투 진입 중 예외가 발생해 이전 상태로 복구합니다.");
            Debug.LogException(exception);
            transaction.Rollback();
            return false;
        }
    }

    private static void CompleteDedicatedRequest(
        EncounterStartTransaction transaction,
        SceneLoadResult result)
    {
        if (SceneLoadResultUtility.WasDestinationActivated(result))
        {
            transaction.Commit();
            if (result != SceneLoadResult.Succeeded)
            {
                Debug.LogError(
                    $"[BattleEncounterService] 전투 Scene은 활성화됐지만 준비에 실패했습니다. Result={result}");
            }
            return;
        }

        Debug.LogWarning($"[BattleEncounterService] 전투 씬 진입에 실패했습니다. Result={result}");
        transaction.Rollback();
    }

    private static bool TryAcquireRequest(out int requestId)
    {
        if (s_activeRequestId != 0)
        {
            requestId = 0;
            return false;
        }

        if (s_nextRequestId == int.MaxValue)
            s_nextRequestId = 0;

        requestId = ++s_nextRequestId;
        s_activeRequestId = requestId;
        return true;
    }

    private static void ReleaseRequest(int requestId)
    {
        if (s_activeRequestId == requestId)
            s_activeRequestId = 0;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetRequestGate()
    {
        s_activeRequestId = 0;
        s_nextRequestId = 0;
    }

    private static void PrepareEncounterContext(
        GlobalDataManager global,
        PlayerController player,
        GameStateManager gameStateManager,
        List<EnemyData> encounterEnemies,
        AudioClip overrideBattleBgm,
        string encounterId,
        bool defeatsOnVictory,
        BattleScenarioData battleScenarioData,
        bool playerPreemptiveAttack)
    {
        global.LastOverworldScene = SceneManager.GetActiveScene().name;
        global.PendingEnemies = new List<EnemyData>(encounterEnemies);
        global.PendingBattleBGM = ResolveBattleBgm(encounterEnemies, overrideBattleBgm);
        global.PendingBattleScenario = battleScenarioData;
        global.BeginOverworldEnemyEncounter(
            encounterId,
            global.LastOverworldScene,
            defeatsOnVictory,
            playerPreemptiveAttack);

        player.SetBattleMode(true);
        player.SavePositionToGlobal();
        gameStateManager?.ChangeState(GameState.Battle);
    }

    private sealed class EncounterStartTransaction
    {
        private readonly int _requestId;
        private readonly GlobalDataManager _global;
        private readonly PlayerController _player;
        private readonly GameStateManager _gameStateManager;
        private readonly List<EnemyData> _pendingEnemies;
        private readonly AudioClip _pendingBattleBgm;
        private readonly BattleScenarioData _pendingBattleScenario;
        private readonly string _lastOverworldScene;
        private readonly float _spawnX;
        private readonly float _spawnY;
        private readonly int _lookingDirection;
        private readonly string _encounterId;
        private readonly bool _defeatsOnVictory;
        private readonly bool _playerPreemptiveAttack;
        private readonly bool _playerWasInBattle;
        private readonly GameState _gameState;
        private readonly float _timeScale;
        private bool _isCompleted;

        public EncounterStartTransaction(
            int requestId,
            GlobalDataManager global,
            PlayerController player,
            GameStateManager gameStateManager)
        {
            _requestId = requestId;
            _global = global;
            _player = player;
            _gameStateManager = gameStateManager;

            _pendingEnemies = global.PendingEnemies == null
                ? null
                : new List<EnemyData>(global.PendingEnemies);
            _pendingBattleBgm = global.PendingBattleBGM;
            _pendingBattleScenario = global.PendingBattleScenario;
            _lastOverworldScene = global.LastOverworldScene;
            _spawnX = global.SpawnX;
            _spawnY = global.SpawnY;
            _lookingDirection = global.LookingDir;
            _encounterId = global.CurrentEncounterEnemyId;
            _defeatsOnVictory = global.CurrentEncounterDefeatsOnVictory;
            _playerPreemptiveAttack = global.CurrentEncounterPlayerPreemptiveAttack;
            _playerWasInBattle = player.State == PlayerController.PlayerState.InBattle;
            _gameState = gameStateManager != null
                ? gameStateManager.CurrentState
                : GameState.Exploration;
            _timeScale = Time.timeScale;
        }

        public void Commit()
        {
            if (_isCompleted)
                return;

            _isCompleted = true;
            ReleaseRequest(_requestId);
        }

        public void Rollback()
        {
            if (_isCompleted)
                return;

            _isCompleted = true;
            try
            {
                RunRollbackStep(RestoreGlobalContext, "global encounter context");
                RunRollbackStep(RestorePlayerMode, "player battle mode");
                RunRollbackStep(() => Time.timeScale = _timeScale, "time scale");
                RunRollbackStep(RestoreGameState, "game state");
            }
            finally
            {
                ReleaseRequest(_requestId);
            }
        }

        private void RestoreGlobalContext()
        {
            if (_global == null)
                return;

            _global.PendingEnemies = _pendingEnemies == null
                ? null
                : new List<EnemyData>(_pendingEnemies);
            _global.PendingBattleBGM = _pendingBattleBgm;
            _global.PendingBattleScenario = _pendingBattleScenario;
            _global.LastOverworldScene = _lastOverworldScene;
            _global.SpawnX = _spawnX;
            _global.SpawnY = _spawnY;
            _global.LookingDir = _lookingDirection;

            if (string.IsNullOrEmpty(_encounterId))
            {
                _global.EndOverworldEnemyEncounterContext();
                return;
            }

            _global.BeginOverworldEnemyEncounter(
                _encounterId,
                _lastOverworldScene,
                _defeatsOnVictory,
                _playerPreemptiveAttack);
        }

        private void RestorePlayerMode()
        {
            if (_player != null)
                _player.SetBattleMode(_playerWasInBattle);
        }

        private void RestoreGameState()
        {
            if (_gameStateManager != null)
                _gameStateManager.ChangeState(_gameState);
        }

        private static void RunRollbackStep(Action action, string stepName)
        {
            try
            {
                action?.Invoke();
            }
            catch (Exception exception)
            {
                Debug.LogError($"[BattleEncounterService] Failed to restore {stepName}: {exception.Message}");
                Debug.LogException(exception);
            }
        }
    }
}
