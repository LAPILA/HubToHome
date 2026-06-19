using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public sealed class ActionCatalogParameter
{
    [Tooltip("액션 파라미터 이름입니다. 예: duration, actor, target")]
    public string Name = string.Empty;

    [Tooltip("파라미터 타입 힌트입니다. 예: string, float, bool, actorRef")]
    public string Type = string.Empty;

    [Tooltip("에디터에 표시할 한국어 이름입니다.")]
    public string DisplayNameKo = string.Empty;

    [TextArea(1, 4)]
    [Tooltip("사람이 읽는 한국어 설명입니다.")]
    public string DescriptionKo = string.Empty;

    [Tooltip("이 파라미터가 없으면 검증 오류로 처리합니다.")]
    public bool Required;

    [Tooltip("값을 생략했을 때 사용할 기본값 표현입니다.")]
    public string DefaultValue = string.Empty;
}

[Serializable]
public sealed class ActionCatalogEntry
{
    [Tooltip("Scenario Source와 Runtime Sequence에서 사용하는 안정적인 액션 ID입니다. 예: flow.wait")]
    public string ActionId = string.Empty;

    [Tooltip("에디터에서 탐색할 카테고리입니다. 예: flow, dialogue, actor, battle")]
    public string Category = string.Empty;

    [Tooltip("에디터에 표시할 한국어 이름입니다.")]
    public string DisplayNameKo = string.Empty;

    [TextArea(1, 6)]
    [Tooltip("사람이 읽는 한국어 설명입니다.")]
    public string DescriptionKo = string.Empty;

    [Tooltip("ActionDirector가 연결할 런타임 adapter ID입니다.")]
    public string RuntimeAdapterId = string.Empty;

    [Tooltip("검증과 에디터 입력 폼에 사용할 파라미터 목록입니다.")]
    public List<ActionCatalogParameter> Parameters = new List<ActionCatalogParameter>();

    [TextArea(1, 10)]
    [Tooltip("AI와 사람이 참고할 YAML 예시입니다.")]
    public string ExampleYaml = string.Empty;

    [Tooltip("체크하면 새 시퀀스에서 사용하지 않는 액션으로 취급합니다.")]
    public bool Disabled;
}

[CreateAssetMenu(fileName = "ActionCatalog", menuName = "HubToHome/Scenario/Action Catalog")]
public sealed class ActionCatalogAsset : ScriptableObject
{
    [Tooltip("이 카탈로그의 안정적인 ID입니다.")]
    public string CatalogId = "default";

    [Tooltip("Scenario Source와 Action Sequence에서 사용할 수 있는 액션 목록입니다.")]
    public List<ActionCatalogEntry> Entries = new List<ActionCatalogEntry>();

    public ActionCatalogEntry FindById(string actionId)
    {
        if (string.IsNullOrWhiteSpace(actionId))
        {
            return null;
        }

        for (int i = 0; i < Entries.Count; i++)
        {
            ActionCatalogEntry entry = Entries[i];
            if (entry != null && entry.ActionId == actionId)
            {
                return entry;
            }
        }

        return null;
    }
}
