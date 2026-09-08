using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace HubToHome.EditorTools.ContentMaker
{
    internal sealed class ContentMakerMarkerPanel : IDisposable
    {
        private static readonly string[] FacingLabels = { "아래", "위", "왼쪽", "오른쪽", "현재 방향 유지" };
        private static readonly string[] ActivationLabels = { "영역에 닿으면 이동", "상호작용 키로 이동", "접촉 또는 상호작용" };
        private readonly ContentMakerMarkerRequest _request = new ContentMakerMarkerRequest { Name = "도착점" };
        private readonly List<Component> _members = new List<Component>();
        private readonly List<RoomDefinition> _destinations = new List<RoomDefinition>();
        private string[] _destinationLabels = Array.Empty<string>();
        private string[] _spawnIds = Array.Empty<string>();
        private RoomDefinition _editDestination;
        private string[] _editSpawnIds = Array.Empty<string>();
        private bool _dirty = true;
        private bool _assetsDirty = true;
        private bool _selectionPending = true;
        private bool _showCreate = true;
        private bool _showAdvanced;
        private int _roomInstanceId;
        private string _regionPath;
        private string _filter = string.Empty;
        private Component _selected;
        private UnityEditor.Editor _componentEditor;
        private GUIStyle _memberButtonStyle;
        private bool _memberStyleDark;
        private Vector2 _listScroll;
        private bool _disposed;

        public ContentMakerMarkerPanel()
        {
            EditorApplication.hierarchyChanged += OnHierarchyChanged;
            EditorApplication.projectChanged += OnProjectChanged;
            Undo.undoRedoPerformed += OnHierarchyChanged;
            Selection.selectionChanged += OnSelectionChanged;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            EditorApplication.hierarchyChanged -= OnHierarchyChanged;
            EditorApplication.projectChanged -= OnProjectChanged;
            Undo.undoRedoPerformed -= OnHierarchyChanged;
            Selection.selectionChanged -= OnSelectionChanged;
            if (_componentEditor != null) Object.DestroyImmediate(_componentEditor);
        }

        public void OnGUI(ContentMakerContext context)
        {
            ContentMakerGUI.Header("마커 / NPC 배치", "방 열기 → 종류와 대화 / 목적지 선택 → 배치 → 씬 뷰에서 위치 조정\n대사는 공유 자산을 연결합니다. 방은 프리팹 편집 모드에서 Ctrl+S로 저장하세요.");
            bool editable = ContentMakerMarkerService.TryGetEditableRoom(context.Room, out RoomInstance room, out string error);
            if (!editable)
            {
                using (ContentMakerGUI.Card())
                {
                    EditorGUILayout.LabelField(error, ContentMakerGUI.Body);
                    EditorGUILayout.Space(8f);
                    using (new EditorGUI.DisabledScope(context.Room == null || context.Room.RoomPrefab == null || EditorApplication.isPlayingOrWillChangePlaymode))
                    {
                        if (ContentMakerGUI.PrimaryButton("선택한 방 열기"))
                            Run(context, () => ContentMakerMarkerService.OpenRoom(context.Room));
                    }
                }
                return;
            }

            RefreshIfNeeded(context, room);
            DrawOpenRoomHeader(room);
            _showCreate = ContentMakerGUI.Foldout(_showCreate, "새 마커 / NPC 만들기");
            if (_showCreate) DrawCreate(context, room);
            EditorGUILayout.Space(10);
            DrawMemberList(context, room);
            if (_selected != null && _selected.transform.IsChildOf(room.transform)) DrawSelected(context, room);
        }

        private void DrawOpenRoomHeader(RoomInstance room)
        {
            using (ContentMakerGUI.Card())
            {
                EditorGUILayout.LabelField("편집 중", ContentMakerGUI.Muted);
                EditorGUILayout.LabelField(room.name, ContentMakerGUI.SectionTitle);
                EditorGUILayout.Space(6f);
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (ContentMakerGUI.SecondaryButton("방 전체 보기")) Focus(room);
                    if (ContentMakerGUI.SecondaryButton("목록 새로고침"))
                    {
                        _dirty = true;
                        _assetsDirty = true;
                    }
                }
            }
        }

        private void DrawCreate(ContentMakerContext context, RoomInstance room)
        {
            using (ContentMakerGUI.Card())
            {
                int kind = EditorGUILayout.Popup("종류", (int)_request.Kind, ContentMakerMarkerService.KindNames);
                if (kind != (int)_request.Kind)
                {
                    _request.Kind = (ContentMakerMarkerKind)kind;
                    _request.Name = DefaultName(_request.Kind);
                    if (_request.Dialogue == null) _request.Dialogue = context.Dialogue;
                }
                EditorGUILayout.LabelField(ContentMakerMarkerService.KindDescriptions[kind], ContentMakerGUI.Muted);
                EditorGUILayout.Space(8f);
                _request.Name = EditorGUILayout.TextField("이름", _request.Name);
                _request.Position = EditorGUILayout.Vector2Field("방 기준 위치", _request.Position);
                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.FlexibleSpace();
                    if (ContentMakerGUI.SecondaryButton("씬 뷰 중앙에 놓기", GUILayout.Width(160f)))
                    {
                        SceneView view = SceneView.lastActiveSceneView;
                        if (view != null) _request.Position = room.transform.InverseTransformPoint(view.pivot);
                    }
                }
                if (_request.Kind != ContentMakerMarkerKind.Spawn)
                {
                    _request.TriggerSize = EditorGUILayout.Vector2Field("상호작용 / 접촉 범위", _request.TriggerSize);
                    _request.TriggerSize = new Vector2(Mathf.Max(.05f, _request.TriggerSize.x), Mathf.Max(.05f, _request.TriggerSize.y));
                }
                if (_request.Kind == ContentMakerMarkerKind.Spawn || ContentMakerMarkerService.IsDoor(_request.Kind))
                {
                    int facing = _request.Facing == FacingDirection.Keep ? 4 : Mathf.Clamp((int)_request.Facing, 0, 3);
                    int chosenFacing = EditorGUILayout.Popup("도착 후 방향", facing, FacingLabels);
                    _request.Facing = chosenFacing == 4 ? FacingDirection.Keep : (FacingDirection)chosenFacing;
                }
                if (ContentMakerMarkerService.IsDoor(_request.Kind)) DrawDestination();
                if (ContentMakerMarkerService.RequiresDialogue(_request.Kind))
                {
                    EditorGUILayout.Space(8f);
                    EditorGUILayout.LabelField("대화 연결", ContentMakerGUI.SectionTitle);
                    _request.Dialogue = (DialogueData)EditorGUILayout.ObjectField("실행할 대화", _request.Dialogue, typeof(DialogueData), false);
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        if (context.Dialogue != null && ContentMakerGUI.SecondaryButton("대사 탭에서 선택한 대화 연결", GUILayout.MinWidth(0f))) _request.Dialogue = context.Dialogue;
                        using (new EditorGUI.DisabledScope(_request.Dialogue == null))
                            if (ContentMakerGUI.SecondaryButton("이 대화 편집", GUILayout.MinWidth(0f))) context.Select(_request.Dialogue);
                    }
                    if (_request.Kind == ContentMakerMarkerKind.BattleNpc
                        && _request.Dialogue != null && !ContentMakerMarkerService.HasBattleChoice(_request.Dialogue))
                        EditorGUILayout.HelpBox("이 대화 흐름에는 전투 시작 선택지가 없습니다. '이 대화 편집' → 선택지 추가 → '전투 시작'을 켜세요. 현재 상태로는 대화만 끝납니다.", MessageType.Warning);
                }
                if (_request.Kind == ContentMakerMarkerKind.BattleNpc || _request.Kind == ContentMakerMarkerKind.Enemy)
                {
                    _request.Enemy = (EnemyData)EditorGUILayout.ObjectField("전투할 적", _request.Enemy, typeof(EnemyData), false);
                    EditorGUILayout.LabelField("기본값은 맵에서 바로 시작하는 심리스 전투입니다. 방에 전투 호스트(SeamlessBattleHost)가 필요합니다. 추가 적 / 배경음악 / 전용 씬 / 도주 허용은 배치 후 상세 설정에서 지정합니다.", ContentMakerGUI.Body);
                }
                if (_request.Kind == ContentMakerMarkerKind.Item)
                {
                    _request.Item = (ItemData)EditorGUILayout.ObjectField("지급할 아이템", _request.Item, typeof(ItemData), false);
                    _request.Amount = Mathf.Max(1, EditorGUILayout.IntField("수량", _request.Amount));
                }
                if (_request.Kind == ContentMakerMarkerKind.Vendor)
                    _request.Shop = (ShopDefinition)EditorGUILayout.ObjectField("상점 데이터", _request.Shop, typeof(ShopDefinition), false);
                if (_request.Kind != ContentMakerMarkerKind.Spawn) DrawVisual();
                if (ContentMakerMarkerService.IsNpc(_request.Kind))
                {
                    _request.SaveNpcPrefab = EditorGUILayout.Toggle("NPC 프리팹도 함께 만들기", _request.SaveNpcPrefab);
                    if (_request.SaveNpcPrefab)
                    {
                        EditorGUILayout.LabelField("저장 폴더 · " + (context.RegionPath ?? "지역을 선택하세요") + "/Prefabs/NPCs", ContentMakerGUI.Muted);
                        EditorGUILayout.LabelField("같은 이름은 새 파일로 구분됩니다. Ctrl+Z로 배치를 취소해도 만들어진 프리팹 파일은 보관됩니다.", ContentMakerGUI.Muted);
                    }
                }
                string problem = GetCreationProblem();
                EditorGUILayout.Space(8f);
                if (!string.IsNullOrEmpty(problem)) EditorGUILayout.HelpBox(problem, MessageType.Warning);
                using (new EditorGUI.DisabledScope(!string.IsNullOrEmpty(problem)))
                {
                    if (ContentMakerGUI.PrimaryButton("선택한 방에 배치"))
                        Run(context, () =>
                        {
                            Component created = ContentMakerMarkerService.Create(context, _request);
                            SetSelected(created);
                            Focus(created);
                            _dirty = true;
                            _assetsDirty = true;
                            context.RefreshAssets();
                            context.Report("배치했습니다. 위치와 필수 상세 설정을 확인한 뒤 방 프리팹을 저장하세요.");
                        });
                }
            }
        }

        private void DrawDestination()
        {
            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("이동 목적지", ContentMakerGUI.SectionTitle);
            using (new EditorGUILayout.HorizontalScope())
            {
                int index = _destinations.IndexOf(_request.Destination);
                int chosen = EditorGUILayout.Popup("도착할 방", index + 1, _destinationLabels);
                RoomDefinition destination = chosen > 0 && chosen <= _destinations.Count ? _destinations[chosen - 1] : null;
                if (destination != _request.Destination)
                {
                    _request.Destination = destination;
                    _spawnIds = ContentMakerMarkerService.GetSpawnIds(destination);
                    _request.DestinationSpawn = _spawnIds.Length > 0 ? _spawnIds[0] : null;
                }
                if (GUILayout.Button("갱신", GUILayout.Width(42)))
                {
                    _spawnIds = ContentMakerMarkerService.GetSpawnIds(_request.Destination);
                    _assetsDirty = true;
                }
            }
            if (_request.Destination != null)
            {
                if (_spawnIds.Length == 0)
                    EditorGUILayout.HelpBox("대상 방에 시작점이 없습니다. 대상 방을 먼저 열어 시작점을 배치하고 저장하세요.", MessageType.Warning);
                else
                {
                    int selected = Array.IndexOf(_spawnIds, _request.DestinationSpawn);
                    int chosen = EditorGUILayout.Popup("도착 시작점", Mathf.Max(0, selected), _spawnIds);
                    if (chosen >= 0 && chosen < _spawnIds.Length) _request.DestinationSpawn = _spawnIds[chosen];
                }
            }
            _request.Activation = (DoorActivationMode)EditorGUILayout.Popup("문 사용 방식", (int)_request.Activation, ActivationLabels);
        }

        private void DrawVisual()
        {
            EditorGUILayout.Space(10f);
            EditorGUILayout.LabelField("외형 (선택 사항)", ContentMakerGUI.SectionTitle);
            _request.VisualPrefab = (GameObject)EditorGUILayout.ObjectField("외형 전용 프리팹", _request.VisualPrefab, typeof(GameObject), false);
            using (new EditorGUI.DisabledScope(_request.VisualPrefab != null))
                _request.Sprite = (Sprite)EditorGUILayout.ObjectField("또는 스프라이트 한 장", _request.Sprite, typeof(Sprite), false);
            EditorGUILayout.LabelField("외형은 자식으로 연결하며 원본 크기 / 애니메이션을 유지합니다. 충돌과 상호작용은 마커 루트가 담당합니다.", ContentMakerGUI.Muted);
            if (_request.VisualPrefab == null && _request.Sprite == null)
                EditorGUILayout.LabelField("외형 없이 만들면 씬 뷰의 마커만 보입니다. 게임에서는 보이지 않으므로 NPC는 외형을 연결하는 편이 좋습니다.", ContentMakerGUI.Muted);
            EditorGUILayout.Space(8f);
        }

        private string GetCreationProblem()
        {
            if (string.IsNullOrWhiteSpace(_request.Name)) return "이름을 입력하세요.";
            if (ContentMakerMarkerService.RequiresDialogue(_request.Kind) && _request.Dialogue == null) return "대사 탭에서 대화를 만들거나 기존 대화를 연결하세요.";
            if ((_request.Kind == ContentMakerMarkerKind.BattleNpc || _request.Kind == ContentMakerMarkerKind.Enemy) && _request.Enemy == null) return "전투에 사용할 적 데이터를 선택하세요.";
            if (_request.Kind == ContentMakerMarkerKind.Item && (_request.Item == null || string.IsNullOrWhiteSpace(_request.Item.ItemID))) return "고유 ID가 있는 아이템 데이터를 연결하세요.";
            if (_request.Kind == ContentMakerMarkerKind.Vendor && _request.Shop == null) return "상점 데이터를 연결하세요.";
            if (ContentMakerMarkerService.IsDoor(_request.Kind) && (_request.Destination == null || Array.IndexOf(_spawnIds, _request.DestinationSpawn) < 0)) return "도착할 방과 시작점을 선택하세요.";
            return _request.Kind == ContentMakerMarkerKind.Spawn ? string.Empty : ContentMakerMarkerService.ValidateVisual(_request.VisualPrefab);
        }

        private void DrawMemberList(ContentMakerContext context, RoomInstance room)
        {
            if (_memberButtonStyle == null || _memberStyleDark != EditorGUIUtility.isProSkin)
            {
                _memberStyleDark = EditorGUIUtility.isProSkin;
                _memberButtonStyle = new GUIStyle(EditorStyles.miniButton)
                {
                    alignment = TextAnchor.MiddleLeft,
                    clipping = TextClipping.Clip,
                    padding = new RectOffset(10, 8, 6, 6)
                };
            }
            using (ContentMakerGUI.Card())
            {
                EditorGUILayout.LabelField("이 방에 있는 시작점 / 마커 / NPC  ·  " + _members.Count, ContentMakerGUI.SectionTitle);
                EditorGUILayout.Space(6f);
                _filter = EditorGUILayout.TextField("이름 / ID 검색", _filter);
                EditorGUILayout.Space(4f);
                _listScroll = EditorGUILayout.BeginScrollView(_listScroll, GUILayout.MinHeight(80), GUILayout.MaxHeight(205));
                foreach (Component member in _members)
                {
                    if (member == null) continue;
                    string label = MemberLabel(member);
                    if (!string.IsNullOrEmpty(_filter) && label.IndexOf(_filter, StringComparison.OrdinalIgnoreCase) < 0) continue;
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        bool selected = member == _selected;
                        int separator = label.IndexOf("  ·  ", StringComparison.Ordinal);
                        string compactLabel = separator >= 0 ? label.Substring(0, separator) : label;
                        Color background = GUI.backgroundColor;
                        try
                        {
                            if (selected)
                                GUI.backgroundColor = EditorGUIUtility.isProSkin ? new Color(.40f, .76f, .72f) : new Color(.68f, .88f, .84f);
                            if (GUILayout.Toggle(selected, new GUIContent(compactLabel, label), _memberButtonStyle, GUILayout.MinWidth(0), GUILayout.MinHeight(30)) && !selected)
                                SetSelected(member);
                        }
                        finally
                        {
                            GUI.backgroundColor = background;
                        }
                        if (ContentMakerGUI.SecondaryButton("위치", GUILayout.Width(48f)))
                        {
                            SetSelected(member);
                            Focus(member);
                        }
                    }
                }
                EditorGUILayout.EndScrollView();
                if (_members.Count == 0) EditorGUILayout.LabelField("아직 마커가 없습니다. 위에서 시작점이나 NPC를 배치하세요.", ContentMakerGUI.Muted);
            }
        }

        private void DrawSelected(ContentMakerContext context, RoomInstance room)
        {
            EditorGUILayout.Space(8);
            using (ContentMakerGUI.Card())
            {
                EditorGUILayout.LabelField("선택: " + _selected.name, ContentMakerGUI.SectionTitle);
                EditorGUILayout.Space(6f);
                EditorGUI.BeginChangeCheck();
                string newName = EditorGUILayout.TextField("표시 오브젝트 이름", _selected.gameObject.name);
                Vector3 position = EditorGUILayout.Vector3Field("방 기준 위치", room.transform.InverseTransformPoint(_selected.transform.position));
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObjects(new Object[] { _selected.gameObject, _selected.transform }, "마커 위치 / 이름 수정");
                    if (!string.IsNullOrWhiteSpace(newName)) _selected.gameObject.name = newName.Trim();
                    _selected.transform.position = room.transform.TransformPoint(position);
                    RecordPrefabOverrides(_selected.gameObject, _selected.transform);
                    EditorSceneManager.MarkSceneDirty(room.gameObject.scene);
                }
                DialogueData linkedDialogue = GetLinkedDialogue(_selected);
                if (linkedDialogue != null && ContentMakerGUI.SecondaryButton("연결된 대사 편집: " + linkedDialogue.name, GUILayout.MinWidth(0f))) context.Select(linkedDialogue);
                if (_selected is DialogueBattleNPC && linkedDialogue != null && !ContentMakerMarkerService.HasBattleChoice(linkedDialogue))
                {
                    var serialized = new SerializedObject(_selected);
                    if (!serialized.FindProperty("_useStagedEncounter").boolValue)
                        EditorGUILayout.HelpBox("대사 흐름에 전투 시작 선택지가 없습니다. 연결된 대사에서 선택지의 '전투 시작'을 설정하세요.", MessageType.Warning);
                }
                if (_selected is AreaConnectionMarker connection)
                {
                    DrawSelectedConnection(connection, room);
                }
                EditorGUILayout.Space(6f);
                _showAdvanced = ContentMakerGUI.Foldout(_showAdvanced, "상세 설정");
                if (_showAdvanced)
                {
                    UnityEditor.Editor.CreateCachedEditor(_selected, null, ref _componentEditor);
                    if (_componentEditor != null)
                    {
                        EditorGUI.BeginChangeCheck();
                        _componentEditor.OnInspectorGUI();
                        if (EditorGUI.EndChangeCheck())
                        {
                            if (PrefabUtility.IsPartOfPrefabInstance(_selected)) PrefabUtility.RecordPrefabInstancePropertyModifications(_selected);
                            EditorSceneManager.MarkSceneDirty(room.gameObject.scene);
                        }
                    }
                    DrawCollider(room);
                }
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (ContentMakerGUI.SecondaryButton("인스펙터에서 보기")) Focus(_selected);
                    if (ContentMakerGUI.SecondaryButton("이 배치 제거"))
                        Run(context, () =>
                        {
                            if (!EditorUtility.DisplayDialog("방에서 배치 제거", _selected.name + " 오브젝트와 그 자식을 이 방에서 제거합니다. 공유 원본 프리팹 / 대화 자산은 삭제하지 않습니다. Ctrl+Z로 복구할 수 있습니다.", "배치 제거", "취소")) return;
                            Undo.DestroyObjectImmediate(_selected.gameObject);
                            SetSelected(null);
                            _dirty = true;
                            EditorSceneManager.MarkSceneDirty(room.gameObject.scene);
                        });
                }
            }
        }

        private void DrawSelectedConnection(AreaConnectionMarker connection, RoomInstance room)
        {
            MapTransitionRequest transition = connection.MapTransition;
            if (transition == null || transition.TransitionType != MapTransitionType.Room)
            {
                EditorGUILayout.HelpBox("씬 이동 문입니다. 대상 씬과 시작점은 아래 상세 설정에서 지정합니다.", MessageType.Info);
                return;
            }
            if (_editDestination != transition.TargetRoom)
            {
                _editDestination = transition.TargetRoom;
                _editSpawnIds = ContentMakerMarkerService.GetSpawnIds(_editDestination);
            }
            EditorGUI.BeginChangeCheck();
            int current = _destinations.IndexOf(transition.TargetRoom);
            int chosen = EditorGUILayout.Popup("도착할 방 변경", current + 1, _destinationLabels);
            RoomDefinition destination = chosen > 0 && chosen <= _destinations.Count ? _destinations[chosen - 1] : transition.TargetRoom;
            if (destination != _editDestination)
            {
                _editDestination = destination;
                _editSpawnIds = ContentMakerMarkerService.GetSpawnIds(destination);
            }
            int spawnIndex = Array.IndexOf(_editSpawnIds, transition.TargetSpawnPointId);
            string spawnId = transition.TargetSpawnPointId;
            if (_editSpawnIds.Length > 0)
            {
                var labels = new string[_editSpawnIds.Length + 1];
                labels[0] = "도착 시작점 선택…";
                Array.Copy(_editSpawnIds, 0, labels, 1, _editSpawnIds.Length);
                int chosenSpawn = EditorGUILayout.Popup("도착 시작점 변경", spawnIndex + 1, labels);
                if (chosenSpawn > 0) spawnId = _editSpawnIds[chosenSpawn - 1];
            }
            if (EditorGUI.EndChangeCheck())
            {
                var serialized = new SerializedObject(connection);
                SerializedProperty request = serialized.FindProperty("mapTransition");
                request.FindPropertyRelative("TargetRoom").objectReferenceValue = destination;
                request.FindPropertyRelative("TargetSpawnPointId").stringValue = destination != transition.TargetRoom
                    ? (_editSpawnIds.Length > 0 ? _editSpawnIds[0] : string.Empty)
                    : spawnId;
                serialized.ApplyModifiedProperties();
                if (PrefabUtility.IsPartOfPrefabInstance(connection)) PrefabUtility.RecordPrefabInstancePropertyModifications(connection);
                EditorSceneManager.MarkSceneDirty(room.gameObject.scene);
            }
            if (_editSpawnIds.Length == 0 || Array.IndexOf(_editSpawnIds, transition.TargetSpawnPointId) < 0)
                EditorGUILayout.HelpBox("대상 방의 도착 시작점 연결이 필요합니다. 대상 방에 시작점이 없으면 먼저 만들고 저장하세요.", MessageType.Warning);
        }

        private void DrawCollider(RoomInstance room)
        {
            BoxCollider2D collider = _selected.GetComponent<BoxCollider2D>();
            if (collider == null) return;
            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField("접촉 범위", ContentMakerGUI.SectionTitle);
            EditorGUI.BeginChangeCheck();
            Vector2 size = EditorGUILayout.Vector2Field("크기", collider.size);
            Vector2 offset = EditorGUILayout.Vector2Field("중심 오프셋", collider.offset);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(collider, "마커 접촉 범위 수정");
                collider.size = new Vector2(Mathf.Max(.05f, size.x), Mathf.Max(.05f, size.y));
                collider.offset = offset;
                if (PrefabUtility.IsPartOfPrefabInstance(collider)) PrefabUtility.RecordPrefabInstancePropertyModifications(collider);
                EditorSceneManager.MarkSceneDirty(room.gameObject.scene);
            }
        }

        private void RefreshIfNeeded(ContentMakerContext context, RoomInstance room)
        {
            if (_roomInstanceId != room.GetInstanceID())
            {
                _roomInstanceId = room.GetInstanceID();
                _dirty = true;
                SetSelected(null);
            }
            if (_regionPath != context.RegionPath)
            {
                _regionPath = context.RegionPath;
                _assetsDirty = true;
            }
            if (_dirty)
            {
                _members.Clear();
                AddMembers(room.GetComponentsInChildren<SpawnPoint>(true), room);
                AddMembers(room.GetComponentsInChildren<AreaMarkerBase>(true), room);
                AddMembers(room.GetComponentsInChildren<DialogueBattleNPC>(true), room);
                AddMembers(room.GetComponentsInChildren<DoorTransition>(true), room);
                _members.Sort((left, right) => string.Compare(left.name, right.name, StringComparison.OrdinalIgnoreCase));
                _spawnIds = ContentMakerMarkerService.GetSpawnIds(_request.Destination);
                _editSpawnIds = ContentMakerMarkerService.GetSpawnIds(_editDestination);
                _dirty = false;
            }
            if (_assetsDirty)
            {
                _destinations.Clear();
                if (!string.IsNullOrEmpty(context.RegionPath) && AssetDatabase.IsValidFolder(context.RegionPath))
                {
                    foreach (string guid in AssetDatabase.FindAssets("t:RoomDefinition", new[] { context.RegionPath }))
                    {
                        RoomDefinition definition = AssetDatabase.LoadAssetAtPath<RoomDefinition>(AssetDatabase.GUIDToAssetPath(guid));
                        if (definition != null) _destinations.Add(definition);
                    }
                }
                _destinations.Sort((left, right) => string.Compare(left.name, right.name, StringComparison.OrdinalIgnoreCase));
                _destinationLabels = new string[_destinations.Count + 1];
                _destinationLabels[0] = "대상 방 선택…";
                for (int i = 0; i < _destinations.Count; i++) _destinationLabels[i + 1] = _destinations[i].name + "  [" + _destinations[i].RoomId + "]";
                _spawnIds = ContentMakerMarkerService.GetSpawnIds(_request.Destination);
                _editSpawnIds = ContentMakerMarkerService.GetSpawnIds(_editDestination);
                _assetsDirty = false;
            }
            if (_selectionPending)
            {
                GameObject selection = Selection.activeGameObject;
                if (selection != null && selection.transform.IsChildOf(room.transform))
                {
                    foreach (Component member in _members)
                    {
                        if (member != null && (member.gameObject == selection || selection.transform.IsChildOf(member.transform)))
                        {
                            SetSelected(member);
                            break;
                        }
                    }
                }
                _selectionPending = false;
            }
        }

        private void AddMembers<T>(T[] components, RoomInstance room) where T : Component
        {
            foreach (T component in components)
                if (component != null && component.transform != room.transform && component.GetComponentInParent<RoomInstance>() == room)
                    _members.Add(component);
        }

        private void SetSelected(Component selected)
        {
            if (_selected == selected) return;
            _selected = selected;
            _showAdvanced = true;
            if (_componentEditor != null) Object.DestroyImmediate(_componentEditor);
            _componentEditor = null;
        }

        private static DialogueData GetLinkedDialogue(Component component)
        {
            var serialized = new SerializedObject(component);
            SerializedProperty property = serialized.FindProperty(component is DialogueBattleNPC ? "_dialogue" : "dialogueData");
            return property?.objectReferenceValue as DialogueData;
        }

        private static string MemberLabel(Component member)
        {
            if (member is SpawnPoint spawn) return "[시작점] " + member.name + "  ·  " + spawn.SpawnPointId;
            if (member is AreaMarkerBase marker) return "[" + marker.ShortTypeLabel + "] " + member.name + "  ·  " + marker.MarkerId;
            if (member is DialogueBattleNPC) return "[대화 전투] " + member.name;
            return "[문] " + member.name;
        }

        private static string DefaultName(ContentMakerMarkerKind kind)
        {
            switch (kind)
            {
                case ContentMakerMarkerKind.Spawn: return "도착점";
                case ContentMakerMarkerKind.Door: return "연결문";
                case ContentMakerMarkerKind.Npc: return "대화NPC";
                case ContentMakerMarkerKind.BattleNpc: return "전투NPC";
                default: return ContentMakerMarkerService.KindNames[(int)kind].Replace(" / ", "_").Replace(" ", "");
            }
        }

        private static void Focus(Component target)
        {
            if (target == null) return;
            Selection.activeGameObject = target.gameObject;
            EditorGUIUtility.PingObject(target.gameObject);
            SceneView.lastActiveSceneView?.FrameSelected();
        }

        private static void RecordPrefabOverrides(GameObject target, Transform transform)
        {
            if (!PrefabUtility.IsPartOfPrefabInstance(target)) return;
            PrefabUtility.RecordPrefabInstancePropertyModifications(target);
            PrefabUtility.RecordPrefabInstancePropertyModifications(transform);
        }

        private static void Run(ContentMakerContext context, Action action)
        {
            try { action(); }
            catch (ExitGUIException) { throw; }
            catch (Exception exception) { context.Report(exception.Message, MessageType.Error); }
        }

        private void OnHierarchyChanged() => _dirty = true;
        private void OnProjectChanged() { _assetsDirty = true; _dirty = true; }
        private void OnSelectionChanged() => _selectionPending = true;
    }
}
