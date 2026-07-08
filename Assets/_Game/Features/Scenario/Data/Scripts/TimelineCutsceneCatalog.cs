using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

[CreateAssetMenu(fileName = "TimelineCutsceneCatalog", menuName = "HubToHome/Scenario/Timeline Cutscene Catalog")]
public sealed class TimelineCutsceneCatalog : SerializedScriptableObject
{
    [BoxGroup("기본 정보")]
    [LabelText("카탈로그 ID")]
    public string CatalogId = "timeline.default";

    [BoxGroup("컷신 목록")]
    [TableList(AlwaysExpanded = true, DrawScrollView = false)]
    [Required]
    [InlineEditor(InlineEditorObjectFieldModes.Boxed)]
    [LabelText("컷신 에셋")]
    public List<TimelineCutsceneData> Cutscenes = new List<TimelineCutsceneData>();

    public TimelineCutsceneData FindById(string cutsceneId)
    {
        string normalized = Normalize(cutsceneId);
        if (string.IsNullOrEmpty(normalized) || Cutscenes == null)
        {
            return null;
        }

        for (int i = 0; i < Cutscenes.Count; i++)
        {
            TimelineCutsceneData cutscene = Cutscenes[i];
            if (cutscene != null && Normalize(cutscene.CutsceneId) == normalized)
            {
                return cutscene;
            }
        }

        return null;
    }

    [Button("Validate Catalog")]
    public void ValidateAndLog()
    {
        var seenIds = new HashSet<string>();
        var issues = new List<string>();
        if (Cutscenes == null)
        {
            issues.Add("컷신 목록이 null입니다.");
        }
        else
        {
            for (int i = 0; i < Cutscenes.Count; i++)
            {
                TimelineCutsceneData cutscene = Cutscenes[i];
                if (cutscene == null)
                {
                    issues.Add("Cutscenes[" + i + "]가 null입니다.");
                    continue;
                }

                string cutsceneId = Normalize(cutscene.CutsceneId);
                if (string.IsNullOrEmpty(cutsceneId))
                {
                    issues.Add("Cutscenes[" + i + "]의 CutsceneId가 비어 있습니다.");
                    continue;
                }

                if (!seenIds.Add(cutsceneId))
                {
                    issues.Add("중복된 컷신 ID가 있습니다: " + cutsceneId);
                }

                if (cutscene.TimelineAsset == null)
                {
                    issues.Add("TimelineAsset이 없습니다: " + cutsceneId);
                }
            }
        }

        if (issues.Count == 0)
        {
            Debug.Log("[TimelineCutsceneCatalog] Validation passed: " + Normalize(CatalogId), this);
            return;
        }

        Debug.LogError(
            "[TimelineCutsceneCatalog] Validation failed for '" + Normalize(CatalogId) + "'\n- "
            + string.Join("\n- ", issues.ToArray()),
            this);
    }

    private static string Normalize(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
    }
}