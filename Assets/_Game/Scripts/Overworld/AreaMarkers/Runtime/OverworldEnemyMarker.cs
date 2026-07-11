using System.Collections;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class OverworldEnemyMarker : AreaMarkerBase, IEncounterSource
{
    [TitleGroup("Enemy Encounter/기본")]
    [SerializeField, LabelText("적 ID")] private string enemyId;
    [TitleGroup("Enemy Encounter/기본")]
    [SerializeField, Min(1), LabelText("적 레벨")] private int enemyLevel = 1;
    [TitleGroup("Enemy Encounter/기본")]
    [SerializeField, LabelText("전투 Encounter ID")] private string battleEncounterId;
    [TitleGroup("Enemy Encounter/기본")]
    [SerializeField, LabelText("나중에 즉사 가능")]
    private bool canInstantKillLater = true;

    [TitleGroup("Enemy Encounter/적 구성")]
    [SerializeField, Tooltip("실제 전투 진입에 사용할 EnemyData입니다. 비어 있으면 Debug.Log만 출력합니다."), LabelText("대표 EnemyData")]
    private EnemyData enemyData;
    [TitleGroup("Enemy Encounter/적 구성")]
    [SerializeField, LabelText("추가 EnemyData 목록")]
    [ListDrawerSettings(ShowFoldout = true, DefaultExpandedState = true)]
    private List<EnemyData> additionalEnemies = new List<EnemyData>();

    [TitleGroup("Enemy Encounter/전투 진입")]
    [SerializeField, LabelText("배틀 BGM Override")] private AudioClip battleBgmOverride;
    [TitleGroup("Enemy Encounter/전투 진입")]
    [SerializeField, LabelText("BattleScenarioData")] private BattleScenarioData battleScenarioData;
    [TitleGroup("Enemy Encounter/전투 진입")]
    [SerializeField, LabelText("전용 배틀 씬 사용")] private bool useDedicatedBattleScene = true;
    [TitleGroup("Enemy Encounter/전투 진입")]
    [SerializeField, ShowIf(nameof(useDedicatedBattleScene)), LabelText("배틀 씬 이름")] private string battleSceneName = "BattleScene";
    [TitleGroup("Enemy Encounter/전투 진입")]
    [SerializeField, Min(0f), ShowIf(nameof(useDedicatedBattleScene)), LabelText("배틀 씬 페이드 시간")] private float battleFadeDuration = 0.08f;

    private bool _battleStarting;

    public bool CanInstantKillLater => canInstantKillLater;

    protected override void Reset()
    {
        markerType = AreaMarkerType.Enemy;
        gizmoColor = AreaMarkerDefaults.GetColor(markerType);
        interactionRange = 1.5f;
        base.Reset();
        Collider2D c = GetComponent<Collider2D>();
        if (c != null) c.isTrigger = true;
    }

    protected override void EnsureDefaults()
    {
        base.EnsureDefaults();
        if (string.IsNullOrWhiteSpace(enemyId)) enemyId = markerId;
        if (string.IsNullOrWhiteSpace(battleEncounterId)) battleEncounterId = enemyId;
    }

    public override void Interact(PlayerController player)
    {
        StartBattle(false, player);
    }

    public void StartBattle(bool playerAdvantage) => StartBattle(playerAdvantage, FindFirstObjectByType<PlayerController>());

    public void StartBattle(bool playerAdvantage, PlayerController player)
    {
        if (_battleStarting || !CanInteract(player)) return;
        StartCoroutine(CoStartBattle(playerAdvantage, player));
    }

    private IEnumerator CoStartBattle(bool playerAdvantage, PlayerController player)
    {
        _battleStarting = true;
        yield return null;

        List<EnemyData> enemies = ResolveEnemies();
        if (player != null && enemies.Count > 0)
        {
            bool started = AreaMarkerRuntimeService.TryStartEncounter(
                this,
                player,
                enemies,
                battleBgmOverride,
                useDedicatedBattleScene,
                battleSceneName,
                battleFadeDuration,
                string.IsNullOrWhiteSpace(battleEncounterId) ? enemyId : battleEncounterId,
                isOneShot,
                this,
                battleScenarioData,
                playerAdvantage);

            if (started)
            {
                Debug.Log($"[OverworldEnemyMarker] 전투 진입: enemy={enemyId}, level={enemyLevel}, preemptive={playerAdvantage}", this);
                yield break;
            }
        }

        Debug.Log($"[OverworldEnemyMarker] 전투 진입 요청(Debug): enemy={enemyId}, level={enemyLevel}, encounter={battleEncounterId}, preemptive={playerAdvantage}", this);
        _battleStarting = false;
    }

    private List<EnemyData> ResolveEnemies()
    {
        List<EnemyData> enemies = new List<EnemyData>();
        if (enemyData != null) enemies.Add(enemyData);
        if (additionalEnemies != null)
        {
            for (int i = 0; i < additionalEnemies.Count; i++)
                if (additionalEnemies[i] != null) enemies.Add(additionalEnemies[i]);
        }
        return enemies;
    }

    public void OnEncounterResolved(bool victory, PlayerController player)
    {
        _battleStarting = false;
        if (victory && isOneShot) CompleteMarker();
    }

    public override void CollectValidationIssues(List<string> issues)
    {
        base.CollectValidationIssues(issues);
        if (enemyData == null)
            issues.Add("EnemyData 참조가 없습니다.");

        if (useDedicatedBattleScene && string.IsNullOrWhiteSpace(battleSceneName))
            issues.Add("전용 배틀 씬 사용 시 battleSceneName이 필요합니다.");
    }
}