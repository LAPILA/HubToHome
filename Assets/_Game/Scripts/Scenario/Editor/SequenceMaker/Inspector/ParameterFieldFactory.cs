using System;
using System.Collections.Generic;
using System.Globalization;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

public enum ParameterEditorKind
{
    Text,
    Number,
    Integer,
    Duration,
    Toggle,
    Enum,
    Color,
    Vector2,
    Vector3,
    Reference,
    Json,
    StringList
}

public sealed class ParameterFieldContext
{
    private readonly Dictionary<string, List<string>> _referenceOptions =
        new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, List<string>> _bindingOptions =
        new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

    public void AddReferenceOptions(string controlId, IEnumerable<string> values)
    {
        AddOptions(_referenceOptions, controlId, values);
    }

    public void AddBindingOptions(string sourceId, IEnumerable<string> values)
    {
        AddOptions(_bindingOptions, sourceId, values);
    }

    public IReadOnlyList<string> GetReferenceOptions(ActionCatalogParameter parameter)
    {
        string key = ParameterFieldFactory.ControlKey(parameter);
        return _referenceOptions.TryGetValue(key, out List<string> values)
            ? values
            : Array.Empty<string>();
    }

    public IReadOnlyList<string> GetBindingOptions(string sourceId)
    {
        return _bindingOptions.TryGetValue(Normalize(sourceId), out List<string> values)
            ? values
            : Array.Empty<string>();
    }

    private static void AddOptions(
        IDictionary<string, List<string>> destination,
        string key,
        IEnumerable<string> values)
    {
        string normalizedKey = Normalize(key);
        if (string.IsNullOrEmpty(normalizedKey))
        {
            return;
        }

        if (!destination.TryGetValue(normalizedKey, out List<string> list))
        {
            list = new List<string>();
            destination[normalizedKey] = list;
        }

        if (values == null)
        {
            return;
        }

        foreach (string value in values)
        {
            string normalized = Normalize(value);
            if (!string.IsNullOrEmpty(normalized) && !list.Contains(normalized))
            {
                list.Add(normalized);
            }
        }
    }

    private static string Normalize(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
    }
}

public sealed class ReferencePickerField : VisualElement
{
    private readonly TextField _textField;
    private readonly List<string> _options;

    public ReferencePickerField(string value, IReadOnlyList<string> options)
    {
        AddToClassList("sm-reference-field");
        _options = new List<string>();
        if (options != null)
        {
            for (int i = 0; i < options.Count; i++)
            {
                if (!string.IsNullOrWhiteSpace(options[i]) && !_options.Contains(options[i]))
                {
                    _options.Add(options[i]);
                }
            }
        }

        _textField = new TextField
        {
            name = "reference-text",
            value = value ?? string.Empty,
            isDelayed = true
        };
        _textField.AddToClassList("sm-reference-text");
        _textField.RegisterValueChangedCallback(evt => ValueChanged?.Invoke(evt.newValue ?? string.Empty));
        Add(_textField);

        var choose = new Button(ShowOptions)
        {
            text = "...",
            name = "reference-picker-button",
            tooltip = _options.Count > 0 ? "등록된 ID에서 선택" : "등록된 ID가 없습니다"
        };
        choose.AddToClassList("sm-reference-button");
        choose.SetEnabled(_options.Count > 0);
        Add(choose);
    }

    public event Action<string> ValueChanged;
    public IReadOnlyList<string> Options => _options;
    public string Value => _textField.value ?? string.Empty;

    public void SetValueWithoutNotify(string value)
    {
        _textField.SetValueWithoutNotify(value ?? string.Empty);
    }

    private void ShowOptions()
    {
        var menu = new GenericMenu();
        for (int i = 0; i < _options.Count; i++)
        {
            string captured = _options[i];
            menu.AddItem(
                new GUIContent(captured),
                string.Equals(captured, Value, StringComparison.Ordinal),
                () =>
                {
                    _textField.SetValueWithoutNotify(captured);
                    ValueChanged?.Invoke(captured);
                });
        }

        menu.ShowAsContext();
    }
}

public static class ParameterFieldFactory
{
    public static VisualElement Create(
        ActionCatalogParameter parameter,
        JToken currentValue,
        ParameterFieldContext context,
        Action<JToken> onChanged)
    {
        parameter = parameter ?? new ActionCatalogParameter();
        context = context ?? new ParameterFieldContext();
        onChanged = onChanged ?? (_ => { });

        var root = new VisualElement { name = "parameter-" + ControlKey(parameter) };
        root.AddToClassList("sm-parameter-editor");
        root.EnableInClassList("is-required", parameter.Required);
        root.EnableInClassList("is-quick", parameter.QuickEdit);
        root.tooltip = parameter.DescriptionKo ?? string.Empty;

        var heading = new VisualElement();
        heading.AddToClassList("sm-parameter-heading");
        string label = !string.IsNullOrWhiteSpace(parameter.DisplayNameKo)
            ? parameter.DisplayNameKo.Trim()
            : (!string.IsNullOrWhiteSpace(parameter.Name) ? parameter.Name.Trim() : "값");
        var title = new Label(label + (parameter.Required ? " *" : string.Empty));
        title.AddToClassList("sm-parameter-label");
        heading.Add(title);
        if (!string.IsNullOrWhiteSpace(parameter.Name))
        {
            var id = new Label(parameter.Name.Trim());
            id.AddToClassList("sm-parameter-id");
            heading.Add(id);
        }

        root.Add(heading);
        if (!string.IsNullOrWhiteSpace(parameter.DescriptionKo))
        {
            var description = new Label(parameter.DescriptionKo.Trim());
            description.AddToClassList("sm-parameter-description");
            root.Add(description);
        }

        Func<JToken, Action<JToken>, VisualElement> literalFactory = (value, changed) =>
            CreateLiteral(parameter, value, context, changed);
        VisualElement editor = SupportsBindings(parameter)
            ? new ValueSourceField(parameter, currentValue, context, literalFactory, onChanged)
            : literalFactory(currentValue, onChanged);
        editor.AddToClassList("sm-parameter-control");
        root.Add(editor);

        if (!string.IsNullOrWhiteSpace(parameter.UnitKo))
        {
            var unit = new Label(parameter.UnitKo.Trim()) { name = "parameter-unit" };
            unit.AddToClassList("sm-parameter-unit");
            root.Add(unit);
        }

        if (parameter.Required && IsMissing(currentValue))
        {
            var warning = new Label("필수 값이 비어 있습니다.");
            warning.AddToClassList("sm-field-warning");
            root.Add(warning);
        }

        return root;
    }

    public static ParameterEditorKind ResolveKind(ActionCatalogParameter parameter)
    {
        string type = Normalize(parameter?.Type).ToLowerInvariant();
        string control = Normalize(parameter?.EditorControlId).ToLowerInvariant();
        if (control == "toggle" || type == "bool" || type == "boolean")
        {
            return ParameterEditorKind.Toggle;
        }

        if (control == "segmented" || control == "enum" || type == "enum")
        {
            return ParameterEditorKind.Enum;
        }

        if (control == "color" || type == "color")
        {
            return ParameterEditorKind.Color;
        }

        if (control == "vector2" || type == "vector2")
        {
            return ParameterEditorKind.Vector2;
        }

        if (control == "vector3" || type == "vector3")
        {
            return ParameterEditorKind.Vector3;
        }

        if (type == "duration")
        {
            return ParameterEditorKind.Duration;
        }

        if (type == "int" || type == "integer")
        {
            return ParameterEditorKind.Integer;
        }

        if (type == "number" || type == "float" || type == "double"
            || control == "number" || control == "slider")
        {
            return ParameterEditorKind.Number;
        }

        if (control == "input_map" || control == "json"
            || type == "object" || type == "json")
        {
            return ParameterEditorKind.Json;
        }

        if (type.EndsWith("[]", StringComparison.Ordinal) || control == "list")
        {
            return ParameterEditorKind.StringList;
        }

        if (IsReferenceControl(control) || type.EndsWith("ref", StringComparison.Ordinal))
        {
            return ParameterEditorKind.Reference;
        }

        return ParameterEditorKind.Text;
    }

    public static string ControlKey(ActionCatalogParameter parameter)
    {
        string control = Normalize(parameter?.EditorControlId);
        if (!string.IsNullOrEmpty(control))
        {
            return control.ToLowerInvariant();
        }

        string type = Normalize(parameter?.Type);
        return string.IsNullOrEmpty(type) ? "text" : type.ToLowerInvariant();
    }

    public static JToken DefaultLiteral(ActionCatalogParameter parameter)
    {
        if (!string.IsNullOrWhiteSpace(parameter?.DefaultValue))
        {
            try
            {
                return JToken.Parse(parameter.DefaultValue);
            }
            catch
            {
                return new JValue(parameter.DefaultValue);
            }
        }

        switch (ResolveKind(parameter))
        {
            case ParameterEditorKind.Toggle: return new JValue(false);
            case ParameterEditorKind.Number:
            case ParameterEditorKind.Duration: return new JValue(0d);
            case ParameterEditorKind.Integer: return new JValue(0);
            case ParameterEditorKind.Vector2: return new JArray(0d, 0d);
            case ParameterEditorKind.Vector3: return new JArray(0d, 0d, 0d);
            case ParameterEditorKind.Json: return new JObject();
            case ParameterEditorKind.StringList: return new JArray();
            case ParameterEditorKind.Enum:
                return parameter?.Options != null && parameter.Options.Count > 0
                    ? new JValue(parameter.Options[0])
                    : new JValue(string.Empty);
            default: return new JValue(string.Empty);
        }
    }

    private static VisualElement CreateLiteral(
        ActionCatalogParameter parameter,
        JToken value,
        ParameterFieldContext context,
        Action<JToken> changed)
    {
        JToken effective = value == null || value.Type == JTokenType.Null
            ? DefaultLiteral(parameter)
            : value;
        switch (ResolveKind(parameter))
        {
            case ParameterEditorKind.Number:
            case ParameterEditorKind.Duration:
                return CreateNumber(parameter, effective, changed);
            case ParameterEditorKind.Integer:
                return CreateInteger(parameter, effective, changed);
            case ParameterEditorKind.Toggle:
                var toggle = new Toggle { value = TokenBool(effective) };
                toggle.RegisterValueChangedCallback(evt => changed(new JValue(evt.newValue)));
                return toggle;
            case ParameterEditorKind.Enum:
                var choices = parameter.Options != null
                    ? new List<string>(parameter.Options)
                    : new List<string>();
                string selected = TokenString(effective);
                if (!string.IsNullOrEmpty(selected) && !choices.Contains(selected))
                {
                    choices.Insert(0, selected);
                }
                if (choices.Count == 0)
                {
                    choices.Add(string.Empty);
                }
                var dropdown = new DropdownField(choices, 0);
                dropdown.SetValueWithoutNotify(selected);
                dropdown.RegisterValueChangedCallback(evt => changed(new JValue(evt.newValue ?? string.Empty)));
                return dropdown;
            case ParameterEditorKind.Color:
                var color = new ColorField { value = TokenColor(effective), showAlpha = true };
                color.RegisterValueChangedCallback(evt => changed(new JValue(
                    "#" + ColorUtility.ToHtmlStringRGBA(evt.newValue))));
                return color;
            case ParameterEditorKind.Vector2:
                var vector2 = new Vector2Field { value = TokenVector2(effective) };
                vector2.RegisterValueChangedCallback(evt => changed(new JArray(evt.newValue.x, evt.newValue.y)));
                return vector2;
            case ParameterEditorKind.Vector3:
                var vector3 = new Vector3Field { value = TokenVector3(effective) };
                vector3.RegisterValueChangedCallback(evt => changed(new JArray(
                    evt.newValue.x,
                    evt.newValue.y,
                    evt.newValue.z)));
                return vector3;
            case ParameterEditorKind.Reference:
                var reference = new ReferencePickerField(
                    TokenString(effective),
                    context.GetReferenceOptions(parameter));
                reference.ValueChanged += next => changed(new JValue(next));
                return reference;
            case ParameterEditorKind.Json:
                return CreateJson(effective, changed);
            case ParameterEditorKind.StringList:
                return CreateStringList(effective, changed);
            default:
                var text = new TextField
                {
                    value = TokenString(effective),
                    isDelayed = true
                };
                text.RegisterValueChangedCallback(evt => changed(new JValue(evt.newValue ?? string.Empty)));
                return text;
        }
    }

    private static VisualElement CreateNumber(
        ActionCatalogParameter parameter,
        JToken value,
        Action<JToken> changed)
    {
        var row = new VisualElement();
        row.AddToClassList("sm-number-field");
        double initial = TokenDouble(value);
        var number = new DoubleField { value = initial, isDelayed = true };
        number.RegisterValueChangedCallback(evt =>
        {
            double next = Clamp(parameter, evt.newValue);
            number.SetValueWithoutNotify(next);
            changed(new JValue(next));
        });
        row.Add(number);
        if (parameter.HasMinimum && parameter.HasMaximum)
        {
            var slider = new Slider(
                (float)parameter.Minimum,
                (float)parameter.Maximum)
            {
                value = (float)Clamp(parameter, initial)
            };
            slider.RegisterValueChangedCallback(evt =>
            {
                number.SetValueWithoutNotify(evt.newValue);
                changed(new JValue((double)evt.newValue));
            });
            row.Add(slider);
        }

        return row;
    }

    private static VisualElement CreateInteger(
        ActionCatalogParameter parameter,
        JToken value,
        Action<JToken> changed)
    {
        int initial = (int)Math.Round(TokenDouble(value));
        var field = new IntegerField { value = initial, isDelayed = true };
        field.RegisterValueChangedCallback(evt => changed(new JValue(
            (int)Math.Round(Clamp(parameter, evt.newValue)))));
        return field;
    }

    private static VisualElement CreateJson(JToken value, Action<JToken> changed)
    {
        var field = new TextField
        {
            multiline = true,
            isDelayed = true,
            value = (value ?? new JObject()).ToString(Formatting.Indented)
        };
        field.AddToClassList("sm-json-parameter");
        field.RegisterValueChangedCallback(evt =>
        {
            try
            {
                JToken parsed = string.IsNullOrWhiteSpace(evt.newValue)
                    ? new JObject()
                    : JToken.Parse(evt.newValue);
                field.RemoveFromClassList("is-invalid");
                changed(parsed);
            }
            catch
            {
                field.AddToClassList("is-invalid");
            }
        });
        return field;
    }

    private static VisualElement CreateStringList(JToken value, Action<JToken> changed)
    {
        string initial;
        if (value is JArray array)
        {
            var parts = new List<string>();
            for (int i = 0; i < array.Count; i++)
            {
                parts.Add(TokenString(array[i]));
            }
            initial = string.Join(", ", parts);
        }
        else
        {
            initial = TokenString(value);
        }

        var field = new TextField { value = initial, isDelayed = true };
        field.RegisterValueChangedCallback(evt =>
        {
            var result = new JArray();
            string[] parts = (evt.newValue ?? string.Empty).Split(',');
            for (int i = 0; i < parts.Length; i++)
            {
                string item = parts[i].Trim();
                if (!string.IsNullOrEmpty(item))
                {
                    result.Add(item);
                }
            }
            changed(result);
        });
        return field;
    }

    private static bool SupportsBindings(ActionCatalogParameter parameter)
    {
        if (parameter?.ValueSources == null || parameter.ValueSources.Count <= 1)
        {
            return false;
        }

        for (int i = 0; i < parameter.ValueSources.Count; i++)
        {
            if (!string.Equals(
                    Normalize(parameter.ValueSources[i]),
                    "literal",
                    StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsReferenceControl(string control)
    {
        switch (control)
        {
            case "actor":
            case "dialogue":
            case "audio":
            case "ui":
            case "module":
            case "animation":
            case "sequence":
            case "position":
            case "block":
            case "timeline":
            case "vfx":
                return true;
            default:
                return false;
        }
    }

    private static double Clamp(ActionCatalogParameter parameter, double value)
    {
        if (parameter.HasMinimum)
        {
            value = Math.Max(parameter.Minimum, value);
        }
        if (parameter.HasMaximum)
        {
            value = Math.Min(parameter.Maximum, value);
        }
        return value;
    }

    private static bool IsMissing(JToken token)
    {
        return token == null
            || token.Type == JTokenType.Null
            || (token.Type == JTokenType.String && string.IsNullOrWhiteSpace(token.Value<string>()));
    }

    private static string TokenString(JToken token)
    {
        if (token == null || token.Type == JTokenType.Null)
        {
            return string.Empty;
        }
        return token.Type == JTokenType.String
            ? token.Value<string>() ?? string.Empty
            : token.ToString(Formatting.None);
    }

    private static double TokenDouble(JToken token)
    {
        if (token != null && token.Type != JTokenType.Null
            && double.TryParse(
                token.ToString(Formatting.None),
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out double value))
        {
            return value;
        }
        return 0d;
    }

    private static bool TokenBool(JToken token)
    {
        return token != null && token.Type == JTokenType.Boolean
            ? token.Value<bool>()
            : string.Equals(TokenString(token), "true", StringComparison.OrdinalIgnoreCase);
    }

    private static Color TokenColor(JToken token)
    {
        return ColorUtility.TryParseHtmlString(TokenString(token), out Color color)
            ? color
            : Color.white;
    }

    private static Vector2 TokenVector2(JToken token)
    {
        return token is JArray array && array.Count >= 2
            ? new Vector2((float)TokenDouble(array[0]), (float)TokenDouble(array[1]))
            : Vector2.zero;
    }

    private static Vector3 TokenVector3(JToken token)
    {
        return token is JArray array && array.Count >= 3
            ? new Vector3(
                (float)TokenDouble(array[0]),
                (float)TokenDouble(array[1]),
                (float)TokenDouble(array[2]))
            : Vector3.zero;
    }

    private static string Normalize(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
    }
}
