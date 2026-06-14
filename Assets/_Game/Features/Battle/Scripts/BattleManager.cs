using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;
using Unity.Cinemachine;
using UnityEngine.UI;
using Sirenix.OdinInspector;

/// <summary>
/// 전투의 전체 흐름을 제어하는 중앙 매니저 (Singleton & State Machine 기반).
/// 옵저버(Observer) 패턴을 활용하여 UI와의 결합도를 낮췄습니다.
/// </summary>
public class BattleManager : MonoBehaviour, ISceneRevealGate
{
    public static BattleManager Instance { get; private set; }

    #region [ Events ]
    public event Action<BattleState>                OnStateChanged;
    public event Action<List<PlayerCharacter>, List<EnemyCharacter>> OnBattleStarted;
    public event Action<List<CharacterBase>>        OnTurnQueueUpdated;  
    public event Action<PlayerCharacter>            OnPlayerTurnStarted;  
    public event Action<EnemyCharacter, EnemyAttackType> OnEnemyActionStarted;
    public event Action<CharacterBase, int, bool>   OnDamageDealt;        
    public event Action<PlayerCharacter, int>       OnMPChanged;          
    public event Action<bool>                       OnBattleEnded;        
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

    [BoxGroup("System Rules"), LabelWidth(140)] [Tooltip("턴 시작 시 회복되는 MP량")]
    public int _mpPerTurn = 5;   
    [BoxGroup("System Rules"), LabelWidth(140)] [Tooltip("패링 퍼펙트 성공 시 회복되는 MP량")]
    public int _mpOnParryPerfect = 20; 
    [BoxGroup("System Rules"), LabelWidth(140)] [Tooltip("우측 상단에 표시될 턴 대기열 아이콘의 최대 개수")]
    [SerializeField] private int _maxTurnQueueSize = 8;
    [BoxGroup("System Rules"), LabelWidth(140)] [Tooltip("실제로 UI에 노출할 턴 대기열 아이콘 수")]
    [SerializeField] private int _visibleTurnQueueSize = 4;

    [Header("Seamless & Scene Settings")]
    [Tooltip("체크 시 전용 배틀 씬으로 동작하며, Start()에서 자동으로 전투 셋업을 시작합니다.")]
    [SerializeField] private bool _isDedicatedBattleScene = false;
    [Tooltip("전투 종료 후 돌아갈 씬이 없을 경우 이동할 기본 씬")]
    [SerializeField] private string _fallbackSceneName = "LobbyScene";
    [SerializeField] private float _postRunEnemyDisableDuration = 3f;
    [SerializeField] private float _postRunEnemyAlpha = 0.5f;
    
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
    
    public SkillData CurrentPendingSkill { get; private set; }
    public ItemData  CurrentPendingItem  { get; private set; }

    private readonly List<CharacterBase> _turnQueue = new List<CharacterBase>();
    private int _currentActorIndex = 0;

    // 캐싱된 대기 시간 (가비지 최적화)
    private readonly WaitForSeconds _waitShort  = new WaitForSeconds(0.4f);
    private readonly WaitForSeconds _waitMedium = new WaitForSeconds(0.8f);

    private PlayerCharacter _pendingActor;
    private PlayerMenuAction _pendingAction;
    private int _battleTurnCounter = 0;
    private Transform _battleCameraFocusPoint;
    private readonly Dictionary<EnemyCharacter, EnemyQueuedAction> _reservedEnemyActionByActor = new Dictionary<EnemyCharacter, EnemyQueuedAction>();
    private bool _isRunInProgress;
    private bool _isBattleEnding;
    private IEncounterSource _activeEncounterSource;
    private PlayerController _activeEncounterPlayer;
    private bool _isReadyToReveal = true;
    private BattleScenarioData _pendingBattleScenarioData;
    private BattleScenarioRuntime _battleScenarioRuntime;
    private BattleScenarioExecutionGate _battleScenarioExecutionGate;
    #endregion

    public bool IsReadyToReveal => !_isDedicatedBattleScene || _isReadyToReveal;

    public void SetBattleScenarioData(BattleScenarioData scenarioData)
    {
        _pendingBattleScenarioData = scenarioData;
    }

    private struct EnemyQueuedAction
    {
        public EnemyAction Action;
        public SkillData Skill;
        public int TurnsRemaining;
    }

    private Transform EnsureBattleCameraFocusPoint()
    {
        if (_battleCameraFocusPoint != null) return _battleCameraFocusPoint;
        GameObject go = new GameObject("BattleCameraFocusPoint");
        _battleCameraFocusPoint = go.transform;
        return _battleCameraFocusPoint;
    }

    private Transform GetPrimaryAlivePlayerTransform()
    {
        int idx = _playerParty.FindIndex(p => p != null && p.IsAlive);
        if (idx < 0) return null;
        return _playerParty[idx].transform;
    }

    private void FocusCameraBetween(Transform a, Transform b)
    {
        if (a == null || b == null) return;

        Transform focus = EnsureBattleCameraFocusPoint();
        focus.position = (a.position + b.position) * 0.5f;
        CameraController.Instance?.SetTarget(focus);
    }

    private List<CharacterBase> GetVisiblePredictedTurnQueue()
    {
        List<CharacterBase> visible = new List<CharacterBase>();

        if (_turnQueue.Count == 0) return visible;

        for (int i = _currentActorIndex; i < _turnQueue.Count && visible.Count < _visibleTurnQueueSize; i++)
        {
            CharacterBase actor = _turnQueue[i];
            if (actor != null && actor.IsAlive)
                visible.Add(actor);
        }

        if (visible.Count >= _visibleTurnQueueSize)
            return visible;

        List<CharacterBase> aliveActors = new List<CharacterBase>();
        foreach (var p in _playerParty) if (p != null && p.IsAlive) aliveActors.Add(p);
        foreach (var e in _enemies) if (e != null && e.IsAlive) aliveActors.Add(e);

        aliveActors.Sort((a, b) => b.SPD.CompareTo(a.SPD));
        int refillIndex = 0;
        while (visible.Count < _visibleTurnQueueSize && aliveActors.Count > 0)
        {
            visible.Add(aliveActors[refillIndex % aliveActors.Count]);
            refillIndex++;
        }

        return visible;
    }

    private void BroadcastVisibleTurnQueue()
    {
        OnTurnQueueUpdated?.Invoke(GetVisiblePredictedTurnQueue());
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
        CameraController.Instance?.ResetCamera(0f);

        yield return null;
        yield return new WaitForEndOfFrame();

        Canvas.ForceUpdateCanvases();
        BattleUIController.Instance?.NormalizeForCurrentResolution();
        CameraController.Instance?.ResetCamera(0f);

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

        if (global != null)
        {
            global.PendingBattleScenario = null;
        }

        BattleEncounterMemoryRecorder.RecordBattleStarted(scenarioData, global, fallbackEncounterId);
        _battleScenarioRuntime = BattleEncounterMemoryRecorder.CreateRuntime(scenarioData, global, fallbackEncounterId);
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
        var registry = new ActionAdapterRegistry();
        registry.Register(new FlowWaitActionAdapter());
        registry.Register(new DialogueWaitActionAdapter());
        registry.Register(new BgmCrossfadeActionAdapter());
        registry.Register(new ScreenFadeActionAdapter());
        registry.Register(new ModuleSwitchActionAdapter());
        registry.Register(new ModuleStartActionAdapter());
        registry.Register(new BattleSkillTimelineActionAdapter());
        return new ActionDirector(registry);
    }

    private ActionExecutionContext CreateBattleScenarioActionContext()
    {
        BattleScenarioData scenarioData = _battleScenarioRuntime != null ? _battleScenarioRuntime.ScenarioData : null;
        return BattleScenarioActionContextFactory.Create(
            scenarioData,
            null,
            new BattleSkillTimelineRunner(this));
    }

    #region [ Initialization ]
    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        _isReadyToReveal = !_isDedicatedBattleScene;
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
            case BattleState.TurnCalc:    StartCoroutine(TurnCalcRoutine()); break;
            case BattleState.EnemyAction: StartCoroutine(EnemyActionRoutine()); break;
            case BattleState.BattleEnd:   StartCoroutine(BattleEndRoutine()); break;
        }
    }
    #endregion

    #region [ Battle Setup & Intro ]
    /// <summary>
    /// 오버월드 맵 위에서 씬 전환 없이 그대로 전투를 시작할 때 호출됩니다.
    /// </summary>
    public void StartSeamlessBattle(List<EnemyData> encounterEnemies, PlayerController playerCtrl, IEncounterSource encounterSource = null)
    {
        _activeEncounterSource = encounterSource;
        _activeEncounterPlayer = playerCtrl;
        StartCoroutine(StartSeamlessBattleRoutine(encounterEnemies, playerCtrl));
    }

    private IEnumerator StartSeamlessBattleRoutine(List<EnemyData> encounterEnemies, PlayerController playerCtrl)
    {
        Debug.Log("<color=cyan>[BattleManager] 심리스 전투 연출 시작!</color>");

        if (GlobalDataManager.Instance != null && GlobalDataManager.Instance.PendingBattleBGM != null)
            AudioManager.Instance?.CrossFadeBGM(GlobalDataManager.Instance.PendingBattleBGM, 0.8f);

        yield return StartCoroutine(WarmupBattlePresentation());

        _playerParty.Clear();
        PlayerCharacter playerChar = playerCtrl.GetComponent<PlayerCharacter>();
        
        if (playerChar != null)
        {
            var global = GlobalDataManager.Instance;
            
            if (global != null && global.Party.Count == 0)
            {
                global.InitializePartyFromScene(playerChar);
            }

            if (global != null && global.Party.Count > 0)
            {
                playerChar.LoadDataFromGlobal(global.Party[0]); 
            }
            
            _playerParty.Add(playerChar);
        }

        // 2. 적군 셋업
        _enemies.Clear();
        var pm = PositionManager.Instance;
        
        for (int i = 0; i < encounterEnemies.Count; i++)
        {
            GameObject enemyPrefab = ResolveEnemyBattlePrefab(encounterEnemies[i]);
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
                    Debug.LogError($"[BattleManager] 전투 프리팹 '{enemyPrefab.name}' 에 EnemyCharacter 컴포넌트가 없어 적을 생성할 수 없습니다.", enemyObj);
                }
            }
            else
            {
                Debug.LogError($"[BattleManager] 전투 적 프리팹을 찾지 못했습니다. EnemyData={encounterEnemies[i]?.EnemyName}");
            }
        }

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
        OnBattleStarted?.Invoke(_playerParty, _enemies);
        _battleNarrationConfig?.ResetRuntimeState();
        _battleTurnCounter = 0;
        _isBattleEnding = false;
        RequestNarration(BattleNarrationFormatter.BattleStart());
        TryRequestFlavorNarration();
        yield return StartCoroutine(WaitForNarrationToFinish());
        ChangeState(BattleState.Init);
        ChangeState(BattleState.TurnCalc);
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

        _playerParty.Clear();
        if (_playerBasePrefab != null)
        {
            int partyCount = (global != null && global.Party.Count > 0) ? global.Party.Count : 1;
            for (int i = 0; i < partyCount; i++)
            {
                GameObject pObj = Instantiate(_playerBasePrefab, Vector3.zero, Quaternion.identity);
                if (pObj.TryGetComponent(out PlayerCharacter pChar))
                {
                    _playerParty.Add(pChar);
                    
                    if (global != null && global.Party.Count > i)
                        pChar.LoadDataFromGlobal(global.Party[i]);

                    var ctrl = pChar.GetComponent<PlayerController>();
                    if (ctrl != null) ctrl.SetBattleMode(true); 
                }
            }
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
                GameObject enemyPrefab = ResolveEnemyBattlePrefab(global.PendingEnemies[i]);
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
                        Debug.LogError($"[BattleManager] 전투 프리팹 '{enemyPrefab.name}' 에 EnemyCharacter 컴포넌트가 없어 적을 생성할 수 없습니다.", enemyObj);
                    }
                }
                else
                {
                    Debug.LogError($"[BattleManager] 전투 적 프리팹을 찾지 못했습니다. EnemyData={global.PendingEnemies[i]?.EnemyName}");
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
        OnBattleStarted?.Invoke(_playerParty, _enemies);
        Canvas.ForceUpdateCanvases();
        yield return null;
        Canvas.ForceUpdateCanvases();
        CameraController.Instance?.ResetCamera(0f);
        _isReadyToReveal = true;

        _battleNarrationConfig?.ResetRuntimeState();
        _battleTurnCounter = 0;
        _isBattleEnding = false;
        RequestNarration(BattleNarrationFormatter.BattleStart());
        TryRequestFlavorNarration();
        yield return StartCoroutine(WaitForNarrationToFinish());
        ChangeState(BattleState.TurnCalc);
    }

    private GameObject ResolveEnemyBattlePrefab(EnemyData enemyData)
    {
        if (enemyData != null && enemyData.BattlePrefab != null)
            return enemyData.BattlePrefab;

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
                DefenseRequirement.ParryOrDodge => EnemyAttackType.MeleeClose, 
                
                DefenseRequirement.JumpOnly => EnemyAttackType.JumpOnly,
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
    private IEnumerator TurnCalcRoutine()
    {
        yield return null;
        _turnQueue.Clear();

        if (_enemies == null || _enemies.Count == 0)
        {
            Debug.LogError("[BattleManager] 전투 시작 시 적 리스트가 비어 있습니다. BattlePrefab 또는 EnemyCharacter 설정을 확인해주세요.");
            yield break;
        }

        List<CharacterBase> aliveChars = new List<CharacterBase>();
        
        foreach (var p in _playerParty) if (p != null && p.IsAlive) aliveChars.Add(p);
        foreach (var e in _enemies)     if (e != null && e.IsAlive) aliveChars.Add(e);

        // 전투 종료 조건 확인
        if (aliveChars.Count == 0 || CheckVictory() || CheckDefeat()) 
        { 
            EndAction(); 
            yield break; 
        }

        // 속도(SPD) 기반 턴 정렬 및 큐 생성
        for (int i = 0; i < _maxTurnQueueSize; i++) 
        {
            aliveChars.Sort((a, b) => b.SPD.CompareTo(a.SPD)); 
            _turnQueue.Add(aliveChars[i % aliveChars.Count]); 
        }

        _currentActorIndex = 0;
        BroadcastVisibleTurnQueue();
        yield return _waitShort;
        
        AdvanceTurn();
    }

    private void AdvanceTurn()
    {
        // 큐를 다 소진했다면 다시 턴 계산
        if (_currentActorIndex >= _turnQueue.Count) { ChangeState(BattleState.TurnCalc); return; }

        var actor = _turnQueue[_currentActorIndex++];
        if (actor == null || !actor.IsAlive) { BroadcastVisibleTurnQueue(); AdvanceTurn(); return; }

        // 상태이상 틱 데미지 처리
        actor.ProcessEffects();
        if (!actor.IsAlive) { BroadcastVisibleTurnQueue(); AdvanceTurn(); return; }

        // 액터 진영에 따른 턴 분기
        if (actor is PlayerCharacter player)
        {
            _battleTurnCounter++;
            StartCoroutine(BeginPlayerTurnRoutine(player));
        }
        else if (actor is EnemyCharacter)
        {
            StartCoroutine(BeginEnemyTurnRoutine());
        }
    }

    private IEnumerator BeginPlayerTurnRoutine(PlayerCharacter player)
    {
        ResetAllPlayerBattlePoses();
        player.GetComponent<PlayerController>()?.PlayBattleAnim(PlayerCharacter.HashBattleIdle);
        player.HealMP(_mpPerTurn);
        OnMPChanged?.Invoke(player, player.CurrentMP);
        if (player.TryShowBattleSpeech(BattleSpeechTrigger.TurnStart, null, null, _battleTurnCounter))
            yield return StartCoroutine(player.WaitForBattleSpeech());
        TryRequestFlavorNarration();
        yield return StartCoroutine(WaitForNarrationToFinish());
        OnPlayerTurnStarted?.Invoke(player);
        ChangeState(BattleState.PlayerActionSelect);
        yield break;
    }

    private IEnumerator BeginEnemyTurnRoutine()
    {
        ResetAllPlayerBattlePoses();
        yield return StartCoroutine(WaitForNarrationToFinish());
        ChangeState(BattleState.EnemyAction);
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
            player.GetComponent<PlayerController>()?.SetBattleSortingBoost(active ? boost : 0);
        }
        else if (actor is EnemyCharacter enemy)
        {
            SpriteRenderer sr = enemy.GetComponent<SpriteRenderer>();
            if (sr != null)
                sr.sortingOrder = active ? boost : 0;
        }
    }
    #endregion

    #region [ Player Input Routing ]
    public void OnPlayerActionSelected(PlayerCharacter actor, PlayerMenuAction action)
    {
        _pendingActor = actor;
        _pendingAction = action;

        // 새로운 행동을 선택했으므로 이전 스킬/아이템 정보 초기화
        CurrentPendingSkill = null;
        CurrentPendingItem = null;

        if (action != PlayerMenuAction.Run)
        {
            actor.PlayBattleAnim(PlayerCharacter.HashBattleReady);
        }

        if (action == PlayerMenuAction.Attack)
        {
            OnTargetSelectionStarted?.Invoke(action);
        }
        else if (action == PlayerMenuAction.Skill || action == PlayerMenuAction.Item)
        {
        }
        else if (action == PlayerMenuAction.Run) 
        {
            StartCoroutine(RunRoutine());
        }
    }

    public void OnSubMenuActionSelected(PlayerCharacter actor, PlayerMenuAction action, SkillData skill, ItemData item)
    {
        _pendingActor = actor;
        _pendingAction = action;
        CurrentPendingSkill = skill; 
        CurrentPendingItem = item;

        bool isAoE = (skill != null && skill.IsAoE) || (item != null && item.IsAoE);
        
        if (isAoE) ConfirmTargetAndExecute(-1); 
        else       OnTargetSelectionStarted?.Invoke(action);
    }

    public void CancelActionSelection() 
    {
        _pendingActor?.PlayBattleAnim(PlayerCharacter.HashBattleIdle);
        ChangeState(BattleState.PlayerActionSelect);
    }

    public void CancelTargetSelection() 
    {
        _pendingActor?.PlayBattleAnim(PlayerCharacter.HashBattleIdle);
        ChangeState(BattleState.PlayerActionSelect);
    }

    public void ConfirmTargetAndExecute(int targetIndex)
    {
        if (CurrentState == BattleState.ActionExecute) return;
        if (_pendingAction == PlayerMenuAction.Attack)
        {
            ChangeState(BattleState.ActionExecute);
            StartCoroutine(ExecuteAttack(_pendingActor, targetIndex));
        }
        else if (_pendingAction == PlayerMenuAction.Skill && CurrentPendingSkill != null)
        {
            if (_pendingActor.CurrentMP < CurrentPendingSkill.MPCost)
            {
                RequestNarration(new BattleNarrationMessage("MP가 부족하다.", BattleNarrationStyle.Warning, BattleNarrationPriority.High, 0.2f, true));
                _pendingActor?.PlayBattleAnim(PlayerCharacter.HashBattleIdle);
                CurrentPendingSkill = null;
                CurrentPendingItem = null;
                ChangeState(BattleState.PlayerActionSelect);
                return;
            }

            ChangeState(BattleState.ActionExecute);
            StartCoroutine(ExecuteSkill(_pendingActor, targetIndex, CurrentPendingSkill));
        }
        else if (_pendingAction == PlayerMenuAction.Item && CurrentPendingItem != null)
        {
            ChangeState(BattleState.ActionExecute);
            StartCoroutine(ExecuteItem(_pendingActor, targetIndex, CurrentPendingItem));
        }
        else
            EndAction();
    }
    #endregion

    #region [ Action Executions ]
    private void EndAction()
    {
        Time.timeScale = 1.0f;
        QTEManager.Instance?.ForceStop();
        ResetAllPlayerBattlePoses();
        _pendingActor = null;
        CurrentPendingSkill = null;
        CurrentPendingItem = null;
        CameraController.Instance?.ResetCamera(0.4f);

        BroadcastVisibleTurnQueue();

        if (CheckVictory() || CheckDefeat()) ChangeState(BattleState.BattleEnd);
        else AdvanceTurn();
    }

    private IEnumerator ExecuteAttack(PlayerCharacter actor, int targetIndex)
    {
        if (targetIndex >= _enemies.Count || !_enemies[targetIndex].IsAlive) { EndAction(); yield break; }
        
        var target = _enemies[targetIndex];
        var pm = PositionManager.Instance;

        // 전투 중 카메라 워킹/줌 연출 비활성화

        // 하드코딩 제거: 인스펙터 변수 참조
        Vector3 frontPos = target.transform.position + _meleeAttackOffset; 
        
        actor.PlayBattleAnim(PlayerCharacter.HashBattleMove);
        SetActorForeground(actor, true);
        SetGhostTrail(actor, true);
        yield return actor.transform.DOMove(frontPos, 0.2f).SetEase(Ease.OutCubic).WaitForCompletion();

        Vector3 pullBackPos = frontPos + _meleePullbackOffset;
        yield return actor.transform.DOMove(pullBackPos, 0.15f).SetEase(Ease.OutBack).WaitForCompletion();

        // 타겟의 반대편으로 지나가는 연출 (X축 대칭)
        Vector3 behindPos = target.transform.position + new Vector3(-_meleeAttackOffset.x, 0, 0);
        
        actor.PlayBasicAttackEffect();
        actor.PlayBattleAnim(PlayerCharacter.HashAttack); 
        actor.transform.DOMove(behindPos, 0.15f).SetEase(Ease.InExpo);

        yield return new WaitForSeconds(_playerAttackHitDelay);

        int previousHp = target.CurrentHP;
        int dmg = target.TakeDamage(actor.ATK);
        CameraController.Instance?.PlayHeavySlam(Vector3.right, 0.75f, true);
        PublishEnemyHpScenarioEvent(target, previousHp, target.CurrentHP, target.MaxHP, BattleRuleTiming.AfterCurrentAction);
        OnDamageDealt?.Invoke(target, dmg, false);
        
        yield return new WaitForSeconds(_playerAttackRecoverDelay); 
        SetGhostTrail(actor, false);
        SetActorForeground(actor, false);

        // 제자리 복귀
        int idx = _playerParty.IndexOf(actor);
        actor.PlayBattleAnim(PlayerCharacter.HashBattleMove);
        SetGhostTrail(actor, true);
        yield return actor.transform.DOJump(pm.GetPlayerDefaultPos(idx), 0.5f, 1, 0.3f).SetEase(Ease.OutQuad).WaitForCompletion();
        SetGhostTrail(actor, false);
        
        actor.PlayBattleAnim(PlayerCharacter.HashBattleIdle);
        CameraController.Instance?.ResetCamera(0.4f);

        yield return StartCoroutine(WaitForNarrationToFinish());
        yield return StartCoroutine(FlushBattleScenarioEvents(BattleRuleTiming.AfterCurrentAction));

        EndAction();
    }
    
    private IEnumerator ExecuteSkill(PlayerCharacter actor, int targetIndex, SkillData skill)
    {
        actor.ConsumeMP(skill.MPCost);
        OnMPChanged?.Invoke(actor, actor.CurrentMP);
        if (actor.TryShowBattleSpeech(BattleSpeechTrigger.SkillUse, skill, null, _battleTurnCounter))
            yield return StartCoroutine(actor.WaitForBattleSpeech());
        List<CharacterBase> targets = new List<CharacterBase>();
        if (skill.IsAoE)
        {
            if (skill.TargetType == TargetAreaType.AllyOnly) targets.AddRange(_playerParty.FindAll(p => p.IsAlive));
            else targets.AddRange(_enemies.FindAll(e => e.IsAlive));
        }
        else
        {
            if (skill.TargetType == TargetAreaType.AllyOnly) targets.Add(_playerParty[targetIndex]);
            else targets.Add(_enemies[targetIndex]);
        }

        if (targets.Count == 0) { EndAction(); yield break; }

        // 전투 중 카메라 워킹/줌 연출 비활성화

        Vector3 originalPos = PositionManager.Instance.GetPlayerDefaultPos(_playerParty.IndexOf(actor));

        SkillContext context = new SkillContext()
        {
            Actor = actor,
            Targets = targets,
            CurrentDamageMultiplier = 1.0f,
            IsPerfectQTE = false
        };

        if (skill.ActionTimeline != null)
        {
            foreach (var block in skill.ActionTimeline)
            {
                context.Targets.RemoveAll(t => t == null || !t.IsAlive);
                if (context.Targets.Count == 0) break; 
                yield return StartCoroutine(block.Execute(context)); 
            }
        }

        if (Vector3.Distance(actor.transform.position, originalPos) > 0.1f)
        {
            actor.PlayBattleAnim(PlayerCharacter.HashBattleMove);
            SetActorForeground(actor, true);
            SetGhostTrail(actor, true);
            yield return actor.transform.DOMove(originalPos, 0.3f).SetEase(Ease.OutBack).WaitForCompletion();
            SetGhostTrail(actor, false);
            SetActorForeground(actor, false);
        }

        actor.PlayBattleAnim(PlayerCharacter.HashBattleIdle);
        CameraController.Instance?.ResetCamera(0.4f); 
        yield return StartCoroutine(WaitForNarrationToFinish());
        yield return StartCoroutine(FlushBattleScenarioEvents(BattleRuleTiming.AfterCurrentSkill));
        
        EndAction();
    }

    private IEnumerator ExecuteItem(PlayerCharacter actor, int targetIndex, ItemData item)
    {
        List<CharacterBase> targets = new List<CharacterBase>();
        
        if (item.IsAoE)
        {
            if (item.TargetType == TargetAreaType.AllyOnly) targets.AddRange(_playerParty.FindAll(p => p.IsAlive));
            else targets.AddRange(_enemies.FindAll(e => e.IsAlive));
        }
        else
        {
            if (item.TargetType == TargetAreaType.AllyOnly)
            {
                if (targetIndex >= 0 && targetIndex < _playerParty.Count) targets.Add(_playerParty[targetIndex]);
            }
            else
            {
                if (targetIndex >= 0 && targetIndex < _enemies.Count && _enemies[targetIndex].IsAlive) targets.Add(_enemies[targetIndex]);
            }
        }

        if (targets.Count == 0) { EndAction(); yield break; }

        var actorCtrl = actor.GetComponent<PlayerController>();
        var pm = PositionManager.Instance;

        actorCtrl?.PlayBattleAnim(PlayerCharacter.HashBattleMove);
        SetActorForeground(actor, true);
        yield return actor.transform.DOMove(actor.transform.position + Vector3.right * 1f, 0.2f).SetEase(Ease.OutQuad).WaitForCompletion();
        actorCtrl?.PlayBattleAnim(PlayerCharacter.HashBattleIdle);

        yield return new WaitForSeconds(0.3f);

        foreach (var t in targets) ExecuteItemEffect(t, item);

        yield return new WaitForSeconds(0.5f);

        int idx = _playerParty.IndexOf(actor);
        actorCtrl?.PlayBattleAnim(PlayerCharacter.HashBattleMove);
        yield return actor.transform.DOMove(pm.GetPlayerDefaultPos(idx), 0.3f).SetEase(Ease.OutBack).WaitForCompletion();
        SetActorForeground(actor, false);
        actorCtrl?.PlayBattleAnim(PlayerCharacter.HashBattleIdle);

        yield return StartCoroutine(WaitForNarrationToFinish());

        EndAction(); 
    }
    #endregion

    #region [ Enemy Action & QTE Handling ]
    private IEnumerator EnemyActionRoutine()
    {
        var enemy = _turnQueue[_currentActorIndex - 1] as EnemyCharacter;
        if (enemy == null) { EndAction(); yield break; }

        SkillData enemySkill = null;
        EnemyAction action;
        bool isExecutingReservedAction = false;

        if (_reservedEnemyActionByActor.TryGetValue(enemy, out EnemyQueuedAction reservedAction))
        {
            reservedAction.TurnsRemaining--;
            if (reservedAction.TurnsRemaining > 0)
            {
                _reservedEnemyActionByActor[enemy] = reservedAction;
                EndAction();
                yield break;
            }

            action = reservedAction.Action;
            enemySkill = reservedAction.Skill;
            _reservedEnemyActionByActor.Remove(enemy);
            isExecutingReservedAction = true;
        }
        else
        {
            action = enemy.DecideAction();
            // 🚨 수정됨: action을 넘겨서 UseSkill, UseStrongSkill 모두 정상적으로 스킬을 뽑아오게 변경
            enemySkill = GetEnemySequenceSkill(enemy, action); 
        }

        var attackType = action switch
        {
            // 🚨 수정됨: 강한 스킬(UseStrongSkill)일 때도 스킬 타입을 판정하도록 추가
            EnemyAction.UseSkill when enemySkill != null => ResolveEnemySkillAttackType(enemySkill),
            EnemyAction.UseStrongSkill when enemySkill != null => ResolveEnemySkillAttackType(enemySkill), 
            EnemyAction.EnragedAttack => EnemyAttackType.AoEAll,
            _ => EnemyAttackType.MeleeClose
        };

        OnEnemyActionStarted?.Invoke(enemy, attackType);
        
        // 🚨 수정됨: 강한 스킬(UseStrongSkill)일 경우에만 예고를 띄우도록 조건 변경
        bool shouldTelegraphSkillThisTurn = enemy.Data != null
            && enemy.Data.TelegraphStrongSkill
            && action == EnemyAction.UseStrongSkill 
            && enemySkill != null
            && !isExecutingReservedAction
            && !_reservedEnemyActionByActor.ContainsKey(enemy);

        if (shouldTelegraphSkillThisTurn)
        {
            string enemyName = enemy != null && enemy.Data != null && !string.IsNullOrWhiteSpace(enemy.Data.EnemyName) ? enemy.Data.EnemyName : "적";
            string warnText = $"{enemyName}가 강한 공격을 준비한다...";
            RequestNarration(new BattleNarrationMessage(warnText, BattleNarrationStyle.Warning, BattleNarrationPriority.High, 0.4f, true));
            yield return StartCoroutine(WaitForNarrationToFinish());

            _reservedEnemyActionByActor[enemy] = new EnemyQueuedAction
            {
                Action = action,
                Skill = enemySkill,
                TurnsRemaining = Mathf.Max(1, enemy.Data.TelegraphTurns)
            };
            EndAction();
            yield break;
        }
        if ((action == EnemyAction.UseSkill || action == EnemyAction.UseStrongSkill) && enemySkill != null)
        {
            if (enemy.TryShowBattleSpeech(BattleSpeechTrigger.SkillUse, enemySkill, null, _battleTurnCounter, 1.2f))
                yield return StartCoroutine(enemy.WaitForBattleSpeech());
            else
                yield return new WaitForSeconds(0.18f);

            yield return StartCoroutine(ExecuteEnemySequenceSkill(enemy, enemySkill));
        }
        else if (attackType == EnemyAttackType.MeleeClose)
        {
            int targetIdx = _playerParty.FindIndex(p => p.IsAlive);
            if (targetIdx >= 0)
            {
                var target = _playerParty[targetIdx];
                var targetCtrl = target.GetComponent<PlayerController>();
                bool movedToCenter = enemy.Data == null || !enemy.Data.IsLargeEnemy;

                yield return StartCoroutine(MoveEnemyToCenterIfNeeded(enemy));
                SetActorForeground(enemy, true);

                enemy.PlayBasicAttackEffect();
                enemy.PlayBattleAnim(EnemyCharacter.HashAttack);

                bool qteFinished = false;
                DefenseInput finalInput = DefenseInput.None;
                QTEManager.QTEGrade finalGrade = QTEManager.QTEGrade.Miss;

                // 공격 애니메이션은 한 번만 재생하고, QTE 판정은 별도로 유지합니다.
                targetCtrl?.PrepareDefenseWindow();
                QTEManager.Instance.StartDefenseQTE(_enemyDefenseQTEWindow, 1.0f, (input, grade) =>
                {
                    finalInput = input;
                    finalGrade = grade;
                    qteFinished = true;
                });
                yield return new WaitForSeconds(_enemyAttackVisualDuration);
                enemy.PlayBattleAnim(EnemyCharacter.HashBattleIdle);
                yield return new WaitUntil(() => qteFinished);

                // 🚨 데미지 및 UI 연출 판정
                if (finalGrade == QTEManager.QTEGrade.Miss)
                {
                    int dmg = target.TakePureDamage(enemy.ATK); 
                    targetCtrl?.PlayHurtEffect();
                    CameraController.Instance?.PlayHeavySlam(Vector3.left, 1.0f, true);
                    
                    // 🚨 핵심 해결: UI에게 데미지 달았다고 방송! (이게 빠져서 체력바가 안 깎였음)
                    OnDamageDealt?.Invoke(target, dmg, false);
                }
                else
                {
                    targetCtrl?.ConfirmDefenseSuccess(finalInput);
                    if (finalInput == DefenseInput.Parry && finalGrade == QTEManager.QTEGrade.Perfect)
                    {
                        target.HealMP(_mpOnParryPerfect);
                        OnMPChanged?.Invoke(target, target.CurrentMP);
                    }
                    if (finalInput == DefenseInput.Dodge || finalInput == DefenseInput.Jump)
                        yield return targetCtrl != null ? StartCoroutine(targetCtrl.WaitForDefenseVisualComplete(0.5f)) : null;
                }

                yield return new WaitForSeconds(_enemyPostHitDelay);
                targetCtrl?.ResetDefenseReactionLock();
                if (target != null && target.IsAlive)
                    targetCtrl?.PlayBattleAnim(PlayerCharacter.HashBattleIdle);
                
                // 적군 제자리 복귀
                if (movedToCenter)
                {
                    enemy.PlayBattleAnim(ResolveEnemyReturnMoveHash(enemy));
                    SetGhostTrail(enemy, true);
                    yield return enemy.transform.DOMove(PositionManager.Instance.GetEnemyDefaultPos(_enemies.IndexOf(enemy)), 0.3f).SetEase(Ease.InQuad).WaitForCompletion();
                    SetGhostTrail(enemy, false);
                }
                SetActorForeground(enemy, false);
                if (enemy != null && enemy.IsAlive)
                    enemy.PlayBattleAnim(EnemyCharacter.HashBattleIdle);
            }
        }
        else // 광역기 처리
        {
            yield return new WaitForSeconds(_enemyAoEWindup);
            foreach (var p in _playerParty) 
            { 
                if (!p.IsAlive) continue; 
                int dmg = p.TakePureDamage(enemy.ATK); 
                p.GetComponent<PlayerController>()?.PlayHurtEffect(); 
                
                OnDamageDealt?.Invoke(p, dmg, false); 
            }
            _impulseSource?.GenerateImpulse(_hitImpulse);
            yield return new WaitForSeconds(_enemyPostHitDelay);
        }

        yield return StartCoroutine(WaitForNarrationToFinish());
        EndAction(); 
    }

    #endregion

    #region [ Outro & Scene Transitions ]
    private bool CheckVictory() => _enemies != null && _enemies.Count > 0 && _enemies.TrueForAll(e => e == null || !e.IsAlive);
    private bool CheckDefeat()  => _playerParty.TrueForAll(p => !p.IsAlive);

    private IEnumerator RunRoutine()
    {
        if (_isRunInProgress) yield break;
        _isRunInProgress = true;

        RequestNarration(new BattleNarrationMessage("도망을 시도했다...", BattleNarrationStyle.Normal, BattleNarrationPriority.High, 0.2f, true));
        yield return StartCoroutine(WaitForNarrationToFinish());

        bool success = UnityEngine.Random.value < 0.6f;
        RequestNarration(new BattleNarrationMessage(success ? "도망에 성공했다!" : "도망에 실패했다...", BattleNarrationStyle.Warning, BattleNarrationPriority.High, 0.2f, true));
        yield return StartCoroutine(WaitForNarrationToFinish());

        if (success)
        {
            CommitOverworldEncounterResult(false);
            _isRunInProgress = false;
            yield return StartCoroutine(BattleOutroRoutine(false));
        }
        else
        {
            _isRunInProgress = false;
            EndAction();
        }
    }

    private IEnumerator BattleEndRoutine()
    {
        bool victory = CheckVictory();
        _isBattleEnding = true;
        QTEManager.Instance?.ForceStop();
        CommitOverworldEncounterResult(victory);
        RequestNarration(victory
            ? new BattleNarrationMessage("전투에서 승리했다!", BattleNarrationStyle.System, BattleNarrationPriority.Critical, 0.8f, true)
            : new BattleNarrationMessage("눈 앞이 캄캄해졌다...", BattleNarrationStyle.System, BattleNarrationPriority.Critical, 2.0f, true));
        yield return StartCoroutine(WaitForNarrationToFinish());
        if (!victory) yield return new WaitForSecondsRealtime(0.75f);
        yield return StartCoroutine(BattleOutroRoutine(victory));
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
    #endif

    private void CommitOverworldEncounterResult(bool isVictory)
    {
        var global = GlobalDataManager.Instance;
        if (global == null) return;

        string enemyId = global.CurrentEncounterEnemyId;
        BattleEncounterMemoryRecorder.RecordBattleResult(
            _battleScenarioRuntime != null ? _battleScenarioRuntime.ScenarioData : null,
            _battleScenarioRuntime,
            global,
            enemyId,
            isVictory);

        if (!string.IsNullOrWhiteSpace(enemyId))
        {
            string sceneName = global.LastOverworldScene;

            if (isVictory)
            {
                if (global.CurrentEncounterDefeatsOnVictory)
                    global.MarkOverworldEnemyDefeated(enemyId, sceneName);
                else
                    global.ClearOverworldEnemyCooldown(enemyId);
            }
            else
            {
                global.MarkOverworldEnemyEscaped(enemyId, sceneName, _postRunEnemyDisableDuration, _postRunEnemyAlpha);
            }
        }
    }

    private IEnumerator BattleOutroRoutine(bool isVictory)
    {
        Time.timeScale = 1.0f; // 슬로우 모션 방지
        OnBattleEnded?.Invoke(isVictory);
        if (isVictory)
            BattleUIController.Instance?.ClearNarrationLog();
        yield return new WaitForSecondsRealtime(0.25f);

        foreach (var player in _playerParty)
        {
            if (player != null && player.IsAlive) player.SaveDataToGlobal();
        }

        if (_isDedicatedBattleScene)
        {
            string returnScene = GlobalDataManager.Instance.LastOverworldScene;
            GlobalDataManager.Instance?.EndOverworldEnemyEncounterContext();
            SceneLoader.Instance?.LoadScene(!string.IsNullOrEmpty(returnScene) ? returnScene : _fallbackSceneName);
        }
        else
        {
            // 오버월드 심리스 복귀 로직
            if (_battleUICanvas != null) _battleUICanvas.SetActive(false);

            PlayerController encounterPlayer = _activeEncounterPlayer;
            if (encounterPlayer == null && _playerParty.Count > 0 && _playerParty[0] != null)
                encounterPlayer = _playerParty[0].GetComponent<PlayerController>();

            _activeEncounterSource?.OnEncounterResolved(isVictory, encounterPlayer);
            _activeEncounterSource = null;
            _activeEncounterPlayer = null;

            foreach (var player in _playerParty)
            {
                var ctrl = player.GetComponent<PlayerController>();
                if (ctrl != null) 
                {
                    ctrl.SetBattleMode(false); 
                    var anim = player.GetComponent<Animator>();
                    if (anim != null)
                    {
                        anim.Rebind();      // 모든 상태 강제 초기화
                        anim.Update(0f);    // 즉시 반영
                    }

                }
            }

            foreach (var enemy in _enemies)
            {
                if (enemy != null) Destroy(enemy.gameObject);
            }
            _enemies.Clear();
            
            CameraController.Instance?.ResetCamera();
            GlobalDataManager.Instance?.EndOverworldEnemyEncounterContext();
            Debug.Log("[BattleManager] 심리스 전투 종료! 오버월드 Idle 복귀 완료.");
        }
    }
    #endregion

    #region [ Static Utilities & Event Bridges ]
    public static void ExecuteItemEffect(CharacterBase target, ItemData item)
    {
        if (item == null || target == null) return;

        if (item.ActionType == EffectActionType.Heal)
        {
            int maxStat = (item.TargetStat == TargetStatType.HP) ? target.MaxHP : target.MaxMP;
            int amount = item.CalcType switch {
                ValueCalcType.Flat => item.EffectValue,
                ValueCalcType.Percentage => Mathf.RoundToInt(maxStat * (item.EffectValue * 0.01f)),
                ValueCalcType.Full => maxStat,
                _ => 0
            };
                
            if (item.TargetStat == TargetStatType.HP) 
            { 
                target.HealHP(amount); 
                Instance.OnDamageDealt?.Invoke(target, -amount, false); 
            }
            else if (item.TargetStat == TargetStatType.MP && target is PlayerCharacter pc) 
            { 
                pc.HealMP(amount); 
                Instance.OnMPChanged?.Invoke(pc, pc.CurrentMP); 
            }
        }
        else if (item.ActionType == EffectActionType.Damage) 
        {
            int damage = item.CalcType == ValueCalcType.Flat ? item.EffectValue : 50;
            target.TakeDamage(damage);
        }
        else if (item.ActionType == EffectActionType.ApplyStatus)
        {
            // 하드코딩된 스트링 대신 상수나 Enum을 사용하는 것이 이상적이나, 기존 데이터 호환을 위해 유지
            StatusEffect eff = item.StatusEffectID switch { 
                "Burn" => new BurnEffect(item.StatusDurationTurns), 
                "Poison" => new PoisonEffect(item.StatusDurationTurns), 
                "Freeze" => new FreezeEffect(item.StatusDurationTurns), 
                "Bind" => new BindEffect(item.StatusDurationTurns), 
                "Stun" => new StunEffect(item.StatusDurationTurns),
                "Berserk" => new BerserkEffect(item.StatusDurationTurns),
                _ => null 
            };
            if (eff != null) target.AddEffect(eff);
        }
    }

    public void InvokeDamageEvent(CharacterBase target, int damage, bool isPerfect)
    {
        int previousHp = target != null ? Mathf.Clamp(target.CurrentHP + Mathf.Max(0, damage), 0, target.MaxHP) : 0;
        InvokeDamageEvent(target, damage, isPerfect, previousHp);
    }

    public void InvokeDamageEvent(CharacterBase target, int damage, bool isPerfect, int previousHp)
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

        OnDamageDealt?.Invoke(target, damage, isPerfect);
    }

    public void InvokeMPChangedEvent(PlayerCharacter player, int newMP)
    {
        OnMPChanged?.Invoke(player, newMP);
    }

    public static void SetGhostTrail(CharacterBase character, bool active)
    {
        if (character == null) return;
        var trail = character.GetComponentInChildren<CharacterGhostTrail>();
        if (trail != null) trail.SetTrailActive(active);
    }
    #endregion
}
