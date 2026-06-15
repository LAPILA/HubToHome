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
    private IGameModuleActionRunner _battleGameModuleActionRunner;
    private IBattleTurnQteModuleController _turnQteModuleController;
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
        _turnQteModuleController = new BattleTurnQteModuleController(this);
        _battleGameModuleActionRunner = CreateBattleGameModuleActionRunner(
            scenarioData,
            _battleScenarioRuntime != null ? _battleScenarioRuntime.SessionState : null,
            _turnQteModuleController);
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
        registry.Register(new BattleParticipantDamageActionAdapter());
        registry.Register(new BattleParticipantHealHpActionAdapter());
        registry.Register(new BattleParticipantHealMpActionAdapter());
        registry.Register(new BattleParticipantConsumeMpActionAdapter());
        registry.Register(new BattleFlagSetActionAdapter());
        registry.Register(new BattleFlagClearActionAdapter());
        return new ActionDirector(registry);
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
            battleSessionState: _battleScenarioRuntime != null ? _battleScenarioRuntime.SessionState : null,
            battleParticipantCommandRunner: new BattleParticipantCommandRunner(this),
            gameModuleEventSink: _battleScenarioExecutionGate);
    }

    private void RefreshBattleSessionParticipants()
    {
        if (_battleScenarioRuntime == null || _battleScenarioRuntime.SessionState == null)
        {
            return;
        }

        var participants = new List<BattleParticipantSnapshot>();
        for (int i = 0; i < _playerParty.Count; i++)
        {
            BattleParticipantSnapshot snapshot = BattleParticipantSnapshot.FromPlayer(_playerParty[i]);
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

    private BattleParticipantCommandResult ApplyPureDamageToParticipant(string subjectId, int amount)
    {
        if (amount <= 0)
        {
            return BattleParticipantCommandResult.Failed(subjectId, "Damage amount must be greater than zero.");
        }

        CharacterBase target = FindBattleParticipant(subjectId);
        if (target == null)
        {
            return BattleParticipantCommandResult.Failed(subjectId, "Battle participant was not found: " + subjectId);
        }

        int previousHp = target.CurrentHP;
        int appliedDamage = target.TakePureDamage(amount);
        InvokeDamageEvent(target, appliedDamage, false, previousHp);
        return BattleParticipantCommandResult.Succeeded(
            ResolveCommandSubjectId(target, subjectId),
            amount,
            appliedDamage,
            previousHp,
            target.CurrentHP);
    }

    private BattleParticipantCommandResult HealHpParticipant(string subjectId, int amount)
    {
        if (amount <= 0)
        {
            return BattleParticipantCommandResult.Failed(subjectId, "Heal amount must be greater than zero.");
        }

        CharacterBase target = FindBattleParticipant(subjectId);
        if (target == null)
        {
            return BattleParticipantCommandResult.Failed(subjectId, "Battle participant was not found: " + subjectId);
        }

        int previousHp = target.CurrentHP;
        target.HealHP(amount);
        int healedAmount = Mathf.Max(0, target.CurrentHP - previousHp);
        RefreshBattleSessionParticipants();
        OnDamageDealt?.Invoke(target, -healedAmount, false);
        return BattleParticipantCommandResult.Succeeded(
            ResolveCommandSubjectId(target, subjectId),
            amount,
            healedAmount,
            previousHp,
            target.CurrentHP);
    }

    private BattleParticipantCommandResult HealMpParticipant(string subjectId, int amount)
    {
        if (amount <= 0)
        {
            return BattleParticipantCommandResult.Failed(subjectId, "MP heal amount must be greater than zero.");
        }

        CharacterBase target = FindBattleParticipant(subjectId);
        if (target == null)
        {
            return BattleParticipantCommandResult.Failed(subjectId, "Battle participant was not found: " + subjectId);
        }

        int previousMp = target.CurrentMP;
        target.HealMP(amount);
        int healedAmount = Mathf.Max(0, target.CurrentMP - previousMp);
        RefreshBattleSessionParticipants();
        PlayerCharacter player = target as PlayerCharacter;
        if (player != null)
        {
            OnMPChanged?.Invoke(player, player.CurrentMP);
        }

        return BattleParticipantCommandResult.Succeeded(
            ResolveCommandSubjectId(target, subjectId),
            amount,
            healedAmount,
            previousMp,
            target.CurrentMP);
    }

    private BattleParticipantCommandResult ConsumeMpParticipant(string subjectId, int amount)
    {
        if (amount <= 0)
        {
            return BattleParticipantCommandResult.Failed(subjectId, "MP consume amount must be greater than zero.");
        }

        CharacterBase target = FindBattleParticipant(subjectId);
        if (target == null)
        {
            return BattleParticipantCommandResult.Failed(subjectId, "Battle participant was not found: " + subjectId);
        }

        int previousMp = target.CurrentMP;
        target.ConsumeMP(amount);
        int consumedAmount = Mathf.Max(0, previousMp - target.CurrentMP);
        RefreshBattleSessionParticipants();
        PlayerCharacter player = target as PlayerCharacter;
        if (player != null)
        {
            OnMPChanged?.Invoke(player, player.CurrentMP);
        }

        return BattleParticipantCommandResult.Succeeded(
            ResolveCommandSubjectId(target, subjectId),
            amount,
            consumedAmount,
            previousMp,
            target.CurrentMP);
    }

    private CharacterBase FindBattleParticipant(string subjectId)
    {
        if (string.IsNullOrWhiteSpace(subjectId))
        {
            return null;
        }

        string normalized = subjectId.Trim();
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

    private sealed class BattleParticipantCommandRunner : IBattleParticipantCommandRunner
    {
        private readonly BattleManager _battleManager;

        public BattleParticipantCommandRunner(BattleManager battleManager)
        {
            _battleManager = battleManager;
        }

        public BattleParticipantCommandResult ApplyPureDamage(
            string subjectId,
            int amount,
            ActionExecutionContext context)
        {
            return _battleManager != null
                ? _battleManager.ApplyPureDamageToParticipant(subjectId, amount)
                : BattleParticipantCommandResult.Failed(subjectId, "BattleManager is missing.");
        }

        public BattleParticipantCommandResult HealHp(
            string subjectId,
            int amount,
            ActionExecutionContext context)
        {
            return _battleManager != null
                ? _battleManager.HealHpParticipant(subjectId, amount)
                : BattleParticipantCommandResult.Failed(subjectId, "BattleManager is missing.");
        }

        public BattleParticipantCommandResult HealMp(
            string subjectId,
            int amount,
            ActionExecutionContext context)
        {
            return _battleManager != null
                ? _battleManager.HealMpParticipant(subjectId, amount)
                : BattleParticipantCommandResult.Failed(subjectId, "BattleManager is missing.");
        }

        public BattleParticipantCommandResult ConsumeMp(
            string subjectId,
            int amount,
            ActionExecutionContext context)
        {
            return _battleManager != null
                ? _battleManager.ConsumeMpParticipant(subjectId, amount)
                : BattleParticipantCommandResult.Failed(subjectId, "BattleManager is missing.");
        }
    }

    private sealed class BattleTurnQteModuleController : IBattleTurnQteModuleController
    {
        private readonly BattleManager _battleManager;

        public BattleTurnQteModuleController(BattleManager battleManager)
        {
            _battleManager = battleManager;
        }

        public IEnumerator EnterTurnQteModule(GameModuleRuntimeContext context)
        {
            BattleUIController.Instance?.ResumeBattleModuleInput();
            yield break;
        }

        public IEnumerator ExitTurnQteModule(GameModuleRuntimeContext context)
        {
            QTEManager.Instance?.ForceStop();
            _battleManager?.ClearTurnQtePendingActionState();
            BattleUIController.Instance?.SuspendBattleModuleInput();
            yield break;
        }

        public IEnumerator StartTurnQteModule(GameModuleRuntimeContext context)
        {
            BattleUIController.Instance?.ResumeBattleModuleInput();
            if (_battleManager != null)
            {
                _battleManager.StartTurnQteCombatLoop();
            }

            yield break;
        }

        public IEnumerator RunTurnCalculation()
        {
            BattleManager bm = _battleManager;
            if (bm == null || !bm.IsTurnQteCombatInputActive())
            {
                yield break;
            }

            yield return null;
            bm._turnQueue.Clear();

            if (bm._enemies == null || bm._enemies.Count == 0)
            {
                Debug.LogError("[BattleManager] 전투 시작 시 적 리스트가 비어 있습니다. BattlePrefab 또는 EnemyCharacter 설정을 확인해주세요.");
                yield break;
            }

            List<CharacterBase> aliveChars = new List<CharacterBase>();

            foreach (var p in bm._playerParty) if (p != null && p.IsAlive) aliveChars.Add(p);
            foreach (var e in bm._enemies)     if (e != null && e.IsAlive) aliveChars.Add(e);

            if (aliveChars.Count == 0 || bm.CheckVictory() || bm.CheckDefeat())
            {
                CompleteAction();
                yield break;
            }

            for (int i = 0; i < bm._maxTurnQueueSize; i++)
            {
                aliveChars.Sort((a, b) => b.SPD.CompareTo(a.SPD));
                bm._turnQueue.Add(aliveChars[i % aliveChars.Count]);
            }

            bm._currentActorIndex = 0;
            bm.BroadcastVisibleTurnQueue();
            yield return bm._waitShort;

            AdvanceTurn();
        }

        public void AdvanceTurn()
        {
            BattleManager bm = _battleManager;
            if (bm == null || !bm.IsTurnQteCombatInputActive())
            {
                return;
            }

            if (bm._currentActorIndex >= bm._turnQueue.Count)
            {
                bm.ChangeState(BattleState.TurnCalc);
                return;
            }

            var actor = bm._turnQueue[bm._currentActorIndex++];
            if (actor == null || !actor.IsAlive)
            {
                bm.BroadcastVisibleTurnQueue();
                AdvanceTurn();
                return;
            }

            actor.ProcessEffects();
            if (!actor.IsAlive)
            {
                bm.BroadcastVisibleTurnQueue();
                AdvanceTurn();
                return;
            }

            if (actor is PlayerCharacter player)
            {
                bm._battleTurnCounter++;
                bm.StartCoroutine(BeginPlayerTurn(player));
            }
            else if (actor is EnemyCharacter)
            {
                bm.StartCoroutine(BeginEnemyTurn());
            }
        }

        public IEnumerator BeginPlayerTurn(PlayerCharacter player)
        {
            BattleManager bm = _battleManager;
            if (bm == null || player == null || !bm.IsTurnQteCombatInputActive())
            {
                yield break;
            }

            bm.ResetAllPlayerBattlePoses();
            player.GetComponent<PlayerController>()?.PlayBattleAnim(PlayerCharacter.HashBattleIdle);
            player.HealMP(bm._mpPerTurn);
            bm.InvokeMPChangedEvent(player, player.CurrentMP);
            if (player.TryShowBattleSpeech(BattleSpeechTrigger.TurnStart, null, null, bm._battleTurnCounter))
                yield return bm.StartCoroutine(player.WaitForBattleSpeech());
            bm.TryRequestFlavorNarration();
            yield return bm.StartCoroutine(bm.WaitForNarrationToFinish());
            bm.NotifyPlayerTurnStarted(player);
            bm.ChangeState(BattleState.PlayerActionSelect);
        }

        public IEnumerator BeginEnemyTurn()
        {
            BattleManager bm = _battleManager;
            if (bm == null || !bm.IsTurnQteCombatInputActive())
            {
                yield break;
            }

            bm.ResetAllPlayerBattlePoses();
            yield return bm.StartCoroutine(bm.WaitForNarrationToFinish());
            bm.ChangeState(BattleState.EnemyAction);
        }

        public IEnumerator RunEnemyAction()
        {
            BattleManager bm = _battleManager;
            if (bm == null || !bm.IsTurnQteCombatInputActive())
            {
                yield break;
            }

            var enemy = bm._turnQueue[bm._currentActorIndex - 1] as EnemyCharacter;
            if (enemy == null)
            {
                CompleteAction();
                yield break;
            }

            SkillData enemySkill = null;
            EnemyAction action;
            bool isExecutingReservedAction = false;

            if (bm._reservedEnemyActionByActor.TryGetValue(enemy, out EnemyQueuedAction reservedAction))
            {
                reservedAction.TurnsRemaining--;
                if (reservedAction.TurnsRemaining > 0)
                {
                    bm._reservedEnemyActionByActor[enemy] = reservedAction;
                    CompleteAction();
                    yield break;
                }

                action = reservedAction.Action;
                enemySkill = reservedAction.Skill;
                bm._reservedEnemyActionByActor.Remove(enemy);
                isExecutingReservedAction = true;
            }
            else
            {
                action = enemy.DecideAction();
                enemySkill = bm.GetEnemySequenceSkill(enemy, action);
            }

            var attackType = action switch
            {
                EnemyAction.UseSkill when enemySkill != null => bm.ResolveEnemySkillAttackType(enemySkill),
                EnemyAction.UseStrongSkill when enemySkill != null => bm.ResolveEnemySkillAttackType(enemySkill),
                EnemyAction.EnragedAttack => EnemyAttackType.AoEAll,
                _ => EnemyAttackType.MeleeClose
            };

            bm.NotifyEnemyActionStarted(enemy, attackType);

            bool shouldTelegraphSkillThisTurn = enemy.Data != null
                && enemy.Data.TelegraphStrongSkill
                && action == EnemyAction.UseStrongSkill
                && enemySkill != null
                && !isExecutingReservedAction
                && !bm._reservedEnemyActionByActor.ContainsKey(enemy);

            if (shouldTelegraphSkillThisTurn)
            {
                string enemyName = enemy != null && enemy.Data != null && !string.IsNullOrWhiteSpace(enemy.Data.EnemyName) ? enemy.Data.EnemyName : "적";
                string warnText = $"{enemyName}가 강한 공격을 준비한다...";
                bm.RequestNarration(new BattleNarrationMessage(warnText, BattleNarrationStyle.Warning, BattleNarrationPriority.High, 0.4f, true));
                yield return bm.StartCoroutine(bm.WaitForNarrationToFinish());

                bm._reservedEnemyActionByActor[enemy] = new EnemyQueuedAction
                {
                    Action = action,
                    Skill = enemySkill,
                    TurnsRemaining = Mathf.Max(1, enemy.Data.TelegraphTurns)
                };
                CompleteAction();
                yield break;
            }

            if ((action == EnemyAction.UseSkill || action == EnemyAction.UseStrongSkill) && enemySkill != null)
            {
                if (enemy.TryShowBattleSpeech(BattleSpeechTrigger.SkillUse, enemySkill, null, bm._battleTurnCounter, 1.2f))
                    yield return bm.StartCoroutine(enemy.WaitForBattleSpeech());
                else
                    yield return new WaitForSeconds(0.18f);

                yield return bm.StartCoroutine(bm.ExecuteEnemySequenceSkill(enemy, enemySkill));
            }
            else if (attackType == EnemyAttackType.MeleeClose)
            {
                int targetIdx = bm._playerParty.FindIndex(p => p.IsAlive);
                if (targetIdx >= 0)
                {
                    var target = bm._playerParty[targetIdx];
                    var targetCtrl = target.GetComponent<PlayerController>();
                    bool movedToCenter = enemy.Data == null || !enemy.Data.IsLargeEnemy;

                    yield return bm.StartCoroutine(bm.MoveEnemyToCenterIfNeeded(enemy));
                    bm.SetActorForeground(enemy, true);

                    enemy.PlayBasicAttackEffect();
                    enemy.PlayBattleAnim(EnemyCharacter.HashAttack);

                    bool qteFinished = false;
                    DefenseInput finalInput = DefenseInput.None;
                    QTEManager.QTEGrade finalGrade = QTEManager.QTEGrade.Miss;

                    targetCtrl?.PrepareDefenseWindow();
                    QTEManager.Instance.StartDefenseQTE(bm._enemyDefenseQTEWindow, 1.0f, (input, grade) =>
                    {
                        finalInput = input;
                        finalGrade = grade;
                        qteFinished = true;
                    });
                    yield return new WaitForSeconds(bm._enemyAttackVisualDuration);
                    enemy.PlayBattleAnim(EnemyCharacter.HashBattleIdle);
                    yield return new WaitUntil(() => qteFinished);

                    if (finalGrade == QTEManager.QTEGrade.Miss)
                    {
                        int dmg = target.TakePureDamage(enemy.ATK);
                        targetCtrl?.PlayHurtEffect();
                        CameraController.Instance?.PlayHeavySlam(Vector3.left, 1.0f, true);
                        bm.InvokeDamageEvent(target, dmg, false);
                    }
                    else
                    {
                        targetCtrl?.ConfirmDefenseSuccess(finalInput);
                        if (finalInput == DefenseInput.Parry && finalGrade == QTEManager.QTEGrade.Perfect)
                        {
                            target.HealMP(bm._mpOnParryPerfect);
                            bm.InvokeMPChangedEvent(target, target.CurrentMP);
                        }
                        if (finalInput == DefenseInput.Dodge || finalInput == DefenseInput.Jump)
                            yield return targetCtrl != null ? bm.StartCoroutine(targetCtrl.WaitForDefenseVisualComplete(0.5f)) : null;
                    }

                    yield return new WaitForSeconds(bm._enemyPostHitDelay);
                    targetCtrl?.ResetDefenseReactionLock();
                    if (target != null && target.IsAlive)
                        targetCtrl?.PlayBattleAnim(PlayerCharacter.HashBattleIdle);

                    if (movedToCenter)
                    {
                        enemy.PlayBattleAnim(bm.ResolveEnemyReturnMoveHash(enemy));
                        SetGhostTrail(enemy, true);
                        yield return enemy.transform.DOMove(PositionManager.Instance.GetEnemyDefaultPos(bm._enemies.IndexOf(enemy)), 0.3f).SetEase(Ease.InQuad).WaitForCompletion();
                        SetGhostTrail(enemy, false);
                    }
                    bm.SetActorForeground(enemy, false);
                    if (enemy != null && enemy.IsAlive)
                        enemy.PlayBattleAnim(EnemyCharacter.HashBattleIdle);
                }
            }
            else
            {
                yield return new WaitForSeconds(bm._enemyAoEWindup);
                foreach (var p in bm._playerParty)
                {
                    if (!p.IsAlive) continue;
                    int dmg = p.TakePureDamage(enemy.ATK);
                    p.GetComponent<PlayerController>()?.PlayHurtEffect();
                    bm.InvokeDamageEvent(p, dmg, false);
                }
                bm._impulseSource?.GenerateImpulse(bm._hitImpulse);
                yield return new WaitForSeconds(bm._enemyPostHitDelay);
            }

            yield return bm.StartCoroutine(bm.WaitForNarrationToFinish());
            CompleteAction();
        }

        public void SelectPlayerAction(PlayerCharacter actor, PlayerMenuAction action)
        {
            BattleManager bm = _battleManager;
            if (bm == null || actor == null || !bm.IsTurnQteCombatInputActive())
            {
                return;
            }

            bm._pendingActor = actor;
            bm._pendingAction = action;
            bm.CurrentPendingSkill = null;
            bm.CurrentPendingItem = null;

            if (action != PlayerMenuAction.Run)
            {
                actor.PlayBattleAnim(PlayerCharacter.HashBattleReady);
            }

            if (action == PlayerMenuAction.Attack)
            {
                bm.NotifyTargetSelectionStarted(action);
            }
            else if (action == PlayerMenuAction.Run)
            {
                bm.StartCoroutine(bm.RunRoutine());
            }
        }

        public void SelectSubMenuAction(PlayerCharacter actor, PlayerMenuAction action, SkillData skill, ItemData item)
        {
            BattleManager bm = _battleManager;
            if (bm == null || actor == null || !bm.IsTurnQteCombatInputActive())
            {
                return;
            }

            bm._pendingActor = actor;
            bm._pendingAction = action;
            bm.CurrentPendingSkill = skill;
            bm.CurrentPendingItem = item;

            bool isAoE = (skill != null && skill.IsAoE) || (item != null && item.IsAoE);

            if (isAoE) ConfirmTargetAndExecute(-1);
            else       bm.NotifyTargetSelectionStarted(action);
        }

        public void CancelActionSelection()
        {
            BattleManager bm = _battleManager;
            if (bm == null || !bm.IsTurnQteCombatInputActive())
            {
                return;
            }

            bm._pendingActor?.PlayBattleAnim(PlayerCharacter.HashBattleIdle);
            bm.ChangeState(BattleState.PlayerActionSelect);
        }

        public void CancelTargetSelection()
        {
            BattleManager bm = _battleManager;
            if (bm == null || !bm.IsTurnQteCombatInputActive())
            {
                return;
            }

            bm._pendingActor?.PlayBattleAnim(PlayerCharacter.HashBattleIdle);
            bm.ChangeState(BattleState.PlayerActionSelect);
        }

        public void ConfirmTargetAndExecute(int targetIndex)
        {
            BattleManager bm = _battleManager;
            if (bm == null || !bm.IsTurnQteCombatInputActive())
            {
                return;
            }

            if (bm.CurrentState == BattleState.ActionExecute) return;
            if (bm._pendingAction == PlayerMenuAction.Attack)
            {
                bm.ChangeState(BattleState.ActionExecute);
                bm.StartCoroutine(ExecuteAttack(bm._pendingActor, targetIndex));
            }
            else if (bm._pendingAction == PlayerMenuAction.Skill && bm.CurrentPendingSkill != null)
            {
                if (bm._pendingActor.CurrentMP < bm.CurrentPendingSkill.MPCost)
                {
                    bm.RequestNarration(new BattleNarrationMessage("MP가 부족하다.", BattleNarrationStyle.Warning, BattleNarrationPriority.High, 0.2f, true));
                    bm._pendingActor?.PlayBattleAnim(PlayerCharacter.HashBattleIdle);
                    bm.CurrentPendingSkill = null;
                    bm.CurrentPendingItem = null;
                    bm.ChangeState(BattleState.PlayerActionSelect);
                    return;
                }

                bm.ChangeState(BattleState.ActionExecute);
                bm.StartCoroutine(ExecuteSkill(bm._pendingActor, targetIndex, bm.CurrentPendingSkill));
            }
            else if (bm._pendingAction == PlayerMenuAction.Item && bm.CurrentPendingItem != null)
            {
                bm.ChangeState(BattleState.ActionExecute);
                bm.StartCoroutine(ExecuteItem(bm._pendingActor, targetIndex, bm.CurrentPendingItem));
            }
            else
            {
                CompleteAction();
            }
        }

        public void CompleteAction()
        {
            BattleManager bm = _battleManager;
            if (bm == null)
            {
                return;
            }

            bm.ClearTurnQtePendingActionState();
            bm.ResetAllPlayerBattlePoses();
            CameraController.Instance?.ResetCamera(0.4f);

            bm.BroadcastVisibleTurnQueue();

            if (!bm.IsTurnQteCombatInputActive())
            {
                return;
            }

            if (bm.CheckVictory() || bm.CheckDefeat()) bm.ChangeState(BattleState.BattleEnd);
            else AdvanceTurn();
        }

        private IEnumerator ExecuteAttack(PlayerCharacter actor, int targetIndex)
        {
            BattleManager bm = _battleManager;
            if (bm == null || actor == null || targetIndex >= bm._enemies.Count || !bm._enemies[targetIndex].IsAlive)
            {
                CompleteAction();
                yield break;
            }

            var target = bm._enemies[targetIndex];
            var pm = PositionManager.Instance;
            Vector3 frontPos = target.transform.position + bm._meleeAttackOffset;

            actor.PlayBattleAnim(PlayerCharacter.HashBattleMove);
            bm.SetActorForeground(actor, true);
            SetGhostTrail(actor, true);
            yield return actor.transform.DOMove(frontPos, 0.2f).SetEase(Ease.OutCubic).WaitForCompletion();

            Vector3 pullBackPos = frontPos + bm._meleePullbackOffset;
            yield return actor.transform.DOMove(pullBackPos, 0.15f).SetEase(Ease.OutBack).WaitForCompletion();

            Vector3 behindPos = target.transform.position + new Vector3(-bm._meleeAttackOffset.x, 0, 0);

            actor.PlayBasicAttackEffect();
            actor.PlayBattleAnim(PlayerCharacter.HashAttack);
            actor.transform.DOMove(behindPos, 0.15f).SetEase(Ease.InExpo);

            yield return new WaitForSeconds(bm._playerAttackHitDelay);

            int previousHp = target.CurrentHP;
            int dmg = target.TakeDamage(actor.ATK);
            CameraController.Instance?.PlayHeavySlam(Vector3.right, 0.75f, true);
            bm.PublishEnemyHpScenarioEvent(target, previousHp, target.CurrentHP, target.MaxHP, BattleRuleTiming.AfterCurrentAction);
            bm.NotifyDamageDealt(target, dmg, false);

            yield return new WaitForSeconds(bm._playerAttackRecoverDelay);
            SetGhostTrail(actor, false);
            bm.SetActorForeground(actor, false);

            int idx = bm._playerParty.IndexOf(actor);
            actor.PlayBattleAnim(PlayerCharacter.HashBattleMove);
            SetGhostTrail(actor, true);
            yield return actor.transform.DOJump(pm.GetPlayerDefaultPos(idx), 0.5f, 1, 0.3f).SetEase(Ease.OutQuad).WaitForCompletion();
            SetGhostTrail(actor, false);

            actor.PlayBattleAnim(PlayerCharacter.HashBattleIdle);
            CameraController.Instance?.ResetCamera(0.4f);

            yield return bm.StartCoroutine(bm.WaitForNarrationToFinish());
            yield return bm.StartCoroutine(bm.FlushBattleScenarioEvents(BattleRuleTiming.AfterCurrentAction));

            CompleteAction();
        }

        private IEnumerator ExecuteSkill(PlayerCharacter actor, int targetIndex, SkillData skill)
        {
            BattleManager bm = _battleManager;
            if (bm == null || actor == null || skill == null)
            {
                CompleteAction();
                yield break;
            }

            actor.ConsumeMP(skill.MPCost);
            bm.InvokeMPChangedEvent(actor, actor.CurrentMP);
            if (actor.TryShowBattleSpeech(BattleSpeechTrigger.SkillUse, skill, null, bm._battleTurnCounter))
                yield return bm.StartCoroutine(actor.WaitForBattleSpeech());

            List<CharacterBase> targets = new List<CharacterBase>();
            if (skill.IsAoE)
            {
                if (skill.TargetType == TargetAreaType.AllyOnly) targets.AddRange(bm._playerParty.FindAll(p => p.IsAlive));
                else targets.AddRange(bm._enemies.FindAll(e => e.IsAlive));
            }
            else
            {
                if (skill.TargetType == TargetAreaType.AllyOnly) targets.Add(bm._playerParty[targetIndex]);
                else targets.Add(bm._enemies[targetIndex]);
            }

            if (targets.Count == 0)
            {
                CompleteAction();
                yield break;
            }

            Vector3 originalPos = PositionManager.Instance.GetPlayerDefaultPos(bm._playerParty.IndexOf(actor));

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
                    yield return bm.StartCoroutine(block.Execute(context));
                }
            }

            if (Vector3.Distance(actor.transform.position, originalPos) > 0.1f)
            {
                actor.PlayBattleAnim(PlayerCharacter.HashBattleMove);
                bm.SetActorForeground(actor, true);
                SetGhostTrail(actor, true);
                yield return actor.transform.DOMove(originalPos, 0.3f).SetEase(Ease.OutBack).WaitForCompletion();
                SetGhostTrail(actor, false);
                bm.SetActorForeground(actor, false);
            }

            actor.PlayBattleAnim(PlayerCharacter.HashBattleIdle);
            CameraController.Instance?.ResetCamera(0.4f);
            yield return bm.StartCoroutine(bm.WaitForNarrationToFinish());
            yield return bm.StartCoroutine(bm.FlushBattleScenarioEvents(BattleRuleTiming.AfterCurrentSkill));

            CompleteAction();
        }

        private IEnumerator ExecuteItem(PlayerCharacter actor, int targetIndex, ItemData item)
        {
            BattleManager bm = _battleManager;
            if (bm == null || actor == null || item == null)
            {
                CompleteAction();
                yield break;
            }

            List<CharacterBase> targets = new List<CharacterBase>();

            if (item.IsAoE)
            {
                if (item.TargetType == TargetAreaType.AllyOnly) targets.AddRange(bm._playerParty.FindAll(p => p.IsAlive));
                else targets.AddRange(bm._enemies.FindAll(e => e.IsAlive));
            }
            else
            {
                if (item.TargetType == TargetAreaType.AllyOnly)
                {
                    if (targetIndex >= 0 && targetIndex < bm._playerParty.Count) targets.Add(bm._playerParty[targetIndex]);
                }
                else
                {
                    if (targetIndex >= 0 && targetIndex < bm._enemies.Count && bm._enemies[targetIndex].IsAlive) targets.Add(bm._enemies[targetIndex]);
                }
            }

            if (targets.Count == 0)
            {
                CompleteAction();
                yield break;
            }

            var actorCtrl = actor.GetComponent<PlayerController>();
            var pm = PositionManager.Instance;

            actorCtrl?.PlayBattleAnim(PlayerCharacter.HashBattleMove);
            bm.SetActorForeground(actor, true);
            yield return actor.transform.DOMove(actor.transform.position + Vector3.right * 1f, 0.2f).SetEase(Ease.OutQuad).WaitForCompletion();
            actorCtrl?.PlayBattleAnim(PlayerCharacter.HashBattleIdle);

            yield return new WaitForSeconds(0.3f);

            foreach (var t in targets) ExecuteItemEffect(t, item);

            yield return new WaitForSeconds(0.5f);

            int idx = bm._playerParty.IndexOf(actor);
            actorCtrl?.PlayBattleAnim(PlayerCharacter.HashBattleMove);
            yield return actor.transform.DOMove(pm.GetPlayerDefaultPos(idx), 0.3f).SetEase(Ease.OutBack).WaitForCompletion();
            bm.SetActorForeground(actor, false);
            actorCtrl?.PlayBattleAnim(PlayerCharacter.HashBattleIdle);

            yield return bm.StartCoroutine(bm.WaitForNarrationToFinish());

            CompleteAction();
        }
    }

    private static IGameModuleActionRunner CreateBattleGameModuleActionRunner(
        BattleScenarioData scenarioData,
        IGameModuleStateStore moduleStateStore,
        IBattleTurnQteModuleController turnQteController)
    {
        var registry = BattleGameModuleRegistryFactory.CreateDefault(turnQteController, BattleUIController.Instance);
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
        RefreshBattleSessionParticipants();
        OnBattleStarted?.Invoke(_playerParty, _enemies);
        _battleNarrationConfig?.ResetRuntimeState();
        _battleTurnCounter = 0;
        _isBattleEnding = false;
        RequestNarration(BattleNarrationFormatter.BattleStart());
        TryRequestFlavorNarration();
        yield return StartCoroutine(WaitForNarrationToFinish());
        ChangeState(BattleState.Init);
        yield return StartCoroutine(StartOpeningBattleGameModule());
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
        RequestNarration(BattleNarrationFormatter.BattleStart());
        TryRequestFlavorNarration();
        yield return StartCoroutine(WaitForNarrationToFinish());
        yield return StartCoroutine(StartOpeningBattleGameModule());
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

        RefreshBattleSessionParticipants();
        OnDamageDealt?.Invoke(target, damage, isPerfect);
    }

    public void InvokeMPChangedEvent(PlayerCharacter player, int newMP)
    {
        RefreshBattleSessionParticipants();
        OnMPChanged?.Invoke(player, newMP);
    }

    private void NotifyDamageDealt(CharacterBase target, int damage, bool isPerfect)
    {
        RefreshBattleSessionParticipants();
        OnDamageDealt?.Invoke(target, damage, isPerfect);
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
