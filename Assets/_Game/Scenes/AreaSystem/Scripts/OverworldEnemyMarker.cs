using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class OverworldEnemyMarker : AreaMarkerBase, IEncounterSource
{
    [Header("Enemy Encounter")]
    [SerializeField] private string enemyId;
    [SerializeField, Min(1)] private int enemyLevel = 1;
    [SerializeField] private string battleEncounterId;
    [SerializeField] private bool canInstantKillLater = true;
    [SerializeField, Tooltip("실제 전투 진입에 사용할 EnemyData입니다. 비어 있으면 Debug.Log만 출력합니다.")]
    private EnemyData enemyData;
    [SerializeField] private List<EnemyData> additionalEnemies = new List<EnemyData>();
    [SerializeField] private AudioClip battleBgmOverride;
    [SerializeField] private BattleScenarioData battleScenarioData;
    [SerializeField] private bool useDedicatedBattleScene = true;
    [SerializeField] private string battleSceneName = "BattleScene";
    [SerializeField, Min(0f)] private float battleFadeDuration = 0.08f;

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
            bool started = BattleEncounterService.StartEncounter(
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
}