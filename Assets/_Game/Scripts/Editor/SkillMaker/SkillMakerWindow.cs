using System;
using System.Collections.Generic;
using System.IO;
using HubToHome.EditorTools.ContentMaker;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace HubToHome.EditorTools.SkillMaker
{
    public sealed class SkillMakerWindow : EditorWindow
    {
        private const string SharedStyle = "Assets/_Game/Scripts/Editor/ContentMaker/ContentMakerWindow.uss";
        private const string WindowStyle = "Assets/_Game/Scripts/Editor/SkillMaker/SkillMakerWindow.uss";
        private static readonly string[] Profiles = { "전체 스킬", "아군 전용", "적 전용", "공용" };
        private static readonly string[] Pages = { "기본 정보", "실행 블록", "검사 · 시간축" };
        [SerializeField] private SkillData _skill;
        [SerializeField] private int _page = 1;
        [SerializeField] private int _blockIndex = -1;
        [SerializeField] private string _query = string.Empty;
        [SerializeField] private int _profile;
        private readonly List<SkillData> _all = new List<SkillData>();
        private readonly List<SkillData> _filtered = new List<SkillData>();
        private readonly List<BlockRow> _blocks = new List<BlockRow>();
        private SkillMakerBlockEditor _blockEditor;
        private SerializedObject _serialized;
        private EnemyAttackAuthoringReport _report;
        private ProjectContentSnapshot _referenceSnapshot;
        private ContentValidationReport _referenceReport;
        private string _referenceError;
        private int _catalogDirtyCount = -1;
        private ListView _skillList, _blockList;
        private ToolbarSearchField _search;
        private DropdownField _profileField;
        private Label _count, _title, _subtitle, _path, _metrics, _blockTitle, _emptyList, _emptyBlock;
        private Image _icon;
        private VisualElement _workspace, _emptyWorkspace, _blockTools;
        private VisualElement[] _panels;
        private Button[] _tabs;
        private Button _create, _copy, _save, _addBlock, _copyBlock, _removeBlock, _up, _down;
        private HelpBox _status;
        private IMGUIContainer _blockDetails;
        private VisualElement _validation;
        private bool _refreshScheduled, _detailsScheduled;
        private int _dirtyCount = -1;
        private bool _editable;
        private double _nextEditCheck;
        private string _editReason = string.Empty;
        private string _analysisError;
        private bool _bindingLibrary, _bindingBlocks;

        private sealed class BlockRow
        {
            public string Title, Timing, Category;
            public bool Enabled;
        }

        [MenuItem("Hub To Home/제작/스킬 메이커", false, 12)]
        public static void Open()
        {
            SkillMakerWindow window = GetWindow<SkillMakerWindow>();
            window.titleContent = new GUIContent("스킬 메이커");
            window.minSize = new Vector2(900, 640);
            window.Show();
            if (Selection.activeObject is SkillData skill) window.SelectSkill(skill);
        }

        private void OnEnable()
        {
            _blockEditor = new SkillMakerBlockEditor();
            _blockEditor.Changed += ScheduleDetails;
            EditorApplication.projectChanged += ScheduleRefresh;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
            Undo.undoRedoPerformed += OnUndo;
        }

        private void OnDisable()
        {
            EditorApplication.projectChanged -= ScheduleRefresh;
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            EditorApplication.delayCall -= RefreshLibrary;
            EditorApplication.delayCall -= RefreshDetails;
            Undo.undoRedoPerformed -= OnUndo;
            _refreshScheduled = _detailsScheduled = false;
            _serialized?.Dispose();
            _serialized = null;
            _blockEditor?.Dispose();
            _blockEditor = null;
        }

        public void CreateGUI()
        {
            VisualElement root = rootVisualElement;
            root.Clear();
            root.AddToClassList("hth-maker");
            root.AddToClassList("hth-skill-maker");
            ApplyTheme();
            AddStyle(root, SharedStyle);
            AddStyle(root, WindowStyle);
            VisualElement header = Element("cm-header");
            VisualElement brand = Element("cm-brand");
            brand.Add(Text("HUB TO HOME  /  COMBAT TOOLS", "cm-eyebrow"));
            brand.Add(Text("스킬 메이커", "cm-title"));
            header.Add(brand);
            VisualElement actions = Element("cm-actions");
            _create = Button("새 스킬", CreateSkill, actions);
            _copy = Button("스킬 복제", DuplicateSkill, actions);
            _save = Button("저장", SaveSkill, actions, "sm-primary");
            header.Add(actions);
            root.Add(header);

            var split = new TwoPaneSplitView(0, 252, TwoPaneSplitViewOrientation.Horizontal);
            split.AddToClassList("cm-split");
            root.Add(split);
            VisualElement navigation = Element("cm-navigation");
            navigation.style.minWidth = 220;
            VisualElement section = Element("cm-section-head");
            section.Add(Text("스킬 보관함", "cm-section-title"));
            _count = Text("", "cm-count");
            section.Add(_count);
            navigation.Add(section);
            _search = new ToolbarSearchField { value = _query };
            _search.AddToClassList("cm-search");
            _search.tooltip = "스킬 이름, ID, 폴더 경로 검색";
            _search.RegisterValueChangedCallback(evt => { _query = evt.newValue; FilterLibrary(); });
            navigation.Add(_search);
            _profileField = new DropdownField(new List<string>(Profiles), Mathf.Clamp(_profile, 0, 3));
            _profileField.RegisterValueChangedCallback(_ => { _profile = _profileField.index; FilterLibrary(); });
            _profileField.AddToClassList("sm-filter");
            navigation.Add(_profileField);
            _skillList = new ListView(_filtered, 61, MakeSkillRow, BindSkillRow)
            { selectionType = SelectionType.Single };
            _skillList.AddToClassList("cm-list");
            _skillList.selectionChanged += selected =>
            {
                if (_bindingLibrary) return;
                foreach (object item in selected) { SelectSkill(item as SkillData); break; }
            };
            navigation.Add(_skillList);
            _emptyList = Text("", "cm-empty");
            navigation.Add(_emptyList);
            Button("목록 새로고침", ScheduleRefresh, navigation);
            navigation.Add(Text("기존 자산을 직접 편집합니다.\n변경 취소: Ctrl+Z · 선택 자산만 저장", "sm-note"));
            split.Add(navigation);

            VisualElement right = Element("cm-workspace");
            right.style.minWidth = 590;
            split.Add(right);
            _emptyWorkspace = Element("sm-welcome");
            _emptyWorkspace.Add(Text("만들고 싶은 스킬을 선택하세요", "sm-welcome-title"));
            _emptyWorkspace.Add(Text("왼쪽에서 기존 스킬을 찾거나 ‘새 스킬’로 시작하세요.\n기본 정보 → 실행 블록 → 검사 순서로 제작합니다.", "cm-empty"));
            right.Add(_emptyWorkspace);
            _workspace = Element("sm-workspace");
            right.Add(_workspace);
            BuildSummary();
            BuildPages();
            _status = new HelpBox("선택한 스킬만 편집·저장합니다. 실행 미리보기는 제공하지 않습니다.", HelpBoxMessageType.Info);
            _status.AddToClassList("cm-status");
            root.Add(_status);
            RefreshLibrary();
        }

        private void BuildSummary()
        {
            VisualElement summary = Element("sm-summary");
            _icon = new Image { scaleMode = ScaleMode.ScaleToFit };
            _icon.AddToClassList("sm-icon");
            summary.Add(_icon);
            VisualElement info = Element("cm-brand");
            _subtitle = Text("", "cm-eyebrow");
            _title = Text("", "sm-skill-title");
            _path = Text("", "cm-path");
            _metrics = Text("", "sm-metrics");
            info.Add(_subtitle); info.Add(_title); info.Add(_path); info.Add(_metrics);
            summary.Add(info);
            Button("Project에서 보기", () => { if (_skill != null) EditorGUIUtility.PingObject(_skill); }, summary);
            _workspace.Add(summary);
        }

        private void BuildPages()
        {
            VisualElement tabs = Element("cm-tabs");
            _tabs = new Button[Pages.Length];
            _panels = new VisualElement[Pages.Length];
            for (int i = 0; i < Pages.Length; i++)
            {
                int index = i;
                _tabs[i] = new Button(() => ShowPage(index)) { text = (i + 1) + "   " + Pages[i] };
                _tabs[i].AddToClassList("cm-tab");
                tabs.Add(_tabs[i]);
            }
            _workspace.Add(tabs);
            var basic = new ScrollView();
            basic.AddToClassList("cm-editor-scroll");
            var basicHost = new IMGUIContainer(DrawBasic);
            basicHost.AddToClassList("cm-editor");
            basic.Add(basicHost);
            _panels[0] = basic;

            VisualElement blocks = Element("sm-block-page");
            _blockTools = Element("sm-block-tools");
            _addBlock = Button("＋ 블록 추가", ShowBlockMenu, _blockTools, "sm-primary");
            _up = Button("↑", () => EditBlock(() => _blockEditor.Move(_blockIndex, _blockIndex - 1)), _blockTools);
            _down = Button("↓", () => EditBlock(() => _blockEditor.Move(_blockIndex, _blockIndex + 1)), _blockTools);
            _copyBlock = Button("복제", () => EditBlock(() => _blockEditor.Duplicate(_blockIndex)), _blockTools);
            _removeBlock = Button("삭제", () => EditBlock(() => _blockEditor.Remove(_blockIndex)), _blockTools);
            _removeBlock.tooltip = "선택 블록 삭제 · Ctrl+Z로 복구할 수 있습니다.";
            blocks.Add(_blockTools);
            _blockList = new ListView(_blocks, 46, MakeBlockRow, BindBlockRow)
            { selectionType = SelectionType.Single };
            _blockList.AddToClassList("cm-list");
            _blockList.AddToClassList("sm-block-list");
            _blockList.selectionChanged += selected =>
            {
                if (_bindingBlocks) return;
                foreach (object item in selected)
                {
                    _blockIndex = _blocks.IndexOf(item as BlockRow);
                    UpdateBlockSelection();
                    break;
                }
            };
            blocks.Add(_blockList);
            _emptyBlock = Text("아직 블록이 없습니다. ‘블록 추가’에서 동작을 선택하세요.", "cm-empty");
            blocks.Add(_emptyBlock);
            _blockTitle = Text("", "sm-detail-title");
            blocks.Add(_blockTitle);
            var detailScroll = new ScrollView();
            detailScroll.AddToClassList("cm-editor-scroll");
            _blockDetails = new IMGUIContainer(DrawBlock);
            _blockDetails.AddToClassList("cm-editor");
            detailScroll.Add(_blockDetails);
            blocks.Add(detailScroll);
            _panels[1] = blocks;
            var validation = new ScrollView();
            validation.AddToClassList("cm-editor-scroll");
            _validation = Element("sm-validation");
            validation.Add(_validation);
            _panels[2] = validation;
            foreach (VisualElement panel in _panels) _workspace.Add(panel);
            ShowPage(Mathf.Clamp(_page, 0, Pages.Length - 1));
        }

        private void DrawBasic()
        {
            if (_skill == null || _serialized == null) return;
            bool editable = CanEdit(IsInputEvent());
            if (!editable) EditorGUILayout.HelpBox(_editReason, MessageType.Info);
            using (new EditorGUI.DisabledScope(!editable))
            using (ContentMakerGUI.FormScope(position.width - 310))
            {
                _serialized.UpdateIfRequiredOrScript();
                EditorGUI.BeginChangeCheck();
                using (ContentMakerGUI.Card())
                {
                    ContentMakerGUI.Header("어떤 스킬인가요?", "이름과 설명은 플레이어에게, ID는 저장·참조에 사용됩니다.");
                    Field(nameof(SkillData.Icon), "아이콘");
                    Field(nameof(SkillData.SkillName), "스킬 이름");
                    Field(nameof(SkillData.Description), "설명");
                    Popup(nameof(SkillData.UsageProfile), "사용 범위", new[] { "공용", "아군 전용", "적 전용" });
                }
                using (ContentMakerGUI.Card())
                {
                    ContentMakerGUI.Header("비용과 대상", "아군 스킬은 캐릭터의 AP를 사용합니다.");
                    Field(nameof(SkillData.APCost), "AP 소모량");
                    Popup(nameof(SkillData.TargetType), "대상", new[] { "아군", "적", "양측 (기존 값)", "전체 (기존 값)" });
                    Field(nameof(SkillData.IsAoE), "전체 대상 선택");
                    if (_skill.TargetType == TargetAreaType.Both || _skill.TargetType == TargetAreaType.AoEAll)
                        EditorGUILayout.HelpBox("현재 전투 경로별 대상 해석이 다릅니다. 일반 제작은 아군/적 + 전체 대상 선택을 사용하고 실제 대상 처리를 확인하세요.", MessageType.Warning);
                }
                using (ContentMakerGUI.Card())
                {
                    ContentMakerGUI.Header("참조 정보", "이미 사용 중인 ID를 바꾸면 저장 데이터·카탈로그 연결을 확인해야 합니다.");
                    Field(nameof(SkillData.SkillID), "고유 ID");
                    EditorGUILayout.HelpBox("새 스킬은 캐릭터의 초기 스킬 또는 스킬 트리에 연결하고, 저장 복원용 콘텐츠 카탈로그에도 등록하세요. 이 창은 자동 등록하지 않습니다.", MessageType.Info);
                }
                if (EditorGUI.EndChangeCheck())
                {
                    if (CanEdit(true))
                    {
                        if (_serialized.ApplyModifiedProperties()) ScheduleDetails();
                    }
                    else
                    {
                        _serialized.Update();
                        SetStatus(_editReason, HelpBoxMessageType.Warning);
                    }
                }
            }
        }

        private void Field(string property, string label) =>
            EditorGUILayout.PropertyField(_serialized.FindProperty(property), new GUIContent(label), true);

        private void Popup(string property, string label, string[] choices)
        {
            SerializedProperty value = _serialized.FindProperty(property);
            int selected = EditorGUILayout.Popup(label, value.intValue, choices);
            if (selected != value.intValue) value.intValue = selected;
        }

        private void DrawBlock()
        {
            if (_skill == null || _blockIndex < 0) return;
            using (new EditorGUI.DisabledScope(!CanEdit()))
            using (ContentMakerGUI.FormScope(position.width - 310))
                _blockEditor?.Draw(_blockIndex);
        }

        private void RefreshLibrary()
        {
            _refreshScheduled = false;
            EditorApplication.delayCall -= RefreshLibrary;
            if (this == null || _skillList == null) return;
            _all.Clear();
            _all.AddRange(SkillMakerAssetUtility.FindSkills());
            try
            {
                _referenceSnapshot = AssetDatabaseContentSource.CaptureSkills();
                _referenceError = null;
            }
            catch (Exception exception)
            {
                _referenceSnapshot = null;
                _referenceError = "스킬 참조 목록을 읽지 못했습니다: " + exception.GetBaseException().Message;
            }
            FilterLibrary();
        }

        private void FilterLibrary()
        {
            if (_skillList == null) return;
            RebuildFilteredList();
            SkillData next = _skill != null && _filtered.Contains(_skill) ? _skill
                : _filtered.Count > 0 ? _filtered[0] : null;
            SelectSkill(next);
        }

        private void RebuildFilteredList()
        {
            _all.RemoveAll(skill => skill == null);
            _all.Sort((left, right) =>
            {
                string leftName = string.IsNullOrWhiteSpace(left.SkillName) ? left.name : left.SkillName;
                string rightName = string.IsNullOrWhiteSpace(right.SkillName) ? right.name : right.SkillName;
                int order = StringComparer.OrdinalIgnoreCase.Compare(leftName, rightName);
                return order != 0 ? order : StringComparer.Ordinal.Compare(
                    AssetDatabase.GetAssetPath(left), AssetDatabase.GetAssetPath(right));
            });
            _filtered.Clear();
            SkillUsageProfile? profile = _profile == 1 ? SkillUsageProfile.PlayerOnly
                : _profile == 2 ? SkillUsageProfile.EnemyOnly : _profile == 3 ? SkillUsageProfile.Shared : (SkillUsageProfile?)null;
            foreach (SkillData skill in _all)
                if (SkillMakerAssetUtility.Matches(skill, _query, profile)) _filtered.Add(skill);
            _bindingLibrary = true;
            try
            {
                _skillList.Rebuild();
                int selected = _filtered.IndexOf(_skill);
                _skillList.SetSelectionWithoutNotify(selected >= 0 ? new[] { selected } : Array.Empty<int>());
            }
            finally { _bindingLibrary = false; }
            _count.text = _filtered.Count + " / " + _all.Count;
            _emptyList.text = _all.Count == 0 ? "스킬이 없습니다. 새 스킬로 시작하세요." : "검색 조건에 맞는 스킬이 없습니다.";
            _emptyList.style.display = _filtered.Count == 0 ? DisplayStyle.Flex : DisplayStyle.None;
        }

        private void SelectSkill(SkillData skill)
        {
            bool changed = _skill != skill;
            _skill = skill;
            _nextEditCheck = 0;
            if (changed) _blockIndex = -1;
            _serialized?.Dispose();
            _serialized = _skill != null ? new SerializedObject(_skill) : null;
            _blockEditor?.SetSkill(_skill);
            if (_skillList == null) return;
            int index = _filtered.IndexOf(_skill);
            _skillList.SetSelectionWithoutNotify(index >= 0 ? new[] { index } : Array.Empty<int>());
            RefreshDetails();
        }

        private void RefreshDetails()
        {
            _detailsScheduled = false;
            EditorApplication.delayCall -= RefreshDetails;
            if (this == null || _workspace == null) return;
            bool exists = _skill != null;
            _workspace.style.display = exists ? DisplayStyle.Flex : DisplayStyle.None;
            _emptyWorkspace.style.display = exists ? DisplayStyle.None : DisplayStyle.Flex;
            UpdateButtons();
            if (!exists) return;
            _dirtyCount = EditorUtility.GetDirtyCount(_skill);
            _referenceReport = null;
            if (_referenceSnapshot != null)
            {
                try
                {
                    _referenceReport = ProjectContentValidator.ValidateSkillReferences(_referenceSnapshot, _skill);
                    _referenceError = null;
                    _catalogDirtyCount = _referenceSnapshot.Catalog != null
                        ? EditorUtility.GetDirtyCount(_referenceSnapshot.Catalog) : -1;
                }
                catch (Exception exception)
                {
                    _referenceError = "스킬 참조 검사에 실패했습니다: " + exception.GetBaseException().Message;
                }
            }
            _analysisError = null;
            try { _report = EnemyAttackAuthoringAnalyzer.Analyze(_skill); }
            catch (Exception exception)
            {
                _report = null;
                _analysisError = "블록 검사 중 오류가 발생했습니다. 블록 편집은 계속할 수 있습니다.\n" + exception.GetBaseException().Message;
            }
            _title.text = string.IsNullOrWhiteSpace(_skill.SkillName) ? _skill.name : _skill.SkillName;
            _subtitle.text = ProfileName(_skill.UsageProfile) + "  /  " + _skill.SkillID;
            _path.text = AssetDatabase.GetAssetPath(_skill);
            _path.tooltip = _path.text;
            _icon.sprite = _skill.Icon;
            _metrics.text = "AP " + _skill.APCost + "   ·   블록 " + (_skill.ActionTimeline?.Count ?? 0)
                + (_report == null ? "   ·   검사 실패" : "   ·   예상 " + _report.EstimatedDuration.ToString("0.00")
                + "초   ·   검사 " + _report.ErrorCount + " 오류 / " + _report.WarningCount + " 경고");
            _blocks.Clear();
            if (_report != null)
            foreach (EnemyAttackTimelineEntry entry in _report.Entries)
            {
                string timing = !entry.Enabled ? "비활성" : !entry.TimingSupported ? "시간 미지원"
                    : entry.StartTime.ToString("0.00") + "–" + entry.EndTime.ToString("0.00") + "s" + (entry.IsVariable ? "+" : "");
                _blocks.Add(new BlockRow { Title = entry.Label, Category = entry.PhaseLabel, Timing = timing, Enabled = entry.Enabled });
            }
            if (_report == null && _skill.ActionTimeline != null)
            foreach (SkillActionBlock block in _skill.ActionTimeline)
                _blocks.Add(new BlockRow { Title = SkillMakerBlockEditor.GetBlockName(block?.GetType()),
                    Category = "검사 실패", Timing = "시간 미확인", Enabled = block != null && block.Enabled });
            _blockIndex = _blocks.Count == 0 ? -1 : Mathf.Clamp(_blockIndex, 0, _blocks.Count - 1);
            _bindingBlocks = true;
            try
            {
                _blockList.Rebuild();
                _blockList.SetSelectionWithoutNotify(_blockIndex >= 0 ? new[] { _blockIndex } : Array.Empty<int>());
            }
            finally { _bindingBlocks = false; }
            _emptyBlock.style.display = _blocks.Count == 0 ? DisplayStyle.Flex : DisplayStyle.None;
            UpdateBlockSelection();
            DrawValidation();
            // 검색 조건에서 벗어나도 편집 중인 자산은 유지하여 저장/Undo할 수 있게 합니다.
            RebuildFilteredList();
        }

        private void DrawValidation()
        {
            _validation.Clear();
            _validation.Add(Text("제작 상태 확인", "sm-detail-title"));
            _validation.Add(new HelpBox("데이터 검사이며 전투 실행 검증은 아닙니다. 가변 시간은 +, 지원하지 않는 블록 시간은 ?로 표시됩니다.", HelpBoxMessageType.Info));
            DrawReferenceValidation();
            if (_report == null)
            {
                _validation.Add(new HelpBox(_analysisError, HelpBoxMessageType.Error));
                return;
            }
            if (_skill.APCost < 0)
                _validation.Add(new HelpBox("AP 소모량은 0 이상이어야 합니다.", HelpBoxMessageType.Error));
            if (_report.Issues.Count == 0)
                _validation.Add(new HelpBox("기존 블록 검사에서 발견된 오류·경고가 없습니다.", HelpBoxMessageType.Info));
            foreach (EnemyAttackAuthoringIssue issue in _report.Issues)
            {
                VisualElement card = Element("sm-issue");
                var box = new HelpBox(issue.Message, issue.Severity == EnemyAttackAuthoringSeverity.Error
                    ? HelpBoxMessageType.Error : HelpBoxMessageType.Warning);
                card.Add(box);
                if (issue.BlockIndex >= 0)
                {
                    int index = issue.BlockIndex;
                    Button((index + 1) + "번 블록 보기", () => { _blockIndex = index; ShowPage(1); RefreshDetails(); }, card);
                }
                _validation.Add(card);
            }
            _validation.Add(Text("실행 순서 · 예상 시간", "sm-detail-title"));
            var preview = new TextField { multiline = true, isReadOnly = true, value = _report.BuildTimelinePreview() };
            preview.AddToClassList("sm-timeline");
            _validation.Add(preview);
            _validation.Add(new HelpBox("QTE 배율은 다음 피해 블록에서 소비됩니다. 방어창 누락·공통 퍼펙트 구간·상태이상 ID 등의 기존 검사 사각지대는 별도 확인이 필요합니다. 이 창은 스킬 수치를 자동 수정하지 않습니다.", HelpBoxMessageType.Warning));
        }

        private void DrawReferenceValidation()
        {
            _validation.Add(Text("ID · 저장 복원용 카탈로그", "sm-detail-title"));
            _validation.Add(new HelpBox("콘텐츠 검사의 기존 ID·카탈로그 규칙을 사용합니다. 선택한 스킬과 공유 스킬 카탈로그의 문제를 표시하며 자동 수정하지 않습니다.", HelpBoxMessageType.Info));
            Button("참조 목록 다시 검사", ScheduleRefresh, _validation);
            Button("프로젝트 콘텐츠 검사 열기", () => GetWindow<ContentValidationWindow>("콘텐츠 검사").Show(), _validation);
            if (_referenceReport == null)
            {
                _validation.Add(new HelpBox(_referenceError ?? "참조 목록을 불러오는 중입니다. 갱신되지 않으면 다시 검사하세요.", HelpBoxMessageType.Warning));
                return;
            }
            if (_referenceReport.Issues.Count == 0)
                _validation.Add(new HelpBox("선택한 스킬의 ID와 카탈로그 등록을 확인했습니다.", HelpBoxMessageType.Info));
            foreach (ContentValidationIssue issue in _referenceReport.Issues)
            {
                string guidance = issue.Code == "skill.id.duplicate" ? "같은 ID를 사용하는 스킬이 있습니다."
                    : issue.Code == "skill.id.missing" ? "스킬의 고유 ID를 입력하세요."
                    : issue.Code == "skill.id.invalid" ? "고유 ID의 형식을 확인하세요."
                    : issue.Code == "catalog.skill.missing" ? "이 스킬을 저장 복원용 콘텐츠 카탈로그에 등록하세요."
                    : "공유 스킬 카탈로그를 확인하세요.";
                _validation.Add(new HelpBox(guidance + "\n" + issue.Message,
                    issue.Severity == ContentValidationSeverity.Error ? HelpBoxMessageType.Error : HelpBoxMessageType.Warning));
                if (issue.Context != null)
                {
                    UnityEngine.Object target = issue.Context;
                    Button("해당 자산 위치", () => EditorGUIUtility.PingObject(target), _validation);
                }
            }
        }

        private void ShowPage(int page)
        {
            bool checkReferences = page == 2 && _page != page;
            _page = page;
            if (_panels == null) return;
            for (int i = 0; i < _panels.Length; i++)
            {
                _tabs[i].EnableInClassList("is-selected", i == page);
                _panels[i].style.display = i == page ? DisplayStyle.Flex : DisplayStyle.None;
            }
            if (checkReferences) ScheduleDetails();
        }

        private void UpdateBlockSelection()
        {
            bool valid = _blockIndex >= 0 && _blockIndex < _blocks.Count;
            _blockTitle.text = valid ? (_blockIndex + 1).ToString("00") + "  " + _blocks[_blockIndex].Title : "블록을 선택하세요";
            _blockDetails.MarkDirtyRepaint();
            UpdateButtons();
        }

        private void ShowBlockMenu()
        {
            if (!CanEdit()) return;
            var menu = new GenericMenu();
            foreach (Type type in SkillMakerBlockEditor.BlockTypes)
            {
                Type captured = type;
                menu.AddItem(new GUIContent(SkillMakerBlockEditor.GetBlockName(type)), false,
                    () => EditBlock(() => _blockEditor.Insert(_blockIndex >= 0 ? _blockIndex + 1 : 0, captured)));
            }
            menu.ShowAsContext();
        }

        private void EditBlock(Func<int> operation)
        {
            if (!CanEdit(true)) { SetStatus(_editReason, HelpBoxMessageType.Warning); return; }
            Run(() =>
            {
                int dirtyCount = EditorUtility.GetDirtyCount(_skill);
                int nextIndex = operation();
                if (dirtyCount == EditorUtility.GetDirtyCount(_skill))
                    throw new InvalidOperationException("블록을 변경하지 못했습니다. 선택한 블록과 편집 가능 상태를 확인하세요.");
                _blockIndex = nextIndex;
                ScheduleDetails();
            }, "블록을 변경했습니다. Ctrl+Z로 취소할 수 있습니다.");
        }

        private void CreateSkill()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            var menu = new GenericMenu();
            menu.AddItem(new GUIContent("빈 스킬"), false, () => CreateSkillAtSelectedPath(false, false));
            menu.AddItem(new GUIContent("적 공격/일반 공격 (Z · X)"), false, () => CreateSkillAtSelectedPath(true, false));
            menu.AddItem(new GUIContent("적 공격/연계 반격 공격 (X · C)"), false, () => CreateSkillAtSelectedPath(true, true));
            menu.ShowAsContext();
        }

        private void CreateSkillAtSelectedPath(bool enemyTemplate, bool counterable)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            string name = !enemyTemplate ? "Skill_New" : counterable ? "Skill_Enemy_LinkCounter" : "Skill_Enemy_Strike";
            string path = EditorUtility.SaveFilePanelInProject("새 스킬", name, "asset", "콘텐츠 폴더에 독립된 새 스킬을 만듭니다. 기존 스킬은 변경하지 않습니다.", "Assets/_Game/Content/Skills");
            if (string.IsNullOrEmpty(path)) return;
            Run(() => RevealCreated(SkillMakerAssetUtility.CreateAtPath(path, skill =>
            {
                if (!enemyTemplate) return;
                skill.UsageProfile = SkillUsageProfile.EnemyOnly;
                skill.APCost = 0;
                skill.TargetType = TargetAreaType.EnemyOnly;
                skill.IsAoE = false;
                skill.SkillName = counterable ? "연계 반격 기회" : "전조 공격";
                skill.Description = counterable ? "가드 불가. 회피하거나 연계 반격으로 대응합니다." : "가드 또는 회피로 대응합니다.";
                skill.ActionTimeline = counterable
                    ? EnemyAttackTemplateFactory.CreateCounterableStrike()
                    : EnemyAttackTemplateFactory.CreateTelegraphedStrike();
            })), enemyTemplate
                ? "새 적 공격 템플릿을 만들었습니다. 방어 대응 블록의 전조·공격 애니메이션 이름을 적 Animator에 맞추고 EnemyData의 스킬 목록에 연결하세요."
                : "새 스킬을 만들었습니다. 기본 정보부터 입력하세요.");
        }

        private void DuplicateSkill()
        {
            if (!CanEdit()) return;
            string source = AssetDatabase.GetAssetPath(_skill);
            string path = EditorUtility.SaveFilePanelInProject("스킬 복제", _skill.name + "_Copy", "asset", "새 ID를 가진 독립 스킬로 복제합니다.", Path.GetDirectoryName(source)?.Replace('\\', '/'));
            if (string.IsNullOrEmpty(path)) return;
            Run(() => RevealCreated(SkillMakerAssetUtility.DuplicateAtPath(_skill, path)), "스킬을 복제했습니다. 새 ID와 캐릭터 연결을 확인하세요.");
        }

        private void RevealCreated(SkillData skill)
        {
            _skill = skill;
            _blockIndex = -1;
            _query = string.Empty;
            _profile = 0;
            _search?.SetValueWithoutNotify(_query);
            _profileField?.SetValueWithoutNotify(Profiles[0]);
            RefreshLibrary();
            if (skill != null) EditorGUIUtility.PingObject(skill);
        }

        private void SaveSkill()
        {
            if (!CanEdit()) return;
            Run(() => { SkillMakerAssetUtility.Save(_skill); RefreshDetails(); }, "선택한 스킬만 저장했습니다.");
        }

        private void Run(Action action, string success)
        {
            try { action(); SetStatus(success, HelpBoxMessageType.Info); }
            catch (Exception exception) { SetStatus(exception.Message, HelpBoxMessageType.Error); }
        }

        private bool CanEdit(bool force = false)
        {
            if (_skill == null || EditorApplication.isPlayingOrWillChangePlaymode)
            {
                _editReason = _skill == null ? "저장된 스킬을 선택하세요." : "Play Mode에서는 스킬 편집이 잠깁니다.";
                return false;
            }
            if (force || EditorApplication.timeSinceStartup >= _nextEditCheck)
            {
                _editable = SkillMakerAssetUtility.CanEdit(_skill, out _editReason);
                _nextEditCheck = EditorApplication.timeSinceStartup + 1;
            }
            return _editable;
        }

        private static bool IsInputEvent() => Event.current != null
            && Event.current.type != EventType.Layout && Event.current.type != EventType.Repaint;

        private void UpdateButtons()
        {
            if (_save == null) return;
            bool playing = EditorApplication.isPlayingOrWillChangePlaymode;
            bool editable = CanEdit();
            bool dirty = _skill != null && EditorUtility.IsDirty(_skill);
            _create.SetEnabled(!playing);
            _copy.SetEnabled(editable && !dirty);
            _copy.tooltip = dirty ? "원본을 저장한 뒤 복제하세요." : "기존 스킬을 새 ID로 복제합니다.";
            _save.SetEnabled(editable && dirty);
            _save.tooltip = editable ? "선택한 스킬 자산만 저장합니다." : _editReason;
            _save.text = dirty ? "● 변경 저장" : "저장됨";
            if (_addBlock == null) return;
            _addBlock.SetEnabled(editable);
            bool valid = _skill?.ActionTimeline != null && _blockIndex >= 0 && _blockIndex < _skill.ActionTimeline.Count;
            _copyBlock.SetEnabled(editable && valid);
            _removeBlock.SetEnabled(editable && valid);
            _up.SetEnabled(editable && valid && _blockIndex > 0);
            _down.SetEnabled(editable && valid && _blockIndex + 1 < _skill.ActionTimeline.Count);
        }

        private void OnUndo()
        {
            _blockEditor?.Invalidate();
            ScheduleRefresh();
        }

        private void OnPlayModeChanged(PlayModeStateChange state)
        {
            _nextEditCheck = 0;
            _blockEditor?.Invalidate();
            UpdateButtons();
            SetStatus(EditorApplication.isPlayingOrWillChangePlaymode ? "Play Mode에서는 스킬 편집이 잠깁니다." : "편집 모드입니다. 선택한 스킬만 저장합니다.", HelpBoxMessageType.Info);
            ScheduleDetails();
        }

        private void OnInspectorUpdate()
        {
            ApplyTheme();
            UpdateButtons();
            if (_skill != null && EditorUtility.GetDirtyCount(_skill) != _dirtyCount)
            {
                if (!_detailsScheduled) _blockEditor?.Invalidate();
                ScheduleDetails();
            }
            else if (_skill == null && _serialized != null) SelectSkill(null);
            if (_skill != null && _referenceSnapshot?.Catalog != null
                && EditorUtility.GetDirtyCount(_referenceSnapshot.Catalog) != _catalogDirtyCount)
                ScheduleDetails();
        }

        private void ScheduleRefresh()
        {
            _nextEditCheck = 0;
            _blockEditor?.Invalidate();
            if (_refreshScheduled) return;
            _refreshScheduled = true;
            EditorApplication.delayCall += RefreshLibrary;
        }

        private void ScheduleDetails()
        {
            if (_detailsScheduled) return;
            _detailsScheduled = true;
            EditorApplication.delayCall += RefreshDetails;
        }

        private void SetStatus(string message, HelpBoxMessageType type)
        {
            if (_status == null) return;
            _status.text = message;
            _status.messageType = type;
            _status.tooltip = message;
        }

        private VisualElement MakeSkillRow()
        {
            VisualElement row = Element("sm-library-row");
            var icon = new Image { name = "icon", scaleMode = ScaleMode.ScaleToFit };
            icon.AddToClassList("sm-list-icon");
            row.Add(icon);
            VisualElement labels = Element("sm-row-labels");
            Label title = Text("", "cm-asset-title"); title.name = "title";
            Label subtitle = Text("", "cm-asset-subtitle"); subtitle.name = "subtitle";
            labels.Add(title); labels.Add(subtitle); row.Add(labels);
            return row;
        }

        private void BindSkillRow(VisualElement row, int index)
        {
            SkillData skill = index >= 0 && index < _filtered.Count ? _filtered[index] : null;
            row.Q<Image>("icon").sprite = skill != null ? skill.Icon : null;
            row.Q<Label>("title").text = skill != null ? (EditorUtility.IsDirty(skill) ? "● " : "") + (string.IsNullOrWhiteSpace(skill.SkillName) ? skill.name : skill.SkillName) : "삭제된 스킬";
            row.Q<Label>("subtitle").text = skill != null ? ProfileName(skill.UsageProfile) + " · " + skill.SkillID : string.Empty;
            row.tooltip = skill != null ? AssetDatabase.GetAssetPath(skill) : string.Empty;
        }

        private VisualElement MakeBlockRow()
        {
            VisualElement row = Element("sm-block-row");
            Label number = Text("", "sm-block-number"); number.name = "number";
            row.Add(number);
            VisualElement labels = Element("sm-row-labels");
            Label title = Text("", "cm-asset-title"); title.name = "title";
            Label detail = Text("", "cm-asset-subtitle"); detail.name = "detail";
            labels.Add(title); labels.Add(detail); row.Add(labels);
            return row;
        }

        private void BindBlockRow(VisualElement row, int index)
        {
            if (index < 0 || index >= _blocks.Count) return;
            BlockRow block = _blocks[index];
            row.Q<Label>("number").text = (index + 1).ToString("00");
            row.Q<Label>("title").text = block.Title;
            row.Q<Label>("detail").text = block.Category + "   ·   " + block.Timing;
            row.EnableInClassList("sm-disabled", !block.Enabled);
        }

        private void ApplyTheme() => rootVisualElement.EnableInClassList("cm-light", !EditorGUIUtility.isProSkin);
        private static string ProfileName(SkillUsageProfile profile) => profile == SkillUsageProfile.PlayerOnly ? "아군 전용" : profile == SkillUsageProfile.EnemyOnly ? "적 전용" : "공용";
        private static VisualElement Element(string style) { var element = new VisualElement(); element.AddToClassList(style); return element; }
        private static Label Text(string text, string style) { var label = new Label(text); label.AddToClassList(style); return label; }
        private static Button Button(string text, Action clicked, VisualElement parent, string extra = null)
        {
            var button = new Button(clicked) { text = text };
            button.AddToClassList("cm-button");
            if (extra != null) button.AddToClassList(extra);
            parent.Add(button);
            return button;
        }
        private static void AddStyle(VisualElement root, string path)
        {
            StyleSheet sheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(path);
            if (sheet != null && !root.styleSheets.Contains(sheet)) root.styleSheets.Add(sheet);
        }
    }
}
