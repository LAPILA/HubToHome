using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

public sealed class ScenarioAuthoringWindow : EditorWindow
{
    private const string WindowTitle = "시나리오 저작";

    private ObjectField _scenarioField;
    private ObjectField _catalogField;
    private TextField _sourcePathField;
    private Label _statusLabel;
    private VisualElement _overviewPanel;
    private VisualElement _rulesPanel;
    private VisualElement _sequencesPanel;
    private VisualElement _validationPanel;
    private TextField _yamlPreviewField;
    private Button _refreshButton;
    private Button _validateSourceButton;
    private Button _exportSourceButton;
    private Button _exportAsButton;

    private BattleScenarioData _scenario;
    private ActionCatalogAsset _catalog;
    private ScenarioSourceYamlExportResult _lastExportResult;

    [MenuItem("HubToHome/시나리오/시나리오 저작 창")]
    public static void Open()
    {
        ScenarioAuthoringWindow window = GetWindow<ScenarioAuthoringWindow>();
        window.titleContent = new GUIContent(WindowTitle);
        window.minSize = new Vector2(760f, 560f);
        window.Show();
    }

    public void CreateGUI()
    {
        BuildLayout();
        SetScenario(Selection.activeObject as BattleScenarioData);
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

        Label title = new Label("시나리오 저작");
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

        _refreshButton = new Button(RefreshAll) { text = "새로고침" };
        _refreshButton.style.marginLeft = 8;
        toolbar.Add(_refreshButton);

        _validateSourceButton = new Button(ValidateSourcePath) { text = "원본 YAML 검증" };
        _validateSourceButton.style.marginLeft = 4;
        toolbar.Add(_validateSourceButton);

        _exportSourceButton = new Button(ExportToSourcePath) { text = "원본 경로로 내보내기" };
        _exportSourceButton.style.marginLeft = 4;
        toolbar.Add(_exportSourceButton);

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

        var split = new TwoPaneSplitView(0, 360, TwoPaneSplitViewOrientation.Horizontal);
        split.style.flexGrow = 1;
        root.Add(split);

        ScrollView summaryScroll = new ScrollView();
        summaryScroll.style.flexGrow = 1;
        summaryScroll.style.paddingRight = 8;
        split.Add(summaryScroll);

        _overviewPanel = MakeSection(summaryScroll, "개요");
        _rulesPanel = MakeSection(summaryScroll, "규칙");
        _sequencesPanel = MakeSection(summaryScroll, "시퀀스");
        _validationPanel = MakeSection(summaryScroll, "동기화 / 검증");

        VisualElement yamlPanel = new VisualElement();
        yamlPanel.style.flexDirection = FlexDirection.Column;
        yamlPanel.style.flexGrow = 1;
        split.Add(yamlPanel);

        Label yamlTitle = new Label("YAML 미리보기");
        yamlTitle.style.unityFontStyleAndWeight = FontStyle.Bold;
        yamlTitle.style.marginBottom = 6;
        yamlPanel.Add(yamlTitle);

        _yamlPreviewField = new TextField();
        _yamlPreviewField.multiline = true;
        _yamlPreviewField.isReadOnly = true;
        _yamlPreviewField.style.flexGrow = 1;
        _yamlPreviewField.style.whiteSpace = WhiteSpace.Normal;
        yamlPanel.Add(_yamlPreviewField);
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
        bool hasScenario = _scenario != null;
        _refreshButton?.SetEnabled(hasScenario);
        _exportAsButton?.SetEnabled(hasScenario);
        _validateSourceButton?.SetEnabled(hasScenario && !string.IsNullOrWhiteSpace(GetSourcePath()));
        _exportSourceButton?.SetEnabled(hasScenario && !string.IsNullOrWhiteSpace(GetSourcePath()));
    }

    private void RefreshSummary()
    {
        ClearPanel(_overviewPanel);
        ClearPanel(_rulesPanel);
        ClearPanel(_sequencesPanel);
        ClearPanel(_validationPanel);

        if (_scenario == null)
        {
            AddInfo(_overviewPanel, "Battle Scenario Data를 선택하세요.");
            AddInfo(_validationPanel, "검증할 시나리오가 없습니다.");
            _sourcePathField.value = string.Empty;
            SetStatus("시나리오 에셋을 선택하면 개요와 YAML 미리보기가 표시됩니다.", MessageType.Info);
            return;
        }

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
        RenderSyncAndValidation();
    }

    private void RefreshYamlPreview()
    {
        if (_scenario == null)
        {
            _lastExportResult = null;
            _yamlPreviewField.value = string.Empty;
            return;
        }

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

            AddInfo(_sequencesPanel, title, CountActions(sequence.Actions) + "개 액션");
            AddSequenceControls(_sequencesPanel, sequence);
            AddActionRows(_sequencesPanel, sequence, sequence.Actions, 0);
        }
    }

    private void RenderSyncAndValidation()
    {
        AddInfo(_validationPanel, "Source", GetSourceSyncStatus());

        if (_lastExportResult != null && _lastExportResult.Validation != null)
        {
            AddValidationRows(_validationPanel, "YAML Export", _lastExportResult.Validation);
        }

        if (_catalog == null)
        {
            AddInfo(_validationPanel, "Action Catalog", "선택되지 않아 카탈로그 기반 검증을 생략했습니다.");
            return;
        }

        ScenarioValidationResult catalogValidation = ScenarioCatalogValidator.ValidateBattleScenario(_scenario, _catalog);
        AddValidationRows(_validationPanel, "Catalog", catalogValidation);
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

            string disabled = action.Disabled ? "비활성 / " : string.Empty;
            VisualElement row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.marginBottom = 3;
            row.style.marginLeft = 8 + depth * 16;
            panel.Add(row);

            Label label = new Label("- " + disabled + EmptyDash(action.ActionId));
            label.style.whiteSpace = WhiteSpace.Normal;
            label.style.flexGrow = 1;
            row.Add(label);

            int index = i;
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
                AddActionRows(panel, owner, action.Children, depth + 1);
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

    private void ExportToSourcePath()
    {
        ScenarioSourceYamlExportResult result = new ScenarioSourceYamlExportCommand().ExportToSourcePath(_scenario);
        _lastExportResult = result;
        _yamlPreviewField.value = result.Text ?? string.Empty;
        SetValidationStatus(result.Validation, result.Success ? "Source YAML 경로로 내보냈습니다." : "Source YAML 내보내기에 실패했습니다.");
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
        actions.Add(new ScenarioActionData
        {
            ActionId = actionId,
            ParametersJson = "{}"
        });
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
        actions.Insert(index + 1, CloneAction(actions[index]));
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
        actions.RemoveAt(index);
        RefreshAll();
    }

    private static ScenarioActionData CloneAction(ScenarioActionData source)
    {
        if (source == null)
        {
            return null;
        }

        var clone = new ScenarioActionData
        {
            ActionId = source.ActionId,
            ParametersJson = source.ParametersJson,
            Disabled = source.Disabled
        };

        if (source.Children != null)
        {
            for (int i = 0; i < source.Children.Count; i++)
            {
                clone.Children.Add(CloneAction(source.Children[i]));
            }
        }

        return clone;
    }

    private static void RecordSequenceChange(ActionSequenceAsset sequence, string undoName)
    {
        Undo.RecordObject(sequence, undoName);
        EditorUtility.SetDirty(sequence);
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

    private void ExportAs()
    {
        if (_scenario == null)
        {
            return;
        }

        string defaultName = string.IsNullOrWhiteSpace(_scenario.ScenarioId)
            ? "battle_scenario.scenario.yaml"
            : _scenario.ScenarioId.Trim() + ".scenario.yaml";

        string path = EditorUtility.SaveFilePanelInProject(
            "시나리오 YAML 내보내기",
            defaultName,
            "yaml",
            "내보낼 Scenario Source YAML 경로를 선택하세요.");
        if (string.IsNullOrEmpty(path))
        {
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
