using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public sealed class ScenarioActionData
{
    [Tooltip("Action Catalog에 등록된 액션 ID입니다. 예: flow.wait, dialogue.wait")]
    public string ActionId = string.Empty;

    [TextArea(1, 8)]
    [Tooltip("액션 파라미터를 JSON 형태로 저장합니다. YAML Source와 Editor가 이 값을 동기화합니다.")]
    public string ParametersJson = "{}";

    [Tooltip("체크하면 실행 시 이 액션과 하위 액션을 건너뜁니다.")]
    public bool Disabled;

    [Tooltip("parallel, branch 같은 flow 액션이 소유하는 하위 액션 목록입니다.")]
    public List<ScenarioActionData> Children = new List<ScenarioActionData>();
}
