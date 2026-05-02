using UnityEngine;
using Unity.Cinemachine;
using DG.Tweening;
using Sirenix.OdinInspector;

public class CameraController : MonoBehaviour
{
    public static CameraController Instance { get; private set; }

    [Title("🎥 컴포넌트 참조")]
    [SerializeField] private CinemachineCamera _vCam;
    [SerializeField] private CinemachineTargetGroup _targetGroup;
    [SerializeField] private CinemachineImpulseSource _impulseSource;

    [Title("⚙️ 기본 설정")]
    [SerializeField] private float _defaultLensSize = 5.5f;
    [SerializeField] private float _battleZoomSize = 4.8f;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DOTween.SetTweensCapacity(500, 100);
    }

    // ─── [1. 안전한 포커스 조절] ───

    /// <summary>
    /// IndexOutOfRangeException 방지를 위해 배열 직접 참조 대신 
    /// 전용 가중치 트위닝 로직을 사용합니다.
    /// </summary>
    public void SetFocusWeight(float playerWeight, float enemyWeight, float duration = 0.5f)
    {
        DOTween.To(() => 0f, x => {
            if (_targetGroup.m_Targets.Length >= 2) {
                _targetGroup.m_Targets[0].Weight = Mathf.Lerp(_targetGroup.m_Targets[0].Weight, playerWeight, x);
                _targetGroup.m_Targets[1].Weight = Mathf.Lerp(_targetGroup.m_Targets[1].Weight, enemyWeight, x);
            }
        }, 1f, duration).SetEase(Ease.OutQuad).SetId("CameraWeight");
    }

    [Button] public void ModeBattleIdle() => SetFocusWeight(1f, 1f);
    [Button] public void ModePlayerAction() => SetFocusWeight(1.5f, 0.5f, 0.4f);
    [Button] public void ModeEnemyAction() => SetFocusWeight(0.5f, 1.5f, 0.4f);


    // ─── [2. 타격 연출 (Slam & Impact)] ───

    public void PlayHitImpact(float intensity = 1f)
    {
        _vCam.Lens.OrthographicSize = _battleZoomSize - (0.4f * intensity);
        DOTween.To(() => _vCam.Lens.OrthographicSize, x => _vCam.Lens.OrthographicSize = x, _battleZoomSize, 0.2f);
        
        _impulseSource.GenerateImpulse(intensity * 0.8f);
        StopFrame(0.04f * intensity);
    }

    
    /// <summary>
    /// 상황에 따라 방향과 강도를 조절할 수 있는 범용 슬램 기능
    /// </summary>
    /// <param name="direction">흔들릴 방향 (Vector3.right, Vector3.up 등)</param>
    /// <param name="intensity">강도 (기본 1.0)</param>
    /// <param name="lockHorizontal">true면 Y축을 무시하고 좌우로만 흔듭니다 (일반 전투용)</param>
    public void PlayHeavySlam(Vector3 direction, float intensity = 1.0f, bool lockHorizontal = true)
    {
        transform.DOKill();

        Vector3 finalDir = lockHorizontal ? new Vector3(direction.x, 0, 0).normalized : new Vector3(direction.x, direction.y, 0).normalized;
        if (finalDir == Vector3.zero) finalDir = Vector3.right;

        transform.DOPunchPosition(finalDir * (intensity * 0.4f), 0.3f, 10, 0.5f);

        _vCam.Lens.Dutch = 3f * (finalDir.x > 0 ? 1 : -1) * intensity;
        DOTween.To(() => _vCam.Lens.Dutch, x => _vCam.Lens.Dutch = x, 0, 0.3f);

        _impulseSource.GenerateImpulse(finalDir * intensity);
        StopFrame(0.05f * intensity);
    }

    public void PlayDashThroughImpact(Vector3 dashDir)
    {
        transform.DOKill();
        transform.DOPunchPosition(dashDir * 1.2f, 0.4f, 10, 0.5f);

        float originalZoom = _vCam.Lens.OrthographicSize;
        DOTween.To(() => _vCam.Lens.OrthographicSize, x => _vCam.Lens.OrthographicSize = x, originalZoom + 0.6f, 0.1f)
               .OnComplete(() => Zoom(originalZoom, 0.2f));

        _impulseSource.GenerateImpulse(1.2f);
        StopFrame(0.06f);
    }


    // ─── [3. 유틸리티] ───

    [Button("🔄 카메라 완전 리셋")]
    public void ResetCamera(float duration = 0.5f)
    {
        DOTween.Kill("CameraWeight");
        DOTween.Kill("HitStop");
        transform.DOKill();

        // 줌 및 비틀기 복구
        DOTween.To(() => _vCam.Lens.OrthographicSize, x => _vCam.Lens.OrthographicSize = x, _defaultLensSize, duration);
        DOTween.To(() => _vCam.Lens.Dutch, x => _vCam.Lens.Dutch = x, 0, duration);

        transform.DOLocalMove(new Vector3(0, 0, -10), duration);
        
        SetFocusWeight(1f, 1f, duration);
    }

    public void Zoom(float size, float duration)
    {
        DOTween.To(() => _vCam.Lens.OrthographicSize, x => _vCam.Lens.OrthographicSize = x, size, duration);
    }

    private void StopFrame(float duration)
    {
        DOTween.Kill("HitStop");
        Time.timeScale = 0.01f;
        DOVirtual.DelayedCall(duration, () => Time.timeScale = 1f).SetUpdate(true).SetId("HitStop");
    }
}