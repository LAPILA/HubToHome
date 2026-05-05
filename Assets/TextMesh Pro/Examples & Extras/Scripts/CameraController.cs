using UnityEngine;
using Unity.Cinemachine;
using DG.Tweening;
using Sirenix.OdinInspector;

/// <summary>
/// 전투 카메라 컨트롤러.
/// TargetGroup 대신 단일 Tracker 오브젝트를 부드럽게 이동시키며 줌인/아웃을 제어합니다.
/// </summary>
public class CameraController : MonoBehaviour
{
    public static CameraController Instance { get; private set; }

    [Title("🎥 컴포넌트 참조")]
    [SerializeField, Tooltip("시네마친 가상 카메라")] 
    private CinemachineCamera _vCam;
    
    [SerializeField, Tooltip("카메라 흔들림 소스")] 
    private CinemachineImpulseSource _impulseSource;
    
    [SerializeField, Tooltip("카메라가 따라다닐 투명한 추적자(빈 게임오브젝트)")] 
    private Transform _cameraTracker; 

    [Title("⚙️ 기본 설정")]
    [SerializeField] private float _defaultLensSize = 5.5f;
    [SerializeField] private float _battleZoomSize = 4.0f;
    
    private Vector3 _centerPosition; // 무대 중앙 (기본 위치)

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DOTween.SetTweensCapacity(500, 100);
    }

    private void Start()
    {
        // 씬 시작 시 트래커의 초기 위치를 중앙으로 기억합니다.
        if (_cameraTracker != null) _centerPosition = _cameraTracker.position;
    }

    // ─── [1. 림버스 스타일: 포커스 및 줌 제어] ───

    /// <summary>
    /// 아군 스킬 사용 시: 특정 캐릭터에게 부드럽게 이동하며 줌 인합니다.
    /// </summary>
    public void ZoomOnTransform(Transform target, float targetZoom, float duration = 0.3f)
    {
        if (_cameraTracker == null || target == null) return;

        DOTween.Kill("CameraMove");
        DOTween.Kill("CameraZoom");

        // 트래커를 타겟 위치로 부드럽게 이동
        _cameraTracker.DOMove(target.position, duration).SetEase(Ease.OutCubic).SetId("CameraMove");
        
        // 렌즈 줌인
        DOTween.To(() => _vCam.Lens.OrthographicSize, x => _vCam.Lens.OrthographicSize = x, targetZoom, duration)
            .SetEase(Ease.OutCubic)
            .SetId("CameraZoom");
    }

    /// <summary>
    /// 적 공격 시 / 턴 대기 시: 중앙으로 복귀하고 줌 아웃하여 시야를 확보합니다. (QTE 대비)
    /// </summary>
    [Button("🔄 카메라 완전 리셋")]
    public void ResetCamera(float duration = 0.4f)
    {
        if (_cameraTracker == null) return;

        DOTween.Kill("CameraMove");
        DOTween.Kill("CameraZoom");
        DOTween.Kill("HitStop");

        // 트래커를 중앙으로 복귀
        _cameraTracker.DOMove(_centerPosition, duration).SetEase(Ease.InOutQuad).SetId("CameraMove");

        // 줌 및 화면 비틀기 복구
        DOTween.To(() => _vCam.Lens.OrthographicSize, x => _vCam.Lens.OrthographicSize = x, _defaultLensSize, duration).SetId("CameraZoom");
        DOTween.To(() => _vCam.Lens.Dutch, x => _vCam.Lens.Dutch = x, 0, duration);

        Time.timeScale = 1f; // 혹시 멈춰있는 시간 복구
    }

    public void ModePlayerAction() => ZoomOnTransform(_cameraTracker, _battleZoomSize, 0.3f);
    public void ModeEnemyAction() => ResetCamera(0.3f); // 적 턴에는 무조건 시야 확보!


    // ─── [2. 타격 연출 (Slam & Impact)] ───

    /// <summary>
    /// 상황에 따라 방향과 강도를 조절할 수 있는 범용 타격감 기능
    /// </summary>
    /// <param name="direction">흔들릴 방향</param>
    /// <param name="intensity">강도 (적 공격 시에는 이 값을 낮게 전달)</param>
    /// <param name="lockHorizontal">Y축 무시 여부</param>
    public void PlayHeavySlam(Vector3 direction, float intensity = 1.0f, bool lockHorizontal = true)
    {
        Vector3 finalDir = lockHorizontal ? new Vector3(direction.x, 0, 0).normalized : new Vector3(direction.x, direction.y, 0).normalized;
        if (finalDir == Vector3.zero) finalDir = Vector3.right;

        // 1. Cinemachine Impulse를 통한 화면 지진 효과
        if (_impulseSource != null)
            _impulseSource.GenerateImpulse(finalDir * intensity);

        // 2. 화면 비틀기 (Dutch) - 큰 타격에만 살짝 적용
        _vCam.Lens.Dutch = 3f * (finalDir.x > 0 ? 1 : -1) * intensity;
        DOTween.To(() => _vCam.Lens.Dutch, x => _vCam.Lens.Dutch = x, 0, 0.3f);

        // 3. 힛스탑 (역경직)
        StopFrame(0.05f * intensity);
    }

    /// <summary>
    /// 플레이어의 돌진/관통 스킬 시 사용되는 특수 카메라 연출 (화면이 순간적으로 뒤로 확 당겨짐)
    /// </summary>
    public void PlayDashThroughImpact(float intensity = 1.0f)
    {
        float currentZoom = _vCam.Lens.OrthographicSize;
        
        // 순간적으로 줌 아웃 되었다가 다시 원래 줌으로 돌아옴 (고속 이동 느낌)
        DOTween.To(() => _vCam.Lens.OrthographicSize, x => _vCam.Lens.OrthographicSize = x, currentZoom + 0.8f, 0.1f)
               .SetEase(Ease.OutQuad)
               .OnComplete(() => ZoomOnTransform(_cameraTracker, currentZoom, 0.2f));

        if (_impulseSource != null)
            _impulseSource.GenerateImpulse(Vector3.right * intensity);

        StopFrame(0.06f);
    }


    // ─── [3. 유틸리티] ───

    private void StopFrame(float duration)
    {
        DOTween.Kill("HitStop");
        Time.timeScale = 0.01f; // 거의 정지 상태
        DOVirtual.DelayedCall(duration, () => Time.timeScale = 1f).SetUpdate(true).SetId("HitStop");
    }
}