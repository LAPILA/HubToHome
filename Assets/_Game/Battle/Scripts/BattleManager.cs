using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;
using Unity.Cinemachine;
using UnityEngine.InputSystem;
using Sirenix.OdinInspector;

/// <summary>
/// 전투 흐름 총괄 싱글톤 (Model/Presenter 역할).
/// BattleUIController(View)와 C# event로만 통신합니다.
/// 
/// 흐름: Init → TurnCalc → PlayerActionSelect → ActionExecute → EnemyAction → BattleEnd
/// 
/// Inspector 연결:
/// - _playerParty (최대 4), _enemies (최대 8)
/// - _impulseSource (CinemachineImpulseSource)
/// - PositionManager는 씬에 별도 배치
/// </summary>
public class BattleManager : MonoBehaviour
{
    public static BattleManager Instance { get; private set; }

    // ── 이벤트 (View 구독용) ──────────────────────────────────
    public event Action<BattleState>                    OnStateChanged;
    public event Action<List<PlayerCharacter>, List<EnemyCharacter>> OnBattleStarted;
    public event Action<List<CharacterBase>>            OnTurnQueueUpdated;   // 최대 6개
    public event Action<PlayerCharacter>                OnPlayerTurnStarted;  // 현재 행동 캐릭터
    public event Action<EnemyCharacter, EnemyAttackType> OnEnemyActionStarted;
    public event Action<CharacterBase, int, bool>       OnDamageDealt;        // (target, damage, isCrit)
    public event Action<PlayerCharacter, int>           OnMPChanged;          // (player, newMP)
    public event Action<bool>                           OnBattleEnded;        // true=victory

    public event Action<PlayerMenuAction> OnTargetSelectionStarted;

    // ── 전투 참가자 ───────────────────────────────────────────
    [BoxGroup("Battle Units"), LabelText("아군 파티 (최대 3)")]
    [SerializeField] private List<PlayerCharacter> _playerParty = new List<PlayerCharacter>();

    [BoxGroup("Battle Units"), LabelText("적 (최대 3)")]
    [SerializeField] private List<EnemyCharacter> _enemies = new List<EnemyCharacter>();

    // ── 카메라 ────────────────────────────────────────────────
    [BoxGroup("Camera"), LabelWidth(140)]
    [SerializeField] private CinemachineImpulseSource _impulseSource;

    [BoxGroup("Camera"), LabelWidth(140)]
    [SerializeField] private float _hitImpulse  = 0.15f;

    [BoxGroup("Camera"), LabelWidth(140)]
    [SerializeField] private float _missImpulse = 0.05f;

    // ── MP 설정 ───────────────────────────────────────────────
    [BoxGroup("MP Settings"), LabelWidth(160)]
    [SerializeField] private int _mpPerTurn       = 5;   // 턴 시작 시 자동 회복

    [BoxGroup("MP Settings"), LabelWidth(160)]
    [SerializeField] private int _mpOnParryPerfect = 20; // 패링 Perfect 시 회복

    [BoxGroup("MP Settings"), LabelWidth(160)]
    [SerializeField] private int _mpOnDefenseSuccess = 10; // 회피/점프 성공 시 회복

    // ── 상태 ──────────────────────────────────────────────────
    public BattleState CurrentState { get; private set; } = BattleState.Init;

    // 턴 대기열 (SPD 기반 정렬, 최대 6개 표시)
    private readonly List<CharacterBase> _turnQueue = new List<CharacterBase>();
    private int _currentActorIndex = 0;

    // MP (플레이어별, 0~100)
    private readonly Dictionary<PlayerCharacter, int> _mpMap = new Dictionary<PlayerCharacter, int>();

    // 캐싱
    private readonly WaitForSeconds _waitShort  = new WaitForSeconds(0.4f);
    private readonly WaitForSeconds _waitMedium = new WaitForSeconds(0.8f);

    // 내부 상태 변수
    private PlayerCharacter _pendingActor;
    private PlayerMenuAction _pendingAction;

    // ── 초기화 ────────────────────────────────────────────────
    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void Start()
    {
        // MP 초기화
        foreach (var p in _playerParty)
            _mpMap[p] = 0;

        // BattleUIController가 OnEnable에서 구독하므로 1프레임 뒤에 이벤트 발생
        StartCoroutine(DelayedStart());
    }

    private System.Collections.IEnumerator DelayedStart()
    {
        yield return new WaitForSeconds(0.2f);

        var pm = PositionManager.Instance;
        if (pm != null)
        {
            for (int i = 0; i < _playerParty.Count; i++)
            {
                if (_playerParty[i] != null)
                    _playerParty[i].transform.position = pm.GetPlayerDefaultPos(i);
            }
            for (int i = 0; i < _enemies.Count; i++)
            {
                if (_enemies[i] != null)
                    _enemies[i].transform.position = pm.GetEnemyDefaultPos(i);
            }
        }
        foreach (var p in _playerParty)
        {
            if (p != null)
            {
                if (p.CurrentHP <= 0) p.Heal(p.MaxHP > 0 ? p.MaxHP : 100);
                var ctrl = p.GetComponent<PlayerController>();
                ctrl?.SetBattleMode(true);
                ctrl?.SetFacingDirection(3);
            }
        }

        foreach (var e in _enemies)
        {
            if (e != null && e.CurrentHP <= 0)
            {
                int max = e.Data != null ? e.Data.MaxHP : 100;
                e.Heal(max);
            }
        }

        Debug.Log($"[BattleManager] 전투 시작! 아군: {_playerParty.Count}명, 적: {_enemies.Count}마리");
        OnBattleStarted?.Invoke(_playerParty, _enemies);
        ChangeState(BattleState.Init);
    }

    // ── 상태 전환 ─────────────────────────────────────────────
    private void ChangeState(BattleState next)
    {
        CurrentState = next;
        OnStateChanged?.Invoke(next);

        switch (next)
        {
            case BattleState.Init:               StartCoroutine(InitRoutine());         break;
            case BattleState.TurnCalc:           StartCoroutine(TurnCalcRoutine());     break;
            case BattleState.PlayerActionSelect: StartCoroutine(PlayerSelectRoutine()); break;
            case BattleState.ActionExecute:      /* BattleMenuUI 콜백으로 진행 */       break;
            case BattleState.EnemyAction:        StartCoroutine(EnemyActionRoutine());  break;
            case BattleState.BattleEnd:          StartCoroutine(BattleEndRoutine());    break;
        }
    }

    // ── Init ──────────────────────────────────────────────────
    private IEnumerator InitRoutine()
    {
        yield return _waitShort;
        ChangeState(BattleState.TurnCalc);
    }

    // ── TurnCalc: SPD 기반 대기열 정렬 ───────────────────────
    private IEnumerator TurnCalcRoutine()
    {
        _turnQueue.Clear();

        // 살아있는 모든 참가자 수집
        List<CharacterBase> aliveChars = new List<CharacterBase>();
        foreach (var p in _playerParty) if (p != null && p.IsAlive) aliveChars.Add(p);
        foreach (var e in _enemies)     if (e != null && e.IsAlive) aliveChars.Add(e);

        if (aliveChars.Count == 0)
        {
            yield return null;
            AdvanceTurn();
            yield break;
        }

        Dictionary<CharacterBase, float> simTime = new Dictionary<CharacterBase, float>();
        
        foreach (var c in aliveChars)
        {
            float randomFactor = UnityEngine.Random.Range(0.95f, 1.05f);
            simTime[c] = (1000f / Mathf.Max(1, c.SPD)) * randomFactor;
        }

        for (int i = 0; i < 8; i++)
        {
            CharacterBase nextActor = null;
            float minTime = float.MaxValue;

            foreach (var c in aliveChars)
            {
                if (simTime[c] < minTime)
                {
                    minTime = simTime[c];
                    nextActor = c;
                }
            }

            _turnQueue.Add(nextActor);

            foreach (var c in aliveChars)
            {
                simTime[c] -= minTime;
            }

            float randomFactor = UnityEngine.Random.Range(0.95f, 1.05f);
            simTime[nextActor] += (1000f / Mathf.Max(1, nextActor.SPD)) * randomFactor;
        }

        _currentActorIndex = 0;
        
        // UI 컨트롤러로 완성된 8턴 대기열 전송
        OnTurnQueueUpdated?.Invoke(_turnQueue);

        yield return _waitShort;
        AdvanceTurn();
    }

    // ── 다음 행동자 결정 ──────────────────────────────────────
    private void AdvanceTurn()
    {
        if (_currentActorIndex >= _turnQueue.Count)
        {
            ChangeState(BattleState.TurnCalc);
            return;
        }

        var actor = _turnQueue[_currentActorIndex];
        _currentActorIndex++;

        if (!actor.IsAlive) { AdvanceTurn(); return; }

        if (actor is PlayerCharacter player)
        {
            // 턴 시작 MP 회복
            AddMP(player, _mpPerTurn);
            // 상태 이상 틱
            player.ProcessEffects();
            OnPlayerTurnStarted?.Invoke(player);
            ChangeState(BattleState.PlayerActionSelect);
        }
        else if (actor is EnemyCharacter enemy)
        {
            enemy.ProcessEffects();
            ChangeState(BattleState.EnemyAction);
        }
    }

    // ── PlayerActionSelect ────────────────────────────────────
    private IEnumerator PlayerSelectRoutine()
    {
        // BattleMenuUI가 OnStateChanged 이벤트를 받아 메뉴를 표시함
        yield return null;
    }

    /// <summary>BattleMenuUI에서 플레이어 커맨드 선택 시 호출</summary>
    public void OnPlayerActionSelected(PlayerCharacter actor, PlayerMenuAction action)
    {
        _pendingActor = actor;
        _pendingAction = action;

        if (action == PlayerMenuAction.Attack || action == PlayerMenuAction.Skill)
        {
            OnTargetSelectionStarted?.Invoke(action);
        }
        else if (action == PlayerMenuAction.Item)
        {
            StartCoroutine(ExecuteItem(actor));
        }
        else if (action == PlayerMenuAction.Run)
        {
            TryRun();
        }
    }

    /// <summary>BattleUIController에서 타겟을 고르고 Z키로 확정했을 때 호출</summary>
    public void ConfirmTargetAndExecute(int targetIndex)
    {
        if (_pendingAction == PlayerMenuAction.Attack)
            StartCoroutine(ExecuteAttack(_pendingActor, targetIndex));
        else if (_pendingAction == PlayerMenuAction.Skill)
        {
            //TODO: 스킬 제작해야함
        }
    }
    
    /// <summary>타겟 선택 중 X키를 눌러 취소했을 때 메뉴 복구용</summary>
    public void CancelTargetSelection()
    {
        ChangeState(BattleState.PlayerActionSelect);
    }

    // ── 플레이어 일반 공격 (이동 및 애니메이션 동기화) ──────────
    private IEnumerator ExecuteAttack(PlayerCharacter actor, int targetIndex)
{
    ChangeState(BattleState.ActionExecute);

    if (targetIndex >= _enemies.Count || !_enemies[targetIndex].IsAlive)
    {
        AdvanceTurn(); yield break;
    }

    var target = _enemies[targetIndex];
    var pm = PositionManager.Instance;
    var actorCtrl = actor.GetComponent<PlayerController>();

    // ── [Step 1: 기본 접근] ──
    // 적의 앞쪽 지점으로 이동
    Vector3 frontPos = target.transform.position + new Vector3(-1.8f, 0, 0); 
    CameraController.Instance?.ModePlayerAction();
    CameraController.Instance?.Zoom(4.2f, 0.3f); // 공격 집중을 위한 줌인

    actorCtrl?.PlayBattleAnim(PlayerController.HashBattleMove);
    yield return actor.transform.DOMove(frontPos, 0.2f).SetEase(Ease.OutCubic).WaitForCompletion();

    // ── [Step 2: 예비 동작 (Anticipation)] ──
    // 뒤로 살짝 물러나며 힘을 모으는 연출
    Vector3 pullBackPos = frontPos + new Vector3(-0.5f, 0, 0);
    yield return actor.transform.DOMove(pullBackPos, 0.15f).SetEase(Ease.OutBack).WaitForCompletion();

    // ── [Step 3: 관통 공격 (Dash Through)] ──
    // 적을 촥! 베면서 적 뒤로 이동
    Vector3 behindPos = target.transform.position + new Vector3(1.8f, 0, 0);
    Vector3 dashDir = (behindPos - pullBackPos).normalized;

    // 타격 애니메이션 실행
    actorCtrl?.ExecuteAttack(); 
    
    // 공격하며 적의 뒤로 순간적으로 이동 (InExpo로 아주 빠르게)
    actor.transform.DOMove(behindPos, 0.15f).SetEase(Ease.InExpo);

    // [타이밍 핵심] 적과 부딪히는 찰나에 이펙트와 카메라 연출
    yield return new WaitForSeconds(0.08f); // 찰나의 대기

    // 타격 판정 및 카메라 슬램
    int dmg = target.TakeDamage(actor.ATK);
    CameraController.Instance?.PlayDashThroughImpact(dashDir); // 카메라 연출
    
    // 히트 스탑 (중량감)
    Time.timeScale = 0.05f;
    DOVirtual.DelayedCall(0.1f, () => Time.timeScale = 1f).SetUpdate(true);

    OnDamageDealt?.Invoke(target, dmg, false);
    yield return new WaitForSeconds(0.3f); // 적 뒤에서 폼 잡는 시간

    // ── [Step 4: 복귀] ──
    int idx = _playerParty.IndexOf(actor);
    actorCtrl?.PlayBattleAnim(PlayerController.HashBattleIdle);
    
    // [중요] 공격 종료 시 카메라 완전 리셋
    CameraController.Instance?.ResetCamera(0.4f);

    if (CheckVictory()) { ChangeState(BattleState.BattleEnd); yield break; }
    AdvanceTurn();
    CameraController.Instance?.ModeBattleIdle(); // 카메라 복구
    
    actorCtrl?.PlayBattleAnim(PlayerController.HashBattleMove);
    // 적 뒤에서 원래 자리로 돌아올 때는 위로 살짝 포물선을 그리며 복귀하면 더 멋짐
    yield return actor.transform.DOJump(pm.GetPlayerDefaultPos(idx), 0.5f, 1, 0.3f).SetEase(Ease.OutQuad).WaitForCompletion();
    
    actorCtrl?.PlayBattleAnim(PlayerController.HashBattleIdle);

    if (CheckVictory()) { ChangeState(BattleState.BattleEnd); yield break; }
    AdvanceTurn();
}

    // ── 스킬 (데이터 기반 다이내믹 연출) ─────────────────────────
    private IEnumerator ExecuteSkill(PlayerCharacter actor, int targetIndex, SkillData skill)
    {
        ChangeState(BattleState.ActionExecute);

        if (targetIndex >= _enemies.Count || !_enemies[targetIndex].IsAlive)
        {
            AdvanceTurn(); yield break;
        }

        var target = _enemies[targetIndex];
        var pm     = PositionManager.Instance;
        var actorCtrl = actor.GetComponent<PlayerController>();

        // 1. QTE 처리
        bool qteFinished = false;
        QTEManager.QTEGrade resultGrade = QTEManager.QTEGrade.Miss;

        if (skill.QTEType != QTEType.None)
        {
            QTEManager.Instance.StartSkillQTE(1.0f);
            Action<QTEManager.QTEGrade> onComplete = null;
            onComplete = (grade) => {
                resultGrade = grade;
                qteFinished = true;
                QTEManager.Instance.OnSkillQTECompleted -= onComplete;
            };
            QTEManager.Instance.OnSkillQTECompleted += onComplete;
            yield return new WaitUntil(() => qteFinished);
        }

        // QTE 결과에 따른 최종 배율
        float finalMult = skill.DamageMultiplier * (resultGrade == QTEManager.QTEGrade.Perfect ? skill.QTESuccessMultiplier : skill.QTEFailMultiplier);

        // 2. 이동 (돌진형 스킬일 경우에만)
        if (skill.CastType == SkillCastType.MeleeDash)
        {
            Transform frontPivot = target.transform.Find("Pivots/Front");
            Vector3 attackPos = (frontPivot != null) ? frontPivot.position : target.transform.position + new Vector3(-1.2f, 0, 0);

            actorCtrl?.PlayBattleAnim(PlayerController.HashBattleMove);
            yield return actor.transform.DOMove(attackPos, 0.25f).SetEase(Ease.Linear).WaitForCompletion();
        }

        // 3. 공격 애니메이션 시작
        actorCtrl?.ExecuteAttack();

        // 4. VFX 및 데미지 타이밍 제어 (SkillData 기반)
        float timer = 0f;
        bool vfxSpawned = false;
        bool damageDealt = false;
        float maxDelay = Mathf.Max(skill.VFXSpawnDelay, skill.DamageDelay);

        while (timer <= maxDelay)
        {
            timer += Time.deltaTime;

            // 지정된 시간이 되면 VFX 소환 (단 1번만)
            if (timer >= skill.VFXSpawnDelay && !vfxSpawned && skill.EffectPrefab != null)
            {
                Transform spawnPivot = skill.SpawnVFXOnTarget ? 
                    target.transform.Find("Pivots/Center") ?? target.transform : 
                    actor.transform.Find("Pivots/Front") ?? actor.transform;

                ObjectPoolManager.Instance.Spawn(skill.EffectPrefab, spawnPivot.position, Quaternion.identity);
                vfxSpawned = true;
            }

            // 지정된 시간이 되면 데미지 적용
            if (timer >= skill.DamageDelay && !damageDealt)
            {
                int dmg = target.TakeDamage(Mathf.RoundToInt(actor.ATK * finalMult));
                bool isCrit = resultGrade == QTEManager.QTEGrade.Perfect;
                
                _impulseSource?.GenerateImpulse(isCrit ? _hitImpulse * 1.5f : _hitImpulse);
                OnDamageDealt?.Invoke(target, dmg, isCrit);
                damageDealt = true;
            }

            yield return null;
        }

        // 여운 대기
        yield return new WaitForSeconds(0.3f);

        // 5. 복귀 (돌진형 스킬이었을 경우만)
        if (skill.CastType == SkillCastType.MeleeDash)
        {
            int idx = _playerParty.IndexOf(actor);
            actorCtrl?.PlayBattleAnim(PlayerController.HashBattleMove);
            yield return actor.transform.DOMove(pm.GetPlayerDefaultPos(idx), 0.25f).SetEase(Ease.OutQuad).WaitForCompletion();
        }
        
        actorCtrl?.PlayBattleAnim(PlayerController.HashBattleIdle);

        if (CheckVictory()) { ChangeState(BattleState.BattleEnd); yield break; }
        AdvanceTurn();
    }

    // ── 아이템 (TODO) ─────────────────────────────────────────
    private IEnumerator ExecuteItem(PlayerCharacter actor)
    {
        ChangeState(BattleState.ActionExecute);
        Debug.Log("[BattleManager] Item use — TODO");
        yield return _waitShort;
        AdvanceTurn();
    }

    // ── 도망 ──────────────────────────────────────────────────
    private void TryRun()
    {
        if (UnityEngine.Random.value < 0.5f)
        {
            Debug.Log("[BattleManager] Escaped!");
            ChangeState(BattleState.BattleEnd);
        }
        else
        {
            Debug.Log("[BattleManager] Failed to escape.");
            AdvanceTurn();
        }
    }

    // ── EnemyAction ───────────────────────────────────────────
    private IEnumerator EnemyActionRoutine()
    {
        var enemy = GetCurrentEnemy();
        if (enemy == null) { AdvanceTurn(); yield break; }

        var action = enemy.DecideAction();
        var attackType = ResolveAttackType(enemy, action);

        OnEnemyActionStarted?.Invoke(enemy, attackType);
        var pm = PositionManager.Instance;

        // 적 행동 시작 시 카메라 적군 포커스
        CameraController.Instance?.ModeEnemyAction();

        switch (attackType)
        {
            case EnemyAttackType.MeleeClose:
                yield return StartCoroutine(EnemyMeleeRoutine(enemy, pm));
                break;
            case EnemyAttackType.RangedAoE:
            case EnemyAttackType.AoEAll:
                yield return StartCoroutine(EnemyAoERoutine(enemy, attackType));
                break;
        }

        CameraController.Instance?.ResetCamera(0.5f);

        if (CheckDefeat()) { ChangeState(BattleState.BattleEnd); yield break; }
        AdvanceTurn();
    }

    // ── 적 근거리 단일 공격 및 실시간 방어 ──────────────────────
    private IEnumerator EnemyMeleeRoutine(EnemyCharacter enemy, PositionManager pm)
{
    int targetIdx = GetAlivePlayerIndex();
    if (targetIdx < 0) yield break;

    var target = _playerParty[targetIdx];
    var targetCtrl = target.GetComponent<PlayerController>();

    // 1. 접근
    enemy.PlayBattleAnim(EnemyCharacter.HashBattleMove);
    Vector3 attackPos = target.transform.position + new Vector3(1.2f, 0, 0); 
    yield return enemy.transform.DOMove(attackPos, 0.25f).SetEase(Ease.OutQuad).WaitForCompletion();

    // 2. 방어 입력 감지 (입력 잠금 장치 포함)
    float defenseWindow = 0.8f;
    float elapsed = 0f;
    bool defensed = false;
    bool inputTaken = false; // 🚨 연타 방지용 플래그

    enemy.PlayBattleAnim(EnemyCharacter.HashAttack);

    while (elapsed < defenseWindow)
    {
        elapsed += Time.deltaTime;

        // 이미 입력을 했다면 더 이상 체크 안 함 (연타 방지 핵심)
        if (!inputTaken)
        {
            // Z(패링)
            if (Keyboard.current.zKey.wasPressedThisFrame)
            {
                inputTaken = true; // 입력 한 번으로 제한
                targetCtrl.ExecuteParry();
                if (elapsed >= 0.3f && elapsed <= 0.6f) defensed = true;
                if (defensed) break; // 패링 성공 시 즉시 루프 탈출
            }
            // C(회피), Space(점프)
            else if (Keyboard.current.cKey.wasPressedThisFrame)
            {
                inputTaken = true;
                targetCtrl.ExecuteDodge();
                defensed = true;
                break;
            }
            else if (Keyboard.current.spaceKey.wasPressedThisFrame)
            {
                inputTaken = true;
                targetCtrl.ExecuteJump();
                defensed = true;
                break;
            }
        }
        yield return null;
    }

    // 3. 결과 적용
    if (!defensed)
    {
        target.TakePureDamage(enemy.ATK);
        targetCtrl.PlayHurtEffect();
        CameraController.Instance?.PlayHeavySlam(Vector3.left, 1.0f, true);
    }
    else
    {
        // 성공 피드백 (방향 자유롭게 조절 가능)
        CameraController.Instance?.PlayHeavySlam(Vector3.right, 0.3f, true);
    }

    yield return new WaitForSeconds(0.4f);

    // 상태 리셋
    targetCtrl?.PlayBattleAnim(PlayerController.HashBattleIdle);

    // 4. 복귀
    enemy.PlayBattleAnim(EnemyCharacter.HashBattleMove);
    yield return enemy.transform.DOMove(pm.GetEnemyDefaultPos(_enemies.IndexOf(enemy)), 0.3f).SetEase(Ease.InQuad).WaitForCompletion();
    enemy.PlayBattleAnim(EnemyCharacter.HashBattleIdle);
    
    CameraController.Instance?.ResetCamera(0.5f);
}

    private IEnumerator EnemyAoERoutine(EnemyCharacter enemy, EnemyAttackType type)
    {
        yield return new WaitForSeconds(1.0f);

        foreach (var p in _playerParty)
        {
            if (!p.IsAlive) continue;
            p.TakePureDamage(enemy.ATK);
            p.GetComponent<PlayerController>()?.PlayHurtEffect();
            OnDamageDealt?.Invoke(p, enemy.ATK, false);
        }

        _impulseSource?.GenerateImpulse(_hitImpulse);
        yield return _waitMedium;
    }

    // ── BattleEnd ─────────────────────────────────────────────
    private IEnumerator BattleEndRoutine()
    {
        bool victory = CheckVictory();
        OnBattleEnded?.Invoke(victory);

        if (victory)
        {
            foreach (var e in _enemies)
                foreach (var p in _playerParty)
                    p.GainEXP(e.Data.EXPReward);

            foreach (var e in _enemies)
                foreach (var id in e.GetDrops())
                    GlobalDataManager.Instance?.AddItem(id);
        }

        yield return _waitMedium;
        SceneLoader.Instance.LoadScene(SceneName.Overworld);
    }

    // ── MP 관리 ───────────────────────────────────────────────
    private void AddMP(PlayerCharacter player, int amount)
    {
        if (!_mpMap.ContainsKey(player)) _mpMap[player] = 0;
        _mpMap[player] = Mathf.Clamp(_mpMap[player] + amount, 0, 100);
        OnMPChanged?.Invoke(player, _mpMap[player]);
    }

    public int GetMP(PlayerCharacter player)
        => _mpMap.TryGetValue(player, out int v) ? v : 0;

    // ── 유틸리티 ──────────────────────────────────────────────
    private bool CheckVictory()
    {
        foreach (var e in _enemies) if (e.IsAlive) return false;
        return true;
    }

    private bool CheckDefeat()
    {
        foreach (var p in _playerParty) if (p.IsAlive) return false;
        return true;
    }

    private int GetAlivePlayerIndex()
    {
        for (int i = 0; i < _playerParty.Count; i++)
            if (_playerParty[i].IsAlive) return i;
        return -1;
    }

    private EnemyCharacter GetCurrentEnemy()
    {
        // _currentActorIndex는 AdvanceTurn에서 이미 +1됨
        int idx = _currentActorIndex - 1;
        if (idx < 0 || idx >= _turnQueue.Count) return null;
        return _turnQueue[idx] as EnemyCharacter;
    }

    private static EnemyAttackType ResolveAttackType(EnemyCharacter enemy, EnemyAction action)
    {
        // EnemyData에 AttackType 필드가 추가되면 그걸 참조.
        // 현재는 action 기반으로 단순 분기.
        return action switch
        {
            EnemyAction.UseSkill      => EnemyAttackType.RangedAoE,
            EnemyAction.EnragedAttack => EnemyAttackType.AoEAll,
            _                         => EnemyAttackType.MeleeClose,
        };
    }

    private static int CalcDefenseDamage(int rawAtk, DefenseInput input, QTEManager.QTEGrade grade)
    {
        return grade switch
        {
            QTEManager.QTEGrade.Perfect => input == DefenseInput.Parry ? 0 : Mathf.RoundToInt(rawAtk * 0.05f),
            QTEManager.QTEGrade.Great   => Mathf.RoundToInt(rawAtk * 0.25f),
            QTEManager.QTEGrade.Good    => Mathf.RoundToInt(rawAtk * 0.55f),
            QTEManager.QTEGrade.Bad     => Mathf.RoundToInt(rawAtk * 0.80f),
            _                           => rawAtk, // Miss: 전체 데미지
        };
    }

    // ── 공개 접근자 ───────────────────────────────────────────
    public IReadOnlyList<PlayerCharacter> PlayerParty => _playerParty;
    public IReadOnlyList<EnemyCharacter>  Enemies     => _enemies;
}
