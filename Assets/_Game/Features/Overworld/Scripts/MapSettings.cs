using UnityEngine;
using Unity.Cinemachine;

/// <summary>
/// 오버월드 씬마다 하나씩 배치되어 맵의 환경(BGM, 카메라 바운더리)을 세팅합니다.
/// </summary>
public class MapSettings : MonoBehaviour
{
    [Header("오디오 세팅")]
    [Tooltip("이 맵에 들어오면 재생할 BGM 에셋을 드래그하세요. (비워두면 이전 BGM이 그대로 유지됩니다)")]
    [SerializeField] private AudioClip _mapBGM; // 🚨 string에서 AudioClip으로 변경!

    [Header("카메라 세팅")]
    [Tooltip("카메라가 밖으로 나가지 못하게 막을 투명한 맵 테두리 (Polygon Collider 2D)")]
    [SerializeField] private PolygonCollider2D _cameraBounds;

    private void Start()
    {
        // 1. 맵 입장 시 BGM 재생 (드래그해둔 에셋이 있다면)
        if (_mapBGM != null)
        {
            AudioManager.Instance?.CrossFadeBGM(_mapBGM, 1.5f);
            Debug.Log($"<color=cyan>[MapSettings]</color> 🎵 맵 BGM 세팅: {_mapBGM.name}");
        }

        // 2. 카메라 세팅 
        Invoke(nameof(SetupCamera), 0.1f);
    }

    private void SetupCamera()
    {
        var vCam = FindFirstObjectByType<CinemachineCamera>();
        if (vCam == null) return;

        var player = FindFirstObjectByType<PlayerController>();
        if (player != null)
        {
            vCam.Follow = player.transform;
            Debug.Log($"<color=cyan>[MapSettings]</color> 🎥 카메라 타겟 설정 완료: {player.gameObject.name}");
        }

        var confiner = vCam.GetComponent<CinemachineConfiner2D>();
        
        if (_cameraBounds != null)
        {
            if (confiner == null) confiner = vCam.gameObject.AddComponent<CinemachineConfiner2D>();
            
            confiner.enabled = true;
            confiner.BoundingShape2D = _cameraBounds;
            confiner.InvalidateBoundingShapeCache();
        }
        else if (confiner != null)
        {
            confiner.enabled = false;
        }
    }
}