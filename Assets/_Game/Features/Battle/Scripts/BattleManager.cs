using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;
using Unity.Cinemachine;
using Sirenix.OdinInspector;

/// <summary>
/// 전투의 전체 흐름을 제어하는 중앙 매니저 (Singleton & State Machine 기반).
/// 옵저버(Observer) 패턴을 활용하여 UI와의 결합도를 낮췄습니다.
/// </summary>
public class BattleManager : MonoBehaviour
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
    [Tooltip("ZEV의 CrossCut 점프 회피 스킬 판정 시간")]
    [SerializeField] private float _zevCrossCutQTEWindow = 0.95f;
    [Tooltip("ZEV의 CrossCut 공격 연출이 한 번 보여지는 시간")]
    [SerializeField] private float _zevCrossCutVisualDuration = 0.28f;
    [Tooltip("플레이어 기본공격이 실제 데미지를 적용하기까지의 시간")]
    [SerializeField] private float _playerAttackHitDelay = 0.03f;
    [Tooltip("플레이어 기본공격 히트 후 복귀 시작까지의 시간")]
    [SerializeField] private float _playerAttackRecoverDelay = 0.14f;
    [Tooltip("적 공격 판정 후 적이 복귀를 시작하기까지의 시간")]
    [SerializeField] private float _enemyPostHitDelay = 0.18f;
    [Tooltip("광역 공격의 판정 전 준비 시간")]
    [SerializeField] private float _enemyAoEWindup = 0.35f;
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
    #endregion

    #region [ Initialization ]
    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void Start() 
    {
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
    public void StartSeamlessBattle(List<EnemyData> encounterEnemies, PlayerController playerCtrl)
    {
        Debug.Log("<color=cyan>[BattleManager] 심리스 전투 연출 시작!</color>");

        if (GlobalDataManager.Instance != null && GlobalDataManager.Instance.PendingBattleBGM != null)
            AudioManager.Instance?.CrossFadeBGM(GlobalDataManager.Instance.PendingBattleBGM, 0.8f);

        if (_battleUICanvas != null) _battleUICanvas.SetActive(true);

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
        if (pm != null && pm.CenterTransform != null)
    {
        CameraController.Instance?.SetTarget(pm.CenterTransform);
        Debug.Log("<color=yellow>[카메라] 타겟을 CenterPos로 변경했습니다.</color>");
    }

        if (pm != null && playerCtrl != null)
        {
            Vector3 battlePos = pm.GetPlayerDefaultPos(0);
            playerCtrl.PlayBattleAnim(PlayerCharacter.HashBattleMove);
            yield return playerCtrl.transform.DOMove(battlePos, 0.5f).SetEase(Ease.OutExpo).WaitForCompletion();
            
            playerCtrl.SetFacingDirection(3); // 오른쪽 보기
            playerCtrl.SetBattleMode(true);   // 이동 잠금 및 Idle 전환
        }

        // QTE UI 레이아웃 예열 (첫 스킬 노드 누락 방지)
        BattleUIController.Instance?.ShowSkillQTE(Vector2.zero, "", 0f);
        yield return null; 
        BattleUIController.Instance?.HideSkillQTE();

        OnBattleStarted?.Invoke(_playerParty, _enemies);
        ChangeState(BattleState.Init);
        ChangeState(BattleState.TurnCalc);
    }

    /// <summary>
    /// 전용 배틀 씬(BattleScene)으로 넘어왔을 때 호출되는 자동 셋업 루틴입니다.
    /// </summary>
    private IEnumerator DelayedStartRoutine()
    {
        ChangeState(BattleState.Init);
        if (_battleUICanvas != null) { _battleUICanvas.SetActive(true); yield return null; }

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

        OnBattleStarted?.Invoke(_playerParty, _enemies);
        yield return new WaitForSeconds(0.5f);
        ChangeState(BattleState.TurnCalc);
    }

    private GameObject ResolveEnemyBattlePrefab(EnemyData enemyData)
    {
        if (enemyData != null && enemyData.BattlePrefab != null)
            return enemyData.BattlePrefab;

        return _enemyBasePrefab;
    }

    private bool IsZevEnemy(EnemyCharacter enemy)
    {
        return enemy != null && enemy.IsEnemyNamed("ZEV");
    }

    private int ResolveEnemyReturnMoveHash(EnemyCharacter enemy)
    {
        return IsZevEnemy(enemy) ? EnemyCharacter.HashBattleMoveBack : EnemyCharacter.HashBattleMove;
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
        OnTurnQueueUpdated?.Invoke(_turnQueue);
        yield return _waitShort;
        
        AdvanceTurn();
    }

    private void AdvanceTurn()
    {
        // 큐를 다 소진했다면 다시 턴 계산
        if (_currentActorIndex >= _turnQueue.Count) { ChangeState(BattleState.TurnCalc); return; }

        var actor = _turnQueue[_currentActorIndex++];
        if (actor == null || !actor.IsAlive) { AdvanceTurn(); return; }

        // 상태이상 틱 데미지 처리
        actor.ProcessEffects();
        if (!actor.IsAlive) { AdvanceTurn(); return; }

        // 액터 진영에 따른 턴 분기
        if (actor is PlayerCharacter player)
        {
            player.HealMP(_mpPerTurn); 
            OnMPChanged?.Invoke(player, player.CurrentMP); 
            OnPlayerTurnStarted?.Invoke(player);
            ChangeState(BattleState.PlayerActionSelect);
        }
        else if (actor is EnemyCharacter)
        {
            ChangeState(BattleState.EnemyAction);
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
        ChangeState(BattleState.ActionExecute);

        if (_pendingAction == PlayerMenuAction.Attack)
            StartCoroutine(ExecuteAttack(_pendingActor, targetIndex));
        else if (_pendingAction == PlayerMenuAction.Skill && CurrentPendingSkill != null)
        {
            if (_pendingActor.CurrentMP >= CurrentPendingSkill.MPCost)
                StartCoroutine(ExecuteSkill(_pendingActor, targetIndex, CurrentPendingSkill));
            else { Debug.LogWarning("MP 부족!"); EndAction(); }
        }
        else if (_pendingAction == PlayerMenuAction.Item && CurrentPendingItem != null)
            StartCoroutine(ExecuteItem(_pendingActor, targetIndex, CurrentPendingItem));
        else
            EndAction();
    }
    #endregion

    #region [ Action Executions ]
    private void EndAction()
    {
        Time.timeScale = 1.0f;
        _pendingActor = null;
        CurrentPendingSkill = null;
        CurrentPendingItem = null;
        CameraController.Instance?.ResetCamera(0.4f);

        if (CheckVictory() || CheckDefeat()) ChangeState(BattleState.BattleEnd);
        else AdvanceTurn();
    }

    private IEnumerator ExecuteAttack(PlayerCharacter actor, int targetIndex)
    {
        if (targetIndex >= _enemies.Count || !_enemies[targetIndex].IsAlive) { EndAction(); yield break; }
        
        var target = _enemies[targetIndex];
        var pm = PositionManager.Instance;

        CameraController.Instance?.ModePlayerAction();
        CameraController.Instance?.ZoomOnTransform(actor.transform, 4.2f, 0.3f); 

        // 하드코딩 제거: 인스펙터 변수 참조
        Vector3 frontPos = target.transform.position + _meleeAttackOffset; 
        
        actor.PlayBattleAnim(PlayerCharacter.HashBattleMove);
        yield return actor.transform.DOMove(frontPos, 0.2f).SetEase(Ease.OutCubic).WaitForCompletion();

        Vector3 pullBackPos = frontPos + _meleePullbackOffset;
        yield return actor.transform.DOMove(pullBackPos, 0.15f).SetEase(Ease.OutBack).WaitForCompletion();

        // 타겟의 반대편으로 지나가는 연출 (X축 대칭)
        Vector3 behindPos = target.transform.position + new Vector3(-_meleeAttackOffset.x, 0, 0);
        
        actor.PlayBasicAttackEffect();
        actor.PlayBattleAnim(PlayerCharacter.HashAttack); 
        actor.transform.DOMove(behindPos, 0.15f).SetEase(Ease.InExpo);

        yield return new WaitForSeconds(_playerAttackHitDelay);

        int dmg = target.TakeDamage(actor.ATK);
        CameraController.Instance?.PlayDashThroughImpact(1.0f);
        OnDamageDealt?.Invoke(target, dmg, false);
        
        yield return new WaitForSeconds(_playerAttackRecoverDelay); 

        // 제자리 복귀
        int idx = _playerParty.IndexOf(actor);
        actor.PlayBattleAnim(PlayerCharacter.HashBattleMove);
        yield return actor.transform.DOJump(pm.GetPlayerDefaultPos(idx), 0.5f, 1, 0.3f).SetEase(Ease.OutQuad).WaitForCompletion();
        
        actor.PlayBattleAnim(PlayerCharacter.HashBattleIdle);
        CameraController.Instance?.ResetCamera(0.4f);

        EndAction();
    }
    
    private IEnumerator ExecuteSkill(PlayerCharacter actor, int targetIndex, SkillData skill)
    {
        actor.ConsumeMP(skill.MPCost);
        OnMPChanged?.Invoke(actor, actor.CurrentMP);

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

        CameraController.Instance?.ModePlayerAction();
        CameraController.Instance?.ZoomOnTransform(actor.transform, 4.0f, 0.3f); 

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
            yield return actor.transform.DOMove(originalPos, 0.3f).SetEase(Ease.OutBack).WaitForCompletion();
        }

        actor.PlayBattleAnim(PlayerCharacter.HashBattleIdle);
        CameraController.Instance?.ResetCamera(0.4f); 
        
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
        yield return actor.transform.DOMove(actor.transform.position + Vector3.right * 1f, 0.2f).SetEase(Ease.OutQuad).WaitForCompletion();
        actorCtrl?.PlayBattleAnim(PlayerCharacter.HashBattleIdle);

        yield return new WaitForSeconds(0.3f);

        foreach (var t in targets) ExecuteItemEffect(t, item);

        yield return new WaitForSeconds(0.5f);

        int idx = _playerParty.IndexOf(actor);
        actorCtrl?.PlayBattleAnim(PlayerCharacter.HashBattleMove);
        yield return actor.transform.DOMove(pm.GetPlayerDefaultPos(idx), 0.3f).SetEase(Ease.OutBack).WaitForCompletion();
        actorCtrl?.PlayBattleAnim(PlayerCharacter.HashBattleIdle);

        EndAction(); 
    }
    #endregion

    #region [ Enemy Action & QTE Handling ]
    private IEnumerator EnemyActionRoutine()
    {
        var enemy = _turnQueue[_currentActorIndex - 1] as EnemyCharacter;
        if (enemy == null) { EndAction(); yield break; }

        var action = enemy.DecideAction();
        var attackType = action switch
        {
            EnemyAction.UseSkill when IsZevEnemy(enemy) => EnemyAttackType.JumpOnly,
            EnemyAction.UseSkill => EnemyAttackType.RangedAoE,
            EnemyAction.EnragedAttack => EnemyAttackType.AoEAll,
            _ => EnemyAttackType.MeleeClose
        };

        OnEnemyActionStarted?.Invoke(enemy, attackType);
        CameraController.Instance?.ModeEnemyAction();

        if (attackType == EnemyAttackType.MeleeClose || attackType == EnemyAttackType.JumpOnly)
        {
            // 첫 번째 살아있는 플레이어 타겟팅
            int targetIdx = _playerParty.FindIndex(p => p.IsAlive);
            if (targetIdx >= 0)
            {
                var target = _playerParty[targetIdx];
                var targetCtrl = target.GetComponent<PlayerController>();
                bool isJumpOnlySkill = attackType == EnemyAttackType.JumpOnly;
                float defenseQteWindow = isJumpOnlySkill ? _zevCrossCutQTEWindow : _enemyDefenseQTEWindow;
                float attackVisualDuration = isJumpOnlySkill ? _zevCrossCutVisualDuration : _enemyAttackVisualDuration;
                bool shouldAdvanceToTarget = enemy.Data == null || !enemy.Data.IsLargeEnemy;

                if (shouldAdvanceToTarget)
                {
                    enemy.PlayBattleAnim(EnemyCharacter.HashBattleMove);
                    yield return enemy.transform.DOMove(target.transform.position + new Vector3(1.2f, 0, 0), 0.25f).SetEase(Ease.OutQuad).WaitForCompletion();
                }
                else
                {
                    enemy.PlayBattleAnim(EnemyCharacter.HashBattleIdle);
                }

                if (isJumpOnlySkill)
                {
                    enemy.PlaySkillAnim("CrossCut", EnemyCharacter.HashSkill);
                }
                else
                {
                    enemy.PlayBasicAttackEffect();
                    enemy.PlayBattleAnim(EnemyCharacter.HashAttack);
                }

                bool qteFinished = false;
                DefenseInput finalInput = DefenseInput.None;
                QTEManager.QTEGrade finalGrade = QTEManager.QTEGrade.Miss;

                // 공격 애니메이션은 한 번만 재생하고, QTE 판정은 별도로 유지합니다.
                QTEManager.Instance.StartDefenseQTE(defenseQteWindow, 1.0f, (input, grade) => { finalInput = input; finalGrade = grade; qteFinished = true; });
                yield return new WaitForSeconds(attackVisualDuration);
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
                    int reducedDmg = 0;

                    if (isJumpOnlySkill)
                    {
                        if (finalInput == DefenseInput.Jump)
                        {
                            reducedDmg = 0;
                            targetCtrl?.ExecuteJump();
                        }
                        else
                        {
                            reducedDmg = enemy.ATK;
                            if (finalInput == DefenseInput.Dodge) targetCtrl?.ExecuteDodge();
                            else if (finalInput == DefenseInput.Parry) targetCtrl?.ExecuteParry();
                        }
                    }
                    else if (finalInput == DefenseInput.Dodge || finalInput == DefenseInput.Jump)
                    {
                        reducedDmg = 0; 
                        if (finalInput == DefenseInput.Dodge) targetCtrl?.ExecuteDodge();
                        else targetCtrl?.ExecuteJump();
                    }
                    else // 패링
                    {
                        targetCtrl?.ExecuteParry(); 
                        if (finalGrade == QTEManager.QTEGrade.Perfect) 
                        { 
                            reducedDmg = 0;
                            target.HealMP(_mpOnParryPerfect); 
                            OnMPChanged?.Invoke(target, target.CurrentMP); 
                        }
                        else
                        {
                            reducedDmg = finalGrade switch { 
                                QTEManager.QTEGrade.Great => Mathf.RoundToInt(enemy.ATK * 0.15f), 
                                QTEManager.QTEGrade.Good  => Mathf.RoundToInt(enemy.ATK * 0.40f), 
                                QTEManager.QTEGrade.Bad   => Mathf.RoundToInt(enemy.ATK * 0.70f), 
                                _ => enemy.ATK 
                            };
                        }
                    }

                    if (reducedDmg > 0) 
                    {
                        int actualDmg = target.TakePureDamage(reducedDmg);
                        CameraController.Instance?.PlayHeavySlam(Vector3.right, 0.3f, true);
                        
                        // 🚨 핵심 해결: 패링을 못 쳐서 깎인 데미지도 체력바 갱신 방송!
                        OnDamageDealt?.Invoke(target, actualDmg, false);
                    }
                }

                yield return new WaitForSeconds(_enemyPostHitDelay);
                targetCtrl?.PlayBattleAnim(PlayerCharacter.HashBattleIdle);
                
                // 적군 제자리 복귀
                if (shouldAdvanceToTarget)
                {
                    enemy.PlayBattleAnim(ResolveEnemyReturnMoveHash(enemy));
                    yield return enemy.transform.DOMove(PositionManager.Instance.GetEnemyDefaultPos(_enemies.IndexOf(enemy)), 0.3f).SetEase(Ease.InQuad).WaitForCompletion();
                }
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

        EndAction(); 
    }
    #endregion

    #region [ Outro & Scene Transitions ]
    private bool CheckVictory() => _enemies != null && _enemies.Count > 0 && _enemies.TrueForAll(e => e == null || !e.IsAlive);
    private bool CheckDefeat()  => _playerParty.TrueForAll(p => !p.IsAlive);

    private IEnumerator RunRoutine()
    {
        Debug.Log("무사히 도망쳤다!");
        CommitOverworldEncounterResult(false);
        yield return StartCoroutine(BattleOutroRoutine(false));
    }

    private IEnumerator BattleEndRoutine()
    {
        CommitOverworldEncounterResult(CheckVictory());
        yield return _waitMedium;
        yield return StartCoroutine(BattleOutroRoutine(CheckVictory()));
    }

    private void CommitOverworldEncounterResult(bool isVictory)
    {
        var global = GlobalDataManager.Instance;
        if (global == null) return;

        string enemyId = global.CurrentEncounterEnemyId;
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

        
        
        // UI 결과창이 충분히 렌더링될 수 있도록 Realtime 사용
        yield return new WaitForSecondsRealtime(2.5f); 

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
        OnDamageDealt?.Invoke(target, damage, isPerfect);
    }

    public void InvokeMPChangedEvent(PlayerCharacter player, int newMP)
    {
        OnMPChanged?.Invoke(player, newMP);
    }
    #endregion
}