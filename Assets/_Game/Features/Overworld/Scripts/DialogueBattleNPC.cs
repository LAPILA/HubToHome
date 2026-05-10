using System.Collections.Generic;
using UnityEngine;
using Sirenix.OdinInspector;

/// <summary>
/// 대화를 통해 전투를 시작하는 NPC.
/// 실제 전투 적은 기존 Enemy_Base prefab + EnemyData 파이프라인을 그대로 사용합니다.
/// 전투 분기는 DialogueData의 ChoiceData 설정을 우선 사용하고,
/// 비어 있을 경우 이 컴포넌트의 기본 적 목록을 보조값으로 채워 넣습니다.
/// </summary>
public class DialogueBattleNPC : InteractableBase
{
    [BoxGroup("Dialogue")]
    [SerializeField] private DialogueData _dialogue;

    [BoxGroup("Battle Encounter")]
    [Tooltip("대화 선택에서 전투가 시작될 때 사용할 적 목록입니다. 여기서만 설정하세요.")]
    [SerializeField] private List<EnemyData> _fallbackEncounterEnemies = new List<EnemyData>();
    [BoxGroup("Battle Encounter")]
    [SerializeField] private AudioClip _fallbackBattleBgm;
    [BoxGroup("Battle Encounter")]
    [SerializeField] private bool _useDedicatedBattleScene;
    [BoxGroup("Battle Encounter"), ShowIf(nameof(_useDedicatedBattleScene))]
    [SerializeField] private string _battleSceneName = "BattleScene";
    [BoxGroup("Battle Encounter"), ShowIf(nameof(_useDedicatedBattleScene))]
    [SerializeField] private float _battleSceneFadeDuration = 0.08f;

    [BoxGroup("Runtime Safety")]
    [SerializeField] private bool _disableSiblingOverworldEnemy = true;

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
            UseDedicatedBattleScene = _useDedicatedBattleScene,
            BattleSceneName = _battleSceneName,
            BattleSceneFadeDuration = _battleSceneFadeDuration
        };

        DialogueManager.Instance?.StartDialogue(_dialogue, null, encounterContext);
    }
}