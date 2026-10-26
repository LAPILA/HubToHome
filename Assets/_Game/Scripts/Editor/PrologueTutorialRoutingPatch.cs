#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>기존 객차의 문·도착 지점·전용 대사만 수정합니다. 전체 콘텐츠 생성기는 실행하지 않습니다.</summary>
public static class PrologueTutorialRoutingPatch
{
    private const string IntroductionDirectionKey = "HTH_PROLOGUE_DLG_INTRODUCTION_05";
    private const string ConductorDirectionKey = "HTH_PROLOGUE_DLG_OPTIONAL_CONDUCTOR_01";

    public static void ApplyInEditor() => PrologueTutorialOpeningPatch.WithClosedSceneSetup(Apply);

    public static void Apply()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating)
            throw new InvalidOperationException("Play 전환·컴파일·에셋 갱신 중에는 프롤로그 동선을 변경하지 않습니다.");
        PrologueTutorialContentBuilder.RequireClosedCleanTargets();
        if (!File.Exists(PrologueTutorialContentBuilder.ManifestPath))
            throw new InvalidOperationException("생성 기록이 없는 프롤로그에는 좁은 동선 패치를 적용하지 않습니다.");
        List<string> previous = PrologueTutorialContentBuilder.ValidatePreviousManifest();
        PrologueTutorialOpeningPatch.RequireNoDirtyAssets(previous);
        PrologueTutorialAssetFactory.ValidateLocalization();
        PrologueTutorialDefinition definition = PrologueTutorialContentBuilder.Require<PrologueTutorialDefinition>(PrologueTutorialContentBuilder.DefinitionPath);
        Dictionary<PrologueTutorialRoom, string> rooms = ValidateTargets(definition);

        // 모든 해시·dirty·도착 ID·문 구조 검사가 첫 쓰기보다 먼저 끝나야 합니다.
        PrologueTutorialContentBuilder.WrittenPaths.Clear();
        PrologueTutorialContentBuilder.WrittenPaths.AddRange(previous);
        Exception patchFailure = null;
        try
        {
            foreach (KeyValuePair<PrologueTutorialRoom, string> room in rooms)
                if (room.Key != PrologueTutorialRoom.Completed) PatchRoom(room.Key, room.Value);
            foreach (PrologueTutorialDialogueBinding binding in definition.Dialogues)
            {
                bool changed = !binding.Dialogue.RevealTextImmediately;
                binding.Dialogue.RevealTextImmediately = true;
                foreach (DialogueNode node in binding.Dialogue.Nodes)
                    if (node.LocalizationKey == IntroductionDirectionKey || node.LocalizationKey == ConductorDirectionKey)
                    {
                        string text = PrologueTutorialAssetFactory.Korean(node.LocalizationKey);
                        changed |= !string.Equals(node.DefaultText, text, StringComparison.Ordinal);
                        node.DefaultText = text;
                    }
                if (changed) PrologueTutorialContentBuilder.Save(binding.Dialogue);
            }
            bool definitionChanged = false;
            foreach (PrologueTutorialResumePoint point in definition.ResumePoints)
                if (point.CompletedFlag == PrologueTutorialProgress.SealCompleted)
                {
                    definitionChanged |= point.Room != PrologueTutorialRoom.Rear || point.SpawnPointId != "after_withdrawal";
                    point.Room = PrologueTutorialRoom.Rear;
                    point.SpawnPointId = "after_withdrawal";
                }
            if (definitionChanged) PrologueTutorialContentBuilder.Save(definition);
        }
        catch (Exception exception)
        {
            patchFailure = exception;
            throw;
        }
        finally
        {
            try { PrologueTutorialContentBuilder.WriteManifest(); }
            catch (Exception manifestFailure)
            {
                if (patchFailure == null) throw;
                Debug.LogError("[PrologueTutorialRoutingPatch] 부분 수정 기록 저장 실패: " + manifestFailure.Message);
            }
        }
        Debug.Log("[PrologueTutorialRoutingPatch] 오른쪽 객차 동선·안전 재개·전용 대사 즉시 표시 적용 완료");
    }

    private static Dictionary<PrologueTutorialRoom, string> ValidateTargets(PrologueTutorialDefinition definition)
    {
        if (definition.Rooms == null || definition.Rooms.Count != 5 || definition.Dialogues == null
            || definition.Dialogues.Count != 27 || definition.ResumePoints == null)
            throw new InvalidOperationException("기존 프롤로그 방5개/대사27개/재개 목록을 확인하지 못했습니다.");
        if (LayerMask.NameToLayer("Interactable") < 0)
            throw new InvalidOperationException("Interactable 레이어가 없습니다.");
        var rooms = new Dictionary<PrologueTutorialRoom, string>();
        var paths = new HashSet<string>(StringComparer.Ordinal);
        foreach (PrologueTutorialRoomBinding binding in definition.Rooms)
        {
            if (binding?.Definition?.RoomPrefab == null || !Enum.IsDefined(typeof(PrologueTutorialRoom), binding.Room))
                throw new InvalidOperationException("유효하지 않은 프롤로그 방 참조입니다.");
            string path = AssetDatabase.GetAssetPath(binding.Definition.RoomPrefab);
            if (!PrologueTutorialContentBuilder.IsOwnedPath(path) || !paths.Add(path) || rooms.ContainsKey(binding.Room)
                || ((binding.Room == PrologueTutorialRoom.Repair) != (path == PrologueTutorialContentBuilder.OriginalRoom)))
                throw new InvalidOperationException("중복되거나 소유 범위 밖인 객차입니다: " + path);
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try { ValidateRoom(root, binding.Room); }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            rooms.Add(binding.Room, path);
        }
        var ids = new HashSet<string>(StringComparer.Ordinal);
        var dialogues = new HashSet<DialogueData>();
        int introductionDirectionNodes = 0, conductorDirectionNodes = 0;
        foreach (PrologueTutorialDialogueBinding binding in definition.Dialogues)
        {
            if (binding?.Dialogue == null || string.IsNullOrWhiteSpace(binding.Id) || !ids.Add(binding.Id)
                || !dialogues.Add(binding.Dialogue) || binding.Dialogue.Nodes == null || binding.Dialogue.Nodes.Count == 0)
                throw new InvalidOperationException("중복되거나 누락된 전용 대사입니다.");
            string path = AssetDatabase.GetAssetPath(binding.Dialogue);
            if (!path.StartsWith(PrologueTutorialContentBuilder.Root + "/Dialogue/", StringComparison.Ordinal)
                || !PrologueTutorialContentBuilder.IsOwnedPath(path))
                throw new InvalidOperationException("공유/외부 대사 자산은 수정하지 않습니다: " + path);
            foreach (DialogueNode node in binding.Dialogue.Nodes)
            {
                if (node == null) throw new InvalidOperationException("빈 대사 노드가 있습니다: " + binding.Id);
                if (node.LocalizationKey == IntroductionDirectionKey) introductionDirectionNodes++;
                if (node.LocalizationKey == ConductorDirectionKey) conductorDirectionNodes++;
            }
        }
        if (introductionDirectionNodes != 1 || conductorDirectionNodes != 1)
            throw new InvalidOperationException("기관사 도입/반복 방향 안내는 각각 한 문장이어야 합니다.");
        PrologueTutorialAssetFactory.Korean(IntroductionDirectionKey);
        PrologueTutorialAssetFactory.Korean(ConductorDirectionKey);
        var checkpoints = new HashSet<string>(StringComparer.Ordinal);
        bool foundSeal = false;
        foreach (PrologueTutorialResumePoint point in definition.ResumePoints)
        {
            if (point == null || !checkpoints.Add(point.CompletedFlag ?? string.Empty) || !rooms.ContainsKey(point.Room))
                throw new InvalidOperationException("중복되거나 유효하지 않은 재개 지점입니다.");
            foundSeal |= point.CompletedFlag == PrologueTutorialProgress.SealCompleted;
        }
        if (!foundSeal) throw new InvalidOperationException("봉쇄 완료 재개 지점이 없습니다.");
        return rooms;
    }

    private static void ValidateRoom(GameObject root, PrologueTutorialRoom room)
    {
        if (root.GetComponent<RoomInstance>() == null) throw new InvalidOperationException("RoomInstance가 없습니다: " + room);
        Transform authored = root.transform.Find("Tutorial Authored");
        if (authored == null) throw new InvalidOperationException("프롤로그 작성 범위가 없습니다: " + room);
        PrologueTutorialDoor[] doors = root.GetComponentsInChildren<PrologueTutorialDoor>(true);
        int expected = room == PrologueTutorialRoom.Completed ? 0
            : room == PrologueTutorialRoom.Common || room == PrologueTutorialRoom.Rear ? 2 : 1;
        bool previousLayout = (room == PrologueTutorialRoom.Repair && doors.Length == 2)
            || (room == PrologueTutorialRoom.Rear && doors.Length == 1);
        if (doors.Length != expected && !previousLayout)
            throw new InvalidOperationException("예상하지 못한 객차 문 개수입니다: " + room + "/" + doors.Length);
        foreach (PrologueTutorialDoor door in doors)
            if (door.transform.parent != authored || door.GetComponent<BoxCollider2D>() == null
                || !door.GetComponent<BoxCollider2D>().isTrigger)
                throw new InvalidOperationException("작성 범위 밖이거나 유효하지 않은 객차 문입니다: " + room);
        switch (room)
        {
            case PrologueTutorialRoom.Repair:
                FindSpawn(root, "tutorial_repair"); FindSpawn(root, "tutorial_rest"); FindSpawn(root, "tutorial_forward");
                break;
            case PrologueTutorialRoom.Common: FindSpawn(root, "after_incident"); break;
            case PrologueTutorialRoom.Rear: FindSpawn(root, "after_withdrawal"); break;
        }
        FindSpawn(root, "default"); FindSpawn(root, "from_left"); FindSpawn(root, "from_right");
    }

    private static void PatchRoom(PrologueTutorialRoom room, string path)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            Transform authored = root.transform.Find("Tutorial Authored");
            PrologueTutorialDoor[] doors = root.GetComponentsInChildren<PrologueTutorialDoor>(true);
            Array.Sort(doors, (left, right) => left.transform.localPosition.x.CompareTo(right.transform.localPosition.x));
            bool changed = false;
            switch (room)
            {
                case PrologueTutorialRoom.Repair:
                    changed |= ConfigureDoor(doors[doors.Length - 1], 7.35f, PrologueTutorialRoom.Common, "from_left");
                    if (doors.Length == 2) { Object.DestroyImmediate(doors[0].gameObject); changed = true; }
                    changed |= SetSpawn(root, "tutorial_repair", -6f);
                    changed |= SetSpawn(root, "tutorial_rest", 5.5f);
                    changed |= SetSpawn(root, "tutorial_forward", 5.5f);
                    break;
                case PrologueTutorialRoom.Common:
                    changed |= ConfigureDoor(doors[0], -7.35f, PrologueTutorialRoom.Repair, "from_right");
                    changed |= ConfigureDoor(doors[1], 7.35f, PrologueTutorialRoom.Rear, "default");
                    changed |= SetSpawn(root, "default", -6f);
                    changed |= SetSpawn(root, "after_incident", -6f);
                    break;
                case PrologueTutorialRoom.Rear:
                    changed |= ConfigureDoor(doors[0], -7.35f, PrologueTutorialRoom.Common, "from_right");
                    PrologueTutorialDoor forward = doors.Length == 2 ? doors[1] : AddDoor(authored);
                    changed |= ConfigureDoor(forward, 7.35f, PrologueTutorialRoom.Control, "default");
                    changed |= SetSpawn(root, "default", -6f);
                    changed |= SetSpawn(root, "after_withdrawal", -6f);
                    break;
                case PrologueTutorialRoom.Control:
                    changed |= ConfigureDoor(doors[0], -7.35f, PrologueTutorialRoom.Rear, "from_right");
                    changed |= SetSpawn(root, "default", -6f);
                    break;
            }
            if (!changed) return;
            PrologueTutorialContentBuilder.Track(path);
            if (PrefabUtility.SaveAsPrefabAsset(root, path) == null)
                throw new IOException("객차 동선 저장 실패: " + path);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    private static PrologueTutorialDoor AddDoor(Transform parent)
    {
        var root = new GameObject("To Control");
        root.transform.SetParent(parent, false);
        root.layer = LayerMask.NameToLayer("Interactable");
        BoxCollider2D collider = root.AddComponent<BoxCollider2D>();
        collider.isTrigger = true;
        collider.size = new Vector2(.8f, 1.5f);
        return root.AddComponent<PrologueTutorialDoor>();
    }

    private static bool ConfigureDoor(PrologueTutorialDoor door, float x, PrologueTutorialRoom target, string spawn)
    {
        var serialized = new SerializedObject(door);
        Vector3 position = new Vector3(x, -.5f, 0f);
        if (door.name == "To " + target && door.transform.localPosition.Equals(position)
            && serialized.FindProperty("_targetRoom").intValue == (int)target
            && serialized.FindProperty("_spawnPointId").stringValue == spawn
            && serialized.FindProperty("_enterOnTrigger").boolValue) return false;
        door.name = "To " + target;
        door.transform.localPosition = position;
        door.Configure(target, spawn, true);
        return true;
    }

    private static SpawnPoint FindSpawn(GameObject root, string id)
    {
        SpawnPoint result = null;
        foreach (SpawnPoint point in root.GetComponentsInChildren<SpawnPoint>(true))
            if (point.SpawnPointId == id)
            {
                if (result != null) throw new InvalidOperationException("중복된 객차 도착 ID: " + id);
                result = point;
            }
        return result != null ? result : throw new InvalidOperationException("객차 도착 ID가 없습니다: " + id);
    }

    private static bool SetSpawn(GameObject root, string id, float x)
    {
        SpawnPoint point = FindSpawn(root, id);
        Vector3 position = new Vector3(x, -.5f, 0f);
        if (point.transform.localPosition.Equals(position) && point.DefaultFacing == FacingDirection.Right) return false;
        point.transform.localPosition = position;
        PrologueTutorialContentBuilder.Set(point, "_defaultFacing", property => property.enumValueIndex = (int)FacingDirection.Right);
        return true;
    }
}
#endif
