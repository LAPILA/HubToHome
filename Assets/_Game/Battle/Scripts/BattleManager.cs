using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;
using Unity.Cinemachine;
using Sirenix.OdinInspector;

public class BattleManager : MonoBehaviour
{
    public static BattleManager Instance { get; private set; }

    public event Action<BattleState>                OnStateChanged;
    public event Action<List<PlayerCharacter>, List<EnemyCharacter>> OnBattleStarted;
    public event Action<List<CharacterBase>>        OnTurnQueueUpdated;  
    public event Action<PlayerCharacter>            OnPlayerTurnStarted;  
    public event Action<EnemyCharacter, EnemyAttackType> OnEnemyActionStarted;
    public event Action<CharacterBase, int, bool>   OnDamageDealt;        
    public event Action<PlayerCharacter, int>       OnMPChanged;          
    public event Action<bool>                       OnBattleEnded;        
    public event Action<PlayerMenuAction>           OnTargetSelectionStarted;

    [BoxGroup("Battle Units")] public List<PlayerCharacter> _playerParty = new List<PlayerCharacter>();
    [BoxGroup("Battle Units")] public List<EnemyCharacter> _enemies = new List<EnemyCharacter>();

    [BoxGroup("Camera")] public CinemachineImpulseSource _impulseSource;
    [BoxGroup("Camera")] public float _hitImpulse  = 0.15f;

    [BoxGroup("MP Settings")] public int _mpPerTurn = 5;   
    [BoxGroup("MP Settings")] public int _mpOnParryPerfect = 20; 

    [Header("Seamless Battle Settings")]
    [Tooltip("체크 해제 시 오버월드용 심리스 매니저로 작동 (Start()에서 자동 전투 안 함)")]
    [SerializeField] private bool _isDedicatedBattleScene = false; // 🚨 자동 실행 버그 방지 플래그
    [SerializeField] private GameObject _battleUICanvas;
    [SerializeField] private GameObject _enemyBasePrefab;

    public BattleState CurrentState { get; private set; } = BattleState.Init;

    private readonly List<CharacterBase> _turnQueue = new List<CharacterBase>();
    private int _currentActorIndex = 0;

    private readonly WaitForSeconds _waitShort  = new WaitForSeconds(0.4f);
    private readonly WaitForSeconds _waitMedium = new WaitForSeconds(0.8f);

    private PlayerCharacter _pendingActor;
    private PlayerMenuAction _pendingAction;
    public SkillData CurrentPendingSkill { get; private set; }
    public ItemData  CurrentPendingItem  { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void Start() 
    {
        // 🚨 오버월드에 배치되었을 땐 시작하자마자 전투가 켜지는 것을 막아야 합니다.
        if (_isDedicatedBattleScene) StartCoroutine(DelayedStart());
    }

    private void ChangeState(BattleState next)
    {
        CurrentState = next;
        OnStateChanged?.Invoke(next);
        switch (next)
        {
            case BattleState.TurnCalc:           StartCoroutine(TurnCalcRoutine()); break;
            case BattleState.EnemyAction:        StartCoroutine(EnemyActionRoutine()); break;
            case BattleState.BattleEnd:          StartCoroutine(BattleEndRoutine()); break;
        }
    }

    // ── 1. 심리스 전투 시작 (AreaTrigger에서 호출) ──────────────────────
    public void StartSeamlessBattle(List<EnemyData> encounterEnemies, PlayerController playerCtrl)
    {
        Debug.Log("<color=cyan>[BattleManager] 심리스 전투 연출 시작!</color>");

        if (_battleUICanvas != null) _battleUICanvas.SetActive(true);

        _playerParty.Clear();
        PlayerCharacter playerChar = playerCtrl.GetComponent<PlayerCharacter>();
        if (playerChar != null)
        {
            // 🚨 파티 데이터가 여러명일 경우를 고려한 로직
            playerChar.LoadDataFromGlobal(GlobalDataManager.Instance.Party[0]); 
            _playerParty.Add(playerChar);
        }

        _enemies.Clear();
        var pm = PositionManager.Instance;
        
        for (int i = 0; i < encounterEnemies.Count; i++)
        {
            if (pm != null)
            {
                Vector3 spawnPos = pm.GetEnemyDefaultPos(i);
                GameObject enemyObj = Instantiate(_enemyBasePrefab, spawnPos, Quaternion.identity);
                EnemyCharacter enemyChar = enemyObj.GetComponent<EnemyCharacter>();
                
                if (enemyChar != null)
                {
                    enemyChar.Setup(encounterEnemies[i]);
                    _enemies.Add(enemyChar);
                }
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
        yield return playerCtrl.transform.DOMove(battlePos, 0.5f).SetEase(Ease.OutExpo).WaitForCompletion();
        
        playerCtrl.SetFacingDirection(3);
        playerCtrl.SetBattleMode(true);
    }
BattleUIController.Instance.ShowSkillQTE(Vector2.zero, "", 0f);
    yield return null; 
    BattleUIController.Instance.HideSkillQTE();

    OnBattleStarted?.Invoke(_playerParty, _enemies);
    ChangeState(BattleState.Init);
    ChangeState(BattleState.TurnCalc);
}

    // ── 2. 심리스 전투 종료 (승리/패배 후 호출) ──────────────────────
    private void EndSeamlessBattle(bool isVictory)
    {
        StartCoroutine(SeamlessOutroRoutine(isVictory));
    }

    private IEnumerator SeamlessOutroRoutine(bool isVictory)
{
    // 1. 결과창 표시
    OnBattleEnded?.Invoke(isVictory);
    yield return new WaitForSeconds(2.0f); 

    // 2. 데이터 저장 (HP, MP 등)
    foreach (var player in _playerParty)
    {
        if (player.IsAlive) player.SaveDataToGlobal();
    }

    // 🚨 3. 원래 맵으로 복귀 (핵심!)
    string returnScene = GlobalDataManager.Instance.LastOverworldScene;
    
    if (!string.IsNullOrEmpty(returnScene))
    {
        Debug.Log($"[BattleManager] {returnScene} 맵으로 복귀합니다.");
        SceneLoader.Instance?.LoadScene(returnScene);
    }
    else
    {
        // 돌아갈 맵 정보가 없다면 기본 로비나 첫 마을로 보냄 (방어코드)
        SceneLoader.Instance?.LoadScene("LobbyScene");
    }
}

    // 전용 배틀 씬용 스타트 코루틴
    private IEnumerator DelayedStart()
{
    // 1. 상태 초기화 및 UI 강제 활성화
    ChangeState(BattleState.Init);
    
    if (_battleUICanvas != null) 
    {
        _battleUICanvas.SetActive(true);
        // 🚨 중요: 캔버스가 켜진 직후 UI 컨트롤러가 Awake를 마칠 수 있도록 한 프레임 쉽니다.
        yield return null; 
    }

    var global = GlobalDataManager.Instance;
    var pm = PositionManager.Instance;

    // 2. 플레이어 배치
    if (_playerParty.Count == 0)
    {
        Debug.LogError("BattleManager의 Player Party 리스트가 비어있습니다! 인스펙터에서 플레이어를 할당하세요.");
    }

    for (int i = 0; i < _playerParty.Count; i++)
    {
        if (_playerParty[i] == null) continue;

        // 위치 이동
        if (pm != null) _playerParty[i].transform.position = pm.GetPlayerDefaultPos(i);
        
        // 데이터 로드 및 모드 전환
        if (global != null && i < global.Party.Count)
            _playerParty[i].LoadDataFromGlobal(global.Party[i]);

        var ctrl = _playerParty[i].GetComponent<PlayerController>();
        if (ctrl != null)
        {
            ctrl.SetBattleMode(true); // 이동 잠금 및 Idle 애니메이션
            ctrl.SetFacingDirection(3); // 오른쪽 보기
        }
    }

    // 3. 적 소환 (PendingEnemies가 있을 때만)
    if (global != null && global.PendingEnemies != null && global.PendingEnemies.Count > 0)
    {
        // 기존 적 제거
        foreach(var e in _enemies) if(e != null) Destroy(e.gameObject);
        _enemies.Clear();

        for (int i = 0; i < global.PendingEnemies.Count; i++)
        {
            if (pm != null && _enemyBasePrefab != null)
            {
                GameObject enemyObj = Instantiate(_enemyBasePrefab, pm.GetEnemyDefaultPos(i), Quaternion.identity);
                EnemyCharacter enemyChar = enemyObj.GetComponent<EnemyCharacter>();
                if (enemyChar != null)
                {
                    enemyChar.Setup(global.PendingEnemies[i]);
                    _enemies.Add(enemyChar);
                }
            }
        }
        global.PendingEnemies.Clear();
    }

    // 4. 전투 시작 이벤트
    OnBattleStarted?.Invoke(_playerParty, _enemies);

    yield return new WaitForSeconds(0.5f);
    ChangeState(BattleState.TurnCalc);
}
    private IEnumerator TurnCalcRoutine()
    {
        yield return null;
        _turnQueue.Clear();
        List<CharacterBase> aliveChars = new List<CharacterBase>();
        
        foreach (var p in _playerParty) if (p != null && p.IsAlive) aliveChars.Add(p);
        foreach (var e in _enemies)     if (e != null && e.IsAlive) aliveChars.Add(e);

        if (aliveChars.Count == 0 || CheckVictory() || CheckDefeat()) { EndAction(); yield break; }

        for (int i = 0; i < 8; i++) 
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
        if (_currentActorIndex >= _turnQueue.Count) { ChangeState(BattleState.TurnCalc); return; }

        var actor = _turnQueue[_currentActorIndex++];
        if (actor == null || !actor.IsAlive) { AdvanceTurn(); return; }

        actor.ProcessEffects();
        if (!actor.IsAlive) { AdvanceTurn(); return; }

        if (actor is PlayerCharacter player)
        {
            player.HealMP(_mpPerTurn); 
            OnMPChanged?.Invoke(player, player.CurrentMP); 
            OnPlayerTurnStarted?.Invoke(player);
            ChangeState(BattleState.PlayerActionSelect);
        }
        else if (actor is EnemyCharacter enemy)
        {
            ChangeState(BattleState.EnemyAction);
        }
    }

    // ── 플레이어 입력 처리 및 라우팅 ─────────────────────────────────────
    public void OnPlayerActionSelected(PlayerCharacter actor, PlayerMenuAction action)
    {
        _pendingActor = actor;
        _pendingAction = action;

        // Attack과 Act, Skill 모두 타겟 선택이 필요합니다.
        if (action == PlayerMenuAction.Attack || action == PlayerMenuAction.Skill || action == PlayerMenuAction.Act)
        {
            actor.PlayBattleAnim(PlayerCharacter.HashBattleReady);
            OnTargetSelectionStarted?.Invoke(action);
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
        CurrentPendingSkill = skill; // Act도 SkillData를 공유하므로 여기 담깁니다.
        CurrentPendingItem = item;

        bool isAoE = (skill != null && skill.IsAoE) || (item != null && item.IsAoE);
        if (isAoE) ConfirmTargetAndExecute(-1); 
        else       OnTargetSelectionStarted?.Invoke(action);
    }

    public void CancelTargetSelection() 
    {
        _pendingActor?.PlayBattleAnim(PlayerCharacter.HashBattleIdle);
        ChangeState(BattleState.PlayerActionSelect);
    }

    public void ConfirmTargetAndExecute(int targetIndex)
    {
        if (CurrentState == BattleState.ActionExecute) return; // 난타 완전 차단
        ChangeState(BattleState.ActionExecute);

        if (_pendingAction == PlayerMenuAction.Attack)
            StartCoroutine(ExecuteAttack(_pendingActor, targetIndex));
        else if ((_pendingAction == PlayerMenuAction.Skill || _pendingAction == PlayerMenuAction.Act) && CurrentPendingSkill != null)
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

    // ── 단일 출구 (여기서 턴을 깔끔하게 마무리하고 다음으로 넘김) ──
    // ── 턴 종료 및 승패 체크 ─────────────────────────────────────
    private void EndAction()
    {
        Time.timeScale = 1.0f;
        _pendingActor = null;
        CurrentPendingSkill = null;
        CurrentPendingItem = null;
        CameraController.Instance?.ResetCamera(0.4f);

        if (CheckVictory()) ChangeState(BattleState.BattleEnd);
        else if (CheckDefeat()) ChangeState(BattleState.BattleEnd);
        else AdvanceTurn();
    }

    // ── 도망치기 로직 ─────────────────────────────────────────────
    private IEnumerator RunRoutine()
    {
        Debug.Log("무사히 도망쳤다!");
        // 도망은 승리가 아니므로 false 전달
        yield return StartCoroutine(BattleOutroRoutine(false));
    }

    // ── 전투 종료 루틴 (상태 머신에 의해 호출) ──────────────────────
    private IEnumerator BattleEndRoutine()
    {
        yield return _waitMedium;
        bool isVictory = CheckVictory();
        
        // 통합된 아웃트로 루틴 실행
        yield return StartCoroutine(BattleOutroRoutine(isVictory));
    }

    // ── [통합] 전투 종료/복귀 로직 ──────────────────────────────────
    private IEnumerator BattleOutroRoutine(bool isVictory)
    {
        // 1. 결과창 표시
        OnBattleEnded?.Invoke(isVictory);
        yield return new WaitForSeconds(2.5f); 

        // 2. 데이터 저장
        foreach (var player in _playerParty)
        {
            if (player != null && player.IsAlive) 
                player.SaveDataToGlobal();
        }

        // 3. 상황별 종료 처리
        if (_isDedicatedBattleScene)
        {
            string returnScene = GlobalDataManager.Instance.LastOverworldScene;
            SceneLoader.Instance?.LoadScene(!string.IsNullOrEmpty(returnScene) ? returnScene : "LobbyScene");
        }
        else
        {
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
            anim.Rebind();      // 모든 상태/파라미터 초기화
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
            Debug.Log("[BattleManager] 심리스 전투 종료! 오버월드 Idle 복귀 완료.");
        }
    }

    private IEnumerator ExecuteAttack(PlayerCharacter actor, int targetIndex)
    {
        if (targetIndex >= _enemies.Count || !_enemies[targetIndex].IsAlive) { EndAction(); yield break; }
        
        var target = _enemies[targetIndex];
        var pm = PositionManager.Instance;

        CameraController.Instance?.ModePlayerAction();
        CameraController.Instance?.ZoomOnTransform(actor.transform, 4.2f, 0.3f); 

        Vector3 frontPos = target.transform.position + new Vector3(-1.8f, 0, 0); 
        
        actor.PlayBattleAnim(PlayerCharacter.HashBattleMove);
        yield return actor.transform.DOMove(frontPos, 0.2f).SetEase(Ease.OutCubic).WaitForCompletion();

        Vector3 pullBackPos = frontPos + new Vector3(-0.5f, 0, 0);
        yield return actor.transform.DOMove(pullBackPos, 0.15f).SetEase(Ease.OutBack).WaitForCompletion();

        Vector3 behindPos = target.transform.position + new Vector3(1.8f, 0, 0);
        
        actor.PlayBattleAnim(PlayerCharacter.HashAttack); 
        actor.transform.DOMove(behindPos, 0.15f).SetEase(Ease.InExpo);

        yield return new WaitForSeconds(0.08f);

        int dmg = target.TakeDamage(actor.ATK);
        CameraController.Instance?.PlayDashThroughImpact(1.0f);
        OnDamageDealt?.Invoke(target, dmg, false);
        
        yield return new WaitForSeconds(0.3f); 

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

    private IEnumerator EnemyActionRoutine()
    {
        var enemy = _turnQueue[_currentActorIndex - 1] as EnemyCharacter;
        if (enemy == null) { EndAction(); yield break; }

        var action = enemy.DecideAction();
        var attackType = action switch { EnemyAction.UseSkill => EnemyAttackType.RangedAoE, EnemyAction.EnragedAttack => EnemyAttackType.AoEAll, _ => EnemyAttackType.MeleeClose };

        OnEnemyActionStarted?.Invoke(enemy, attackType);
        CameraController.Instance?.ModeEnemyAction();

        if (attackType == EnemyAttackType.MeleeClose)
        {
            // 랜덤 타겟이 아닌 첫번째 살아있는 플레이어를 타겟팅 (추후 어그로 시스템 확장 가능)
            int targetIdx = _playerParty.FindIndex(p => p.IsAlive);
            if (targetIdx >= 0)
            {
                var target = _playerParty[targetIdx];
                var targetCtrl = target.GetComponent<PlayerController>();

                enemy.PlayBattleAnim(EnemyCharacter.HashBattleMove);
                yield return enemy.transform.DOMove(target.transform.position + new Vector3(1.2f, 0, 0), 0.25f).SetEase(Ease.OutQuad).WaitForCompletion();

                enemy.PlayBattleAnim(EnemyCharacter.HashAttack);
                
                bool qteFinished = false;
                DefenseInput finalInput = DefenseInput.None;
                QTEManager.QTEGrade finalGrade = QTEManager.QTEGrade.Miss;

                QTEManager.Instance.StartDefenseQTE(0.8f, 1.0f, (input, grade) => { finalInput = input; finalGrade = grade; qteFinished = true; });
                yield return new WaitUntil(() => qteFinished);

                if (finalGrade == QTEManager.QTEGrade.Miss)
                {
                    target.TakePureDamage(enemy.ATK); targetCtrl.PlayHurtEffect();
                    CameraController.Instance?.PlayHeavySlam(Vector3.left, 1.0f, true);
                }
                else
                {
                    if (finalInput == DefenseInput.Parry) 
                    { 
                        targetCtrl.ExecuteParry(); 
                        if (finalGrade == QTEManager.QTEGrade.Perfect) 
                        { 
                            target.HealMP(_mpOnParryPerfect); 
                            OnMPChanged?.Invoke(target, target.CurrentMP); 
                        } 
                    }
                    else if (finalInput == DefenseInput.Dodge) targetCtrl.ExecuteDodge();
                    else if (finalInput == DefenseInput.Jump)  targetCtrl.ExecuteJump();

                    int reducedDmg = finalGrade switch { 
                        QTEManager.QTEGrade.Perfect => (finalInput == DefenseInput.Parry ? 0 : Mathf.RoundToInt(enemy.ATK * 0.05f)), 
                        QTEManager.QTEGrade.Great => Mathf.RoundToInt(enemy.ATK * 0.25f), 
                        QTEManager.QTEGrade.Good => Mathf.RoundToInt(enemy.ATK * 0.55f), 
                        QTEManager.QTEGrade.Bad => Mathf.RoundToInt(enemy.ATK * 0.80f), 
                        _ => enemy.ATK 
                    };
                    if (reducedDmg > 0) target.TakePureDamage(reducedDmg);
                    CameraController.Instance?.PlayHeavySlam(Vector3.right, 0.3f, true);
                }

                yield return new WaitForSeconds(0.4f);
                targetCtrl?.PlayBattleAnim(PlayerCharacter.HashBattleIdle);
                enemy.PlayBattleAnim(EnemyCharacter.HashBattleMove);
                yield return enemy.transform.DOMove(PositionManager.Instance.GetEnemyDefaultPos(_enemies.IndexOf(enemy)), 0.3f).SetEase(Ease.InQuad).WaitForCompletion();
                enemy.PlayBattleAnim(EnemyCharacter.HashBattleIdle);
            }
        }
        else
        {
            yield return new WaitForSeconds(1.0f);
            foreach (var p in _playerParty) { if (!p.IsAlive) continue; p.TakePureDamage(enemy.ATK); p.GetComponent<PlayerController>()?.PlayHurtEffect(); OnDamageDealt?.Invoke(p, enemy.ATK, false); }
            _impulseSource?.GenerateImpulse(_hitImpulse);
            yield return _waitMedium;
        }

        EndAction(); 
    }

    public static void ExecuteItemEffect(CharacterBase target, ItemData item)
    {
        if (item == null || target == null) return;

        if (item.ActionType == EffectActionType.Heal)
        {
            int maxStat = (item.TargetStat == TargetStatType.HP) ? target.MaxHP : target.MaxMP;
            int amount = 0;

            if (item.CalcType == ValueCalcType.Flat) 
                amount = item.EffectValue;
            else if (item.CalcType == ValueCalcType.Percentage) 
                amount = Mathf.RoundToInt(maxStat * (item.EffectValue * 0.01f));
            else if (item.CalcType == ValueCalcType.Full) 
                amount = maxStat;
                
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
            // 🚨 Enum 방식이 아닌 String 방식으로 변경한 부분 적용 완료
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

    private bool CheckVictory() => _enemies.TrueForAll(e => !e.IsAlive);
    private bool CheckDefeat()  => _playerParty.TrueForAll(p => !p.IsAlive);
    // ── 외부 호출용 이벤트 브리지 ──────────────────────────────────
    
    /// <summary>
    /// SkillActionBlock이나 외부 로직에서 데미지 이벤트를 발생시킬 때 사용합니다.
    /// UI(BattleUIController)가 이 이벤트를 구독하여 HP 바를 갱신합니다.
    /// </summary>
    /// <param name="target">데미지를 입은 대상</param>
    /// <param name="damage">적용된 최종 데미지 (음수일 경우 회복으로 처리 가능)</param>
    /// <param name="isPerfect">QTE가 퍼펙트였는지 여부</param>
    public void InvokeDamageEvent(CharacterBase target, int damage, bool isPerfect)
    {
        OnDamageDealt?.Invoke(target, damage, isPerfect);
    }

    /// <summary>
    /// MP 변경 사항을 UI에 알리기 위한 브리지 메서드입니다.
    /// </summary>
    public void InvokeMPChangedEvent(PlayerCharacter player, int newMP)
    {
        OnMPChanged?.Invoke(player, newMP);
    }
}