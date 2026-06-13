using UnityEngine;
using Unity.Cinemachine;

/// <summary>
/// 룸 프리팹 루트에 붙는 런타임 설정 컴포넌트입니다.
/// 타일맵, 충돌, NPC, 문, 이벤트 트리거는 이 오브젝트 하위에 배치합니다.
/// </summary>
public class RoomInstance : MonoBehaviour
{
    [SerializeField] private string _roomId;
    [SerializeField] private PolygonCollider2D _cameraBounds;

    public string RoomId => _roomId;
    public PolygonCollider2D CameraBounds => _cameraBounds;

    public void OnRoomEntered(PlayerController player)
    {
        ConfigureCamera(player);
    }

    public void OnRoomExited()
    {
    }

    public void ConfigureCamera(PlayerController player)
    {
        CinemachineCamera vCam = FindFirstObjectByType<CinemachineCamera>();
        if (vCam == null) return;

        if (player != null)
            vCam.Follow = player.transform;

        CinemachineConfiner2D confiner = vCam.GetComponent<CinemachineConfiner2D>();
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
