using UnityEngine;

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

    public bool OnRoomEntered(PlayerController player)
    {
        return ConfigureCamera(player);
    }

    public void OnRoomExited()
    {
    }

    public bool ConfigureCamera(PlayerController player)
    {
        return OverworldCameraBinding.TryApply(player, _cameraBounds, this);
    }
}
