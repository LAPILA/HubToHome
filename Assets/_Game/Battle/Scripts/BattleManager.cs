using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;

/// <summary>
/// 전투 흐름을 총괄하는 싱글톤 매니저.
/// BattleState 기반 상태 머신으로 동작합니다.
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

    // ── 상태 ──────────────────────────────────────────────────
    public BattleState CurrentState { get; private set; } = BattleState.Idle;

    // 현재 행동 중인 플레이어/적 인덱스
    private int _currentPlayerIndex = 0;
    private int _currentEnemyIndex  = 0;

    // 캐싱
    private WaitForSeconds _waitShort = new WaitForSeconds(0.5f);

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void Start()
    {
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
            case BattleState.ActionPhase: /* QTEManager가 처리 후 콜백 */      break;
            case BattleState.EnemyTurn:   StartCoroutine(EnemyTurnRoutine());  break;
            case BattleState.Result:      StartCoroutine(ResultRoutine());     break;
        }
    }

    // ── Intro 연출 ────────────────────────────────────────────
    private IEnumerator IntroRoutine()
    {
        // TODO: 전투 진입 연출 (카메라 줌, 적 등장 등)
        yield return _waitShort;
        ChangeState(BattleState.PlayerTurn);
    }

    // ── 플레이어 턴 ───────────────────────────────────────────
    private IEnumerator PlayerTurnRoutine()
    {
        // 상태 이상 틱 처리
        foreach (var player in _playerParty)
            player.ProcessEffects();

        // Speed Gap: 추가 행동권 체크
        // TODO: BattleUI에 메뉴 표시 요청
        Debug.Log("[BattleManager] Player Turn - Waiting for menu input.");
        yield return null;
    }

    /// <summary>플레이어가 메뉴에서 행동을 선택했을 때 BattleUI가 호출합니다.</summary>
    public void OnPlayerActionSelected(PlayerMenuAction action, int targetIndex = 0)
    {
        switch (action)
        {
            case PlayerMenuAction.Attack:
                StartCoroutine(ExecutePlayerAttack(targetIndex));
                break;
            case PlayerMenuAction.Skill:
                // TODO: 스킬 선택 UI → QTEManager 호출
                break;
            case PlayerMenuAction.Item:
                // TODO: 아이템 선택 UI
                break;
            case PlayerMenuAction.Run:
                TryRun();
                break;
        }
    }

    // ── 플레이어 기본 공격 ────────────────────────────────────
    private IEnumerator ExecutePlayerAttack(int targetIndex)
    {
        ChangeState(BattleState.ActionPhase);

        var player = _playerParty[_currentPlayerIndex];
        var target = _enemies[targetIndex];

        // 캐릭터를 중앙으로 이동 (DOTween)
        if (!target.Data.IsLargeEnemy)
        {
            yield return player.transform
                .DOMove(_centerPosition.position, 0.3f)
                .SetEase(Ease.OutQuad)
                .WaitForCompletion();
        }

        // 데미지 적용
        int damage = target.TakeDamage(player.ATK);
        Debug.Log($"[BattleManager] Player attacked for {damage} damage.");

        // TODO: 타격 이펙트 (ObjectPool), 카메라 쉐이크

        // 원위치 복귀
        yield return player.transform
            .DOMove(_playerDefaultPositions[_currentPlayerIndex].position, 0.3f)
            .SetEase(Ease.InQuad)
            .WaitForCompletion();

        // 승리 체크
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
        // 보스전 등 강제 전투는 Run 비활성화 처리 필요
        float runChance = 0.5f;
        if (Random.value < runChance)
        {
            Debug.Log("[BattleManager] Player escaped!");
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
        foreach (var enemy in _enemies)
        {
            if (!enemy.IsAlive) continue;

            enemy.ProcessEffects();
            var action = enemy.DecideAction();

            // 적이 중앙으로 이동 (대형 적 제외)
            if (!enemy.Data.IsLargeEnemy)
            {
                yield return enemy.transform
                    .DOMove(_centerPosition.position, 0.3f)
                    .SetEase(Ease.OutQuad)
                    .WaitForCompletion();
            }

            // TODO: 방어 QTE 활성화 → DefenseQTEManager 호출
            Debug.Log($"[BattleManager] Enemy {enemy.Data.EnemyName} attacks!");

            // 임시: QTE 없이 직접 데미지
            var target = _playerParty[0];
            int damage = target.TakeDamage(enemy.ATK);
            Debug.Log($"[BattleManager] Player took {damage} damage.");

            // 원위치 복귀
            if (!enemy.Data.IsLargeEnemy)
            {
                yield return enemy.transform
                    .DOMove(_enemyDefaultPositions[_enemies.IndexOf(enemy)].position, 0.3f)
                    .SetEase(Ease.InQuad)
                    .WaitForCompletion();
            }

            yield return _waitShort;
        }

        // 패배 체크
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
        Debug.Log($"[BattleManager] Battle Result: {(victory ? "Victory" : "Defeat")}");

        if (victory)
        {
            // EXP / 드롭 처리
            foreach (var enemy in _enemies)
            {
                foreach (var player in _playerParty)
                    player.GainEXP(enemy.Data.EXPReward);
            }
            // Auto Save
            var saveData = GlobalDataManager.Instance.ToSaveData();
            SaveManager.Save(saveData, SaveManager.AutoSlotIndex);
        }

        yield return _waitShort;
        SceneLoader.Instance.LoadScene(SceneName.Overworld);
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
