using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;
using Unity.Cinemachine;

/// <summary>
/// 전투 흐름을 총괄하는 싱글톤 매니저.
/// BattleState 기반 상태 머신으로 동작합니다.
/// 
/// Inspector 연결 목록:
/// - _playerParty, _enemies
/// - _playerDefaultPositions, _enemyDefaultPositions, _centerPosition
/// - _battleHUD, _battleMenu, _defenseQTEUI
/// - _impulseSource (CinemachineImpulseSource)
/// </summary>
public class BattleManager : MonoBehaviour
{
    // ── 싱글톤 ────────────────────────────────────────────────
    public static BattleManager Instance { get; private set; }

    // ── 전투 참가자 ───────────────────────────────────────────
    [Header("Battle Units")]
    [SerializeField] private List<PlayerCharacter> _playerParty;
    [SerializeField] private List<EnemyCharacter>  _enemies;

    // ── 포지션 ────────────────────────────────────────────────
    [Header("Positions")]
    [SerializeField] private Transform[] _playerDefaultPositions;
    [SerializeField] private Transform[] _enemyDefaultPositions;
    [SerializeField] private Transform   _centerPosition;

    // ── UI 참조 ───────────────────────────────────────────────
    [Header("UI")]
    [SerializeField] private BattleHUD      _battleHUD;
    [SerializeField] private BattleMenuUI   _battleMenu;
    [SerializeField] private DefenseQTEUI   _defenseQTEUI;

    // ── 카메라 연출 ───────────────────────────────────────────
    [Header("Camera")]
    [SerializeField] private CinemachineImpulseSource _impulseSource;
    [SerializeField] private float _hitImpulseForce  = 0.15f;  // 타격 쉐이크 강도 (약하게)
    [SerializeField] private float _missImpulseForce = 0.05f;  // 미스 쉐이크

    // ── 상태 ──────────────────────────────────────────────────
    public BattleState CurrentState { get; private set; } = BattleState.Idle;

    private int _currentPlayerIndex = 0;

    // 캐싱
    private WaitForSeconds _waitShort  = new WaitForSeconds(0.5f);
    private WaitForSeconds _waitMedium = new WaitForSeconds(1.0f);

    // ── 초기화 ────────────────────────────────────────────────
    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void Start()
    {
        // HUD 초기값 설정
        RefreshHUD();
        StartBattle();
    }

    // ── 전투 시작 ─────────────────────────────────────────────
    public void StartBattle()
    {
        ChangeState(BattleState.Intro);
    }

    // ── 상태 전환 ─────────────────────────────────────────────
    public void ChangeState(BattleState newState)
    {
        CurrentState = newState;
        switch (newState)
        {
            case BattleState.Intro:       StartCoroutine(IntroRoutine());      break;
            case BattleState.PlayerTurn:  StartCoroutine(PlayerTurnRoutine()); break;
            case BattleState.ActionPhase: /* QTE 처리 후 콜백으로 진행 */      break;
            case BattleState.EnemyTurn:   StartCoroutine(EnemyTurnRoutine());  break;
            case BattleState.Result:      StartCoroutine(ResultRoutine());     break;
        }
    }

    // ── Intro 연출 ────────────────────────────────────────────
    private IEnumerator IntroRoutine()
    {
        _battleHUD?.ShowImmediate();
        _battleHUD?.SetTurnLabel("전투 시작!");
        yield return _waitShort;
        ChangeState(BattleState.PlayerTurn);
    }

    // ── 플레이어 턴 ───────────────────────────────────────────
    private IEnumerator PlayerTurnRoutine()
    {
        foreach (var player in _playerParty)
            player.ProcessEffects();

        RefreshHUD();
        _battleHUD?.SetTurnLabel("플레이어 턴");

        yield return null; // 1프레임 대기 후 메뉴 표시
        _battleMenu?.Show();
    }

    /// <summary>BattleMenuUI 버튼 클릭 시 호출됩니다.</summary>
    public void OnPlayerActionSelected(PlayerMenuAction action, int targetIndex = 0)
    {
        switch (action)
        {
            case PlayerMenuAction.Attack: StartCoroutine(ExecutePlayerAttack(targetIndex)); break;
            case PlayerMenuAction.Skill:  StartCoroutine(ExecutePlayerAttack(targetIndex)); break; // TODO: 스킬 분기
            case PlayerMenuAction.Item:   StartCoroutine(PlayerTurnRoutine());              break; // TODO: 아이템 UI
            case PlayerMenuAction.Run:    TryRun();                                         break;
        }
    }

    // ── 플레이어 공격 ─────────────────────────────────────────
    private IEnumerator ExecutePlayerAttack(int targetIndex)
    {
        ChangeState(BattleState.ActionPhase);

        if (targetIndex >= _enemies.Count || !_enemies[targetIndex].IsAlive)
        {
            ChangeState(BattleState.EnemyTurn);
            yield break;
        }

        var player = _playerParty[_currentPlayerIndex];
        var target = _enemies[targetIndex];

        // 중앙으로 이동
        if (!target.Data.IsLargeEnemy)
        {
            yield return player.transform
                .DOMove(_centerPosition.position, 0.25f)
                .SetEase(Ease.OutQuad)
                .WaitForCompletion();
        }

        // 공격 연출 (PlayerController가 있으면 애니메이션 재생)
        var pc = player.GetComponent<PlayerController>();
        pc?.PlayBattleAnim(PlayerController.HashAttack);

        yield return new WaitForSeconds(0.1f);

        // 데미지 적용
        int damage = target.TakeDamage(player.ATK);
        Debug.Log($"[BattleManager] Player attacked for {damage} damage.");

        // 카메라 쉐이크
        _impulseSource?.GenerateImpulse(_hitImpulseForce);

        RefreshHUD();
        yield return new WaitForSeconds(0.15f);

        // 원위치 복귀
        yield return player.transform
            .DOMove(_playerDefaultPositions[_currentPlayerIndex].position, 0.25f)
            .SetEase(Ease.InQuad)
            .WaitForCompletion();

        if (CheckAllEnemiesDefeated())
        {
            ChangeState(BattleState.Result);
            yield break;
        }

        ChangeState(BattleState.EnemyTurn);
    }

    // ── 도망 ──────────────────────────────────────────────────
    private void TryRun()
    {
        if (Random.value < 0.5f)
        {
            Debug.Log("[BattleManager] Player escaped!");
            _battleHUD?.Hide();
            SceneLoader.Instance.LoadScene(SceneName.Overworld);
        }
        else
        {
            Debug.Log("[BattleManager] Failed to escape.");
            ChangeState(BattleState.EnemyTurn);
        }
    }

    // ── 적 턴 ─────────────────────────────────────────────────
    private IEnumerator EnemyTurnRoutine()
    {
        _battleHUD?.SetTurnLabel("적 턴");

        foreach (var enemy in _enemies)
        {
            if (!enemy.IsAlive) continue;

            enemy.ProcessEffects();
            var action = enemy.DecideAction();

            // 적 중앙 이동
            if (!enemy.Data.IsLargeEnemy)
            {
                yield return enemy.transform
                    .DOMove(_centerPosition.position, 0.25f)
                    .SetEase(Ease.OutQuad)
                    .WaitForCompletion();
            }

            // ── 방어 QTE ──────────────────────────────────────
            float attackDelay = 1.5f;
            bool qteResolved  = false;
            QTEManager.QTEGrade defenseGrade = QTEManager.QTEGrade.Miss;
            DefenseInput        defenseInput = DefenseInput.None;

            _defenseQTEUI?.ShowQTE(attackDelay);

            QTEManager.Instance?.StartDefenseQTE(attackDelay, (input, grade) =>
            {
                defenseInput  = input;
                defenseGrade  = grade;
                qteResolved   = true;
            });

            // QTE 완료 대기
            yield return new WaitUntil(() => qteResolved);

            // 결과 표시
            _defenseQTEUI?.ShowResult(defenseGrade, defenseInput);

            // 데미지 계산 (패링/회피 성공 시 감소)
            int rawDamage = enemy.ATK;
            int finalDamage;

            switch (defenseGrade)
            {
                case QTEManager.QTEGrade.Perfect:
                    // 패링 Perfect → 데미지 0 + 반격 가능 (TODO)
                    finalDamage = defenseInput == DefenseInput.Parry ? 0 : Mathf.RoundToInt(rawDamage * 0.1f);
                    _impulseSource?.GenerateImpulse(_missImpulseForce);
                    break;
                case QTEManager.QTEGrade.Great:
                    finalDamage = Mathf.RoundToInt(rawDamage * 0.3f);
                    _impulseSource?.GenerateImpulse(_missImpulseForce);
                    break;
                case QTEManager.QTEGrade.Good:
                    finalDamage = Mathf.RoundToInt(rawDamage * 0.6f);
                    _impulseSource?.GenerateImpulse(_hitImpulseForce * 0.5f);
                    break;
                default: // Bad / Miss
                    finalDamage = _playerParty[0].TakeDamage(rawDamage);
                    _impulseSource?.GenerateImpulse(_hitImpulseForce);
                    var pc = _playerParty[0].GetComponent<PlayerController>();
                    pc?.PlayBattleAnim(PlayerController.HashHurt);
                    goto skipDamage;
            }

            // 감소된 데미지 적용
            _playerParty[0].TakePureDamage(finalDamage);
            skipDamage:

            Debug.Log($"[BattleManager] Enemy attacked! Defense: {defenseInput}/{defenseGrade}, Damage: {finalDamage}");
            RefreshHUD();

            yield return _waitMedium;

            // 원위치 복귀
            if (!enemy.Data.IsLargeEnemy)
            {
                yield return enemy.transform
                    .DOMove(_enemyDefaultPositions[_enemies.IndexOf(enemy)].position, 0.25f)
                    .SetEase(Ease.InQuad)
                    .WaitForCompletion();
            }

            yield return _waitShort;
        }

        if (CheckAllPlayersDefeated())
        {
            ChangeState(BattleState.Result);
            yield break;
        }

        ChangeState(BattleState.PlayerTurn);
    }

    // ── 결과 처리 ─────────────────────────────────────────────
    private IEnumerator ResultRoutine()
    {
        bool victory = CheckAllEnemiesDefeated();
        _battleHUD?.SetTurnLabel(victory ? "승리!" : "패배...");
        Debug.Log($"[BattleManager] Battle Result: {(victory ? "Victory" : "Defeat")}");

        if (victory)
        {
            foreach (var enemy in _enemies)
                foreach (var player in _playerParty)
                    player.GainEXP(enemy.Data.EXPReward);

            // 드롭 아이템 처리
            foreach (var enemy in _enemies)
                foreach (var dropID in enemy.GetDrops())
                    GlobalDataManager.Instance?.AddItem(dropID);
        }

        yield return _waitMedium;
        _battleHUD?.Hide();
        yield return _waitShort;
        SceneLoader.Instance.LoadScene(SceneName.Overworld);
    }

    // ── HUD 갱신 ──────────────────────────────────────────────
    private void RefreshHUD()
    {
        if (_battleHUD == null) return;

        if (_playerParty.Count > 0)
        {
            var p = _playerParty[0];
            _battleHUD.SetPlayerHP(p.CurrentHP, p.MaxHP);
        }

        if (_enemies.Count > 0)
        {
            var e = _enemies[0];
            _battleHUD.SetEnemyHP(e.CurrentHP, e.MaxHP);
        }
    }

    // ── 유틸리티 ──────────────────────────────────────────────
    private bool CheckAllEnemiesDefeated()
    {
        foreach (var e in _enemies)
            if (e.IsAlive) return false;
        return true;
    }

    private bool CheckAllPlayersDefeated()
    {
        foreach (var p in _playerParty)
            if (p.IsAlive) return false;
        return true;
    }
}
