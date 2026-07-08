using System.Collections.Generic;
using Sirenix.OdinInspector;
using Sirenix.Serialization;
using UnityEngine;

[System.Serializable]
public sealed class AreaMarkerSummaryEntry
{
    [LabelText("마커 ID")]
    public string MarkerId;

    [LabelText("타입")]
    public AreaMarkerType MarkerType;

    [LabelText("표시 이름")]
    public string DisplayName;

    [LabelText("Area ID")]
    public string AreaId;

    [LabelText("프리팹 경로")]
    public string HierarchyPath;

    [MultiLineProperty(3)]
    [LabelText("검증")]
    public string ValidationSummary;
}

[CreateAssetMenu(menuName = "HubToHome/Overworld/Area Definition", fileName = "AreaDefinition")]
public sealed class AreaDefinition : SerializedScriptableObject
{
    [TitleGroup("기본 정보")]
    [SerializeField]
    [LabelText("Area ID")]
    private string _areaId;

    [TitleGroup("기본 정보")]
    [SerializeField]
    [LabelText("Room Definition")]
    private RoomDefinition _roomDefinition;

    [TitleGroup("기본 정보")]
    [TextArea(2, 4)]
    [SerializeField]
    [LabelText("설명")]
    private string _description;

    [TitleGroup("마커 요약")]
    [OdinSerialize]
    [TableList(AlwaysExpanded = true, DrawScrollView = true)]
    [LabelText("마커 목록")]
    private List<AreaMarkerSummaryEntry> _markerSummaries = new List<AreaMarkerSummaryEntry>();

    public string AreaId => _areaId;
    public RoomDefinition RoomDefinition => _roomDefinition;
    public IReadOnlyList<AreaMarkerSummaryEntry> MarkerSummaries => _markerSummaries;

    [TitleGroup("검증")]
    [ShowInInspector, ReadOnly]
    [LabelText("마커 수")]
    public int MarkerCount => _markerSummaries != null ? _markerSummaries.Count : 0;

    [TitleGroup("검증")]
    [ShowInInspector, ReadOnly]
    [LabelText("이슈 있는 마커 수")]
    public int InvalidMarkerCount
    {
        get
        {
            if (_markerSummaries == null)
                return 0;

            int count = 0;
            for (int i = 0; i < _markerSummaries.Count; i++)
            {
                if (!string.IsNullOrWhiteSpace(_markerSummaries[i].ValidationSummary))
                    count++;
            }

            return count;
        }
    }

#if UNITY_EDITOR
    [Button("Refresh Marker Summary")]
    public void RefreshMarkerSummary()
    {
        if (_roomDefinition == null || _roomDefinition.RoomPrefab == null)
        {
            Debug.LogWarning("[AreaDefinition] RoomDefinition 또는 RoomPrefab이 비어 있습니다.", this);
            return;
        }

        string prefabPath = UnityEditor.AssetDatabase.GetAssetPath(_roomDefinition.RoomPrefab.gameObject);
        if (string.IsNullOrWhiteSpace(prefabPath))
        {
            Debug.LogWarning("[AreaDefinition] RoomPrefab asset path를 찾을 수 없습니다.", this);
            return;
        }

        GameObject prefabRoot = UnityEditor.PrefabUtility.LoadPrefabContents(prefabPath);
        try
        {
            AreaMarkerBase[] markers = prefabRoot.GetComponentsInChildren<AreaMarkerBase>(true);
            _markerSummaries.Clear();

            for (int i = 0; i < markers.Length; i++)
            {
                AreaMarkerBase marker = markers[i];
                if (marker == null)
                    continue;

                var issues = new List<string>();
                marker.CollectValidationIssues(issues);
                _markerSummaries.Add(new AreaMarkerSummaryEntry
                {
                    MarkerId = marker.MarkerId,
                    MarkerType = marker.MarkerType,
                    DisplayName = marker.DisplayName,
                    AreaId = marker.AreaId,
                    HierarchyPath = BuildHierarchyPath(marker.transform, prefabRoot.transform),
                    ValidationSummary = issues.Count > 0 ? string.Join("\n", issues.ToArray()) : string.Empty
                });
            }

            if (string.IsNullOrWhiteSpace(_areaId))
                _areaId = _roomDefinition.RoomId;

            UnityEditor.EditorUtility.SetDirty(this);
            Debug.Log($"[AreaDefinition] Marker summary refreshed: area={_areaId}, markers={_markerSummaries.Count}", this);
        }
        finally
        {
            UnityEditor.PrefabUtility.UnloadPrefabContents(prefabRoot);
        }
    }

    [Button("Validate Area")]
    public void ValidateAndLog()
    {
        RefreshMarkerSummary();
        if (InvalidMarkerCount <= 0)
        {
            Debug.Log($"[AreaDefinition] Validation passed: area={_areaId}, markers={MarkerCount}", this);
            return;
        }

        Debug.LogWarning($"[AreaDefinition] Validation found {InvalidMarkerCount} marker(s) with issues in area={_areaId}", this);
    }

    private static string BuildHierarchyPath(Transform target, Transform root)
    {
        if (target == null)
            return string.Empty;

        if (target == root)
            return root.name;

        var names = new Stack<string>();
        Transform current = target;
        while (current != null)
        {
            names.Push(current.name);
            if (current == root)
                break;
            current = current.parent;
        }

        return string.Join("/", names.ToArray());
    }
#endif
}