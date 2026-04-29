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
                
                p.GetComponent<PlayerController>()?.SetBattleMode(true);
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
            StartCoroutine(ExecuteSkill(_pendingActor, targetIndex));
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
        var pm     = PositionManager.Instance;
        var actorCtrl = actor.GetComponent<PlayerController>();

        // 1. 돌진
        Transform frontPivot = target.transform.Find("Pivots/Front");
        Vector3 attackPos = (frontPivot != null) ? frontPivot.position : target.transform.position + new Vector3(-1.2f, 0, 0);

        actorCtrl?.PlayBattleAnim(PlayerController.HashBattleMove);
        yield return actor.transform.DOMove(attackPos, 0.25f).SetEase(Ease.Linear).WaitForCompletion();

        // 2. 타격
        actorCtrl?.PlayBattleAnim(PlayerController.HashAttack);
        yield return new WaitForSeconds(0.15f);

        int dmg = target.TakeDamage(actor.ATK);
        _impulseSource?.GenerateImpulse(_hitImpulse);
        OnDamageDealt?.Invoke(target, dmg, false); 

        yield return new WaitForSeconds(0.3f);

        // 3. 복귀
        int idx = _playerParty.IndexOf(actor);
        actorCtrl?.PlayBattleAnim(PlayerController.HashBattleMove);
        yield return actor.transform.DOMove(pm.GetPlayerDefaultPos(idx), 0.25f).SetEase(Ease.OutQuad).WaitForCompletion();
        actorCtrl?.PlayBattleAnim(PlayerController.HashBattleIdle);

        if (CheckVictory()) { ChangeState(BattleState.BattleEnd); yield break; }
        AdvanceTurn();
    }

    // ── 스킬 (QTE 연동) ───────────────────────────────────────
    // ── 스킬 (플레이어 공격 시 위력 강화 QTE) ───────────────────
    private IEnumerator ExecuteSkill(PlayerCharacter actor, int targetIndex)
    {
        ChangeState(BattleState.ActionExecute);

        if (targetIndex >= _enemies.Count || !_enemies[targetIndex].IsAlive)
        {
            AdvanceTurn(); yield break;
        }

        var target = _enemies[targetIndex];
        var pm     = PositionManager.Instance;
        var actorCtrl = actor.GetComponent<PlayerController>();

        // 🚨 1. 스킬 QTE 시작 (오직 여기서만 게이지 바 UI가 나타남)
        bool qteFinished = false;
        QTEManager.QTEGrade resultGrade = QTEManager.QTEGrade.Miss;

        QTEManager.Instance.StartSkillQTE(1.0f); 
        Action<QTEManager.QTEGrade> onComplete = null;
        onComplete = (grade) => {
            resultGrade = grade;
            qteFinished = true;
            QTEManager.Instance.OnSkillQTECompleted -= onComplete;
        };
        QTEManager.Instance.OnSkillQTECompleted += onComplete;

        yield return new WaitUntil(() => qteFinished);

        // 🚨 2. 등급별 데미지 배율 결정
        float mult = resultGrade switch
        {
            QTEManager.QTEGrade.Perfect => 2.0f,
            QTEManager.QTEGrade.Great   => 1.5f,
            QTEManager.QTEGrade.Good    => 1.2f,
            QTEManager.QTEGrade.Bad     => 0.8f,
            _                           => 0.5f,
        };

        // 🚨 3. 이동 및 공격 연출 (ExecuteAttack과 동일 템포)
        Transform frontPivot = target.transform.Find("Pivots/Front");
        Vector3 attackPos = (frontPivot != null) ? frontPivot.position : target.transform.position + new Vector3(-1.2f, 0, 0);

        actorCtrl?.PlayBattleAnim(PlayerController.HashBattleMove);
        yield return actor.transform.DOMove(attackPos, 0.25f).SetEase(Ease.Linear).WaitForCompletion();

        actorCtrl?.PlayBattleAnim(PlayerController.HashAttack);
        yield return new WaitForSeconds(0.15f);

        // 결과 적용
        int dmg = target.TakeDamage(Mathf.RoundToInt(actor.ATK * mult));
        bool isCrit = resultGrade == QTEManager.QTEGrade.Perfect;
        _impulseSource?.GenerateImpulse(isCrit ? _hitImpulse * 1.5f : _hitImpulse);
        OnDamageDealt?.Invoke(target, dmg, isCrit);

        yield return new WaitForSeconds(0.3f);

        // 복귀
        int idx = _playerParty.IndexOf(actor);
        actorCtrl?.PlayBattleAnim(PlayerController.HashBattleMove);
        yield return actor.transform.DOMove(pm.GetPlayerDefaultPos(idx), 0.25f).SetEase(Ease.OutQuad).WaitForCompletion();
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
        // 현재 행동 적 찾기 (AdvanceTurn에서 이미 인덱스 증가됨)
        var enemy = GetCurrentEnemy();
        if (enemy == null) { AdvanceTurn(); yield break; }

        var action     = enemy.DecideAction();
        var attackType = ResolveAttackType(enemy, action);

        OnEnemyActionStarted?.Invoke(enemy, attackType);

        var pm = PositionManager.Instance;

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

        if (CheckDefeat()) { ChangeState(BattleState.BattleEnd); yield break; }
        AdvanceTurn();
    }

    // ── 적 근거리 단일 공격 ───────────────────────────────────
    // ── 적 근거리 단일 공격 및 실시간 방어 ──────────────────────
    private IEnumerator EnemyMeleeRoutine(EnemyCharacter enemy, PositionManager pm)
    {
        int targetIdx = GetAlivePlayerIndex();
        if (targetIdx < 0) yield break;

        var target = _playerParty[targetIdx];
        var targetCtrl = target.GetComponent<PlayerController>();

        // 1. 적 다가옴 (BattleMove)
        enemy.PlayBattleAnim(EnemyCharacter.HashBattleMove);
        Transform frontPivot = target.transform.Find("Pivots/Front");
        Vector3 attackPos = (frontPivot != null) ? frontPivot.position : target.transform.position + new Vector3(1.2f, 0, 0);
        yield return enemy.transform.DOMove(attackPos, 0.25f).SetEase(Ease.Linear).WaitForCompletion();

        // 2. 적 공격 시작 (선딜레이 및 입력 감지 루프)
        float defenseWindow = 0.8f; // 적 공격 전체 판정 시간
        float elapsed = 0f;
        bool defensed = false;

        enemy.PlayBattleAnim(EnemyCharacter.HashAttack); // 적 공격 애니메이션 1회 실행

        while (elapsed < defenseWindow)
        {
            elapsed += Time.deltaTime;

            // Z(패링): 타이밍이 맞아야 함
            if (Keyboard.current.zKey.wasPressedThisFrame)
            {
                targetCtrl.ExecuteParry(); // 실패해도 애니메이션은 실행됨
                if (elapsed >= 0.3f && elapsed <= 0.6f) // 패링 유효 프레임
                {
                    targetCtrl.PlayParryEffect();
                    defensed = true;
                    break;
                }
            }
            // C(회피), Space(점프): 누르는 즉시 연출 및 무적 판정
            if (Keyboard.current.cKey.wasPressedThisFrame)
            {
                targetCtrl.ExecuteDodge();
                defensed = true;
                break;
            }
            if (Keyboard.current.spaceKey.wasPressedThisFrame)
            {
                targetCtrl.ExecuteJump();
                defensed = true;
                break;
            }
            yield return null;
        }

        // 3. 결과 적용
        if (!defensed)
        {
            // 방어 실패 시에만 데미지
            target.TakePureDamage(enemy.ATK);
            targetCtrl.PlayHurtEffect();
            _impulseSource?.GenerateImpulse(_hitImpulse);
            OnDamageDealt?.Invoke(target, enemy.ATK, false);
        }
        else
        {
            // 성공 시 임펄스만 살짝 (타격감)
            _impulseSource?.GenerateImpulse(_missImpulse);
        }

        yield return new WaitForSeconds(0.4f);

        // 4. 복귀
        enemy.PlayBattleAnim(EnemyCharacter.HashBattleMove);
        yield return enemy.transform.DOMove(pm.GetEnemyDefaultPos(_enemies.IndexOf(enemy)), 0.3f).SetEase(Ease.InQuad).WaitForCompletion();
        enemy.PlayBattleAnim(EnemyCharacter.HashBattleIdle);
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
