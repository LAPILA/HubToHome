using System;
using System.Collections.Generic;
using UnityEngine;

public enum ActionSequenceLifecycle
{
    Draft,
    Ready,
    Deprecated
}

[Serializable]
public sealed class ActionSequenceContractData
{
    [TextArea(2, 6)]
    [Tooltip("이 시퀀스가 무엇을 하는지 설명합니다.")]
    public string DescriptionKo = string.Empty;

    [TextArea(2, 6)]
    [Tooltip("이 시퀀스를 언제, 어떤 상황에서 사용하는지 설명합니다.")]
    public string UsageKo = string.Empty;

    [Tooltip("시퀀스의 제작 상태입니다.")]
    public ActionSequenceLifecycle Lifecycle = ActionSequenceLifecycle.Draft;

    [Tooltip("검색과 분류에 사용하는 태그입니다.")]
    public List<string> Tags = new List<string>();

    [Tooltip("이 시퀀스를 실행할 수 있는 Primary Mode ID입니다. 비어 있으면 제한하지 않습니다.")]
    public List<string> AllowedPrimaryModes = new List<string>();

    public static ActionSequenceContractData CopyOf(ActionSequenceContractData source)
    {
        var copy = new ActionSequenceContractData();
        if (source == null)
        {
            return copy;
        }

        copy.DescriptionKo = source.DescriptionKo ?? string.Empty;
        copy.UsageKo = source.UsageKo ?? string.Empty;
        copy.Lifecycle = source.Lifecycle;
        CopyStrings(source.Tags, copy.Tags);
        CopyStrings(source.AllowedPrimaryModes, copy.AllowedPrimaryModes);
        return copy;
    }

    private static void CopyStrings(List<string> source, List<string> destination)
    {
        if (source == null)
        {
            return;
        }

        for (int i = 0; i < source.Count; i++)
        {
            destination.Add(source[i] ?? string.Empty);
        }
    }
}
