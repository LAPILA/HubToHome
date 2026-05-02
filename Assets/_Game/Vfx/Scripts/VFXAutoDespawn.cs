using UnityEngine;

/// <summary>
/// 파티클 재생이 모두 끝나면 자동으로 Object Pool에 반납합니다.
/// Vefects 프리팹의 Stop Action은 'None'으로 설정하고 이 스크립트를 붙이세요.
/// </summary>
[RequireComponent(typeof(ParticleSystem))]
public class VFXAutoDespawn : MonoBehaviour
{
    private ParticleSystem _mainPS;

    private void Awake()
    {
        _mainPS = GetComponent<ParticleSystem>();
    }

    private void Update()
    {
        // IsAlive(true)는 자식 파티클까지 모두 재생 중인지 확인합니다.
        // 재생이 완전히 끝났다면 풀로 반납!
        if (_mainPS != null && !_mainPS.IsAlive(true))
        {
            ObjectPoolManager.Instance?.Despawn(gameObject);
        }
    }
}