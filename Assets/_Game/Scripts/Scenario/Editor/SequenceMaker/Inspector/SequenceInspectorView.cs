using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine.UIElements;

public sealed class SequenceInspectorView : VisualElement
{
    private static readonly string[] InputTypes =
    {
        "any", "string", "int", "number", "bool", "actorRef", "moduleRef",
        "sequenceRef", "dialogueRef", "audioRef", "color", "vector2", "vector3", "json"
    };

    private ActionSequenceAsset _sequence;
    private SequenceEditCommandStack _commands;
    private SequenceUsageIndex _usage;
    private ActionCatalogAsset _catalog;

    public SequenceInspectorView()
    {
        AddToClassList("sm-sequence-inspector");
    }

    public event Action EditApplied;
    public event Action<string> Error;

    public void Bind(
        ActionSequenceAsset sequence,
        SequenceEditCommandStack commands,
        SequenceUsageIndex usage,
        ActionCatalogAsset catalog)
    {
        _sequence = sequence;
        _commands = commands;
        _usage = usage;
        _catalog = catalog;
        Render();
    }

    private void Render()
    {
        Clear();
        if (_sequence == null)
        {
            Add(Empty("시퀀스를 선택하면 계약이 표시됩니다."));
            return;
        }

        var title = new Label("시퀀스 설정");
        title.AddToClassList("sm-inspector-action-title");
        Add(title);
        var id = new TextField("Sequence ID")
        {
            value = _sequence.SequenceId ?? string.Empty,
            isReadOnly = true
        };
        id.tooltip = "YAML, 규칙, sequence.call이 사용하는 안정적인 ID입니다. 일반 편집에서는 변경하지 않습니다.";
        Add(id);
        AddUsageImpact();

        var displayName = new TextField("표시 이름")
        {
            value = _sequence.DisplayNameKo ?? string.Empty,
            isDelayed = true
        };
        displayName.RegisterValueChangedCallback(evt => Execute(
            SequenceEditCommands.SetSequenceDisplayName(evt.newValue)));
        Add(displayName);

        ActionSequenceContractData contract = _sequence.Contract
            ?? new ActionSequenceContractData();
        var description = new TextField("무엇을 하는 시퀀스인가")
        {
            value = contract.DescriptionKo ?? string.Empty,
            multiline = true,
            isDelayed = true
        };
        description.AddToClassList("sm-contract-textarea");
        description.RegisterValueChangedCallback(evt => ChangeContract(next =>
            next.DescriptionKo = evt.newValue ?? string.Empty));
        Add(description);

        var usage = new TextField("언제 사용하는가")
        {
            value = contract.UsageKo ?? string.Empty,
            multiline = true,
            isDelayed = true
        };
        usage.AddToClassList("sm-contract-textarea");
        usage.RegisterValueChangedCallback(evt => ChangeContract(next =>
            next.UsageKo = evt.newValue ?? string.Empty));
        Add(usage);

        var lifecycle = new DropdownField(
            "상태",
            new List<string> { "작업 중", "사용 가능", "호환용" },
            LifecycleIndex(contract.Lifecycle));
        lifecycle.RegisterValueChangedCallback(evt => ChangeContract(next =>
            next.Lifecycle = ParseLifecycle(evt.newValue)));
        Add(lifecycle);

        var tags = new TextField("태그")
        {
            value = contract.Tags != null ? string.Join(", ", contract.Tags) : string.Empty,
            isDelayed = true
        };
        tags.tooltip = "쉼표로 구분";
        tags.RegisterValueChangedCallback(evt => ChangeContract(next =>
            next.Tags = ParseList(evt.newValue)));
        Add(tags);

        var modes = new TextField("사용 가능 Primary Mode")
        {
            value = contract.AllowedPrimaryModes != null
                ? string.Join(", ", contract.AllowedPrimaryModes)
                : string.Empty,
            isDelayed = true
        };
        modes.tooltip = "예: overworld, battle. 비우면 제한하지 않습니다.";
        modes.RegisterValueChangedCallback(evt => ChangeContract(next =>
            next.AllowedPrimaryModes = ParseList(evt.newValue)));
        Add(modes);

        AddCapabilities();
        AddInputSection(contract);
    }

    private void AddUsageImpact()
    {
        int count = _usage?.GetUsages(_sequence.SequenceId).Count ?? 0;
        var impact = new Label(count == 0
            ? "현재 다른 규칙이나 시퀀스에서 직접 호출하지 않음"
            : count + "곳에서 이 시퀀스를 사용 중");
        impact.AddToClassList("sm-contract-impact");
        Add(impact);
    }

    private void AddCapabilities()
    {
        var contexts = new HashSet<string>(StringComparer.Ordinal);
        CollectContexts(_sequence.Actions, contexts);
        if (contexts.Count == 0)
        {
            return;
        }
        var sorted = new List<string>(contexts);
        sorted.Sort(StringComparer.Ordinal);
        var box = new VisualElement();
        box.AddToClassList("sm-contract-capabilities");
        var heading = new Label("필요한 실행 기능");
        heading.AddToClassList("sm-mini-label");
        box.Add(heading);
        box.Add(new Label(string.Join(", ", sorted)));
        Add(box);
    }

    private void AddInputSection(ActionSequenceContractData contract)
    {
        var header = new VisualElement();
        header.AddToClassList("sm-contract-input-header");
        var title = new Label("시퀀스 입력");
        title.AddToClassList("sm-inspector-section-title");
        title.style.flexGrow = 1f;
        header.Add(title);
        var add = new Button(AddInput) { text = "+ 입력" };
        add.AddToClassList("sm-small-command");
        header.Add(add);
        Add(header);

        if (contract.Inputs == null || contract.Inputs.Count == 0)
        {
            Add(Empty("외부에서 받을 값이 없습니다."));
            return;
        }
        for (int i = 0; i < contract.Inputs.Count; i++)
        {
            Add(CreateInputRow(contract.Inputs[i], i));
        }
    }

    private VisualElement CreateInputRow(SequenceInputDefinition input, int index)
    {
        input = input ?? new SequenceInputDefinition();
        var row = new VisualElement();
        row.AddToClassList("sm-contract-input");
        int uses = CountInputBindings(_sequence.Actions, input.InputId);

        var rowHeader = new VisualElement();
        rowHeader.AddToClassList("sm-contract-input-row-header");
        var number = new Label("입력 " + (index + 1));
        number.AddToClassList("sm-contract-input-title");
        rowHeader.Add(number);
        if (uses > 0)
        {
            var usage = new Label(uses + "개 binding");
            usage.AddToClassList("sm-contract-input-usage");
            rowHeader.Add(usage);
        }
        rowHeader.Add(CommandButton("↑", "위로 이동", () => MoveInput(index, index - 1), index > 0));
        int count = _sequence.Contract?.Inputs?.Count ?? 0;
        rowHeader.Add(CommandButton("↓", "아래로 이동", () => MoveInput(index, index + 1), index < count - 1));
        rowHeader.Add(CommandButton("×", "입력 삭제", () => DeleteInput(index, uses), true));
        row.Add(rowHeader);

        var id = new TextField("Input ID")
        {
            value = input.InputId ?? string.Empty,
            isDelayed = true
        };
        id.tooltip = "${input." + (input.InputId ?? "id") + "} binding에 사용하는 안정적인 ID";
        id.RegisterValueChangedCallback(evt => RenameInput(index, input.InputId, evt.newValue, uses));
        row.Add(id);

        var displayName = new TextField("표시 이름")
        {
            value = input.DisplayNameKo ?? string.Empty,
            isDelayed = true
        };
        displayName.RegisterValueChangedCallback(evt => ChangeInput(index, next =>
            next.DisplayNameKo = evt.newValue ?? string.Empty));
        row.Add(displayName);

        var description = new TextField("설명")
        {
            value = input.DescriptionKo ?? string.Empty,
            multiline = true,
            isDelayed = true
        };
        description.RegisterValueChangedCallback(evt => ChangeInput(index, next =>
            next.DescriptionKo = evt.newValue ?? string.Empty));
        row.Add(description);

        var type = new ReferencePickerField(input.TypeId, InputTypes);
        type.ValueChanged += value => ChangeInput(index, next =>
            next.TypeId = string.IsNullOrWhiteSpace(value) ? "any" : value.Trim());
        var typeField = new VisualElement();
        typeField.AddToClassList("sm-contract-labeled-field");
        typeField.Add(new Label("값 타입"));
        typeField.Add(type);
        row.Add(typeField);

        var required = new Toggle("필수 입력") { value = input.Required };
        required.RegisterValueChangedCallback(evt => ChangeInput(index, next =>
            next.Required = evt.newValue));
        row.Add(required);

        var defaultValue = new TextField("기본값 JSON")
        {
            value = input.DefaultValueJson ?? string.Empty,
            isDelayed = true
        };
        defaultValue.tooltip = "문자열은 따옴표를 포함합니다. 예: \"player\"";
        defaultValue.RegisterValueChangedCallback(evt =>
        {
            string value = evt.newValue ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(value))
            {
                try
                {
                    JToken.Parse(value);
                }
                catch (Exception exception)
                {
                    Error?.Invoke("기본값 JSON 오류: " + exception.Message);
                    defaultValue.AddToClassList("is-invalid");
                    return;
                }
            }
            defaultValue.RemoveFromClassList("is-invalid");
            ChangeInput(index, next => next.DefaultValueJson = value);
        });
        row.Add(defaultValue);
        return row;
    }

    private void AddInput()
    {
        ActionSequenceContractData next = CopyContract();
        next.Inputs = next.Inputs ?? new List<SequenceInputDefinition>();
        int suffix = next.Inputs.Count + 1;
        string id;
        do
        {
            id = "input" + suffix++;
        }
        while (HasInput(next.Inputs, id));
        next.Inputs.Add(new SequenceInputDefinition
        {
            InputId = id,
            DisplayNameKo = "새 입력",
            TypeId = "any"
        });
        Execute(SequenceEditCommands.SetSequenceContract(next));
    }

    private void MoveInput(int source, int target)
    {
        ActionSequenceContractData next = CopyContract();
        if (source < 0 || source >= next.Inputs.Count || target < 0 || target >= next.Inputs.Count)
        {
            return;
        }
        SequenceInputDefinition input = next.Inputs[source];
        next.Inputs.RemoveAt(source);
        next.Inputs.Insert(target, input);
        Execute(SequenceEditCommands.SetSequenceContract(next));
    }

    private void DeleteInput(int index, int uses)
    {
        if (uses > 0 && !EditorUtility.DisplayDialog(
                "사용 중인 입력 삭제",
                uses + "개 binding이 이 입력을 사용합니다. 삭제 후 검증 오류가 생길 수 있습니다.",
                "삭제",
                "취소"))
        {
            return;
        }
        ActionSequenceContractData next = CopyContract();
        if (index >= 0 && index < next.Inputs.Count)
        {
            next.Inputs.RemoveAt(index);
            Execute(SequenceEditCommands.SetSequenceContract(next));
        }
    }

    private void RenameInput(int index, string previous, string value, int uses)
    {
        string normalized = value?.Trim() ?? string.Empty;
        if (string.IsNullOrEmpty(normalized))
        {
            Error?.Invoke("Input ID는 비울 수 없습니다.");
            return;
        }
        ActionSequenceContractData next = CopyContract();
        for (int i = 0; i < next.Inputs.Count; i++)
        {
            if (i != index && next.Inputs[i]?.InputId == normalized)
            {
                Error?.Invoke("같은 Input ID가 이미 있습니다: " + normalized);
                return;
            }
        }
        next.Inputs[index].InputId = normalized;
        Execute(normalized == previous
            ? SequenceEditCommands.SetSequenceContract(next)
            : SequenceEditCommands.RenameSequenceInput(previous, normalized, next));
    }

    private void ChangeInput(int index, Action<SequenceInputDefinition> change)
    {
        ActionSequenceContractData next = CopyContract();
        if (index < 0 || index >= next.Inputs.Count)
        {
            return;
        }
        change(next.Inputs[index]);
        Execute(SequenceEditCommands.SetSequenceContract(next));
    }

    private void ChangeContract(Action<ActionSequenceContractData> change)
    {
        ActionSequenceContractData next = CopyContract();
        change(next);
        Execute(SequenceEditCommands.SetSequenceContract(next));
    }

    private ActionSequenceContractData CopyContract()
    {
        return ActionSequenceContractData.CopyOf(
            _sequence.Contract ?? new ActionSequenceContractData());
    }

    private void Execute(ISequenceEditCommand command)
    {
        if (_commands == null)
        {
            Error?.Invoke("시퀀스 편집 히스토리가 준비되지 않았습니다.");
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

    private void CollectContexts(
        IList<ScenarioActionData> actions,
        HashSet<string> destination)
    {
        if (actions == null)
        {
            return;
        }
        for (int i = 0; i < actions.Count; i++)
        {
            ScenarioActionData action = actions[i];
            ActionCatalogEntry entry = _catalog?.FindById(action?.ActionId);
            if (entry?.RequiredContexts != null)
            {
                for (int j = 0; j < entry.RequiredContexts.Count; j++)
                {
                    if (!string.IsNullOrWhiteSpace(entry.RequiredContexts[j]))
                    {
                        destination.Add(entry.RequiredContexts[j].Trim());
                    }
                }
            }
            CollectContexts(action?.Children, destination);
        }
    }

    private static int CountInputBindings(IList<ScenarioActionData> actions, string inputId)
    {
        if (actions == null || string.IsNullOrWhiteSpace(inputId))
        {
            return 0;
        }
        int count = 0;
        for (int i = 0; i < actions.Count; i++)
        {
            ScenarioActionData action = actions[i];
            if (action == null)
            {
                continue;
            }
            try
            {
                JToken root = string.IsNullOrWhiteSpace(action.ParametersJson)
                    ? new JObject()
                    : JToken.Parse(action.ParametersJson);
                count += CountBindings(root, "input." + inputId.Trim());
            }
            catch
            {
                // Validation reports malformed JSON separately.
            }
            count += CountInputBindings(action.Children, inputId);
        }
        return count;
    }

    private static int CountBindings(JToken token, string path)
    {
        if (token == null)
        {
            return 0;
        }
        int count = ValueSourceField.TryReadBinding(token, out string binding)
            && binding == path ? 1 : 0;
        foreach (JToken child in token.Children())
        {
            count += CountBindings(child, path);
        }
        return count;
    }

    private static Button CommandButton(
        string text,
        string tooltip,
        Action clicked,
        bool enabled)
    {
        var button = new Button(clicked) { text = text, tooltip = tooltip };
        button.AddToClassList("sm-contract-icon-command");
        button.SetEnabled(enabled);
        return button;
    }

    private static bool HasInput(IReadOnlyList<SequenceInputDefinition> inputs, string inputId)
    {
        for (int i = 0; i < inputs.Count; i++)
        {
            if (inputs[i]?.InputId == inputId)
            {
                return true;
            }
        }
        return false;
    }

    private static List<string> ParseList(string value)
    {
        var result = new List<string>();
        string[] parts = (value ?? string.Empty).Split(',');
        for (int i = 0; i < parts.Length; i++)
        {
            string item = parts[i].Trim();
            if (!string.IsNullOrEmpty(item) && !result.Contains(item))
            {
                result.Add(item);
            }
        }
        return result;
    }

    private static int LifecycleIndex(ActionSequenceLifecycle lifecycle)
    {
        switch (lifecycle)
        {
            case ActionSequenceLifecycle.Ready: return 1;
            case ActionSequenceLifecycle.Deprecated: return 2;
            default: return 0;
        }
    }

    private static ActionSequenceLifecycle ParseLifecycle(string value)
    {
        switch (value)
        {
            case "사용 가능": return ActionSequenceLifecycle.Ready;
            case "호환용": return ActionSequenceLifecycle.Deprecated;
            default: return ActionSequenceLifecycle.Draft;
        }
    }

    private static Label Empty(string text)
    {
        var label = new Label(text ?? string.Empty);
        label.AddToClassList("sm-inspector-empty");
        return label;
    }
}
