using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public enum AreaMarkerIssueFilter
{
    All,
    Problems,
    Clean
}

public sealed class AreaMarkerWorkbenchWindow : EditorWindow
{
    public const string AllRoomsKey = "__all_rooms__";
    public const string UnboundRoomKey = "__unbound__";

    private const double RefreshDelaySeconds = 0.15d;

    private static readonly string[] TypeLabels = BuildTypeLabels();
    private static readonly string[] IssueFilterLabels = { "전체", "문제 있음", "정상" };

    private RoomMapValidationReport _report = new RoomMapValidationReport("Current Authoring Scope");
    private Vector2 _scroll;
    private string _search = string.Empty;
    private string _roomFilter = AllRoomsKey;
    private int _typeFilter = -1;
    private AreaMarkerIssueFilter _issueFilter = AreaMarkerIssueFilter.All;
    private bool _showErrors = true;
    private bool _showWarnings = true;
    private bool _autoRefresh = true;
    private bool _scanQueued;
    private double _nextScanAt;
    private string[] _roomKeys = { AllRoomsKey };
    private string[] _roomLabels = { "모든 Room" };

    [MenuItem("Hub To Home/오버월드/Area 마커/마커 작업창")]
    private static void Open()
    {
        AreaMarkerWorkbenchWindow window =
            GetWindow<AreaMarkerWorkbenchWindow>("Area Marker");
        window.minSize = new Vector2(620f, 380f);
        window.Show();
    }

    private void OnEnable()
    {
        EditorApplication.hierarchyChanged += QueueScan;
        EditorApplication.projectChanged += QueueScan;
        Undo.undoRedoPerformed += QueueScan;
        Scan();
    }

    private void OnDisable()
    {
        EditorApplication.hierarchyChanged -= QueueScan;
        EditorApplication.projectChanged -= QueueScan;
        Undo.undoRedoPerformed -= QueueScan;
    }

    private void OnFocus()
    {
        QueueScan();
    }

    private void OnInspectorUpdate()
    {
        if (!_autoRefresh || !_scanQueued || EditorApplication.timeSinceStartup < _nextScanAt)
            return;

        Scan();
    }

    private void OnGUI()
    {
        DrawHeader();
        DrawFilters();
        EditorGUILayout.Space(4f);

        _scroll = EditorGUILayout.BeginScrollView(_scroll);
        DrawScopeIssues();
        DrawFeatures();
        EditorGUILayout.EndScrollView();
    }

    public static bool TrySelectAndFrame(RoomMapValidationIssue issue)
    {
        if (issue == null || !issue.CanSelect)
            return false;

        if (issue.Context is Component component)
        {
            Selection.activeGameObject = component.gameObject;
            EditorGUIUtility.PingObject(component.gameObject);
            SceneView.lastActiveSceneView?.FrameSelected();
            return true;
        }

        Selection.activeObject = issue.Context;
        EditorGUIUtility.PingObject(issue.Context);
        return true;
    }

    public static bool MatchesMarker(
        RoomMapMarkerEntry entry,
        RoomMapValidationReport report,
        string search,
        string roomFilter,
        int typeFilter,
        AreaMarkerIssueFilter issueFilter)
    {
        if (entry?.Marker == null || report == null)
            return false;

        AreaMarkerBase marker = entry.Marker;
        if (!string.IsNullOrEmpty(roomFilter)
            && roomFilter != AllRoomsKey
            && !string.Equals(GetRoomKey(entry.Room), roomFilter, StringComparison.Ordinal))
        {
            return false;
        }

        if (typeFilter >= 0 && (int)marker.MarkerType != typeFilter)
            return false;

        bool hasIssues = report.HasIssue(marker);
        if (issueFilter == AreaMarkerIssueFilter.Problems && !hasIssues)
            return false;
        if (issueFilter == AreaMarkerIssueFilter.Clean && hasIssues)
            return false;

        if (string.IsNullOrWhiteSpace(search))
            return true;

        return ContainsIgnoreCase(marker.MarkerId, search)
            || ContainsIgnoreCase(marker.DisplayName, search)
            || ContainsIgnoreCase(marker.Description, search)
            || ContainsIgnoreCase(entry.RoomId, search)
            || ContainsIgnoreCase(marker.MarkerType.ToString(), search);
    }

    public static bool MatchesFeature(
        RoomMapFeatureEntry entry,
        RoomMapValidationReport report,
        string search,
        string roomFilter,
        int typeFilter,
        AreaMarkerIssueFilter issueFilter)
    {
        if (entry?.Context == null || report == null)
            return false;
        if (entry.Marker != null)
        {
            return MatchesMarker(
                new RoomMapMarkerEntry(entry.Marker, entry.Room),
                report,
                search,
                roomFilter,
                typeFilter,
                issueFilter);
        }

        if (!string.IsNullOrEmpty(roomFilter)
            && roomFilter != AllRoomsKey
            && !string.Equals(GetRoomKey(entry.Room), roomFilter, StringComparison.Ordinal))
            return false;
        if (typeFilter >= 0 && (int)entry.FeatureType != typeFilter)
            return false;

        bool hasIssues = report.HasIssue(entry.Context);
        if (issueFilter == AreaMarkerIssueFilter.Problems && !hasIssues)
            return false;
        if (issueFilter == AreaMarkerIssueFilter.Clean && hasIssues)
            return false;
        if (string.IsNullOrWhiteSpace(search))
            return true;

        return ContainsIgnoreCase(entry.StableId, search)
            || ContainsIgnoreCase(entry.DisplayName, search)
            || ContainsIgnoreCase(entry.Description, search)
            || ContainsIgnoreCase(entry.RoomId, search)
            || ContainsIgnoreCase(entry.FeatureType.ToString(), search);
    }

    private void DrawHeader()
    {
        using (new EditorGUILayout.HorizontalScope())
        {
            EditorGUILayout.LabelField("Area Marker 작업창", EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();
            _autoRefresh = GUILayout.Toggle(
                _autoRefresh,
                "자동 갱신",
                EditorStyles.toolbarButton,
                GUILayout.Width(72f));
            if (GUILayout.Button("Scan", GUILayout.Width(72f), GUILayout.Height(22f)))
                Scan();
        }

        EditorGUILayout.LabelField(
            $"{_report.ScopeName}  |  Room {_report.RoomCount}  |  기능 {_report.Features.Count}  "
            + $"|  Spawn {_report.SpawnPointCount}  |  Error {_report.ErrorCount}  "
            + $"|  Warning {_report.WarningCount}",
            EditorStyles.miniLabel);
    }

    private void DrawFilters()
    {
        using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
        {
            _search = GUILayout.TextField(
                _search,
                EditorStyles.toolbarSearchField,
                GUILayout.MinWidth(140f));

            int roomIndex = FindRoomFilterIndex();
            int selectedRoom = EditorGUILayout.Popup(
                roomIndex,
                _roomLabels,
                EditorStyles.toolbarPopup,
                GUILayout.Width(145f));
            if (selectedRoom >= 0 && selectedRoom < _roomKeys.Length)
                _roomFilter = _roomKeys[selectedRoom];

            int typeIndex = Mathf.Clamp(_typeFilter + 1, 0, TypeLabels.Length - 1);
            typeIndex = EditorGUILayout.Popup(
                typeIndex,
                TypeLabels,
                EditorStyles.toolbarPopup,
                GUILayout.Width(116f));
            _typeFilter = typeIndex - 1;

            int issueFilterIndex = EditorGUILayout.Popup(
                (int)_issueFilter,
                IssueFilterLabels,
                EditorStyles.toolbarPopup,
                GUILayout.Width(82f));
            _issueFilter = (AreaMarkerIssueFilter)issueFilterIndex;

            _showErrors = GUILayout.Toggle(
                _showErrors,
                new GUIContent("오류", "오류 표시"),
                EditorStyles.toolbarButton,
                GUILayout.Width(42f));
            _showWarnings = GUILayout.Toggle(
                _showWarnings,
                new GUIContent("경고", "경고 표시"),
                EditorStyles.toolbarButton,
                GUILayout.Width(42f));
        }
    }

    private void DrawScopeIssues()
    {
        bool drewHeader = false;
        for (int i = 0; i < _report.Issues.Count; i++)
        {
            RoomMapValidationIssue issue = _report.Issues[i];
            if (issue.Marker != null || !IsSeverityVisible(issue))
                continue;

            if (!drewHeader)
            {
                EditorGUILayout.LabelField("Room / Scope 문제", EditorStyles.boldLabel);
                drewHeader = true;
            }

            DrawIssue(issue);
        }

        if (drewHeader)
            EditorGUILayout.Space(5f);
    }

    private void DrawFeatures()
    {
        int visibleCount = 0;
        for (int i = 0; i < _report.Features.Count; i++)
        {
            RoomMapFeatureEntry entry = _report.Features[i];
            if (!MatchesFeature(
                    entry,
                    _report,
                    _search,
                    _roomFilter,
                    _typeFilter,
                    _issueFilter))
                continue;

            DrawFeature(entry);
            visibleCount++;
        }

        if (visibleCount == 0)
            EditorGUILayout.HelpBox("현재 필터에 표시할 기능이 없습니다.", MessageType.Info);
    }

    private void DrawFeature(RoomMapFeatureEntry entry)
    {
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                Rect swatch = GUILayoutUtility.GetRect(
                    12f,
                    12f,
                    GUILayout.Width(12f),
                    GUILayout.Height(12f));
                EditorGUI.DrawRect(swatch, entry.GizmoColor);
                EditorGUILayout.LabelField(
                    $"[{entry.ShortTypeLabel}] {entry.DisplayName}",
                    EditorStyles.boldLabel,
                    GUILayout.MinWidth(140f));
                GUILayout.FlexibleSpace();

                int issueCount = _report.GetIssueCount(entry.Context);
                if (issueCount > 0)
                    GUILayout.Label($"{issueCount} 문제", EditorStyles.miniBoldLabel);
                if (GUILayout.Button("선택", GUILayout.Width(52f)))
                {
                    TrySelectAndFrame(new RoomMapValidationIssue(
                        "AREA_FEATURE_SELECT",
                        RoomMapValidationSeverity.Warning,
                        string.Empty,
                        entry.Context,
                        entry.Room,
                        entry.Marker));
                }
            }

            string roomLabel = entry.Room != null
                ? GetRoomDisplayName(entry.Room)
                : "Room 미지정";
            string stableId = string.IsNullOrWhiteSpace(entry.StableId)
                ? "(미지정)"
                : entry.StableId;
            EditorGUILayout.LabelField(
                $"ID: {stableId}    Room: {roomLabel}",
                EditorStyles.miniLabel);
            if (!string.IsNullOrWhiteSpace(entry.Description))
                EditorGUILayout.LabelField(entry.Description, EditorStyles.wordWrappedMiniLabel);

            for (int issueIndex = 0; issueIndex < _report.Issues.Count; issueIndex++)
            {
                RoomMapValidationIssue issue = _report.Issues[issueIndex];
                if (issue.Context == entry.Context && IsSeverityVisible(issue))
                    DrawIssue(issue);
            }
        }
    }

    private static void DrawIssue(RoomMapValidationIssue issue)
    {
        using (new EditorGUILayout.HorizontalScope())
        {
            GUIContent icon = EditorGUIUtility.IconContent(
                issue.Severity == RoomMapValidationSeverity.Error
                    ? "console.erroricon.sml"
                    : "console.warnicon.sml");
            GUILayout.Label(icon, GUILayout.Width(20f), GUILayout.Height(18f));
            EditorGUILayout.LabelField(
                $"[{issue.Code}] {issue.Message}",
                EditorStyles.wordWrappedMiniLabel);

            using (new EditorGUI.DisabledScope(!issue.CanSelect))
            {
                if (GUILayout.Button("이동", GUILayout.Width(48f)))
                    TrySelectAndFrame(issue);
            }
        }
    }

    private void Scan()
    {
        RoomMapValidationInput input = RoomMapValidationScopeCapture.CaptureCurrent();
        _report = RoomMapValidationScanner.Scan(input);
        _scanQueued = false;
        RebuildRoomFilters();
        Repaint();
    }

    private void QueueScan()
    {
        _scanQueued = true;
        _nextScanAt = EditorApplication.timeSinceStartup + RefreshDelaySeconds;
        Repaint();
    }

    private void RebuildRoomFilters()
    {
        var labelsByKey = new Dictionary<string, string>(StringComparer.Ordinal);
        bool hasUnbound = false;
        for (int i = 0; i < _report.Features.Count; i++)
        {
            RoomInstance room = _report.Features[i].Room;
            if (room == null)
            {
                hasUnbound = true;
                continue;
            }

            labelsByKey[GetRoomKey(room)] = GetRoomDisplayName(room);
        }

        var keys = new List<string>(labelsByKey.Keys);
        keys.Sort(StringComparer.Ordinal);
        _roomKeys = new string[keys.Count + (hasUnbound ? 2 : 1)];
        _roomLabels = new string[_roomKeys.Length];
        _roomKeys[0] = AllRoomsKey;
        _roomLabels[0] = "모든 Room";

        int destination = 1;
        for (int i = 0; i < keys.Count; i++)
        {
            _roomKeys[destination] = keys[i];
            _roomLabels[destination] = labelsByKey[keys[i]];
            destination++;
        }

        if (hasUnbound)
        {
            _roomKeys[destination] = UnboundRoomKey;
            _roomLabels[destination] = "Room 미지정";
        }

        if (Array.IndexOf(_roomKeys, _roomFilter) < 0)
            _roomFilter = AllRoomsKey;
    }

    private int FindRoomFilterIndex()
    {
        int index = Array.IndexOf(_roomKeys, _roomFilter);
        return index >= 0 ? index : 0;
    }

    private bool IsSeverityVisible(RoomMapValidationIssue issue)
    {
        return issue.Severity == RoomMapValidationSeverity.Error
            ? _showErrors
            : _showWarnings;
    }

    private static string GetRoomKey(RoomInstance room)
    {
        if (room == null)
            return UnboundRoomKey;

        return !string.IsNullOrWhiteSpace(room.RoomId)
            ? room.RoomId
            : "instance:" + room.GetInstanceID();
    }

    private static string GetRoomDisplayName(RoomInstance room)
    {
        return !string.IsNullOrWhiteSpace(room.RoomId) ? room.RoomId : room.name;
    }

    private static bool ContainsIgnoreCase(string value, string search)
    {
        return !string.IsNullOrEmpty(value)
            && value.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static string[] BuildTypeLabels()
    {
        Array values = Enum.GetValues(typeof(AreaMarkerType));
        var labels = new string[values.Length + 1];
        labels[0] = "모든 타입";
        for (int i = 0; i < values.Length; i++)
        {
            labels[i + 1] = ObjectNames.NicifyVariableName(values.GetValue(i).ToString());
        }

        return labels;
    }
}
