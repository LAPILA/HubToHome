using System.Collections.Generic;
using UnityEngine;
using Sirenix.OdinInspector;

/// <summary>
/// 대화를 통해 전투를 시작하는 NPC.
/// 실제 전투 적은 기존 Enemy_Base prefab + EnemyData 파이프라인을 그대로 사용합니다.
/// 전투 분기는 DialogueData의 ChoiceData 설정을 우선 사용하고,
/// 비어 있을 경우 이 컴포넌트의 기본 적 목록을 보조값으로 채워 넣습니다.
/// </summary>
public class DialogueBattleNPC : InteractableBase, IPreemptiveAttackTarget, IEncounterSource
{
    [BoxGroup("Dialogue")]
    [SerializeField] private DialogueData _dialogue;

    [BoxGroup("Battle Encounter")]
    [Tooltip("대화 선택에서 전투가 시작될 때 사용할 적 목록입니다. 여기서만 설정하세요.")]
    [SerializeField] private List<EnemyData> _fallbackEncounterEnemies = new List<EnemyData>();
    [BoxGroup("Battle Encounter")]
    [SerializeField] private AudioClip _fallbackBattleBgm;
    [BoxGroup("Battle Encounter")]
    [Tooltip("비워두면 BattleManager 기본 시나리오를 사용합니다. 이 NPC 전투만 별도 Scenario Source 흐름으로 실행할 때 지정합니다.")]
    [SerializeField] private BattleScenarioData _fallbackBattleScenarioData;
    [BoxGroup("Battle Encounter")]
    [SerializeField] private bool _useDedicatedBattleScene;
    [BoxGroup("Battle Encounter"), ShowIf(nameof(_useDedicatedBattleScene))]
    [SerializeField] private string _battleSceneName = "BattleScene";
    [BoxGroup("Battle Encounter"), ShowIf(nameof(_useDedicatedBattleScene))]
    [SerializeField] private float _battleSceneFadeDuration = 0.08f;

    [BoxGroup("Runtime Safety")]
    [SerializeField] private bool _disableSiblingOverworldEnemy = true;

    private bool _preemptiveEncounterInProgress;

    private void Reset()
    {
        _useRequiredFlagCondition = false;
    }

    private void Awake()
    {
        if (!_disableSiblingOverworldEnemy) return;

        OverworldEnemy overworldEnemy = GetComponent<OverworldEnemy>();
        if (overworldEnemy != null)
            overworldEnemy.enabled = false;
    }

    public override void Interact(PlayerController player)
    {
        if (_dialogue == null)
        {
            Debug.LogWarning($"[DialogueBattleNPC] DialogueData가 비어있습니다. Object={gameObject.name}", this);
            return;
        }

        var encounterContext = new DialogueEncounterContext
        {
            EncounterEnemies = new List<EnemyData>(_fallbackEncounterEnemies),
            OverrideBattleBGM = _fallbackBattleBgm,
            BattleScenarioData = _fallbackBattleScenarioData,
            UseDedicatedBattleScene = _useDedicatedBattleScene,
            BattleSceneName = _battleSceneName,
            BattleSceneFadeDuration = _battleSceneFadeDuration
        };

        DialogueManager.Instance?.StartDialogue(_dialogue, null, encounterContext);
    }

    public bool CanStartPreemptiveAttack(PlayerController player)
    {
        return isActiveAndEnabled
            && player != null
            && !_preemptiveEncounterInProgress
            && _fallbackEncounterEnemies != null
            && _fallbackEncounterEnemies.Count > 0;
    }

    public bool TryStartPreemptiveAttack(PlayerController player)
    {
        if (!CanStartPreemptiveAttack(player)) return false;

        List<EnemyData> enemies = new List<EnemyData>();
        for (int i = 0; i < _fallbackEncounterEnemies.Count; i++)
        {
            if (_fallbackEncounterEnemies[i] != null)
                enemies.Add(_fallbackEncounterEnemies[i]);
        }

        if (enemies.Count == 0)
        {
            Debug.LogWarning($"[DialogueBattleNPC] 선공 전투 적 목록이 비어있습니다. Object={gameObject.name}", this);
            return false;
        }

        _preemptiveEncounterInProgress = true;
        bool started = BattleEncounterService.StartEncounter(
            player,
            enemies,
            _fallbackBattleBgm,
            _useDedicatedBattleScene,
            _battleSceneName,
            _battleSceneFadeDuration,
            ResolveEncounterId(enemies),
            true,
            this,
            _fallbackBattleScenarioData,
            true);

        if (!started)
            _preemptiveEncounterInProgress = false;

        return started;
    }

    public void OnEncounterResolved(bool victory, PlayerController player)
    {
        _preemptiveEncounterInProgress = false;
    }

    private string ResolveEncounterId(List<EnemyData> enemies)
    {
        if (enemies != null && enemies.Count > 0 && enemies[0] != null && !string.IsNullOrWhiteSpace(enemies[0].EnemyId))
            return enemies[0].EnemyId;

        return gameObject.name;
    }
}
