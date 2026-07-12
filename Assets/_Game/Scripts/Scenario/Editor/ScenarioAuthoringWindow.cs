using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

public sealed class ScenarioAuthoringWindow : EditorWindow
{
    private const string WindowTitle = "시퀀스 메이커";

    private ObjectField _scenarioField;
    private ObjectField _standaloneSequenceField;
    private ObjectField _catalogField;
    private TextField _sourcePathField;
    private Label _statusLabel;
    private VisualElement _overviewPanel;
    private VisualElement _rulesPanel;
    private VisualElement _sequencesPanel;
    private VisualElement _validationPanel;
    private VisualElement _timelinePanel;
    private VisualElement _inspectorPanel;
    private VisualElement _syncPanel;
    private TextField _yamlPreviewField;
    private Button _refreshButton;
    private Button _validateSourceButton;
    private Button _reimportSourceButton;
    private Button _exportSourceButton;
    private Button _saveAndReimportButton;
    private Button _exportAsButton;

    private BattleScenarioData _scenario;
    private ActionSequenceAsset _standaloneSequence;
    private ActionCatalogAsset _catalog;
    private ScenarioSourceYamlExportResult _lastExportResult;
    private ActionSequenceSourceExportResult _lastStandaloneExportResult;
    private ScenarioValidationResult _cachedCatalogValidation;
    private ActionSequenceAsset _selectedSequence;
    private ScenarioActionData _selectedAction;
    private List<ScenarioActionData> _selectedActionList;
    private int _selectedActionIndex = -1;
    private string _selectedActionObjectId = string.Empty;

    [MenuItem("HubToHome/시나리오/개발/기존 시퀀스 메이커")]
    public static void Open()
    {
        SequenceMakerWindow.Open();
        Debug.LogWarning(
            "[Sequence Maker] 기존 편집기는 종료되었습니다. 공식 UI Toolkit Sequence Maker를 열었습니다.");
    }

    public void CreateGUI()
    {
        BuildLayout();
        ActionSequenceAsset selectedSequence = Selection.activeObject as ActionSequenceAsset;
        if (selectedSequence != null)
        {
            SetStandaloneSequence(selectedSequence);
            return;
        }

        SetScenario(Selection.activeObject as BattleScenarioData);
    }

    private void OnSelectionChange()
    {
        ActionSequenceAsset selectedSequence = Selection.activeObject as ActionSequenceAsset;
        if (selectedSequence != null && selectedSequence != _standaloneSequence)
        {
            SetStandaloneSequence(selectedSequence);
            return;
        }

        BattleScenarioData selectedScenario = Selection.activeObject as BattleScenarioData;
        if (selectedScenario != null && selectedScenario != _scenario)
        {
            SetScenario(selectedScenario);
        }
    }

    private void BuildLayout()
    {
        VisualElement root = rootVisualElement;
        root.Clear();
        root.style.flexDirection = FlexDirection.Column;
        root.style.paddingLeft = 12;
        root.style.paddingRight = 12;
        root.style.paddingTop = 10;
        root.style.paddingBottom = 10;

        Label title = new Label("시퀀스 메이커");
        title.style.unityFontStyleAndWeight = FontStyle.Bold;
        title.style.fontSize = 18;
        title.style.marginBottom = 8;
        root.Add(title);

        VisualElement toolbar = new VisualElement();
        toolbar.style.flexDirection = FlexDirection.Row;
        toolbar.style.alignItems = Align.Center;
        toolbar.style.marginBottom = 8;
        root.Add(toolbar);

        _scenarioField = new ObjectField("Battle Scenario");
        _scenarioField.objectType = typeof(BattleScenarioData);
        _scenarioField.allowSceneObjects = false;
        _scenarioField.style.flexGrow = 1;
        _scenarioField.RegisterValueChangedCallback(evt => SetScenario(evt.newValue as BattleScenarioData));
        toolbar.Add(_scenarioField);

        _standaloneSequenceField = new ObjectField("독립 Action Sequence");
        _standaloneSequenceField.objectType = typeof(ActionSequenceAsset);
        _standaloneSequenceField.allowSceneObjects = false;
        _standaloneSequenceField.style.flexGrow = 1;
        _standaloneSequenceField.style.marginLeft = 8;
        _standaloneSequenceField.RegisterValueChangedCallback(evt => SetStandaloneSequence(evt.newValue as ActionSequenceAsset));
        toolbar.Add(_standaloneSequenceField);

        _refreshButton = new Button(RefreshAll) { text = "새로고침" };
        _refreshButton.style.marginLeft = 8;
        toolbar.Add(_refreshButton);

        _validateSourceButton = new Button(ValidateSourcePath) { text = "원본 YAML 검증" };
        _validateSourceButton.style.marginLeft = 4;
        toolbar.Add(_validateSourceButton);

        _reimportSourceButton = new Button(ReimportSourcePath) { text = "런타임 에셋 반영" };
        _reimportSourceButton.style.marginLeft = 4;
        toolbar.Add(_reimportSourceButton);

        _exportSourceButton = new Button(SaveToSourcePath) { text = "원본 YAML 저장" };
        _exportSourceButton.style.marginLeft = 4;
        toolbar.Add(_exportSourceButton);

        _saveAndReimportButton = new Button(SaveAndReimportSourcePath) { text = "저장 및 반영" };
        _saveAndReimportButton.style.marginLeft = 4;
        toolbar.Add(_saveAndReimportButton);

        _exportAsButton = new Button(ExportAs) { text = "다른 경로로 내보내기" };
        _exportAsButton.style.marginLeft = 4;
        toolbar.Add(_exportAsButton);

        _catalogField = new ObjectField("Action Catalog");
        _catalogField.objectType = typeof(ActionCatalogAsset);
        _catalogField.allowSceneObjects = false;
        _catalogField.style.marginBottom = 8;
        _catalogField.RegisterValueChangedCallback(evt =>
        {
            _catalog = evt.newValue as ActionCatalogAsset;
            RefreshAll();
        });
        root.Add(_catalogField);

        _sourcePathField = new TextField("Source YAML");
        _sourcePathField.isReadOnly = true;
        _sourcePathField.style.marginBottom = 8;
        root.Add(_sourcePathField);

        _statusLabel = new Label();
        _statusLabel.style.marginBottom = 8;
        _statusLabel.style.whiteSpace = WhiteSpace.Normal;
        root.Add(_statusLabel);

        var board = new TwoPaneSplitView(0, 300, TwoPaneSplitViewOrientation.Horizontal);
        board.style.flexGrow = 1;
        root.Add(board);

        ScrollView flowScroll = new ScrollView();
        flowScroll.style.flexGrow = 1;
        flowScroll.style.paddingRight = 8;
        board.Add(flowScroll);

        _overviewPanel = MakeSection(flowScroll, "개요");
        _rulesPanel = MakeSection(flowScroll, "규칙");
        _sequencesPanel = MakeSection(flowScroll, "시퀀스 목록");
        _validationPanel = MakeSection(flowScroll, "검증 요약");

        var workArea = new TwoPaneSplitView(0, 430, TwoPaneSplitViewOrientation.Horizontal);
        workArea.style.flexGrow = 1;
        board.Add(workArea);

        ScrollView timelineScroll = new ScrollView();
        timelineScroll.style.flexGrow = 1;
        timelineScroll.style.paddingLeft = 8;
        timelineScroll.style.paddingRight = 8;
        workArea.Add(timelineScroll);

        _timelinePanel = MakeSection(timelineScroll, "액션 타임라인");

        ScrollView inspectorScroll = new ScrollView();
        inspectorScroll.style.flexGrow = 1;
        inspectorScroll.style.paddingLeft = 8;
        workArea.Add(inspectorScroll);

        _inspectorPanel = MakeSection(inspectorScroll, "액션 인스펙터");
        _syncPanel = MakeSection(inspectorScroll, "YAML / 동기화");

        _yamlPreviewField = new TextField("YAML 미리보기");
        _yamlPreviewField.multiline = true;
        _yamlPreviewField.isReadOnly = true;
        _yamlPreviewField.style.minHeight = 180;
        _yamlPreviewField.style.whiteSpace = WhiteSpace.Normal;
        _syncPanel.Add(_yamlPreviewField);
    }

    private static VisualElement MakeSection(VisualElement parent, string title)
    {
        VisualElement section = new VisualElement();
        section.style.marginBottom = 10;
        section.style.paddingLeft = 8;
        section.style.paddingRight = 8;
        section.style.paddingTop = 8;
        section.style.paddingBottom = 8;
        section.style.borderBottomWidth = 1;
        section.style.borderTopWidth = 1;
        section.style.borderLeftWidth = 1;
        section.style.borderRightWidth = 1;
        section.style.borderBottomColor = new Color(0.22f, 0.22f, 0.22f);
        section.style.borderTopColor = new Color(0.22f, 0.22f, 0.22f);
        section.style.borderLeftColor = new Color(0.22f, 0.22f, 0.22f);
        section.style.borderRightColor = new Color(0.22f, 0.22f, 0.22f);
        parent.Add(section);

        Label label = new Label(title);
        label.style.unityFontStyleAndWeight = FontStyle.Bold;
        label.style.marginBottom = 6;
        section.Add(label);

        return section;
    }

    private void SetScenario(BattleScenarioData scenario)
    {
        if (_scenarioField != null && _scenarioField.value != scenario)
        {
            _scenarioField.SetValueWithoutNotify(scenario);
        }

        _scenario = scenario;
        if (scenario != null)
        {
            _standaloneSequence = null;
            _standaloneSequenceField?.SetValueWithoutNotify(null);
        }

        RefreshAll();
    }

    private void SetStandaloneSequence(ActionSequenceAsset sequence)
    {
        if (_standaloneSequenceField != null && _standaloneSequenceField.value != sequence)
        {
            _standaloneSequenceField.SetValueWithoutNotify(sequence);
        }

        _standaloneSequence = sequence;
        if (sequence != null)
        {
            _scenario = null;
            _scenarioField?.SetValueWithoutNotify(null);
        }

        RefreshAll();
    }

    private void RefreshAll()
    {
        UpdateButtonState();
        RefreshYamlPreview();
        RefreshSummary();
    }

    private void UpdateButtonState()
    {
        bool hasTarget = _scenario != null || _standaloneSequence != null;
        _refreshButton?.SetEnabled(hasTarget);
        _exportAsButton?.SetEnabled(hasTarget);
        _validateSourceButton?.SetEnabled(hasTarget && !string.IsNullOrWhiteSpace(GetSourcePath()));
        _reimportSourceButton?.SetEnabled(hasTarget && !string.IsNullOrWhiteSpace(GetSourcePath()));
        _exportSourceButton?.SetEnabled(hasTarget && !string.IsNullOrWhiteSpace(GetSourcePath()));
        _saveAndReimportButton?.SetEnabled(hasTarget && !string.IsNullOrWhiteSpace(GetSourcePath()));
    }

    private void RefreshSummary()
    {
        ClearPanel(_overviewPanel);
        ClearPanel(_rulesPanel);
        ClearPanel(_sequencesPanel);
        ClearPanel(_validationPanel);
        ClearPanel(_timelinePanel);
        ClearPanel(_inspectorPanel);
        ClearPanel(_syncPanel);
        if (_syncPanel != null && _yamlPreviewField != null)
        {
            _syncPanel.Add(_yamlPreviewField);
        }

        if (_scenario == null && _standaloneSequence == null)
        {
            AddInfo(_overviewPanel, "Battle Scenario Data 또는 독립 Action Sequence를 선택하세요.");
            AddInfo(_validationPanel, "검증할 시나리오가 없습니다.");
            AddInfo(_timelinePanel, "시퀀스를 선택하면 액션 타임라인이 표시됩니다.");
            AddInfo(_inspectorPanel, "액션을 선택하면 파라미터를 편집할 수 있습니다.");
            _sourcePathField.value = string.Empty;
            _cachedCatalogValidation = null;
            ClearSelection();
            SetStatus("시나리오 또는 독립 시퀀스 에셋을 선택하면 개요와 YAML 미리보기가 표시됩니다.", MessageType.Info);
            return;
        }

        if (_standaloneSequence != null)
        {
            RefreshStandaloneSequenceSummary();
            return;
        }

        _cachedCatalogValidation = _catalog != null
            ? ScenarioCatalogValidator.ValidateBattleScenario(_scenario, _catalog)
            : null;
        EnsureSelection();

        _sourcePathField.value = GetSourcePath();
        AddInfo(_overviewPanel, "ID", EmptyDash(_scenario.ScenarioId));
        AddInfo(_overviewPanel, "제목", EmptyDash(_scenario.TitleKo));
        AddInfo(_overviewPanel, "Primary Mode", EmptyDash(_scenario.PrimaryMode));
        AddInfo(_overviewPanel, "시작 모듈", EmptyDash(_scenario.OpeningModule));
        AddInfo(_overviewPanel, "메모리 키", EmptyDash(_scenario.MemoryKey));
        AddInfo(_overviewPanel, "아군", JoinIds(_scenario.PartyIds));
        AddInfo(_overviewPanel, "적", JoinIds(_scenario.EnemyIds));
        AddInfo(_overviewPanel, "Source Hash", ShortHash(_scenario.Source != null ? _scenario.Source.SourceHash : string.Empty));

        RenderRules();
        RenderSequences();
        RenderSelectedSequenceTimeline();
        RenderActionInspector();
        RenderSyncAndValidation();
    }

    private void RefreshYamlPreview()
    {
        if (_standaloneSequence != null)
        {
            _lastExportResult = null;
            _lastStandaloneExportResult = ActionSequenceSourceSync.Export(_standaloneSequence);
            _yamlPreviewField.value = _lastStandaloneExportResult.Text ?? string.Empty;
            SetValidationStatus(_lastStandaloneExportResult.Validation, "독립 시퀀스 YAML 미리보기를 생성했습니다.");
            UpdateButtonState();
            return;
        }

        if (_scenario == null)
        {
            _lastExportResult = null;
            _lastStandaloneExportResult = null;
            _yamlPreviewField.value = string.Empty;
            return;
        }

        _lastStandaloneExportResult = null;
        _lastExportResult = new ScenarioSourceYamlExportCommand().ExportToText(_scenario);
        _yamlPreviewField.value = _lastExportResult.Text ?? string.Empty;
        SetValidationStatus(_lastExportResult.Validation, "YAML 미리보기를 생성했습니다.");
        UpdateButtonState();
    }

    private void RenderRules()
    {
        if (_scenario.Rules == null || _scenario.Rules.Count == 0)
        {
            AddInfo(_rulesPanel, "등록된 규칙이 없습니다.");
            return;
        }

        for (int i = 0; i < _scenario.Rules.Count; i++)
        {
            BattleEventRuleData rule = _scenario.Rules[i];
            if (rule == null)
            {
                AddInfo(_rulesPanel, $"#{i + 1}", "비어 있는 규칙");
                continue;
            }

            string disabled = rule.Disabled ? "비활성 / " : string.Empty;
            string summary = disabled
                + FormatEvent(rule.EventType)
                + " / 대상 " + EmptyDash(rule.SubjectId)
                + " / " + FormatTiming(rule.Timing)
                + " / " + FormatOnce(rule.Once)
                + " -> " + EmptyDash(rule.SequenceId);
            if (rule.EventType == BattleEventType.EnemyHpCrossedBelow)
            {
                summary += " / HP " + Mathf.RoundToInt(rule.ThresholdRatio * 100f) + "% 이하";
            }
            else if (rule.EventType == BattleEventType.GameModuleCompleted
                && !string.IsNullOrWhiteSpace(rule.OutcomeId))
            {
                summary += " / 결과 " + rule.OutcomeId.Trim();
            }

            AddInfo(_rulesPanel, EmptyDash(rule.RuleId), summary);
        }
    }

    private void RenderSequences()
    {
        if (_scenario.Sequences == null || _scenario.Sequences.Count == 0)
        {
            AddInfo(_sequencesPanel, "등록된 시퀀스가 없습니다.");
            return;
        }

        for (int i = 0; i < _scenario.Sequences.Count; i++)
        {
            ActionSequenceAsset sequence = _scenario.Sequences[i];
            if (sequence == null)
            {
                AddInfo(_sequencesPanel, $"#{i + 1}", "비어 있는 시퀀스");
                continue;
            }

            string title = EmptyDash(sequence.SequenceId);
            if (!string.IsNullOrWhiteSpace(sequence.DisplayNameKo))
            {
                title += " / " + sequence.DisplayNameKo.Trim();
            }

            bool selected = sequence == _selectedSequence;
            Button row = new Button(() => SelectSequence(sequence))
            {
                text = (selected ? "▶ " : string.Empty) + title + " · " + CountActions(sequence.Actions) + "개 액션"
            };
            row.style.marginBottom = 4;
            row.style.unityTextAlign = TextAnchor.MiddleLeft;
            row.style.whiteSpace = WhiteSpace.Normal;
            if (selected)
            {
                row.style.backgroundColor = new Color(0.16f, 0.30f, 0.42f);
            }

            _sequencesPanel.Add(row);
        }
    }

    private void RefreshStandaloneSequenceSummary()
    {
        _cachedCatalogValidation = _catalog != null
            ? ScenarioCatalogValidator.ValidateSequence(_standaloneSequence, _catalog)
            : null;
        EnsureSelection();

        _sourcePathField.value = GetSourcePath();
        AddInfo(_overviewPanel, "형식", "독립 Action Sequence");
        AddInfo(_overviewPanel, "ID", EmptyDash(_standaloneSequence.SequenceId));
        AddInfo(_overviewPanel, "제목", EmptyDash(_standaloneSequence.DisplayNameKo));
        AddInfo(_overviewPanel, "Primary Mode", ActionSequenceSourceSync.DefaultPrimaryMode);
        AddInfo(_overviewPanel, "Source Hash", ShortHash(_standaloneSequence.Source != null ? _standaloneSequence.Source.SourceHash : string.Empty));
        AddInfo(_rulesPanel, "전투 규칙과 분리된 전역/오버월드 시퀀스입니다.");

        string title = EmptyDash(_standaloneSequence.SequenceId);
        if (!string.IsNullOrWhiteSpace(_standaloneSequence.DisplayNameKo))
        {
            title += " / " + _standaloneSequence.DisplayNameKo.Trim();
        }

        Button row = new Button(() => SelectSequence(_standaloneSequence))
        {
            text = "▶ " + title + " · " + CountActions(_standaloneSequence.Actions) + "개 액션"
        };
        row.style.marginBottom = 4;
        row.style.unityTextAlign = TextAnchor.MiddleLeft;
        row.style.backgroundColor = new Color(0.16f, 0.30f, 0.42f);
        _sequencesPanel.Add(row);

        RenderSelectedSequenceTimeline();
        RenderActionInspector();
        RenderSyncAndValidation();
    }

    private void RenderSelectedSequenceTimeline()
    {
        if (_selectedSequence == null)
        {
            AddInfo(_timelinePanel, "선택된 시퀀스가 없습니다.");
            return;
        }

        string title = EmptyDash(_selectedSequence.SequenceId);
        if (!string.IsNullOrWhiteSpace(_selectedSequence.DisplayNameKo))
        {
            title += " / " + _selectedSequence.DisplayNameKo.Trim();
        }

        AddInfo(_timelinePanel, title, CountActions(_selectedSequence.Actions) + "개 액션");
        AddSequenceControls(_timelinePanel, _selectedSequence);
        AddActionRows(_timelinePanel, _selectedSequence, _selectedSequence.Actions, 0);
    }

    private void RenderActionInspector()
    {
        if (_selectedAction == null)
        {
            AddInfo(_inspectorPanel, "액션 row를 선택하면 상세 정보와 파라미터 편집기가 표시됩니다.");
            return;
        }

        ActionCatalogEntry entry = _catalog != null ? _catalog.FindById(_selectedAction.ActionId) : null;
        AddInfo(_inspectorPanel, "액션", FormatActionLabel(_selectedAction));
        AddInfo(_inspectorPanel, "ID", EmptyDash(_selectedAction.ActionId));
        AddInfo(_inspectorPanel, "카테고리", entry != null ? EmptyDash(entry.Category) : "카탈로그 미등록");
        AddInfo(_inspectorPanel, "상태", _selectedAction.Disabled ? "비활성" : "활성");
        if (entry != null && !string.IsNullOrWhiteSpace(entry.DescriptionKo))
        {
            AddInfo(_inspectorPanel, "설명", entry.DescriptionKo.Trim());
        }

        ScenarioValidationMessage validationMessage = FindValidationMessage(_selectedActionObjectId);
        if (validationMessage != null)
        {
            Label badge = MakeValidationBadge(validationMessage);
            badge.style.marginBottom = 6;
            _inspectorPanel.Add(badge);
            AddInfo(_inspectorPanel, "검증", validationMessage.Code + " / " + validationMessage.Message);
        }

        AddParameterEditor(entry);
        AddAdvancedJsonEditor();
    }

    private void AddParameterEditor(ActionCatalogEntry entry)
    {
        VisualElement section = new VisualElement();
        section.style.marginTop = 8;
        section.style.marginBottom = 8;
        _inspectorPanel.Add(section);

        Label title = new Label("파라미터");
        title.style.unityFontStyleAndWeight = FontStyle.Bold;
        title.style.marginBottom = 4;
        section.Add(title);

        List<string> names = ScenarioAuthoringParameterView.GetParameterNames(_selectedAction, entry);
        if (names.Count == 0)
        {
            AddInfo(section, "파라미터가 없습니다. 고급 JSON에서 직접 추가할 수 있습니다.");
            return;
        }

        for (int i = 0; i < names.Count; i++)
        {
            string parameterName = names[i];
            ActionCatalogParameter parameter = ScenarioAuthoringParameterView.FindParameter(entry, parameterName);
            string label = parameter != null && !string.IsNullOrWhiteSpace(parameter.DisplayNameKo)
                ? parameter.DisplayNameKo.Trim() + " (" + parameterName + ")"
                : parameterName;
            string currentValue = ScenarioAuthoringParameterView.GetParameterValue(_selectedAction, parameterName);

            TextField field = new TextField(label);
            field.value = currentValue;
            field.isDelayed = true;
            field.style.marginBottom = 4;
            field.tooltip = parameter != null
                ? parameter.DescriptionKo
                : "카탈로그 파라미터 정의가 없어 현재 JSON 값을 기준으로 표시합니다.";
            string capturedName = parameterName;
            ActionCatalogParameter capturedParameter = parameter;
            field.RegisterValueChangedCallback(evt => ApplyParameterValue(capturedName, evt.newValue, capturedParameter));
            section.Add(field);

            if (parameter != null && parameter.Required && string.IsNullOrWhiteSpace(currentValue))
            {
                Label warning = new Label("필수 값입니다.");
                warning.style.color = new Color(1f, 0.78f, 0.35f);
                warning.style.marginBottom = 4;
                section.Add(warning);
            }
        }
    }

    private void AddAdvancedJsonEditor()
    {
        Foldout foldout = new Foldout { text = "고급 JSON" };
        foldout.value = false;
        foldout.style.marginTop = 8;
        _inspectorPanel.Add(foldout);

        TextField jsonField = new TextField();
        jsonField.multiline = true;
        jsonField.value = ScenarioAuthoringParameterView.FormatJson(_selectedAction);
        jsonField.style.minHeight = 96;
        jsonField.style.whiteSpace = WhiteSpace.Normal;
        foldout.Add(jsonField);

        Button applyButton = new Button(() =>
        {
            if (_selectedSequence == null || _selectedAction == null)
            {
                return;
            }

            string error;
            if (!ScenarioAuthoringParameterView.TrySetRawJson(_selectedAction, jsonField.value, out error))
            {
                SetStatus("JSON 적용 실패: " + error, MessageType.Error);
                return;
            }

            RecordSequenceChange(_selectedSequence, "시나리오 액션 JSON 편집");
            RefreshAll();
            SetStatus("액션 JSON을 적용했습니다.", MessageType.Info);
        })
        {
            text = "JSON 적용"
        };
        applyButton.style.marginTop = 4;
        foldout.Add(applyButton);
    }

    private void SelectSequence(ActionSequenceAsset sequence)
    {
        _selectedSequence = sequence;
        _selectedAction = null;
        _selectedActionList = null;
        _selectedActionIndex = -1;
        _selectedActionObjectId = string.Empty;
        RefreshAll();
    }

    private void SelectAction(
        ActionSequenceAsset owner,
        List<ScenarioActionData> actions,
        int index,
        ScenarioActionData action,
        string objectId)
    {
        _selectedSequence = owner;
        _selectedActionList = actions;
        _selectedActionIndex = index;
        _selectedAction = action;
        _selectedActionObjectId = objectId ?? string.Empty;
        RefreshAll();
    }

    private void EnsureSelection()
    {
        if (_standaloneSequence != null)
        {
            if (_selectedSequence != _standaloneSequence)
            {
                _selectedSequence = _standaloneSequence;
                _selectedAction = null;
                _selectedActionList = null;
                _selectedActionIndex = -1;
                _selectedActionObjectId = string.Empty;
            }

            if (_selectedActionList == null
                || _selectedActionIndex < 0
                || _selectedActionIndex >= _selectedActionList.Count
                || _selectedActionList[_selectedActionIndex] != _selectedAction)
            {
                _selectedAction = null;
                _selectedActionList = null;
                _selectedActionIndex = -1;
                _selectedActionObjectId = string.Empty;
            }

            return;
        }

        if (_scenario == null || _scenario.Sequences == null || _scenario.Sequences.Count == 0)
        {
            ClearSelection();
            return;
        }

        if (_selectedSequence == null || !_scenario.Sequences.Contains(_selectedSequence))
        {
            _selectedSequence = _scenario.Sequences[0];
            _selectedAction = null;
            _selectedActionList = null;
            _selectedActionIndex = -1;
            _selectedActionObjectId = string.Empty;
            return;
        }

        if (_selectedActionList == null
            || _selectedActionIndex < 0
            || _selectedActionIndex >= _selectedActionList.Count
            || _selectedActionList[_selectedActionIndex] != _selectedAction)
        {
            _selectedAction = null;
            _selectedActionList = null;
            _selectedActionIndex = -1;
            _selectedActionObjectId = string.Empty;
        }
    }

    private void ClearSelection()
    {
        _selectedSequence = null;
        _selectedAction = null;
        _selectedActionList = null;
        _selectedActionIndex = -1;
        _selectedActionObjectId = string.Empty;
    }

    private void ApplyParameterValue(
        string parameterName,
        string rawValue,
        ActionCatalogParameter parameter)
    {
        if (_selectedSequence == null || _selectedAction == null)
        {
            return;
        }

        string error;
        if (!ScenarioAuthoringParameterView.SetParameterValue(
            _selectedAction,
            parameterName,
            rawValue,
            parameter,
            out error))
        {
            SetStatus("파라미터 적용 실패: " + error, MessageType.Error);
            return;
        }

        RecordSequenceChange(_selectedSequence, "시나리오 액션 파라미터 편집");
        RefreshAll();
        SetStatus("파라미터를 적용했습니다. 원본 YAML 저장 또는 저장 및 반영을 실행하세요.", MessageType.Info);
    }

    private void RenderSyncAndValidation()
    {
        string sourceSyncStatus = GetSourceSyncStatus();
        AddInfo(_validationPanel, "Source", sourceSyncStatus);
        AddInfo(_syncPanel, "Source", sourceSyncStatus);
        AddInfo(_syncPanel, "경로", EmptyDash(GetSourcePath()));

        if (_lastExportResult != null && _lastExportResult.Validation != null)
        {
            AddValidationRows(_validationPanel, "YAML Export", _lastExportResult.Validation);
            AddValidationRows(_syncPanel, "YAML Export", _lastExportResult.Validation);
        }

        if (_lastStandaloneExportResult != null && _lastStandaloneExportResult.Validation != null)
        {
            AddValidationRows(_validationPanel, "YAML Export", _lastStandaloneExportResult.Validation);
            AddValidationRows(_syncPanel, "YAML Export", _lastStandaloneExportResult.Validation);
        }

        if (_catalog == null)
        {
            AddInfo(_validationPanel, "Action Catalog", "선택되지 않아 카탈로그 기반 검증을 생략했습니다.");
            AddInfo(_syncPanel, "Action Catalog", "선택되지 않음");
            return;
        }

        AddValidationRows(_validationPanel, "Catalog", _cachedCatalogValidation);
        AddValidationRows(_syncPanel, "Catalog", _cachedCatalogValidation);
    }

    private string GetSourceSyncStatus()
    {
        string sourcePath = GetSourcePath();
        if (string.IsNullOrEmpty(sourcePath))
        {
            return "Source YAML 경로가 없습니다.";
        }

        try
        {
            string fullPath = Path.GetFullPath(sourcePath);
            if (!File.Exists(fullPath))
            {
                return "파일 없음: " + sourcePath;
            }

            string sourceHash = _scenario != null && _scenario.Source != null
                ? (_scenario.Source.SourceHash ?? string.Empty).Trim()
                : _standaloneSequence != null && _standaloneSequence.Source != null
                    ? (_standaloneSequence.Source.SourceHash ?? string.Empty).Trim()
                    : string.Empty;
            if (string.IsNullOrEmpty(sourceHash))
            {
                return "저장된 source hash가 없어 stale 여부를 판단할 수 없습니다.";
            }

            string currentHash = ScenarioSourceHash.Compute(File.ReadAllText(fullPath));
            return currentHash == sourceHash
                ? "동기화됨"
                : "YAML 파일이 runtime asset metadata보다 새롭거나 달라졌습니다.";
        }
        catch (System.Exception exception)
        {
            return "Source YAML 확인 실패: " + exception.Message;
        }
    }

    private static void AddValidationRows(VisualElement panel, string label, ScenarioValidationResult validation)
    {
        if (validation == null || validation.Messages.Count == 0)
        {
            AddInfo(panel, label, "문제 없음");
            return;
        }

        int limit = Mathf.Min(validation.Messages.Count, 8);
        for (int i = 0; i < limit; i++)
        {
            ScenarioValidationMessage message = validation.Messages[i];
            AddInfo(panel, label + " " + FormatSeverity(message.Severity), message.Code + " / " + message.Message);
        }

        if (validation.Messages.Count > limit)
        {
            AddInfo(panel, label, "추가 메시지 " + (validation.Messages.Count - limit) + "개");
        }
    }

    private void AddSequenceControls(VisualElement panel, ActionSequenceAsset sequence)
    {
        VisualElement row = new VisualElement();
        row.style.flexDirection = FlexDirection.Row;
        row.style.alignItems = Align.Center;
        row.style.marginBottom = 5;
        row.style.marginLeft = 8;
        panel.Add(row);

        TextField actionIdField = new TextField();
        actionIdField.style.flexGrow = 1;
        actionIdField.label = "추가할 액션 ID";
        actionIdField.tooltip = "예: dialogue.wait, module.switch, battle.flag.set";
        row.Add(actionIdField);

        List<string> pickerLabels = ScenarioAuthoringCatalogView.BuildActionPickerLabels(_catalog);
        if (pickerLabels.Count > 0)
        {
            var picker = new PopupField<string>("카탈로그", pickerLabels, 0);
            picker.style.marginLeft = 4;
            picker.style.minWidth = 190;
            picker.RegisterValueChangedCallback(evt =>
            {
                string actionId = ScenarioAuthoringCatalogView.ResolveActionIdFromPickerLabel(evt.newValue);
                if (!string.IsNullOrWhiteSpace(actionId))
                {
                    actionIdField.value = actionId;
                }
            });
            actionIdField.value = ScenarioAuthoringCatalogView.ResolveActionIdFromPickerLabel(picker.value);
            row.Add(picker);
        }

        Button addButton = new Button(() => AddAction(sequence, sequence.Actions, actionIdField))
        {
            text = "삽입"
        };
        addButton.style.marginLeft = 4;
        row.Add(addButton);
    }

    private void AddActionRows(
        VisualElement panel,
        ActionSequenceAsset owner,
        List<ScenarioActionData> actions,
        int depth)
    {
        AddActionRows(panel, owner, actions, depth, owner != null ? owner.SequenceId : string.Empty);
    }

    private void AddActionRows(
        VisualElement panel,
        ActionSequenceAsset owner,
        List<ScenarioActionData> actions,
        int depth,
        string objectIdPrefix)
    {
        if (actions == null)
        {
            return;
        }

        for (int i = 0; i < actions.Count; i++)
        {
            ScenarioActionData action = actions[i];
            if (action == null)
            {
                AddInfo(panel, Indent(depth) + "- 비어 있는 액션");
                continue;
            }

            string objectId = objectIdPrefix + ".actions[" + i + "]";
            string disabled = action.Disabled ? "비활성 / " : string.Empty;
            VisualElement row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.marginBottom = 3;
            row.style.marginLeft = 8 + depth * 16;
            row.style.paddingTop = 3;
            row.style.paddingBottom = 3;
            row.style.paddingLeft = 4;
            row.style.paddingRight = 4;
            if (action == _selectedAction)
            {
                row.style.backgroundColor = new Color(0.12f, 0.28f, 0.40f);
            }

            int index = i;
            row.RegisterCallback<MouseDownEvent>(_ => SelectAction(owner, actions, index, action, objectId));
            panel.Add(row);

            Label label = new Label("- " + disabled + FormatActionLabel(action));
            label.style.whiteSpace = WhiteSpace.Normal;
            label.style.flexGrow = 1;
            row.Add(label);

            ScenarioValidationMessage validationMessage = FindValidationMessage(objectId);
            if (validationMessage != null)
            {
                Label badge = MakeValidationBadge(validationMessage);
                row.Add(badge);
            }

            Button upButton = MakeSmallButton("위", () => MoveAction(owner, actions, index, -1));
            upButton.SetEnabled(index > 0);
            row.Add(upButton);

            Button downButton = MakeSmallButton("아래", () => MoveAction(owner, actions, index, 1));
            downButton.SetEnabled(index < actions.Count - 1);
            row.Add(downButton);

            row.Add(MakeSmallButton("복제", () => DuplicateAction(owner, actions, index)));
            row.Add(MakeSmallButton(action.Disabled ? "켜기" : "끄기", () => ToggleAction(owner, action)));
            row.Add(MakeSmallButton("삭제", () => DeleteAction(owner, actions, index)));

            if (action.Children != null && action.Children.Count > 0)
            {
                AddActionRows(panel, owner, action.Children, depth + 1, objectId);
            }
        }
    }

    private static Button MakeSmallButton(string text, System.Action action)
    {
        Button button = new Button(action) { text = text };
        button.style.marginLeft = 3;
        button.style.minWidth = 34;
        return button;
    }

    private string FormatActionLabel(ScenarioActionData action)
    {
        if (action == null)
        {
            return "비어 있는 액션";
        }

        ActionCatalogEntry entry = _catalog != null ? _catalog.FindById(action.ActionId) : null;
        if (entry != null && !string.IsNullOrWhiteSpace(entry.DisplayNameKo))
        {
            return entry.DisplayNameKo.Trim() + " (" + EmptyDash(action.ActionId) + ")";
        }

        return EmptyDash(action.ActionId);
    }

    private ScenarioValidationMessage FindValidationMessage(string objectId)
    {
        if (_cachedCatalogValidation == null || string.IsNullOrWhiteSpace(objectId))
        {
            return null;
        }

        return ScenarioAuthoringCatalogView.FindMessageForObject(_cachedCatalogValidation, objectId);
    }

    private static Label MakeValidationBadge(ScenarioValidationMessage message)
    {
        Label badge = new Label(FormatSeverity(message.Severity));
        badge.tooltip = message.Code + ": " + message.Message;
        badge.style.marginLeft = 4;
        badge.style.marginRight = 2;
        badge.style.paddingLeft = 4;
        badge.style.paddingRight = 4;
        badge.style.unityFontStyleAndWeight = FontStyle.Bold;
        badge.style.color = Color.white;
        switch (message.Severity)
        {
            case ScenarioValidationSeverity.Error:
                badge.style.backgroundColor = new Color(0.72f, 0.16f, 0.16f);
                break;
            case ScenarioValidationSeverity.Warning:
                badge.style.backgroundColor = new Color(0.64f, 0.42f, 0.12f);
                break;
            default:
                badge.style.backgroundColor = new Color(0.18f, 0.38f, 0.62f);
                break;
        }

        return badge;
    }

    private void SaveToSourcePath()
    {
        if (_standaloneSequence != null)
        {
            try
            {
                ActionSequenceSourceSync.SaveToSourcePath(_standaloneSequence);
                RefreshAll();
                SetStatus("독립 시퀀스 Source YAML을 저장하고 metadata를 갱신했습니다.", MessageType.Info);
            }
            catch (System.Exception exception)
            {
                SetStatus("독립 시퀀스 Source YAML 저장에 실패했습니다: " + exception.Message, MessageType.Error);
            }

            return;
        }

        ScenarioSourceYamlExportResult result = new ScenarioSourceYamlExportCommand().ExportToSourcePath(_scenario);
        _lastExportResult = result;
        _yamlPreviewField.value = result.Text ?? string.Empty;
        if (result.Success)
        {
            Undo.RecordObject(_scenario, "시나리오 원본 YAML 저장");

            ScenarioSourceMetadataEditorSync.ApplyExportResult(_scenario, result, DateTime.UtcNow);
            MarkScenarioDirty(_scenario);
            RefreshAll();
        }

        SetValidationStatus(result.Validation, result.Success ? "Source YAML을 저장하고 metadata를 갱신했습니다." : "Source YAML 저장에 실패했습니다.");
    }

    private void ValidateSourcePath()
    {
        string sourcePath = GetSourcePath();
        if (string.IsNullOrWhiteSpace(sourcePath))
        {
            SetStatus("검증할 Source YAML 경로가 없습니다.", MessageType.Warning);
            return;
        }

        try
        {
            string text = File.ReadAllText(Path.GetFullPath(sourcePath));
            if (_standaloneSequence != null)
            {
                ActionSequenceSourceImportResult standaloneResult = ActionSequenceSourceSync.Import(text, sourcePath);
                if (standaloneResult.Success && _catalog != null)
                {
                    standaloneResult.Validation.Merge(ScenarioCatalogValidator.ValidateSequence(standaloneResult.Sequence, _catalog));
                }

                if (standaloneResult.Success)
                {
                    SetStatus("독립 시퀀스 원본 YAML 검증 성공: 액션 " + CountActions(standaloneResult.Sequence.Actions) + "개를 읽었습니다.", MessageType.Info);
                }
                else
                {
                    SetValidationStatus(standaloneResult.Validation, "독립 시퀀스 원본 YAML을 읽었습니다.");
                }

                DestroyTemporaryStandaloneSequence(standaloneResult.Sequence);
                return;
            }

            var resolver = new AssetDatabaseScenarioDialogueReferenceResolver();
            var importer = new ScenarioSourceImporter(
                new ScenarioSourceYamlParser(),
                resolver,
                resolver);
            ScenarioSourceSyncResult result = importer.Import(text, sourcePath);
            if (result.Success)
            {
                SetStatus(
                    "원본 YAML 검증 성공: 규칙 "
                    + result.Scenario.Rules.Count
                    + "개, 시퀀스 "
                    + result.Scenario.Sequences.Count
                    + "개를 읽었습니다.",
                    MessageType.Info);
            }
            else
            {
                SetValidationStatus(result.Validation, "원본 YAML을 읽었습니다.");
            }

            DestroyTemporaryScenario(result.Scenario);
        }
        catch (System.Exception exception)
        {
            SetStatus("원본 YAML 검증 실패: " + exception.Message, MessageType.Error);
        }
    }

    private void ReimportSourcePath()
    {
        if (_standaloneSequence != null)
        {
            ActionSequenceSourceRuntimeAssetReimportResult standaloneResult = ActionSequenceSourceSync.ReimportFromSourcePath(
                _standaloneSequence,
                _catalog,
                ActionSequenceSourceSync.DefaultPrimaryMode,
                DateTime.UtcNow);
            RefreshAll();
            SetValidationStatus(
                standaloneResult.Validation,
                standaloneResult.Success
                    ? "독립 시퀀스 원본 YAML을 런타임 에셋에 반영했습니다."
                    : "독립 시퀀스 원본 YAML을 런타임 에셋에 반영하지 못했습니다.");
            return;
        }

        if (_scenario == null)
        {
            SetStatus("반영할 Battle Scenario Data를 선택하세요.", MessageType.Warning);
            return;
        }

        string sourcePath = GetSourcePath();
        if (string.IsNullOrWhiteSpace(sourcePath))
        {
            SetStatus("반영할 Source YAML 경로가 없습니다.", MessageType.Warning);
            return;
        }

        var command = new ScenarioSourceRuntimeAssetReimportCommand();
        ScenarioSourceRuntimeAssetReimportResult result = command.ReimportFromSourcePath(
            _scenario,
            _catalog,
            DateTime.UtcNow);

        if (result.Success)
        {
            string message = "원본 YAML을 런타임 에셋에 반영했습니다. 기존 시퀀스 "
                + result.ReusedSequenceCount
                + "개 재사용, 새 시퀀스 "
                + result.CreatedSequenceCount
                + "개 생성";
            if (result.DetachedSequenceCount > 0)
            {
                message += ", 제외된 기존 시퀀스 " + result.DetachedSequenceCount + "개";
            }

            message += ".";
            RefreshAll();
            SetValidationStatus(result.Validation, message);
            return;
        }

        SetValidationStatus(result.Validation, "원본 YAML을 런타임 에셋에 반영하지 못했습니다.");
    }

    private void SaveAndReimportSourcePath()
    {
        if (_standaloneSequence != null)
        {
            try
            {
                ActionSequenceSourceSync.SaveToSourcePath(_standaloneSequence);
            }
            catch (System.Exception exception)
            {
                SetStatus("독립 시퀀스 저장 및 반영에 실패했습니다: " + exception.Message, MessageType.Error);
                return;
            }

            ReimportSourcePath();
            return;
        }

        if (_scenario == null)
        {
            SetStatus("저장할 Battle Scenario Data를 선택하세요.", MessageType.Warning);
            return;
        }

        string sourcePath = GetSourcePath();
        if (string.IsNullOrWhiteSpace(sourcePath))
        {
            SetStatus("저장 및 반영할 Source YAML 경로가 없습니다.", MessageType.Warning);
            return;
        }

        ScenarioSourceYamlExportResult exportResult = new ScenarioSourceYamlExportCommand().ExportToSourcePath(_scenario);
        _lastExportResult = exportResult;
        _yamlPreviewField.value = exportResult.Text ?? string.Empty;
        if (!exportResult.Success)
        {
            SetValidationStatus(exportResult.Validation, "Source YAML 저장에 실패했습니다.");
            return;
        }

        ScenarioSourceMetadataEditorSync.ApplyExportResult(_scenario, exportResult, DateTime.UtcNow);
        MarkScenarioDirty(_scenario);

        var command = new ScenarioSourceRuntimeAssetReimportCommand();
        ScenarioSourceRuntimeAssetReimportResult reimportResult = command.ReimportFromSourcePath(
            _scenario,
            _catalog,
            DateTime.UtcNow);

        RefreshAll();
        if (!reimportResult.Success)
        {
            SetValidationStatus(reimportResult.Validation, "Source YAML은 저장했지만 런타임 에셋 반영에 실패했습니다.");
            return;
        }

        string message = "Source YAML 저장 후 런타임 에셋에 반영했습니다. 기존 시퀀스 "
            + reimportResult.ReusedSequenceCount
            + "개 재사용, 새 시퀀스 "
            + reimportResult.CreatedSequenceCount
            + "개 생성";
        if (reimportResult.DetachedSequenceCount > 0)
        {
            message += ", 제외된 기존 시퀀스 " + reimportResult.DetachedSequenceCount + "개";
        }

        SetValidationStatus(reimportResult.Validation, message + ".");
    }

    private void AddAction(
        ActionSequenceAsset owner,
        List<ScenarioActionData> actions,
        TextField actionIdField)
    {
        if (owner == null || actions == null || actionIdField == null)
        {
            return;
        }

        string actionId = actionIdField.value != null ? actionIdField.value.Trim() : string.Empty;
        if (string.IsNullOrWhiteSpace(actionId))
        {
            SetStatus("추가할 액션 ID를 입력하세요.", MessageType.Warning);
            return;
        }

        RecordSequenceChange(owner, "시나리오 액션 삽입");
        ActionCatalogEntry entry = _catalog != null ? _catalog.FindById(actionId) : null;
        var action = new ScenarioActionData
        {
            BlockId = ScenarioBlockIdentity.Create(),
            ActionId = actionId,
            ParametersJson = ScenarioAuthoringParameterView.CreateDefaultParameterJson(entry)
        };
        actions.Add(action);
        _selectedSequence = owner;
        _selectedActionList = actions;
        _selectedActionIndex = actions.Count - 1;
        _selectedAction = action;
        _selectedActionObjectId = owner.SequenceId + ".actions[" + _selectedActionIndex + "]";
        actionIdField.value = string.Empty;
        RefreshAll();
    }

    private void MoveAction(
        ActionSequenceAsset owner,
        List<ScenarioActionData> actions,
        int index,
        int direction)
    {
        int target = index + direction;
        if (owner == null || actions == null || index < 0 || index >= actions.Count || target < 0 || target >= actions.Count)
        {
            return;
        }

        RecordSequenceChange(owner, "시나리오 액션 순서 변경");
        ScenarioActionData action = actions[index];
        actions[index] = actions[target];
        actions[target] = action;
        if (_selectedAction == action)
        {
            _selectedActionIndex = target;
            _selectedActionObjectId = owner.SequenceId + ".actions[" + target + "]";
        }

        RefreshAll();
    }

    private void DuplicateAction(
        ActionSequenceAsset owner,
        List<ScenarioActionData> actions,
        int index)
    {
        if (owner == null || actions == null || index < 0 || index >= actions.Count)
        {
            return;
        }

        RecordSequenceChange(owner, "시나리오 액션 복제");
        ScenarioActionData clone = CloneAction(actions[index]);
        actions.Insert(index + 1, clone);
        _selectedSequence = owner;
        _selectedActionList = actions;
        _selectedActionIndex = index + 1;
        _selectedAction = clone;
        _selectedActionObjectId = owner.SequenceId + ".actions[" + _selectedActionIndex + "]";
        RefreshAll();
    }

    private void ToggleAction(ActionSequenceAsset owner, ScenarioActionData action)
    {
        if (owner == null || action == null)
        {
            return;
        }

        RecordSequenceChange(owner, "시나리오 액션 활성 상태 변경");
        action.Disabled = !action.Disabled;
        RefreshAll();
    }

    private void DeleteAction(
        ActionSequenceAsset owner,
        List<ScenarioActionData> actions,
        int index)
    {
        if (owner == null || actions == null || index < 0 || index >= actions.Count)
        {
            return;
        }

        RecordSequenceChange(owner, "시나리오 액션 삭제");
        if (_selectedAction == actions[index])
        {
            _selectedAction = null;
            _selectedActionList = null;
            _selectedActionIndex = -1;
            _selectedActionObjectId = string.Empty;
        }

        actions.RemoveAt(index);
        RefreshAll();
    }

    private static ScenarioActionData CloneAction(ScenarioActionData source)
    {
        return ScenarioBlockIdentity.CloneWithNewIds(source);
    }

    private static void RecordSequenceChange(ActionSequenceAsset sequence, string undoName)
    {
        EditorUtility.SetDirty(sequence);
    }

    private static void MarkScenarioDirty(BattleScenarioData scenario)
    {
        if (scenario == null)
        {
            return;
        }

        EditorUtility.SetDirty(scenario);
        if (scenario.Sequences == null)
        {
            return;
        }

        for (int i = 0; i < scenario.Sequences.Count; i++)
        {
            if (scenario.Sequences[i] != null)
            {
                EditorUtility.SetDirty(scenario.Sequences[i]);
            }
        }
    }

    private static void DestroyTemporaryScenario(BattleScenarioData scenario)
    {
        if (scenario == null)
        {
            return;
        }

        if (scenario.Sequences != null)
        {
            for (int i = 0; i < scenario.Sequences.Count; i++)
            {
                if (scenario.Sequences[i] != null)
                {
                    DestroyImmediate(scenario.Sequences[i]);
                }
            }
        }

        DestroyImmediate(scenario);
    }

    private static void DestroyTemporaryStandaloneSequence(ActionSequenceAsset sequence)
    {
        if (sequence != null)
        {
            DestroyImmediate(sequence);
        }
    }

    private void ExportAs()
    {
        if (_scenario == null && _standaloneSequence == null)
        {
            return;
        }

        string defaultName = _standaloneSequence != null
            ? (string.IsNullOrWhiteSpace(_standaloneSequence.SequenceId)
                ? "action_sequence.sequence.yaml"
                : _standaloneSequence.SequenceId.Trim() + ".sequence.yaml")
            : (string.IsNullOrWhiteSpace(_scenario.ScenarioId)
                ? "battle_scenario.scenario.yaml"
                : _scenario.ScenarioId.Trim() + ".scenario.yaml");

        string path = EditorUtility.SaveFilePanelInProject(
            "시나리오 YAML 내보내기",
            defaultName,
            "yaml",
            "내보낼 Scenario Source YAML 경로를 선택하세요.");
        if (string.IsNullOrEmpty(path))
        {
            return;
        }

        if (_standaloneSequence != null)
        {
            ActionSequenceSourceExportResult standaloneResult = ActionSequenceSourceSync.ExportToFile(_standaloneSequence, path);
            _lastStandaloneExportResult = standaloneResult;
            _yamlPreviewField.value = standaloneResult.Text ?? string.Empty;
            _sourcePathField.value = path;
            SetValidationStatus(
                standaloneResult.Validation,
                standaloneResult.Success ? "선택한 경로로 독립 시퀀스 YAML을 내보냈습니다." : "독립 시퀀스 YAML 내보내기에 실패했습니다.");
            return;
        }

        ScenarioSourceYamlExportResult result = new ScenarioSourceYamlExportCommand().ExportToFile(_scenario, path);
        _lastExportResult = result;
        _yamlPreviewField.value = result.Text ?? string.Empty;
        _sourcePathField.value = path;
        SetValidationStatus(result.Validation, result.Success ? "선택한 경로로 YAML을 내보냈습니다." : "YAML 내보내기에 실패했습니다.");
    }

    private void SetValidationStatus(ScenarioValidationResult validation, string successMessage)
    {
        if (validation != null && validation.HasErrors)
        {
            SetStatus(FirstValidationMessage(validation), MessageType.Error);
            return;
        }

        SetStatus(successMessage, MessageType.Info);
    }

    private void SetStatus(string message, MessageType type)
    {
        if (_statusLabel == null)
        {
            return;
        }

        _statusLabel.text = message ?? string.Empty;
        switch (type)
        {
            case MessageType.Error:
                _statusLabel.style.color = new Color(1f, 0.45f, 0.45f);
                break;
            case MessageType.Warning:
                _statusLabel.style.color = new Color(1f, 0.78f, 0.35f);
                break;
            default:
                _statusLabel.style.color = new Color(0.72f, 0.86f, 1f);
                break;
        }
    }

    private string GetSourcePath()
    {
        if (_standaloneSequence != null && _standaloneSequence.Source != null)
        {
            return (_standaloneSequence.Source.SourcePath ?? string.Empty).Trim();
        }

        return _scenario != null && _scenario.Source != null
            ? (_scenario.Source.SourcePath ?? string.Empty).Trim()
            : string.Empty;
    }

    private static void ClearPanel(VisualElement panel)
    {
        if (panel == null)
        {
            return;
        }

        VisualElement title = panel.childCount > 0 ? panel[0] : null;
        panel.Clear();
        if (title != null)
        {
            panel.Add(title);
        }
    }

    private static void AddInfo(VisualElement parent, string label)
    {
        Label row = new Label(label);
        row.style.whiteSpace = WhiteSpace.Normal;
        row.style.marginBottom = 3;
        parent.Add(row);
    }

    private static void AddInfo(VisualElement parent, string label, string value)
    {
        Label row = new Label(label + ": " + value);
        row.style.whiteSpace = WhiteSpace.Normal;
        row.style.marginBottom = 3;
        parent.Add(row);
    }

    private static int CountActions(List<ScenarioActionData> actions)
    {
        if (actions == null)
        {
            return 0;
        }

        int count = 0;
        for (int i = 0; i < actions.Count; i++)
        {
            count++;
            if (actions[i] != null)
            {
                count += CountActions(actions[i].Children);
            }
        }

        return count;
    }

    private static string JoinIds(List<string> ids)
    {
        if (ids == null || ids.Count == 0)
        {
            return "-";
        }

        var trimmed = new List<string>();
        for (int i = 0; i < ids.Count; i++)
        {
            if (!string.IsNullOrWhiteSpace(ids[i]))
            {
                trimmed.Add(ids[i].Trim());
            }
        }

        return trimmed.Count == 0 ? "-" : string.Join(", ", trimmed.ToArray());
    }

    private static string FirstValidationMessage(ScenarioValidationResult validation)
    {
        if (validation == null || validation.Messages.Count == 0)
        {
            return "검증 메시지가 없습니다.";
        }

        ScenarioValidationMessage first = validation.Messages[0];
        return first.Code + ": " + first.Message;
    }

    private static string EmptyDash(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? "-" : value.Trim();
    }

    private static string ShortHash(string value)
    {
        string normalized = EmptyDash(value);
        return normalized.Length > 12 ? normalized.Substring(0, 12) : normalized;
    }

    private static string Indent(int depth)
    {
        return new string(' ', Mathf.Max(0, depth) * 2);
    }

    private static string FormatEvent(BattleEventType eventType)
    {
        switch (eventType)
        {
            case BattleEventType.EnemyHpCrossedBelow:
                return "적 HP 임계치";
            case BattleEventType.EnemyDefeated:
                return "적 처치";
            case BattleEventType.SkillCompleted:
                return "스킬 종료";
            case BattleEventType.GameModuleCompleted:
                return "모듈 종료";
            case BattleEventType.BattleStarted:
                return "전투 시작";
            default:
                return "이벤트 없음";
        }
    }

    private static string FormatTiming(BattleRuleTiming timing)
    {
        switch (timing)
        {
            case BattleRuleTiming.AfterCurrentAction:
                return "현재 액션 후";
            case BattleRuleTiming.AfterCurrentSkill:
                return "현재 스킬 후";
            case BattleRuleTiming.AfterCurrentModule:
                return "현재 모듈 후";
            default:
                return "즉시";
        }
    }

    private static string FormatOnce(BattleRuleOnceMode once)
    {
        switch (once)
        {
            case BattleRuleOnceMode.Always:
                return "반복 가능";
            case BattleRuleOnceMode.PerEncounterMemory:
                return "조우 기억당 1회";
            default:
                return "전투당 1회";
        }
    }

    private static string FormatSeverity(ScenarioValidationSeverity severity)
    {
        switch (severity)
        {
            case ScenarioValidationSeverity.Error:
                return "오류";
            case ScenarioValidationSeverity.Warning:
                return "주의";
            default:
                return "정보";
        }
    }
}

public static class ScenarioAuthoringCatalogView
{
    public static List<string> BuildActionPickerLabels(ActionCatalogAsset catalog)
    {
        var labels = new List<string>();
        if (catalog == null || catalog.Entries == null)
        {
            return labels;
        }

        for (int i = 0; i < catalog.Entries.Count; i++)
        {
            ActionCatalogEntry entry = catalog.Entries[i];
            if (entry == null || entry.Disabled || string.IsNullOrWhiteSpace(entry.ActionId))
            {
                continue;
            }

            string displayName = string.IsNullOrWhiteSpace(entry.DisplayNameKo)
                ? entry.ActionId.Trim()
                : entry.DisplayNameKo.Trim();
            labels.Add(displayName + " (" + entry.ActionId.Trim() + ")");
        }

        return labels;
    }

    public static string ResolveActionIdFromPickerLabel(string label)
    {
        if (string.IsNullOrWhiteSpace(label))
        {
            return string.Empty;
        }

        string trimmed = label.Trim();
        int open = trimmed.LastIndexOf('(');
        int close = trimmed.LastIndexOf(')');
        if (open >= 0 && close > open)
        {
            return trimmed.Substring(open + 1, close - open - 1).Trim();
        }

        return trimmed;
    }

    public static ScenarioValidationMessage FindMessageForObject(
        ScenarioValidationResult validation,
        string objectId)
    {
        if (validation == null || validation.Messages == null || string.IsNullOrWhiteSpace(objectId))
        {
            return null;
        }

        ScenarioValidationMessage fallback = null;
        for (int i = 0; i < validation.Messages.Count; i++)
        {
            ScenarioValidationMessage message = validation.Messages[i];
            if (message == null || message.ObjectId != objectId)
            {
                continue;
            }

            if (message.Severity == ScenarioValidationSeverity.Error)
            {
                return message;
            }

            if (fallback == null)
            {
                fallback = message;
            }
        }

        return fallback;
    }
}

public static class ScenarioAuthoringParameterView
{
    public static List<string> GetParameterNames(
        ScenarioActionData action,
        ActionCatalogEntry entry)
    {
        var names = new List<string>();
        if (entry != null && entry.Parameters != null)
        {
            for (int i = 0; i < entry.Parameters.Count; i++)
            {
                ActionCatalogParameter parameter = entry.Parameters[i];
                if (parameter == null || string.IsNullOrWhiteSpace(parameter.Name))
                {
                    continue;
                }

                AddUnique(names, parameter.Name.Trim());
            }
        }

        JObject json = ParseOrNew(action);
        foreach (JProperty property in json.Properties())
        {
            AddUnique(names, property.Name);
        }

        return names;
    }

    public static ActionCatalogParameter FindParameter(
        ActionCatalogEntry entry,
        string parameterName)
    {
        if (entry == null || entry.Parameters == null || string.IsNullOrWhiteSpace(parameterName))
        {
            return null;
        }

        for (int i = 0; i < entry.Parameters.Count; i++)
        {
            ActionCatalogParameter parameter = entry.Parameters[i];
            if (parameter != null && parameter.Name == parameterName)
            {
                return parameter;
            }
        }

        return null;
    }

    public static string GetParameterValue(
        ScenarioActionData action,
        string parameterName)
    {
        if (string.IsNullOrWhiteSpace(parameterName))
        {
            return string.Empty;
        }

        JObject json = ParseOrNew(action);
        JToken token;
        if (!json.TryGetValue(parameterName, out token) || token == null || token.Type == JTokenType.Null)
        {
            return string.Empty;
        }

        if (token.Type == JTokenType.String)
        {
            return token.Value<string>() ?? string.Empty;
        }

        return token.ToString(Formatting.None);
    }

    public static bool SetParameterValue(
        ScenarioActionData action,
        string parameterName,
        string rawValue,
        ActionCatalogParameter parameter,
        out string error)
    {
        error = string.Empty;
        if (action == null)
        {
            error = "액션이 없습니다.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(parameterName))
        {
            error = "파라미터 이름이 없습니다.";
            return false;
        }

        JObject json = ParseOrNew(action);
        JToken value;
        if (!TryCreateToken(rawValue, parameter, out value, out error))
        {
            return false;
        }

        json[parameterName.Trim()] = value;
        action.ParametersJson = json.ToString(Formatting.None);
        return true;
    }

    public static bool TrySetRawJson(
        ScenarioActionData action,
        string rawJson,
        out string error)
    {
        error = string.Empty;
        if (action == null)
        {
            error = "액션이 없습니다.";
            return false;
        }

        try
        {
            JObject json = string.IsNullOrWhiteSpace(rawJson)
                ? new JObject()
                : JObject.Parse(rawJson);
            action.ParametersJson = json.ToString(Formatting.None);
            return true;
        }
        catch (Exception exception)
        {
            error = exception.Message;
            return false;
        }
    }

    public static string FormatJson(ScenarioActionData action)
    {
        return ParseOrNew(action).ToString(Formatting.Indented);
    }

    public static string CreateDefaultParameterJson(ActionCatalogEntry entry)
    {
        var json = new JObject();
        if (entry == null || entry.Parameters == null)
        {
            return "{}";
        }

        for (int i = 0; i < entry.Parameters.Count; i++)
        {
            ActionCatalogParameter parameter = entry.Parameters[i];
            if (parameter == null || string.IsNullOrWhiteSpace(parameter.Name))
            {
                continue;
            }

            if (string.IsNullOrWhiteSpace(parameter.DefaultValue))
            {
                continue;
            }

            string error;
            JToken value;
            if (TryCreateToken(parameter.DefaultValue, parameter, out value, out error))
            {
                json[parameter.Name.Trim()] = value;
            }
        }

        return json.ToString(Formatting.None);
    }

    private static bool TryCreateToken(
        string rawValue,
        ActionCatalogParameter parameter,
        out JToken value,
        out string error)
    {
        error = string.Empty;
        string type = parameter != null && !string.IsNullOrWhiteSpace(parameter.Type)
            ? parameter.Type.Trim().ToLowerInvariant()
            : string.Empty;
        string normalized = rawValue ?? string.Empty;

        if (type.Contains("int"))
        {
            int intValue;
            if (!int.TryParse(normalized, out intValue))
            {
                error = "정수 값이어야 합니다.";
                value = JValue.CreateNull();
                return false;
            }

            value = new JValue(intValue);
            return true;
        }

        if (type.Contains("float") || type.Contains("number"))
        {
            float floatValue;
            if (!float.TryParse(normalized, NumberStyles.Float, CultureInfo.InvariantCulture, out floatValue))
            {
                error = "숫자 값이어야 합니다.";
                value = JValue.CreateNull();
                return false;
            }

            value = new JValue(floatValue);
            return true;
        }

        if (type.Contains("bool"))
        {
            bool boolValue;
            if (!bool.TryParse(normalized, out boolValue))
            {
                error = "true 또는 false 값이어야 합니다.";
                value = JValue.CreateNull();
                return false;
            }

            value = new JValue(boolValue);
            return true;
        }

        if (type.Contains("[]"))
        {
            var array = new JArray();
            string[] parts = normalized.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < parts.Length; i++)
            {
                array.Add(parts[i].Trim());
            }

            value = array;
            return true;
        }

        value = new JValue(normalized);
        return true;
    }

    private static JObject ParseOrNew(ScenarioActionData action)
    {
        if (action == null || string.IsNullOrWhiteSpace(action.ParametersJson))
        {
            return new JObject();
        }

        try
        {
            return JObject.Parse(action.ParametersJson);
        }
        catch
        {
            return new JObject();
        }
    }

    private static void AddUnique(List<string> names, string name)
    {
        if (string.IsNullOrWhiteSpace(name) || names.Contains(name))
        {
            return;
        }

        names.Add(name);
    }
}

public static class ScenarioSourceMetadataEditorSync
{
    public static bool ApplyExportResult(
        BattleScenarioData scenario,
        ScenarioSourceYamlExportResult result,
        DateTime writtenAtUtc)
    {
        if (scenario == null || result == null || !result.Success)
        {
            return false;
        }

        DateTime normalized = writtenAtUtc.Kind == DateTimeKind.Utc
            ? writtenAtUtc
            : writtenAtUtc.ToUniversalTime();
        string sourcePath = string.IsNullOrWhiteSpace(result.TargetPath)
            ? (scenario.Source != null ? scenario.Source.SourcePath : string.Empty)
            : result.TargetPath.Trim().Replace('\\', '/');
        string sourceHash = ScenarioSourceHash.Compute(result.Text ?? string.Empty);
        string importedAt = normalized.ToString("O");

        ApplyMetadata(scenario.Source ?? (scenario.Source = new ScenarioSourceMetadata()), sourcePath, sourceHash, importedAt);
        if (scenario.Sequences != null)
        {
            for (int i = 0; i < scenario.Sequences.Count; i++)
            {
                ActionSequenceAsset sequence = scenario.Sequences[i];
                if (sequence != null)
                {
                    ApplyMetadata(sequence.Source ?? (sequence.Source = new ScenarioSourceMetadata()), sourcePath, sourceHash, importedAt);
                }
            }
        }

        return true;
    }

    private static void ApplyMetadata(
        ScenarioSourceMetadata metadata,
        string sourcePath,
        string sourceHash,
        string importedAt)
    {
        metadata.SourcePath = sourcePath ?? string.Empty;
        metadata.SourceHash = sourceHash ?? string.Empty;
        metadata.ImportedAtIso8601 = importedAt ?? string.Empty;
    }
}
