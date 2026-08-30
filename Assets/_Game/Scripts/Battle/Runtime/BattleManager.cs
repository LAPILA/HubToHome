using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;
using Unity.Cinemachine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using UnityEngine.Serialization;
using Sirenix.OdinInspector;

/// <summary>
/// 전투의 전체 흐름을 제어하는 중앙 매니저 (Singleton & State Machine 기반).
/// 옵저버(Observer) 패턴을 활용하여 UI와의 결합도를 낮췄습니다.
/// </summary>
public class BattleManager : MonoBehaviour, ISceneRevealGate, IBattleParticipantCommandHost, IBattleCinematicHost, IBattleTurnQteHost, IActionSequenceLiveContextSource
{
    public static BattleManager Instance { get; private set; }

    #region [ Events ]
    public event Action<BattleState>                OnStateChanged;
    public event Action<List<PlayerCharacter>, List<EnemyCharacter>> OnBattleStarted;
    public event Action<List<PlayerCharacter>>      OnPlayerPartyChanged;
    public event Action<List<CharacterBase>>        OnTurnQueueUpdated;  
    public event Action<PlayerCharacter>            OnPlayerTurnStarted;  
    public event Action<EnemyCharacter, EnemyAttackType> OnEnemyActionStarted;
    public event Action<CharacterBase, int, bool>   OnDamageDealt;        
    public event Action<BattleDamageFeedback>      OnDamageFeedbackRequested;
    public event Action<PlayerCharacter, int>       OnAPChanged;

    [Obsolete("Use OnAPChanged.")]
    public event Action<PlayerCharacter, int> OnMPChanged
    {
        add => OnAPChanged += value;
        remove => OnAPChanged -= value;
    }
    public event Action<bool>                       OnBattleEnded;
    public event Action<BattleRewardResult>         OnBattleRewardsGranted;
    public event Action<PlayerMenuAction>           OnTargetSelectionStarted;
    public event Action<BattleNarrationMessage>     OnBattleNarrationRequested;
    public event Action<List<BattleScenarioTrigger>> OnBattleScenarioTriggersReady;
    #endregion

    #region [ Inspector Settings ]
    [BoxGroup("Battle Units"), LabelWidth(140)] 
    public List<PlayerCharacter> _playerParty = new List<PlayerCharacter>();
    
    [BoxGroup("Battle Units"), LabelWidth(140)] 
    public List<EnemyCharacter> _enemies = new List<EnemyCharacter>();

    [BoxGroup("Camera Settings"), LabelWidth(140)] 
    public CinemachineImpulseSource _impulseSource;
    [BoxGroup("Camera Settings"), LabelWidth(140)] 
    public float _hitImpulse = 0.15f;

    [FormerlySerializedAs("_mpPerTurn")]
    [BoxGroup("System Rules"), LabelWidth(140)]
    [Tooltip("턴 시작 시 회복되는 AP입니다.")]
    public int _apPerTurn = 5;

    [FormerlySerializedAs("_mpOnParryPerfect")]
    [BoxGroup("System Rules"), LabelWidth(140)]
    [Tooltip("퍼펙트 패링 성공 시 회복되는 AP입니다.")]
    public int _apOnParryPerfect = 20;
    [BoxGroup("System Rules"), LabelWidth(140)] [Tooltip("우측 상단에 표시될 턴 대기열 아이콘의 최대 개수")]
    [SerializeField] private int _maxTurnQueueSize = 8;
    [BoxGroup("System Rules"), LabelWidth(140)] [Tooltip("실제로 UI에 노출할 턴 대기열 아이콘 수")]
    [SerializeField] private int _visibleTurnQueueSize = 4;
    [BoxGroup("System Rules"), LabelWidth(140)] [Tooltip("도망 기본 성공 확률")]
    [SerializeField, Range(0f, 1f)] private float _runSuccessChance = 0.6f;

    [Header("Seamless & Scene Settings")]
    [Tooltip("체크 시 전용 배틀 씬으로 동작하며, Start()에서 자동으로 전투 셋업을 시작합니다.")]
    [SerializeField] private bool _isDedicatedBattleScene = false;
    [Tooltip("전투 종료 후 돌아갈 씬이 없을 경우 이동할 기본 씬")]
    [SerializeField] private string _fallbackSceneName = "LobbyScene";
    [SerializeField] private float _postRunEnemyDisableDuration = 3f;
    [SerializeField] private float _postRunEnemyAlpha = 0.5f;
    [Tooltip("심리스 전투 종료 후 이전 맵 BGM으로 돌아가는 페이드 시간")]
    [SerializeField, Min(0f)] private float _seamlessBgmRestoreFadeDuration = 0.6f;
    
    [SerializeField] private GameObject _battleUICanvas;
    [SerializeField] private GameObject _enemyBasePrefab;
    [SerializeField] private GameObject _playerBasePrefab;

    [Header("Action Offsets (Hardcoding Removed)")]
    [Tooltip("근접 공격 시 적 앞으로 이동할 오프셋 위치")]
    [SerializeField] private Vector3 _meleeAttackOffset = new Vector3(-1.8f, 0, 0);
    [Tooltip("근접 공격 직전 뒤로 살짝 당기는 연출 오프셋")]
    [SerializeField] private Vector3 _meleePullbackOffset = new Vector3(-0.5f, 0, 0);

    [Header("Enemy Action Timing")]
    [Tooltip("적이 공격 애니메이션을 시작한 뒤 실제 방어 QTE 판정이 유지되는 시간")]
    [SerializeField] private float _enemyDefenseQTEWindow = 0.8f;
    [Tooltip("적 공격 애니메이션을 한 번만 보여주고 BattleIdle로 되돌리기까지의 시간")]
    [SerializeField] private float _enemyAttackVisualDuration = 0.18f;
    [Tooltip("플레이어 기본공격이 실제 데미지를 적용하기까지의 시간")]
    [SerializeField] private float _playerAttackHitDelay = 0.03f;
    [Tooltip("플레이어 기본공격 히트 후 복귀 시작까지의 시간")]
    [SerializeField] private float _playerAttackRecoverDelay = 0.14f;
    [Tooltip("적 공격 판정 후 적이 복귀를 시작하기까지의 시간")]
    [SerializeField] private float _enemyPostHitDelay = 0.18f;
    [Tooltip("광역 공격의 판정 전 준비 시간")]
    [SerializeField] private float _enemyAoEWindup = 0.35f;
    [Tooltip("적 단일공격이 시작되기 전에 화면 중앙으로 이동하는 시간")]
    [SerializeField] private float _enemyCenterAdvanceDuration = 0.18f;
    [Header("Narration")]
    [SerializeField] private BattleNarrationConfig _battleNarrationConfig;
    [Header("Scenario")]
    [SerializeField] private BattleScenarioData _defaultBattleScenarioData;
    #endregion

    #region [ Internal State ]
    public BattleState CurrentState { get; private set; } = BattleState.Init;
    
    private readonly Dictionary<CharacterBase, int> _actorForegroundSortingOrderCache = new Dictionary<CharacterBase, int>();
    private readonly Dictionary<CharacterBase, bool> _actorDefaultFlipXCache = new Dictionary<CharacterBase, bool>();
    public SkillData CurrentPendingSkill { get; private set; }
    public ItemData  CurrentPendingItem  { get; private set; }

    private readonly List<CharacterBase> _turnQueue = new List<CharacterBase>();
    private readonly List<PlayerCharacter> _seamlessSpawnedPlayers = new List<PlayerCharacter>();
    private readonly List<PlayerCharacter> _reserveParty = new List<PlayerCharacter>();
    private readonly List<PlayerCharacter> _battlePartyRoster = new List<PlayerCharacter>();
    private const int ActivePartyLimit = 3;
    private const int BattleRosterLimit = 6;
    private int _currentActorIndex = 0;

    // 캐싱된 대기 시간 (가비지 최적화)
    private readonly WaitForSeconds _waitShort  = new WaitForSeconds(0.4f);
    private readonly WaitForSeconds _waitMedium = new WaitForSeconds(0.8f);

    private PlayerCharacter _pendingActor;
    private PlayerMenuAction _pendingAction;
    private int _battleTurnCounter = 0;
    private readonly Dictionary<EnemyCharacter, BattleQueuedEnemyAction> _reservedEnemyActionByActor = new Dictionary<EnemyCharacter, BattleQueuedEnemyAction>();
    private bool _isRunInProgress;
    private bool _allowEscape = true;
    private bool _isAbortCleanupInProgress;
    private bool _isBattleEnding;
    private bool _isBattleActive;
    private bool _isPartyWaveTransitioning;
    private Coroutine _partyWaveTransitionCoroutine;
    private int _partyWaveTransitionVersion;
    private bool _rewardCommitted;
    private bool _playerPreemptiveAttackAvailable;
    private BattleRewardResult _lastRewardResult;
    private CameraDefaultTargetSnapshot _seamlessCameraDefaultTarget;
    private BgmPlaybackSnapshot _seamlessBgmSnapshot;
    private bool _hasSeamlessBgmSnapshot;
    private IEncounterSource _activeEncounterSource;
    private PlayerController _activeEncounterPlayer;
    private bool _isReadyToReveal = true;
    private BattleScenarioData _pendingBattleScenarioData;
    private BattleScenarioRuntime _battleScenarioRuntime;
    private BattleScenarioExecutionGate _battleScenarioExecutionGate;
    private IBattleParticipantIdRegistry _battleParticipantIdRegistry;
    private readonly HashSet<CharacterBase> _scenarioDefeatPublished = new HashSet<CharacterBase>();
    private IGameModuleActionRunner _battleGameModuleActionRunner;
    private IBattleParticipantCommandRunner _battleParticipantCommandRunner;
    private IBattleCinematicRunner _battleCinematicRunner;
    private IBattleTweenCinematicService _battleTweenCinematicService;
    private IBattleTurnQteModuleController _turnQteModuleController;
    private IBattleAimShooterModuleController _aimShooterModuleController;
    #endregion

    public bool IsReadyToReveal => !_isDedicatedBattleScene || _isReadyToReveal;

    public bool IsSeamlessBattleActive => !_isDedicatedBattleScene && (_isBattleActive || _isBattleEnding);

    public bool AllowEscape => _allowEscape;

    public IBattleAimShooterModuleController AimShooterModuleController => _aimShooterModuleController;

    public int LiveContextPriority => 100;

    public string LiveContextLabel => "현재 BattleManager";

    public bool TryCreateLiveContext(
        BattleScenarioData requestedScenario,
        ActionSequenceAsset sequence,
        out ActionDirector director,
        out ActionExecutionContext context,
        out string error)
    {
        director = null;
        context = null;
        if (requestedScenario == null || sequence == null
            || requestedScenario.Sequences == null
            || !requestedScenario.Sequences.Contains(sequence))
        {
            error = string.Empty;
            return false;
        }
        if (!Application.isPlaying)
        {
            error = "Play Mode에서만 Battle 시퀀스를 실동작 테스트할 수 있습니다.";
            return false;
        }
        if (_battleScenarioRuntime == null || !_battleScenarioRuntime.HasScenario)
        {
            error = "현재 BattleManager에 실행 중인 Battle Scenario가 없습니다.";
            return false;
        }
        if (requestedScenario != null
            && _battleScenarioRuntime.ScenarioData != requestedScenario)
        {
            error = "현재 전투의 Battle Scenario와 Sequence Maker 대상이 다릅니다.";
            return false;
        }

        BattleScenarioData activeScenario = _battleScenarioRuntime.ScenarioData;
        if (activeScenario.Sequences == null
            || !activeScenario.Sequences.Contains(sequence))
        {
            error = "선택한 Action Sequence가 현재 전투 Scenario에 속하지 않습니다.";
            return false;
        }

        director = CreateBattleScenarioActionDirector();
        context = CreateBattleScenarioActionContext();
        error = string.Empty;
        return true;
    }

    public void SetBattleScenarioData(BattleScenarioData scenarioData)
    {
        _pendingBattleScenarioData = scenarioData;
    }

    private void BroadcastVisibleTurnQueue()
    {
        List<CharacterBase> visibleQueue = BattleTurnQueueProjection.BuildVisible(
            _turnQueue,
            _currentActorIndex,
            _visibleTurnQueueSize,
            _playerParty,
            _enemies);
        OnTurnQueueUpdated?.Invoke(visibleQueue);
    }

    private IEnumerator WaitForNarrationToFinish()
    {
        BattleUIController ui = BattleUIController.Instance;

        while ((ui != null && ui.IsNarrationBlockingInput()) || IsAnyBattleSpeechShowing())
            yield return null;
    }

    private bool IsAnyBattleSpeechShowing()
    {
        for (int i = 0; i < _playerParty.Count; i++)
        {
            PlayerCharacter player = _playerParty[i];
            if (player != null && player.IsBattleSpeechShowing())
                return true;
        }

        for (int i = 0; i < _enemies.Count; i++)
        {
            EnemyCharacter enemy = _enemies[i];
            if (enemy != null && enemy.IsBattleSpeechShowing())
                return true;
        }

        return false;
    }

    private IEnumerator WarmupBattlePresentation()
    {
        if (_battleUICanvas != null)
            _battleUICanvas.SetActive(true);

        BattleUIController.Instance?.NormalizeForCurrentResolution();
        Canvas.ForceUpdateCanvases();

        CameraController cameraController = CameraController.Instance;
        if (cameraController != null && PositionManager.Instance != null)
        {
            cameraController.SetDefaultTarget(PositionManager.Instance.CenterTransform, true);
        }
        cameraController?.ResetCamera(0f);

        yield return null;
        yield return new WaitForEndOfFrame();

        Canvas.ForceUpdateCanvases();
        BattleUIController.Instance?.NormalizeForCurrentResolution();
        cameraController?.ResetCamera(0f);

        if (_battleUICanvas != null && _battleUICanvas.TryGetComponent(out RectTransform battleUiRect))
            LayoutRebuilder.ForceRebuildLayoutImmediate(battleUiRect);
    }

    public void RequestNarration(BattleNarrationMessage message)
    {
        if (_isBattleEnding && message.Priority != BattleNarrationPriority.Critical)
            return;

        OnBattleNarrationRequested?.Invoke(message);
    }

    private void TryRequestFlavorNarration()
    {
        if (_battleNarrationConfig == null || _battleNarrationConfig.FlavorRules == null) return;

        for (int i = 0; i < _battleNarrationConfig.FlavorRules.Count; i++)
        {
            BattleFlavorRule rule = _battleNarrationConfig.FlavorRules[i];
            if (rule == null || rule.TriggeredOnce) continue;

            EnemyCharacter focusEnemy = _enemies.Find(e => e != null && e.IsAlive && (string.IsNullOrWhiteSpace(rule.EnemyNameFilter) || (e.Data != null && e.Data.EnemyName == rule.EnemyNameFilter)));
            if (!IsFlavorRuleSatisfied(rule, focusEnemy)) continue;

            string enemyName = focusEnemy != null && focusEnemy.Data != null ? focusEnemy.Data.EnemyName : "적";
            string text = rule.Template.Replace("{enemy}", enemyName).Replace("{turn}", _battleTurnCounter.ToString());
            RequestNarration(BattleNarrationFormatter.Flavor(text, rule.Style, rule.Priority, rule.HoldOverride));
            rule.TriggeredOnce = true;
        }
    }

    private bool IsFlavorRuleSatisfied(BattleFlavorRule rule, EnemyCharacter focusEnemy)
    {
        return rule.TriggerType switch
        {
            BattleFlavorTriggerType.BattleStart => _battleTurnCounter <= 1,
            BattleFlavorTriggerType.TurnCountAtLeast => _battleTurnCounter >= Mathf.Max(1, rule.MinTurnCount),
            BattleFlavorTriggerType.EnemyHpBelowPercent => focusEnemy != null && focusEnemy.MaxHP > 0 && ((float)focusEnemy.CurrentHP / focusEnemy.MaxHP) <= rule.EnemyHpBelowPercent,
            _ => false
        };
    }

    private void InitializeBattleScenarioRuntime()
    {
        BattleScenarioData scenarioData = ResolveBattleScenarioData();
        _pendingBattleScenarioData = null;

        GlobalDataManager global = GlobalDataManager.Instance;
        string fallbackEncounterId = global != null ? global.CurrentEncounterEnemyId : null;
        _allowEscape = global == null || global.CurrentEncounterAllowsEscape;

        if (global != null)
        {
            global.PendingBattleScenario = null;
        }

        BattleEncounterMemoryRecorder.RecordBattleStarted(scenarioData, global, fallbackEncounterId);
        _battleScenarioRuntime = BattleEncounterMemoryRecorder.CreateRuntime(scenarioData, global, fallbackEncounterId);
        _battleParticipantIdRegistry = new BattleParticipantIdRegistry();
        BattleScenarioSubjectResolver.SetRegistry(_battleParticipantIdRegistry);
        _scenarioDefeatPublished.Clear();
        _battleParticipantCommandRunner = new BattleParticipantCommandService(this);
        _battleTweenCinematicService = new BattleTweenCinematicService(this);
        _battleCinematicRunner = new BattleCinematicService(this, _battleTweenCinematicService);
        _turnQteModuleController = new BattleTurnQteModuleControllerService(this);
        _aimShooterModuleController = new BattleAimShooterModuleController(BattleUIController.Instance);
        _battleGameModuleActionRunner = CreateBattleGameModuleActionRunner(
            scenarioData,
            _battleScenarioRuntime != null ? _battleScenarioRuntime.SessionState : null,
            _turnQteModuleController,
            _aimShooterModuleController);
        _battleScenarioExecutionGate = CreateBattleScenarioExecutionGate(_battleScenarioRuntime);
    }

    private BattleScenarioData ResolveBattleScenarioData()
    {
        if (_pendingBattleScenarioData != null)
        {
            return _pendingBattleScenarioData;
        }

        if (GlobalDataManager.Instance != null && GlobalDataManager.Instance.PendingBattleScenario != null)
        {
            return GlobalDataManager.Instance.PendingBattleScenario;
        }

        return _defaultBattleScenarioData;
    }

    private void PublishEnemyHpScenarioEvent(
        CharacterBase target,
        int previousHp,
        int currentHp,
        int maxHp,
        BattleRuleTiming timing)
    {
        if (_battleScenarioRuntime == null || target == null)
        {
            return;
        }

        if (!(target is EnemyCharacter))
        {
            return;
        }

        string subjectId = BattleScenarioSubjectResolver.ResolveSubjectId(target);
        if (string.IsNullOrWhiteSpace(subjectId))
        {
            return;
        }

        if (_battleScenarioExecutionGate == null)
        {
            return;
        }

        _battleScenarioExecutionGate.PublishEnemyHpCrossedBelow(
            subjectId,
            previousHp,
            currentHp,
            maxHp,
            timing);
    }

    private void PublishEnemyDefeatedScenarioEvent(CharacterBase target, CharacterBase sourceActor)
    {
        if (_battleScenarioExecutionGate == null
            || !(target is EnemyCharacter)
            || target.IsAlive
            || !_scenarioDefeatPublished.Add(target))
        {
            return;
        }

        _battleScenarioExecutionGate.PublishEnemyDefeated(
            BattleScenarioSubjectResolver.ResolveSubjectId(target),
            BattleScenarioSubjectResolver.ResolveSubjectId(sourceActor),
            BattleRuleTiming.AfterCurrentAction);
    }

    private void PublishSkillCompletedScenarioEvent(SkillData skill, CharacterBase sourceActor)
    {
        if (_battleScenarioExecutionGate == null
            || skill == null
            || string.IsNullOrWhiteSpace(skill.SkillID))
        {
            return;
        }

        _battleScenarioExecutionGate.PublishSkillCompleted(
            skill.SkillID,
            BattleScenarioSubjectResolver.ResolveSubjectId(sourceActor),
            string.Empty,
            BattleRuleTiming.AfterCurrentSkill);
    }
    private IEnumerator FlushBattleScenarioEvents(BattleRuleTiming timing)
    {
        if (_battleScenarioExecutionGate == null)
        {
            yield break;
        }

        yield return StartCoroutine(_battleScenarioExecutionGate.Flush(timing));
        ReportBattleScenarioExecutionResult(_battleScenarioExecutionGate.LastHandle);
    }

    private BattleScenarioExecutionGate CreateBattleScenarioExecutionGate(BattleScenarioRuntime runtime)
    {
        if (runtime == null)
        {
            return null;
        }

        var bridge = new BattleScenarioActionBridge(runtime, CreateBattleScenarioActionDirector());
        var gate = new BattleScenarioExecutionGate(runtime, bridge, CreateBattleScenarioActionContext);
        gate.TriggersReady += HandleBattleScenarioTriggersReady;
        return gate;
    }

    private void HandleBattleScenarioTriggersReady(IReadOnlyList<BattleScenarioTrigger> triggers)
    {
        if (triggers == null || triggers.Count == 0)
        {
            return;
        }

        OnBattleScenarioTriggersReady?.Invoke(new List<BattleScenarioTrigger>(triggers));
    }

    private void ReportBattleScenarioExecutionResult(ActionExecutionHandle handle)
    {
        if (handle == null)
        {
            return;
        }

        if (handle.Status == ActionExecutionStatus.Failed)
        {
            Debug.LogError("[BattleManager] Battle scenario action sequence failed: " + handle.Result.Message, this);
        }
        else if (handle.Status == ActionExecutionStatus.Canceled)
        {
            Debug.LogWarning("[BattleManager] Battle scenario action sequence canceled: " + handle.Result.Message, this);
        }
    }

    private static ActionDirector CreateBattleScenarioActionDirector()
    {
        return BattleScenarioActionRegistryFactory.CreateDirector();
    }

    private ActionExecutionContext CreateBattleScenarioActionContext()
    {
        RefreshBattleSessionParticipants();
        BattleScenarioData scenarioData = _battleScenarioRuntime != null ? _battleScenarioRuntime.ScenarioData : null;
        return BattleScenarioActionContextFactory.Create(
            scenarioData,
            skillTimelineRunner: new BattleSkillTimelineRunner(this),
            gameModuleActionRunner: _battleGameModuleActionRunner,
            audioActionRunner: new AudioManagerActionRunner(
                new ScenarioAudioClipResolver(
                    scenarioData != null ? scenarioData.AudioClips : null,
                    new ResourcesAudioClipResolver())),
            screenTransitionRunner: new ScreenTransitionRunner(),
            timelineCutsceneRunner: new TimelineCutsceneRunner(
                scenarioData != null ? scenarioData.TimelineCutsceneCatalog : null,
                new BattleTimelineCutsceneBindingSource(this),
                _battleCinematicRunner,
                _battleTweenCinematicService),
            battleCinematicRunner: _battleCinematicRunner,
            battleTweenCinematicService: _battleTweenCinematicService,
            battleSessionState: _battleScenarioRuntime != null ? _battleScenarioRuntime.SessionState : null,
            battleParticipantCommandRunner: _battleParticipantCommandRunner,
            gameModuleEventSink: _battleScenarioExecutionGate);
    }

    private void RefreshBattleSessionParticipants()
    {
        if (_battleScenarioRuntime == null || _battleScenarioRuntime.SessionState == null)
        {
            return;
        }

        _battleParticipantIdRegistry?.Rebuild(_playerParty, _enemies);

        var participants = new List<BattleParticipantSnapshot>();
        List<PlayerCharacter> sessionPlayers = _battlePartyRoster.Count > 0
            ? _battlePartyRoster
            : _playerParty;
        for (int i = 0; i < sessionPlayers.Count; i++)
        {
            BattleParticipantSnapshot snapshot = BattleParticipantSnapshot.FromPlayer(sessionPlayers[i]);
            if (snapshot != null)
            {
                participants.Add(snapshot);
            }
        }

        for (int i = 0; i < _enemies.Count; i++)
        {
            BattleParticipantSnapshot snapshot = BattleParticipantSnapshot.FromEnemy(_enemies[i]);
            if (snapshot != null)
            {
                participants.Add(snapshot);
            }
        }

        _battleScenarioRuntime.SessionState.SetParticipants(participants);
    }

    private CharacterBase FindBattleParticipant(string subjectId)
    {
        if (string.IsNullOrWhiteSpace(subjectId))
        {
            return null;
        }

        string normalized = subjectId.Trim();
        if (_battleParticipantIdRegistry != null
            && _battleParticipantIdRegistry.TryResolve(normalized, out CharacterBase registered))
        {
            return registered;
        }
        for (int i = 0; i < _playerParty.Count; i++)
        {
            PlayerCharacter player = _playerParty[i];
            if (player == null)
            {
                continue;
            }

            if ((i == 0 && string.Equals(normalized, "player", StringComparison.OrdinalIgnoreCase))
                || SubjectMatches(normalized, player.CharacterID)
                || SubjectMatches(normalized, player.DisplayName)
                || SubjectMatches(normalized, player.name))
            {
                return player;
            }
        }

        for (int i = 0; i < _enemies.Count; i++)
        {
            EnemyCharacter enemy = _enemies[i];
            if (enemy == null)
            {
                continue;
            }

            string enemySubjectId = BattleScenarioSubjectResolver.ResolveEnemySubjectId(enemy);
            string enemyDisplayName = enemy.Data != null ? enemy.Data.EnemyName : string.Empty;
            if (SubjectMatches(normalized, enemySubjectId)
                || SubjectMatches(normalized, enemyDisplayName)
                || SubjectMatches(normalized, enemy.name))
            {
                return enemy;
            }
        }

        return null;
    }

    private static string ResolveCommandSubjectId(CharacterBase target, string fallbackSubjectId)
    {
        string subjectId = BattleScenarioSubjectResolver.ResolveSubjectId(target);
        return string.IsNullOrWhiteSpace(subjectId) ? fallbackSubjectId : subjectId;
    }

    private static bool SubjectMatches(string normalizedSubjectId, string candidate)
    {
        return !string.IsNullOrWhiteSpace(candidate)
            && string.Equals(normalizedSubjectId, candidate.Trim(), StringComparison.OrdinalIgnoreCase);
    }


    private static IGameModuleActionRunner CreateBattleGameModuleActionRunner(
        BattleScenarioData scenarioData,
        IGameModuleStateStore moduleStateStore,
        IBattleTurnQteModuleController turnQteController,
        IBattleAimShooterModuleController aimShooterController)
    {
        var registry = BattleGameModuleRegistryFactory.CreateDefault(
            turnQteController,
            BattleUIController.Instance,
            aimShooterController);
        string currentModuleId = scenarioData != null ? scenarioData.OpeningModule : BattleTurnQteGameModuleRuntime.Id;
        return new GameModuleActionRunner(registry, currentModuleId, moduleStateStore);
    }

    #region [ Initialization ]
    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        _isReadyToReveal = !_isDedicatedBattleScene;
    }

    private void OnDestroy()
    {
        CancelPartyWaveTransition();
        _turnQteModuleController?.CancelActiveCameraPresentation();
        BattleScenarioSubjectResolver.ClearRegistry(_battleParticipantIdRegistry);
        if (Instance == this)
            Instance = null;
    }
    private void Start() 
    {
        BattleNarrationFormatter.Config = _battleNarrationConfig;
        // 전용 배틀 씬일 경우에만 자동 셋업 루틴을 시작합니다. (오버월드 버그 방지)
        if (_isDedicatedBattleScene) StartCoroutine(DelayedStartRoutine());
    }
    #endregion

    #region [ State Machine ]
    private void ChangeState(BattleState next)
    {
        CurrentState = next;
        OnStateChanged?.Invoke(next);

        switch (next)
        {
            case BattleState.TurnCalc:    StartCoroutine(RunTurnQteTurnCalculation()); break;
            case BattleState.EnemyAction: StartCoroutine(RunTurnQteEnemyAction()); break;
            case BattleState.BattleEnd:   StartCoroutine(BattleEndRoutine()); break;
        }
    }

    private IEnumerator RunTurnQteTurnCalculation()
    {
        if (_turnQteModuleController != null)
        {
            yield return StartCoroutine(_turnQteModuleController.RunTurnCalculation());
            yield break;
        }

        Debug.LogError("[BattleManager] turn_qte controller is missing. Cannot run QTE turn calculation.");
    }

    private IEnumerator RunTurnQteEnemyAction()
    {
        if (_turnQteModuleController != null)
        {
            yield return StartCoroutine(_turnQteModuleController.RunEnemyAction());
            yield break;
        }

        Debug.LogError("[BattleManager] turn_qte controller is missing. Cannot run QTE enemy action.");
    }

    private IEnumerator StartOpeningBattleGameModule()
    {
        string moduleId = _battleGameModuleActionRunner != null
            && !string.IsNullOrWhiteSpace(_battleGameModuleActionRunner.CurrentModuleId)
            ? _battleGameModuleActionRunner.CurrentModuleId
            : BattleTurnQteGameModuleRuntime.Id;

        yield return StartCoroutine(StartBattleGameModule(moduleId));
    }

    private IEnumerator StartBattleGameModule(string moduleId)
    {
        if (_battleGameModuleActionRunner == null)
        {
            StartTurnQteCombatLoop();
            yield break;
        }

        ActionExecutionContext context = CreateBattleScenarioActionContext();
        IEnumerator routine = _battleGameModuleActionRunner.Start(moduleId, context);
        while (routine.MoveNext())
        {
            yield return routine.Current;
        }

        if (context.Handle.Status == ActionExecutionStatus.Failed)
        {
            Debug.LogError("[BattleManager] Game Module start failed: " + context.Handle.Result.Message);
            if (!string.Equals(moduleId, BattleTurnQteGameModuleRuntime.Id, StringComparison.Ordinal))
            {
                yield return StartCoroutine(StartBattleGameModule(BattleTurnQteGameModuleRuntime.Id));
            }
            else
            {
                StartTurnQteCombatLoop();
            }
        }
    }

    private void StartTurnQteCombatLoop()
    {
        if (_isBattleEnding)
        {
            return;
        }

        ChangeState(BattleState.TurnCalc);
    }

    private bool IsTurnQteCombatInputActive()
    {
        if (_isBattleEnding)
        {
            return false;
        }

        if (_battleGameModuleActionRunner == null)
        {
            return true;
        }

        string moduleId = _battleGameModuleActionRunner.CurrentModuleId;
        return string.IsNullOrWhiteSpace(moduleId)
            || string.Equals(moduleId, BattleTurnQteGameModuleRuntime.Id, StringComparison.Ordinal);
    }
    #endregion

    #region [ Battle Setup & Intro ]
    /// <summary>
    /// 오버월드 맵 위에서 씬 전환 없이 그대로 전투를 시작할 때 호출됩니다.
    /// </summary>
    public void StartSeamlessBattle(List<EnemyData> encounterEnemies, PlayerController playerCtrl, IEncounterSource encounterSource = null)
    {
        if (!TryStartSeamlessBattle(encounterEnemies, playerCtrl, encounterSource, out string error))
            Debug.LogWarning($"[BattleManager] 심리스 전투 시작 실패: {error}", this);
    }

    public bool CanStartSeamlessBattle(List<EnemyData> encounterEnemies, PlayerController playerCtrl, out string error)
    {
        if (_isDedicatedBattleScene)
        {
            error = "전용 BattleScene용 BattleManager는 심리스 전투를 시작할 수 없습니다.";
            return false;
        }

        if (_isBattleActive || _isBattleEnding)
        {
            error = "이미 전투를 시작했거나 종료 처리 중입니다.";
            return false;
        }

        if (!isActiveAndEnabled)
        {
            error = "BattleManager가 활성화되어 있지 않습니다.";
            return false;
        }

        if (playerCtrl == null)
        {
            error = "PlayerController가 없습니다.";
            return false;
        }

        if (!playerCtrl.TryGetComponent(out PlayerCharacter _))
        {
            error = "PlayerController에 PlayerCharacter가 없습니다.";
            return false;
        }

        if (encounterEnemies == null || encounterEnemies.Count == 0)
        {
            error = "EncounterEnemies가 비어 있습니다.";
            return false;
        }

        for (int i = 0; i < encounterEnemies.Count; i++)
        {
            EnemyData enemy = encounterEnemies[i];
            if (enemy == null)
            {
                error = $"EncounterEnemies[{i}]가 비어 있습니다.";
                return false;
            }

            GameObject prefab = ResolveEnemyPrefab(enemy);
            if (prefab == null || prefab.GetComponent<EnemyCharacter>() == null)
            {
                error = $"'{enemy.EnemyName}'의 공용 적 프리팹에 EnemyCharacter가 없습니다.";
                return false;
            }
        }

        if (PositionManager.Instance == null)
        {
            error = "PositionManager가 없습니다.";
            return false;
        }

        if (!PositionManager.Instance.IsConfigured(out error))
            return false;

        if (_battleUICanvas == null)
        {
            error = "Battle UI Canvas가 연결되지 않았습니다.";
            return false;
        }

        error = string.Empty;
        return true;
    }

    public bool TryStartSeamlessBattle(
        List<EnemyData> encounterEnemies,
        PlayerController playerCtrl,
        IEncounterSource encounterSource,
        out string error)
    {
        if (!CanStartSeamlessBattle(encounterEnemies, playerCtrl, out error))
            return false;

        CaptureSeamlessBgm();
        CameraController cameraController = CameraController.Instance;
        _seamlessCameraDefaultTarget = cameraController != null
            ? cameraController.CaptureDefaultTarget()
            : default;
        _isBattleActive = true;
        _activeEncounterSource = encounterSource;
        _activeEncounterPlayer = playerCtrl;
        StartCoroutine(StartSeamlessBattleRoutine(new List<EnemyData>(encounterEnemies), playerCtrl));
        return true;
    }
    private IEnumerator StartSeamlessBattleRoutine(List<EnemyData> encounterEnemies, PlayerController playerCtrl)
    {
        Debug.Log("<color=cyan>[BattleManager] 심리스 전투 연출 시작!</color>");

        if (GlobalDataManager.Instance != null && GlobalDataManager.Instance.PendingBattleBGM != null)
            AudioManager.Instance?.CrossFadeBGM(GlobalDataManager.Instance.PendingBattleBGM, 0.8f);
        if (GlobalDataManager.Instance != null)
            GlobalDataManager.Instance.PendingBattleBGM = null;

        yield return StartCoroutine(WarmupBattlePresentation());

        ResetBattlePartyCollections();
        PlayerCharacter playerChar = playerCtrl.GetComponent<PlayerCharacter>();
        CharacterSaveData scenePlayerSaveData = null;
        
        if (playerChar != null)
        {
            GlobalDataManager global = GlobalDataManager.Instance;
            scenePlayerSaveData = global != null
                ? global.InitializePartyFromScene(playerChar)
                : null;
            if (scenePlayerSaveData != null)
                playerChar.LoadDataFromGlobal(scenePlayerSaveData);

            RegisterPreparedPartyMember(playerChar, PositionManager.Instance, false);
        }

        // 2. 적군 셋업
        _enemies.Clear();
        _seamlessSpawnedPlayers.Clear();
        var pm = PositionManager.Instance;

        GlobalDataManager globalData = GlobalDataManager.Instance;
        if (globalData != null)
        {
            if (globalData.Party.Count > BattleRosterLimit)
            {
                Debug.LogWarning(
                    $"[BattleManager] 전투 파티는 앞 {BattleRosterLimit}명만 사용합니다. 저장 파티원 수={globalData.Party.Count}",
                    this);
            }

            int sourceCount = Mathf.Min(globalData.Party.Count, BattleRosterLimit);
            for (int i = 0; i < sourceCount && _battlePartyRoster.Count < BattleRosterLimit; i++)
            {
                CharacterSaveData saveData = globalData.Party[i];
                bool representsScenePlayer = scenePlayerSaveData != null
                    ? ReferenceEquals(saveData, scenePlayerSaveData)
                    : i == 0;
                if (representsScenePlayer)
                    continue;

                GameObject playerPrefab = ResolvePlayerBattlePrefab(saveData);
                if (playerPrefab == null)
                {
                    Debug.LogError($"[BattleManager] Battle prefab is missing for party index {i}.", this);
                    continue;
                }

                GameObject playerObject = Instantiate(playerPrefab, Vector3.zero, Quaternion.identity);
                if (!playerObject.TryGetComponent(out PlayerCharacter additionalPlayer))
                {
                    Debug.LogError($"[BattleManager] Player prefab '{playerPrefab.name}' has no PlayerCharacter.", playerObject);
                    Destroy(playerObject);
                    continue;
                }

                additionalPlayer.LoadDataFromGlobal(saveData);
                RegisterPreparedPartyMember(additionalPlayer, pm, true);
                _seamlessSpawnedPlayers.Add(additionalPlayer);
            }
        }
        
        for (int i = 0; i < encounterEnemies.Count; i++)
        {
            GameObject enemyPrefab = ResolveEnemyPrefab(encounterEnemies[i]);
            if (pm != null && enemyPrefab != null)
            {
                Vector3 spawnPos = pm.GetEnemyDefaultPos(i);
                GameObject enemyObj = Instantiate(enemyPrefab, spawnPos, Quaternion.identity);
                EnemyCharacter enemyChar = enemyObj.GetComponent<EnemyCharacter>();
                
                if (enemyChar != null)
                {
                    var overworldEnemy = enemyObj.GetComponent<OverworldEnemy>();
                    if (overworldEnemy != null) overworldEnemy.DisableForBattleInstance();
                    enemyChar.Setup(encounterEnemies[i]);
                    enemyChar.SetBattleMode(true);
                    enemyChar.PlayBattleAnim(EnemyCharacter.HashBattleIdle);
                    StartCoroutine(enemyChar.ForceEnterBattleIdleRoutine());
                    _enemies.Add(enemyChar);
                }
                else
                {
                    Debug.LogError($"[BattleManager] 공용 적 프리팹 '{enemyPrefab.name}'에 EnemyCharacter 컴포넌트가 없어 적을 생성할 수 없습니다.", enemyObj);
                }
            }
            else
            {
                Debug.LogError($"[BattleManager] 공용 적 프리팹을 찾지 못했습니다. EnemyData={encounterEnemies[i]?.EnemyName}");
            }
        }

        globalData?.PendingEnemies.Clear();
        StartCoroutine(SeamlessIntroRoutine(playerCtrl));
    }

    private IEnumerator SeamlessIntroRoutine(PlayerController playerCtrl)
    {
        var pm = PositionManager.Instance;
        if (pm != null && playerCtrl != null)
        {
            Vector3 battlePos = pm.GetPlayerDefaultPos(0);
            playerCtrl.PlayBattleAnim(PlayerCharacter.HashBattleMove);
            
            SetGhostTrail(playerCtrl.GetComponent<CharacterBase>(), true);
            yield return playerCtrl.transform.DOMove(battlePos, 0.5f).SetEase(Ease.OutExpo).WaitForCompletion();
            SetGhostTrail(playerCtrl.GetComponent<CharacterBase>(), false);
            
            playerCtrl.SetFacingDirection(3);
            playerCtrl.SetBattleMode(true);
        }

        // QTE UI 레이아웃 예열 (첫 스킬 노드 누락 방지)
        BattleUIController.Instance?.ShowSkillQTE(Vector2.zero, "", 0f);
        yield return null; 
        BattleUIController.Instance?.HideSkillQTE();
        Canvas.ForceUpdateCanvases();
        CameraController.Instance?.ResetCamera(0f);

        InitializeBattleScenarioRuntime();
        RefreshBattleSessionParticipants();
        OnBattleStarted?.Invoke(_playerParty, _enemies);
        _battleNarrationConfig?.ResetRuntimeState();
        _battleTurnCounter = 0;
        _isBattleEnding = false;
        _rewardCommitted = false;
        _lastRewardResult = null;
        _playerPreemptiveAttackAvailable = GlobalDataManager.Instance != null
            && GlobalDataManager.Instance.CurrentEncounterPlayerPreemptiveAttack;
        yield return StartCoroutine(PlayBattleStartedScenarioSequence());
        if (!HasImmediateBattleStartScenario())
        {
            RequestNarration(BattleNarrationFormatter.BattleStart());
            TryRequestFlavorNarration();
            yield return StartCoroutine(WaitForNarrationToFinish());
        }
        ChangeState(BattleState.Init);
        yield return StartCoroutine(StartOpeningBattleGameModule());
    }

    private IEnumerator PlayBattleStartedScenarioSequence()
    {
        if (_battleScenarioExecutionGate == null)
        {
            yield break;
        }

        _battleScenarioExecutionGate.PublishBattleStarted(BattleRuleTiming.Immediate);
        yield return StartCoroutine(_battleScenarioExecutionGate.PlayReadyTriggers());
        ReportBattleScenarioExecutionResult(_battleScenarioExecutionGate.LastHandle);
    }

    private bool HasImmediateBattleStartScenario()
    {
        if (_battleScenarioRuntime == null || _battleScenarioRuntime.ScenarioData == null)
        {
            return false;
        }

        List<BattleEventRuleData> rules = _battleScenarioRuntime.ScenarioData.Rules;
        if (rules == null)
        {
            return false;
        }

        for (int i = 0; i < rules.Count; i++)
        {
            BattleEventRuleData rule = rules[i];
            if (rule == null || rule.Disabled)
            {
                continue;
            }

            if (rule.EventType == BattleEventType.BattleStarted
                && rule.Timing == BattleRuleTiming.Immediate
                && !string.IsNullOrWhiteSpace(rule.SequenceId))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// 전용 배틀 씬(BattleScene)으로 넘어왔을 때 호출되는 자동 셋업 루틴입니다.
    /// </summary>
    private IEnumerator DelayedStartRoutine()
    {
        _isReadyToReveal = false;
        ChangeState(BattleState.Init);
        yield return StartCoroutine(WarmupBattlePresentation());

        var global = GlobalDataManager.Instance;
        var pm = PositionManager.Instance;

        if (global != null && global.PendingBattleBGM != null)
            AudioManager.Instance?.CrossFadeBGM(global.PendingBattleBGM, 0.8f);
        if (global != null)
            global.PendingBattleBGM = null;

        var existingPlayers = FindObjectsByType<PlayerCharacter>(FindObjectsSortMode.None);
        foreach (var p in existingPlayers)
        {
            if (p.gameObject.scene != gameObject.scene) 
            {
                var sr = p.GetComponent<SpriteRenderer>();
                if (sr != null) sr.enabled = false;
                var rb = p.GetComponent<Rigidbody2D>();
                if (rb != null) rb.simulated = false; 
            }
        }

        ResetBattlePartyCollections();
        int partyCount = global != null && global.Party.Count > 0 ? global.Party.Count : 1;
        if (partyCount > BattleRosterLimit)
        {
            Debug.LogWarning(
                $"[BattleManager] 전투 파티는 앞 {BattleRosterLimit}명만 사용합니다. 저장 파티원 수={partyCount}",
                this);
        }

        int sourceCount = Mathf.Min(partyCount, BattleRosterLimit);
        for (int i = 0; i < sourceCount && _battlePartyRoster.Count < BattleRosterLimit; i++)
        {
            CharacterSaveData saveData = global != null && global.Party.Count > i ? global.Party[i] : null;
            GameObject playerPrefab = ResolvePlayerBattlePrefab(saveData);
            if (playerPrefab == null)
            {
                Debug.LogError($"[BattleManager] Battle prefab is missing for party index {i}.");
                continue;
            }

            GameObject playerObject = Instantiate(playerPrefab, Vector3.zero, Quaternion.identity);
            if (!playerObject.TryGetComponent(out PlayerCharacter playerCharacter))
            {
                Debug.LogError($"[BattleManager] Player prefab '{playerPrefab.name}' has no PlayerCharacter.", playerObject);
                Destroy(playerObject);
                continue;
            }

            if (saveData != null)
                playerCharacter.LoadDataFromGlobal(saveData);

            RegisterPreparedPartyMember(playerCharacter, pm, true);
        }

        yield return null; 
        yield return new WaitForEndOfFrame(); 

        for (int i = 0; i < _playerParty.Count; i++)
        {
            var pChar = _playerParty[i];
            Vector3 targetPos = pm != null ? pm.GetPlayerDefaultPos(i) : new Vector3(-6f + (i * 2f), -1f, 0f);
            
            pChar.transform.position = targetPos;
            var rb = pChar.GetComponent<Rigidbody2D>();
            if (rb != null) rb.position = targetPos;

            var ctrl = pChar.GetComponent<PlayerController>();
            if (ctrl != null) ctrl.SetFacingDirection(3); 
        }

        if (global != null && global.PendingEnemies != null && global.PendingEnemies.Count > 0)
        {
            foreach(var e in _enemies) if(e != null) Destroy(e.gameObject);
            _enemies.Clear();

            for (int i = 0; i < global.PendingEnemies.Count; i++)
            {
                Vector3 spawnPos = pm != null ? pm.GetEnemyDefaultPos(i) : new Vector3(6f, -1f, 0f);
                GameObject enemyPrefab = ResolveEnemyPrefab(global.PendingEnemies[i]);
                if (enemyPrefab != null)
                {
                    GameObject enemyObj = Instantiate(enemyPrefab, spawnPos, Quaternion.identity);
                    if (enemyObj.TryGetComponent(out EnemyCharacter enemyChar))
                    {
                        var overworldEnemy = enemyObj.GetComponent<OverworldEnemy>();
                        if (overworldEnemy != null) overworldEnemy.DisableForBattleInstance();
                        enemyChar.Setup(global.PendingEnemies[i]);
                        enemyChar.SetBattleMode(true);
                        enemyChar.PlayBattleAnim(EnemyCharacter.HashBattleIdle);
                        StartCoroutine(enemyChar.ForceEnterBattleIdleRoutine());
                        _enemies.Add(enemyChar);
                    }
                    else
                    {
                        Debug.LogError($"[BattleManager] 공용 적 프리팹 '{enemyPrefab.name}'에 EnemyCharacter 컴포넌트가 없어 적을 생성할 수 없습니다.", enemyObj);
                    }
                }
                else
                {
                    Debug.LogError($"[BattleManager] 공용 적 프리팹을 찾지 못했습니다. EnemyData={global.PendingEnemies[i]?.EnemyName}");
                }
            }
            global.PendingEnemies.Clear();
        }

        if (BattleUIController.Instance != null)
        {
            BattleUIController.Instance.ShowSkillQTE(Vector2.zero, "", 0f);
            yield return null; // 1프레임 쉬면서 캔버스 렌더링 확정
            BattleUIController.Instance.HideSkillQTE();
        }

        Canvas.ForceUpdateCanvases();
        CameraController.Instance?.ResetCamera(0f);

        InitializeBattleScenarioRuntime();
        RefreshBattleSessionParticipants();
        OnBattleStarted?.Invoke(_playerParty, _enemies);
        Canvas.ForceUpdateCanvases();
        yield return null;
        Canvas.ForceUpdateCanvases();
        CameraController.Instance?.ResetCamera(0f);
        _isReadyToReveal = true;

        _battleNarrationConfig?.ResetRuntimeState();
        _battleTurnCounter = 0;
        _isBattleEnding = false;
        _rewardCommitted = false;
        _lastRewardResult = null;
        _playerPreemptiveAttackAvailable = global != null && global.CurrentEncounterPlayerPreemptiveAttack;
        yield return StartCoroutine(WaitForNarrationToFinish());
        yield return StartCoroutine(PlayBattleStartedScenarioSequence());
        if (!HasImmediateBattleStartScenario())
        {
            RequestNarration(BattleNarrationFormatter.BattleStart());
            TryRequestFlavorNarration();
            yield return StartCoroutine(WaitForNarrationToFinish());
        }
        yield return StartCoroutine(StartOpeningBattleGameModule());
    }

    private void ResetBattlePartyCollections()
    {
        CancelPartyWaveTransition();
        _playerParty.Clear();
        _reserveParty.Clear();
        _battlePartyRoster.Clear();
    }

    private bool RegisterPreparedPartyMember(
        PlayerCharacter player,
        PositionManager positionManager,
        bool activateImmediately)
    {
        if (player == null || _battlePartyRoster.Count >= BattleRosterLimit)
            return false;

        _battlePartyRoster.Add(player);
        if (_playerParty.Count < ActivePartyLimit)
        {
            int slotIndex = _playerParty.Count;
            _playerParty.Add(player);
            if (activateImmediately)
                ActivatePartyMemberAtSlot(player, slotIndex, positionManager);
            return true;
        }

        _reserveParty.Add(player);
        player.gameObject.SetActive(false);
        return true;
    }

    private static void ActivatePartyMemberAtSlot(
        PlayerCharacter player,
        int slotIndex,
        PositionManager positionManager)
    {
        if (player == null)
            return;

        player.gameObject.SetActive(true);
        Vector3 targetPosition = positionManager != null
            ? positionManager.GetPlayerDefaultPos(slotIndex)
            : new Vector3(-6f + (slotIndex * 2f), -1f, 0f);
        player.transform.position = targetPosition;

        Rigidbody2D body = player.GetComponent<Rigidbody2D>();
        if (body != null)
            body.position = targetPosition;

        PlayerController controller = player.GetComponent<PlayerController>();
        if (controller != null)
        {
            controller.SetFacingDirection(3);
            controller.SetBattleMode(true);
        }
    }

    private GameObject ResolvePlayerBattlePrefab(CharacterSaveData saveData)
    {
        CharacterData characterData = saveData != null
            ? CharacterDatabase.FindById(saveData.CharacterDataID)
            : null;
        return characterData != null && characterData.BattlePrefab != null
            ? characterData.BattlePrefab
            : _playerBasePrefab;
    }

    private GameObject ResolveEnemyPrefab(EnemyData enemyData)
    {
        if (enemyData != null && enemyData.Prefab != null)
            return enemyData.Prefab;

        return _enemyBasePrefab;
    }

    private bool EnemyHasSequenceSkill(EnemyCharacter enemy)
    {
        return enemy != null && enemy.Data != null && enemy.Data.SkillList != null && enemy.Data.SkillList.Count > 0;
    }
private SkillData GetEnemySequenceSkill(EnemyCharacter enemy, EnemyAction action)
{
    if (enemy == null || enemy.Data == null) return null;
    if (action == EnemyAction.UseSkill && enemy.Data.SkillList != null && enemy.Data.SkillList.Count > 0)
    {
        int rand = UnityEngine.Random.Range(0, enemy.Data.SkillList.Count);
        return enemy.Data.SkillList[rand];
    }
    if (action == EnemyAction.UseStrongSkill && enemy.Data.StrongSkillList != null && enemy.Data.StrongSkillList.Count > 0)
    {
        int rand = UnityEngine.Random.Range(0, enemy.Data.StrongSkillList.Count);
        return enemy.Data.StrongSkillList[rand];
    }

    return null;
}

    private int ResolveEnemyReturnMoveHash(EnemyCharacter enemy)
    {
        if (enemy != null && enemy.Data != null && !string.IsNullOrWhiteSpace(enemy.Data.ReturnMoveTrigger))
            return Animator.StringToHash(enemy.Data.ReturnMoveTrigger);

        return EnemyCharacter.HashBattleMove;
    }

    private EnemyAttackType ResolveEnemySkillAttackType(SkillData skill)
    {
        if (skill == null) return EnemyAttackType.MeleeClose;

        Action_DefenseWindow defenseWindow = null;
        if (skill.ActionTimeline != null)
        {
            for (int i = 0; i < skill.ActionTimeline.Count; i++)
            {
                defenseWindow = skill.ActionTimeline[i] as Action_DefenseWindow;
                if (defenseWindow != null) break;
            }
        }

        if (defenseWindow != null)
        {
            return defenseWindow.Requirement switch
            {
                DefenseRequirement.ParryOnly => EnemyAttackType.ParryOnly,
                DefenseRequirement.DodgeOnly => EnemyAttackType.DodgeOnly,
                DefenseRequirement.JumpOnly => EnemyAttackType.JumpOnly,
                DefenseRequirement.DodgeOrJump => EnemyAttackType.DodgeOrJump,
                DefenseRequirement.ParryOrDodge => EnemyAttackType.MeleeClose,
                DefenseRequirement.Any => EnemyAttackType.MeleeClose,
                _ => EnemyAttackType.MeleeClose
            };
        }

        return skill.IsAoE || skill.TargetType == TargetAreaType.AoEAll ? EnemyAttackType.AoEAll : EnemyAttackType.MeleeClose;
    }

    private IEnumerator MoveEnemyToCenterIfNeeded(EnemyCharacter enemy)
    {
        if (enemy == null || enemy.Data == null) yield break;
        if (enemy.Data.IsLargeEnemy) yield break;

        Vector3 centerPos = PositionManager.Instance != null ? PositionManager.Instance.GetCenterPos() : enemy.transform.position;
        enemy.PlayBattleAnim(EnemyCharacter.HashBattleMove);
        
        SetGhostTrail(enemy, true);
        yield return enemy.transform.DOMove(centerPos, _enemyCenterAdvanceDuration).SetEase(Ease.OutQuad).WaitForCompletion();
        SetGhostTrail(enemy, false);
        
        enemy.PlayBattleAnim(EnemyCharacter.HashBattleIdle);
    }

    private IEnumerator ExecuteEnemySequenceSkill(EnemyCharacter enemy, SkillData skill)
    {
        if (enemy == null || skill == null || skill.ActionTimeline == null || skill.ActionTimeline.Count == 0)
            yield break;

        Vector3 originalPos = enemy.transform.position;
        int enemyIndex = _enemies.IndexOf(enemy);
        Vector3 defaultPos = enemyIndex >= 0 && PositionManager.Instance != null
            ? PositionManager.Instance.GetEnemyDefaultPos(enemyIndex)
            : originalPos;

        List<CharacterBase> targets = new List<CharacterBase>();
        if (skill.IsAoE || skill.TargetType == TargetAreaType.AoEAll)
        {
            targets.AddRange(_playerParty.FindAll(p => p != null && p.IsAlive));
        }
        else
        {
            int targetIdx = _playerParty.FindIndex(p => p != null && p.IsAlive);
            if (targetIdx >= 0) targets.Add(_playerParty[targetIdx]);
        }

        if (targets.Count == 0) yield break;

        SkillContext context = new SkillContext
        {
            Actor = enemy,
            Targets = targets,
            CurrentDamageMultiplier = 1.0f,
            IsPerfectQTE = false
        };

        foreach (var block in skill.ActionTimeline)
        {
            context.Targets.RemoveAll(t => t == null || !t.IsAlive);
            if (context.Targets.Count == 0 || context.StopTimelineExecution) break;
            yield return StartCoroutine(block.Execute(context));
            if (context.StopTimelineExecution) break;
        }

        if (Vector3.Distance(enemy.transform.position, defaultPos) > 0.05f)
        {
            enemy.PlayBattleAnim(ResolveEnemyReturnMoveHash(enemy));
            SetGhostTrail(enemy, true);
            yield return enemy.transform.DOMove(defaultPos, 0.25f).SetEase(Ease.OutQuad).WaitForCompletion();
            SetGhostTrail(enemy, false);
        }

        enemy.PlayBattleAnim(EnemyCharacter.HashBattleIdle);
    }
    #endregion

    #region [ Turn Management ]
    private void AdvanceTurn()
    {
        if (_turnQteModuleController != null)
        {
            _turnQteModuleController.AdvanceTurn();
            return;
        }

        Debug.LogError("[BattleManager] turn_qte controller is missing. Cannot advance QTE turn.");
    }

    private void ResetAllPlayerBattlePoses()
    {
        PositionManager pm = PositionManager.Instance;

        for (int i = 0; i < _playerParty.Count; i++)
        {
            PlayerCharacter player = _playerParty[i];
            if (player == null) continue;
            if (!player.IsAlive) continue;

            PlayerController ctrl = player.GetComponent<PlayerController>();
            Vector3 defaultPos = pm != null ? pm.GetPlayerDefaultPos(i) : player.transform.position;

            if (ctrl != null)
            {
                ctrl.SetBattleSortingBoost(0);
                ctrl.SnapToBattleAnchor(defaultPos);
            }
            else
                player.transform.position = defaultPos;

            player.PlayBattleAnim(PlayerCharacter.HashBattleIdle);
        }
    }

    private void SetActorForeground(CharacterBase actor, bool active)
    {
        const int boost = 5000;
        if (actor == null) return;

        if (actor is PlayerCharacter player)
        {
            PlayerController controller = player.GetComponent<PlayerController>();
            if (controller != null)
            {
                controller.SetBattleSortingBoost(active ? boost : 0);
                return;
            }

            SetSpriteRendererForeground(player, active, boost);
        }
        else if (actor is EnemyCharacter enemy)
        {
            SetSpriteRendererForeground(enemy, active, boost);
        }
        else
        {
            SetSpriteRendererForeground(actor, active, boost);
        }
    }

    private void SetSpriteRendererForeground(CharacterBase actor, bool active, int boost)
    {
        if (actor == null) return;

        SpriteRenderer sr = actor.GetComponent<SpriteRenderer>();
        if (sr == null) return;

        if (active)
        {
            if (!_actorForegroundSortingOrderCache.ContainsKey(actor))
            {
                _actorForegroundSortingOrderCache.Add(actor, sr.sortingOrder);
            }

            sr.sortingOrder = boost;
            return;
        }

        int originalSortingOrder;
        if (_actorForegroundSortingOrderCache.TryGetValue(actor, out originalSortingOrder))
        {
            sr.sortingOrder = originalSortingOrder;
            _actorForegroundSortingOrderCache.Remove(actor);
        }
    }

    IReadOnlyList<PlayerCharacter> IBattleCinematicHost.PlayerParty => _playerParty;
    IReadOnlyList<EnemyCharacter> IBattleCinematicHost.Enemies => _enemies;
    CharacterBase IBattleCinematicHost.FindBattleParticipantBySubjectId(string subjectId) => FindBattleParticipant(subjectId);
    void IBattleCinematicHost.SetActorForeground(CharacterBase actor, bool active) => SetActorForeground(actor, active);
    int IBattleCinematicHost.ResolveEnemyReturnMoveHash(EnemyCharacter enemy) => ResolveEnemyReturnMoveHash(enemy);

    CharacterBase IBattleParticipantCommandHost.FindBattleParticipantBySubjectId(string subjectId) => FindBattleParticipant(subjectId);
    string IBattleParticipantCommandHost.ResolveBattleParticipantSubjectId(CharacterBase target, string fallbackSubjectId) => ResolveCommandSubjectId(target, fallbackSubjectId);
    void IBattleParticipantCommandHost.RefreshBattleSessionParticipants() => RefreshBattleSessionParticipants();
    void IBattleParticipantCommandHost.EmitParticipantDamage(CharacterBase target, int damage, bool isPerfect, int previousHp) => InvokeDamageEvent(target, damage, isPerfect, previousHp);
    void IBattleParticipantCommandHost.EmitParticipantHealed(CharacterBase target, int healedAmount)
    {
        OnDamageDealt?.Invoke(target, -Mathf.Max(0, healedAmount), false);
    }
    void IBattleParticipantCommandHost.EmitParticipantApChanged(PlayerCharacter player, int newAp) => InvokeAPChangedEvent(player, newAp);

    IReadOnlyList<PlayerCharacter> IBattleTurnQteHost.PlayerParty => _playerParty;
    IReadOnlyList<EnemyCharacter> IBattleTurnQteHost.Enemies => _enemies;
    IList<CharacterBase> IBattleTurnQteHost.TurnQueue => _turnQueue;
    IDictionary<EnemyCharacter, BattleQueuedEnemyAction> IBattleTurnQteHost.ReservedEnemyActions => _reservedEnemyActionByActor;
    WaitForSeconds IBattleTurnQteHost.WaitShort => _waitShort;
    int IBattleTurnQteHost.MaxTurnQueueSize => _maxTurnQueueSize;
    int IBattleTurnQteHost.ApPerTurn => _apPerTurn;
    int IBattleTurnQteHost.ApOnParryPerfect => _apOnParryPerfect;
    float IBattleTurnQteHost.EnemyDefenseQteWindow => _enemyDefenseQTEWindow;
    float IBattleTurnQteHost.EnemyAttackVisualDuration => _enemyAttackVisualDuration;
    float IBattleTurnQteHost.EnemyPostHitDelay => _enemyPostHitDelay;
    float IBattleTurnQteHost.EnemyAoeWindup => _enemyAoEWindup;
    float IBattleTurnQteHost.PlayerAttackHitDelay => _playerAttackHitDelay;
    float IBattleTurnQteHost.PlayerAttackRecoverDelay => _playerAttackRecoverDelay;
    Vector3 IBattleTurnQteHost.MeleeAttackOffset => _meleeAttackOffset;
    Vector3 IBattleTurnQteHost.MeleePullbackOffset => _meleePullbackOffset;
    int IBattleTurnQteHost.BattleTurnCounter { get => _battleTurnCounter; set => _battleTurnCounter = value; }
    int IBattleTurnQteHost.CurrentActorIndex { get => _currentActorIndex; set => _currentActorIndex = value; }
    PlayerCharacter IBattleTurnQteHost.PendingActor { get => _pendingActor; set => _pendingActor = value; }
    PlayerMenuAction IBattleTurnQteHost.PendingAction { get => _pendingAction; set => _pendingAction = value; }
    SkillData IBattleTurnQteHost.PendingSkill { get => CurrentPendingSkill; set => CurrentPendingSkill = value; }
    ItemData IBattleTurnQteHost.PendingItem { get => CurrentPendingItem; set => CurrentPendingItem = value; }
    BattleState IBattleTurnQteHost.CurrentBattleState => CurrentState;
    bool IBattleTurnQteHost.CanEscape => _allowEscape;
    bool IBattleTurnQteHost.IsTurnQteCombatInputActive() => IsTurnQteCombatInputActive();
    void IBattleTurnQteHost.StartTurnQteCombatLoop() => StartTurnQteCombatLoop();
    void IBattleTurnQteHost.ChangeBattleState(BattleState state) => ChangeState(state);
    bool IBattleTurnQteHost.CheckVictory() => CheckVictory();
    bool IBattleTurnQteHost.CheckDefeat() => CheckDefeat();
    bool IBattleTurnQteHost.TryStartNextPartyWave() => TryStartNextPartyWave();
    bool IBattleTurnQteHost.ConsumePlayerPreemptiveAttack()
    {
        bool available = _playerPreemptiveAttackAvailable;
        _playerPreemptiveAttackAvailable = false;
        return available;
    }
    void IBattleTurnQteHost.BroadcastVisibleTurnQueue() => BroadcastVisibleTurnQueue();
    void IBattleTurnQteHost.ResetAllPlayerBattlePoses() => ResetAllPlayerBattlePoses();
    IEnumerator IBattleTurnQteHost.WaitForNarrationToFinish() => WaitForNarrationToFinish();
    void IBattleTurnQteHost.TryRequestFlavorNarration() => TryRequestFlavorNarration();
    void IBattleTurnQteHost.NotifyPlayerTurnStarted(PlayerCharacter player) => NotifyPlayerTurnStarted(player);
    void IBattleTurnQteHost.NotifyEnemyActionStarted(EnemyCharacter enemy, EnemyAttackType attackType) => NotifyEnemyActionStarted(enemy, attackType);
    void IBattleTurnQteHost.NotifyTargetSelectionStarted(PlayerMenuAction action) => NotifyTargetSelectionStarted(action);
    void IBattleTurnQteHost.RequestNarration(BattleNarrationMessage message) => RequestNarration(message);
    IEnumerator IBattleTurnQteHost.RunAwayRoutine() => RunRoutine();
    void IBattleTurnQteHost.ClearTurnQtePendingActionState() => ClearTurnQtePendingActionState();
    Coroutine IBattleTurnQteHost.StartManagedCoroutine(IEnumerator routine) => StartCoroutine(routine);
    void IBattleTurnQteHost.SetActorForeground(CharacterBase actor, bool active) => SetActorForeground(actor, active);
    void IBattleTurnQteHost.EmitDamage(CharacterBase target, int damage, bool isPerfect) => InvokeDamageEvent(target, damage, isPerfect);
    void IBattleTurnQteHost.EmitDamage(CharacterBase target, int damage, bool isPerfect, int previousHp) => InvokeDamageEvent(target, damage, isPerfect, previousHp);
    void IBattleTurnQteHost.EmitDamage(CharacterBase source, CharacterBase target, int damage, bool isCritical) => InvokeDamageEvent(source, target, damage, isCritical, target != null ? Mathf.Clamp(target.CurrentHP + Mathf.Max(0, damage), 0, target.MaxHP) : 0);
    void IBattleTurnQteHost.EmitApChanged(PlayerCharacter player, int newAp) => InvokeAPChangedEvent(player, newAp);
    void IBattleTurnQteHost.EmitDamageNotificationOnly(CharacterBase target, int damage, bool isPerfect) => NotifyDamageDealt(target, damage, isPerfect);
    void IBattleTurnQteHost.EmitDamageNotificationOnly(CharacterBase source, CharacterBase target, int damage, bool isCritical) => NotifyDamageDealt(source, target, damage, isCritical);
    void IBattleTurnQteHost.EmitMiss(CharacterBase source, CharacterBase target) => InvokeMissFeedback(source, target);
    void IBattleTurnQteHost.PublishEnemyHpScenarioEvent(CharacterBase target, int previousHp, int currentHp, int maxHp, BattleRuleTiming timing) => PublishEnemyHpScenarioEvent(target, previousHp, currentHp, maxHp, timing);
    void IBattleTurnQteHost.PublishEnemyDefeatedScenarioEvent(CharacterBase target, CharacterBase sourceActor) => PublishEnemyDefeatedScenarioEvent(target, sourceActor);
    void IBattleTurnQteHost.PublishSkillCompletedScenarioEvent(SkillData skill, CharacterBase sourceActor) => PublishSkillCompletedScenarioEvent(skill, sourceActor);
    IEnumerator IBattleTurnQteHost.FlushBattleScenarioEvents(BattleRuleTiming timing) => FlushBattleScenarioEvents(timing);
    SkillData IBattleTurnQteHost.ResolveEnemySequenceSkill(EnemyCharacter enemy, EnemyAction action) => GetEnemySequenceSkill(enemy, action);
    EnemyAttackType IBattleTurnQteHost.ResolveEnemySkillAttackType(SkillData skill) => ResolveEnemySkillAttackType(skill);
    IEnumerator IBattleTurnQteHost.MoveEnemyToCenterIfNeeded(EnemyCharacter enemy) => MoveEnemyToCenterIfNeeded(enemy);
    int IBattleTurnQteHost.ResolveEnemyReturnMoveHash(EnemyCharacter enemy) => ResolveEnemyReturnMoveHash(enemy);
    #endregion

    #region [ Player Input Routing ]
    public void OnPlayerActionSelected(PlayerCharacter actor, PlayerMenuAction action)
    {
        if (!IsTurnQteCombatInputActive())
        {
            return;
        }

        if (_turnQteModuleController != null)
            _turnQteModuleController.SelectPlayerAction(actor, action);
        else
            Debug.LogError("[BattleManager] turn_qte controller is missing. Cannot select QTE player action.");
    }

    public void OnSubMenuActionSelected(PlayerCharacter actor, PlayerMenuAction action, SkillData skill, ItemData item)
    {
        if (!IsTurnQteCombatInputActive())
        {
            return;
        }

        if (_turnQteModuleController != null)
            _turnQteModuleController.SelectSubMenuAction(actor, action, skill, item);
        else
            Debug.LogError("[BattleManager] turn_qte controller is missing. Cannot select QTE submenu action.");
    }

    public void CancelActionSelection() 
    {
        if (!IsTurnQteCombatInputActive())
        {
            return;
        }

        if (_turnQteModuleController != null)
            _turnQteModuleController.CancelActionSelection();
        else
            Debug.LogError("[BattleManager] turn_qte controller is missing. Cannot cancel QTE action selection.");
    }

    public void CancelTargetSelection() 
    {
        if (!IsTurnQteCombatInputActive())
        {
            return;
        }

        if (_turnQteModuleController != null)
            _turnQteModuleController.CancelTargetSelection();
        else
            Debug.LogError("[BattleManager] turn_qte controller is missing. Cannot cancel QTE target selection.");
    }

    public void ConfirmTargetAndExecute(int targetIndex)
    {
        if (!IsTurnQteCombatInputActive())
        {
            return;
        }

        if (_turnQteModuleController != null)
            _turnQteModuleController.ConfirmTargetAndExecute(targetIndex);
        else
            Debug.LogError("[BattleManager] turn_qte controller is missing. Cannot confirm QTE target.");
    }
    #endregion

    #region [ Action Executions ]
    private void EndAction()
    {
        if (_turnQteModuleController != null)
            _turnQteModuleController.CompleteAction();
        else
            Debug.LogError("[BattleManager] turn_qte controller is missing. Cannot complete QTE action.");
    }

    private void ClearTurnQtePendingActionState()
    {
        Time.timeScale = 1.0f;
        QTEManager.Instance?.ForceStop();
        _pendingActor = null;
        _pendingAction = default;
        CurrentPendingSkill = null;
        CurrentPendingItem = null;
    }

    #endregion

    #region [ Outro & Scene Transitions ]
    private bool CheckVictory() => _enemies != null && _enemies.Count > 0 && _enemies.TrueForAll(e => e == null || !e.IsAlive);
    private bool CheckDefeat()  => _playerParty.TrueForAll(p => p == null || !p.IsAlive);

    private bool TryStartNextPartyWave()
    {
        if (_isPartyWaveTransitioning || _isBattleEnding)
            return false;

        for (int i = 0; i < _playerParty.Count; i++)
        {
            PlayerCharacter player = _playerParty[i];
            if (player != null && player.IsAlive)
                return false;
        }

        int transitionVersion = ++_partyWaveTransitionVersion;
        _isPartyWaveTransitioning = true;
        if (!TryPromoteReservePartyWave())
        {
            _isPartyWaveTransitioning = false;
            return false;
        }

        _partyWaveTransitionCoroutine = StartCoroutine(
            CompletePartyWaveTransition(transitionVersion));
        return true;
    }

    private bool TryPromoteReservePartyWave()
    {
        for (int i = 0; i < _playerParty.Count; i++)
        {
            PlayerCharacter player = _playerParty[i];
            if (player != null && player.IsAlive)
                return false;
        }

        int availableReserveCount = 0;
        for (int i = 0; i < _reserveParty.Count; i++)
        {
            PlayerCharacter reserve = _reserveParty[i];
            if (reserve != null && reserve.IsAlive)
                availableReserveCount++;
        }

        if (availableReserveCount == 0)
            return false;

        for (int i = 0; i < _playerParty.Count; i++)
        {
            PlayerCharacter player = _playerParty[i];
            if (player != null)
                player.gameObject.SetActive(false);
        }

        _playerParty.Clear();
        PositionManager positionManager = PositionManager.Instance;
        for (int i = 0; i < _reserveParty.Count && _playerParty.Count < ActivePartyLimit; i++)
        {
            PlayerCharacter reserve = _reserveParty[i];
            if (reserve == null || !reserve.IsAlive)
                continue;

            int slotIndex = _playerParty.Count;
            _playerParty.Add(reserve);
            ActivatePartyMemberAtSlot(reserve, slotIndex, positionManager);
        }

        _reserveParty.Clear();
        _turnQueue.Clear();
        _currentActorIndex = 0;
        RefreshBattleSessionParticipants();
        OnPlayerPartyChanged?.Invoke(_playerParty);
        return _playerParty.Count > 0;
    }

    private IEnumerator CompletePartyWaveTransition(int transitionVersion)
    {
        RequestNarration(new BattleNarrationMessage(
            "후열이 전투에 합류했다!",
            BattleNarrationStyle.System,
            BattleNarrationPriority.Critical,
            0.6f,
            true));
        yield return StartCoroutine(WaitForNarrationToFinish());

        if (transitionVersion != _partyWaveTransitionVersion
            || !_isPartyWaveTransitioning
            || _isBattleEnding)
        {
            yield break;
        }

        _partyWaveTransitionCoroutine = null;
        _isPartyWaveTransitioning = false;
        if (IsTurnQteCombatInputActive())
            ChangeState(BattleState.TurnCalc);
    }

    private void CancelPartyWaveTransition()
    {
        _partyWaveTransitionVersion++;
        if (_partyWaveTransitionCoroutine != null)
        {
            StopCoroutine(_partyWaveTransitionCoroutine);
            _partyWaveTransitionCoroutine = null;
        }

        _isPartyWaveTransitioning = false;
    }

    private IEnumerator RunRoutine()
    {
        if (!_allowEscape) yield break;
        if (_isRunInProgress) yield break;
        _isRunInProgress = true;
        _turnQteModuleController?.CancelActiveCameraPresentation();

        RequestNarration(new BattleNarrationMessage("도망을 시도했다...", BattleNarrationStyle.Normal, BattleNarrationPriority.High, 0.2f, true));
        yield return StartCoroutine(WaitForNarrationToFinish());

        bool success = BattleRunPolicy.IsSuccessful(_runSuccessChance, UnityEngine.Random.value);
        RequestNarration(new BattleNarrationMessage(success ? "도망에 성공했다!" : "도망에 실패했다...", BattleNarrationStyle.Warning, BattleNarrationPriority.High, 0.2f, true));
        yield return StartCoroutine(WaitForNarrationToFinish());

        if (success)
        {
            CommitOverworldEncounterResult(BattleEncounterOutcome.Escaped);
            _isRunInProgress = false;
            yield return StartCoroutine(BattleOutroRoutine(BattleEncounterOutcome.Escaped));
        }
        else
        {
            _isRunInProgress = false;
            EndAction();
        }
    }

    private IEnumerator BattleEndRoutine()
    {
        CancelPartyWaveTransition();
        bool victory = CheckVictory();
        BattleEncounterOutcome outcome = victory
            ? BattleEncounterOutcome.Victory
            : BattleEncounterOutcome.PartyDefeated;
        _isBattleEnding = true;
        _turnQteModuleController?.CancelActiveCameraPresentation();
        QTEManager.Instance?.ForceStop();
        CommitOverworldEncounterResult(outcome);
        RequestNarration(victory
            ? new BattleNarrationMessage("전투에서 승리했다!", BattleNarrationStyle.System, BattleNarrationPriority.Critical, 0.8f, true)
            : new BattleNarrationMessage("눈 앞이 캄캄해졌다...", BattleNarrationStyle.System, BattleNarrationPriority.Critical, 2.0f, true));
        yield return StartCoroutine(WaitForNarrationToFinish());
        if (!victory) yield return new WaitForSecondsRealtime(0.75f);
        yield return StartCoroutine(BattleOutroRoutine(outcome));
    }

    #if UNITY_EDITOR
    public void EditorCheatWinBattle()
    {
        if (_isBattleEnding || _enemies == null || _enemies.Count == 0) return;

        foreach (EnemyCharacter enemy in _enemies)
        {
            if (enemy == null || !enemy.IsAlive) continue;

            int damage = Mathf.Max(999999, enemy.MaxHP * 10);
            int dealt = enemy.TakePureDamage(damage);
            OnDamageDealt?.Invoke(enemy, dealt, false);
        }

        if (CheckVictory())
            ChangeState(BattleState.BattleEnd);
    }

    public bool EditorCheatDefeatActivePartyWave(out string error)
    {
        if (_isBattleEnding || CurrentState != BattleState.PlayerActionSelect)
        {
            error = "Defeat Active Wave is only available during player action selection.";
            return false;
        }

        if (_turnQteModuleController == null)
        {
            error = "Turn QTE controller is not ready.";
            return false;
        }

        for (int i = 0; i < _playerParty.Count; i++)
        {
            PlayerCharacter player = _playerParty[i];
            if (player == null || !player.IsAlive)
                continue;

            player.IsInvincible = false;
            int previousHp = player.CurrentHP;
            player.TakePureDamage(Mathf.Max(1, player.MaxHP));
            int dealt = Mathf.Max(0, previousHp - player.CurrentHP);
            OnDamageDealt?.Invoke(player, dealt, false);
        }

        if (!CheckDefeat())
        {
            error = "At least one active party member is still alive.";
            return false;
        }

        _turnQteModuleController.CompleteAction();
        error = string.Empty;
        return true;
    }
    #endif

    private void CommitOverworldEncounterResult(BattleEncounterOutcome outcome)
    {
        var global = GlobalDataManager.Instance;
        if (global == null) return;

        string enemyId = global.CurrentEncounterEnemyId;
        BattleEncounterMemoryRecorder.RecordBattleResult(
            _battleScenarioRuntime != null ? _battleScenarioRuntime.ScenarioData : null,
            _battleScenarioRuntime,
            global,
            enemyId,
            outcome);

        if (!string.IsNullOrWhiteSpace(enemyId))
        {
            string sceneName = global.LastOverworldScene;

            if (outcome == BattleEncounterOutcome.Victory)
            {
                if (global.CurrentEncounterDefeatsOnVictory)
                    global.MarkOverworldEnemyDefeated(enemyId, sceneName);
                else
                    global.ClearOverworldEnemyCooldown(enemyId);
            }
            else if (outcome == BattleEncounterOutcome.Escaped)
            {
                global.MarkOverworldEnemyEscaped(enemyId, sceneName, _postRunEnemyDisableDuration, _postRunEnemyAlpha);
            }
        }
    }

    private static CharacterSaveData FindUniquePartySave(
        IReadOnlyList<CharacterSaveData> party,
        string characterId)
    {
        if (party == null || string.IsNullOrWhiteSpace(characterId))
            return null;

        string normalizedId = characterId.Trim();
        CharacterSaveData match = null;
        for (int i = 0; i < party.Count; i++)
        {
            CharacterSaveData candidate = party[i];
            if (candidate == null
                || !string.Equals(
                    normalizedId,
                    candidate.CharacterDataID?.Trim(),
                    StringComparison.Ordinal))
            {
                continue;
            }

            if (match != null)
                return null;

            match = candidate;
        }

        return match;
    }

    private void ReloadBattlePartyFromGlobal(GlobalDataManager global)
    {
        if (global == null)
            return;

        List<PlayerCharacter> runtimePlayers = _battlePartyRoster.Count > 0
            ? _battlePartyRoster
            : _playerParty;
        var reportedIds = new HashSet<string>(StringComparer.Ordinal);
        for (int i = 0; i < runtimePlayers.Count; i++)
        {
            PlayerCharacter player = runtimePlayers[i];
            if (player == null)
                continue;

            string characterId = player.CharacterID?.Trim();
            CharacterSaveData saveData = FindUniquePartySave(global.Party, characterId);
            if (saveData != null)
            {
                player.LoadDataFromGlobal(saveData);
                continue;
            }

            string diagnosticId = string.IsNullOrEmpty(characterId) ? "<empty>" : characterId;
            if (reportedIds.Add(diagnosticId))
            {
                Debug.LogError(
                    $"[BattleManager] 보상 적용 뒤 파티원을 고유 ID로 다시 불러올 수 없습니다. CharacterDataID={diagnosticId}",
                    player);
            }
        }
    }

    private BattleRewardResult CommitVictoryRewards()
    {
        if (_rewardCommitted)
            return _lastRewardResult;

        _rewardCommitted = true;
        var defeatedEnemies = new List<EnemyData>();
        for (int i = 0; i < _enemies.Count; i++)
        {
            EnemyCharacter enemy = _enemies[i];
            if (enemy != null && enemy.Data != null)
                defeatedEnemies.Add(enemy.Data);
        }

        GlobalDataManager global = GlobalDataManager.Instance;
        _lastRewardResult = BattleRewardService.Grant(defeatedEnemies, global);

        if (global != null)
            ReloadBattlePartyFromGlobal(global);

        OnBattleRewardsGranted?.Invoke(_lastRewardResult);
        return _lastRewardResult;
    }

    private IEnumerator BattleOutroRoutine(BattleEncounterOutcome outcome)
    {
        CancelPartyWaveTransition();
        bool isVictory = outcome == BattleEncounterOutcome.Victory;
        Time.timeScale = 1.0f; // 슬로우 모션 방지
        AudioManager.Instance?.StopBGM(isVictory ? 0.35f : 0.15f);
        OnBattleEnded?.Invoke(isVictory);
        if (isVictory)
            BattleUIController.Instance?.ClearNarrationLog();
        yield return new WaitForSecondsRealtime(0.25f);

        List<PlayerCharacter> playersToSave = _battlePartyRoster.Count > 0
            ? _battlePartyRoster
            : _playerParty;
        foreach (var player in playersToSave)
        {
            if (player != null)
                player.SaveDataToGlobal();
        }

        if (outcome == BattleEncounterOutcome.PartyDefeated)
        {
            if (_isDedicatedBattleScene)
            {
                GlobalDataManager.Instance?.EndOverworldEnemyEncounterContext();
                GameStateManager.Instance?.ChangeState(GameState.Cutscene);
            }
            else
            {
                CompleteSeamlessBattleCleanup(outcome, true);
            }

            GameOverUI gameOver = GameOverUI.EnsureGlobal();
            if (gameOver != null)
                yield return StartCoroutine(gameOver.Show());
            else
                SceneLoader.Instance?.LoadScene(SceneName.Title);
            yield break;
        }
        if (isVictory)
        {
            BattleRewardResult rewards = CommitVictoryRewards();
            BattleResultUI resultUi = _battleUICanvas != null
                ? BattleResultUI.Ensure(_battleUICanvas.transform)
                : null;
            if (resultUi != null)
                yield return StartCoroutine(resultUi.Show(rewards));
        }

        if (_isDedicatedBattleScene)
        {
            GlobalDataManager global = GlobalDataManager.Instance;
            string returnScene = global != null ? global.LastOverworldScene : string.Empty;
            global?.EndOverworldEnemyEncounterContext();

            string destination = !string.IsNullOrWhiteSpace(returnScene)
                ? returnScene
                : _fallbackSceneName;
            if (string.IsNullOrWhiteSpace(destination)
                || !Application.CanStreamedLevelBeLoaded(destination))
            {
                Debug.LogWarning(
                    $"[BattleManager] 복귀 씬 '{destination}'을 로드할 수 없어 {SceneName.Overworld}(으)로 대체합니다.",
                    this);
                destination = SceneName.Overworld;
            }

            GameStateManager.Instance?.ChangeState(GameState.Exploration);
            if (SceneLoader.Instance != null)
                SceneLoader.Instance.LoadScene(destination);
            else
                SceneManager.LoadScene(destination);
        }
        else
        {
            CompleteSeamlessBattleCleanup(outcome, true);
        }
    }

    public bool AbortSeamlessBattle()
    {
        if (_isDedicatedBattleScene
            || _isAbortCleanupInProgress
            || (!_isBattleActive && !_isBattleEnding))
            return false;

        _isAbortCleanupInProgress = true;
        try
        {
            StopAllCoroutines();
            NotifyEncounterAbortedIfSupported(ResolveActiveEncounterPlayer());
            CompleteSeamlessBattleCleanup(BattleEncounterOutcome.Unknown, false);
            return true;
        }
        finally
        {
            _isAbortCleanupInProgress = false;
        }
    }

    private void NotifyEncounterAbortedIfSupported(PlayerController encounterPlayer)
    {
        if (!(_activeEncounterSource is IEncounterAbortSource abortSource))
            return;

        try
        {
            abortSource.OnEncounterAborted(encounterPlayer);
        }
        catch (Exception exception)
        {
            Debug.LogError($"[BattleManager] 조우 중단 콜백에 실패했습니다: {exception.Message}", this);
            Debug.LogException(exception, this);
        }
    }

    private void CompleteSeamlessBattleCleanup(BattleEncounterOutcome outcome, bool notifyEncounterSource)
    {
        if (_isDedicatedBattleScene)
            return;

        CancelPartyWaveTransition();
        bool isVictory = outcome == BattleEncounterOutcome.Victory;
        _turnQteModuleController?.CancelActiveCameraPresentation();
        ClearTurnQtePendingActionState();
        PlayerController encounterPlayer = ResolveActiveEncounterPlayer();
        RestoreSeamlessPlayers(encounterPlayer);
        NotifyEncounterResolved(notifyEncounterSource, outcome, encounterPlayer);
        DestroySeamlessBattleActors();
        RestoreSeamlessBattlePresentation();
        RestoreSeamlessBgm();
        ResetSeamlessBattleState();

        GlobalDataManager.Instance?.EndOverworldEnemyEncounterContext();
        GameStateManager.Instance?.ChangeState(GameState.Exploration);
        Debug.Log("[BattleManager] 심리스 전투 종료. 오버월드 상태를 복구했습니다.");
    }

    private void CaptureSeamlessBgm()
    {
        AudioManager audioManager = AudioManager.Instance;
        _hasSeamlessBgmSnapshot = audioManager != null;
        _seamlessBgmSnapshot = _hasSeamlessBgmSnapshot
            ? audioManager.CaptureBgmPlayback()
            : BgmPlaybackSnapshot.Stopped;
    }

    private void RestoreSeamlessBgm()
    {
        if (!_hasSeamlessBgmSnapshot)
            return;

        BgmPlaybackSnapshot snapshot = _seamlessBgmSnapshot;
        _hasSeamlessBgmSnapshot = false;
        _seamlessBgmSnapshot = BgmPlaybackSnapshot.Stopped;
        AudioManager audioManager = AudioManager.Instance;
        if (audioManager == null)
        {
            Debug.LogWarning(
                "[BattleManager] 심리스 전투 BGM을 복원할 AudioManager가 없습니다.",
                this);
            return;
        }

        audioManager.RestoreBgmPlayback(
            snapshot,
            _seamlessBgmRestoreFadeDuration);
    }

    private PlayerController ResolveActiveEncounterPlayer()
    {
        if (_activeEncounterPlayer != null)
            return _activeEncounterPlayer;

        for (int i = 0; i < _battlePartyRoster.Count; i++)
        {
            PlayerCharacter player = _battlePartyRoster[i];
            if (player != null && !_seamlessSpawnedPlayers.Contains(player))
                return player.GetComponent<PlayerController>();
        }

        if (_playerParty.Count > 0 && _playerParty[0] != null)
            return _playerParty[0].GetComponent<PlayerController>();
        return null;
    }

    private void RestoreSeamlessPlayers(PlayerController encounterPlayer)
    {
        PlayerCharacter encounterCharacter = encounterPlayer != null
            ? encounterPlayer.GetComponent<PlayerCharacter>()
            : null;
        if (encounterCharacter != null && !encounterCharacter.gameObject.activeSelf)
            encounterCharacter.gameObject.SetActive(true);

        List<PlayerCharacter> playersToRestore = _battlePartyRoster.Count > 0
            ? _battlePartyRoster
            : _playerParty;
        for (int i = 0; i < playersToRestore.Count; i++)
        {
            PlayerCharacter player = playersToRestore[i];
            if (player == null)
                continue;

            player.HideBattleSpeechImmediate();
            player.transform.DOKill(false);
            PlayerController controller = player.GetComponent<PlayerController>();
            controller?.SetBattleMode(false);
            Animator animator = player.GetComponent<Animator>();
            if (animator != null)
            {
                animator.Rebind();
                animator.Update(0f);
            }
        }

        if (encounterPlayer != null)
        {
            encounterPlayer.transform.DOKill(false);
            encounterPlayer.SetBattleMode(false);
            encounterPlayer.LoadPositionFromGlobal();
        }

        Physics2D.SyncTransforms();
    }

    private void NotifyEncounterResolved(
        bool shouldNotify,
        BattleEncounterOutcome outcome,
        PlayerController encounterPlayer)
    {
        IEncounterSource source = _activeEncounterSource;
        _activeEncounterSource = null;
        _activeEncounterPlayer = null;
        if (!shouldNotify || source == null)
            return;

        try
        {
            if (source is IEncounterOutcomeSource outcomeSource)
                outcomeSource.OnEncounterResolved(outcome, encounterPlayer);
            else
                source.OnEncounterResolved(
                    outcome == BattleEncounterOutcome.Victory,
                    encounterPlayer);
        }
        catch (Exception exception)
        {
            Debug.LogError($"[BattleManager] 조우 종료 콜백에 실패했습니다: {exception.Message}", this);
            Debug.LogException(exception, this);
        }
    }

    private void DestroySeamlessBattleActors()
    {
        for (int i = 0; i < _enemies.Count; i++)
        {
            EnemyCharacter enemy = _enemies[i];
            if (enemy == null)
                continue;
            enemy.HideBattleSpeechImmediate();
            enemy.transform.DOKill(false);
            Destroy(enemy.gameObject);
        }
        _enemies.Clear();

        for (int i = 0; i < _seamlessSpawnedPlayers.Count; i++)
        {
            PlayerCharacter player = _seamlessSpawnedPlayers[i];
            if (player == null)
                continue;
            player.transform.DOKill(false);
            Destroy(player.gameObject);
        }
        _seamlessSpawnedPlayers.Clear();
        _playerParty.Clear();
        _reserveParty.Clear();
        _battlePartyRoster.Clear();
    }

    private void ResetSeamlessBattleState()
    {
        CancelPartyWaveTransition();
        BattleScenarioSubjectResolver.ClearRegistry(_battleParticipantIdRegistry);
        _battleParticipantIdRegistry = null;
        _battleScenarioRuntime = null;
        _battleScenarioExecutionGate = null;
        _battleGameModuleActionRunner = null;
        _battleParticipantCommandRunner = null;
        _battleCinematicRunner = null;
        _battleTweenCinematicService = null;
        _turnQteModuleController = null;
        _aimShooterModuleController = null;
        _scenarioDefeatPublished.Clear();
        _turnQueue.Clear();
        _playerParty.Clear();
        _reserveParty.Clear();
        _battlePartyRoster.Clear();
        _reservedEnemyActionByActor.Clear();
        _pendingBattleScenarioData = null;
        _currentActorIndex = 0;
        _battleTurnCounter = 0;
        _isRunInProgress = false;
        _allowEscape = true;
        _isAbortCleanupInProgress = false;
        _isBattleActive = false;
        _isBattleEnding = false;
        _rewardCommitted = false;
        _playerPreemptiveAttackAvailable = false;
        _lastRewardResult = null;
        _hasSeamlessBgmSnapshot = false;
        _seamlessBgmSnapshot = BgmPlaybackSnapshot.Stopped;
        CurrentState = BattleState.Init;
    }

    private void RestoreSeamlessBattlePresentation()
    {
        if (_battleUICanvas != null)
            _battleUICanvas.SetActive(false);

        CameraController cameraController = CameraController.Instance;
        if (cameraController != null && _seamlessCameraDefaultTarget.IsValid)
            cameraController.RestoreDefaultTarget(_seamlessCameraDefaultTarget, 0.4f);

        _seamlessCameraDefaultTarget = default;
    }
    #endregion

    #region [ Static Utilities & Event Bridges ]
    public static void ExecuteItemEffect(CharacterBase target, ItemData item)
    {
        if (target == null || item == null) return;

        int previousHp = target.CurrentHP;
        int previousAp = target.CurrentAP;
        if (!ItemEffectService.TryApply(item, target, true, out string error))
        {
            Debug.LogWarning($"[BattleManager] Item effect failed: {error}");
            return;
        }

        if (Instance == null) return;
        int hpDelta = previousHp - target.CurrentHP;
        if (hpDelta != 0)
            Instance.InvokeDamageEvent(target, hpDelta, false, previousHp);
        if (target is PlayerCharacter player && player.CurrentAP != previousAp)
            Instance.InvokeAPChangedEvent(player, player.CurrentAP);
    }


    public void InvokeDamageEvent(CharacterBase target, int damage, bool isPerfect)
    {
        int previousHp = target != null ? Mathf.Clamp(target.CurrentHP + Mathf.Max(0, damage), 0, target.MaxHP) : 0;
        InvokeDamageEvent(null, target, damage, isPerfect, previousHp);
    }

    public void InvokeDamageEvent(CharacterBase target, int damage, bool isPerfect, int previousHp)
    {
        InvokeDamageEvent(null, target, damage, isPerfect, previousHp);
    }

    public void InvokeDamageEvent(
        CharacterBase source,
        CharacterBase target,
        int damage,
        bool isCritical,
        int previousHp)
    {
        if (target != null)
        {
            PublishEnemyHpScenarioEvent(
                target,
                previousHp,
                target.CurrentHP,
                target.MaxHP,
                BattleRuleTiming.AfterCurrentSkill);
        }

        RefreshBattleSessionParticipants();
        OnDamageDealt?.Invoke(target, damage, isCritical);
        PublishDamageFeedback(source, target, damage, isCritical);
    }

    public void InvokeMissFeedback(CharacterBase source, CharacterBase target)
    {
        if (target == null)
            return;

        OnDamageFeedbackRequested?.Invoke(new BattleDamageFeedback(
            source,
            target,
            0,
            false,
            BattleDamageFeedbackKind.Miss));
    }

    public void InvokeAPChangedEvent(PlayerCharacter player, int newAP)
    {
        RefreshBattleSessionParticipants();
        OnAPChanged?.Invoke(player, newAP);
    }

    [Obsolete("Use InvokeAPChangedEvent.")]
    public void InvokeMPChangedEvent(PlayerCharacter player, int newAP)
    {
        InvokeAPChangedEvent(player, newAP);
    }

    private void NotifyDamageDealt(CharacterBase target, int damage, bool isPerfect)
    {
        NotifyDamageDealt(null, target, damage, isPerfect);
    }

    private void NotifyDamageDealt(
        CharacterBase source,
        CharacterBase target,
        int damage,
        bool isCritical)
    {
        RefreshBattleSessionParticipants();
        OnDamageDealt?.Invoke(target, damage, isCritical);
        PublishDamageFeedback(source, target, damage, isCritical);
    }

    private void PublishDamageFeedback(
        CharacterBase source,
        CharacterBase target,
        int damage,
        bool isCritical)
    {
        if (target == null || damage <= 0)
            return;

        AudioManager.Instance?.PlayCombatHitSfx();
        OnDamageFeedbackRequested?.Invoke(new BattleDamageFeedback(
            source,
            target,
            damage,
            isCritical,
            BattleDamageFeedbackKind.Damage));
    }
    private void NotifyPlayerTurnStarted(PlayerCharacter player)
    {
        OnPlayerTurnStarted?.Invoke(player);
    }

    private void NotifyEnemyActionStarted(EnemyCharacter enemy, EnemyAttackType attackType)
    {
        OnEnemyActionStarted?.Invoke(enemy, attackType);
    }

    private void NotifyTargetSelectionStarted(PlayerMenuAction action)
    {
        OnTargetSelectionStarted?.Invoke(action);
    }

    public static void SetGhostTrail(CharacterBase character, bool active)
    {
        if (character == null) return;
        var trail = character.GetComponentInChildren<CharacterGhostTrail>();
        if (trail != null) trail.SetTrailActive(active);
    }
    #endregion
}
