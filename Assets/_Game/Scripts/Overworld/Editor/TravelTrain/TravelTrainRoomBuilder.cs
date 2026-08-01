using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Cinemachine;
using UnityEditor;
using UnityEngine;

public static class TravelTrainRoomBuilder
{
    public static void EnsureRoom(
        TravelTrainCoreAssetBundle core,
        TravelTrainDataBundle data,
        TravelTrainPromotionStatus promotionStatus)
    {
        if (core?.TrainRoom == null || data?.Network == null)
            throw new ArgumentNullException(nameof(data));
        if (!TravelWorldBuildPreflight.ValidateNoDirtyOwnedContent(out string preflightError))
            throw new InvalidOperationException(preflightError);

        GameObject root = PrefabUtility.LoadPrefabContents(TravelTrainPaths.Prefab);
        try
        {
            root.name = "Room_TravelTrainInterior";
            RoomInstance roomInstance = GetOrAddSingle<RoomInstance>(root);
            TravelTrainEditorAssetUtility.Set(
                roomInstance,
                "_roomId",
                p => p.stringValue = TravelTrainIds.Room);

            Transform geometry = EnsureDirectChild(root.transform, "Geometry");
            Transform props = EnsureDirectChild(root.transform, "Props");
            Transform actors = EnsureDirectChild(root.transform, "Actors");
            Transform markers = EnsureDirectChild(root.transform, "Markers");
            Transform anchors = EnsureDirectChild(root.transform, "Event Anchors");
            Transform cinematics = EnsureDirectChild(root.transform, "Cinematics");
            Transform systems = EnsureDirectChild(root.transform, "Systems");
            Transform spawns = EnsureDirectChild(markers, "Spawns");
            var context = new GeneratedRoomContext
            {
                RoomId = TravelTrainIds.Room,
                Root = root,
                Geometry = geometry,
                Props = props,
                Actors = actors,
                Markers = markers,
                EventAnchors = anchors,
                Cinematics = cinematics,
                Systems = systems,
                Spawns = spawns
            };

            EnsureCameraBounds(roomInstance, root.transform);
            EnsureSpawn(context, "entry", new Vector2(0f, -1.35f), FacingDirection.Up);
            EnsureSpawn(context, "exit", new Vector2(0f, -1.65f), FacingDirection.Down);

            Transform route = EnsureAnchor(anchors, "route_console", new Vector2(0f, 0.25f));
            Transform conductor = EnsureAnchor(anchors, "conductor", new Vector2(-2.7f, 0.2f));
            Transform window = EnsureAnchor(anchors, "window_event", new Vector2(0f, 1.25f));
            EnsureAnchor(anchors, "party_event_left", new Vector2(-1.65f, -0.35f));
            EnsureAnchor(anchors, "party_event_right", new Vector2(1.65f, -0.35f));

            OverworldCinematicStage stage = EnsureDepartureStage(
                cinematics,
                props,
                data.DepartureShot);
            SceneActionSequencePlayer sequencePlayer =
                GetOrAddSingle<SceneActionSequencePlayer>(systems.gameObject);
            sequencePlayer.Configure(
                data.DepartureSequence,
                stage,
                TravelTrainIds.DepartureShot);
            TrainTravelController controller =
                GetOrAddSingle<TrainTravelController>(systems.gameObject);
            controller.Configure(
                data.Network,
                sequencePlayer,
                0.25f,
                "* 열차가 지금은 출발할 수 없다.");
            TrainStopStateSynchronizer synchronizer =
                GetOrAddSingle<TrainStopStateSynchronizer>(systems.gameObject);
            synchronizer.Configure(data.Network);

            ConfigureDestinationSelector(
                route, controller, data.ShowcaseStop, data.WideFieldStop);

            TrainExitMarker exit = EnsureExitMarker(context);
            exit.Configure(data.Network, 0.25f);
            ConfigureConductor(context, conductor.position, data.ConductorDialogue);
            ConfigureWindowSign(context, window.position, data.WindowDialogue);

            PrefabUtility.SaveAsPrefabAsset(root, TravelTrainPaths.Prefab);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }

        GameObject saved = TravelTrainEditorAssetUtility.RequireAsset<GameObject>(TravelTrainPaths.Prefab);
        RoomInstance savedRoom = saved.GetComponent<RoomInstance>();
        TravelTrainEditorAssetUtility.Set(
            core.TrainRoom,
            "_roomPrefab",
            p => p.objectReferenceValue = savedRoom);
        core.TrainArea.RefreshMarkerSummary();
        AssetDatabase.SaveAssets();
    }

    private static void EnsureCameraBounds(RoomInstance room, Transform root)
    {
        if (room.CameraBounds != null)
            return;

        Transform boundsTransform = EnsureDirectChild(root, "CameraBounds");
        PolygonCollider2D bounds = GetOrAddSingle<PolygonCollider2D>(boundsTransform.gameObject);
        bounds.isTrigger = true;
        bounds.points = new[]
        {
            new Vector2(-4.8f, -2.3f),
            new Vector2(-4.8f, 2.3f),
            new Vector2(4.8f, 2.3f),
            new Vector2(4.8f, -2.3f)
        };
        TravelTrainEditorAssetUtility.Set(room, "_cameraBounds", p => p.objectReferenceValue = bounds);
    }

    private static void EnsureSpawn(
        GeneratedRoomContext context,
        string id,
        Vector2 position,
        FacingDirection facing)
    {
        SpawnPoint[] matches = context.Root.GetComponentsInChildren<SpawnPoint>(true)
            .Where(point => string.Equals(point.SpawnPointId, id, StringComparison.Ordinal))
            .ToArray();
        if (matches.Length > 1)
            throw new InvalidOperationException("중복 열차 Spawn ID: " + id);
        if (matches.Length == 1)
            return;
        GeneratedRoomEditorUtility.CreateSpawn(context, id, position, facing);
    }

    private static Transform EnsureAnchor(Transform parent, string id, Vector2 position)
    {
        Transform[] matches = parent.Cast<Transform>()
            .Where(child => string.Equals(child.name, id, StringComparison.Ordinal))
            .ToArray();
        if (matches.Length > 1)
            throw new InvalidOperationException("중복 열차 Event Anchor: " + id);
        if (matches.Length == 1)
            return matches[0];
        return GeneratedRoomEditorUtility.CreateEmpty(id, parent, position);
    }

    private static void ConfigureDestinationSelector(
        Transform route,
        TrainTravelController controller,
        TrainStopDefinition showcaseStop,
        TrainStopDefinition wideFieldStop)
    {
        RemoveDirectChild(route, "Destination_Showcase");
        RemoveDirectChild(route, "Destination_WideField");

        int interactableLayer = LayerMask.NameToLayer("Interactable");
        if (interactableLayer >= 0)
            route.gameObject.layer = interactableLayer;

        CircleCollider2D collider = GetOrAddSingle<CircleCollider2D>(route.gameObject);
        collider.isTrigger = true;
        collider.radius = 0.6f;

        TrainDestinationSelectorInteractable selector =
            GetOrAddSingle<TrainDestinationSelectorInteractable>(route.gameObject);
        selector.Configure(
            controller,
            new[] { showcaseStop, wideFieldStop },
            "* \uC5B4\uB290 \uC815\uB958\uC18C\uB85C \uC774\uB3D9\uD560\uAE4C?");
    }

    private static void RemoveDirectChild(Transform parent, string name)
    {
        Transform child = parent.Find(name);
        if (child != null && child.parent == parent)
            UnityEngine.Object.DestroyImmediate(child.gameObject);
    }

    private static TrainExitMarker EnsureExitMarker(GeneratedRoomContext context)
    {
        TrainExitMarker[] exits = context.Root.GetComponentsInChildren<TrainExitMarker>(true);
        TrainExitMarker exact = exits.SingleOrDefault(
            marker => string.Equals(
                marker.MarkerId,
                "travel_train.main_car.train_exit",
                StringComparison.Ordinal));
        if (exits.Length > 1 || (exits.Length == 1 && exact == null))
            throw new InvalidOperationException("열차 하차 Marker 구성이 중복되거나 ID가 잘못됐습니다.");
        if (exact != null)
            return exact;

        return GeneratedRoomEditorUtility.CreateMarker<TrainExitMarker>(
            context,
            "travel_train.main_car.train_exit",
            "현재 정류소로 내리기",
            new Vector2(0f, -2.05f),
            AreaMarkerType.Sublocation,
            0.58f);
    }

    private static void ConfigureConductor(
        GeneratedRoomContext context,
        Vector3 worldPosition,
        FlagDialogueSelector selector)
    {
        NPCMarker[] markers = context.Root.GetComponentsInChildren<NPCMarker>(true);
        NPCMarker conductor = markers.SingleOrDefault(
            marker => string.Equals(
                marker.MarkerId,
                "travel_train.main_car.conductor",
                StringComparison.Ordinal));
        if (conductor == null)
        {
            conductor = GeneratedRoomEditorUtility.CreateMarker<NPCMarker>(
                context,
                "travel_train.main_car.conductor",
                "차장",
                context.Root.transform.InverseTransformPoint(worldPosition),
                AreaMarkerType.NPC);
        }
        TravelTrainEditorAssetUtility.Set(conductor, "npcId", p => p.stringValue = "travel_train.conductor");
        TravelTrainEditorAssetUtility.Set(conductor, "dialogueId", p => p.stringValue = "travel_train.conductor.current_stop");
        TravelTrainEditorAssetUtility.Set(conductor, "dialogueSelector", p => p.objectReferenceValue = selector);

        if (context.Actors.Find("Npc_Conductor") == null)
        {
            Transform visual = GeneratedRoomEditorUtility.CreateEmpty(
                "Npc_Conductor",
                context.Actors,
                context.Root.transform.InverseTransformPoint(worldPosition));
            SpriteRenderer renderer = visual.gameObject.AddComponent<SpriteRenderer>();
            renderer.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(ShowcaseStationPaths.TestNpcSprite);
            renderer.sortingLayerName = "Characters";
            renderer.sortingOrder = 6;
        }
    }

    private static void ConfigureWindowSign(
        GeneratedRoomContext context,
        Vector3 worldPosition,
        FlagDialogueSelector selector)
    {
        SignMarker[] signs = context.Root.GetComponentsInChildren<SignMarker>(true);
        SignMarker sign = signs.SingleOrDefault(
            marker => string.Equals(
                marker.MarkerId,
                "travel_train.main_car.travel_note",
                StringComparison.Ordinal));
        if (sign == null)
        {
            sign = GeneratedRoomEditorUtility.CreateMarker<SignMarker>(
                context,
                "travel_train.main_car.travel_note",
                "창밖 풍경",
                context.Root.transform.InverseTransformPoint(worldPosition),
                AreaMarkerType.Sign);
        }
        TravelTrainEditorAssetUtility.Set(sign, "dialogueSelector", p => p.objectReferenceValue = selector);
        TravelTrainEditorAssetUtility.Set(sign, "signText", p => p.stringValue = "* 창밖 풍경을 바라본다.");
    }

    private static OverworldCinematicStage EnsureDepartureStage(
        Transform cinematics,
        Transform props,
        CinematicShotAsset shot)
    {
        Transform stageTransform = EnsureDirectChild(cinematics, "Departure Cinematic Stage");
        OverworldCinematicStage stage = GetOrAddSingle<OverworldCinematicStage>(stageTransform.gameObject);
        Transform rail = EnsureDirectChild(stageTransform, "Camera Rail");
        Transform streaks = props.Find("Window Streaks");
        if (streaks == null)
        {
            streaks = GeneratedRoomEditorUtility.CreateBlock(
                "Window Streaks",
                props,
                new Vector2(-3.6f, 0.65f),
                new Vector2(2.4f, 0.18f),
                new Color(0.62f, 0.76f, 0.76f, 0.75f),
                5).transform;
        }

        Transform cameraTransform = EnsureDirectChild(stageTransform, "Cinematic Camera");
        cameraTransform.localPosition = new Vector3(0f, 0f, -1f);
        CinemachineCamera camera = GetOrAddSingle<CinemachineCamera>(cameraTransform.gameObject);
        CinemachineFollow follow = GetOrAddSingle<CinemachineFollow>(cameraTransform.gameObject);
        var tracking = follow.TrackerSettings;
        tracking.PositionDamping = shot.CameraPositionDamping;
        follow.TrackerSettings = tracking;
        camera.Priority = new PrioritySettings { Enabled = true, Value = 100 };
        camera.Lens.ModeOverride = LensSettings.OverrideModes.Orthographic;
        camera.Lens.OrthographicSize = shot.StartOrthographicSize;

        var serialized = new SerializedObject(stage);
        serialized.FindProperty("_stageId").stringValue = TravelTrainIds.DepartureSequence;
        serialized.FindProperty("_cinematicCamera").objectReferenceValue = camera;
        SerializedProperty subjects = serialized.FindProperty("_subjects");
        subjects.arraySize = 2;
        ConfigureSubject(subjects.GetArrayElementAtIndex(0), "camera_rail", rail);
        ConfigureSubject(subjects.GetArrayElementAtIndex(1), "window_streaks", streaks);
        SerializedProperty shots = serialized.FindProperty("_shots");
        shots.arraySize = 1;
        shots.GetArrayElementAtIndex(0).objectReferenceValue = shot;
        serialized.ApplyModifiedPropertiesWithoutUndo();

        ScenarioValidationResult validation = stage.ValidateDefinition();
        if (validation.HasErrors)
            throw new InvalidOperationException("열차 출발 Cinematic Stage 검증에 실패했습니다.");
        cameraTransform.gameObject.SetActive(false);
        return stage;
    }

    private static void ConfigureSubject(
        SerializedProperty subject,
        string id,
        Transform target)
    {
        subject.FindPropertyRelative("SubjectId").stringValue = id;
        subject.FindPropertyRelative("Target").objectReferenceValue = target;
    }

    private static Transform EnsureDirectChild(Transform parent, string name)
    {
        Transform found = null;
        for (int i = 0; i < parent.childCount; i++)
        {
            Transform child = parent.GetChild(i);
            if (!string.Equals(child.name, name, StringComparison.Ordinal))
                continue;
            if (found != null)
                throw new InvalidOperationException("중복 hierarchy root: " + name);
            found = child;
        }
        if (found != null)
            return found;
        return GeneratedRoomEditorUtility.CreateEmpty(name, parent);
    }

    private static T GetOrAddSingle<T>(GameObject owner) where T : Component
    {
        T[] components = owner.GetComponents<T>();
        if (components.Length > 1)
            throw new InvalidOperationException($"{owner.name}에 {typeof(T).Name}이 중복됐습니다.");
        return components.Length == 1 ? components[0] : owner.AddComponent<T>();
    }
}