using System;
using UnityEngine;

[Serializable]
public sealed class ScenarioDialogueReferenceData
{
    [Tooltip("Scenario Source의 dialogue.wait 액션이 참조하는 안정적인 Dialogue ID입니다.")]
    public string DialogueId = string.Empty;

    [Tooltip("해당 Dialogue ID로 실행할 DialogueData입니다.")]
    public DialogueData Dialogue;
}
