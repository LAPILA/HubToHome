using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Room 기반 맵 제작 시 자주 나는 연결 실수를 검사합니다.
/// 메뉴: HubToHome > Overworld > Validate Open Room Map
/// </summary>
public static class RoomMapValidator
{
    [MenuItem("HubToHome/오버월드/맵 검사/현재 열린 룸 맵 검사")]
    public static void ValidateOpenRoomMap()
    {
        DoorTransition[] doors = Object.FindObjectsByType<DoorTransition>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        SpawnPoint[] spawnPoints = Object.FindObjectsByType<SpawnPoint>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        RoomContainer[] containers = Object.FindObjectsByType<RoomContainer>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        MapTransitionService[] services = Object.FindObjectsByType<MapTransitionService>(FindObjectsInactive.Include, FindObjectsSortMode.None);

        int errorCount = 0;
        int warningCount = 0;

        if (services.Length == 0)
        {
            Debug.LogError("[RoomMapValidator] 현재 씬에 MapTransitionService가 없습니다.");
            errorCount++;
        }

        if (containers.Length == 0)
        {
            Debug.LogError("[RoomMapValidator] 현재 씬에 RoomContainer가 없습니다.");
            errorCount++;
        }

        Dictionary<string, List<SpawnPoint>> spawnPointMap = new Dictionary<string, List<SpawnPoint>>();
        for (int i = 0; i < spawnPoints.Length; i++)
        {
            string id = spawnPoints[i].SpawnPointId;
            if (string.IsNullOrWhiteSpace(id))
            {
                Debug.LogError($"[RoomMapValidator] SpawnPointId가 비어 있습니다: {spawnPoints[i].name}", spawnPoints[i]);
                errorCount++;
                continue;
            }

            if (!spawnPointMap.TryGetValue(id, out List<SpawnPoint> list))
            {
                list = new List<SpawnPoint>();
                spawnPointMap[id] = list;
            }

            list.Add(spawnPoints[i]);
        }

        foreach (KeyValuePair<string, List<SpawnPoint>> pair in spawnPointMap)
        {
            if (pair.Value.Count <= 1) continue;
            Debug.LogWarning($"[RoomMapValidator] 현재 로드된 범위에 중복 SpawnPointId가 있습니다. Id={pair.Key}, Count={pair.Value.Count}");
            warningCount++;
        }

        for (int i = 0; i < doors.Length; i++)
        {
            DoorTransition door = doors[i];
            MapTransitionRequest request = door.Request;
            if (request == null)
            {
                Debug.LogError($"[RoomMapValidator] DoorTransition 요청이 비어 있습니다. Door={door.name}", door);
                errorCount++;
                continue;
            }

            if (!request.IsValid(out string error))
            {
                Debug.LogError($"[RoomMapValidator] DoorTransition 요청 오류: Door={door.name}, Error={error}", door);
                errorCount++;
                continue;
            }

            if (request.TransitionType == MapTransitionType.Room && request.TargetRoom != null && !request.TargetRoom.IsValid)
            {
                Debug.LogError($"[RoomMapValidator] Door={door.name}의 TargetRoom이 유효하지 않습니다.", door);
                errorCount++;
            }

            if (!string.IsNullOrWhiteSpace(request.TargetSpawnPointId) && !spawnPointMap.ContainsKey(request.TargetSpawnPointId))
            {
                Debug.LogWarning($"[RoomMapValidator] 현재 로드된 씬/룸 안에서 목적지 SpawnPointId를 찾지 못했습니다. Door={door.name}, TargetSpawnPointId={request.TargetSpawnPointId}", door);
                warningCount++;
            }
        }

        Debug.Log($"[RoomMapValidator] 검사 완료. Doors={doors.Length}, SpawnPoints={spawnPoints.Length}, Errors={errorCount}, Warnings={warningCount}");
    }
}
