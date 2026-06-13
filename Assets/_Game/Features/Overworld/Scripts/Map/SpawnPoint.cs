using UnityEngine;

/// <summary>
/// 좌표 하드코딩 대신 ID로 도착 지점을 찾기 위한 컴포넌트입니다.
/// 룸 프리팹 또는 씬 안의 도착 위치마다 배치합니다.
/// </summary>
public class SpawnPoint : MonoBehaviour
{
    [SerializeField] private string _spawnPointId;
    [SerializeField] private FacingDirection _defaultFacing = FacingDirection.Keep;

    public string SpawnPointId => _spawnPointId;
    public FacingDirection DefaultFacing => _defaultFacing;

    public static bool TryFind(string spawnPointId, out SpawnPoint spawnPoint)
    {
        spawnPoint = null;
        if (string.IsNullOrWhiteSpace(spawnPointId)) return false;

        SpawnPoint[] points = FindObjectsByType<SpawnPoint>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < points.Length; i++)
        {
            if (points[i] != null && points[i].SpawnPointId == spawnPointId)
            {
                spawnPoint = points[i];
                return true;
            }
        }

        return false;
    }

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, 0.18f);
        Gizmos.DrawLine(transform.position, transform.position + DirectionToVector(_defaultFacing) * 0.45f);
    }
#endif

    private static Vector3 DirectionToVector(FacingDirection direction)
    {
        return direction switch
        {
            FacingDirection.Up => Vector3.up,
            FacingDirection.Left => Vector3.left,
            FacingDirection.Right => Vector3.right,
            _ => Vector3.down
        };
    }
}
