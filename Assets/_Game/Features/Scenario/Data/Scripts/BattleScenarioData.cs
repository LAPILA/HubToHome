using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "BattleScenario", menuName = "HubToHome/Scenario/Battle Scenario")]
public sealed class BattleScenarioData : ScriptableObject
{
    [Tooltip("Scenario Source와 저장/메모리에서 사용하는 안정적인 scenario ID입니다.")]
    public string ScenarioId = string.Empty;

    [Tooltip("사람이 읽는 한국어 제목입니다.")]
    public string TitleKo = string.Empty;

    [Tooltip("현재 계획상 battle 또는 overworld 같은 Primary Mode ID입니다.")]
    public string PrimaryMode = "battle";

    [Tooltip("처음 시작할 Game Module ID입니다. 예: turn_qte")]
    public string OpeningModule = "turn_qte";

    [Tooltip("Encounter Memory에 사용할 저장 키입니다.")]
    public string MemoryKey = string.Empty;

    public ScenarioSourceMetadata Source = new ScenarioSourceMetadata();

    [Tooltip("참가 아군 ID 목록입니다.")]
    public List<string> PartyIds = new List<string>();

    [Tooltip("참가 적 ID 목록입니다.")]
    public List<string> EnemyIds = new List<string>();

    [Tooltip("전투 중 발생하는 when -> do 규칙입니다.")]
    public List<BattleEventRuleData> Rules = new List<BattleEventRuleData>();

    [Tooltip("이 전투 시나리오가 참조하는 Action Sequence 목록입니다.")]
    public List<ActionSequenceAsset> Sequences = new List<ActionSequenceAsset>();
}
