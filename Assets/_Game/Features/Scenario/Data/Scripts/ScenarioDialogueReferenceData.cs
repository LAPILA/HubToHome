using System;
using UnityEngine;

[Serializable]
public sealed class ScenarioDialogueReferenceData
{
    [Tooltip("Scenario Source의 dialogue.wait 액션이 참조하는 안정적인 Dialogue ID입니다.")]
    public string DialogueId = string.Empty;

    [Tooltip("Scenario Source의 dialogues.dialogueData 값입니다. YAML export 시 GUID 대신 이 값을 보존합니다.")]
    public string DialogueDataId = string.Empty;

    [Tooltip("해당 Dialogue ID로 실행할 DialogueData입니다.")]
    public DialogueData Dialogue;
}
