using System;
using UnityEngine;

/// <summary>
/// Keeps exactly one committed Room prefab active in the current scene.
/// </summary>
public class RoomContainer : MonoBehaviour
{
    [SerializeField] private RoomDefinition _initialRoom;
    [SerializeField] private bool _loadInitialRoomOnStart;

    public RoomInstance CurrentRoom { get; private set; }
    public RoomDefinition CurrentDefinition { get; private set; }
    public RoomDefinition InitialRoom => _initialRoom;
    public bool LoadInitialRoomOnStart => _loadInitialRoomOnStart;

    private void Start()
    {
        if (_loadInitialRoomOnStart && _initialRoom != null)
            LoadRoom(_initialRoom, FindFirstObjectByType<PlayerController>());
    }

    public RoomInstance LoadRoom(RoomDefinition roomDefinition, PlayerController player)
    {
        if (!TryLoadRoom(roomDefinition, null, out RoomInstance room, out string error))
        {
            Debug.LogError("[RoomContainer] " + error, this);
            return null;
        }

        try
        {
            room.OnRoomEntered(player);
        }
        catch (Exception exception)
        {
            Debug.LogException(exception, room);
        }

        return room;
    }

    /// <summary>
    /// Instantiates and validates the candidate before replacing the committed Room.
    /// The validator runs while the candidate is inactive and may inspect inactive children.
    /// </summary>
    public bool TryLoadRoom(
        RoomDefinition roomDefinition,
        Func<RoomInstance, bool> candidateValidator,
        out RoomInstance room,
        out string error)
    {
        room = null;
        if (roomDefinition == null || !roomDefinition.IsValid)
        {
            error = "유효하지 않은 RoomDefinition입니다.";
            return false;
        }

        GameObject stagingObject = new GameObject("__RoomStaging");
        stagingObject.transform.SetParent(transform, false);
        stagingObject.SetActive(false);
        RoomInstance candidate = null;

        try
        {
            candidate = Instantiate(roomDefinition.RoomPrefab, stagingObject.transform, false);
            if (candidate == null)
            {
                error = "Room Prefab을 생성하지 못했습니다.";
                return false;
            }

            candidate.gameObject.name = roomDefinition.RoomId;
            if (candidateValidator != null && !candidateValidator(candidate))
            {
                error = "생성된 Room이 도착 조건을 충족하지 않습니다.";
                return false;
            }

            RoomInstance previousRoom = CurrentRoom;
            RoomDefinition previousDefinition = CurrentDefinition;
            if (previousRoom != null)
                previousRoom.gameObject.SetActive(false);

            try
            {
                CurrentRoom = candidate;
                CurrentDefinition = roomDefinition;
                candidate.transform.SetParent(transform, false);
                room = candidate;
                candidate = null;
            }
            catch
            {
                CurrentRoom = previousRoom;
                CurrentDefinition = previousDefinition;
                if (previousRoom != null)
                    previousRoom.gameObject.SetActive(true);
                throw;
            }

            if (previousRoom != null)
            {
                try
                {
                    previousRoom.OnRoomExited();
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception, previousRoom);
                }

                DestroyOwnedObject(previousRoom.gameObject);
            }

            error = string.Empty;
            return true;
        }
        catch (Exception exception)
        {
            error = "Room 교체 중 예외가 발생했습니다: " + exception.Message;
            Debug.LogException(exception, this);
            return false;
        }
        finally
        {
            if (candidate != null)
                DestroyOwnedObject(candidate.gameObject);
            DestroyOwnedObject(stagingObject);
        }
    }

    public void UnloadCurrentRoom()
    {
        RoomInstance room = CurrentRoom;
        CurrentRoom = null;
        CurrentDefinition = null;
        if (room == null)
            return;

        try
        {
            room.OnRoomExited();
        }
        catch (Exception exception)
        {
            Debug.LogException(exception, room);
        }

        DestroyOwnedObject(room.gameObject);
    }

    private static void DestroyOwnedObject(UnityEngine.Object target)
    {
        if (target == null)
            return;

        if (Application.isPlaying)
            Destroy(target);
        else
            DestroyImmediate(target);
    }
}