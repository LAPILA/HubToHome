using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEngine.UIElements;

public sealed class ValueSourceField : VisualElement
{
    private static readonly string[] OrderedSources =
    {
        "literal", "input", "event", "session", "memory", "flag", "context", "result"
    };

    private readonly ActionCatalogParameter _parameter;
    private readonly ParameterFieldContext _context;
    private readonly Func<JToken, Action<JToken>, VisualElement> _literalFactory;
    private readonly Action<JToken> _changed;
    private readonly VisualElement _valueHost;
    private readonly DropdownField _sourceField;
    private readonly List<string> _sourceIds = new List<string>();
    private JToken _lastLiteral;
    private string _bindingPath = string.Empty;

    public ValueSourceField(
        ActionCatalogParameter parameter,
        JToken currentValue,
        ParameterFieldContext context,
        Func<JToken, Action<JToken>, VisualElement> literalFactory,
        Action<JToken> changed)
    {
        _parameter = parameter ?? new ActionCatalogParameter();
        _context = context ?? new ParameterFieldContext();
        _literalFactory = literalFactory ?? throw new ArgumentNullException(nameof(literalFactory));
        _changed = changed ?? (_ => { });
        AddToClassList("sm-value-source-field");

        bool binding = TryReadBinding(currentValue, out string path);
        string initialSource = binding ? Root(path) : "literal";
        _bindingPath = binding ? path : string.Empty;
        _lastLiteral = binding ? ParameterFieldFactory.DefaultLiteral(_parameter) : currentValue;
        BuildSources(initialSource);

        var sourceRow = new VisualElement();
        sourceRow.AddToClassList("sm-value-source-row");
        var sourceLabel = new Label("값 출처");
        sourceLabel.AddToClassList("sm-value-source-label");
        sourceRow.Add(sourceLabel);
        var labels = new List<string>();
        for (int i = 0; i < _sourceIds.Count; i++)
        {
            labels.Add(DisplaySource(_sourceIds[i]));
        }

        int sourceIndex = Math.Max(0, _sourceIds.IndexOf(initialSource));
        _sourceField = new DropdownField(labels, sourceIndex)
        {
            name = "value-source-dropdown"
        };
        _sourceField.RegisterValueChangedCallback(evt =>
        {
            int index = labels.IndexOf(evt.newValue);
            RenderValue(index >= 0 ? _sourceIds[index] : "literal", true);
        });
        sourceRow.Add(_sourceField);
        Add(sourceRow);

        _valueHost = new VisualElement();
        _valueHost.AddToClassList("sm-value-source-content");
        Add(_valueHost);
        RenderValue(_sourceIds[sourceIndex], false);
    }

    public string SourceId
    {
        get
        {
            int index = _sourceField.choices.IndexOf(_sourceField.value);
            return index >= 0 && index < _sourceIds.Count ? _sourceIds[index] : "literal";
        }
    }

    public static bool TryReadBinding(JToken token, out string path)
    {
        path = string.Empty;
        if (!(token is JObject objectValue)
            || !objectValue.TryGetValue("$bind", out JToken binding)
            || binding.Type != JTokenType.String)
        {
            return false;
        }

        path = binding.Value<string>()?.Trim() ?? string.Empty;
        return !string.IsNullOrEmpty(path);
    }

    public static JObject CreateBindingToken(string path)
    {
        return new JObject { ["$bind"] = path ?? string.Empty };
    }

    private void BuildSources(string initialSource)
    {
        var declared = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (_parameter.ValueSources != null)
        {
            for (int i = 0; i < _parameter.ValueSources.Count; i++)
            {
                string source = Normalize(_parameter.ValueSources[i]);
                if (!string.IsNullOrEmpty(source))
                {
                    declared.Add(source);
                }
            }
        }

        if (!string.IsNullOrEmpty(initialSource))
        {
            declared.Add(initialSource);
        }

        for (int i = 0; i < OrderedSources.Length; i++)
        {
            if (declared.Contains(OrderedSources[i]))
            {
                _sourceIds.Add(OrderedSources[i]);
            }
        }

        foreach (string source in declared)
        {
            if (!_sourceIds.Contains(source))
            {
                _sourceIds.Add(source);
            }
        }

        if (_sourceIds.Count == 0)
        {
            _sourceIds.Add("literal");
        }
    }

    private void RenderValue(string sourceId, bool notify)
    {
        _valueHost.Clear();
        if (string.Equals(sourceId, "literal", StringComparison.OrdinalIgnoreCase))
        {
            VisualElement literal = _literalFactory(_lastLiteral, value =>
            {
                _lastLiteral = value;
                _changed(value);
            });
            _valueHost.Add(literal);
            if (notify)
            {
                _changed(_lastLiteral ?? ParameterFieldFactory.DefaultLiteral(_parameter));
            }
            return;
        }

        IReadOnlyList<string> options = _context.GetBindingOptions(sourceId);
        string initial = BindingForSource(sourceId, options);
        if (options.Count > 0)
        {
            var choices = new List<string>(options);
            if (!string.IsNullOrEmpty(initial) && !choices.Contains(initial))
            {
                choices.Insert(0, initial);
            }
            var dropdown = new DropdownField(choices, 0)
            {
                name = "binding-path-dropdown"
            };
            dropdown.SetValueWithoutNotify(initial);
            dropdown.RegisterValueChangedCallback(evt => SetBindingPath(evt.newValue));
            _valueHost.Add(dropdown);
        }
        else
        {
            var path = new TextField
            {
                name = "binding-path-field",
                value = initial,
                isDelayed = true,
                tooltip = sourceId + ".값 경로"
            };
            path.RegisterValueChangedCallback(evt => SetBindingPath(evt.newValue));
            _valueHost.Add(path);
        }

        _bindingPath = initial;
        if (notify)
        {
            _changed(CreateBindingToken(_bindingPath));
        }
    }

    private string BindingForSource(string sourceId, IReadOnlyList<string> options)
    {
        if (string.Equals(Root(_bindingPath), sourceId, StringComparison.OrdinalIgnoreCase))
        {
            return _bindingPath;
        }
        if (options != null && options.Count > 0)
        {
            return options[0];
        }
        return sourceId + ".";
    }

    public void SetBindingPath(string path)
    {
        _bindingPath = Normalize(path);
        _changed(CreateBindingToken(_bindingPath));
    }

    private static string Root(string path)
    {
        string normalized = Normalize(path);
        int separator = normalized.IndexOf('.');
        return separator < 0 ? normalized : normalized.Substring(0, separator);
    }

    private static string DisplaySource(string sourceId)
    {
        switch (sourceId)
        {
            case "literal": return "직접 입력";
            case "input": return "시퀀스 입력";
            case "event": return "이벤트 값";
            case "session": return "실행 세션";
            case "memory": return "저장 메모리";
            case "flag": return "플래그";
            case "context": return "실행 컨텍스트";
            case "result": return "이전 결과";
            default: return sourceId;
        }
    }

    private static string Normalize(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
    }
}
