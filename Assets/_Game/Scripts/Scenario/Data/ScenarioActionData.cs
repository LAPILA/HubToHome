using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public sealed class ScenarioActionData
{
    [Tooltip("시퀀스 안에서 이 액션 블록을 식별하는 안정적인 ID입니다.")]
    public string BlockId = string.Empty;

    [Tooltip("기획자가 읽기 쉬운 블록 표시명입니다. 비워두면 ActionId/카탈로그 이름을 사용합니다.")]
    public string DesignerLabel = string.Empty;

    [Tooltip("Action Catalog에 등록된 액션 ID입니다. 예: flow.wait, dialogue.wait")]
    public string ActionId = string.Empty;

    [TextArea(1, 8)]
    [Tooltip("액션 파라미터를 JSON 형태로 저장합니다. YAML Source와 Editor가 이 값을 동기화합니다.")]
    public string ParametersJson = "{}";

    [TextArea(1, 4)]
    [Tooltip("기획 메모입니다. 런타임 실행에는 영향을 주지 않습니다.")]
    public string Note = string.Empty;

    [Tooltip("체크하면 실행 시 이 액션과 하위 액션을 건너뜁니다.")]
    public bool Disabled;

    [SerializeReference]
    [Tooltip("parallel, branch 같은 flow 액션이 소유하는 하위 액션 목록입니다.")]
    public List<ScenarioActionData> Children = new List<ScenarioActionData>();

    public bool Enabled
    {
        get { return !Disabled; }
        set { Disabled = !value; }
    }
}
