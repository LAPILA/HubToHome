using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace HubToHome.EditorTools.ContentMaker
{
    /// <summary>Explicit authoring commands. Existing rooms and open scenes are never regenerated.</summary>
    internal static class ContentMakerMapService
    {
        internal const string RegionsRoot = "Assets/_Game/Content/Maps/Regions";
        private const string WhiteSpritePath = "Assets/_Game/Content/Maps/Shared/Generated/RoomMap_WhiteSquare.png";
        private const string PlayerPrefabPath = "Assets/_Game/Content/Characters/Prefabs/Player/Player_Base.prefab";
        private const string BootstrapPrefabPath = "Assets/_Game/Core/Prefabs/[GameBootstrap].prefab";
        private const string BattleHostPrefabPath = "Assets/_Game/Content/Battle/Prefabs/System/SeamlessBattleHost.prefab";

        internal static string CreateRegion(string name)
        {
            EnsureEditMode();
            string fileName = RequireName(name);
            string path = RegionsRoot + "/" + fileName;
            if (AssetDatabase.IsValidFolder(path) || Directory.Exists(path) || File.Exists(path))
                throw new InvalidOperationException("같은 이름의 지역 폴더가 있습니다. 기존 지역을 선택하거나 다른 이름을 입력하세요.");
            ContentMakerAssetUtility.EnsureFolder(path);
            EnsureRegionFolders(path);
            return path;
        }

        internal static RoomDefinition CreateRoom(string regionPath, string name, Vector2 size, bool interior)
        {
            EnsureEditMode();
            RequireMainStage();
            RequireRegion(regionPath);
            string roomName = RequireName(name);
            if (!float.IsFinite(size.x) || !float.IsFinite(size.y)
                || size.x < 2f || size.y < 2f || size.x > 500f || size.y > 500f)
                throw new InvalidOperationException("Room 크기는 가로·세로 각각 2~500 Unity 단위로 입력하세요.");

            Sprite floorSprite = RequireAsset<Sprite>(WhiteSpritePath);
            string id = NewRoomId(regionPath, roomName);
            string stem = "Room_" + Path.GetFileName(regionPath) + "_" + roomName;
            EnsureRegionFolders(regionPath);
            var createdPaths = new List<string>();
            Scene previous = SceneManager.GetActiveScene();
            Scene temporary = default;
            try
            {
                temporary = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
                SceneManager.SetActiveScene(temporary);
                GameObject root = BuildRoom(stem, id, size, interior, floorSprite, temporary);
                RoomDefinition room = SaveNewRoom(regionPath, stem, id, root, null, createdPaths);
                return room;
            }
            catch
            {
                RollbackCreatedAssets(createdPaths);
                throw;
            }
            finally
            {
                RestoreScene(previous, temporary);
            }
        }

        internal static RoomDefinition DuplicateRoom(string regionPath, RoomDefinition source, string name)
        {
            EnsureEditMode();
            RequireRegion(regionPath);
            RequireRoom(source);
            string prefabPath = AssetDatabase.GetAssetPath(source.RoomPrefab.gameObject);
            PrefabStage openStage = PrefabStageUtility.GetCurrentPrefabStage();
            if (openStage != null && openStage.assetPath == prefabPath && openStage.scene.isDirty)
                throw new InvalidOperationException("원본 Room에 저장되지 않은 편집이 있습니다. Prefab Mode에서 저장한 뒤 복제하세요.");

            string roomName = RequireName(name);
            string id = NewRoomId(regionPath, roomName);
            string stem = "Room_" + Path.GetFileName(regionPath) + "_" + roomName;
            EnsureRegionFolders(regionPath);
            var createdPaths = new List<string>();
            GameObject root = null;
            try
            {
                root = PrefabUtility.LoadPrefabContents(prefabPath);
                // Independent room copy; nested reusable actor/prop prefabs keep their own links.
                if (PrefabUtility.IsPartOfPrefabInstance(root))
                    PrefabUtility.UnpackPrefabInstance(root, PrefabUnpackMode.OutermostRoot, InteractionMode.AutomatedAction);
                root.name = stem;
                ReidentifyRoom(root, id);
                RoomDefinition room = SaveNewRoom(regionPath, stem, id, root, source, createdPaths);
                return room;
            }
            catch
            {
                RollbackCreatedAssets(createdPaths);
                throw;
            }
            finally
            {
                if (root != null)
                    PrefabUtility.UnloadPrefabContents(root);
            }
        }

        internal static SceneAsset CreateRegionScene(
            string regionPath, string name, RoomDefinition initialRoom, bool includeBattleHost)
        {
            EnsureEditMode();
            RequireMainStage();
            RequireRegion(regionPath);
            RequireRoom(initialRoom);
            if (!AssetDatabase.GetAssetPath(initialRoom).StartsWith(regionPath + "/", StringComparison.Ordinal))
                throw new InvalidOperationException("현재 지역 안의 Room을 시작 Room으로 선택하세요.");

            GameObject cameraPrefab = RequireAsset<GameObject>(DevelopmentContentPaths.GameplayCameraRigPrefab);
            GameObject playerPrefab = RequireAsset<GameObject>(PlayerPrefabPath);
            GameObject bootstrapPrefab = RequireAsset<GameObject>(BootstrapPrefabPath);
            GameObject hostPrefab = includeBattleHost ? RequireAsset<GameObject>(BattleHostPrefabPath) : null;
            List<RoomDefinition> rooms = GetRegionRooms(regionPath);
            if (includeBattleHost)
            {
                foreach (RoomDefinition room in rooms)
                {
                    if (room.RoomPrefab.GetComponentInChildren<SeamlessBattleHost>(true) != null)
                        throw new InvalidOperationException("Room '" + room.name
                            + "'에 전투 Host가 이미 있습니다. Scene의 '전투 호스트 포함'을 끄세요.");
                }
            }

            ContentMakerAssetUtility.EnsureFolder(regionPath + "/Scenes");
            string path = ContentMakerAssetUtility.UniqueAssetPath(
                regionPath + "/Scenes", "Region_" + RequireName(name), ".unity");
            RequireUnusedPath(path);
            Scene previous = SceneManager.GetActiveScene();
            Scene scene = default;
            bool saved = false;
            try
            {
                scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
                SceneManager.SetActiveScene(scene);
                InstantiateInScene(bootstrapPrefab, scene);
                GameObject playerRoot = InstantiateInScene(playerPrefab, scene);
                PlayerController player = playerRoot.GetComponentInChildren<PlayerController>(true);
                CameraController camera = InstantiateInScene(cameraPrefab, scene).GetComponentInChildren<CameraController>(true);
                if (player == null || camera == null || camera.VirtualCamera == null)
                    throw new InvalidOperationException("공용 Player 또는 Camera Prefab의 필수 컴포넌트가 없습니다.");

                GameObject systems = new GameObject("Map Systems");
                SceneManager.MoveGameObjectToScene(systems, scene);
                RoomContainer container = systems.AddComponent<RoomContainer>();
                MapTransitionService transition = systems.AddComponent<MapTransitionService>();
                Set(container, "_initialRoom", p => p.objectReferenceValue = initialRoom);
                Set(container, "_loadInitialRoomOnStart", p => p.boolValue = false);
                Set(transition, "_roomContainer", p => p.objectReferenceValue = container);
                Set(transition, "_dontDestroyOnLoad", p => p.boolValue = false);
                RegionEntryCoordinator entry = systems.AddComponent<RegionEntryCoordinator>();
                entry.Configure(container, player, initialRoom, rooms, true);
                Set(entry, "_prepareOnAwake", p => p.boolValue = true);

                camera.VirtualCamera.Follow = player.transform;
                Set(camera, "_centerTarget", p => p.objectReferenceValue = player.transform);
                PrefabUtility.RecordPrefabInstancePropertyModifications(camera.VirtualCamera);
                PrefabUtility.RecordPrefabInstancePropertyModifications(camera);
                if (hostPrefab != null)
                    InstantiateInScene(hostPrefab, scene);

                if (!EditorSceneManager.SaveScene(scene, path, false))
                    throw new InvalidOperationException("새 지역 Scene을 저장하지 못했습니다: " + path);
                saved = true;
            }
            finally
            {
                RestoreScene(previous, scene);
                // Only the reserved new path belongs to this command; no pre-existing scene is touched.
                if (!saved && File.Exists(path))
                    AssetDatabase.DeleteAsset(path);
            }

            SceneAsset result = AssetDatabase.LoadAssetAtPath<SceneAsset>(path);
            if (result == null)
                throw new InvalidOperationException("Scene 생성 후 자산을 확인하지 못했습니다: " + path);
            return result;
        }

        internal static RoomMapValidationReport ValidateRoom(RoomDefinition room)
        {
            RequireRoom(room);
            string path = AssetDatabase.GetAssetPath(room.RoomPrefab.gameObject);
            PrefabStage stage = PrefabStageUtility.GetCurrentPrefabStage();
            GameObject root = stage != null && stage.assetPath == path
                ? stage.prefabContentsRoot : room.RoomPrefab.gameObject;
            return RoomMapValidationScanner.Scan(
                RoomMapValidationScopeCapture.CaptureRoots(new[] { root }, "Room: " + room.RoomId, false));
        }

        internal static bool RegisterRegionRooms(string regionPath, SceneAsset sceneAsset, Func<string, bool> confirm)
        {
            EnsureEditMode();
            RequireRegion(regionPath);
            if (PrefabStageUtility.GetCurrentPrefabStage() != null)
                throw new InvalidOperationException("Prefab Mode를 종료한 뒤 지역 Scene에 Room 목록을 등록하세요.");
            string path = AssetDatabase.GetAssetPath(sceneAsset);
            if (sceneAsset == null || !path.StartsWith(regionPath + "/Scenes/", StringComparison.Ordinal))
                throw new InvalidOperationException("현재 지역의 Scenes 폴더 안에 있는 Scene을 선택하세요.");
            if (confirm == null)
                throw new ArgumentNullException(nameof(confirm));
            List<RoomDefinition> regionRooms = GetRegionRooms(regionPath);
            if (regionRooms.Count == 0)
                throw new InvalidOperationException("등록할 Room이 없습니다. 먼저 Room을 만드세요.");

            Scene previous = SceneManager.GetActiveScene();
            Scene target = FindLoadedScene(path);
            bool opened = !target.IsValid();
            if (!opened && target.isDirty)
                throw new InvalidOperationException("대상 Scene에 저장되지 않은 변경이 있습니다. 먼저 직접 저장하거나 취소해 주세요.");
            if (opened)
                target = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            int undoGroup = -1;
            bool changed = false;
            try
            {
                RegionEntryCoordinator[] entries = FindComponents<RegionEntryCoordinator>(target);
                if (entries.Length > 1)
                    throw new InvalidOperationException("RegionEntryCoordinator가 여러 개여서 등록 대상을 결정할 수 없습니다.");
                RegionEntryCoordinator entry = entries.Length == 1 ? entries[0] : null;
                RoomContainer container;
                PlayerController player;
                RoomDefinition defaultRoom;
                var merged = new List<RoomDefinition>();
                var ids = new Dictionary<string, RoomDefinition>(StringComparer.Ordinal);
                if (entry != null)
                {
                    var serialized = new SerializedObject(entry);
                    container = serialized.FindProperty("_roomContainer").objectReferenceValue as RoomContainer;
                    player = serialized.FindProperty("_player").objectReferenceValue as PlayerController;
                    defaultRoom = serialized.FindProperty("_defaultRoom").objectReferenceValue as RoomDefinition;
                    if (container == null || player == null || container.gameObject.scene != target || player.gameObject.scene != target)
                        throw new InvalidOperationException("기존 Coordinator의 RoomContainer/Player 참조가 누락되었거나 다른 Scene을 가리킵니다.");
                    SerializedProperty existingRooms = serialized.FindProperty("_rooms");
                    for (int i = 0; i < existingRooms.arraySize; i++)
                        MergeRoom(merged, ids, existingRooms.GetArrayElementAtIndex(i).objectReferenceValue as RoomDefinition);
                }
                else
                {
                    RoomContainer[] containers = FindComponents<RoomContainer>(target);
                    PlayerController[] players = FindComponents<PlayerController>(target);
                    if (containers.Length != 1 || players.Length != 1)
                        throw new InvalidOperationException("Coordinator가 없는 Scene은 RoomContainer와 PlayerController가 각각 하나여야 합니다.");
                    container = containers[0];
                    player = players[0];
                    defaultRoom = container.InitialRoom;
                }
                RequireRoom(defaultRoom);
                int previousCount = merged.Count;
                MergeRoom(merged, ids, defaultRoom);
                foreach (RoomDefinition room in regionRooms)
                    MergeRoom(merged, ids, room);

                string summary = "대상: " + path + "\n\n시작 Room 유지: " + defaultRoom.name + "\n"
                    + "기존 고유 Room: " + previousCount + "개 / 추가: " + (merged.Count - previousCount)
                    + "개 / 반영 후: " + merged.Count + "개\n"
                    + "현재 지역의 유효한 Room " + regionRooms.Count + "개를 합칩니다. 기존 참조는 삭제하지 않습니다.\n\n"
                    + (entry == null ? "지역 진입 Coordinator를 추가하고 초기 Room 로드를 그쪽으로 연결합니다.\n" : "기존 Coordinator의 Room 목록만 갱신합니다.\n")
                    + "이 Scene만 저장하며 Build Settings와 다른 Scene은 바꾸지 않습니다.";
                if (!confirm(summary))
                    return false;

                Undo.IncrementCurrentGroup();
                undoGroup = Undo.GetCurrentGroup();
                Undo.SetCurrentGroupName("지역 Scene에 Room 목록 등록");
                changed = true;
                if (entry == null)
                {
                    Undo.RegisterCompleteObjectUndo(container, "지역 초기 Room 진입 연결");
                    entry = Undo.AddComponent<RegionEntryCoordinator>(container.gameObject);
                    entry.Configure(container, player, defaultRoom, merged, true);
                    Set(container, "_loadInitialRoomOnStart", p => p.boolValue = false);
                }
                else
                {
                    Undo.RegisterCompleteObjectUndo(entry, "지역 Room 목록 등록");
                    var serialized = new SerializedObject(entry);
                    SerializedProperty list = serialized.FindProperty("_rooms");
                    list.arraySize = merged.Count;
                    for (int i = 0; i < merged.Count; i++)
                        list.GetArrayElementAtIndex(i).objectReferenceValue = merged[i];
                    serialized.ApplyModifiedPropertiesWithoutUndo();
                }
                if (PrefabUtility.IsPartOfPrefabInstance(entry))
                    PrefabUtility.RecordPrefabInstancePropertyModifications(entry);
                EditorSceneManager.MarkSceneDirty(target);
                if (!EditorSceneManager.SaveScene(target, path, false))
                    throw new InvalidOperationException("대상 Scene 저장에 실패했습니다. 이번 Room 등록 변경을 취소합니다.");
                Undo.CollapseUndoOperations(undoGroup);
                return true;
            }
            catch
            {
                if (changed && undoGroup >= 0)
                    Undo.RevertAllDownToGroup(undoGroup);
                throw;
            }
            finally
            {
                if (previous.IsValid() && previous.isLoaded)
                    SceneManager.SetActiveScene(previous);
                if (opened && target.IsValid() && target.isLoaded)
                    EditorSceneManager.CloseScene(target, true);
            }
        }

        internal static void OpenRoom(RoomDefinition room)
        {
            EnsureEditMode();
            RequireRoom(room);
            PrefabStageUtility.OpenPrefab(AssetDatabase.GetAssetPath(room.RoomPrefab.gameObject));
        }

        internal static void SelectIssue(RoomDefinition room, RoomMapValidationIssue issue)
        {
            EnsureEditMode();
            RequireRoom(room);
            if (issue == null)
                return;
            string path = AssetDatabase.GetAssetPath(room.RoomPrefab.gameObject);
            string relativePath = string.Empty;
            if (issue.Context is Component component)
            {
                RoomInstance sourceRoot = component.GetComponentInParent<RoomInstance>();
                if (sourceRoot != null)
                    relativePath = AnimationUtility.CalculateTransformPath(component.transform, sourceRoot.transform);
            }

            PrefabStage stage = PrefabStageUtility.OpenPrefab(path);
            if (stage == null || stage.prefabContentsRoot == null)
                return;
            Transform target = string.IsNullOrEmpty(relativePath)
                ? stage.prefabContentsRoot.transform : stage.prefabContentsRoot.transform.Find(relativePath);
            Selection.activeGameObject = target != null ? target.gameObject : stage.prefabContentsRoot;
            EditorGUIUtility.PingObject(Selection.activeGameObject);
            SceneView.lastActiveSceneView?.FrameSelected();
        }

        internal static void RenameRoomData(RoomDefinition room, string name)
        {
            EnsureEditMode();
            if (room == null)
                throw new InvalidOperationException("이름을 바꿀 Room 데이터를 선택하세요.");
            string path = AssetDatabase.GetAssetPath(room);
            if (string.IsNullOrEmpty(path) || !path.StartsWith(RegionsRoot + "/", StringComparison.Ordinal))
                throw new InvalidOperationException("본편 Regions 아래의 Room 데이터만 이름을 변경할 수 있습니다.");
            string error = AssetDatabase.RenameAsset(path, RequireName(name));
            if (!string.IsNullOrEmpty(error))
                throw new InvalidOperationException("파일명 변경 실패: " + error);
        }

        private static RoomDefinition SaveNewRoom(
            string regionPath, string stem, string id, GameObject root,
            RoomDefinition source, List<string> createdPaths)
        {
            string prefabPath = ContentMakerAssetUtility.UniqueAssetPath(regionPath + "/Prefabs/Rooms", stem, ".prefab");
            string definitionPath = ContentMakerAssetUtility.UniqueAssetPath(regionPath + "/Data/Rooms", stem + "_Definition", ".asset");
            string areaPath = ContentMakerAssetUtility.UniqueAssetPath(regionPath + "/Data/Rooms", stem + "_Area", ".asset");
            RoomDefinition definition = ScriptableObject.CreateInstance<RoomDefinition>();
            AreaDefinition area = ScriptableObject.CreateInstance<AreaDefinition>();
            try
            {
                if (source != null)
                    EditorUtility.CopySerialized(source, definition);
                definition.name = Path.GetFileNameWithoutExtension(definitionPath);
                area.name = Path.GetFileNameWithoutExtension(areaPath);
                Set(definition, "_roomId", p => p.stringValue = id);
                Set(definition, "_areaDefinition", p => p.objectReferenceValue = area);
                Set(area, "_areaId", p => p.stringValue = id);
                Set(area, "_roomDefinition", p => p.objectReferenceValue = definition);
                if (source != null && source.AreaDefinition != null)
                {
                    var sourceArea = new SerializedObject(source.AreaDefinition);
                    string description = sourceArea.FindProperty("_description").stringValue;
                    Set(area, "_description", p => p.stringValue = description);
                }
                CreateAsset(definition, definitionPath, createdPaths);
                CreateAsset(area, areaPath, createdPaths);

                if (source != null)
                    RetargetSelfConnections(root, source, definition, id);
                RequireUnusedPath(prefabPath);
                createdPaths.Add(prefabPath);
                GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                RoomInstance instance = prefab != null ? prefab.GetComponent<RoomInstance>() : null;
                if (instance == null)
                    throw new InvalidOperationException("Room Prefab 생성에 실패했습니다: " + prefabPath);
                Set(definition, "_roomPrefab", p => p.objectReferenceValue = instance);
                ContentMakerAssetUtility.Save(definition);
                area.RefreshMarkerSummary();
                ContentMakerAssetUtility.Save(area);
                return definition;
            }
            finally
            {
                if (definition != null && !EditorUtility.IsPersistent(definition))
                    Object.DestroyImmediate(definition);
                if (area != null && !EditorUtility.IsPersistent(area))
                    Object.DestroyImmediate(area);
            }
        }

        private static GameObject BuildRoom(string name, string id, Vector2 size, bool interior, Sprite sprite, Scene scene)
        {
            GameObject root = new GameObject(name);
            SceneManager.MoveGameObjectToScene(root, scene);
            RoomInstance room = root.AddComponent<RoomInstance>();
            Set(room, "_roomId", p => p.stringValue = id);
            Transform geometry = Child(root.transform, "Geometry");
            Child(root.transform, "Props");
            Child(root.transform, "Actors");
            Transform markers = Child(root.transform, "Markers");
            Transform spawns = Child(markers, "Spawns");
            Child(root.transform, "Event Anchors");
            Child(root.transform, "Systems");
            GameObject floor = Child(geometry, "임시 바닥 - 그림으로 교체").gameObject;
            floor.transform.localScale = new Vector3(size.x, size.y, 1f);
            SpriteRenderer renderer = floor.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.color = interior ? new Color(0.32f, 0.24f, 0.19f) : new Color(0.22f, 0.33f, 0.25f);
            renderer.sortingLayerName = "Background";
            renderer.sortingOrder = -10;

            GameObject boundsObject = Child(root.transform, "CameraBounds").gameObject;
            PolygonCollider2D bounds = boundsObject.AddComponent<PolygonCollider2D>();
            bounds.isTrigger = true;
            Vector2 half = size * 0.5f;
            bounds.points = new[] { new Vector2(-half.x, -half.y), new Vector2(-half.x, half.y),
                new Vector2(half.x, half.y), new Vector2(half.x, -half.y) };
            Set(room, "_cameraBounds", p => p.objectReferenceValue = bounds);
            SpawnPoint spawn = Child(spawns, "Spawn_default").gameObject.AddComponent<SpawnPoint>();
            Set(spawn, "_spawnPointId", p => p.stringValue = "default");
            Set(spawn, "_defaultFacing", p => p.enumValueIndex = (int)FacingDirection.Down);
            return root;
        }

        private static void ReidentifyRoom(GameObject root, string id)
        {
            RoomInstance room = root.GetComponent<RoomInstance>();
            if (room == null)
                throw new InvalidOperationException("원본 Prefab의 루트에 RoomInstance가 없습니다.");
            if (root.GetComponentsInChildren<RoomInstance>(true).Length != 1)
                throw new InvalidOperationException("여러 RoomInstance가 중첩된 Prefab은 자동 복제할 수 없습니다. 단일 Room을 선택하세요.");
            Set(room, "_roomId", p => p.stringValue = id);
            AreaMarkerBase[] markers = root.GetComponentsInChildren<AreaMarkerBase>(true);
            for (int i = 0; i < markers.Length; i++)
            {
                string markerId = id + "." + ContentMakerAssetUtility.MakeId(markers[i].MarkerType.ToString()) + "." + (i + 1);
                Set(markers[i], "markerId", p => p.stringValue = markerId);
                Set(markers[i], "areaId", p => p.stringValue = id);
                if (markers[i] is NPCMarker)
                    Set(markers[i], "npcId", p => p.stringValue = markerId);
                if (markers[i] is SavePointMarker)
                    Set(markers[i], "savePointId", p => p.stringValue = markerId);
            }
            OverworldEnemy[] enemies = root.GetComponentsInChildren<OverworldEnemy>(true);
            for (int i = 0; i < enemies.Length; i++)
                Set(enemies[i], "_enemyId", p => p.stringValue = id + ".enemy." + (i + 1));
            DialogueBattleNPC[] battleNpcs = root.GetComponentsInChildren<DialogueBattleNPC>(true);
            for (int i = 0; i < battleNpcs.Length; i++)
                Set(battleNpcs[i], "_encounterIdOverride", p => p.stringValue = id + ".encounter." + (i + 1));
        }

        private static void RetargetSelfConnections(GameObject root, RoomDefinition source, RoomDefinition copy, string id)
        {
            foreach (AreaConnectionMarker marker in root.GetComponentsInChildren<AreaConnectionMarker>(true))
                RetargetSelfConnection(marker, "mapTransition", source, copy, id);
            foreach (DoorTransition door in root.GetComponentsInChildren<DoorTransition>(true))
                RetargetSelfConnection(door, "_request", source, copy, id);
        }

        private static void RetargetSelfConnection(Object target, string propertyPath, RoomDefinition source, RoomDefinition copy, string id)
        {
            var serialized = new SerializedObject(target);
            SerializedProperty request = serialized.FindProperty(propertyPath);
            SerializedProperty room = request?.FindPropertyRelative("TargetRoom");
            if (room == null || room.objectReferenceValue != source)
                return;
            room.objectReferenceValue = copy;
            request.FindPropertyRelative("TargetRoomId").stringValue = id;
            request.FindPropertyRelative("TargetAreaId").stringValue = id;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            if (PrefabUtility.IsPartOfPrefabInstance(target))
                PrefabUtility.RecordPrefabInstancePropertyModifications(target);
        }

        private static List<RoomDefinition> GetRegionRooms(string regionPath)
        {
            string[] guids = AssetDatabase.FindAssets("t:RoomDefinition", new[] { regionPath });
            var rooms = new List<RoomDefinition>();
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (string guid in guids)
            {
                RoomDefinition room = AssetDatabase.LoadAssetAtPath<RoomDefinition>(AssetDatabase.GUIDToAssetPath(guid));
                if (room == null || !room.IsValid)
                    throw new InvalidOperationException("지역 안에 유효하지 않은 Room 데이터가 있습니다. 먼저 해당 Room의 Prefab 연결을 확인하세요.");
                if (!ids.Add(room.RoomId))
                    throw new InvalidOperationException("지역 안의 Room ID가 중복됩니다: " + room.RoomId);
                rooms.Add(room);
            }
            return rooms;
        }

        private static void MergeRoom(List<RoomDefinition> rooms, Dictionary<string, RoomDefinition> ids, RoomDefinition room)
        {
            RequireRoom(room);
            if (ids.TryGetValue(room.RoomId, out RoomDefinition existing))
            {
                if (existing != room)
                    throw new InvalidOperationException("서로 다른 Room 데이터의 ID가 같습니다: " + room.RoomId);
                return;
            }
            ids.Add(room.RoomId, room);
            rooms.Add(room);
        }

        private static Scene FindLoadedScene(string path)
        {
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene scene = SceneManager.GetSceneAt(i);
                if (scene.isLoaded && string.Equals(scene.path, path, StringComparison.OrdinalIgnoreCase))
                    return scene;
            }
            return default;
        }

        private static T[] FindComponents<T>(Scene scene) where T : Component
        {
            var result = new List<T>();
            foreach (GameObject root in scene.GetRootGameObjects())
                result.AddRange(root.GetComponentsInChildren<T>(true));
            return result.ToArray();
        }

        private static string NewRoomId(string regionPath, string name)
        {
            string stem = ContentMakerAssetUtility.MakeId(Path.GetFileName(regionPath)) + "." + ContentMakerAssetUtility.MakeId(name);
            var used = new HashSet<string>(StringComparer.Ordinal);
            foreach (string guid in AssetDatabase.FindAssets("t:RoomDefinition"))
            {
                RoomDefinition room = AssetDatabase.LoadAssetAtPath<RoomDefinition>(AssetDatabase.GUIDToAssetPath(guid));
                if (room != null)
                    used.Add(room.RoomId);
            }
            string candidate = stem;
            for (int suffix = 2; used.Contains(candidate); suffix++)
                candidate = stem + "." + suffix;
            return candidate;
        }

        private static void EnsureRegionFolders(string path)
        {
            string[] folders = { "Scenes", "Prefabs/Rooms", "Prefabs/NPCs", "Data/Rooms", "Data/Dialogue", "Notes" };
            foreach (string folder in folders)
                ContentMakerAssetUtility.EnsureFolder(path + "/" + folder);
        }

        private static string RequireName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new InvalidOperationException("이름을 입력하세요.");
            string result = ContentMakerAssetUtility.MakeFileName(name.Trim());
            if (string.IsNullOrWhiteSpace(result) || result == "." || result == "..")
                throw new InvalidOperationException("파일명으로 사용할 수 없는 이름입니다.");
            return result;
        }

        private static void RequireRegion(string path)
        {
            if (string.IsNullOrEmpty(path) || !path.StartsWith(RegionsRoot + "/", StringComparison.Ordinal)
                || path.IndexOf("..", StringComparison.Ordinal) >= 0 || !AssetDatabase.IsValidFolder(path))
                throw new InvalidOperationException("Regions 아래에서 작업할 지역을 먼저 선택하세요.");
        }

        private static void RequireRoom(RoomDefinition room)
        {
            if (room == null || !room.IsValid || !EditorUtility.IsPersistent(room.RoomPrefab))
                throw new InvalidOperationException("Room 데이터와 Room Prefab 연결을 먼저 확인하세요.");
        }

        private static void EnsureEditMode()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("콘텐츠 생성·구조 변경은 Play Mode를 종료한 뒤 사용하세요.");
        }

        private static void RequireMainStage()
        {
            if (PrefabStageUtility.GetCurrentPrefabStage() != null)
                throw new InvalidOperationException("Prefab Mode에서 편집 내용을 저장하고 나간 뒤 새 Room 또는 지역 Scene을 만들어 주세요.");
        }

        private static T RequireAsset<T>(string path) where T : Object
        {
            T asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset == null)
                throw new InvalidOperationException("필수 공용 자산을 찾을 수 없습니다: " + path);
            return asset;
        }

        private static GameObject InstantiateInScene(GameObject prefab, Scene scene)
        {
            GameObject result = PrefabUtility.InstantiatePrefab(prefab, scene) as GameObject;
            if (result == null)
                throw new InvalidOperationException("공용 Prefab 생성에 실패했습니다: " + prefab.name);
            return result;
        }

        private static Transform Child(Transform parent, string name)
        {
            var child = new GameObject(name);
            SceneManager.MoveGameObjectToScene(child, parent.gameObject.scene);
            child.transform.SetParent(parent, false);
            return child.transform;
        }

        private static void Set(Object target, string field, Action<SerializedProperty> assign)
        {
            var serialized = new SerializedObject(target);
            SerializedProperty property = serialized.FindProperty(field);
            if (property == null)
                throw new InvalidOperationException(target.GetType().Name + "의 필드를 찾을 수 없습니다: " + field);
            assign(property);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            if (PrefabUtility.IsPartOfPrefabInstance(target))
                PrefabUtility.RecordPrefabInstancePropertyModifications(target);
        }

        private static void RequireUnusedPath(string path)
        {
            if (AssetDatabase.LoadMainAssetAtPath(path) != null || File.Exists(path)
                || File.Exists(path + ".meta") || Directory.Exists(path))
                throw new InvalidOperationException("기존 자산을 덮어쓰지 않습니다: " + path);
        }

        private static void CreateAsset(Object asset, string path, List<string> createdPaths)
        {
            RequireUnusedPath(path);
            createdPaths.Add(path);
            AssetDatabase.CreateAsset(asset, path);
        }

        private static void RestoreScene(Scene previous, Scene temporary)
        {
            if (previous.IsValid() && previous.isLoaded)
                SceneManager.SetActiveScene(previous);
            if (temporary.IsValid() && temporary.isLoaded)
                EditorSceneManager.CloseScene(temporary, true);
        }

        private static void RollbackCreatedAssets(List<string> paths)
        {
            for (int i = paths.Count - 1; i >= 0; i--)
            {
                if (!AssetDatabase.DeleteAsset(paths[i]) && File.Exists(paths[i]))
                    Debug.LogError("[Content Maker] 실패한 생성 자산을 정리하지 못했습니다: " + paths[i]);
            }
        }
    }
}
