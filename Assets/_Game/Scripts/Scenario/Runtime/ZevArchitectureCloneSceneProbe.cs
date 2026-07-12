using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Test-scene-only probe for validating the ZEV architecture clone entry path in Play Mode.
/// Attach only to Assets/_Game/Scenes/Tests/ZEV_ArchitectureClone_TestScene.unity.
/// </summary>
public sealed class ZevArchitectureCloneSceneProbe : MonoBehaviour
{
    private const string LogPrefix = "[ZEV Clone Scene Probe]";
    private static readonly FieldInfo FallbackEnemiesField = typeof(DialogueBattleNPC).GetField(
        "_fallbackEncounterEnemies",
        BindingFlags.Instance | BindingFlags.NonPublic);
    private static readonly FieldInfo FallbackBgmField = typeof(DialogueBattleNPC).GetField(
        "_fallbackBattleBgm",
        BindingFlags.Instance | BindingFlags.NonPublic);
    private static readonly FieldInfo FallbackScenarioField = typeof(DialogueBattleNPC).GetField(
        "_fallbackBattleScenarioData",
        BindingFlags.Instance | BindingFlags.NonPublic);
    private static readonly FieldInfo UseDedicatedBattleSceneField = typeof(DialogueBattleNPC).GetField(
        "_useDedicatedBattleScene",
        BindingFlags.Instance | BindingFlags.NonPublic);
    private static readonly FieldInfo BattleSceneNameField = typeof(DialogueBattleNPC).GetField(
        "_battleSceneName",
        BindingFlags.Instance | BindingFlags.NonPublic);
    private static readonly FieldInfo BattleSceneFadeDurationField = typeof(DialogueBattleNPC).GetField(
        "_battleSceneFadeDuration",
        BindingFlags.Instance | BindingFlags.NonPublic);
    private static readonly FieldInfo BattleScenarioRuntimeField = typeof(BattleManager).GetField(
        "_battleScenarioRuntime",
        BindingFlags.Instance | BindingFlags.NonPublic);
    private static readonly FieldInfo BattleGameModuleActionRunnerField = typeof(BattleManager).GetField(
        "_battleGameModuleActionRunner",
        BindingFlags.Instance | BindingFlags.NonPublic);
    private static readonly FieldInfo BattleScenarioExecutionGateField = typeof(BattleManager).GetField(
        "_battleScenarioExecutionGate",
        BindingFlags.Instance | BindingFlags.NonPublic);
    private static readonly FieldInfo AcceptsTurnQteInputField = typeof(BattleUIController).GetField(
        "_acceptsTurnQteInput",
        BindingFlags.Instance | BindingFlags.NonPublic);
    private static readonly FieldInfo ScenarioCinematicModeField = typeof(BattleUIController).GetField(
        "_isScenarioCinematicMode",
        BindingFlags.Instance | BindingFlags.NonPublic);

    [SerializeField] private bool _autoStartEncounter = true;
    [SerializeField] private bool _autoTriggerPhaseTransition = true;
    [SerializeField] private string _expectedEnemyId = "zev_architecture_clone";
    [SerializeField] private string _probeEncounterId = "zev_architecture_clone_scene_probe";
    [SerializeField] private float _startupTimeoutSeconds = 2f;
    [SerializeField] private float _openingSequenceTimeoutSeconds = 30f;
    [SerializeField] private float _phaseTransitionTimeoutSeconds = 8f;

    private bool _probeRunning;
    private bool _phaseSequenceRunning;
    private int _phaseDialoguesObserved;

    private IEnumerator Start()
    {
        if (!_autoStartEncounter)
        {
            Debug.Log(LogPrefix + " Auto start disabled.");
            yield break;
        }

        yield return WaitForBootstrap();

        PlayerController player = FindFirstObjectByType<PlayerController>();
        DialogueBattleNPC npc = FindFirstObjectByType<DialogueBattleNPC>();
        if (player == null || npc == null)
        {
            Debug.LogError(LogPrefix + " Missing PlayerController or DialogueBattleNPC in test scene.");
            yield break;
        }

        List<EnemyData> enemies = ReadField<List<EnemyData>>(FallbackEnemiesField, npc);
        AudioClip battleBgm = ReadField<AudioClip>(FallbackBgmField, npc);
        BattleScenarioData scenario = ReadField<BattleScenarioData>(FallbackScenarioField, npc);
        bool useDedicatedBattleScene = ReadField<bool>(UseDedicatedBattleSceneField, npc);
        string battleSceneName = ReadField<string>(BattleSceneNameField, npc);
        float fadeDuration = ReadField<float>(BattleSceneFadeDurationField, npc);

        if (!ValidateCloneReferences(enemies, scenario))
        {
            yield break;
        }

        bool started = BattleEncounterService.StartEncounter(
            player,
            new List<EnemyData>(enemies),
            battleBgm,
            useDedicatedBattleScene,
            string.IsNullOrWhiteSpace(battleSceneName) ? "BattleScene" : battleSceneName,
            fadeDuration,
            _probeEncounterId,
            false,
            null,
            scenario);

        DontDestroyOnLoad(gameObject);

        if (!started)
        {
            Debug.LogError(LogPrefix + " BattleEncounterService.StartEncounter returned false.");
            yield break;
        }

        GlobalDataManager global = GlobalDataManager.Instance;
        bool pendingEnemyOk = global != null
            && global.PendingEnemies != null
            && global.PendingEnemies.Count == enemies.Count
            && global.PendingEnemies[0] == enemies[0];
        bool pendingScenarioOk = global != null && global.PendingBattleScenario == scenario;

        if (!pendingEnemyOk || !pendingScenarioOk)
        {
            Debug.LogError(LogPrefix + " Encounter pending data mismatch. enemyOk=" + pendingEnemyOk + " scenarioOk=" + pendingScenarioOk);
            yield break;
        }

        Debug.Log(LogPrefix + " PASS: clone prefab started encounter with scenario=" + scenario.ScenarioId + " enemy=" + enemies[0].EnemyId);

        _probeRunning = true;
        StartCoroutine(AutoCompleteDialogueWhileProbeRuns());
        yield return WaitForBattleSceneRuntime(enemies[0], scenario);
    }

    private IEnumerator WaitForBootstrap()
    {
        float deadline = Time.realtimeSinceStartup + Mathf.Max(0.1f, _startupTimeoutSeconds);
        while (Time.realtimeSinceStartup < deadline)
        {
            if (GlobalDataManager.Instance != null && GameStateManager.Instance != null)
            {
                yield break;
            }

            yield return null;
        }
    }

    private bool ValidateCloneReferences(List<EnemyData> enemies, BattleScenarioData scenario)
    {
        if (enemies == null || enemies.Count == 0 || enemies[0] == null)
        {
            Debug.LogError(LogPrefix + " Clone NPC has no fallback encounter enemy.");
            return false;
        }

        if (scenario == null)
        {
            Debug.LogError(LogPrefix + " Clone NPC has no fallback battle scenario.");
            return false;
        }

        if (enemies[0].EnemyId != _expectedEnemyId)
        {
            Debug.LogError(LogPrefix + " Unexpected enemy id. expected=" + _expectedEnemyId + " actual=" + enemies[0].EnemyId);
            return false;
        }

        if (scenario.OpeningModule != BattleTurnQteGameModuleRuntime.Id)
        {
            Debug.LogError(LogPrefix + " Unexpected opening module. actual=" + scenario.OpeningModule);
            return false;
        }

        return true;
    }

    private IEnumerator WaitForBattleSceneRuntime(EnemyData expectedEnemy, BattleScenarioData expectedScenario)
    {
        float deadline = Time.realtimeSinceStartup + Mathf.Max(1f, _startupTimeoutSeconds + 4f);
        BattleManager battleManager = null;
        BattleScenarioRuntime runtime = null;
        IGameModuleActionRunner moduleRunner = null;
        while (Time.realtimeSinceStartup < deadline)
        {
            battleManager = BattleManager.Instance;
            if (battleManager != null && SceneManager.GetActiveScene().name == "BattleScene")
            {
                runtime = ReadField<BattleScenarioRuntime>(BattleScenarioRuntimeField, battleManager);
                moduleRunner = ReadField<IGameModuleActionRunner>(BattleGameModuleActionRunnerField, battleManager);
                if (runtime != null && moduleRunner != null)
                {
                    break;
                }
            }

            yield return null;
        }

        if (battleManager == null)
        {
            Debug.LogError(LogPrefix + " BattleManager was not found after encounter scene transition.");
            yield break;
        }

        if (runtime == null || runtime.ScenarioData != expectedScenario)
        {
            string actual = runtime != null && runtime.ScenarioData != null ? runtime.ScenarioData.ScenarioId : "null";
            Debug.LogError(LogPrefix + " BattleManager scenario runtime mismatch. actual=" + actual);
            yield break;
        }

        if (moduleRunner == null || moduleRunner.CurrentModuleId != BattleTurnQteGameModuleRuntime.Id)
        {
            string actualModule = moduleRunner != null ? moduleRunner.CurrentModuleId : "null";
            Debug.LogError(LogPrefix + " Battle module runner mismatch. actual=" + actualModule);
            yield break;
        }

        EnemyCharacter battleEnemy = FindFirstObjectByType<EnemyCharacter>();
        if (battleEnemy == null || battleEnemy.Data != expectedEnemy)
        {
            string actualEnemy = battleEnemy != null && battleEnemy.Data != null ? battleEnemy.Data.EnemyId : "null";
            Debug.LogError(LogPrefix + " Battle enemy data mismatch. actual=" + actualEnemy);
            yield break;
        }

        Debug.Log(LogPrefix + " PASS: BattleScene received scenario runtime=" + runtime.ScenarioData.ScenarioId + " module=" + moduleRunner.CurrentModuleId);

        if (_autoTriggerPhaseTransition)
        {
            yield return TriggerPhaseTransition(battleManager, runtime, moduleRunner, battleEnemy);
        }

        Destroy(gameObject);
    }

    private IEnumerator TriggerPhaseTransition(
        BattleManager battleManager,
        BattleScenarioRuntime runtime,
        IGameModuleActionRunner moduleRunner,
        EnemyCharacter battleEnemy)
    {
        BattleScenarioExecutionGate gate = ReadField<BattleScenarioExecutionGate>(
            BattleScenarioExecutionGateField,
            battleManager);
        if (gate == null)
        {
            Debug.LogError(LogPrefix + " BattleScenarioExecutionGate was not found.");
            yield break;
        }

        yield return null;
        float openingDeadline = Time.realtimeSinceStartup + Mathf.Max(1f, _openingSequenceTimeoutSeconds);
        while (gate.IsExecuting
            || battleManager.CurrentState == BattleState.Init
            || (DialogueManager.Instance != null && DialogueManager.Instance.IsPlaying))
        {
            if (Time.realtimeSinceStartup > openingDeadline)
            {
                Debug.LogError(LogPrefix + " Opening scenario sequence timed out before phase transition. state=" + battleManager.CurrentState);
                yield break;
            }

            yield return null;
        }

        int previousHp = battleEnemy.CurrentHP;
        int targetHp = Mathf.FloorToInt(battleEnemy.MaxHP * 0.4f);
        int damage = Mathf.Max(0, previousHp - targetHp);
        if (damage > 0)
        {
            battleEnemy.TakePureDamage(damage);
        }

        gate.PublishEnemyHpCrossedBelow(
            _expectedEnemyId,
            previousHp,
            battleEnemy.CurrentHP,
            battleEnemy.MaxHP,
            BattleRuleTiming.AfterCurrentSkill);

        _phaseDialoguesObserved = 0;
        _phaseSequenceRunning = true;
        IEnumerator flushRoutine = gate.Flush(BattleRuleTiming.AfterCurrentSkill);
        float deadline = Time.realtimeSinceStartup + Mathf.Max(1f, _phaseTransitionTimeoutSeconds);
        while (flushRoutine.MoveNext())
        {
            if (Time.realtimeSinceStartup > deadline)
            {
                _phaseSequenceRunning = false;
                Debug.LogError(LogPrefix + " Phase transition sequence timed out.");
                yield break;
            }

            yield return flushRoutine.Current;
        }

        _phaseSequenceRunning = false;

        if (gate.LastHandle == null || gate.LastHandle.Status == ActionExecutionStatus.Failed || gate.LastHandle.Status == ActionExecutionStatus.Canceled)
        {
            string status = gate.LastHandle != null ? gate.LastHandle.Status.ToString() : "null";
            string message = gate.LastHandle != null && gate.LastHandle.Result != null ? gate.LastHandle.Result.Message : string.Empty;
            Debug.LogError(LogPrefix + " Phase transition sequence failed. status=" + status + " message=" + message);
            yield break;
        }

        if (moduleRunner.CurrentModuleId != BattleTurnQteGameModuleRuntime.Id)
        {
            Debug.LogError(LogPrefix + " Phase transition left the playable turn_qte module. actual=" + moduleRunner.CurrentModuleId);
            yield break;
        }

        BattleUIController battleUi = BattleUIController.Instance;
        bool acceptsTurnQteInput = battleUi != null
            && AcceptsTurnQteInputField != null
            && (bool)AcceptsTurnQteInputField.GetValue(battleUi);
        if (!acceptsTurnQteInput)
        {
            Debug.LogError(LogPrefix + " Phase transition did not restore turn_qte input/UI ownership.");
            yield break;
        }

        bool cinematicModeActive = battleUi != null
            && ScenarioCinematicModeField != null
            && (bool)ScenarioCinematicModeField.GetValue(battleUi);
        if (cinematicModeActive)
        {
            Debug.LogError(LogPrefix + " Phase transition left scenario cinematic mode active.");
            yield break;
        }

        CameraController cameraController = CameraController.Instance;
        PositionManager positionManager = PositionManager.Instance;
        bool cameraCentered = cameraController != null
            && cameraController.VirtualCamera != null
            && positionManager != null
            && cameraController.VirtualCamera.Follow == positionManager.CenterTransform;
        if (!cameraCentered)
        {
            Debug.LogError(LogPrefix + " Phase transition did not restore the camera to battle center.");
            yield break;
        }

        if (_phaseDialoguesObserved < 2)
        {
            Debug.LogError(LogPrefix + " Phase transition did not start all authored dialogues. observed=" + _phaseDialoguesObserved);
            yield break;
        }

        string flagValue = string.Empty;
        bool flagOk = runtime.SessionState != null
            && runtime.SessionState.TryGetFlagValue("zev.clone.phase", out flagValue)
            && flagValue == "shooter";
        if (!flagOk)
        {
            Debug.LogError(LogPrefix + " Phase transition did not set battle flag zev.clone.phase=shooter.");
            yield break;
        }

        Debug.Log(LogPrefix + " PASS: HP threshold sequence restored gameplay module=" + moduleRunner.CurrentModuleId + " dialogues=" + _phaseDialoguesObserved + " input=true camera=center flag=zev.clone.phase:" + flagValue);
    }

    private IEnumerator AutoCompleteDialogueWhileProbeRuns()
    {
        while (_probeRunning)
        {
            DialogueManager dialogueManager = DialogueManager.Instance;
            if (dialogueManager != null && dialogueManager.IsPlaying)
            {
                if (_phaseSequenceRunning)
                {
                    _phaseDialoguesObserved++;
                }

                yield return null;
                dialogueManager.EndDialogue();
            }

            yield return null;
        }
    }

    private void OnDestroy()
    {
        _probeRunning = false;
        _phaseSequenceRunning = false;
    }

    private static T ReadField<T>(FieldInfo field, DialogueBattleNPC source)
    {
        if (field == null || source == null)
        {
            return default;
        }

        object value = field.GetValue(source);
        if (value is T typedValue)
        {
            return typedValue;
        }

        return default;
    }

    private static T ReadField<T>(FieldInfo field, BattleManager source)
    {
        if (field == null || source == null)
        {
            return default;
        }

        object value = field.GetValue(source);
        if (value is T typedValue)
        {
            return typedValue;
        }

        return default;
    }
}
