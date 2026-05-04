using UnityEngine;
using Sirenix.OdinInspector;

/// <summary>
/// 전투 씬의 위치(Transform)를 제공하는 Service Locator / Registry 클래스.
/// </summary>
public class PositionManager : MonoBehaviour
{
    public static PositionManager Instance { get; private set; }

    // 🚨 배열 크기를 고정하지 않아, 나중에 파티원이나 적이 4~5명으로 늘어나도 에러가 나지 않습니다.
    [BoxGroup("Player Positions")] [SerializeField] private Transform[] _playerDefaultPos;
    [BoxGroup("Enemy Positions")]  [SerializeField] private Transform[] _enemyDefaultPos;
    [BoxGroup("Key Positions")]    [SerializeField] private Transform _centerPos;
    [BoxGroup("Key Positions")]    [SerializeField] private Transform[] _enemyAttackPos;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    // ── 안전한 배열 접근 헬퍼 ──
    private Vector3 GetSafePosition(Transform[] array, int index, string arrayName)
    {
        if (array == null || index < 0 || index >= array.Length || array[index] == null)
        {
            Debug.LogWarning($"[PositionManager] {arrayName}[{index}] is missing! Returning Vector3.zero.");
            return Vector3.zero;
        }
        return array[index].position;
    }

    public Vector3 GetPlayerDefaultPos(int index) => GetSafePosition(_playerDefaultPos, index, "PlayerDefaultPos");
    public Vector3 GetEnemyDefaultPos(int index)  => GetSafePosition(_enemyDefaultPos, index, "EnemyDefaultPos");
    public Vector3 GetCenterPos()                 => _centerPos != null ? _centerPos.position : Vector3.zero;
    
    public Vector3 GetEnemyAttackPos(int playerIndex)
    {
        // AttackPos가 없으면 기본 플레이어 위치라도 반환하여 멈춤 방지
        if (_enemyAttackPos == null || playerIndex < 0 || playerIndex >= _enemyAttackPos.Length || _enemyAttackPos[playerIndex] == null)
            return GetPlayerDefaultPos(playerIndex);
            
        return _enemyAttackPos[playerIndex].position;
    }

    public Vector3 GetAttackStagingPos(CharacterBase attacker, CharacterBase target)
    {
        if (target == null) return Vector3.zero;

        Transform frontPivot = target.transform.Find("Pivots/Front");
        if (frontPivot != null) return frontPivot.position;

        // 피벗이 없을 경우 수학적 계산으로 땜빵 (안전 장치)
        float direction = (attacker is PlayerCharacter) ? -1.0f : 1.0f; 
        return target.transform.position + new Vector3(direction * 1.2f, 0, 0);
    }
}