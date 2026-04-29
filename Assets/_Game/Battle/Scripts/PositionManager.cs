using UnityEngine;
using Sirenix.OdinInspector;

/// <summary>
/// 전투 씬의 모든 포지션 Transform을 관리합니다.
/// 
/// Inspector 연결:
/// - PlayerDefaultPos[0~3]  : 아군 기본 위치 (좌측, 최대 4명)
/// - EnemyDefaultPos[0~7]   : 적 기본 위치 (우측, 최대 8마리)
/// - CenterPos              : 근거리 교전 무대 중앙
/// - EnemyAttackPos[0~3]    : 적이 아군 공격 시 다가오는 아군 바로 앞 위치
/// </summary>
public class PositionManager : MonoBehaviour
{
    public static PositionManager Instance { get; private set; }

    [BoxGroup("Player Positions"), LabelText("아군 기본 위치 (최대 3)")]
    [SerializeField] private Transform[] _playerDefaultPos = new Transform[3];

    [BoxGroup("Enemy Positions"), LabelText("적 기본 위치 (최대 3)")]
    [SerializeField] private Transform[] _enemyDefaultPos = new Transform[3];

    [BoxGroup("Key Positions"), LabelText("교전 중앙")]
    [SerializeField] private Transform _centerPos;

    [BoxGroup("Key Positions"), LabelText("적 공격 도달 위치 (아군 앞, 최대 3)")]
    [SerializeField] private Transform[] _enemyAttackPos = new Transform[3];

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    // ── 접근자 ────────────────────────────────────────────────

    /// <summary>아군 index의 기본 위치를 반환합니다.</summary>
    public Vector3 GetPlayerDefaultPos(int index)
    {
        if (index < 0 || index >= _playerDefaultPos.Length || _playerDefaultPos[index] == null)
        {
            Debug.LogWarning($"[PositionManager] PlayerDefaultPos[{index}] is not set.");
            return Vector3.zero;
        }
        return _playerDefaultPos[index].position;
    }

    /// <summary>적 index의 기본 위치를 반환합니다.</summary>
    public Vector3 GetEnemyDefaultPos(int index)
    {
        if (index < 0 || index >= _enemyDefaultPos.Length || _enemyDefaultPos[index] == null)
        {
            Debug.LogWarning($"[PositionManager] EnemyDefaultPos[{index}] is not set.");
            return Vector3.zero;
        }
        return _enemyDefaultPos[index].position;
    }

    /// <summary>교전 중앙 위치를 반환합니다.</summary>
    public Vector3 GetCenterPos()
    {
        if (_centerPos == null)
        {
            Debug.LogWarning("[PositionManager] CenterPos is not set.");
            return Vector3.zero;
        }
        return _centerPos.position;
    }

    /// <summary>적이 아군 index를 공격할 때 도달하는 위치를 반환합니다.</summary>
    public Vector3 GetEnemyAttackPos(int playerIndex)
    {
        if (playerIndex < 0 || playerIndex >= _enemyAttackPos.Length || _enemyAttackPos[playerIndex] == null)
        {
            Debug.LogWarning($"[PositionManager] EnemyAttackPos[{playerIndex}] is not set.");
            return GetPlayerDefaultPos(playerIndex);
        }
        return _enemyAttackPos[playerIndex].position;
    }
}
