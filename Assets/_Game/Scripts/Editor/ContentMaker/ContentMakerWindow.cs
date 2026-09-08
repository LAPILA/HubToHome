using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace HubToHome.EditorTools.ContentMaker
{
    public sealed class ContentMakerWindow : EditorWindow
    {
        private const string PreferencePrefix = "HubToHome.ContentMaker.";
        private const string StyleSheetPath = "Assets/_Game/Scripts/Editor/ContentMaker/ContentMakerWindow.uss";
        private static readonly string[] PageNames = { "챕터 · 방", "마커 · NPC", "대사 · 화자" };
        private static readonly string[] PageHints = { "목록 관리 · 지형과 음악", "시작점과 문 · 인물 배치", "대본과 표정 · 엑셀 CSV" };
        private readonly List<RoomDefinition> _rooms = new List<RoomDefinition>();
        private readonly List<DialogueData> _dialogues = new List<DialogueData>();
        private readonly List<RoomDefinition> _allRooms = new List<RoomDefinition>();
        private readonly List<DialogueData> _allDialogues = new List<DialogueData>();
        private readonly List<string> _regions = new List<string>();
        private ContentMakerContext _context;
        private ContentMakerMapPanel _mapPanel;
        private ContentMakerMarkerPanel _markerPanel;
        private ContentMakerDialoguePanel _dialoguePanel;
        private ListView _regionList;
        private ListView _roomList;
        private ListView _dialogueList;
        private VisualElement _roomSection;
        private VisualElement _dialogueSection;
        private Button _addRoomButton, _deleteRoomButton, _deleteRegionButton;
        private Label _regionCount, _regionEmpty;
        private Label _contextLabel;
        private Label _pathLabel;
        private HelpBox _statusBox;
        private IMGUIContainer _editorHost;
        private IMGUIContainer _validationHost;
        private Foldout _validationFoldout;
        private Button[] _pageButtons;
        private Button _validateButton;
        private ScrollView _editorScroll;
        private Label _roomCount, _dialogueCount, _roomEmpty, _dialogueEmpty;
        private ObjectField _roomField;
        private ObjectField _dialogueField;
        private RoomMapValidationReport _validationReport;
        private string _filter = string.Empty;
        private string _status = "챕터와 방을 선택해 편집하세요. 목록 옆의 추가·삭제 버튼으로 관리할 수 있습니다.";
        private MessageType _statusType = MessageType.Info;
        private int _page;
        private bool _refreshScheduled;

        [MenuItem("Hub To Home/제작/콘텐츠 메이커", false, 10)]
        public static void Open()
        {
            ContentMakerWindow window = GetWindow<ContentMakerWindow>();
            window.titleContent = new GUIContent("콘텐츠 메이커");
            window.minSize = new Vector2(880, 600);
            window.Show();
        }

        private void OnEnable()
        {
            _context = _context ?? new ContentMakerContext();
            _context.RegionPath = EditorPrefs.GetString(PreferencePrefix + "Region", ContentMakerAssetUtility.RegionsRoot + "/Chapter01");
            _context.SelectionChanged = OnContentSelected;
            _context.MapPageChanged = OnMapPageChanged;
            _context.RefreshRequested = ScheduleRefresh;
            _context.StatusChanged = SetStatus;
            _mapPanel = new ContentMakerMapPanel();
            _markerPanel = new ContentMakerMarkerPanel();
            _dialoguePanel = new ContentMakerDialoguePanel();
            EditorApplication.projectChanged += ScheduleRefresh;
            EditorApplication.hierarchyChanged += OnHierarchyChanged;
            Undo.undoRedoPerformed += OnUndoRedo;
            _page = Mathf.Clamp(EditorPrefs.GetInt(PreferencePrefix + "Page", 0), 0, 2);
        }

        private void OnDisable()
        {
            EditorApplication.projectChanged -= ScheduleRefresh;
            EditorApplication.hierarchyChanged -= OnHierarchyChanged;
            EditorApplication.delayCall -= RefreshAssets;
            Undo.undoRedoPerformed -= OnUndoRedo;
            _refreshScheduled = false;
            if (_context != null) EditorPrefs.SetString(PreferencePrefix + "Region", _context.RegionPath ?? string.Empty);
            if (_context != null) _context.MapPageChanged = null;
            EditorPrefs.SetInt(PreferencePrefix + "Page", _page);
            DisposePanel(_mapPanel);
            DisposePanel(_markerPanel);
            DisposePanel(_dialoguePanel);
        }

        private static void DisposePanel(object panel)
        {
            if (panel is IDisposable disposable) disposable.Dispose();
        }

        public void CreateGUI()
        {
            titleContent = new GUIContent("콘텐츠 메이커");
            minSize = new Vector2(880, 600);
            rootVisualElement.Clear();
            rootVisualElement.AddToClassList("hth-maker");
            rootVisualElement.EnableInClassList("cm-light", !EditorGUIUtility.isProSkin);
            StyleSheet sheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(StyleSheetPath);
            if (sheet == null)
            {
                // A newly imported editor script can become available before its stylesheet.
                _regionList = null;
                _editorHost = null;
                _pageButtons = null;
                _validateButton = null;
                rootVisualElement.Add(new HelpBox("편집창 스타일을 아직 불러오지 못했습니다. Unity 자산 처리가 끝난 뒤 화면을 다시 불러와 주세요.\n" + StyleSheetPath, HelpBoxMessageType.Warning));
                rootVisualElement.Add(new Button(CreateGUI) { text = "화면 다시 불러오기", style = { height = 32, marginTop = 8 } });
                return;
            }
            if (!rootVisualElement.styleSheets.Contains(sheet))
                rootVisualElement.styleSheets.Add(sheet);

            VisualElement header = Styled(new VisualElement(), "cm-header");
            VisualElement brand = Styled(new VisualElement(), "cm-brand");
            brand.Add(Styled(new Label("HUB TO HOME  /  제작 도구"), "cm-eyebrow"));
            brand.Add(Styled(new Label("콘텐츠 메이커"), "cm-title"));
            header.Add(brand);
            VisualElement actions = Styled(new VisualElement(), "cm-actions");
            _validateButton = MakeButton("방 검사", ValidateSelectedRoom);
            _validateButton.tooltip = "선택한 방의 구성을 검사합니다. 자산을 자동 수정하지 않습니다.";
            actions.Add(_validateButton);
            actions.Add(MakeButton("시퀀스 메이커 ↗", OpenSequenceMaker));
            actions.Add(MakeButton("사용 순서", ShowGuide));
            header.Add(actions);
            rootVisualElement.Add(header);

            VisualElement tabs = Styled(new VisualElement(), "cm-tabs");
            _pageButtons = new Button[PageNames.Length];
            for (int index = 0; index < PageNames.Length; index++)
            {
                int captured = index;
                Button button = Styled(new Button(() => SetPage(captured)), "cm-tab");
                button.tooltip = PageHints[index];
                button.Add(Styled(new Label((index + 1).ToString("00")) { pickingMode = PickingMode.Ignore }, "cm-tab-number"));
                VisualElement words = new VisualElement { pickingMode = PickingMode.Ignore };
                words.Add(Styled(new Label(PageNames[index]) { pickingMode = PickingMode.Ignore }, "cm-tab-label"));
                words.Add(Styled(new Label(PageHints[index]) { pickingMode = PickingMode.Ignore }, "cm-tab-hint"));
                button.Add(words);
                _pageButtons[index] = button;
                tabs.Add(button);
            }
            rootVisualElement.Add(tabs);

            TwoPaneSplitView split = Styled(new TwoPaneSplitView(0, 260, TwoPaneSplitViewOrientation.Horizontal), "cm-split");
            split.viewDataKey = PreferencePrefix + "Split";
            rootVisualElement.Add(split);
            VisualElement navigation = Styled(new VisualElement(), "cm-navigation");
            split.Add(navigation);
            BuildNavigation(navigation);

            VisualElement workspace = Styled(new VisualElement(), "cm-workspace");
            split.Add(workspace);
            VisualElement context = Styled(new VisualElement(), "cm-context");
            _contextLabel = Styled(new Label(), "cm-context-title");
            _pathLabel = Styled(new Label(), "cm-path");
            context.Add(_contextLabel);
            context.Add(_pathLabel);
            workspace.Add(context);

            _editorScroll = Styled(new ScrollView(ScrollViewMode.Vertical), "cm-editor-scroll");
            _editorHost = Styled(new IMGUIContainer(DrawCurrentPage), "cm-editor");
            _editorScroll.Add(_editorHost);
            workspace.Add(_editorScroll);

            _validationFoldout = Styled(new Foldout { text = "방 검사 결과", value = false }, "cm-validation");
            ScrollView validationScroll = Styled(new ScrollView(ScrollViewMode.Vertical), "cm-validation-scroll");
            _validationHost = new IMGUIContainer(DrawValidation);
            validationScroll.Add(_validationHost);
            _validationFoldout.Add(validationScroll);
            workspace.Add(_validationFoldout);

            _statusBox = Styled(new HelpBox(_status, HelpBoxMessageType.Info), "cm-status");
            rootVisualElement.Add(_statusBox);
            RefreshAssets();
            SetPage(_page);
            SetStatus(_status, _statusType);
        }

        private void BuildNavigation(VisualElement navigation)
        {
            VisualElement regionSection = Styled(new VisualElement(), "cm-region-section");
            VisualElement regionHeading = ListHeading("챕터 (지역)", out _regionCount);
            regionHeading.Add(ListAction("+ 추가", "새 챕터 폴더 만들기", () => ShowMapPage(ContentMakerMapPage.NewRegion)));
            _deleteRegionButton = ListAction("삭제", "선택한 챕터의 삭제 대상과 참조 확인", () => ShowMapPage(ContentMakerMapPage.DeleteRegion));
            regionHeading.Add(_deleteRegionButton);
            regionSection.Add(regionHeading);
            _regionList = new ListView(_regions, 30, () =>
            {
                Label row = Styled(new Label(), "cm-region-row");
                row.RegisterCallback<ClickEvent>(evt =>
                {
                    if (evt.button == 0 && row.userData is string region) SelectRegion(region);
                });
                return row;
            }, (element, index) =>
            {
                string path = index >= 0 && index < _regions.Count ? _regions[index] : string.Empty;
                ((Label)element).text = Path.GetFileName(path);
                element.tooltip = path;
                element.userData = path;
            });
            _regionList.AddToClassList("cm-list");
            _regionList.selectionType = SelectionType.Single;
            _regionList.selectionChanged += selection =>
            {
                foreach (object item in selection)
                    if (item is string region) { SelectRegion(region); break; }
            };
            regionSection.Add(_regionList);
            _regionEmpty = Styled(new Label("챕터가 없습니다. '+ 추가'로 시작하세요."), "cm-empty");
            regionSection.Add(_regionEmpty);
            navigation.Add(regionSection);

            ToolbarSearchField search = Styled(new ToolbarSearchField { tooltip = "현재 챕터의 방·대화를 이름 또는 방 ID로 찾기" }, "cm-search");
            search.SetValueWithoutNotify(_filter);
            search.RegisterValueChangedCallback(change => { _filter = change.newValue; FilterLists(); });
            navigation.Add(search);

            _roomSection = Styled(new VisualElement(), "cm-list-section");
            VisualElement roomHeading = ListHeading("방 목록", out _roomCount);
            _addRoomButton = ListAction("+ 추가", "선택한 챕터에 새 방 만들기", () => ShowMapPage(ContentMakerMapPage.NewRoom));
            roomHeading.Add(_addRoomButton);
            _deleteRoomButton = ListAction("삭제", "선택한 방의 삭제 대상과 참조 확인", () => ShowMapPage(ContentMakerMapPage.DeleteRoom));
            roomHeading.Add(_deleteRoomButton);
            _roomSection.Add(roomHeading);
            _roomList = MakeAssetList(_rooms, room => DisplayRoomName(room.name), room => room.RoomId, room => _context.Select(room));
            _roomList.selectionChanged += selection =>
            {
                foreach (object item in selection) if (item is RoomDefinition room) { _context.Select(room); break; }
            };
            _roomSection.Add(_roomList);
            _roomEmpty = Styled(new Label(), "cm-empty");
            _roomSection.Add(_roomEmpty);
            _roomField = Styled(new ObjectField("방 데이터 직접 선택") { objectType = typeof(RoomDefinition), allowSceneObjects = false }, "cm-reference");
            _roomField.RegisterValueChangedCallback(change =>
            {
                _context.Room = change.newValue as RoomDefinition;
                _context.Select(change.newValue);
            });
            _roomSection.Add(_roomField);
            navigation.Add(_roomSection);

            _dialogueSection = Styled(new VisualElement(), "cm-list-section");
            VisualElement dialogueHeading = ListHeading("대화 목록", out _dialogueCount);
            dialogueHeading.Add(ListAction("+ 추가", "선택한 챕터에 새 대화 만들기", () =>
            {
                _dialoguePanel.ShowCreate();
                SetPage(2);
            }));
            _dialogueSection.Add(dialogueHeading);
            _dialogueList = MakeAssetList(_dialogues, dialogue => dialogue.name, dialogue => (dialogue.Nodes?.Count ?? 0) + "줄의 대사", dialogue => _context.Select(dialogue));
            _dialogueList.selectionChanged += selection =>
            {
                foreach (object item in selection) if (item is DialogueData dialogue) { _context.Select(dialogue); break; }
            };
            _dialogueSection.Add(_dialogueList);
            _dialogueEmpty = Styled(new Label(), "cm-empty");
            _dialogueSection.Add(_dialogueEmpty);
            _dialogueField = Styled(new ObjectField("공용 / 다른 대화") { objectType = typeof(DialogueData), allowSceneObjects = false }, "cm-reference");
            _dialogueField.RegisterValueChangedCallback(change =>
            {
                _context.Dialogue = change.newValue as DialogueData;
                _context.Select(change.newValue);
            });
            _dialogueSection.Add(_dialogueField);
            navigation.Add(_dialogueSection);

            VisualElement tools = Styled(new VisualElement(), "cm-navigation-tools");
            Button refresh = MakeButton("목록 새로고침", ScheduleRefresh);
            refresh.style.flexGrow = 1;
            tools.Add(refresh);
            tools.Add(MakeButton("파일 위치", () => Ping(CurrentPageAsset)));
            navigation.Add(tools);
        }

        private Object CurrentPageAsset
        {
            get
            {
                if (_context == null) return null;
                if (_page == 2) return _context.Dialogue;
                if (_page == 0 && (_context.MapPage == ContentMakerMapPage.NewRegion || _context.MapPage == ContentMakerMapPage.NewRoom)) return null;
                if (_page == 0 && (_context.MapPage == ContentMakerMapPage.RegionDetails || _context.MapPage == ContentMakerMapPage.DeleteRegion))
                    return AssetDatabase.LoadAssetAtPath<DefaultAsset>(_context.RegionPath);
                return _context.Room;
            }
        }

        private void SelectRegion(string region)
        {
            if (_context.RegionPath != region)
            {
                _context.RegionPath = region;
                _context.Room = null;
                _context.Dialogue = null;
                _context.SelectedAsset = null;
                _validationReport = null;
                RefreshAssets();
            }
            if (_page == 0) ShowMapPage(ContentMakerMapPage.RegionDetails);
            else UpdateContextLabels();
        }

        private void ShowMapPage(ContentMakerMapPage page)
        {
            _context.MapPage = page;
            SetPage(0);
            if (_editorScroll != null) _editorScroll.scrollOffset = Vector2.zero;
        }

        private void OnMapPageChanged()
        {
            if (_page != 0) return;
            if (_editorScroll != null) _editorScroll.scrollOffset = Vector2.zero;
            UpdateContextLabels();
            _editorHost?.MarkDirtyRepaint();
        }

        private static T Styled<T>(T element, string className) where T : VisualElement
        {
            element.AddToClassList(className);
            return element;
        }

        private static Button MakeButton(string text, Action action) =>
            Styled(new Button(action) { text = text }, "cm-button");

        private static Button ListAction(string text, string tooltip, Action action) =>
            Styled(new Button(action) { text = text, tooltip = tooltip }, "cm-list-action");

        private static VisualElement ListHeading(string title, out Label count)
        {
            VisualElement heading = Styled(new VisualElement(), "cm-section-head");
            heading.Add(SectionLabel(title));
            count = Styled(new Label(), "cm-count");
            count.style.flexGrow = 1;
            heading.Add(count);
            return heading;
        }

        private static string DisplayRoomName(string name)
        {
            if (name.StartsWith("Room_", StringComparison.Ordinal)) name = name.Substring(5);
            if (name.EndsWith("_Definition", StringComparison.Ordinal)) name = name.Substring(0, name.Length - 11);
            return name;
        }

        private static Label SectionLabel(string text) => Styled(new Label(text), "cm-section-title");

        private void OnFocus()
        {
            rootVisualElement.EnableInClassList("cm-light", !EditorGUIUtility.isProSkin);
        }

        private static ListView MakeAssetList<T>(List<T> items, Func<T, string> title, Func<T, string> subtitle, Action<T> onClick) where T : Object
        {
            ListView list = new ListView(items, 46, () =>
            {
                VisualElement row = Styled(new VisualElement(), "cm-asset-row");
                row.Add(Styled(new Label { name = "title" }, "cm-asset-title"));
                row.Add(Styled(new Label { name = "subtitle" }, "cm-asset-subtitle"));
                row.RegisterCallback<ClickEvent>(evt =>
                {
                    if (evt.button == 0 && row.userData is T item && item != null) onClick(item);
                });
                return row;
            }, (element, index) =>
            {
                T item = index >= 0 && index < items.Count ? items[index] : null;
                element.Q<Label>("title").text = item == null ? string.Empty : title(item);
                element.Q<Label>("subtitle").text = item == null ? string.Empty : subtitle(item);
                element.tooltip = item == null ? string.Empty : AssetDatabase.GetAssetPath(item);
                element.userData = item;
            });
            list.selectionType = SelectionType.Single;
            list.AddToClassList("cm-list");
            list.showAlternatingRowBackgrounds = AlternatingRowBackground.None;
            return list;
        }

        private void SetPage(int page)
        {
            bool changed = _page != Mathf.Clamp(page, 0, 2);
            _page = Mathf.Clamp(page, 0, 2);
            if (_pageButtons != null)
                for (int index = 0; index < _pageButtons.Length; index++) _pageButtons[index].EnableInClassList("is-selected", index == _page);
            if (_roomSection != null) _roomSection.style.display = _page == 2 ? DisplayStyle.None : DisplayStyle.Flex;
            if (_dialogueSection != null) _dialogueSection.style.display = _page == 2 ? DisplayStyle.Flex : DisplayStyle.None;
            if (changed && _editorScroll != null) _editorScroll.scrollOffset = Vector2.zero;
            _editorHost?.MarkDirtyRepaint();
            UpdateContextLabels();
        }

        private void DrawCurrentPage()
        {
            if (_context == null) return;
            bool playing = EditorApplication.isPlayingOrWillChangePlaymode;
            if (playing) EditorGUILayout.HelpBox("게임 재생 중에는 제작 데이터를 변경하지 않습니다. 재생을 종료한 뒤 편집해 주세요.", MessageType.Info);
            using (new EditorGUI.DisabledScope(playing))
            using (ContentMakerGUI.FormScope(_editorHost?.contentRect.width ?? 500f))
            {
                if (_page == 0) _mapPanel.OnGUI(_context);
                else if (_page == 1) _markerPanel.OnGUI(_context);
                else _dialoguePanel.OnGUI(_context);
            }
        }

        private void OnContentSelected(Object target)
        {
            if (target is DialogueData) SetPage(2);
            if (target is RoomDefinition selectedRoom)
            {
                _context.MapPage = ContentMakerMapPage.RoomEdit;
                _validationReport = null;
                string region = GetOwningRegion(selectedRoom);
                if (region == null)
                {
                    _context.Room = null;
                    _context.SelectedAsset = null;
                    SetStatus("본편 Regions 폴더 안의 방 데이터를 선택해 주세요. 개발 샘플은 프로젝트 창에서 확인할 수 있습니다.", MessageType.Warning);
                }
                else if (_context.RegionPath != region)
                {
                    _context.RegionPath = region;
                    _context.Dialogue = null;
                    ScheduleRefresh();
                }
                if (_page == 2) SetPage(0);
            }
            _roomField?.SetValueWithoutNotify(_context.Room);
            _dialogueField?.SetValueWithoutNotify(_context.Dialogue);
            RestoreListSelection(_roomList, _rooms.IndexOf(_context.Room));
            RestoreListSelection(_dialogueList, _dialogues.IndexOf(_context.Dialogue));
            RestoreListSelection(_regionList, _regions.IndexOf(_context.RegionPath));
            UpdateContextLabels();
            _editorHost?.MarkDirtyRepaint();
            Repaint();
        }

        private static string GetOwningRegion(Object target)
        {
            string path = AssetDatabase.GetAssetPath(target);
            string prefix = ContentMakerAssetUtility.RegionsRoot + "/";
            if (!path.StartsWith(prefix, StringComparison.Ordinal)) return null;
            int slash = path.IndexOf('/', prefix.Length);
            return slash >= 0 ? path.Substring(0, slash) : null;
        }

        private void UpdateContextLabels()
        {
            if (_contextLabel == null || _context == null) return;
            Object target = CurrentPageAsset;
            string regionName = Path.GetFileName(_context.RegionPath ?? string.Empty);
            string selectedName = target != null ? (_page == 2 ? target.name : DisplayRoomName(target.name)) : (_page == 2 ? "대화 미선택" : "방 미선택");
            string taskName = _page == 0 ? MapPageTitle(_context.MapPage) : PageNames[_page];
            _contextLabel.text = string.IsNullOrEmpty(regionName) ? taskName : regionName + "  /  " + taskName;
            if (_page != 0 || (_context.MapPage != ContentMakerMapPage.RegionDetails && _context.MapPage != ContentMakerMapPage.DeleteRegion
                && _context.MapPage != ContentMakerMapPage.NewRegion && _context.MapPage != ContentMakerMapPage.NewRoom))
                _contextLabel.text += "  /  " + selectedName;
            _pathLabel.text = target != null ? AssetDatabase.GetAssetPath(target) : (_context.MapPage == ContentMakerMapPage.NewRegion && _page == 0
                ? ContentMakerAssetUtility.RegionsRoot : (_context.RegionPath ?? string.Empty));
            _contextLabel.tooltip = _contextLabel.text;
            _pathLabel.tooltip = _pathLabel.text;
            _validateButton?.SetEnabled(_context.Room != null && _context.Room.RoomPrefab != null);
            bool hasRegion = !string.IsNullOrEmpty(_context.RegionPath) && _regions.Contains(_context.RegionPath);
            _addRoomButton?.SetEnabled(hasRegion);
            _deleteRegionButton?.SetEnabled(hasRegion);
            _deleteRoomButton?.SetEnabled(hasRegion && _context.Room != null);
        }

        private static string MapPageTitle(ContentMakerMapPage page)
        {
            switch (page)
            {
                case ContentMakerMapPage.RegionDetails: return "챕터 정보";
                case ContentMakerMapPage.NewRegion: return "새 챕터";
                case ContentMakerMapPage.NewRoom: return "새 방";
                case ContentMakerMapPage.RoomDuplicate: return "방 복제";
                case ContentMakerMapPage.RoomScene: return "씬 관리";
                case ContentMakerMapPage.DeleteRoom: return "방 삭제 확인";
                case ContentMakerMapPage.DeleteRegion: return "챕터 삭제 확인";
                default: return "방 설정";
            }
        }

        private void ScheduleRefresh()
        {
            if (_refreshScheduled) return;
            _refreshScheduled = true;
            EditorApplication.delayCall += RefreshAssets;
        }

        private void RefreshAssets()
        {
            _refreshScheduled = false;
            EditorApplication.delayCall -= RefreshAssets;
            if (this == null || _context == null || _regionList == null) return;
            _regions.Clear();
            if (AssetDatabase.IsValidFolder(ContentMakerAssetUtility.RegionsRoot))
                _regions.AddRange(AssetDatabase.GetSubFolders(ContentMakerAssetUtility.RegionsRoot));
            _regions.Sort(StringComparer.OrdinalIgnoreCase);
            int regionIndex = _regions.IndexOf(_context.RegionPath);
            if (regionIndex < 0)
            {
                _context.RegionPath = _regions.Count > 0 ? _regions[0] : string.Empty;
                _context.Room = null;
                _context.Dialogue = null;
                _context.SelectedAsset = null;
                regionIndex = _regions.Count > 0 ? 0 : -1;
            }
            _regionList.Rebuild();
            RestoreListSelection(_regionList, regionIndex);
            _regionCount.text = _regions.Count + "개";
            _regionList.style.display = _regions.Count == 0 ? DisplayStyle.None : DisplayStyle.Flex;
            _regionEmpty.style.display = _regions.Count == 0 ? DisplayStyle.Flex : DisplayStyle.None;
            _allRooms.Clear();
            _allDialogues.Clear();
            if (AssetDatabase.IsValidFolder(_context.RegionPath))
            {
                LoadAssets(_allRooms, _context.RegionPath, "t:RoomDefinition");
                LoadAssets(_allDialogues, _context.RegionPath, "t:DialogueData");
            }
            if (_context.Room != null && !_allRooms.Contains(_context.Room)) _context.Room = null;
            if (_context.Room == null && _allRooms.Count > 0) _context.Room = _allRooms[0];
            FilterLists();
            _validationReport = null;
            _roomField?.SetValueWithoutNotify(_context.Room);
            _dialogueField?.SetValueWithoutNotify(_context.Dialogue);
            UpdateContextLabels();
            _editorHost?.MarkDirtyRepaint();
        }

        private static void LoadAssets<T>(List<T> target, string folder, string type) where T : Object
        {
            foreach (string guid in AssetDatabase.FindAssets(type, new[] { folder }))
            {
                T asset = AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guid));
                if (asset != null) target.Add(asset);
            }
            target.Sort((left, right) => StringComparer.OrdinalIgnoreCase.Compare(left.name, right.name));
        }

        private void FilterLists()
        {
            _rooms.Clear();
            _dialogues.Clear();
            foreach (RoomDefinition room in _allRooms)
                if (room != null && MatchesFilter(room.name + " " + room.RoomId)) _rooms.Add(room);
            foreach (DialogueData dialogue in _allDialogues)
                if (dialogue != null && MatchesFilter(dialogue.name)) _dialogues.Add(dialogue);
            _roomList?.Rebuild();
            _dialogueList?.Rebuild();
            RestoreListSelection(_roomList, _rooms.IndexOf(_context.Room));
            RestoreListSelection(_dialogueList, _dialogues.IndexOf(_context.Dialogue));
            UpdateListState(_roomList, _roomEmpty, _roomCount, _rooms.Count, _allRooms.Count, "아직 방이 없습니다. 목록 옆 '+ 추가'로 첫 방을 만드세요.");
            UpdateListState(_dialogueList, _dialogueEmpty, _dialogueCount, _dialogues.Count, _allDialogues.Count, "아직 대화가 없습니다. 대사 탭에서 작성할 수 있습니다.");
        }

        private void UpdateListState(ListView list, Label empty, Label count, int visible, int total, string emptyText)
        {
            if (list == null || empty == null || count == null) return;
            count.text = string.IsNullOrEmpty(_filter) ? total + "개" : visible + " / " + total;
            list.style.display = visible == 0 ? DisplayStyle.None : DisplayStyle.Flex;
            empty.style.display = visible == 0 ? DisplayStyle.Flex : DisplayStyle.None;
            empty.text = total == 0 ? emptyText : "검색 결과가 없습니다. 검색어를 바꿔 보세요.";
        }

        private bool MatchesFilter(string value) => string.IsNullOrEmpty(_filter)
            || value.IndexOf(_filter, StringComparison.OrdinalIgnoreCase) >= 0;

        private static void RestoreListSelection(ListView list, int index)
        {
            if (list == null) return;
            if (index >= 0) list.SetSelectionWithoutNotify(new[] { index });
            else list.SetSelectionWithoutNotify(Array.Empty<int>());
        }

        private void SetStatus(string message, MessageType type)
        {
            _status = message;
            _statusType = type;
            if (_statusBox == null) return;
            _statusBox.text = message;
            _statusBox.tooltip = message;
            _statusBox.messageType = type == MessageType.Error ? HelpBoxMessageType.Error
                : type == MessageType.Warning ? HelpBoxMessageType.Warning : HelpBoxMessageType.Info;
        }

        private void OnHierarchyChanged()
        {
            if (_validationReport != null) _validationReport = null;
            _editorHost?.MarkDirtyRepaint();
            _validationHost?.MarkDirtyRepaint();
        }

        private void OnUndoRedo()
        {
            OnHierarchyChanged();
            ScheduleRefresh();
        }

        private void ValidateSelectedRoom()
        {
            RoomDefinition room = _context?.Room;
            if (room == null || room.RoomPrefab == null) { SetStatus("검사할 방 데이터와 지형 프리팹을 먼저 선택해 주세요.", MessageType.Warning); return; }
            try
            {
                _validationReport = ContentMakerMapService.ValidateRoom(room);
                _validationFoldout.value = true;
                _validationHost.MarkDirtyRepaint();
                SetStatus("방 검사: 오류 " + _validationReport.ErrorCount + "개 / 경고 " + _validationReport.WarningCount
                    + "개. 지역 씬의 실행 구성은 별도 확인이 필요합니다.", _validationReport.HasErrors ? MessageType.Warning : MessageType.Info);
            }
            catch (Exception exception) { SetStatus(exception.Message, MessageType.Error); }
        }

        private void DrawValidation()
        {
            if (_validationReport == null) { EditorGUILayout.LabelField("방 검사를 누르면 결과를 표시합니다. 편집 후에는 다시 검사해 주세요.", EditorStyles.wordWrappedLabel); return; }
            if (_validationReport.Issues.Count == 0) EditorGUILayout.HelpBox("방 검사에서 발견된 문제가 없습니다.", MessageType.Info);
            foreach (RoomMapValidationIssue issue in _validationReport.Issues)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.HelpBox(issue.Message, issue.Severity == RoomMapValidationSeverity.Error ? MessageType.Error : MessageType.Warning);
                    using (new EditorGUI.DisabledScope(!issue.CanSelect || EditorApplication.isPlayingOrWillChangePlaymode))
                        if (GUILayout.Button("위치", GUILayout.Width(44), GUILayout.Height(30)))
                        {
                            try { ContentMakerMapService.SelectIssue(_context.Room, issue); }
                            catch (ExitGUIException) { throw; }
                            catch (Exception exception) { SetStatus(exception.Message, MessageType.Error); }
                        }
                }
            }
        }

        private static void Ping(Object target)
        {
            if (target == null) return;
            Selection.activeObject = target;
            EditorGUIUtility.PingObject(target);
        }

        private static void OpenSequenceMaker() => SequenceMakerWindow.Open();

        private static void ShowGuide()
        {
            EditorUtility.DisplayDialog("콘텐츠 제작 순서",
                "1. 왼쪽 챕터 목록에서 지역을 선택합니다. 챕터·방 목록 옆 ‘+ 추가’로 만들고, ‘삭제’에서는 대상과 연결을 확인한 뒤 휴지통으로 보냅니다.\n\n"
                + "2. 방 프리팹을 열어 지형을 배치합니다. ‘마커 · NPC’에서 문·시작점·NPC를 추가합니다.\n\n"
                + "3. ‘대사 · 화자’의 목록 옆 ‘+ 추가’로 대화를 만듭니다. 대사/화자·표정/조건·NPC/CSV·번역/문서 중 필요한 작업을 선택하세요. 조건 문서는 선택한 방의 일반 NPC에 연결할 수 있습니다.\n\n"
                + "4. 방 목록에서 선택 → 방 설정/복제/씬 관리 중 필요한 작업을 고릅니다. 방 검사 후 각 편집 화면에서 저장합니다. 프리팹 편집의 저장 상태도 확인해 주세요.\n\n"
                + "복잡한 전투·연출 흐름은 기존 시퀀스 메이커에서 작성합니다. 새 지역 씬은 빌드 목록에 자동 등록하지 않습니다.", "확인");
        }
    }
}
