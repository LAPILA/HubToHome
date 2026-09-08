using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace HubToHome.EditorTools.ContentMaker
{
    internal enum ContentMakerMarkerKind
    {
        Spawn, Door, Npc, BattleNpc, Sign, Enemy, Item, Save, Hazard, Puzzle, Vendor, Shortcut, Plot, Sublocation
    }

    internal sealed class ContentMakerMarkerRequest
    {
        public ContentMakerMarkerKind Kind;
        public string Name;
        public Vector2 Position;
        public Vector2 TriggerSize = Vector2.one;
        public FacingDirection Facing = FacingDirection.Down;
        public RoomDefinition Destination;
        public string DestinationSpawn;
        public DoorActivationMode Activation = DoorActivationMode.OnInteract;
        public DialogueData Dialogue;
        public EnemyData Enemy;
        public ItemData Item;
        public ShopDefinition Shop;
        public int Amount = 1;
        public Sprite Sprite;
        public GameObject VisualPrefab;
        public bool SaveNpcPrefab = true;
    }

    // All writes are restricted to the explicitly selected Room's open Prefab Stage.
    internal static class ContentMakerMarkerService
    {
        public static readonly string[] KindNames =
        {
            "도착 시작점", "다른 방으로 가는 문", "일반 대화 NPC", "대화 선택지로 전투하는 NPC",
            "표지판", "전투 조우", "아이템 줍기", "저장 지점", "위험 구역", "퍼즐 연결",
            "상점 연결", "잠금 / 지름길 문", "자동 대화 이벤트", "별도 공간 왕복"
        };

        public static readonly string[] KindDescriptions =
        {
            "문에서 도착할 위치입니다. 위치와 방향을 정하면 중복 없는 시작점 ID를 붙입니다.",
            "대상 방과 그 안의 시작점을 선택합니다. 반대편 문은 별도로 만들어 연결합니다.",
            "상호작용 키로 실제 DialogueData를 재생합니다. 기본은 반복 대화이며 대본 원본을 공유합니다.",
            "DialogueData의 '전투 시작' 선택지로 지정한 적과 싸웁니다. 자동 접근 / 공격 연출은 켜지지 않습니다.",
            "누르면 읽는 안내판입니다. 대화 자산을 연결하고 별도 화자 없이 본문만 사용할 수도 있습니다.",
            "선택한 적 데이터로 전투를 요청합니다. 심리스 전투에는 방의 공용 전투 호스트가 필요합니다.",
            "기존 ItemData의 ID로 아이템을 지급합니다. 플레이에서 쓰는 아이템 카탈로그에 등록된 자산을 선택하세요.",
            "상호작용하면 지정 슬롯에 저장합니다. 접촉 자동 저장과 슬롯은 생성 뒤 상세 설정에서 조정합니다.",
            "플레이어가 닿으면 파티 피해와 넉백을 적용합니다. 피해량과 재피격 간격은 상세 설정에서 조정합니다.",
            "기존 IPuzzleRuntime 구현을 연결하는 지점입니다. 생성 후 퍼즐 Runtime과 안내 대화를 연결해야 동작합니다.",
            "ShopDefinition을 연결합니다. 실제 상점 화면은 기존 Shop Session Launcher가 씬에 준비되어야 합니다.",
            "문 목적지를 먼저 연결합니다. 생성 뒤 잠금 플래그와 반대편 문 ID를 상세 설정에 입력하세요.",
            "영역에 들어오면 대화를 재생합니다. 1회성 / 완료 플래그는 생성 뒤 상세 설정에서 조정합니다.",
            "다른 씬의 공간을 방문하고 복귀 주소를 기억합니다. 대상 씬 / 방 / 도착점 / 복귀점을 상세 설정에서 연결하세요."
        };

        private static readonly Type[] ComponentTypes =
        {
            typeof(SpawnPoint), typeof(AreaConnectionMarker), typeof(NPCMarker), typeof(DialogueBattleNPC),
            typeof(SignMarker), typeof(OverworldEnemyMarker), typeof(ItemPickupMarker), typeof(SavePointMarker),
            typeof(HazardMarker), typeof(PuzzleMarker), typeof(VendorMarker), typeof(ShortcutDoorMarker),
            typeof(PlotPointMarker), typeof(SublocationMarker)
        };

        public static bool TryGetEditableRoom(RoomDefinition definition, out RoomInstance room, out string error)
        {
            room = null;
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                error = "플레이를 종료한 뒤 제작할 수 있습니다.";
                return false;
            }
            if (definition == null || definition.RoomPrefab == null)
            {
                error = "왼쪽 목록에서 방을 선택하세요.";
                return false;
            }
            if (string.IsNullOrWhiteSpace(definition.RoomId))
            {
                error = "선택한 RoomDefinition의 Room ID가 비어 있습니다. 맵 탭에서 먼저 설정하세요.";
                return false;
            }
            PrefabStage stage = PrefabStageUtility.GetCurrentPrefabStage();
            string assetPath = AssetDatabase.GetAssetPath(definition.RoomPrefab);
            if (stage == null || stage.prefabContentsRoot == null || stage.assetPath != assetPath)
            {
                error = "선택한 방을 프리팹 편집 모드로 열어야 배치 / 편집할 수 있습니다. 다른 씬이나 프리팹은 수정하지 않습니다.";
                return false;
            }
            room = stage.prefabContentsRoot.GetComponent<RoomInstance>();
            if (room == null)
            {
                error = "Room Prefab 루트에 RoomInstance가 없습니다. 맵 검사에서 구성을 확인하세요.";
                return false;
            }
            if (!string.Equals(room.RoomId, definition.RoomId, StringComparison.Ordinal))
            {
                error = "Room Prefab과 RoomDefinition의 Room ID가 다릅니다. 맵 검사에서 연결을 확인한 뒤 배치하세요.";
                room = null;
                return false;
            }
            error = string.Empty;
            return true;
        }

        public static void OpenRoom(RoomDefinition definition)
        {
            if (definition == null || definition.RoomPrefab == null)
                throw new InvalidOperationException("열 방 프리팹이 없습니다.");
            PrefabStageUtility.OpenPrefab(AssetDatabase.GetAssetPath(definition.RoomPrefab));
        }

        public static string[] GetSpawnIds(RoomDefinition destination)
        {
            if (destination == null || destination.RoomPrefab == null) return Array.Empty<string>();
            RoomInstance source = destination.RoomPrefab;
            if (TryGetEditableRoom(destination, out RoomInstance opened, out _)) source = opened;
            SpawnPoint[] points = source.GetComponentsInChildren<SpawnPoint>(true);
            var values = new List<string>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (SpawnPoint point in points)
            {
                string id = point.SpawnPointId?.Trim();
                if (!string.IsNullOrEmpty(id) && seen.Add(id)) values.Add(id);
            }
            values.Sort(StringComparer.Ordinal);
            return values.ToArray();
        }

        public static string ValidateVisual(GameObject prefab)
        {
            if (prefab == null) return string.Empty;
            if (!EditorUtility.IsPersistent(prefab) || !PrefabUtility.IsPartOfPrefabAsset(prefab))
                return "외형은 프로젝트 창의 프리팹을 선택하세요. 씬 오브젝트는 원본으로 사용할 수 없습니다.";
            foreach (Component component in prefab.GetComponentsInChildren<Component>(true))
            {
                if (component == null) return "외형 프리팹에 누락된 스크립트가 있습니다.";
                if (component is Collider2D || component is Collider || component is Rigidbody2D || component is Rigidbody
                    || component is MonoBehaviour || component is Camera || component is AudioSource)
                    return "외형 전용 Prefab만 넣을 수 있습니다. 스크립트 / Collider / Rigidbody / Camera / AudioSource가 있는 전투 원본은 직접 중첩하지 말고 Sprite 또는 외형 전용 Prefab을 선택하세요. 원본을 제거 / 수정하지 않습니다.";
            }
            return string.Empty;
        }

        public static Component Create(ContentMakerContext context, ContentMakerMarkerRequest request)
        {
            if (!TryGetEditableRoom(context.Room, out RoomInstance room, out string error))
                throw new InvalidOperationException(error);
            if (request == null || (int)request.Kind < 0 || (int)request.Kind >= ComponentTypes.Length)
                throw new ArgumentException("만들 마커 종류가 올바르지 않습니다.");
            if (string.IsNullOrWhiteSpace(request.Name)) throw new ArgumentException("이름을 입력하세요.");
            if (!float.IsFinite(request.Position.x) || !float.IsFinite(request.Position.y)
                || !float.IsFinite(request.TriggerSize.x) || !float.IsFinite(request.TriggerSize.y)
                || request.TriggerSize.x <= 0 || request.TriggerSize.y <= 0)
                throw new ArgumentException("위치는 유효한 숫자, 범위 크기는 0보다 큰 숫자여야 합니다.");
            error = request.Kind == ContentMakerMarkerKind.Spawn ? string.Empty : ValidateVisual(request.VisualPrefab);
            if (!string.IsNullOrEmpty(error)) throw new ArgumentException(error);
            if (RequiresDialogue(request.Kind) && request.Dialogue == null)
                throw new ArgumentException("실행할 대화 자산을 먼저 연결하세요. 대사 탭에서 새로 만들 수도 있습니다.");
            if ((request.Kind == ContentMakerMarkerKind.BattleNpc || request.Kind == ContentMakerMarkerKind.Enemy) && request.Enemy == null)
                throw new ArgumentException("전투에 사용할 EnemyData를 선택하세요.");
            if (request.Kind == ContentMakerMarkerKind.Item && (request.Item == null || string.IsNullOrWhiteSpace(request.Item.ItemID)))
                throw new ArgumentException("ID가 있는 ItemData를 선택하세요.");
            if (request.Kind == ContentMakerMarkerKind.Vendor && request.Shop == null)
                throw new ArgumentException("연결할 ShopDefinition을 선택하세요.");
            if (IsDoor(request.Kind))
            {
                if (request.Destination == null || !request.Destination.IsValid
                    || Array.IndexOf(GetSpawnIds(request.Destination), request.DestinationSpawn) < 0)
                    throw new ArgumentException("대상 방과 그 안에 존재하는 도착 시작점을 선택하세요.");
            }

            bool saveNpc = IsNpc(request.Kind) && request.SaveNpcPrefab;
            string npcAssetPath = null;
            if (saveNpc)
            {
                string regionPath = ContentMakerAssetUtility.NormalizeContentPath(context.RegionPath);
                if (!regionPath.StartsWith(ContentMakerAssetUtility.RegionsRoot + "/", StringComparison.Ordinal))
                    throw new ArgumentException("NPC 프리팹은 선택한 지역 폴더 아래에 저장해야 합니다.");
                npcAssetPath = ContentMakerAssetUtility.UniqueAssetPath(regionPath + "/Prefabs/NPCs", "NPC_" + request.Name, ".prefab");
            }

            Undo.IncrementCurrentGroup();
            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("콘텐츠 메이커: " + request.Name + " 배치");
            GameObject created = null;
            try
            {
                string group = request.Kind == ContentMakerMarkerKind.Spawn ? "Spawns" : IsNpc(request.Kind) ? "Actors" : "Markers";
                Transform parent = EnsureGroup(room.transform, group);
                created = new GameObject(request.Name.Trim());
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(created, room.gameObject.scene);
                Undo.RegisterCreatedObjectUndo(created, "마커 생성");
                Undo.SetTransformParent(created.transform, parent, "Room 안에 배치");
                created.transform.position = room.transform.TransformPoint(request.Position);
                created.transform.localRotation = Quaternion.identity;
                created.transform.localScale = Vector3.one;
                if (request.Kind != ContentMakerMarkerKind.Spawn)
                {
                    int layer = LayerMask.NameToLayer("Interactable");
                    if (layer < 0) throw new InvalidOperationException("Interactable 레이어가 없습니다. 프로젝트 레이어 설정을 확인하세요.");
                    created.layer = layer;
                    BoxCollider2D collider = Undo.AddComponent<BoxCollider2D>(created);
                    collider.isTrigger = true;
                    collider.size = request.TriggerSize;
                }
                Component component = Undo.AddComponent(created, ComponentTypes[(int)request.Kind]);
                Configure(component, context.Room.RoomId, room, request);
                if (request.Kind != ContentMakerMarkerKind.Spawn)
                    AddVisual(created.transform, request.VisualPrefab, request.Sprite);
                if (saveNpc)
                {
                    // Native prefab API preserves nested visual instances and their source references.
                    PrefabUtility.SaveAsPrefabAssetAndConnect(created, npcAssetPath, InteractionMode.UserAction, out bool saved);
                    if (!saved) throw new InvalidOperationException("NPC 프리팹을 저장하지 못했습니다: " + npcAssetPath);
                }
                EditorSceneManager.MarkSceneDirty(room.gameObject.scene);
                Undo.CollapseUndoOperations(undoGroup);
                return component;
            }
            catch
            {
                Undo.RevertAllDownToGroup(undoGroup);
                // This path was generated uniquely by this operation, never a pre-existing user asset.
                if (npcAssetPath != null && AssetDatabase.LoadMainAssetAtPath(npcAssetPath) != null)
                    AssetDatabase.DeleteAsset(npcAssetPath);
                throw;
            }
        }

        public static bool RequiresDialogue(ContentMakerMarkerKind kind) => IsNpc(kind)
            || kind == ContentMakerMarkerKind.Sign || kind == ContentMakerMarkerKind.Plot;
        public static bool IsNpc(ContentMakerMarkerKind kind) => kind == ContentMakerMarkerKind.Npc || kind == ContentMakerMarkerKind.BattleNpc;
        public static bool IsDoor(ContentMakerMarkerKind kind) => kind == ContentMakerMarkerKind.Door || kind == ContentMakerMarkerKind.Shortcut;

        public static bool HasBattleChoice(DialogueData dialogue)
        {
            var visited = new HashSet<DialogueData>();
            var pending = new Stack<DialogueData>();
            if (dialogue != null) pending.Push(dialogue);
            while (pending.Count > 0)
            {
                DialogueData current = pending.Pop();
                if (!visited.Add(current) || current.Nodes == null) continue;
                foreach (DialogueNode node in current.Nodes)
                {
                    if (node == null || !node.IsChoiceNode || node.Choices == null || node.Choices.Count == 0) continue;
                    foreach (ChoiceData choice in node.Choices)
                    {
                        if (choice == null) continue;
                        if (choice.StartBattleEncounter) return true;
                        if (choice.NextDialogue != null) pending.Push(choice.NextDialogue);
                    }
                    // DialogueManager ends or branches at the first choice node.
                    break;
                }
            }
            return false;
        }

        private static Transform EnsureGroup(Transform root, string name)
        {
            Transform existing = root.Find(name);
            if (existing != null) return existing;
            if (name == "Spawns")
            {
                existing = root.Find("Markers/Spawns");
                if (existing != null) return existing;
                root = EnsureGroup(root, "Markers");
            }
            var group = new GameObject(name);
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(group, root.gameObject.scene);
            Undo.RegisterCreatedObjectUndo(group, "Room 배치 폴더 생성");
            Undo.SetTransformParent(group.transform, root, "Room 배치 폴더 연결");
            group.transform.localPosition = Vector3.zero;
            group.transform.localRotation = Quaternion.identity;
            group.transform.localScale = Vector3.one;
            return group.transform;
        }

        private static void Configure(Component component, string roomId, RoomInstance room, ContentMakerMarkerRequest request)
        {
            var serialized = new SerializedObject(component);
            string id = roomId + "." + ContentMakerAssetUtility.MakeId(request.Name) + "." + Guid.NewGuid().ToString("N").Substring(0, 12);
            SetString(serialized, "markerId", id);
            SetString(serialized, "areaId", roomId);
            SetString(serialized, "displayName", request.Name.Trim());
            switch (request.Kind)
            {
                case ContentMakerMarkerKind.Spawn:
                    var used = new HashSet<string>(StringComparer.Ordinal);
                    foreach (SpawnPoint point in room.GetComponentsInChildren<SpawnPoint>(true))
                        if (point != component && !string.IsNullOrWhiteSpace(point.SpawnPointId)) used.Add(point.SpawnPointId.Trim());
                    string baseId = ContentMakerAssetUtility.MakeId(request.Name);
                    string spawnId = baseId;
                    for (int index = 2; used.Contains(spawnId); index++) spawnId = baseId + "_" + index;
                    SetString(serialized, "_spawnPointId", spawnId);
                    serialized.FindProperty("_defaultFacing").intValue = (int)request.Facing;
                    break;
                case ContentMakerMarkerKind.Door:
                case ContentMakerMarkerKind.Shortcut:
                    SetConnection(serialized, request);
                    if (request.Kind == ContentMakerMarkerKind.Shortcut) SetString(serialized, "doorId", id);
                    break;
                case ContentMakerMarkerKind.Npc:
                    SetString(serialized, "npcId", id);
                    SetString(serialized, "dialogueId", request.Dialogue.name);
                    serialized.FindProperty("dialogueData").objectReferenceValue = request.Dialogue;
                    break;
                case ContentMakerMarkerKind.BattleNpc:
                    serialized.FindProperty("_dialogue").objectReferenceValue = request.Dialogue;
                    SerializedProperty enemies = serialized.FindProperty("_fallbackEncounterEnemies");
                    enemies.arraySize = 1;
                    enemies.GetArrayElementAtIndex(0).objectReferenceValue = request.Enemy;
                    SetString(serialized, "_encounterIdOverride", id);
                    serialized.FindProperty("_useDedicatedBattleScene").boolValue = false;
                    serialized.FindProperty("_useStagedEncounter").boolValue = false;
                    break;
                case ContentMakerMarkerKind.Sign:
                case ContentMakerMarkerKind.Plot:
                    serialized.FindProperty("dialogueData").objectReferenceValue = request.Dialogue;
                    SetString(serialized, "plotId", id);
                    break;
                case ContentMakerMarkerKind.Enemy:
                    serialized.FindProperty("enemyData").objectReferenceValue = request.Enemy;
                    SetString(serialized, "enemyId", request.Enemy.EnemyId);
                    SetString(serialized, "battleEncounterId", id);
                    serialized.FindProperty("useDedicatedBattleScene").boolValue = false;
                    break;
                case ContentMakerMarkerKind.Item:
                    SetString(serialized, "itemId", request.Item.ItemID);
                    serialized.FindProperty("amount").intValue = Mathf.Max(1, request.Amount);
                    break;
                case ContentMakerMarkerKind.Save: SetString(serialized, "savePointId", id); break;
                case ContentMakerMarkerKind.Vendor:
                    SetString(serialized, "vendorId", id);
                    SetString(serialized, "shopId", request.Shop.ShopId);
                    serialized.FindProperty("shopDefinition").objectReferenceValue = request.Shop;
                    break;
                case ContentMakerMarkerKind.Sublocation: SetString(serialized, "sublocationId", id); break;
            }
            serialized.ApplyModifiedProperties();
        }

        private static void SetConnection(SerializedObject serialized, ContentMakerMarkerRequest request)
        {
            SerializedProperty transition = serialized.FindProperty("mapTransition");
            transition.FindPropertyRelative("TransitionType").intValue = (int)MapTransitionType.Room;
            transition.FindPropertyRelative("TargetRoom").objectReferenceValue = request.Destination;
            transition.FindPropertyRelative("TargetSpawnPointId").stringValue = request.DestinationSpawn;
            transition.FindPropertyRelative("FacingAfterEnter").intValue = (int)request.Facing;
            serialized.FindProperty("activationMode").intValue = (int)request.Activation;
            serialized.FindProperty("interactToUse").boolValue = request.Activation != DoorActivationMode.OnTriggerEnter;
        }

        private static void SetString(SerializedObject serialized, string field, string value)
        {
            SerializedProperty property = serialized.FindProperty(field);
            if (property != null) property.stringValue = value ?? string.Empty;
        }

        private static void AddVisual(Transform parent, GameObject visualPrefab, Sprite sprite)
        {
            if (visualPrefab != null)
            {
                var visual = (GameObject)PrefabUtility.InstantiatePrefab(visualPrefab, parent);
                Undo.RegisterCreatedObjectUndo(visual, "외형 Prefab 연결");
                visual.transform.localPosition = Vector3.zero;
                // Authored scale, rotation, renderer settings and animation are preserved.
                return;
            }
            if (sprite == null) return;
            var spriteObject = new GameObject("Visual");
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(spriteObject, parent.gameObject.scene);
            Undo.RegisterCreatedObjectUndo(spriteObject, "외형 Sprite 추가");
            Undo.SetTransformParent(spriteObject.transform, parent, "외형 연결");
            spriteObject.transform.localPosition = Vector3.zero;
            spriteObject.transform.localRotation = Quaternion.identity;
            SpriteRenderer renderer = Undo.AddComponent<SpriteRenderer>(spriteObject);
            renderer.sprite = sprite;
            renderer.sortingLayerName = "Characters";
        }
    }
}
