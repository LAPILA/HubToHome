using System;
using UnityEngine;

/// <summary>
/// 문, 이벤트, 컷씬이 공통으로 사용하는 맵 이동 요청 데이터입니다.
/// </summary>
[Serializable]
public class MapTransitionRequest
{
    [Header("Destination")]
    public MapTransitionType TransitionType = MapTransitionType.Room;
    public string TargetSceneName;
    public RoomDefinition TargetRoom;
    public string TargetSpawnPointId;

    [Header("Arrival")]
    public FacingDirection FacingAfterEnter = FacingDirection.Keep;
    public Vector2 FallbackPosition;
    public bool UseFallbackPosition;

    [Header("Presentation")]
    [Min(0f)] public float FadeDuration = 0.25f;

    public bool IsValid(out string error)
    {
        if (TransitionType == MapTransitionType.Scene && string.IsNullOrWhiteSpace(TargetSceneName))
        {
            error = "Scene 전환 요청인데 TargetSceneName이 비어 있습니다.";
            return false;
        }

        if (TransitionType == MapTransitionType.Room && TargetRoom == null)
        {
            error = "Room 전환 요청인데 TargetRoom이 비어 있습니다.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(TargetSpawnPointId) && !UseFallbackPosition)
        {
            error = "TargetSpawnPointId가 비어 있고 FallbackPosition도 사용하지 않습니다.";
            return false;
        }

        error = string.Empty;
        return true;
    }
}
