using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine.UIElements;

public sealed class ActionInspectorView : VisualElement
{
    private ScenarioActionData _action;
    private ActionCatalogEntry _entry;
    private SequenceEditCommandStack _commands;
    private ParameterFieldContext _fieldContext;

    public ActionInspectorView()
    {
        AddToClassList("sm-action-inspector");
    }

    public event Action ReplaceRequested;
    public event Action EditApplied;
    public event Action<string> Error;

    public void Bind(
        ScenarioActionData action,
        ActionCatalogEntry entry,
        SequenceEditCommandStack commands,
        ParameterFieldContext fieldContext,
        ScenarioValidationResult validation)
    {
        _action = action;
        _entry = entry;
        _commands = commands;
        _fieldContext = fieldContext ?? new ParameterFieldContext();
        Render(validation);
    }

    private void Render(ScenarioValidationResult validation)
    {
        Clear();
        if (_action == null)
        {
            Add(Empty("블록을 선택하면 속성이 표시됩니다."));
            return;
        }

        AddActionOverview();
        AddValidation(validation);
        AddInstanceFields();
        AddParameterFields();
        AddDeveloperSection();
    }

    private void AddActionOverview()
    {
        var header = new VisualElement();
        header.AddToClassList("sm-inspector-action-header");
        var titleGroup = new VisualElement();
        titleGroup.style.flexGrow = 1f;
        var title = new Label(DisplayName());
        title.AddToClassList("sm-inspector-action-title");
        titleGroup.Add(title);
        var id = new Label(_action.ActionId ?? string.Empty);
        id.AddToClassList("sm-inspector-action-id");
        titleGroup.Add(id);
        header.Add(titleGroup);
        var replace = new Button(() => ReplaceRequested?.Invoke())
        {
            text = "교체",
            tooltip = "Action Library에서 다른 액션 선택"
        };
        replace.AddToClassList("sm-small-command");
        header.Add(replace);
        Add(header);

        var badges = new VisualElement();
        badges.AddToClassList("sm-inspector-badges");
        if (_entry != null)
        {
            badges.Add(Badge(CategoryName(_entry.Category), "sm-badge--category"));
            badges.Add(Badge(PreviewName(_entry.PreviewSupport), "sm-badge--preview"));
            if (_entry.Deprecated)
            {
                badges.Add(Badge("호환용", "sm-badge--warning"));
            }
        }
        else
        {
            badges.Add(Badge("카탈로그 미등록", "sm-badge--error"));
        }
        Add(badges);

        if (_entry == null)
        {
            Add(Notice("이 Action ID는 현재 Action Library에 없습니다. 교체하거나 고급 JSON을 확인하세요.", true));
            return;
        }

        if (!string.IsNullOrWhiteSpace(_entry.DescriptionKo))
        {
            var description = new Label(_entry.DescriptionKo.Trim());
            description.AddToClassList("sm-inspector-description");
            Add(description);
        }
        if (!string.IsNullOrWhiteSpace(_entry.UsageKo))
        {
            var usage = new VisualElement();
            usage.AddToClassList("sm-inspector-usage");
            var usageLabel = new Label("언제 사용");
            usageLabel.AddToClassList("sm-inspector-usage-label");
            usage.Add(usageLabel);
            usage.Add(new Label(_entry.UsageKo.Trim()));
            Add(usage);
        }
    }

    private void AddValidation(ScenarioValidationResult validation)
    {
        if (validation?.Messages == null)
        {
            return;
        }

        for (int i = 0; i < validation.Messages.Count; i++)
        {
            ScenarioValidationMessage message = validation.Messages[i];
            if (message == null || message.ObjectId != _action.BlockId)
            {
                continue;
            }

            bool isError = message.Severity == ScenarioValidationSeverity.Error;
            VisualElement notice = Notice(message.Message, isError);
            notice.tooltip = message.Code;
            Add(notice);
        }
    }

    private void AddInstanceFields()
    {
        Add(SectionTitle("이 블록"));
        var active = new Toggle("실행") { value = !_action.Disabled };
        active.tooltip = "끄면 시퀀스에 남아 있지만 실행하지 않습니다.";
        active.AddToClassList("sm-inspector-toggle");
        active.RegisterValueChangedCallback(evt =>
            Execute(SequenceEditCommands.SetEnabled(_action.BlockId, evt.newValue)));
        Add(active);

        var label = new TextField("블록 이름")
        {
            value = _action.DesignerLabel ?? string.Empty,
            isDelayed = true
        };
        label.tooltip = "이 시퀀스 안에서만 보이는 선택적 이름";
        label.RegisterValueChangedCallback(evt =>
        {
            string next = evt.newValue ?? string.Empty;
            if (next != (_action.DesignerLabel ?? string.Empty))
            {
                Execute(SequenceEditCommands.SetDesignerLabel(_action.BlockId, next));
            }
        });
        Add(label);

        var note = new TextField("메모")
        {
            value = _action.Note ?? string.Empty,
            multiline = true,
            isDelayed = true
        };
        note.AddToClassList("sm-inspector-note");
        note.tooltip = "이 블록의 연출 의도나 확인할 내용을 기록";
        note.RegisterValueChangedCallback(evt =>
        {
            string next = evt.newValue ?? string.Empty;
            if (next != (_action.Note ?? string.Empty))
            {
                Execute(SequenceEditCommands.SetNote(_action.BlockId, next));
            }
        });
        Add(note);
    }

    private void AddParameterFields()
    {
        Add(SectionTitle("값"));
        if (!TryParameters(out JObject parameters, out string error))
        {
            Add(Notice("파라미터 JSON을 읽을 수 없습니다: " + error, true));
            return;
        }

        var names = new HashSet<string>(StringComparer.Ordinal);
        if (_entry?.Parameters != null)
        {
            for (int i = 0; i < _entry.Parameters.Count; i++)
            {
                ActionCatalogParameter parameter = _entry.Parameters[i];
                if (parameter == null || string.IsNullOrWhiteSpace(parameter.Name))
                {
                    continue;
                }

                names.Add(parameter.Name);
                parameters.TryGetValue(parameter.Name, out JToken value);
                Add(ParameterFieldFactory.Create(
                    parameter,
                    value,
                    _fieldContext,
                    next => SetParameter(parameter.Name, next)));
            }
        }

        foreach (JProperty property in parameters.Properties())
        {
            if (names.Contains(property.Name))
            {
                continue;
            }

            ActionCatalogParameter fallback = FallbackParameter(property);
            VisualElement editor = ParameterFieldFactory.Create(
                fallback,
                property.Value,
                _fieldContext,
                next => SetParameter(property.Name, next));
            editor.AddToClassList("is-uncataloged");
            editor.tooltip = "Action Library에 정의되지 않은 기존 파라미터입니다.";
            Add(editor);
        }

        if ((_entry?.Parameters == null || _entry.Parameters.Count == 0)
            && !parameters.HasValues)
        {
            Add(Empty("이 액션에는 설정할 값이 없습니다."));
        }
    }

    private void AddDeveloperSection()
    {
        var foldout = new Foldout { text = "개발자 정보", value = false };
        foldout.AddToClassList("sm-developer-foldout");

        foldout.Add(ReadOnlyField("Action ID", _action.ActionId));
        var blockRow = new VisualElement();
        blockRow.AddToClassList("sm-copy-row");
        blockRow.Add(ReadOnlyField("Block ID", _action.BlockId));
        var copy = new Button(() => EditorGUIUtility.systemCopyBuffer = _action.BlockId ?? string.Empty)
        {
            text = "복사",
            tooltip = "Block ID 복사"
        };
        copy.AddToClassList("sm-small-command");
        blockRow.Add(copy);
        foldout.Add(blockRow);

        var raw = new TextField("Raw JSON")
        {
            value = FormattedParameters(),
            multiline = true,
            isDelayed = false
        };
        raw.AddToClassList("sm-raw-json");
        foldout.Add(raw);
        var apply = new Button(() => ApplyRawJson(raw)) { text = "JSON 적용" };
        apply.AddToClassList("sm-small-command");
        foldout.Add(apply);

        if (_entry != null && !string.IsNullOrWhiteSpace(_entry.ExampleYaml))
        {
            var exampleTitle = new Label("YAML 예시");
            exampleTitle.AddToClassList("sm-mini-label");
            foldout.Add(exampleTitle);
            var example = new TextField
            {
                value = _entry.ExampleYaml,
                multiline = true,
                isReadOnly = true
            };
            example.AddToClassList("sm-example-yaml");
            foldout.Add(example);
        }

        Add(foldout);
    }

    private void SetParameter(string name, JToken value)
    {
        if (!TryParameters(out JObject parameters, out string error))
        {
            Error?.Invoke(error);
            return;
        }

        JToken current = parameters[name];
        if (JToken.DeepEquals(current, value))
        {
            return;
        }

        if (value == null || value.Type == JTokenType.Null)
        {
            parameters.Remove(name);
        }
        else
        {
            parameters[name] = value.DeepClone();
        }
        Execute(SequenceEditCommands.SetParameters(
            _action.BlockId,
            parameters.ToString(Formatting.None)));
    }

    private void ApplyRawJson(TextField raw)
    {
        try
        {
            JObject parsed = string.IsNullOrWhiteSpace(raw.value)
                ? new JObject()
                : JObject.Parse(raw.value);
            raw.RemoveFromClassList("is-invalid");
            Execute(SequenceEditCommands.SetParameters(
                _action.BlockId,
                parsed.ToString(Formatting.None)));
        }
        catch (Exception exception)
        {
            raw.AddToClassList("is-invalid");
            Error?.Invoke("JSON 적용 실패: " + exception.Message);
        }
    }

    private void Execute(ISequenceEditCommand command)
    {
        if (_commands == null || command == null)
        {
            Error?.Invoke("현재 시퀀스를 편집할 수 없습니다.");
            return;
        }

        try
        {
            _commands.Execute(command);
            EditApplied?.Invoke();
        }
        catch (Exception exception)
        {
            Error?.Invoke(exception.Message);
        }
    }

    private bool TryParameters(out JObject parameters, out string error)
    {
        try
        {
            parameters = string.IsNullOrWhiteSpace(_action?.ParametersJson)
                ? new JObject()
                : JObject.Parse(_action.ParametersJson);
            error = string.Empty;
            return true;
        }
        catch (Exception exception)
        {
            parameters = new JObject();
            error = exception.Message;
            return false;
        }
    }

    private string FormattedParameters()
    {
        return TryParameters(out JObject parameters, out _)
            ? parameters.ToString(Formatting.Indented)
            : _action.ParametersJson ?? string.Empty;
    }

    private string DisplayName()
    {
        return !string.IsNullOrWhiteSpace(_entry?.DisplayNameKo)
            ? _entry.DisplayNameKo.Trim()
            : (!string.IsNullOrWhiteSpace(_action?.ActionId) ? _action.ActionId : "액션");
    }

    private static ActionCatalogParameter FallbackParameter(JProperty property)
    {
        var parameter = new ActionCatalogParameter
        {
            Name = property.Name,
            DisplayNameKo = property.Name,
            DescriptionKo = "카탈로그에 정의되지 않은 기존 값"
        };
        switch (property.Value.Type)
        {
            case JTokenType.Boolean:
                parameter.Type = "bool";
                parameter.EditorControlId = "toggle";
                break;
            case JTokenType.Integer:
                parameter.Type = "int";
                parameter.EditorControlId = "number";
                break;
            case JTokenType.Float:
                parameter.Type = "number";
                parameter.EditorControlId = "number";
                break;
            case JTokenType.Object:
                parameter.Type = "object";
                parameter.EditorControlId = "json";
                break;
            case JTokenType.Array:
                parameter.Type = "string[]";
                parameter.EditorControlId = "list";
                break;
            default:
                parameter.Type = "string";
                parameter.EditorControlId = "text";
                break;
        }
        return parameter;
    }

    private static Label SectionTitle(string text)
    {
        var title = new Label(text ?? string.Empty);
        title.AddToClassList("sm-inspector-section-title");
        return title;
    }

    private static Label Badge(string text, string modifier)
    {
        var badge = new Label(text ?? string.Empty);
        badge.AddToClassList("sm-inspector-badge");
        if (!string.IsNullOrWhiteSpace(modifier))
        {
            badge.AddToClassList(modifier);
        }
        return badge;
    }

    private static VisualElement Notice(string text, bool error)
    {
        var notice = new VisualElement();
        notice.AddToClassList("sm-inspector-notice");
        notice.AddToClassList(error ? "is-error" : "is-info");
        notice.Add(new Label(text ?? string.Empty));
        return notice;
    }

    private static Label Empty(string text)
    {
        var label = new Label(text ?? string.Empty);
        label.AddToClassList("sm-inspector-empty");
        return label;
    }

    private static TextField ReadOnlyField(string label, string value)
    {
        return new TextField(label)
        {
            value = value ?? string.Empty,
            isReadOnly = true
        };
    }

    private static string PreviewName(ActionPreviewSupport support)
    {
        switch (support)
        {
            case ActionPreviewSupport.SafePreview: return "안전 미리보기";
            case ActionPreviewSupport.LiveOnly: return "Play Mode만";
            default: return "미리보기 미지원";
        }
    }

    private static string CategoryName(string category)
    {
        switch ((category ?? string.Empty).Trim().ToLowerInvariant())
        {
            case "flow": return "흐름";
            case "dialogue": return "대화";
            case "screen": return "화면";
            case "audio": return "오디오";
            case "module": return "게임 모듈";
            case "actor": return "캐릭터";
            case "battle": return "전투";
            case "camera": return "카메라";
            case "cinematic": return "시네마틱";
            case "timeline": return "타임라인";
            default: return string.IsNullOrWhiteSpace(category) ? "기타" : category;
        }
    }
}
