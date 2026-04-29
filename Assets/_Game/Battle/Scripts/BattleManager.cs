using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;
using Unity.Cinemachine;
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
        List<CharacterBase> allParticipants = new List<CharacterBase>();
        foreach (var p in _playerParty) if (p != null && p.IsAlive) allParticipants.Add(p);
        foreach (var e in _enemies)     if (e != null && e.IsAlive) allParticipants.Add(e);
        allParticipants.Sort((a, b) => b.SPD.CompareTo(a.SPD));
        foreach (var chara in allParticipants) _turnQueue.Add(chara);
        
        _currentActorIndex = 0;
        int displayCount = Mathf.Min(_turnQueue.Count, 8);
        var display = _turnQueue.GetRange(0, displayCount);
        
        OnTurnQueueUpdated?.Invoke(display);

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

    // ── 플레이어 공격 ─────────────────────────────────────────
    private IEnumerator ExecuteAttack(PlayerCharacter actor, int targetIndex)
    {
        ChangeState(BattleState.ActionExecute);

        if (targetIndex >= _enemies.Count || !_enemies[targetIndex].IsAlive)
        {
            AdvanceTurn(); yield break;
        }

        var target = _enemies[targetIndex];
        var pm     = PositionManager.Instance;

        // 근거리: 중앙으로 이동
        if (!target.Data.IsLargeEnemy)
        {
            yield return actor.transform
                .DOMove(pm.GetCenterPos(), 0.25f)
                .SetEase(Ease.OutQuad)
                .WaitForCompletion();
        }

        // 애니메이션
        actor.GetComponent<PlayerController>()?.PlayBattleAnim(PlayerController.HashAttack);
        yield return new WaitForSeconds(0.1f);

        // 데미지
        int dmg = target.TakeDamage(actor.ATK);
        _impulseSource?.GenerateImpulse(_hitImpulse);
        OnDamageDealt?.Invoke(target, dmg, false);

        yield return new WaitForSeconds(0.15f);

        // 복귀
        int idx = _playerParty.IndexOf(actor);
        yield return actor.transform
            .DOMove(pm.GetPlayerDefaultPos(idx), 0.25f)
            .SetEase(Ease.InQuad)
            .WaitForCompletion();

        if (CheckVictory()) { ChangeState(BattleState.BattleEnd); yield break; }
        AdvanceTurn();
    }

    // ── 스킬 (QTE 연동) ───────────────────────────────────────
    private IEnumerator ExecuteSkill(PlayerCharacter actor, int targetIndex)
    {
        ChangeState(BattleState.ActionExecute);

        if (targetIndex >= _enemies.Count || !_enemies[targetIndex].IsAlive)
        {
            AdvanceTurn(); yield break;
        }

        var target = _enemies[targetIndex];

        // 스킬 QTE 시작 (UI는 OnStateChanged 이벤트로 표시)
        bool qteResolved = false;
        QTEManager.QTEGrade skillGrade = QTEManager.QTEGrade.Miss;

        QTEManager.Instance.OnSkillQTECompleted += OnSkillResult;
        QTEManager.Instance.StartSkillQTE();

        yield return new WaitUntil(() => qteResolved);

        // 등급별 데미지 배율
        float mult = skillGrade switch
        {
            QTEManager.QTEGrade.Perfect => 2.0f,
            QTEManager.QTEGrade.Great   => 1.5f,
            QTEManager.QTEGrade.Good    => 1.2f,
            QTEManager.QTEGrade.Bad     => 0.8f,
            _                           => 0.5f,
        };

        int dmg = target.TakeDamage(Mathf.RoundToInt(actor.ATK * mult));
        bool isCrit = skillGrade == QTEManager.QTEGrade.Perfect;
        _impulseSource?.GenerateImpulse(isCrit ? _hitImpulse * 1.5f : _hitImpulse);
        OnDamageDealt?.Invoke(target, dmg, isCrit);

        yield return _waitShort;

        if (CheckVictory()) { ChangeState(BattleState.BattleEnd); yield break; }
        AdvanceTurn();

        void OnSkillResult(QTEManager.QTEGrade g)
        {
            skillGrade  = g;
            qteResolved = true;
            QTEManager.Instance.OnSkillQTECompleted -= OnSkillResult;
        }
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
    private IEnumerator EnemyMeleeRoutine(EnemyCharacter enemy, PositionManager pm)
    {
        int targetIdx = GetAlivePlayerIndex();
        if (targetIdx < 0) yield break;

        var target = _playerParty[targetIdx];

        // 적이 아군 앞으로 이동
        if (!enemy.Data.IsLargeEnemy)
        {
            yield return enemy.transform
                .DOMove(pm.GetEnemyAttackPos(targetIdx), 0.25f)
                .SetEase(Ease.OutQuad)
                .WaitForCompletion();
        }

        // 적 공격 애니메이션
        enemy.PlayBattleAnim(Animator.StringToHash("Attack"));
        yield return new WaitForSeconds(0.1f);

        // 방어 QTE
        float attackDelay = 1.5f;
        bool  resolved    = false;
        var   defInput    = DefenseInput.None;
        var   defGrade    = QTEManager.QTEGrade.Miss;

        QTEManager.Instance.StartDefenseQTE(attackDelay, enemy.Data.QTEDifficultyMultiplier,
            (inp, grd) => { defInput = inp; defGrade = grd; resolved = true; });

        yield return new WaitUntil(() => resolved);

        // 결과 UI 표시
        BattleUIController.Instance?.ShowDefenseResult(defGrade, defInput);

        // 데미지 계산
        int finalDmg = CalcDefenseDamage(enemy.ATK, defInput, defGrade);

        // MP 회복
        if (defInput == DefenseInput.Parry && defGrade == QTEManager.QTEGrade.Perfect)
            AddMP(target, _mpOnParryPerfect);
        else if (defGrade >= QTEManager.QTEGrade.Good)
            AddMP(target, _mpOnDefenseSuccess);

        // 데미지 적용
        if (finalDmg > 0)
        {
            target.TakePureDamage(finalDmg);
            _impulseSource?.GenerateImpulse(_hitImpulse);
            target.GetComponent<PlayerController>()?.PlayBattleAnim(PlayerController.HashHurt);
        }
        else
        {
            _impulseSource?.GenerateImpulse(_missImpulse);
            target.GetComponent<PlayerController>()?.PlayBattleAnim(PlayerController.HashParry);
        }

        OnDamageDealt?.Invoke(target, finalDmg, false);

        yield return _waitMedium;

        // 복귀
        if (!enemy.Data.IsLargeEnemy)
        {
            int eIdx = _enemies.IndexOf(enemy);
            yield return enemy.transform
                .DOMove(pm.GetEnemyDefaultPos(eIdx), 0.25f)
                .SetEase(Ease.InQuad)
                .WaitForCompletion();
        }

        yield return _waitShort;
    }

    // ── 적 AoE 공격 ───────────────────────────────────────────
    private IEnumerator EnemyAoERoutine(EnemyCharacter enemy, EnemyAttackType type)
    {
        float attackDelay = 1.5f;
        int   aliveCount  = 0;
        foreach (var p in _playerParty) if (p.IsAlive) aliveCount++;

        // 전원 동시 QTE
        bool[]        resolved  = new bool[aliveCount];
        DefenseInput[] inputs   = new DefenseInput[aliveCount];
        QTEManager.QTEGrade[] grades = new QTEManager.QTEGrade[aliveCount];

        int i = 0;
        foreach (var p in _playerParty)
        {
            if (!p.IsAlive) continue;
            int captured = i++;
            QTEManager.Instance.StartDefenseQTE(attackDelay, enemy.Data.QTEDifficultyMultiplier,
                (inp, grd) => { inputs[captured] = inp; grades[captured] = grd; resolved[captured] = true; });
        }

        yield return new WaitUntil(() => System.Array.TrueForAll(resolved, r => r));

        // 각 플레이어에 데미지 적용
        int pi = 0;
        foreach (var p in _playerParty)
        {
            if (!p.IsAlive) continue;
            int dmg = CalcDefenseDamage(enemy.ATK, inputs[pi], grades[pi]);
            if (dmg > 0)
            {
                p.TakePureDamage(dmg);
                OnDamageDealt?.Invoke(p, dmg, false);
            }
            if (inputs[pi] == DefenseInput.Parry && grades[pi] == QTEManager.QTEGrade.Perfect)
                AddMP(p, _mpOnParryPerfect);
            else if (grades[pi] >= QTEManager.QTEGrade.Good)
                AddMP(p, _mpOnDefenseSuccess);
            pi++;
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
