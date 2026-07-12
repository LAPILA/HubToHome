using System;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

public sealed class SequenceProblemsView : VisualElement
{
    private ScenarioValidationResult _validation;
    private readonly ToolbarSearchField _search;
    private readonly ToolbarToggle _errors;
    private readonly ToolbarToggle _warnings;
    private readonly ToolbarToggle _info;
    private readonly ScrollView _list;

    public SequenceProblemsView()
    {
        AddToClassList("sm-problems-view");
        var toolbar = new Toolbar();
        toolbar.AddToClassList("sm-problems-toolbar");
        _errors = Filter("오류", true);
        _warnings = Filter("경고", true);
        _info = Filter("정보", true);
        toolbar.Add(_errors);
        toolbar.Add(_warnings);
        toolbar.Add(_info);
        _search = new ToolbarSearchField();
        _search.tooltip = "메시지, 코드, 대상 ID 검색";
        _search.RegisterValueChangedCallback(_ => Render());
        toolbar.Add(_search);
        Add(toolbar);

        _list = new ScrollView(ScrollViewMode.Vertical);
        _list.style.flexGrow = 1f;
        Add(_list);
    }

    public event Action<ScenarioValidationMessage> ProblemSelected;

    public void Bind(ScenarioValidationResult validation)
    {
        _validation = validation ?? new ScenarioValidationResult();
        UpdateLabels();
        Render();
    }

    private ToolbarToggle Filter(string label, bool value)
    {
        var toggle = new ToolbarToggle { text = label, value = value };
        toggle.RegisterValueChangedCallback(_ => Render());
        return toggle;
    }

    private void UpdateLabels()
    {
        int errors = 0;
        int warnings = 0;
        int infos = 0;
        for (int i = 0; i < _validation.Messages.Count; i++)
        {
            switch (_validation.Messages[i].Severity)
            {
                case ScenarioValidationSeverity.Error: errors++; break;
                case ScenarioValidationSeverity.Warning: warnings++; break;
                default: infos++; break;
            }
        }
        _errors.text = "오류 " + errors;
        _warnings.text = "경고 " + warnings;
        _info.text = "정보 " + infos;
    }

    private void Render()
    {
        _list.Clear();
        string query = (_search.value ?? string.Empty).Trim();
        int visible = 0;
        for (int i = 0; i < (_validation?.Messages.Count ?? 0); i++)
        {
            ScenarioValidationMessage message = _validation.Messages[i];
            if (!IsSeverityVisible(message.Severity) || !Matches(message, query))
            {
                continue;
            }
            _list.Add(CreateRow(message));
            visible++;
        }
        if (visible == 0)
        {
            var empty = new Label((_validation?.Messages.Count ?? 0) == 0
                ? "문제 없음"
                : "현재 필터에 맞는 문제 없음");
            empty.AddToClassList("sm-empty-copy");
            _list.Add(empty);
        }
    }

    private VisualElement CreateRow(ScenarioValidationMessage message)
    {
        var row = new VisualElement();
        row.AddToClassList("sm-problem-row");
        row.AddToClassList("is-" + message.Severity.ToString().ToLowerInvariant());
        var main = new Button(() => ProblemSelected?.Invoke(message));
        main.AddToClassList("sm-problem-main");
        var heading = new VisualElement();
        heading.AddToClassList("sm-problem-heading");
        var severity = new Label(SeverityLabel(message.Severity));
        severity.AddToClassList("sm-problem-severity");
        heading.Add(severity);
        var code = new Label(message.Code ?? string.Empty);
        code.AddToClassList("sm-problem-code");
        heading.Add(code);
        main.Add(heading);
        var copy = new Label(message.Message ?? string.Empty);
        copy.AddToClassList("sm-problem-copy");
        main.Add(copy);
        if (!string.IsNullOrWhiteSpace(message.ObjectId))
        {
            var target = new Label("대상  " + message.ObjectId);
            target.AddToClassList("sm-problem-target");
            main.Add(target);
        }
        row.Add(main);
        var copyButton = new Button(() =>
        {
            EditorGUIUtility.systemCopyBuffer = (message.Code ?? string.Empty)
                + "\n" + (message.Message ?? string.Empty)
                + (string.IsNullOrWhiteSpace(message.ObjectId)
                    ? string.Empty
                    : "\n" + message.ObjectId);
        }) { text = "복사", tooltip = "문제 내용 복사" };
        copyButton.AddToClassList("sm-small-command");
        row.Add(copyButton);
        return row;
    }

    private bool IsSeverityVisible(ScenarioValidationSeverity severity)
    {
        switch (severity)
        {
            case ScenarioValidationSeverity.Error: return _errors.value;
            case ScenarioValidationSeverity.Warning: return _warnings.value;
            default: return _info.value;
        }
    }

    private static bool Matches(ScenarioValidationMessage message, string query)
    {
        if (string.IsNullOrEmpty(query))
        {
            return true;
        }
        return Contains(message.Message, query)
            || Contains(message.Code, query)
            || Contains(message.ObjectId, query);
    }

    private static bool Contains(string value, string query)
    {
        return (value ?? string.Empty).IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static string SeverityLabel(ScenarioValidationSeverity severity)
    {
        switch (severity)
        {
            case ScenarioValidationSeverity.Error: return "오류";
            case ScenarioValidationSeverity.Warning: return "경고";
            default: return "정보";
        }
    }
}
