using UnityEditor;
using UnityEngine;

public static class AreaMarkerPrefabGenerator
{
    private const string PrefabFolder = "Assets/_Game/Content/Maps/Shared/Markers";
    private const string SampleFolder = DevelopmentContentPaths.SharedMarkerSamplesRoot;
    private const string SampleRoomPath = SampleFolder + "/Room_AreaMarker_AllGizmos.prefab";
    private const string InteractableLayerName = "Interactable";

    [MenuItem("HubToHome/오버월드/Area 마커/마커 Prefab 생성")]
    public static void GenerateMarkerPrefabs()
    {
        EnsureFolder(PrefabFolder);

        foreach (AreaMarkerType type in System.Enum.GetValues(typeof(AreaMarkerType)))
        {
            CreatePrefab(type);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }

    [MenuItem("HubToHome/오버월드/Area 마커/선택 위치에 마커 생성/Connection")]
    private static void CreateConnection() => CreateSceneMarker(AreaMarkerType.Connection);
    [MenuItem("HubToHome/오버월드/Area 마커/선택 위치에 마커 생성/Enemy")]
    private static void CreateEnemy() => CreateSceneMarker(AreaMarkerType.Enemy);
    [MenuItem("HubToHome/오버월드/Area 마커/선택 위치에 마커 생성/Hazard")]
    private static void CreateHazard() => CreateSceneMarker(AreaMarkerType.Hazard);
    [MenuItem("HubToHome/오버월드/Area 마커/선택 위치에 마커 생성/Puzzle")]
    private static void CreatePuzzle() => CreateSceneMarker(AreaMarkerType.Puzzle);
    [MenuItem("HubToHome/오버월드/Area 마커/선택 위치에 마커 생성/Vendor")]
    private static void CreateVendor() => CreateSceneMarker(AreaMarkerType.Vendor);
    [MenuItem("HubToHome/오버월드/Area 마커/선택 위치에 마커 생성/Shortcut Door")]
    private static void CreateShortcutDoor() => CreateSceneMarker(AreaMarkerType.ShortcutDoor);
    [MenuItem("HubToHome/오버월드/Area 마커/선택 위치에 마커 생성/NPC")]
    private static void CreateNpc() => CreateSceneMarker(AreaMarkerType.NPC);
    [MenuItem("HubToHome/오버월드/Area 마커/선택 위치에 마커 생성/Item")]
    private static void CreateItem() => CreateSceneMarker(AreaMarkerType.Item);
    [MenuItem("HubToHome/오버월드/Area 마커/선택 위치에 마커 생성/Sign")]
    private static void CreateSign() => CreateSceneMarker(AreaMarkerType.Sign);
    [MenuItem("HubToHome/오버월드/Area 마커/선택 위치에 마커 생성/Save Point")]
    private static void CreateSavePoint() => CreateSceneMarker(AreaMarkerType.SavePoint);
    [MenuItem("HubToHome/오버월드/Area 마커/선택 위치에 마커 생성/Plot Point")]
    private static void CreatePlotPoint() => CreateSceneMarker(AreaMarkerType.PlotPoint);
    [MenuItem("HubToHome/오버월드/Area 마커/선택 위치에 마커 생성/Sublocation")]
    private static void CreateSublocation() => CreateSceneMarker(AreaMarkerType.Sublocation);

    [MenuItem("HubToHome/오버월드/Area 마커/샘플/전체 마커 Room Prefab 생성")]
    public static void GenerateAllMarkerSampleRoomPrefab()
    {
        EnsureFolder(SampleFolder);

        GameObject room = new GameObject("Room_AreaMarker_AllGizmos");

        AreaMarkerType[] types =
        {
            AreaMarkerType.Connection,
            AreaMarkerType.Enemy,
            AreaMarkerType.Hazard,
            AreaMarkerType.Puzzle,
            AreaMarkerType.Vendor,
            AreaMarkerType.ShortcutDoor,
            AreaMarkerType.NPC,
            AreaMarkerType.Item,
            AreaMarkerType.Sign,
            AreaMarkerType.SavePoint,
            AreaMarkerType.PlotPoint,
            AreaMarkerType.Sublocation
        };

        Vector2[] positions =
        {
            new Vector2(-6f, 2.5f),
            new Vector2(-3.5f, 2.5f),
            new Vector2(-1f, 2.5f),
            new Vector2(1.5f, 2.5f),
            new Vector2(4f, 2.5f),
            new Vector2(6.5f, 2.5f),
            new Vector2(-6f, -1f),
            new Vector2(-3.5f, -1f),
            new Vector2(-1f, -1f),
            new Vector2(1.5f, -1f),
            new Vector2(4f, -1f),
            new Vector2(6.5f, -1f)
        };

        for (int i = 0; i < types.Length; i++)
        {
            AreaMarkerType type = types[i];
            GameObject markerObject = new GameObject(GetPrefabName(type));
            markerObject.transform.SetParent(room.transform, false);
            markerObject.transform.localPosition = positions[i];
            ApplyInteractableLayer(markerObject);
            EnsureMarkerCollider(markerObject);

            AreaMarkerBase marker = AddMarkerComponent(markerObject, type);
            if (marker == null)
            {
                Object.DestroyImmediate(room);
                Debug.LogError($"[AreaMarkerPrefabGenerator] 샘플 Room 생성 실패: type={type}");
                return;
            }

            ConfigureSampleMarker(marker, type);
        }

        GameObject notes = new GameObject("README_Runtime_Test_Notes");
        notes.transform.SetParent(room.transform, false);
        notes.transform.localPosition = new Vector3(0f, -3f, 0f);

        PrefabUtility.SaveAsPrefabAsset(room, SampleRoomPath);
        Object.DestroyImmediate(room);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[AreaMarkerPrefabGenerator] Sample room generated: {SampleRoomPath}");
    }

    private static void CreateSceneMarker(AreaMarkerType type)
    {
        GameObject go = new GameObject(GetPrefabName(type));
        ApplyInteractableLayer(go);
        EnsureMarkerCollider(go);
        AreaMarkerBase marker = AddMarkerComponent(go, type);
        if (marker == null)
        {
            Object.DestroyImmediate(go);
            Debug.LogError($"[AreaMarkerPrefabGenerator] Scene Marker 생성 실패: type={type}");
            return;
        }

        if (Selection.activeTransform != null)
            go.transform.SetParent(Selection.activeTransform, false);

        SceneView.lastActiveSceneView?.MoveToView(go.transform);
        Selection.activeObject = go;
        Undo.RegisterCreatedObjectUndo(go, $"Create {type} Marker");
    }

    private static void CreatePrefab(AreaMarkerType type)
    {
        string prefabName = GetPrefabName(type);
        string path = $"{PrefabFolder}/{prefabName}.prefab";
        if (AssetDatabase.LoadAssetAtPath<GameObject>(path) != null)
            Debug.Log($"[AreaMarkerPrefabGenerator] 기존 Prefab을 덮어씁니다: {path}");

        GameObject go = new GameObject(prefabName);
        ApplyInteractableLayer(go);
        EnsureMarkerCollider(go);
        AreaMarkerBase marker = AddMarkerComponent(go, type);
        if (marker == null)
        {
            Object.DestroyImmediate(go);
            Debug.LogError($"[AreaMarkerPrefabGenerator] Prefab 생성 실패: type={type}, path={path}");
            return;
        }

        PrefabUtility.SaveAsPrefabAsset(go, path);
        Object.DestroyImmediate(go);
        Debug.Log($"[AreaMarkerPrefabGenerator] Prefab generated: {path}");
    }

    private static void ApplyInteractableLayer(GameObject go)
    {
        int interactableLayer = LayerMask.NameToLayer(InteractableLayerName);
        if (interactableLayer >= 0)
            go.layer = interactableLayer;
        else
            Debug.LogWarning($"[AreaMarkerPrefabGenerator] '{InteractableLayerName}' 레이어가 없습니다. InteractionSystem LayerMask 설정을 확인하세요.");
    }

    private static void EnsureFolder(string folder)
    {
        string[] parts = folder.Split('/');
        string current = parts[0];
        for (int i = 1; i < parts.Length; i++)
        {
            string next = current + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(next))
                AssetDatabase.CreateFolder(current, parts[i]);
            current = next;
        }
    }

    private static CircleCollider2D EnsureMarkerCollider(GameObject go)
    {
        CircleCollider2D collider = go.GetComponent<CircleCollider2D>();
        if (collider == null)
            collider = go.AddComponent<CircleCollider2D>();

        collider.isTrigger = true;
        collider.radius = 0.35f;
        return collider;
    }

    private static AreaMarkerBase AddMarkerComponent(GameObject go, AreaMarkerType type)
    {
        AreaMarkerBase marker;
        switch (type)
        {
            case AreaMarkerType.Connection: marker = go.AddComponent<AreaConnectionMarker>(); break;
            case AreaMarkerType.Enemy: marker = go.AddComponent<OverworldEnemyMarker>(); break;
            case AreaMarkerType.Hazard: marker = go.AddComponent<HazardMarker>(); break;
            case AreaMarkerType.Puzzle: marker = go.AddComponent<PuzzleMarker>(); break;
            case AreaMarkerType.Vendor: marker = go.AddComponent<VendorMarker>(); break;
            case AreaMarkerType.ShortcutDoor: marker = go.AddComponent<ShortcutDoorMarker>(); break;
            case AreaMarkerType.NPC: marker = go.AddComponent<NPCMarker>(); break;
            case AreaMarkerType.Item: marker = go.AddComponent<ItemPickupMarker>(); break;
            case AreaMarkerType.Sign: marker = go.AddComponent<SignMarker>(); break;
            case AreaMarkerType.SavePoint: marker = go.AddComponent<SavePointMarker>(); break;
            case AreaMarkerType.PlotPoint: marker = go.AddComponent<PlotPointMarker>(); break;
            case AreaMarkerType.Sublocation: marker = go.AddComponent<SublocationMarker>(); break;
            default: marker = go.AddComponent<PlotPointMarker>(); break;
        }

        if (marker == null) return null;

        SerializedObject so = new SerializedObject(marker);
        so.FindProperty("markerType").enumValueIndex = (int)type;
        so.FindProperty("displayName").stringValue = GetDisplayName(type);
        so.FindProperty("markerId").stringValue = GetPrefabName(type);
        so.FindProperty("gizmoColor").colorValue = AreaMarkerDefaults.GetColor(type);
        so.ApplyModifiedPropertiesWithoutUndo();

        return marker;
    }

    private static void ConfigureSampleMarker(AreaMarkerBase marker, AreaMarkerType type)
    {
        SerializedObject so = new SerializedObject(marker);
        SetString(so, "markerId", $"sample_{type.ToString().ToLowerInvariant()}_01");
        SetString(so, "areaId", "sample_area_all_markers");
        SetString(so, "description", GetSampleDescription(type));
        SetFloat(so, "interactionRange", 1.5f);
        SetBool(so, "showLabelInSceneView", true);

        switch (type)
        {
            case AreaMarkerType.Connection:
                SetString(so, "targetSceneName", "OverworldScene");
                SetString(so, "targetSpawnId", "sample_spawn_connection");
                SetBool(so, "interactToUse", true);
                SetFloat(so, "fadeDuration", 0.25f);
                break;
            case AreaMarkerType.Enemy:
                SetString(so, "enemyId", "sample_enemy_01");
                SetString(so, "battleEncounterId", "sample_encounter_enemy_01");
                break;
            case AreaMarkerType.Hazard:
                SetInt(so, "damage", 10);
                SetFloat(so, "knockback", 0.75f);
                SetBool(so, "triggerOnEnter", false);
                break;
            case AreaMarkerType.Puzzle:
                SetString(so, "puzzleId", "sample_puzzle_switch_01");
                SetString(so, "solvedFlag", "sample.puzzle.switch_01.solved");
                break;
            case AreaMarkerType.Vendor:
                SetString(so, "vendorId", "sample_vendor_01");
                SetString(so, "shopId", "sample_shop_basic");
                break;
            case AreaMarkerType.ShortcutDoor:
                SetString(so, "targetSceneName", "OverworldScene");
                SetString(so, "targetSpawnId", "sample_spawn_shortcut");
                SetBool(so, "interactToUse", true);
                SetFloat(so, "fadeDuration", 0.25f);
                SetString(so, "doorId", "sample_shortcut_a");
                SetString(so, "linkedDoorId", "sample_shortcut_b");
                SetBool(so, "isLocked", false);
                break;
            case AreaMarkerType.NPC:
                SetBool(so, "isOneShot", true);
                SetString(so, "setFlagOnComplete", "sample.npc.hello.seen");
                SetString(so, "npcId", "sample_npc_01");
                SetString(so, "dialogueId", "sample_dialogue_hello");
                SetString(so, "fallbackDialogueText", "* 안녕. 여기는 Area Marker NPC 대화 테스트야.\n* Z를 누르면 다음 대사로 넘기는 델타룬식 대화 캔버스를 사용해.");
                break;
            case AreaMarkerType.Item:
                SetBool(so, "isOneShot", true);
                SetString(so, "setFlagOnComplete", "sample.item.heart_candy.picked");
                SetString(so, "itemId", "sample_item_heart_candy");
                SetInt(so, "amount", 1);
                SetString(so, "pickupMessage", "* sample_item_heart_candy를 얻었다.");
                break;
            case AreaMarkerType.Sign:
                SetBool(so, "isOneShot", true);
                SetString(so, "setFlagOnComplete", "sample.sign.read");
                SetString(so, "signText", "* 샘플 표지판이다.\n* 이제 로그가 아니라 실제 대화창으로 표시된다.");
                break;
            case AreaMarkerType.SavePoint:
                SetString(so, "savePointId", "sample_save_point_01");
                break;
            case AreaMarkerType.PlotPoint:
                SetBool(so, "isOneShot", true);
                SetString(so, "setFlagOnComplete", "sample.plot.interact_01.complete");
                SetString(so, "plotId", "sample_plot_interact_01");
                SetString(so, "fallbackDialogueText", "* 어딘가에서 이벤트가 진행되는 느낌이 든다.");
                SetEnum(so, "triggerMode", (int)AreaPlotTriggerMode.OnInteract);
                break;
            case AreaMarkerType.Sublocation:
                SetString(so, "sublocationId", "sample_sublocation_01");
                SetString(so, "displayTitle", "Sample Sublocation");
                break;
        }

        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static string GetSampleDescription(AreaMarkerType type)
    {
        switch (type)
        {
            case AreaMarkerType.Connection: return "Z 상호작용으로 씬/스폰 이동 요청을 테스트하는 샘플입니다.";
            case AreaMarkerType.Enemy: return "Z 상호작용으로 전투 진입 요청을 테스트하는 샘플입니다. EnemyData가 비어 있으면 Debug.Log fallback을 출력합니다.";
            case AreaMarkerType.Hazard: return "Z 상호작용으로 피해/넉백 요청을 테스트하는 샘플입니다.";
            case AreaMarkerType.Puzzle: return "Z 상호작용으로 퍼즐 시작/해결 플래그 요청을 테스트하는 샘플입니다.";
            case AreaMarkerType.Vendor: return "Z 상호작용으로 상점 UI 연결 지점을 테스트하는 샘플입니다.";
            case AreaMarkerType.ShortcutDoor: return "Z 상호작용으로 잠금 해제된 숏컷 문 이동 요청을 테스트하는 샘플입니다.";
            case AreaMarkerType.NPC: return "Z 상호작용으로 DialogueManager/DialogueCanvas 대화를 테스트하는 샘플입니다.";
            case AreaMarkerType.Item: return "Z 상호작용으로 아이템 지급/완료 플래그를 테스트하는 샘플입니다.";
            case AreaMarkerType.Sign: return "Z 상호작용으로 표지판 텍스트를 DialogueCanvas에 표시하는 샘플입니다.";
            case AreaMarkerType.SavePoint: return "Z 상호작용으로 저장 지점 요청을 테스트하는 샘플입니다.";
            case AreaMarkerType.PlotPoint: return "Z 상호작용으로 플롯 이벤트와 DialogueCanvas 표시를 테스트하는 샘플입니다.";
            case AreaMarkerType.Sublocation: return "Z 상호작용으로 하위 위치 진입 기록을 테스트하는 샘플입니다.";
            default: return "Z 상호작용 테스트 샘플입니다.";
        }
    }

    private static void SetString(SerializedObject so, string propertyName, string value)
    {
        SerializedProperty property = so.FindProperty(propertyName);
        if (property != null) property.stringValue = value;
    }

    private static void SetBool(SerializedObject so, string propertyName, bool value)
    {
        SerializedProperty property = so.FindProperty(propertyName);
        if (property != null) property.boolValue = value;
    }

    private static void SetInt(SerializedObject so, string propertyName, int value)
    {
        SerializedProperty property = so.FindProperty(propertyName);
        if (property != null) property.intValue = value;
    }

    private static void SetFloat(SerializedObject so, string propertyName, float value)
    {
        SerializedProperty property = so.FindProperty(propertyName);
        if (property != null) property.floatValue = value;
    }

    private static void SetEnum(SerializedObject so, string propertyName, int value)
    {
        SerializedProperty property = so.FindProperty(propertyName);
        if (property != null) property.enumValueIndex = value;
    }

    private static string GetPrefabName(AreaMarkerType type)
    {
        switch (type)
        {
            case AreaMarkerType.Connection: return "Marker_Connection";
            case AreaMarkerType.Enemy: return "Marker_Enemy";
            case AreaMarkerType.Hazard: return "Marker_Hazard";
            case AreaMarkerType.Puzzle: return "Marker_Puzzle";
            case AreaMarkerType.Vendor: return "Marker_Vendor";
            case AreaMarkerType.ShortcutDoor: return "Marker_ShortcutDoor";
            case AreaMarkerType.NPC: return "Marker_NPC";
            case AreaMarkerType.Item: return "Marker_Item";
            case AreaMarkerType.Sign: return "Marker_Sign";
            case AreaMarkerType.SavePoint: return "Marker_SavePoint";
            case AreaMarkerType.PlotPoint: return "Marker_PlotPoint";
            case AreaMarkerType.Sublocation: return "Marker_Sublocation";
            default: return "Marker_Unknown";
        }
    }

    private static string GetDisplayName(AreaMarkerType type)
    {
        switch (type)
        {
            case AreaMarkerType.ShortcutDoor: return "Shortcut Door";
            case AreaMarkerType.SavePoint: return "SAVE Point";
            case AreaMarkerType.PlotPoint: return "Plot Point";
            default: return type.ToString();
        }
    }
}