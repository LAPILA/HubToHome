using System;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 현재 Scene 또는 Room Prefab 편집 범위를 공용 Room Map 규칙으로 검사합니다.
/// </summary>
public static class RoomMapValidator
{
    [MenuItem("Hub To Home/오버월드/맵 검사/현재 열린 룸 맵 검사")]
    public static void ValidateOpenRoomMap()
    {
        RoomMapValidationInput input = RoomMapValidationScopeCapture.CaptureCurrent();
        RoomMapValidationReport report = RoomMapValidationScanner.Scan(input);
        LogReport(report);
    }

    public static void LogReport(RoomMapValidationReport report)
    {
        if (report == null)
            throw new ArgumentNullException(nameof(report));

        for (int i = 0; i < report.Issues.Count; i++)
        {
            RoomMapValidationIssue issue = report.Issues[i];
            string message = $"[RoomMapValidator][{issue.Code}] {issue.Message}";
            if (issue.Severity == RoomMapValidationSeverity.Error)
                Debug.LogError(message, issue.Context);
            else
                Debug.LogWarning(message, issue.Context);
        }

        Debug.Log(
            $"[RoomMapValidator] 검사 완료. Scope={report.ScopeName}, "
            + $"Rooms={report.RoomCount}, Markers={report.Markers.Count}, "
            + $"Doors={report.DoorCount}, SpawnPoints={report.SpawnPointCount}, "
            + $"Errors={report.ErrorCount}, Warnings={report.WarningCount}");
    }
}
