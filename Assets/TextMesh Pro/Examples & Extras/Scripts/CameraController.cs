using UnityEngine;
using Unity.Cinemachine;
using DG.Tweening;
using Sirenix.OdinInspector;

/// <summary>
/// 프로젝트 실사용 카메라 컨트롤러.
/// 현재 파일 경로는 TMP Examples 아래에 남아 있지만, 실제로는 전투/시나리오/오버월드가 함께 참조하는 게임 전용 스크립트다.
/// 씬과 프리팹이 현재 GUID를 직접 참조하므로, 이동이 필요할 때는 .meta 보존 전제로 안전하게 옮겨야 한다.
/// 지터링 방지를 위해 위치 이동은 Cinemachine의 Follow 타겟팅을 사용하며,
/// DOTween은 렌즈 줌(Zoom)과 이펙트 연출에만 사용한다.
/// </summary>
public class CameraController : MonoBehaviour
{
    public static CameraController Instance { get; private set; }
    private const string CameraZoomTweenId = "CameraZoom";
    private const string CameraImpactTweenId = "CameraImpact";
    private const string CameraDutchTweenId = "CameraDutch";
    private const string HitStopTweenId = "HitStop";

    [Title("🎥 컴포넌트 참조")]
    [SerializeField, Tooltip("시네마친 가상 카메라")] 
    private CinemachineCamera _vCam;
    
    [SerializeField, Tooltip("카메라 흔들림 소스 (Impulse)")] 
    private CinemachineImpulseSource _impulseSource;
    
    [SerializeField, Tooltip("전장 중앙을 나타내는 기본 타겟 오브젝트")] 
    private Transform _centerTarget; 

    [Title("⚙️ 기본 설정")]
    [SerializeField] private float _defaultLensSize = 5.5f;
    [SerializeField] private float _battleZoomSize = 4.0f;

    public CinemachineCamera VirtualCamera => _vCam;
    public Transform CenterTarget => _centerTarget;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    private void Start()
    {
        // 시작 시 카메라의 기본 타겟을 Center로 고정
        if (_centerTarget != null && _vCam != null)
        {
            _vCam.Follow = _centerTarget;
        }

        ResetCamera(0f);
    }

    // ─── [1. 시네머신 네이티브 포커스 및 줌 제어] ───

    /// <summary>
    /// 타겟을 변경합니다. 위치 이동은 시네머신 자체의 Damping이 부드럽게 처리합니다.
    /// </summary>
    public void SetTarget(Transform newTarget)
    {
        if (_vCam == null || newTarget == null) return;
        
        // 🚨 DOTween 이동을 삭제하고, 시네머신의 추적 대상을 직접 갈아끼움 (지터링 완벽 해결)
        _vCam.Follow = newTarget;
        SnapVirtualCameraToTarget(newTarget);
    }

    /// <summary>
    /// 아군 스킬 사용 시: 특정 캐릭터를 타겟으로 잡고 줌 인합니다.
    /// </summary>
    public void ZoomOnTransform(Transform target, float targetZoom, float duration = 0.3f)
    {
        if (_vCam == null || target == null) return;

        SetTarget(target);
        SnapVirtualCameraToTarget(target);

        DOTween.Kill(CameraZoomTweenId);
        DOTween.Kill(CameraImpactTweenId);
        DOTween.To(() => _vCam.Lens.OrthographicSize, x => _vCam.Lens.OrthographicSize = x, targetZoom, duration)
            .SetEase(Ease.OutCubic)
            .SetUpdate(UpdateType.Late) 
            .SetId(CameraZoomTweenId);
    }

    /// <summary>
    /// 적 공격 시 / 턴 대기 시: 중앙으로 타겟을 복귀하고 줌 아웃하여 시야를 확보합니다.
    /// </summary>
    [Button("🔄 카메라 완전 리셋")]
    public void ResetCamera(float duration = 0.4f)
    {
        if (_vCam == null) return;

        // 중앙으로 타겟 복귀
        if (_centerTarget != null) SetTarget(_centerTarget);

        DOTween.Kill(CameraZoomTweenId);
        DOTween.Kill(CameraImpactTweenId);
        DOTween.Kill(CameraDutchTweenId);
        DOTween.Kill(HitStopTweenId);

        // 줌 및 화면 비틀기 복구 (LateUpdate 동기화)
        if (duration <= 0f)
        {
            _vCam.Lens.OrthographicSize = _defaultLensSize;
            _vCam.Lens.Dutch = 0f;
        }
        else
        {
            DOTween.To(() => _vCam.Lens.OrthographicSize, x => _vCam.Lens.OrthographicSize = x, _defaultLensSize, duration)
                .SetEase(Ease.InOutQuad)
                .SetUpdate(UpdateType.Late)
                .SetId(CameraZoomTweenId);
                
            DOTween.To(() => _vCam.Lens.Dutch, x => _vCam.Lens.Dutch = x, 0, duration)
                .SetUpdate(UpdateType.Late)
                .SetId(CameraDutchTweenId);
        }

        Time.timeScale = 1f; 
    }

    public void ModePlayerAction(Transform playerTarget = null) 
    {
        // 타겟이 없으면 중앙을 줌인, 있으면 플레이어를 줌인
        ZoomOnTransform(playerTarget != null ? playerTarget : _centerTarget, _battleZoomSize, 0.3f);
    }
    
    public void ModeEnemyAction() => ResetCamera(0.3f); 

    private void SnapVirtualCameraToTarget(Transform target)
    {
        if (_vCam == null || target == null)
        {
            return;
        }

        Transform cameraTransform = _vCam.transform;
        Vector3 currentPosition = cameraTransform.position;
        cameraTransform.position = new Vector3(target.position.x, target.position.y, currentPosition.z);
    }


    // ─── [2. 타격 연출 (Slam & Impact)] ───

    public void PlayHeavySlam(Vector3 direction, float intensity = 1.0f, bool lockHorizontal = true)
    {
        if (_vCam == null) return;
        Vector3 finalDir = lockHorizontal ? new Vector3(direction.x, 0, 0).normalized : new Vector3(direction.x, direction.y, 0).normalized;
        if (finalDir == Vector3.zero) finalDir = Vector3.right;

        if (_impulseSource != null)
            _impulseSource.GenerateImpulse(finalDir * intensity);

        _vCam.Lens.Dutch = 3f * (finalDir.x > 0 ? 1 : -1) * intensity;
        DOTween.Kill(CameraDutchTweenId);
        DOTween.To(() => _vCam.Lens.Dutch, x => _vCam.Lens.Dutch = x, 0, 0.3f).SetUpdate(UpdateType.Late).SetId(CameraDutchTweenId);

        StopFrame(0.05f * intensity);
    }

    public void PlayDashThroughImpact(float intensity = 1.0f)
    {
        if (_vCam == null) return;
        float impactZoom = _defaultLensSize + 0.8f;

        DOTween.Kill(CameraZoomTweenId);
        DOTween.Kill(CameraImpactTweenId);
        DOTween.To(() => _vCam.Lens.OrthographicSize, x => _vCam.Lens.OrthographicSize = x, impactZoom, 0.1f)
               .SetEase(Ease.OutQuad)
               .SetUpdate(UpdateType.Late)
               .SetId(CameraImpactTweenId)
               .OnComplete(() =>
               {
                   DOTween.To(() => _vCam.Lens.OrthographicSize, x => _vCam.Lens.OrthographicSize = x, _defaultLensSize, 0.2f)
                       .SetEase(Ease.OutQuad)
                       .SetUpdate(UpdateType.Late)
                       .SetId(CameraZoomTweenId);
               });

        if (_impulseSource != null)
            _impulseSource.GenerateImpulse(Vector3.right * intensity);

        StopFrame(0.06f);
    }

    // ─── [3. 유틸리티] ───

    private void StopFrame(float duration)
    {
        DOTween.Kill(HitStopTweenId);
        Time.timeScale = 0.01f;
        DOVirtual.DelayedCall(duration, () => Time.timeScale = 1f).SetUpdate(true).SetId(HitStopTweenId);
    }
}
