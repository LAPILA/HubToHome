using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

public sealed class SequenceMakerWindow : EditorWindow
{
    public const string MenuPath = "HubToHome/시나리오/시퀀스 메이커";
    public const string UxmlPath =
        "Assets/_Game/Scripts/Scenario/Editor/SequenceMaker/SequenceMakerWindow.uxml";
    public const string UssPath =
        "Assets/_Game/Scripts/Scenario/Editor/SequenceMaker/SequenceMakerWindow.uss";

    private const string WindowTitle = "시퀀스 메이커";

    [SerializeField] private BattleScenarioData _serializedBattleScenario;
    [SerializeField] private ActionSequenceAsset _serializedStandaloneSequence;
    [SerializeField] private ActionCatalogAsset _catalog;

    private readonly Dictionary<int, SequenceEditCommandStack> _editStacks =
        new Dictionary<int, SequenceEditCommandStack>();

    private SequenceMakerWorkspaceState _workspace;
    private EditorSequenceMakerPreferences _preferences;
    private VisualElement _root;
    private ObjectField _targetField;
    private Button _undoButton;
    private Button _redoButton;
    private Button _validateButton;
    private Button _saveButton;
    private DropdownField _playModeField;
    private Button _playButton;
    private Button _playSelectedButton;
    private Button _pauseButton;
    private Button _stepButton;
    private Button _stopButton;
    private Label _breadcrumbLabel;
    private TextField _searchField;
    private Button _densityButton;
    private Button _drawerToggleButton;
    private VisualElement _workspaceHost;
    private VisualElement _navigatorPanel;
    private VisualElement _navigatorContent;
    private Label _flowTitle;
    private Label _flowSubtitle;
    private VisualElement _flowContent;
    private VisualElement _inspectorContent;
    private VisualElement _drawer;
    private VisualElement _drawerContent;
    private Button _problemsTab;
    private Button _traceTab;
    private Button _yamlTab;
    private Button _drawerCloseButton;
    private VisualElement _saveStateDot;
    private Label _statusLabel;
    private Label _sourceStatusLabel;
    private ScenarioValidationResult _lastValidation = new ScenarioValidationResult();
    private string _yamlPreview = string.Empty;
    private string _statusText = "준비됨";
    private bool _statusHasError;

    [MenuItem(MenuPath)]
    public static void Open()
    {
        SequenceMakerWindow window = GetWindow<SequenceMakerWindow>();
        window.titleContent = new GUIContent(WindowTitle);
        window.minSize = new Vector2(960f, 620f);
        window.Show();
        window.Focus();
    }

    public void CreateGUI()
    {
        titleContent = new GUIContent(WindowTitle);
        minSize = new Vector2(960f, 620f);
        _preferences = new EditorSequenceMakerPreferences();
        _workspace = new SequenceMakerWorkspaceState();
        _workspace.LoadPreferences(_preferences);
        _workspace.Changed += OnWorkspaceChanged;

        BuildVisualTree();
        BindControls();
        BuildWorkspacePanels();
        RestoreTarget();
        RefreshDerivedState(true);
        RenderAll();
    }

    private void OnDisable()
    {
        if (_workspace == null)
        {
            return;
        }

        CaptureLayoutDimensions();
        _workspace.SavePreferences(_preferences ?? new EditorSequenceMakerPreferences());
        _workspace.Changed -= OnWorkspaceChanged;
    }

    private void OnSelectionChange()
    {
        if (_workspace == null || _workspace.IsDirty)
        {
            return;
        }

        if (Selection.activeObject is BattleScenarioData battle
            && battle != _workspace.BattleScenario)
        {
            SetTarget(battle);
        }
        else if (Selection.activeObject is ActionSequenceAsset sequence
            && sequence != _workspace.StandaloneSequence
            && !IsSequenceInCurrentBattle(sequence))
        {
            SetTarget(sequence);
        }
    }

    private void BuildVisualTree()
    {
        rootVisualElement.Clear();
        VisualTreeAsset tree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(UxmlPath);
        StyleSheet style = AssetDatabase.LoadAssetAtPath<StyleSheet>(UssPath);
        if (tree == null)
        {
            var missing = new Label("Sequence Maker UI 리소스를 불러오지 못했습니다.");
            missing.style.paddingLeft = 12f;
            missing.style.paddingTop = 12f;
            rootVisualElement.Add(missing);
            return;
        }

        tree.CloneTree(rootVisualElement);
        if (style != null)
        {
            rootVisualElement.styleSheets.Add(style);
        }

        _root = rootVisualElement.Q<VisualElement>("sequence-maker-root");
        SequenceMakerTheme.Apply(_root, _workspace.Density);
        _root.focusable = true;
        _root.RegisterCallback<KeyDownEvent>(OnKeyDown, TrickleDown.TrickleDown);
    }

    private void BindControls()
    {
        if (_root == null)
        {
            return;
        }

        VisualElement targetHost = _root.Q<VisualElement>("target-field-host");
        _targetField = new ObjectField("편집 대상")
        {
            objectType = typeof(ScriptableObject),
            allowSceneObjects = false,
            tooltip = "Battle Scenario 또는 독립 Action Sequence"
        };
        _targetField.RegisterValueChangedCallback(OnTargetFieldChanged);
        targetHost.Add(_targetField);

        _undoButton = Require<Button>("undo-button");
        _redoButton = Require<Button>("redo-button");
        _validateButton = Require<Button>("validate-button");
        _saveButton = Require<Button>("save-button");
        _playModeField = Require<DropdownField>("play-mode-field");
        _playButton = Require<Button>("play-button");
        _playSelectedButton = Require<Button>("play-selected-button");
        _pauseButton = Require<Button>("pause-button");
        _stepButton = Require<Button>("step-button");
        _stopButton = Require<Button>("stop-button");
        _breadcrumbLabel = Require<Label>("breadcrumb-label");
        _searchField = Require<TextField>("search-field");
        _densityButton = Require<Button>("density-button");
        _drawerToggleButton = Require<Button>("drawer-toggle-button");
        _workspaceHost = Require<VisualElement>("workspace-host");
        _drawer = Require<VisualElement>("drawer");
        _drawerContent = Require<VisualElement>("drawer-content");
        _problemsTab = Require<Button>("problems-tab");
        _traceTab = Require<Button>("trace-tab");
        _yamlTab = Require<Button>("yaml-tab");
        _drawerCloseButton = Require<Button>("drawer-close-button");
        _saveStateDot = Require<VisualElement>("save-state-dot");
        _statusLabel = Require<Label>("status-label");
        _sourceStatusLabel = Require<Label>("source-status-label");

        SequenceMakerTheme.SetButtonIcon(_undoButton, "Undo", "U");
        SequenceMakerTheme.SetButtonIcon(_redoButton, "Redo", "R");
        SequenceMakerTheme.PrependButtonIcon(_validateButton, "TestPassed");
        SequenceMakerTheme.PrependButtonIcon(_saveButton, "SaveAs");
        SequenceMakerTheme.SetButtonIcon(_playButton, "d_PlayButton", ">");
        SequenceMakerTheme.SetButtonIcon(_playSelectedButton, "d_PlayButton On", ">|");
        SequenceMakerTheme.SetButtonIcon(_pauseButton, "PauseButton", "||");
        SequenceMakerTheme.SetButtonIcon(_stepButton, "StepButton", ">.");
        SequenceMakerTheme.SetButtonIcon(_stopButton, "d_PreMatQuad", "[]");
        SequenceMakerTheme.SetButtonIcon(_densityButton, "Settings", "D");
        SequenceMakerTheme.SetButtonIcon(_drawerToggleButton, "console.infoicon", "_");
        SequenceMakerTheme.SetButtonIcon(_drawerCloseButton, "winbtn_win_close", "X");

        _playModeField.choices = new List<string> { "안전 미리보기", "Play Mode 테스트" };
        _playModeField.SetValueWithoutNotify("안전 미리보기");
        _searchField.label = string.Empty;
        _searchField.value = string.Empty;

        _undoButton.clicked += Undo;
        _redoButton.clicked += Redo;
        _validateButton.clicked += ValidateCurrent;
        _saveButton.clicked += () => SaveCurrent();
        _densityButton.clicked += ToggleDensity;
        _drawerToggleButton.clicked += () =>
            _workspace.SetDrawerOpen(!_workspace.IsDrawerOpen);
        _drawerCloseButton.clicked += () => _workspace.SetDrawerOpen(false);
        _problemsTab.clicked += () =>
            _workspace.SetDrawer(SequenceMakerDrawerTab.Problems, true);
        _traceTab.clicked += () =>
            _workspace.SetDrawer(SequenceMakerDrawerTab.Trace, true);
        _yamlTab.clicked += () =>
            _workspace.SetDrawer(SequenceMakerDrawerTab.Yaml, true);
        _searchField.RegisterValueChangedCallback(_ => RenderFlow());

        SetPlaybackControlsEnabled(false);
    }

    private void BuildWorkspacePanels()
    {
        if (_workspaceHost == null)
        {
            return;
        }

        _workspaceHost.Clear();
        var outer = new TwoPaneSplitView(
            0,
            _workspace.NavigatorWidth,
            TwoPaneSplitViewOrientation.Horizontal);
        outer.AddToClassList("sm-workspace-split");

        _navigatorPanel = CreatePanel(
            "탐색",
            "",
            "sm-panel--navigator",
            out _navigatorContent,
            false);
        outer.Add(_navigatorPanel);

        var right = new TwoPaneSplitView(
            1,
            _workspace.InspectorWidth,
            TwoPaneSplitViewOrientation.Horizontal);
        right.AddToClassList("sm-workspace-split");

        VisualElement flowPanel = CreatePanel(
            "시퀀스",
            "",
            "sm-panel--flow",
            out _flowContent,
            true);
        VisualElement flowHeader = flowPanel.Q<VisualElement>(className: "sm-panel-header");
        _flowTitle = flowHeader.Q<Label>(className: "sm-panel-title");
        _flowSubtitle = flowHeader.Q<Label>(className: "sm-panel-subtitle");
        right.Add(flowPanel);

        VisualElement inspectorPanel = CreatePanel(
            "속성",
            "",
            "sm-panel--inspector",
            out _inspectorContent,
            true);
        right.Add(inspectorPanel);
        outer.Add(right);
        _workspaceHost.Add(outer);
    }

    private VisualElement CreatePanel(
        string title,
        string subtitle,
        string modifierClass,
        out VisualElement content,
        bool scroll)
    {
        var panel = new VisualElement();
        panel.AddToClassList("sm-panel");
        panel.AddToClassList(modifierClass);

        var header = new VisualElement();
        header.AddToClassList("sm-panel-header");
        var titleLabel = new Label(title);
        titleLabel.AddToClassList("sm-panel-title");
        header.Add(titleLabel);
        var subtitleLabel = new Label(subtitle);
        subtitleLabel.AddToClassList("sm-panel-subtitle");
        header.Add(subtitleLabel);
        panel.Add(header);

        if (scroll)
        {
            var scrollView = new ScrollView(ScrollViewMode.Vertical);
            scrollView.AddToClassList("sm-panel-scroll");
            content = scrollView.contentContainer;
            panel.Add(scrollView);
        }
        else
        {
            var scrollView = new ScrollView(ScrollViewMode.Vertical);
            scrollView.AddToClassList("sm-panel-scroll");
            content = scrollView.contentContainer;
            panel.Add(scrollView);
        }

        return panel;
    }

    private void RestoreTarget()
    {
        if (_serializedBattleScenario != null)
        {
            _workspace.SetBattleScenario(_serializedBattleScenario);
            return;
        }

        if (_serializedStandaloneSequence != null)
        {
            _workspace.SetStandaloneSequence(_serializedStandaloneSequence);
            return;
        }

        if (Selection.activeObject is BattleScenarioData battle)
        {
            _workspace.SetBattleScenario(battle);
        }
        else if (Selection.activeObject is ActionSequenceAsset sequence)
        {
            _workspace.SetStandaloneSequence(sequence);
        }

        if (_catalog == null)
        {
            _catalog = AssetDatabase.LoadAssetAtPath<ActionCatalogAsset>(
                ProductionActionLibraryBuildCommand.GeneratedAssetPath);
        }
    }

    private void OnTargetFieldChanged(ChangeEvent<UnityEngine.Object> evt)
    {
        UnityEngine.Object next = evt.newValue;
        if (next != null
            && !(next is BattleScenarioData)
            && !(next is ActionSequenceAsset))
        {
            SetStatus("Battle Scenario 또는 Action Sequence만 열 수 있습니다.", true);
            _targetField.SetValueWithoutNotify(_workspace.ActiveTarget);
            return;
        }

        if (!CanLeaveCurrentTarget())
        {
            _targetField.SetValueWithoutNotify(_workspace.ActiveTarget);
            return;
        }

        SetTarget(next);
    }

    private void SetTarget(UnityEngine.Object target)
    {
        if (target is BattleScenarioData battle)
        {
            _workspace.SetBattleScenario(battle);
            _serializedBattleScenario = battle;
            _serializedStandaloneSequence = null;
        }
        else if (target is ActionSequenceAsset sequence)
        {
            _workspace.SetStandaloneSequence(sequence);
            _serializedBattleScenario = null;
            _serializedStandaloneSequence = sequence;
        }
        else
        {
            _workspace.SetBattleScenario(null);
            _serializedBattleScenario = null;
            _serializedStandaloneSequence = null;
        }

        RefreshDerivedState(true);
        RenderAll();
    }

    private bool CanLeaveCurrentTarget()
    {
        if (!_workspace.IsDirty)
        {
            return true;
        }

        int choice = EditorUtility.DisplayDialogComplex(
            "저장되지 않은 시퀀스",
            "현재 편집 내용을 YAML에 저장하지 않고 다른 대상을 열까요?",
            "저장",
            "취소",
            "저장하지 않음");
        if (choice == 0)
        {
            return SaveCurrent();
        }

        return choice == 2;
    }

    private void RefreshDerivedState(bool refreshValidation)
    {
        GetCurrentEditStack();
        RefreshYamlPreview();
        if (refreshValidation)
        {
            RefreshCatalogValidation();
        }

        _workspace.SetDirty(AnyEditStackDirty());
    }

    private void RefreshCatalogValidation()
    {
        _lastValidation = new ScenarioValidationResult();
        if (_catalog == null)
        {
            _lastValidation.AddWarning(
                "sequence_maker.catalog.missing",
                "공식 Action Library를 찾지 못했습니다.",
                TargetId());
            return;
        }

        if (_workspace.TargetKind == SequenceMakerTargetKind.BattleScenario)
        {
            _lastValidation.Merge(
                ScenarioCatalogValidator.ValidateBattleScenario(
                    _workspace.BattleScenario,
                    _catalog));
        }
        else if (_workspace.SelectedSequence != null)
        {
            _lastValidation.Merge(
                ScenarioCatalogValidator.ValidateSequence(
                    _workspace.SelectedSequence,
                    _catalog));
        }
    }

    private void RefreshYamlPreview()
    {
        _yamlPreview = string.Empty;
        if (_workspace.TargetKind == SequenceMakerTargetKind.BattleScenario
            && _workspace.BattleScenario != null)
        {
            ScenarioSourceYamlExportResult result =
                new ScenarioSourceYamlExportCommand().ExportToText(_workspace.BattleScenario);
            _yamlPreview = result.Text ?? string.Empty;
            return;
        }

        if (_workspace.StandaloneSequence != null)
        {
            ActionSequenceSourceExportResult result = ActionSequenceSourceSync.Export(
                _workspace.StandaloneSequence,
                PrimaryModeFor(_workspace.StandaloneSequence));
            _yamlPreview = result.Text ?? string.Empty;
        }
    }

    private void RenderAll()
    {
        if (_root == null)
        {
            return;
        }

        SequenceMakerTheme.Apply(_root, _workspace.Density);
        _targetField?.SetValueWithoutNotify(_workspace.ActiveTarget);
        RenderCommandState();
        RenderBreadcrumb();
        RenderNavigator();
        RenderFlow();
        RenderInspector();
        RenderDrawer();
        RenderStatus();
    }

    private void RenderCommandState()
    {
        SequenceEditCommandStack stack = GetCurrentEditStack();
        _undoButton?.SetEnabled(stack != null && stack.CanUndo);
        _redoButton?.SetEnabled(stack != null && stack.CanRedo);
        if (_undoButton != null)
        {
            _undoButton.tooltip = stack != null && stack.CanUndo
                ? "실행 취소: " + stack.UndoLabel
                : "실행 취소";
        }

        if (_redoButton != null)
        {
            _redoButton.tooltip = stack != null && stack.CanRedo
                ? "다시 실행: " + stack.RedoLabel
                : "다시 실행";
        }

        bool hasTarget = _workspace.HasTarget;
        _validateButton?.SetEnabled(hasTarget);
        _saveButton?.SetEnabled(hasTarget && !string.IsNullOrWhiteSpace(SourcePath()));
        _playModeField?.SetEnabled(hasTarget);
    }

    private void RenderBreadcrumb()
    {
        if (_breadcrumbLabel == null)
        {
            return;
        }

        if (!_workspace.HasTarget)
        {
            _breadcrumbLabel.text = "편집 대상을 선택";
            return;
        }

        string target = _workspace.TargetKind == SequenceMakerTargetKind.BattleScenario
            ? DisplayName(_workspace.BattleScenario != null
                ? _workspace.BattleScenario.TitleKo
                : string.Empty, TargetId())
            : "독립 시퀀스";
        string sequence = _workspace.SelectedSequence != null
            ? DisplayName(
                _workspace.SelectedSequence.DisplayNameKo,
                _workspace.SelectedSequence.SequenceId)
            : "시퀀스 없음";
        string block = BlockDisplayName(_workspace.SelectedBlockId);
        _breadcrumbLabel.text = string.IsNullOrEmpty(block)
            ? target + "  /  " + sequence
            : target + "  /  " + sequence + "  /  " + block;
    }

    private void RenderNavigator()
    {
        if (_navigatorContent == null)
        {
            return;
        }

        _navigatorContent.Clear();
        if (!_workspace.HasTarget)
        {
            _navigatorContent.Add(CreateEmptyState(
                "열린 대상 없음",
                "Project 창에서 Battle Scenario 또는 Action Sequence를 선택"));
            return;
        }

        if (_workspace.TargetKind == SequenceMakerTargetKind.BattleScenario)
        {
            AddSectionLabel(_navigatorContent, "전투 흐름");
            Button scenario = CreateNavigatorRow(
                DisplayName(_workspace.BattleScenario.TitleKo, _workspace.BattleScenario.ScenarioId),
                true);
            scenario.tooltip = _workspace.BattleScenario.ScenarioId;
            _navigatorContent.Add(scenario);

            AddSectionLabel(_navigatorContent, "시퀀스");
            List<ActionSequenceAsset> sequences = _workspace.BattleScenario.Sequences;
            if (sequences != null)
            {
                for (int i = 0; i < sequences.Count; i++)
                {
                    ActionSequenceAsset sequence = sequences[i];
                    if (sequence == null)
                    {
                        continue;
                    }

                    Button row = CreateNavigatorRow(
                        DisplayName(sequence.DisplayNameKo, sequence.SequenceId),
                        sequence == _workspace.SelectedSequence);
                    row.tooltip = sequence.SequenceId;
                    ActionSequenceAsset captured = sequence;
                    row.clicked += () =>
                    {
                        if (_workspace.TrySelectSequence(captured))
                        {
                            RefreshDerivedState(true);
                            RenderAll();
                        }
                    };
                    _navigatorContent.Add(row);
                }
            }
        }
        else
        {
            AddSectionLabel(_navigatorContent, "독립 시퀀스");
            Button row = CreateNavigatorRow(
                DisplayName(
                    _workspace.StandaloneSequence.DisplayNameKo,
                    _workspace.StandaloneSequence.SequenceId),
                true);
            row.tooltip = _workspace.StandaloneSequence.SequenceId;
            _navigatorContent.Add(row);
        }

        AddSectionLabel(_navigatorContent, "원본");
        Button sourceRow = CreateNavigatorRow(
            string.IsNullOrWhiteSpace(SourcePath()) ? "YAML 경로 없음" : Path.GetFileName(SourcePath()),
            false);
        sourceRow.clicked += () => _workspace.SetDrawer(SequenceMakerDrawerTab.Yaml, true);
        sourceRow.tooltip = SourcePath();
        _navigatorContent.Add(sourceRow);
    }

    private void RenderFlow()
    {
        if (_flowContent == null)
        {
            return;
        }

        _flowContent.Clear();
        ActionSequenceAsset sequence = _workspace.SelectedSequence;
        if (_flowTitle != null)
        {
            _flowTitle.text = sequence != null
                ? DisplayName(sequence.DisplayNameKo, sequence.SequenceId)
                : "시퀀스";
        }

        if (_flowSubtitle != null)
        {
            _flowSubtitle.text = sequence != null
                ? CountBlocks(sequence.Actions) + "개 블록"
                : string.Empty;
        }

        if (sequence == null)
        {
            _flowContent.Add(CreateEmptyState("시퀀스 없음", "현재 흐름에 연결된 시퀀스가 없습니다."));
            return;
        }

        if (sequence.Actions == null || sequence.Actions.Count == 0)
        {
            _flowContent.Add(CreateEmptyState("빈 시퀀스", "액션 블록이 없습니다."));
            return;
        }

        var list = new VisualElement();
        list.AddToClassList("sm-flow-list");
        int displayIndex = 0;
        AddActionRows(list, sequence.Actions, 0, ref displayIndex);
        _flowContent.Add(list);
    }

    private void AddActionRows(
        VisualElement parent,
        IList<ScenarioActionData> actions,
        int depth,
        ref int displayIndex)
    {
        if (actions == null)
        {
            return;
        }

        string search = Normalize(_searchField != null ? _searchField.value : string.Empty);
        for (int i = 0; i < actions.Count; i++)
        {
            ScenarioActionData action = actions[i];
            if (action == null)
            {
                continue;
            }

            displayIndex++;
            bool visible = MatchesSearch(action, search);
            if (visible)
            {
                parent.Add(CreateActionRow(action, depth, displayIndex));
            }

            AddActionRows(parent, action.Children, depth + 1, ref displayIndex);
        }
    }

    private VisualElement CreateActionRow(ScenarioActionData action, int depth, int index)
    {
        var row = new Button();
        row.AddToClassList("sm-flow-row");
        row.EnableInClassList(
            "is-selected",
            string.Equals(action.BlockId, _workspace.SelectedBlockId, StringComparison.Ordinal));
        row.EnableInClassList("is-disabled", action.Disabled);
        row.style.marginLeft = 12f + depth * 18f;
        row.tooltip = string.IsNullOrWhiteSpace(action.Note)
            ? action.ActionId
            : action.Note;

        var indexLabel = new Label(index.ToString("00"));
        indexLabel.AddToClassList("sm-flow-index");
        row.Add(indexLabel);

        var accent = new VisualElement();
        accent.AddToClassList("sm-flow-accent");
        ActionCatalogEntry entry = FindCatalogEntry(action.ActionId);
        if (entry != null
            && !string.IsNullOrWhiteSpace(entry.AccentHex)
            && ColorUtility.TryParseHtmlString(entry.AccentHex, out Color color))
        {
            accent.style.backgroundColor = color;
        }
        row.Add(accent);

        var copy = new VisualElement();
        copy.AddToClassList("sm-flow-copy");
        var title = new Label(ActionDisplayName(action, entry));
        title.AddToClassList("sm-flow-title");
        copy.Add(title);
        var summary = new Label(ActionSummary(action, entry));
        summary.AddToClassList("sm-flow-summary");
        copy.Add(summary);
        row.Add(copy);

        var badge = new Label(entry != null && !string.IsNullOrWhiteSpace(entry.Category)
            ? entry.Category
            : "action");
        badge.AddToClassList("sm-flow-badge");
        row.Add(badge);

        string blockId = action.BlockId;
        row.clicked += () =>
        {
            _workspace.SelectBlock(blockId);
            SequenceEditCommandStack stack = GetCurrentEditStack();
            stack?.SetSelection(blockId);
            RenderAll();
        };
        return row;
    }

    private void RenderInspector()
    {
        if (_inspectorContent == null)
        {
            return;
        }

        _inspectorContent.Clear();
        if (!_workspace.HasTarget)
        {
            _inspectorContent.Add(CreateEmptyState("속성 없음", "편집 대상을 선택"));
            return;
        }

        var body = new VisualElement();
        body.AddToClassList("sm-inspector-content");
        if (TryGetSelectedAction(out ScenarioActionData action))
        {
            RenderActionInspector(body, action);
        }
        else if (_workspace.SelectedSequence != null)
        {
            RenderSequenceInspector(body, _workspace.SelectedSequence);
        }
        else
        {
            RenderBattleInspector(body, _workspace.BattleScenario);
        }

        _inspectorContent.Add(body);
    }

    private void RenderActionInspector(VisualElement body, ScenarioActionData action)
    {
        ActionCatalogEntry entry = FindCatalogEntry(action.ActionId);
        AddInspectorHeading(
            body,
            ActionDisplayName(action, entry),
            action.ActionId);
        AddProperty(body, "블록 ID", action.BlockId);
        if (entry != null)
        {
            AddProperty(body, "설명", entry.DescriptionKo);
            AddProperty(body, "사용 시점", entry.UsageKo);
            AddProperty(body, "미리보기", entry.PreviewSupport.ToString());
        }

        AddProperty(body, "이 블록 이름", action.DesignerLabel);
        AddProperty(body, "메모", action.Note);
        AddProperty(body, "파라미터", action.ParametersJson);
        AddProperty(body, "상태", action.Disabled ? "비활성" : "활성");
    }

    private void RenderSequenceInspector(VisualElement body, ActionSequenceAsset sequence)
    {
        AddInspectorHeading(
            body,
            DisplayName(sequence.DisplayNameKo, sequence.SequenceId),
            sequence.SequenceId);
        ActionSequenceContractData contract = sequence.Contract ?? new ActionSequenceContractData();
        AddProperty(body, "설명", contract.DescriptionKo);
        AddProperty(body, "사용 시점", contract.UsageKo);
        AddProperty(body, "상태", contract.Lifecycle.ToString());
        AddProperty(body, "태그", contract.Tags != null ? string.Join(", ", contract.Tags) : string.Empty);
        AddProperty(
            body,
            "사용 가능 모드",
            contract.AllowedPrimaryModes != null
                ? string.Join(", ", contract.AllowedPrimaryModes)
                : string.Empty);
        AddProperty(body, "입력", contract.Inputs != null ? contract.Inputs.Count + "개" : "0개");
    }

    private void RenderBattleInspector(VisualElement body, BattleScenarioData battle)
    {
        if (battle == null)
        {
            return;
        }

        AddInspectorHeading(body, DisplayName(battle.TitleKo, battle.ScenarioId), battle.ScenarioId);
        AddProperty(body, "Primary Mode", battle.PrimaryMode);
        AddProperty(body, "시작 모듈", battle.OpeningModule);
        AddProperty(body, "Encounter Memory", battle.MemoryKey);
        AddProperty(body, "규칙", (battle.Rules?.Count ?? 0) + (battle.TriggerRules?.Count ?? 0) + "개");
        AddProperty(body, "시퀀스", battle.Sequences != null ? battle.Sequences.Count + "개" : "0개");
    }

    private void RenderDrawer()
    {
        if (_drawer == null || _drawerContent == null)
        {
            return;
        }

        _drawer.style.display = _workspace.IsDrawerOpen
            ? DisplayStyle.Flex
            : DisplayStyle.None;
        if (_workspace.IsDrawerOpen)
        {
            _drawer.style.height = _workspace.DrawerHeight;
        }

        _problemsTab.EnableInClassList(
            "is-selected",
            _workspace.DrawerTab == SequenceMakerDrawerTab.Problems);
        _traceTab.EnableInClassList(
            "is-selected",
            _workspace.DrawerTab == SequenceMakerDrawerTab.Trace);
        _yamlTab.EnableInClassList(
            "is-selected",
            _workspace.DrawerTab == SequenceMakerDrawerTab.Yaml);
        _problemsTab.text = "문제 " + ErrorCount(_lastValidation);

        _drawerContent.Clear();
        switch (_workspace.DrawerTab)
        {
            case SequenceMakerDrawerTab.Trace:
                _drawerContent.Add(CreateEmptyState("실행 기록 없음", ""));
                break;
            case SequenceMakerDrawerTab.Yaml:
                var yaml = new TextField
                {
                    multiline = true,
                    isReadOnly = true,
                    value = _yamlPreview
                };
                yaml.AddToClassList("sm-yaml-field");
                _drawerContent.Add(yaml);
                break;
            default:
                RenderProblems();
                break;
        }
    }

    private void RenderProblems()
    {
        if (_lastValidation == null || _lastValidation.Messages.Count == 0)
        {
            _drawerContent.Add(CreateEmptyState("문제 없음", ""));
            return;
        }

        var scroll = new ScrollView(ScrollViewMode.Vertical);
        scroll.style.flexGrow = 1f;
        for (int i = 0; i < _lastValidation.Messages.Count; i++)
        {
            ScenarioValidationMessage message = _lastValidation.Messages[i];
            var row = new VisualElement();
            row.AddToClassList("sm-problem-row");
            var icon = new Label(message.Severity == ScenarioValidationSeverity.Error
                ? "!"
                : message.Severity == ScenarioValidationSeverity.Warning ? "!" : "i");
            icon.AddToClassList("sm-problem-icon");
            icon.AddToClassList(message.Severity == ScenarioValidationSeverity.Error
                ? "sm-problem-icon--error"
                : message.Severity == ScenarioValidationSeverity.Warning
                    ? "sm-problem-icon--warning"
                    : "sm-problem-icon--info");
            row.Add(icon);
            var copy = new Label(message.Message);
            copy.AddToClassList("sm-problem-copy");
            copy.tooltip = message.Code + "\n" + message.ObjectId;
            row.Add(copy);
            scroll.Add(row);
        }

        _drawerContent.Add(scroll);
    }

    private void RenderStatus()
    {
        if (_statusLabel == null)
        {
            return;
        }

        bool dirty = AnyEditStackDirty();
        _statusLabel.text = dirty && !_statusHasError
            ? "저장되지 않은 변경"
            : _statusText;
        SequenceMakerTheme.SetSaveState(_saveStateDot, dirty, _statusHasError);

        string sourcePath = SourcePath();
        _sourceStatusLabel.text = string.IsNullOrWhiteSpace(sourcePath)
            ? "YAML 연결 안 됨"
            : ShortPath(sourcePath) + SourceHashStatus();
    }

    private void ValidateCurrent()
    {
        ISequenceSaveTarget target = CreateSaveTarget();
        if (target == null)
        {
            return;
        }

        SequenceSaveExportResult exported = target.Export();
        var validation = new ScenarioValidationResult();
        validation.Merge(exported.Validation);
        if (!exported.Validation.HasErrors)
        {
            validation.Merge(target.ValidateRoundTrip(
                exported.Text,
                target.SourcePath + ".validation"));
        }

        _lastValidation = validation;
        if (validation.HasErrors)
        {
            SetStatus("검증 실패", true);
            _workspace.SetDrawer(SequenceMakerDrawerTab.Problems, true);
        }
        else
        {
            SetStatus("검증 완료", false);
        }

        RenderAll();
    }

    private bool SaveCurrent()
    {
        ISequenceSaveTarget target = CreateSaveTarget();
        if (target == null)
        {
            SetStatus("저장할 대상이 없습니다.", true);
            RenderStatus();
            return false;
        }

        SequenceSaveResult result = new SequenceSaveCoordinator().Save(target);
        _lastValidation = result.Validation ?? new ScenarioValidationResult();
        if (!result.Success)
        {
            string status = result.Status == SequenceSaveStatus.Conflict
                ? "YAML 외부 변경 충돌"
                : result.ErrorMessage;
            SetStatus(status, true);
            _workspace.SetDrawer(
                result.Status == SequenceSaveStatus.Conflict
                    ? SequenceMakerDrawerTab.Yaml
                    : SequenceMakerDrawerTab.Problems,
                true);
            RenderAll();
            return false;
        }

        foreach (SequenceEditCommandStack stack in _editStacks.Values)
        {
            stack.MarkSaved();
        }

        _workspace.SetDirty(false);
        SetStatus("YAML 저장 완료", false);
        RefreshYamlPreview();
        RenderAll();
        return true;
    }

    private ISequenceSaveTarget CreateSaveTarget()
    {
        if (_workspace.TargetKind == SequenceMakerTargetKind.BattleScenario
            && _workspace.BattleScenario != null)
        {
            return new BattleScenarioSaveTarget(_workspace.BattleScenario, _catalog);
        }

        if (_workspace.StandaloneSequence != null)
        {
            return new StandaloneSequenceSaveTarget(
                _workspace.StandaloneSequence,
                _catalog,
                PrimaryModeFor(_workspace.StandaloneSequence));
        }

        return null;
    }

    private void Undo()
    {
        SequenceEditCommandStack stack = GetCurrentEditStack();
        if (stack != null && stack.Undo())
        {
            AfterEdit();
        }
    }

    private void Redo()
    {
        SequenceEditCommandStack stack = GetCurrentEditStack();
        if (stack != null && stack.Redo())
        {
            AfterEdit();
        }
    }

    private void AfterEdit()
    {
        _workspace.SetDirty(AnyEditStackDirty());
        RefreshYamlPreview();
        RefreshCatalogValidation();
        SetStatus("편집됨", false);
        RenderAll();
    }

    private void ToggleDensity()
    {
        _workspace.SetDensity(_workspace.Density == SequenceMakerDensity.Compact
            ? SequenceMakerDensity.Comfortable
            : SequenceMakerDensity.Compact);
    }

    private void SetPlaybackControlsEnabled(bool enabled)
    {
        _playButton?.SetEnabled(enabled);
        _playSelectedButton?.SetEnabled(enabled);
        _pauseButton?.SetEnabled(enabled);
        _stepButton?.SetEnabled(enabled);
        _stopButton?.SetEnabled(enabled);
    }

    private void OnWorkspaceChanged()
    {
        RenderAll();
    }

    private void OnKeyDown(KeyDownEvent evt)
    {
        bool command = evt.ctrlKey || evt.commandKey;
        if (!command)
        {
            return;
        }

        if (evt.keyCode == KeyCode.S)
        {
            SaveCurrent();
            evt.StopPropagation();
        }
        else if (evt.keyCode == KeyCode.Z && !evt.shiftKey)
        {
            Undo();
            evt.StopPropagation();
        }
        else if (evt.keyCode == KeyCode.Y
            || (evt.keyCode == KeyCode.Z && evt.shiftKey))
        {
            Redo();
            evt.StopPropagation();
        }
    }

    private SequenceEditCommandStack GetCurrentEditStack()
    {
        ActionSequenceAsset sequence = _workspace != null ? _workspace.SelectedSequence : null;
        if (sequence == null)
        {
            return null;
        }

        int id = sequence.GetInstanceID();
        if (_editStacks.TryGetValue(id, out SequenceEditCommandStack stack))
        {
            return stack;
        }

        try
        {
            stack = new SequenceEditCommandStack(sequence);
            stack.Changed += _ =>
            {
                _workspace.SetDirty(AnyEditStackDirty());
                RenderCommandState();
                RenderStatus();
            };
            _editStacks.Add(id, stack);
            return stack;
        }
        catch (Exception exception)
        {
            SetStatus(exception.Message, true);
            return null;
        }
    }

    private bool AnyEditStackDirty()
    {
        foreach (SequenceEditCommandStack stack in _editStacks.Values)
        {
            if (stack.IsDirty)
            {
                return true;
            }
        }

        return false;
    }

    private void CaptureLayoutDimensions()
    {
        if (_navigatorPanel == null)
        {
            return;
        }

        float navigator = _navigatorPanel.resolvedStyle.width;
        float inspector = _inspectorContent?.parent?.parent?.resolvedStyle.width
            ?? _workspace.InspectorWidth;
        float drawer = _workspace.IsDrawerOpen && _drawer != null
            ? _drawer.resolvedStyle.height
            : _workspace.DrawerHeight;
        if (!float.IsNaN(navigator)
            && !float.IsNaN(inspector)
            && !float.IsNaN(drawer))
        {
            _workspace.SetLayout(navigator, inspector, drawer);
        }
    }

    private bool TryGetSelectedAction(out ScenarioActionData action)
    {
        action = null;
        ActionSequenceAsset sequence = _workspace.SelectedSequence;
        if (sequence == null
            || string.IsNullOrWhiteSpace(_workspace.SelectedBlockId)
            || !SequenceBlockTree.TryFind(
                sequence,
                _workspace.SelectedBlockId,
                out SequenceBlockLocation location))
        {
            return false;
        }

        action = location.Action;
        return action != null;
    }

    private string BlockDisplayName(string blockId)
    {
        if (string.IsNullOrWhiteSpace(blockId)
            || !TryGetSelectedAction(out ScenarioActionData action))
        {
            return string.Empty;
        }

        return ActionDisplayName(action, FindCatalogEntry(action.ActionId));
    }

    private ActionCatalogEntry FindCatalogEntry(string actionId)
    {
        return _catalog != null ? _catalog.FindById(actionId) : null;
    }

    private bool IsSequenceInCurrentBattle(ActionSequenceAsset sequence)
    {
        return _workspace.BattleScenario != null
            && _workspace.BattleScenario.Sequences != null
            && _workspace.BattleScenario.Sequences.Contains(sequence);
    }

    private string SourcePath()
    {
        ScenarioSourceMetadata source = _workspace.TargetKind == SequenceMakerTargetKind.BattleScenario
            ? _workspace.BattleScenario?.Source
            : _workspace.StandaloneSequence?.Source;
        return source != null ? source.SourcePath ?? string.Empty : string.Empty;
    }

    private string SourceHashStatus()
    {
        ScenarioSourceMetadata source = _workspace.TargetKind == SequenceMakerTargetKind.BattleScenario
            ? _workspace.BattleScenario?.Source
            : _workspace.StandaloneSequence?.Source;
        if (source == null || string.IsNullOrWhiteSpace(source.SourceHash))
        {
            return "  ·  hash 없음";
        }

        return "  ·  " + source.SourceHash.Substring(0, Math.Min(8, source.SourceHash.Length));
    }

    private string TargetId()
    {
        if (_workspace.TargetKind == SequenceMakerTargetKind.BattleScenario)
        {
            return _workspace.BattleScenario != null
                ? _workspace.BattleScenario.ScenarioId ?? string.Empty
                : string.Empty;
        }

        return _workspace.StandaloneSequence != null
            ? _workspace.StandaloneSequence.SequenceId ?? string.Empty
            : string.Empty;
    }

    private static string PrimaryModeFor(ActionSequenceAsset sequence)
    {
        if (sequence?.Contract?.AllowedPrimaryModes != null
            && sequence.Contract.AllowedPrimaryModes.Count > 0
            && !string.IsNullOrWhiteSpace(sequence.Contract.AllowedPrimaryModes[0]))
        {
            return sequence.Contract.AllowedPrimaryModes[0].Trim();
        }

        return ActionSequenceSourceSync.DefaultPrimaryMode;
    }

    private static Button CreateNavigatorRow(string text, bool selected)
    {
        var row = new Button { text = text ?? string.Empty };
        row.AddToClassList("sm-nav-row");
        row.EnableInClassList("is-selected", selected);
        return row;
    }

    private static void AddSectionLabel(VisualElement parent, string text)
    {
        var label = new Label(text);
        label.AddToClassList("sm-section-label");
        parent.Add(label);
    }

    private static VisualElement CreateEmptyState(string title, string copy)
    {
        var state = new VisualElement();
        state.AddToClassList("sm-empty-state");
        var titleLabel = new Label(title ?? string.Empty);
        titleLabel.AddToClassList("sm-empty-title");
        state.Add(titleLabel);
        if (!string.IsNullOrWhiteSpace(copy))
        {
            var copyLabel = new Label(copy);
            copyLabel.AddToClassList("sm-empty-copy");
            state.Add(copyLabel);
        }

        return state;
    }

    private static void AddInspectorHeading(VisualElement parent, string title, string id)
    {
        var heading = new Label(title ?? string.Empty);
        heading.AddToClassList("sm-inspector-heading");
        parent.Add(heading);
        var idLabel = new Label(id ?? string.Empty);
        idLabel.AddToClassList("sm-inspector-id");
        parent.Add(idLabel);
    }

    private static void AddProperty(VisualElement parent, string label, string value)
    {
        var row = new VisualElement();
        row.AddToClassList("sm-property-row");
        var labelElement = new Label(label ?? string.Empty);
        labelElement.AddToClassList("sm-property-label");
        row.Add(labelElement);
        var valueElement = new Label(string.IsNullOrWhiteSpace(value) ? "-" : value);
        valueElement.AddToClassList("sm-property-value");
        row.Add(valueElement);
        parent.Add(row);
    }

    private static string ActionDisplayName(ScenarioActionData action, ActionCatalogEntry entry)
    {
        if (!string.IsNullOrWhiteSpace(action?.DesignerLabel))
        {
            return action.DesignerLabel.Trim();
        }

        if (!string.IsNullOrWhiteSpace(entry?.DisplayNameKo))
        {
            return entry.DisplayNameKo.Trim();
        }

        return action?.ActionId ?? "액션";
    }

    private static string ActionSummary(ScenarioActionData action, ActionCatalogEntry entry)
    {
        if (!string.IsNullOrWhiteSpace(action?.Note))
        {
            return action.Note.Trim();
        }

        if (!string.IsNullOrWhiteSpace(action?.ParametersJson)
            && action.ParametersJson.Trim() != "{}")
        {
            string parameters = action.ParametersJson.Trim();
            return parameters.Length <= 96 ? parameters : parameters.Substring(0, 93) + "...";
        }

        return entry?.UsageKo ?? action?.ActionId ?? string.Empty;
    }

    private bool MatchesSearch(ScenarioActionData action, string search)
    {
        if (string.IsNullOrEmpty(search))
        {
            return true;
        }

        ActionCatalogEntry entry = FindCatalogEntry(action.ActionId);
        string haystack = string.Join(" ", new[]
        {
            action.ActionId,
            action.DesignerLabel,
            action.Note,
            action.ParametersJson,
            entry?.DisplayNameKo,
            entry?.DescriptionKo,
            entry?.Category
        });
        return haystack.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static int CountBlocks(IList<ScenarioActionData> actions)
    {
        if (actions == null)
        {
            return 0;
        }

        int count = 0;
        for (int i = 0; i < actions.Count; i++)
        {
            if (actions[i] == null)
            {
                continue;
            }

            count++;
            count += CountBlocks(actions[i].Children);
        }

        return count;
    }

    private static int ErrorCount(ScenarioValidationResult validation)
    {
        if (validation == null)
        {
            return 0;
        }

        int count = 0;
        for (int i = 0; i < validation.Messages.Count; i++)
        {
            if (validation.Messages[i].Severity == ScenarioValidationSeverity.Error)
            {
                count++;
            }
        }

        return count;
    }

    private static string DisplayName(string preferred, string fallback)
    {
        return !string.IsNullOrWhiteSpace(preferred)
            ? preferred.Trim()
            : (!string.IsNullOrWhiteSpace(fallback) ? fallback.Trim() : "이름 없음");
    }

    private static string Normalize(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
    }

    private static string ShortPath(string path)
    {
        string normalized = path?.Replace('\\', '/') ?? string.Empty;
        const int max = 72;
        return normalized.Length <= max
            ? normalized
            : ".../" + normalized.Substring(normalized.Length - (max - 4));
    }

    private void SetStatus(string message, bool isError)
    {
        _statusText = string.IsNullOrWhiteSpace(message) ? "준비됨" : message.Trim();
        _statusHasError = isError;
    }

    private T Require<T>(string name) where T : VisualElement
    {
        T element = _root.Q<T>(name);
        if (element == null)
        {
            throw new InvalidOperationException("Sequence Maker UI element is missing: " + name);
        }

        return element;
    }
}
