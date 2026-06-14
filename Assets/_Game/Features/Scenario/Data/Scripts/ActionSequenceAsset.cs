using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "ActionSequence", menuName = "HubToHome/Scenario/Action Sequence")]
public sealed class ActionSequenceAsset : ScriptableObject
{
    [Tooltip("Scenario Source에서 사용하는 안정적인 sequence ID입니다.")]
    public string SequenceId = string.Empty;

    [Tooltip("에디터에 표시할 한국어 이름입니다.")]
    public string DisplayNameKo = string.Empty;

    [Tooltip("이 런타임 에셋을 만든 Scenario Source 정보입니다.")]
    public ScenarioSourceMetadata Source = new ScenarioSourceMetadata();

    [Tooltip("순서대로 실행할 Action 목록입니다.")]
    public List<ScenarioActionData> Actions = new List<ScenarioActionData>();
}
