using UnityEngine;

/// <summary>
/// 현재 씬 안에서 활성 룸 프리팹을 하나만 유지하는 컨테이너입니다.
/// </summary>
public class RoomContainer : MonoBehaviour
{
    [SerializeField] private RoomDefinition _initialRoom;
    [SerializeField] private bool _loadInitialRoomOnStart;

    public RoomInstance CurrentRoom { get; private set; }
    public RoomDefinition CurrentDefinition { get; private set; }

    private void Start()
    {
        if (_loadInitialRoomOnStart && _initialRoom != null)
            LoadRoom(_initialRoom, FindFirstObjectByType<PlayerController>());
    }

    public RoomInstance LoadRoom(RoomDefinition roomDefinition, PlayerController player)
    {
        if (roomDefinition == null || !roomDefinition.IsValid)
        {
            Debug.LogError("[RoomContainer] 유효하지 않은 RoomDefinition입니다.");
            return null;
        }

        UnloadCurrentRoom();

        CurrentDefinition = roomDefinition;
        CurrentRoom = Instantiate(roomDefinition.RoomPrefab, transform);
        CurrentRoom.gameObject.name = roomDefinition.RoomId;
        CurrentRoom.OnRoomEntered(player);

        return CurrentRoom;
    }

    public void UnloadCurrentRoom()
    {
        if (CurrentRoom == null) return;

        CurrentRoom.OnRoomExited();
        Destroy(CurrentRoom.gameObject);
        CurrentRoom = null;
        CurrentDefinition = null;
    }
}
