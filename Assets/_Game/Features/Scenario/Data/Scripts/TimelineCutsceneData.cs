using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

public enum TimelineCutsceneBindingMode
{
    GenericBinding,
    ReferenceValue
}

public enum TimelineCutsceneBindingKeyKind
{
    ActorKey,
    CameraKey,
    AudioKey,
    SceneObjectName
}

public enum TimelineCutsceneBindingValueType
{
    Auto,
    GameObject,
    Transform,
    Animator,
    AudioSource,
    CameraController,
    CinemachineCamera,
    PlayableDirector
}

[Serializable]
public sealed class TimelineCutsceneBindingEntry
{
    [TableColumnWidth(120, Resizable = false)]
    [LabelText("연결 방식")]
    public TimelineCutsceneBindingMode BindingMode = TimelineCutsceneBindingMode.GenericBinding;

    [TableColumnWidth(220)]
    [LabelText("바인딩 이름")]
    [ValidateInput(nameof(HasBindingName), "Track streamName 또는 exposed reference 이름이 필요합니다.")]
    public string BindingName = string.Empty;

    [TableColumnWidth(100, Resizable = false)]
    [LabelText("키 종류")]
    public TimelineCutsceneBindingKeyKind KeyKind = TimelineCutsceneBindingKeyKind.ActorKey;

    [TableColumnWidth(180)]
    [LabelText("키")]
    [ValueDropdown(nameof(GetSuggestedKeys))]
    [ValidateInput(nameof(HasKey), "키가 비어 있습니다.")]
    public string Key = string.Empty;

    [TableColumnWidth(110, Resizable = false)]
    [LabelText("값 타입")]
    public TimelineCutsceneBindingValueType ValueType = TimelineCutsceneBindingValueType.Auto;

    [TableColumnWidth(64, Resizable = false)]
    [LabelText("필수")]
    public bool Required = true;

    [LabelText("메모")]
    public string Note = string.Empty;

    private bool HasBindingName(string value)
    {
        return !string.IsNullOrWhiteSpace(value);
    }

    private bool HasKey(string value)
    {
        return !string.IsNullOrWhiteSpace(value);
    }

    private static IEnumerable<string> GetSuggestedKeys()
    {
        return new[]
        {
            "player",
            "battle",
            "center",
            "bgm",
            "bgm_secondary",
            "sfx",
            "voice"
        };
    }
}

[CreateAssetMenu(fileName = "TimelineCutscene", menuName = "HubToHome/Scenario/Timeline Cutscene")]
public sealed class TimelineCutsceneData : SerializedScriptableObject
{
    [BoxGroup("기본 정보")]
    [LabelText("컷신 ID")]
    [ValidateInput(nameof(HasCutsceneId), "컷신 ID가 필요합니다.")]
    public string CutsceneId = "new_cutscene";

    [BoxGroup("기본 정보")]
    [LabelText("표시 이름")]
    public string DisplayNameKo = string.Empty;

    [BoxGroup("기본 정보")]
    [TextArea(2, 4)]
    [LabelText("설명")]
    public string DescriptionKo = string.Empty;

    [BoxGroup("타임라인")]
    [Required]
    [InlineEditor(InlineEditorObjectFieldModes.Boxed)]
    [ValidateInput(nameof(HasTimelineAsset), "TimelineAsset 참조가 필요합니다.")]
    [LabelText("Timeline Asset")]
    public TimelineAsset TimelineAsset;

    [BoxGroup("바인딩/트랙 출력")]
    [TableList(AlwaysExpanded = true, DrawScrollView = false)]
    [LabelText("Generic Binding")]
    public List<TimelineCutsceneBindingEntry> OutputBindings = new List<TimelineCutsceneBindingEntry>();

    [BoxGroup("바인딩/Exposed Reference")]
    [TableList(AlwaysExpanded = true, DrawScrollView = false)]
    [LabelText("Reference Binding")]
    public List<TimelineCutsceneBindingEntry> ReferenceBindings = new List<TimelineCutsceneBindingEntry>();

    [BoxGroup("검증")]
    [ShowIf(nameof(HasAnyBindings))]
    [ReadOnly]
    [LabelText("총 바인딩 수")]
    public int TotalBindingCount => CountBindings(OutputBindings) + CountBindings(ReferenceBindings);

    [BoxGroup("검증")]
    [Button("Validate")]
    public void ValidateAndLog()
    {
        var issues = new List<string>();
        if (!HasCutsceneId(CutsceneId))
        {
            issues.Add("컷신 ID가 비어 있습니다.");
        }

        if (!HasTimelineAsset(TimelineAsset))
        {
            issues.Add("TimelineAsset 참조가 없습니다.");
        }

        ValidateBindingList("OutputBindings", OutputBindings, issues);
        ValidateBindingList("ReferenceBindings", ReferenceBindings, issues);

        if (issues.Count == 0)
        {
            Debug.Log("[TimelineCutsceneData] Validation passed: " + SafeId(CutsceneId), this);
            return;
        }

        Debug.LogError(
            "[TimelineCutsceneData] Validation failed for '" + SafeId(CutsceneId) + "'\n- "
            + string.Join("\n- ", issues.ToArray()),
            this);
    }

    private bool HasCutsceneId(string value)
    {
        return !string.IsNullOrWhiteSpace(value);
    }

    private bool HasTimelineAsset(TimelineAsset asset)
    {
        return asset != null;
    }

    private bool HasAnyBindings()
    {
        return CountBindings(OutputBindings) + CountBindings(ReferenceBindings) > 0;
    }

    private static int CountBindings(List<TimelineCutsceneBindingEntry> bindings)
    {
        return bindings != null ? bindings.Count : 0;
    }

    private static void ValidateBindingList(
        string listName,
        List<TimelineCutsceneBindingEntry> bindings,
        List<string> issues)
    {
        if (bindings == null)
        {
            return;
        }

        for (int i = 0; i < bindings.Count; i++)
        {
            TimelineCutsceneBindingEntry binding = bindings[i];
            if (binding == null)
            {
                issues.Add(listName + "[" + i + "] 항목이 null입니다.");
                continue;
            }

            if (string.IsNullOrWhiteSpace(binding.BindingName))
            {
                issues.Add(listName + "[" + i + "] 바인딩 이름이 비어 있습니다.");
            }

            if (string.IsNullOrWhiteSpace(binding.Key))
            {
                issues.Add(listName + "[" + i + "] 키가 비어 있습니다.");
            }
        }
    }

    private static string SafeId(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? "<empty>" : value.Trim();
    }
}