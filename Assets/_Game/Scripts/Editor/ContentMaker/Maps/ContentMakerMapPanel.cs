using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace HubToHome.EditorTools.ContentMaker
{
    internal sealed class ContentMakerMapPanel : IDisposable
    {
        private static readonly string[] RoomPages = { "방 설정", "방 복제", "씬 관리" };
        private static readonly string[] ScenePages = { "새 씬 만들기", "기존 씬에 방 등록" };
        private string _regionName = string.Empty;
        private string _roomName = "NewRoom";
        private string _copyName = string.Empty;
        private string _sceneName = string.Empty;
        private string _rename = string.Empty;
        private bool _includeBattleHost;
        private int _scenePage;
        private Vector2 _size = new Vector2(20f, 15f);
        private RoomDefinition _lastRoom;
        private SceneAsset _registrationScene;
        private SerializedObject _roomObject;
        private SerializedObject _areaObject;
        private ContentMakerMapPage _lastPage;
        private string _lastRegionPath;
        private RoomDefinition _previewRoom;
        private ContentMakerDeletionPreview _deletionPreview;
        private Vector2 _deletionScroll;

        public void OnGUI(ContentMakerContext context)
        {
            if (_lastPage != context.MapPage || _lastRegionPath != context.RegionPath || _previewRoom != context.Room)
            {
                _deletionPreview = null;
                _deletionScroll = Vector2.zero;
                if (_lastRegionPath != context.RegionPath)
                {
                    _registrationScene = null;
                    _sceneName = string.IsNullOrEmpty(context.RegionPath) ? string.Empty : Path.GetFileName(context.RegionPath);
                }
                _lastPage = context.MapPage;
                _lastRegionPath = context.RegionPath;
                _previewRoom = context.Room;
            }

            using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling))
            {
                if (context.MapPage == ContentMakerMapPage.NewRegion)
                {
                    DrawNewRegion(context);
                    return;
                }
                if (string.IsNullOrEmpty(context.RegionPath))
                {
                    ContentMakerGUI.Header("챕터부터 시작하세요", "챕터는 방과 대사를 모아 두는 기존 지역 폴더입니다.");
                    using (ContentMakerGUI.Card())
                    {
                        EditorGUILayout.LabelField("왼쪽 챕터 목록에서 선택하거나 새 챕터를 만들어 주세요.", ContentMakerGUI.Body);
                        EditorGUILayout.Space(8f);
                        if (ContentMakerGUI.PrimaryButton("새 챕터 만들기"))
                            Navigate(context, ContentMakerMapPage.NewRegion);
                    }
                    return;
                }

                switch (context.MapPage)
                {
                    case ContentMakerMapPage.RegionDetails: DrawRegion(context); break;
                    case ContentMakerMapPage.NewRoom: DrawNewRoom(context); break;
                    case ContentMakerMapPage.DeleteRegion: DrawDeletion(context, true); break;
                    case ContentMakerMapPage.DeleteRoom: DrawDeletion(context, false); break;
                    default: DrawSelectedRoom(context); break;
                }
            }
        }

        private static void DrawRegion(ContentMakerContext context)
        {
            ContentMakerGUI.Header("챕터 (지역)", "왼쪽에서 방을 선택해 편집하거나, 추가 버튼으로 새 방을 만드세요.");
            using (ContentMakerGUI.Card())
            {
                EditorGUILayout.LabelField(Path.GetFileName(context.RegionPath), ContentMakerGUI.SectionTitle);
                EditorGUILayout.LabelField(context.RegionPath, ContentMakerGUI.Muted);
                EditorGUILayout.Space(6f);
                EditorGUILayout.LabelField("챕터는 기존 지역 폴더의 표시 이름입니다. 새로운 런타임 데이터나 진행 조건을 만드는 기능은 아닙니다.", ContentMakerGUI.Body);
                EditorGUILayout.Space(8f);
                EditorGUILayout.LabelField("Scenes  ·  플레이할 지역 씬\nPrefabs/Rooms  ·  지형과 배치\nData/Rooms  ·  방 설정과 배경음악\nData/Dialogue  ·  이 지역의 대화", ContentMakerGUI.Muted);
                EditorGUILayout.Space(8f);
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (ContentMakerGUI.PrimaryButton("새 방 만들기"))
                        Navigate(context, ContentMakerMapPage.NewRoom);
                    if (ContentMakerGUI.SecondaryButton("폴더 보기", GUILayout.Width(92f)))
                        Ping(AssetDatabase.LoadAssetAtPath<DefaultAsset>(context.RegionPath));
                }
            }
        }

        private void DrawNewRegion(ContentMakerContext context)
        {
            ContentMakerGUI.Header("새 챕터 만들기", "챕터에 필요한 폴더를 한 번에 준비합니다. 기존 폴더는 덮어쓰지 않습니다.");
            using (ContentMakerGUI.Card())
            {
                _regionName = EditorGUILayout.TextField(new GUIContent("챕터 폴더 이름", "예: Chapter02. 기존 지역 폴더와 같은 구조로 생성합니다."), _regionName);
                EditorGUILayout.Space(4f);
                EditorGUILayout.LabelField("생성 위치", ContentMakerGUI.Muted);
                EditorGUILayout.LabelField(ContentMakerMapService.RegionsRoot + "/" + ContentMakerAssetUtility.MakeFileName(_regionName), ContentMakerGUI.Muted);
                EditorGUILayout.Space(8f);
                using (new EditorGUI.DisabledScope(string.IsNullOrWhiteSpace(_regionName)))
                {
                    if (ContentMakerGUI.PrimaryButton("챕터 만들고 방 추가하기"))
                        Run(context, () =>
                        {
                            context.RegionPath = ContentMakerMapService.CreateRegion(_regionName);
                            context.Room = null;
                            context.Dialogue = null;
                            context.RefreshAssets();
                            context.Select(AssetDatabase.LoadAssetAtPath<DefaultAsset>(context.RegionPath));
                            context.Report("챕터 폴더를 만들었습니다. 장소 이름을 입력해 첫 방을 추가하세요.");
                            Navigate(context, ContentMakerMapPage.NewRoom);
                        });
                }
                DrawBack(context);
            }
        }

        private void DrawNewRoom(ContentMakerContext context)
        {
            ContentMakerGUI.Header("새 방 만들기", Path.GetFileName(context.RegionPath) + "에 장소를 추가합니다.");
            using (ContentMakerGUI.Card())
            {
                _roomName = EditorGUILayout.TextField(new GUIContent("장소 이름", "예: WindmillExterior. 기존 자산은 덮어쓰지 않습니다."), _roomName);
                _size = EditorGUILayout.Vector2Field("기본 크기 (가로/세로)", _size);
                EditorGUILayout.Space(8f);
                EditorGUILayout.LabelField("프리팹 · 방 데이터 · 구역 데이터 · 카메라 경계 · 기본 시작점을 함께 만듭니다. 같은 기본 방에 지형을 배치해 마을·던전·실내로 꾸미면 됩니다.", ContentMakerGUI.Body);
                EditorGUILayout.Space(4f);
                EditorGUILayout.LabelField("프리팹 편집을 저장하고 나온 뒤 만드세요.\n저장 위치 · " + context.RegionPath + "/Prefabs/Rooms, Data/Rooms", ContentMakerGUI.Muted);
                EditorGUILayout.Space(8f);
                using (new EditorGUI.DisabledScope(string.IsNullOrWhiteSpace(_roomName)))
                {
                    if (ContentMakerGUI.PrimaryButton("방 만들기"))
                        Run(context, () =>
                        {
                            RoomDefinition room = ContentMakerMapService.CreateRoom(context.RegionPath, _roomName, _size, false);
                            context.RefreshAssets();
                            context.Select(room);
                            context.Report("방을 만들었습니다. '지형 배치하기'에서 그림과 충돌벽을 배치하세요.");
                            Navigate(context, ContentMakerMapPage.RoomEdit);
                        });
                }
                DrawBack(context);
            }
        }

        private void DrawSelectedRoom(ContentMakerContext context)
        {
            RoomDefinition room = context.Room;
            if (room == null)
            {
                DrawRegion(context);
                return;
            }
            BindRoom(context, room);
            ContentMakerGUI.Header(room.name, "왼쪽 목록에서 방을 바꾸고, 아래에서 할 작업을 선택하세요.");
            int page = context.MapPage == ContentMakerMapPage.RoomDuplicate ? 1 : context.MapPage == ContentMakerMapPage.RoomScene ? 2 : 0;
            int next = GUILayout.Toolbar(page, RoomPages, GUILayout.Height(30f));
            if (next != page)
                Navigate(context, next == 1 ? ContentMakerMapPage.RoomDuplicate : next == 2 ? ContentMakerMapPage.RoomScene : ContentMakerMapPage.RoomEdit);
            EditorGUILayout.Space(10f);

            if (page == 1)
                DrawDuplicate(context, room);
            else if (page == 2)
                DrawSceneManagement(context, room);
            else
            {
                DrawRoomSummary(context, room);
                DrawRoomSettings(context, room);
                DrawRename(context, room);
            }
            DrawBack(context);
        }

        private void BindRoom(ContentMakerContext context, RoomDefinition room)
        {
            if (_lastRoom == room && _roomObject != null) return;
            Dispose();
            _lastRoom = room;
            _roomObject = new SerializedObject(room);
            _areaObject = room.AreaDefinition != null ? new SerializedObject(room.AreaDefinition) : null;
            _copyName = room.name.Replace("Room_", string.Empty).Replace("_Definition", string.Empty) + "_Copy";
            _sceneName = Path.GetFileName(context.RegionPath);
            _rename = room.name;
        }

        private static void DrawRoomSummary(ContentMakerContext context, RoomDefinition room)
        {
            using (ContentMakerGUI.Card())
            {
                EditorGUILayout.LabelField("지형과 배치", ContentMakerGUI.SectionTitle);
                EditorGUILayout.LabelField("방 고유 ID · " + room.RoomId, ContentMakerGUI.Muted);
                EditorGUILayout.LabelField(AssetDatabase.GetAssetPath(room), ContentMakerGUI.Muted);
                EditorGUILayout.Space(8f);
                using (new EditorGUILayout.HorizontalScope())
                {
                    using (new EditorGUI.DisabledScope(!room.IsValid))
                    {
                        if (ContentMakerGUI.PrimaryButton("지형 배치하기"))
                            Run(context, () => ContentMakerMapService.OpenRoom(room));
                    }
                    if (ContentMakerGUI.SecondaryButton("파일 위치", GUILayout.Width(92f)))
                        Ping(room);
                }
            }
        }

        private void DrawRoomSettings(ContentMakerContext context, RoomDefinition room)
        {
            using (ContentMakerGUI.Card())
            {
                EditorGUILayout.LabelField("방 설정", ContentMakerGUI.SectionTitle);
                _roomObject.Update();
                EditorGUILayout.PropertyField(_roomObject.FindProperty("_roomPrefab"), new GUIContent("지형 프리팹", "RoomInstance 컴포넌트가 붙은 프리팹을 연결합니다."));
                EditorGUILayout.PropertyField(_roomObject.FindProperty("_areaDefinition"), new GUIContent("구역 데이터"));
                EditorGUILayout.Space(8f);
                EditorGUILayout.PropertyField(_roomObject.FindProperty("_bgmOverride"), new GUIContent("이 방의 배경음악", "곡을 넣으면 입장할 때 이 음악으로 전환합니다."));
                if (_roomObject.FindProperty("_bgmOverride").objectReferenceValue == null)
                    EditorGUILayout.PropertyField(_roomObject.FindProperty("_keepCurrentBgm"), new GUIContent("곡이 없으면 이전 음악 유지", "끄면 이 방 입장 시 배경음악을 종료합니다."));
                EditorGUILayout.PropertyField(_roomObject.FindProperty("_bgmFadeDuration"), new GUIContent("음악 전환 시간 (초)"));
                _roomObject.FindProperty("_bgmFadeDuration").floatValue = Mathf.Max(0f, _roomObject.FindProperty("_bgmFadeDuration").floatValue);
                _roomObject.ApplyModifiedProperties();
                if (room.AreaDefinition != null)
                {
                    if (_areaObject == null || _areaObject.targetObject != room.AreaDefinition)
                    {
                        _areaObject?.Dispose();
                        _areaObject = new SerializedObject(room.AreaDefinition);
                    }
                    _areaObject.Update();
                    EditorGUILayout.Space(8f);
                    EditorGUILayout.PropertyField(_areaObject.FindProperty("_description"), new GUIContent("장소 메모"));
                    _areaObject.ApplyModifiedProperties();
                }
                EditorGUILayout.Space(8f);
                if (ContentMakerGUI.PrimaryButton("방 설정 저장"))
                    Run(context, () =>
                    {
                        ContentMakerAssetUtility.Save(room);
                        if (room.AreaDefinition != null) ContentMakerAssetUtility.Save(room.AreaDefinition);
                        context.Report("선택한 방·구역 데이터만 저장했습니다. 지형 편집은 프리팹 편집 모드에서 저장하세요.");
                    });
            }
        }

        private void DrawDuplicate(ContentMakerContext context, RoomDefinition room)
        {
            using (ContentMakerGUI.Card())
            {
                EditorGUILayout.LabelField("이 방을 복제하여 새 장소 만들기", ContentMakerGUI.SectionTitle);
                _copyName = EditorGUILayout.TextField("새 장소 이름", _copyName);
                EditorGUILayout.HelpBox("지형을 복사하고 방·마커·NPC 개체 ID를 새로 만듭니다. 공유 대화/캐릭터, 외부 문 연결, 조건·완료 플래그는 유지하므로 복제 후 검토하세요.", MessageType.Warning);
                EditorGUILayout.Space(6f);
                using (new EditorGUI.DisabledScope(string.IsNullOrWhiteSpace(_copyName)))
                {
                    if (ContentMakerGUI.PrimaryButton("새 방으로 복제"))
                        Run(context, () =>
                        {
                            if (!EditorUtility.DisplayDialog("방 복제", "새 파일로 복제합니다. 외부 문 연결과 조건·완료 플래그, 공유 대화는 원본을 유지합니다. 이후 마커 탭에서 연결을 검토하세요.", "복제", "취소")) return;
                            RoomDefinition copy = ContentMakerMapService.DuplicateRoom(context.RegionPath, room, _copyName);
                            context.RefreshAssets();
                            context.Select(copy);
                            context.Report("복제했습니다. 원본의 문 연결과 조건·완료 플래그를 검토하세요.", MessageType.Warning);
                            Navigate(context, ContentMakerMapPage.RoomEdit);
                        });
                }
            }
        }

        private void DrawSceneManagement(ContentMakerContext context, RoomDefinition room)
        {
            using (ContentMakerGUI.Card())
            {
                EditorGUILayout.LabelField("플레이할 지역 씬", ContentMakerGUI.SectionTitle);
                EditorGUILayout.LabelField("방 프리팹을 플레이어·카메라·게임 초기화 구성이 있는 씬에 연결합니다. 지형만 편집할 때는 '방 설정 → 지형 배치하기'를 사용하세요.", ContentMakerGUI.Body);
                EditorGUILayout.Space(8f);
                int next = GUILayout.Toolbar(_scenePage, ScenePages, GUILayout.Height(28f));
                if (next != _scenePage)
                {
                    _scenePage = next;
                    GUI.FocusControl(null);
                    GUIUtility.ExitGUI();
                }
                EditorGUILayout.Space(10f);
                if (_scenePage == 0)
                    DrawNewScene(context, room);
                else
                    DrawRegisterScene(context);
            }
        }

        private void DrawNewScene(ContentMakerContext context, RoomDefinition room)
        {
            _sceneName = EditorGUILayout.TextField("씬 이름", _sceneName);
            _includeBattleHost = EditorGUILayout.Toggle(new GUIContent("전투 호스트 포함", "방 안에 이미 전투 호스트가 있다면 끕니다."), _includeBattleHost);
            EditorGUILayout.LabelField("선택한 방에서 시작하는 새 씬을 만듭니다. 프리팹 편집 모드를 종료한 뒤 만드세요. 현재 열린 씬과 빌드 설정은 바꾸지 않습니다. 이후 추가한 방은 '기존 씬에 방 등록'으로 연결하세요.", ContentMakerGUI.Body);
            EditorGUILayout.Space(8f);
            using (new EditorGUI.DisabledScope(string.IsNullOrWhiteSpace(_sceneName)))
            {
                if (ContentMakerGUI.PrimaryButton("새 지역 씬 만들기"))
                    Run(context, () =>
                    {
                        SceneAsset scene = ContentMakerMapService.CreateRegionScene(context.RegionPath, _sceneName, room, _includeBattleHost);
                        context.RefreshAssets();
                        Ping(scene);
                        context.Report("씬을 만들었습니다. 프로젝트 창에서 열 수 있습니다. 출시 빌드에 사용할 때 빌드 설정에 직접 등록하세요.");
                    });
            }
        }

        private void DrawRegisterScene(ContentMakerContext context)
        {
            _registrationScene = EditorGUILayout.ObjectField("등록할 지역 씬", _registrationScene, typeof(SceneAsset), false) as SceneAsset;
            EditorGUILayout.LabelField("방을 추가했다면 지역 씬의 진입 목록에도 등록합니다. 현재 챕터의 Scenes 폴더만 허용하며, 확인창에서 대상·시작 방·반영 개수를 확인한 뒤 저장합니다.", ContentMakerGUI.Body);
            EditorGUILayout.Space(8f);
            using (new EditorGUI.DisabledScope(_registrationScene == null))
            {
                if (ContentMakerGUI.PrimaryButton("등록 내용 확인 → 반영"))
                    Run(context, () =>
                    {
                        bool registered = ContentMakerMapService.RegisterRegionRooms(context.RegionPath, _registrationScene,
                            summary => EditorUtility.DisplayDialog("지역 씬에 방 목록 등록", summary, "등록하고 저장", "취소"));
                        if (registered) context.Report("선택한 지역 씬에 방 목록을 등록했습니다. 기존 시작 방과 다른 씬은 유지했습니다.");
                    });
            }
        }

        private void DrawRename(ContentMakerContext context, RoomDefinition room)
        {
            using (ContentMakerGUI.Card())
            {
                EditorGUILayout.LabelField("목록에 표시되는 파일명", ContentMakerGUI.SectionTitle);
                _rename = EditorGUILayout.TextField("방 데이터 파일명", _rename);
                EditorGUILayout.LabelField("방 데이터 파일명만 바꿉니다. 지형 프리팹 파일명, 저장용 방 ID와 연결 참조는 그대로 유지합니다.", ContentMakerGUI.Muted);
                using (new EditorGUI.DisabledScope(string.IsNullOrWhiteSpace(_rename) || _rename == room.name))
                {
                    if (ContentMakerGUI.SecondaryButton("파일명 변경"))
                        Run(context, () =>
                        {
                            ContentMakerMapService.RenameRoomData(room, _rename);
                            _rename = room.name;
                            context.RefreshAssets();
                            context.Report("방 데이터 파일명을 변경했습니다. 고유 ID와 참조는 유지했습니다.");
                        });
                }
            }
        }

        private void DrawDeletion(ContentMakerContext context, bool region)
        {
            ContentMakerGUI.Header(region ? "챕터 삭제" : "방 삭제", "대상과 사용 중인 연결을 확인한 뒤 휴지통으로 이동합니다.");
            using (ContentMakerGUI.Card())
            {
                EditorGUILayout.LabelField(region ? context.RegionPath : context.Room != null ? AssetDatabase.GetAssetPath(context.Room) : "선택한 방이 없습니다.", ContentMakerGUI.Body);
                EditorGUILayout.HelpBox(region
                    ? "챕터 폴더와 그 안의 방·씬·대사 등 모든 파일이 대상입니다. 다른 곳에서 사용하는 자산은 먼저 연결을 정리해야 합니다."
                    : "선택한 방 데이터와 이 지역의 연결된 지형·구역 데이터를 확인합니다. 공유 자산과 다른 방의 문 연결을 자동으로 삭제하지 않습니다.", MessageType.Warning);
                EditorGUILayout.LabelField("검사는 버튼을 누를 때만 실행합니다. 삭제는 Undo 대상이 아니며, 휴지통에서 원래 경로로 복원할 때 .meta도 함께 복원해야 참조를 유지할 수 있습니다.", ContentMakerGUI.Muted);
                using (new EditorGUI.DisabledScope(!region && context.Room == null))
                {
                    if (ContentMakerGUI.SecondaryButton(_deletionPreview == null ? "삭제 대상·참조 검사" : "대상·참조 다시 검사"))
                        Run(context, () =>
                        {
                            _deletionPreview = null;
                            _deletionPreview = region
                                ? ContentMakerDeletionService.PreviewRegion(context.RegionPath)
                                : ContentMakerDeletionService.PreviewRoom(context.RegionPath, context.Room);
                            _deletionScroll = Vector2.zero;
                            GUIUtility.ExitGUI();
                        });
                }
            }

            if (_deletionPreview != null)
            {
                using (ContentMakerGUI.Card())
                {
                    EditorGUILayout.LabelField("삭제 대상 · " + _deletionPreview.Paths.Length + "개", ContentMakerGUI.SectionTitle);
                    using (var scroll = new EditorGUILayout.ScrollViewScope(_deletionScroll, GUILayout.MaxHeight(220f)))
                    {
                        _deletionScroll = scroll.scrollPosition;
                        foreach (string path in _deletionPreview.Paths)
                            EditorGUILayout.LabelField(path, ContentMakerGUI.Muted);
                    }
                    if (_deletionPreview.Blockers.Length > 0)
                    {
                        EditorGUILayout.Space(8f);
                        EditorGUILayout.LabelField("먼저 정리할 연결 · " + _deletionPreview.Blockers.Length + "개", ContentMakerGUI.SectionTitle);
                        foreach (string blocker in _deletionPreview.Blockers)
                            EditorGUILayout.HelpBox(blocker, MessageType.Warning);
                    }
                    else
                        EditorGUILayout.HelpBox("검사에서 삭제를 막는 참조를 찾지 못했습니다. 실제 이동 직전에 한 번 더 검사합니다.", MessageType.Info);
                    EditorGUILayout.Space(8f);
                    using (new EditorGUI.DisabledScope(!_deletionPreview.CanDelete))
                    {
                        if (ContentMakerGUI.SecondaryButton("확인 후 휴지통으로 이동"))
                            ConfirmDeletion(context, region);
                    }
                }
            }
            DrawBack(context);
        }

        private void ConfirmDeletion(ContentMakerContext context, bool region)
        {
            ContentMakerDeletionPreview preview = _deletionPreview;
            if (preview == null || !preview.CanDelete) return;
            if (!EditorUtility.DisplayDialog(preview.Title,
                    "위 목록의 " + preview.Paths.Length + "개 경로를 휴지통으로 이동합니다.\n\n" +
                    (region ? preview.RegionPath : AssetDatabase.GetAssetPath(preview.Room)) +
                    "\n\nUndo로 되돌릴 수 없습니다. 대상과 연결을 확인하셨나요?", "휴지통으로 이동", "취소")) return;

            Run(context, () =>
            {
                string result;
                try
                {
                    result = ContentMakerDeletionService.MoveToTrash(preview);
                    Dispose();
                    context.Room = null;
                    context.SelectedAsset = null;
                    if (region)
                    {
                        context.RegionPath = string.Empty;
                        context.Dialogue = null;
                    }
                }
                finally
                {
                    _deletionPreview = null;
                    // Partial failures also need a fresh list; a stale preview cannot run again.
                    context.RefreshAssets();
                    context.MapPage = ContentMakerMapPage.RegionDetails;
                }
                context.Report(result);
                Navigate(context, context.Room != null ? ContentMakerMapPage.RoomEdit : ContentMakerMapPage.RegionDetails);
            });
        }

        private static void DrawBack(ContentMakerContext context)
        {
            if (string.IsNullOrEmpty(context.RegionPath)) return;
            EditorGUILayout.Space(4f);
            if (ContentMakerGUI.SecondaryButton("챕터 정보로 돌아가기"))
                Navigate(context, ContentMakerMapPage.RegionDetails);
        }

        private static void Navigate(ContentMakerContext context, ContentMakerMapPage page)
        {
            context.MapPage = page;
            GUI.FocusControl(null);
            GUIUtility.ExitGUI();
        }

        private static void Ping(UnityEngine.Object target)
        {
            if (target == null) return;
            Selection.activeObject = target;
            EditorGUIUtility.PingObject(target);
        }

        public void Dispose()
        {
            _roomObject?.Dispose();
            _areaObject?.Dispose();
            _roomObject = null;
            _areaObject = null;
            _lastRoom = null;
        }

        private static void Run(ContentMakerContext context, Action action)
        {
            try { action(); }
            catch (ExitGUIException) { throw; }
            catch (Exception exception) { context.Report(exception.Message, MessageType.Error); }
        }
    }
}
