using UnityEngine;
using Sirenix.OdinInspector;
using System.Collections.Generic;

/// <summary>
/// 전투 씬의 위치(Transform)를 제공하는 Service Locator.
/// 다중 파티원과 다수 적군의 위치를 유연하게 관리합니다.
/// </summary>
public class PositionManager : MonoBehaviour
{
    public static PositionManager Instance { get; private set; }

    [Title("Player Positions (Left Side)")]
    [InfoBox("파티원의 스폰 위치입니다. (순서대로 1p, 2p, 3p)")]
    [ListDrawerSettings(ShowIndexLabels = true)]
    [SerializeField] private List<Transform> _playerDefaultPos = new List<Transform>();

    [Title("Enemy Positions (Right Side)")]
    [InfoBox("적들의 스폰 위치입니다.")]
    [ListDrawerSettings(ShowIndexLabels = true)]
    [SerializeField] private List<Transform> _enemyDefaultPos = new List<Transform>();

    [Title("Action & Staging Positions")]
    [Tooltip("광역기 연출이나 보스 등장 시 사용되는 화면 중앙 위치")]
    [SerializeField] private Transform _centerPos;

    [Tooltip("적이 근접 공격(MeleeClose)을 할 때 아군 코앞으로 달려오는 목표 위치")]
    [ListDrawerSettings(ShowIndexLabels = true)]
    [SerializeField] private List<Transform> _enemyAttackPos = new List<Transform>();

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        
        // 🚨 심리스 전투의 경우 Overworld에 배치되므로 DontDestroyOnLoad가 필요 없을 수 있습니다.
        // 만약 전투 전용 씬을 쓴다면 DontDestroy를 추가하거나 씬 종속 매니저로 둡니다.
    }

    // ── 🚨 안전한 리스트 접근 헬퍼 (Index Out of Range 원천 차단) ──
    private Vector3 GetSafePosition(List<Transform> list, int index, string listName)
    {
        if (list == null || list.Count == 0)
        {
            Debug.LogError($"[PositionManager] {listName} 리스트가 비어있습니다! Inspector를 확인하세요.");
            return Vector3.zero;
        }

        // 인덱스가 리스트 크기를 초과하면, 에러를 내지 않고 가장 마지막 위치를 반환
        int safeIndex = Mathf.Clamp(index, 0, list.Count - 1);

        if (list[safeIndex] == null)
        {
            Debug.LogWarning($"[PositionManager] {listName}[{safeIndex}] 의 Transform이 연결되지 않았습니다.");
            return Vector3.zero;
        }
        
        return list[safeIndex].position;
    }

    public Vector3 GetPlayerDefaultPos(int index) => GetSafePosition(_playerDefaultPos, index, "PlayerDefaultPos");
    public Vector3 GetEnemyDefaultPos(int index)  => GetSafePosition(_enemyDefaultPos, index, "EnemyDefaultPos");
    public Vector3 GetCenterPos()                 => _centerPos != null ? _centerPos.position : Vector3.zero;
    
    /// <summary>
    /// 적이 특정 플레이어를 공격하기 위해 달려올 때 멈출 위치를 반환합니다.
    /// </summary>
    public Vector3 GetEnemyAttackPos(int playerIndex)
    {
        // 전용 AttackPos가 세팅되어 있지 않다면, 플레이어의 기본 위치를 반환하여 오류를 방지합니다.
        if (_enemyAttackPos == null || _enemyAttackPos.Count == 0)
        {
            return GetPlayerDefaultPos(playerIndex);
        }
            
        return GetSafePosition(_enemyAttackPos, playerIndex, "EnemyAttackPos");
    }

    /// <summary>
    /// 공격자가 타겟에게 다가갈 때 정확한 충돌 직전(Pivots/Front) 위치를 계산합니다.
    /// </summary>
    public Vector3 GetAttackStagingPos(CharacterBase attacker, CharacterBase target)
    {
        if (target == null) return Vector3.zero;

        // 1. 타겟에게 Front 피벗이 명시적으로 세팅되어 있다면 그 위치를 우선 사용
        Transform frontPivot = target.transform.Find("Pivots/Front");
        if (frontPivot != null) return frontPivot.position;

        // 2. 피벗이 없을 경우 수학적 계산으로 땜빵 (거리를 살짝 띄움)
        // 아군이면 오른쪽(+1)으로 전진, 적군이면 왼쪽(-1)으로 전진
        float direction = (attacker is PlayerCharacter) ? -1.0f : 1.0f; 
        
        // 타겟의 중심점에서 X축으로 1.2유닛만큼 떨어진 곳을 타격 위치로 설정
        return target.transform.position + new Vector3(direction * 1.2f, 0, 0);
    }
}