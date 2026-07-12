using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine.UIElements;

public sealed class TriggerRuleEditorView : VisualElement
{
    private readonly Dictionary<string, SimulationState> _simulationStates =
        new Dictionary<string, SimulationState>(StringComparer.Ordinal);

    private BattleScenarioData _battle;
    private ScenarioTriggerRuleData _rule;
    private BattleEventRuleData _legacyRule;
    private int _legacyIndex = -1;
    private TriggerLibraryAsset _library;
    private ParameterFieldContext _fieldContext;
    private BattleScenarioEditCommandStack _commands;
    private ScenarioValidationResult _validation;

    public TriggerRuleEditorView()
    {
        AddToClassList("sm-rule-editor");
    }

    public event Action EditApplied;
    public event Action<string> Error;
    public event Action<int> ConvertLegacyRequested;

    public void BindTriggerRule(
        BattleScenarioData battle,
        ScenarioTriggerRuleData rule,
        TriggerLibraryAsset library,
        ParameterFieldContext fieldContext,
        BattleScenarioEditCommandStack commands,
        ScenarioValidationResult validation)
    {
        _battle = battle;
        _rule = rule;
        _legacyRule = null;
        _legacyIndex = -1;
        _library = library;
        _fieldContext = fieldContext ?? new ParameterFieldContext();
        _commands = commands;
        _validation = validation;
        Render();
    }

    public void BindLegacyRule(
        BattleScenarioData battle,
        BattleEventRuleData rule,
        int index,
        TriggerLibraryAsset library)
    {
        _battle = battle;
        _rule = null;
        _legacyRule = rule;
        _legacyIndex = index;
        _library = library;
        Render();
    }

    public void ClearSelection()
    {
        _battle = null;
        _rule = null;
        _legacyRule = null;
        _legacyIndex = -1;
        Clear();
        Add(Empty("왼쪽에서 이벤트 규칙을 선택하세요."));
    }

    private void Render()
    {
        Clear();
        if (_legacyRule != null)
        {
            RenderLegacy();
            return;
        }
        if (_rule == null)
        {
            Add(Empty("왼쪽에서 이벤트 규칙을 선택하세요."));
            return;
        }

        AddHeader();
        AddValidation();
        AddBasicFields();
        AddWhenSection();
        AddConditionTree();
        AddDoSection();
        AddSimulator();
    }

    private void AddHeader()
    {
        var header = new VisualElement();
        header.AddToClassList("sm-rule-editor-header");
        var copy = new VisualElement();
        copy.style.flexGrow = 1f;
        var title = new Label(DisplayName(_rule));
        title.AddToClassList("sm-rule-editor-title");
        copy.Add(title);
        var id = new Label(_rule.RuleId ?? string.Empty);
        id.AddToClassList("sm-rule-editor-id");
        copy.Add(id);
        header.Add(copy);
        var enabled = new Toggle("실행") { value = !_rule.Disabled };
        enabled.RegisterValueChangedCallback(evt => SetRule(next => next.Disabled = !evt.newValue));
        header.Add(enabled);
        Add(header);

        var sentence = new Label(TriggerRuleSentenceFormatter.Format(
            _rule,
            _library,
            SequenceDisplayName));
        sentence.AddToClassList("sm-rule-sentence");
        Add(sentence);
    }

    private void AddValidation()
    {
        if (_validation?.Messages == null)
        {
            return;
        }
        for (int i = 0; i < _validation.Messages.Count; i++)
        {
            ScenarioValidationMessage message = _validation.Messages[i];
            if (message == null || message.ObjectId != _rule.RuleId)
            {
                continue;
            }
            var label = new Label(message.Message);
            label.AddToClassList("sm-rule-validation");
            label.AddToClassList(message.Severity == ScenarioValidationSeverity.Error
                ? "is-error"
                : "is-warning");
            label.tooltip = message.Code;
            Add(label);
        }
    }

    private void AddBasicFields()
    {
        var name = new TextField("규칙 이름")
        {
            value = _rule.DisplayNameKo ?? string.Empty,
            isDelayed = true
        };
        name.RegisterValueChangedCallback(evt => SetRule(next =>
            next.DisplayNameKo = evt.newValue ?? string.Empty));
        Add(name);

        var policy = new VisualElement();
        policy.AddToClassList("sm-rule-policy-row");
        var timing = Dropdown(
            "실행 시점",
            TimingLabels(),
            TimingLabel(_rule.Timing),
            value => SetRule(next => next.Timing = ParseTiming(value)));
        policy.Add(timing);
        var once = Dropdown(
            "실행 횟수",
            OnceLabels(),
            OnceLabel(_rule.Once),
            value => SetRule(next => next.Once = ParseOnce(value)));
        policy.Add(once);
        Add(policy);

        if (_rule.Timing == ScenarioTriggerTiming.Checkpoint)
        {
            var checkpoint = new TextField("Checkpoint ID")
            {
                value = _rule.CheckpointId ?? string.Empty,
                isDelayed = true
            };
            checkpoint.RegisterValueChangedCallback(evt => SetRule(next =>
                next.CheckpointId = evt.newValue ?? string.Empty));
            Add(checkpoint);
        }
    }

    private void AddWhenSection()
    {
        Add(FlowHeading("WHEN", "언제 이 규칙을 확인할까", "sm-rule-heading--when"));
        Button eventField = ReferencePickerButton(
            "Event",
            DefinitionLabel(
                _library?.FindEvent(_rule.EventId)?.DisplayNameKo,
                _rule.EventId));
        eventField.clicked += () => SequenceReferencePickerPopup.Show(
            eventField,
            "Scenario Event 선택",
            BuildEventOptions(),
            _rule.EventId,
            id => SetRule(next => next.EventId = id));
        Add(eventField);

        ScenarioEventDefinition current = _library?.FindEvent(_rule.EventId);
        if (current != null)
        {
            Add(DefinitionHelp(current.DescriptionKo, current.UsageKo));
        }
    }

    private void AddConditionTree()
    {
        Add(FlowHeading("IF", "추가 조건", "sm-rule-heading--condition"));
        if (_rule.Conditions == null)
        {
            var create = new Button(() => SetRule(next => next.Conditions = NewGroup()))
            {
                text = "조건 그룹 만들기"
            };
            create.AddToClassList("sm-rule-primary-small");
            Add(create);
            return;
        }
        Add(CreateConditionNode(_rule.Conditions, null, 0, 0));
    }

    private VisualElement CreateConditionNode(
        ScenarioTriggerConditionNodeData node,
        ScenarioTriggerConditionNodeData parent,
        int index,
        int depth)
    {
        var view = new VisualElement();
        view.AddToClassList("sm-condition-node");
        view.AddToClassList(node.Kind == ScenarioConditionNodeKind.Group
            ? "is-group"
            : "is-condition");
        view.style.marginLeft = depth * 14f;

        var header = new VisualElement();
        header.AddToClassList("sm-condition-header");
        if (node.Kind == ScenarioConditionNodeKind.Group)
        {
            var mode = new DropdownField(
                new List<string> { "모두 만족", "하나라도 만족" },
                node.GroupMode == ScenarioConditionGroupMode.All ? 0 : 1);
            mode.RegisterValueChangedCallback(evt => ModifyNode(node.NodeId, next =>
                next.GroupMode = evt.newValue == "하나라도 만족"
                    ? ScenarioConditionGroupMode.Any
                    : ScenarioConditionGroupMode.All));
            header.Add(mode);
        }
        else
        {
            AddConditionPicker(header, node);
        }
        var negate = new Toggle("아님") { value = node.Negate };
        negate.RegisterValueChangedCallback(evt => ModifyNode(node.NodeId, next =>
            next.Negate = evt.newValue));
        header.Add(negate);
        if (parent != null)
        {
            header.Add(NodeCommand("↑", "위로", () => MoveNode(parent.NodeId, index, index - 1), index > 0));
            header.Add(NodeCommand("↓", "아래로", () => MoveNode(parent.NodeId, index, index + 1), index < parent.Children.Count - 1));
            header.Add(NodeCommand("×", "삭제", () => DeleteNode(parent.NodeId, index), true));
        }
        view.Add(header);

        if (node.Kind == ScenarioConditionNodeKind.Group)
        {
            var children = new VisualElement();
            children.AddToClassList("sm-condition-children");
            if (node.Children != null)
            {
                for (int i = 0; i < node.Children.Count; i++)
                {
                    children.Add(CreateConditionNode(node.Children[i], node, i, depth + 1));
                }
            }
            var addRow = new VisualElement();
            addRow.AddToClassList("sm-condition-add-row");
            addRow.Add(NodeCommand("+ 조건", "조건 추가", () => AddCondition(node.NodeId), true));
            addRow.Add(NodeCommand("+ 그룹", "중첩 all/any 그룹 추가", () => AddGroup(node.NodeId), true));
            children.Add(addRow);
            view.Add(children);
        }
        else
        {
            AddConditionParameters(view, node);
        }
        return view;
    }

    private void AddConditionPicker(VisualElement header, ScenarioTriggerConditionNodeData node)
    {
        TriggerConditionDefinition current = _library?.FindCondition(node.ConditionId);
        Button picker = ReferencePickerButton(
            string.Empty,
            DefinitionLabel(current?.DisplayNameKo, node.ConditionId));
        picker.AddToClassList("sm-condition-picker");
        picker.clicked += () => SequenceReferencePickerPopup.Show(
            picker,
            "Trigger Condition 선택",
            BuildConditionOptions(),
            node.ConditionId,
            id =>
            {
                if (id == node.ConditionId)
                {
                    return;
                }
            ModifyNode(node.NodeId, next =>
            {
                next.ConditionId = id;
                next.ParametersJson = DefaultConditionParameters(
                    _library?.FindCondition(id)).ToString(Formatting.None);
            });
            });
        header.Add(picker);
    }

    private void AddConditionParameters(
        VisualElement parent,
        ScenarioTriggerConditionNodeData node)
    {
        TriggerConditionDefinition definition = _library?.FindCondition(node.ConditionId);
        if (definition != null)
        {
            parent.Add(DefinitionHelp(definition.DescriptionKo, definition.UsageKo));
        }
        JObject parameters = ParseObject(node.ParametersJson);
        if (definition?.Parameters == null || definition.Parameters.Count == 0)
        {
            return;
        }
        for (int i = 0; i < definition.Parameters.Count; i++)
        {
            TriggerFieldDefinition field = definition.Parameters[i];
            if (field == null || string.IsNullOrWhiteSpace(field.FieldId))
            {
                continue;
            }
            parameters.TryGetValue(field.FieldId, out JToken value);
            ActionCatalogParameter parameter = TriggerParameter(field, false);
            parent.Add(ParameterFieldFactory.Create(
                parameter,
                value,
                _fieldContext,
                next => SetConditionParameter(node.NodeId, field.FieldId, next)));
        }
    }

    private void AddDoSection()
    {
        Add(FlowHeading("DO", "조건이 맞으면 실행", "sm-rule-heading--do"));
        ActionSequenceAsset current = FindSequence(_rule.SequenceId);
        Button sequenceField = ReferencePickerButton(
            "시퀀스",
            DefinitionLabel(current?.DisplayNameKo, _rule.SequenceId));
        sequenceField.clicked += () => SequenceReferencePickerPopup.Show(
            sequenceField,
            "실행할 Action Sequence 선택",
            BuildSequenceOptions(),
            _rule.SequenceId,
            id =>
            {
                SetRule(next =>
                {
                    next.SequenceId = id;
                    next.TargetInputsJson = "{}";
                });
            });
        Add(sequenceField);
        AddTargetInputs();
    }

    private void AddTargetInputs()
    {
        ActionSequenceAsset target = FindSequence(_rule.SequenceId);
        IReadOnlyList<SequenceInputDefinition> inputs = target?.Contract?.Inputs
            ?? (IReadOnlyList<SequenceInputDefinition>)Array.Empty<SequenceInputDefinition>();
        if (inputs.Count == 0)
        {
            Add(Empty("대상 시퀀스가 요구하는 입력값이 없습니다."));
            return;
        }
        JObject values = ParseObject(_rule.TargetInputsJson);
        var heading = new Label("전달할 입력값");
        heading.AddToClassList("sm-mini-label");
        Add(heading);
        for (int i = 0; i < inputs.Count; i++)
        {
            SequenceInputDefinition input = inputs[i];
            if (input == null || string.IsNullOrWhiteSpace(input.InputId))
            {
                continue;
            }
            values.TryGetValue(input.InputId, out JToken value);
            var parameter = new ActionCatalogParameter
            {
                Name = input.InputId,
                DisplayNameKo = input.DisplayNameKo,
                DescriptionKo = input.DescriptionKo,
                Type = input.TypeId,
                EditorControlId = ControlForType(input.TypeId),
                Required = input.Required,
                DefaultValue = input.DefaultValueJson,
                ValueSources =
                {
                    "literal", "input", "event", "session", "memory", "flag", "context", "result"
                }
            };
            Add(ParameterFieldFactory.Create(
                parameter,
                value,
                _fieldContext,
                next => SetTargetInput(input.InputId, next)));
        }
    }

    private void AddSimulator()
    {
        var foldout = new Foldout { text = "조건 시험", value = false };
        foldout.AddToClassList("sm-rule-simulator");
        SimulationState state = GetSimulationState();
        ScenarioEventDefinition definition = _library?.FindEvent(_rule.EventId);
        MergePayloadDefaults(state, definition);

        var intro = new Label("실제 게임 상태를 바꾸지 않고 이 규칙이 실행될지 확인합니다.");
        intro.AddToClassList("sm-inspector-description");
        foldout.Add(intro);
        if (definition?.Payload != null)
        {
            for (int i = 0; i < definition.Payload.Count; i++)
            {
                TriggerFieldDefinition field = definition.Payload[i];
                if (field == null || string.IsNullOrWhiteSpace(field.FieldId))
                {
                    continue;
                }
                state.Payload.TryGetValue(field.FieldId, out JToken value);
                foldout.Add(ParameterFieldFactory.Create(
                    TriggerParameter(field, true),
                    value,
                    _fieldContext,
                    next => state.Payload[field.FieldId] = next));
            }
        }
        var context = new TextField("Context / Memory JSON")
        {
            value = state.ContextJson,
            multiline = true,
            isDelayed = true
        };
        context.tooltip = "예: { \"memory\": { \"meetCount\": 2 }, \"flag\": { \"quest\": true } }";
        context.RegisterValueChangedCallback(evt => state.ContextJson = evt.newValue ?? "{}");
        foldout.Add(context);
        var fired = new Toggle("이미 실행된 once 규칙으로 시험") { value = state.AlreadyFired };
        fired.RegisterValueChangedCallback(evt => state.AlreadyFired = evt.newValue);
        foldout.Add(fired);
        var resultHost = new VisualElement();
        resultHost.AddToClassList("sm-rule-simulation-result");
        var run = new Button(() => RunSimulation(state, resultHost)) { text = "조건 시험 실행" };
        run.AddToClassList("sm-rule-primary-small");
        foldout.Add(run);
        foldout.Add(resultHost);
        Add(foldout);
    }

    private void RunSimulation(SimulationState state, VisualElement host)
    {
        TriggerRuleSimulationResult result = new TriggerRuleSimulator().Simulate(
            _rule,
            new TriggerRuleSimulationRequest
            {
                EventId = _rule.EventId,
                PayloadJson = state.Payload.ToString(Formatting.None),
                ContextValuesJson = state.ContextJson,
                RuleAlreadyFired = state.AlreadyFired
            });
        host.Clear();
        var summary = new Label(result.Message);
        summary.AddToClassList("sm-simulation-summary");
        summary.AddToClassList(result.Status == TriggerRuleSimulationStatus.Matched
            ? "is-match"
            : result.Status == TriggerRuleSimulationStatus.Error ? "is-error" : "is-no-match");
        host.Add(summary);
        for (int i = 0; i < result.Traces.Count; i++)
        {
            TriggerConditionSimulationTrace trace = result.Traces[i];
            var row = new Label((trace.Matched ? "통과  " : "불일치  ")
                + trace.ConditionId
                + (string.IsNullOrWhiteSpace(trace.Error) ? string.Empty : "  ·  " + trace.Error));
            row.AddToClassList("sm-simulation-trace");
            row.AddToClassList(trace.Matched ? "is-match" : "is-no-match");
            host.Add(row);
        }
    }

    private void RenderLegacy()
    {
        var badge = new Label("기존 호환 규칙");
        badge.AddToClassList("sm-rule-legacy-banner");
        Add(badge);
        var title = new Label(string.IsNullOrWhiteSpace(_legacyRule.RuleId)
            ? "기존 규칙 " + (_legacyIndex + 1)
            : _legacyRule.RuleId);
        title.AddToClassList("sm-rule-editor-title");
        Add(title);
        if (!BattleTriggerRuleCompatibilityMapper.TryMap(
                _legacyRule,
                out ScenarioTriggerRuleData mapped,
                out string error))
        {
            Add(DefinitionHelp(error, string.Empty));
            return;
        }
        var sentence = new Label(TriggerRuleSentenceFormatter.Format(
            mapped,
            _library,
            SequenceDisplayName));
        sentence.AddToClassList("sm-rule-sentence");
        Add(sentence);
        Add(ReadOnly("Event", mapped.EventId));
        Add(ReadOnly("실행 시점", TriggerRuleSentenceFormatter.Timing(mapped.Timing, mapped.CheckpointId)));
        Add(ReadOnly("실행 횟수", TriggerRuleSentenceFormatter.Once(mapped.Once)));
        Add(ReadOnly("대상 시퀀스", mapped.SequenceId));
        var convert = new Button(() => ConvertLegacyRequested?.Invoke(_legacyIndex))
        {
            text = "확장 Trigger Rule로 변환"
        };
        convert.AddToClassList("sm-rule-primary-small");
        Add(convert);
    }

    private void AddCondition(string parentId)
    {
        TriggerConditionDefinition definition = _library?.Conditions != null
            && _library.Conditions.Count > 0 ? _library.Conditions[0] : null;
        ModifyNode(parentId, parent =>
        {
            parent.Children = parent.Children ?? new List<ScenarioTriggerConditionNodeData>();
            parent.Children.Add(new ScenarioTriggerConditionNodeData
            {
                NodeId = ScenarioTriggerIdentity.Create(),
                Kind = ScenarioConditionNodeKind.Condition,
                ConditionId = definition?.ConditionId ?? string.Empty,
                ParametersJson = DefaultConditionParameters(definition).ToString(Formatting.None)
            });
        });
    }

    private void AddGroup(string parentId)
    {
        ModifyNode(parentId, parent =>
        {
            parent.Children = parent.Children ?? new List<ScenarioTriggerConditionNodeData>();
            parent.Children.Add(NewGroup());
        });
    }

    private void DeleteNode(string parentId, int index)
    {
        ModifyNode(parentId, parent =>
        {
            if (parent.Children != null && index >= 0 && index < parent.Children.Count)
            {
                parent.Children.RemoveAt(index);
            }
        });
    }

    private void MoveNode(string parentId, int source, int target)
    {
        ModifyNode(parentId, parent =>
        {
            if (parent.Children == null
                || source < 0 || source >= parent.Children.Count
                || target < 0 || target >= parent.Children.Count)
            {
                return;
            }
            ScenarioTriggerConditionNodeData node = parent.Children[source];
            parent.Children.RemoveAt(source);
            parent.Children.Insert(target, node);
        });
    }

    private void SetConditionParameter(string nodeId, string fieldId, JToken value)
    {
        ModifyNode(nodeId, node =>
        {
            JObject parameters = ParseObject(node.ParametersJson);
            parameters[fieldId] = value?.DeepClone() ?? JValue.CreateNull();
            node.ParametersJson = parameters.ToString(Formatting.None);
        });
    }

    private void SetTargetInput(string inputId, JToken value)
    {
        SetRule(next =>
        {
            JObject inputs = ParseObject(next.TargetInputsJson);
            inputs[inputId] = value?.DeepClone() ?? JValue.CreateNull();
            next.TargetInputsJson = inputs.ToString(Formatting.None);
        });
    }

    private void ModifyNode(string nodeId, Action<ScenarioTriggerConditionNodeData> change)
    {
        SetRule(next =>
        {
            if (!TryFindNode(next.Conditions, nodeId, out ScenarioTriggerConditionNodeData node))
            {
                throw new InvalidOperationException("조건 노드를 찾지 못했습니다: " + nodeId);
            }
            change(node);
        });
    }

    private void SetRule(Action<ScenarioTriggerRuleData> change)
    {
        if (_rule == null || _commands == null)
        {
            return;
        }
        try
        {
            ScenarioTriggerRuleData next = ScenarioTriggerIdentity.CloneRule(_rule);
            change(next);
            _commands.Execute(BattleScenarioEditCommands.ReplaceTriggerRule(_rule.RuleId, next));
            EditApplied?.Invoke();
        }
        catch (Exception exception)
        {
            Error?.Invoke(exception.Message);
        }
    }

    private ActionSequenceAsset FindSequence(string sequenceId)
    {
        if (_battle?.Sequences == null)
        {
            return null;
        }
        for (int i = 0; i < _battle.Sequences.Count; i++)
        {
            if (_battle.Sequences[i]?.SequenceId == sequenceId)
            {
                return _battle.Sequences[i];
            }
        }
        return null;
    }

    private string SequenceDisplayName(string sequenceId)
    {
        ActionSequenceAsset sequence = FindSequence(sequenceId);
        return !string.IsNullOrWhiteSpace(sequence?.DisplayNameKo)
            ? sequence.DisplayNameKo
            : sequenceId;
    }

    private SimulationState GetSimulationState()
    {
        string key = _rule.RuleId ?? string.Empty;
        if (!_simulationStates.TryGetValue(key, out SimulationState state))
        {
            state = new SimulationState();
            _simulationStates[key] = state;
        }
        return state;
    }

    private static void MergePayloadDefaults(
        SimulationState state,
        ScenarioEventDefinition definition)
    {
        if (definition?.Payload == null)
        {
            return;
        }
        for (int i = 0; i < definition.Payload.Count; i++)
        {
            TriggerFieldDefinition field = definition.Payload[i];
            if (field == null || string.IsNullOrWhiteSpace(field.FieldId)
                || state.Payload.ContainsKey(field.FieldId))
            {
                continue;
            }
            state.Payload[field.FieldId] = DefaultToken(field.DefaultValueJson, field.TypeId);
        }
    }

    private static ActionCatalogParameter TriggerParameter(
        TriggerFieldDefinition field,
        bool forceLiteral)
    {
        var result = new ActionCatalogParameter
        {
            Name = field.FieldId,
            DisplayNameKo = field.DisplayNameKo,
            DescriptionKo = field.DescriptionKo,
            Type = NormalizeTriggerType(field.TypeId),
            EditorControlId = NormalizeTriggerControl(field.EditorControlId, field.TypeId),
            Required = field.Required,
            DefaultValue = field.DefaultValueJson,
            PlaceholderKo = field.PlaceholderKo,
            HasMinimum = field.HasMinimum,
            Minimum = field.Minimum,
            HasMaximum = field.HasMaximum,
            Maximum = field.Maximum,
            UnitKo = field.UnitKo
        };
        if (field.Options != null) result.Options.AddRange(field.Options);
        if (forceLiteral)
        {
            result.ValueSources.Add("literal");
        }
        else if (field.ValueSources != null)
        {
            result.ValueSources.AddRange(field.ValueSources);
        }
        return result;
    }

    private static JObject DefaultConditionParameters(TriggerConditionDefinition definition)
    {
        var result = new JObject();
        if (definition?.Parameters == null)
        {
            return result;
        }
        for (int i = 0; i < definition.Parameters.Count; i++)
        {
            TriggerFieldDefinition field = definition.Parameters[i];
            if (field == null || string.IsNullOrWhiteSpace(field.FieldId))
            {
                continue;
            }
            if (!string.IsNullOrWhiteSpace(field.DefaultValueJson))
            {
                result[field.FieldId] = DefaultToken(field.DefaultValueJson, field.TypeId);
            }
            else if (field.Required && field.Options != null && field.Options.Count > 0)
            {
                result[field.FieldId] = field.Options[0];
            }
        }
        return result;
    }

    private static JToken DefaultToken(string json, string type)
    {
        if (!string.IsNullOrWhiteSpace(json))
        {
            try { return JToken.Parse(json); }
            catch { return new JValue(json); }
        }
        string normalized = NormalizeTriggerType(type);
        if (normalized == "bool") return new JValue(false);
        if (normalized == "number" || normalized == "int") return new JValue(0);
        if (normalized == "json") return JValue.CreateNull();
        return new JValue(string.Empty);
    }

    private static bool TryFindNode(
        ScenarioTriggerConditionNodeData root,
        string nodeId,
        out ScenarioTriggerConditionNodeData result)
    {
        result = null;
        if (root == null)
        {
            return false;
        }
        if (root.NodeId == nodeId)
        {
            result = root;
            return true;
        }
        if (root.Children != null)
        {
            for (int i = 0; i < root.Children.Count; i++)
            {
                if (TryFindNode(root.Children[i], nodeId, out result))
                {
                    return true;
                }
            }
        }
        return false;
    }

    private static ScenarioTriggerConditionNodeData NewGroup()
    {
        return new ScenarioTriggerConditionNodeData
        {
            NodeId = ScenarioTriggerIdentity.Create(),
            Kind = ScenarioConditionNodeKind.Group,
            GroupMode = ScenarioConditionGroupMode.All,
            Children = new List<ScenarioTriggerConditionNodeData>()
        };
    }

    private static JObject ParseObject(string json)
    {
        try { return string.IsNullOrWhiteSpace(json) ? new JObject() : JObject.Parse(json); }
        catch { return new JObject(); }
    }

    private static VisualElement FlowHeading(string key, string title, string className)
    {
        var heading = new VisualElement();
        heading.AddToClassList("sm-rule-flow-heading");
        heading.AddToClassList(className);
        var badge = new Label(key);
        badge.AddToClassList("sm-rule-flow-key");
        heading.Add(badge);
        var copy = new Label(title);
        copy.AddToClassList("sm-rule-flow-title");
        heading.Add(copy);
        return heading;
    }

    private static VisualElement DefinitionHelp(string description, string usage)
    {
        var help = new VisualElement();
        help.AddToClassList("sm-rule-definition-help");
        if (!string.IsNullOrWhiteSpace(description)) help.Add(new Label(description.Trim()));
        if (!string.IsNullOrWhiteSpace(usage))
        {
            var when = new Label("사용: " + usage.Trim());
            when.AddToClassList("sm-rule-definition-usage");
            help.Add(when);
        }
        return help;
    }

    private static VisualElement ReadOnly(string label, string value)
    {
        return new TextField(label) { value = value ?? string.Empty, isReadOnly = true };
    }

    private static DropdownField Dropdown(
        string label,
        List<string> choices,
        string value,
        Action<string> changed)
    {
        int index = Math.Max(0, choices.IndexOf(value));
        var field = new DropdownField(label, choices, index);
        field.RegisterValueChangedCallback(evt => changed(evt.newValue));
        return field;
    }

    private static Button NodeCommand(string text, string tooltip, Action clicked, bool enabled)
    {
        var button = new Button(clicked) { text = text, tooltip = tooltip };
        button.AddToClassList("sm-condition-command");
        button.SetEnabled(enabled);
        return button;
    }

    private List<SequenceReferencePickerOption> BuildEventOptions()
    {
        var options = new List<SequenceReferencePickerOption>();
        if (_library?.Events == null)
        {
            return options;
        }
        for (int i = 0; i < _library.Events.Count; i++)
        {
            ScenarioEventDefinition definition = _library.Events[i];
            if (definition == null || string.IsNullOrWhiteSpace(definition.EventId))
            {
                continue;
            }
            var option = new SequenceReferencePickerOption
            {
                Id = definition.EventId,
                DisplayNameKo = definition.DisplayNameKo,
                Category = definition.Category,
                DescriptionKo = definition.DescriptionKo,
                Deprecated = definition.Deprecated
            };
            AddKeywords(option, definition.Tags);
            AddKeywords(option, definition.Aliases);
            options.Add(option);
        }
        return options;
    }

    private List<SequenceReferencePickerOption> BuildConditionOptions()
    {
        var options = new List<SequenceReferencePickerOption>();
        if (_library?.Conditions == null)
        {
            return options;
        }
        for (int i = 0; i < _library.Conditions.Count; i++)
        {
            TriggerConditionDefinition definition = _library.Conditions[i];
            if (definition == null || string.IsNullOrWhiteSpace(definition.ConditionId))
            {
                continue;
            }
            var option = new SequenceReferencePickerOption
            {
                Id = definition.ConditionId,
                DisplayNameKo = definition.DisplayNameKo,
                Category = definition.Category,
                DescriptionKo = definition.DescriptionKo,
                Deprecated = definition.Deprecated
            };
            AddKeywords(option, definition.Tags);
            AddKeywords(option, definition.Aliases);
            options.Add(option);
        }
        return options;
    }

    private List<SequenceReferencePickerOption> BuildSequenceOptions()
    {
        var options = new List<SequenceReferencePickerOption>();
        if (_battle?.Sequences == null)
        {
            return options;
        }
        for (int i = 0; i < _battle.Sequences.Count; i++)
        {
            ActionSequenceAsset sequence = _battle.Sequences[i];
            if (sequence == null || string.IsNullOrWhiteSpace(sequence.SequenceId))
            {
                continue;
            }
            var option = new SequenceReferencePickerOption
            {
                Id = sequence.SequenceId,
                DisplayNameKo = sequence.DisplayNameKo,
                Category = "Action Sequence",
                DescriptionKo = sequence.Contract?.DescriptionKo ?? string.Empty,
                Deprecated = sequence.Contract?.Lifecycle == ActionSequenceLifecycle.Deprecated
            };
            AddKeywords(option, sequence.Contract?.Tags);
            options.Add(option);
        }
        return options;
    }

    private static Button ReferencePickerButton(string label, string value)
    {
        var button = new Button
        {
            text = (string.IsNullOrWhiteSpace(label) ? string.Empty : label + "  ·  ")
                + (string.IsNullOrWhiteSpace(value) ? "선택" : value)
                + "   ▾",
            tooltip = "클릭해서 검색하고 선택"
        };
        button.AddToClassList("sm-rule-reference-picker");
        return button;
    }

    private static void AddKeywords(
        SequenceReferencePickerOption option,
        IList<string> values)
    {
        if (option == null || values == null)
        {
            return;
        }
        for (int i = 0; i < values.Count; i++)
        {
            if (!string.IsNullOrWhiteSpace(values[i]))
            {
                option.Keywords.Add(values[i]);
            }
        }
    }

    private static void EnsureCurrentChoice(
        List<string> ids,
        List<string> labels,
        string id,
        string label)
    {
        if (!string.IsNullOrWhiteSpace(id) && !ids.Contains(id))
        {
            ids.Insert(0, id);
            labels.Insert(0, DefinitionLabel(label, id));
        }
        if (ids.Count == 0)
        {
            ids.Add(string.Empty);
            labels.Add("선택 항목 없음");
        }
    }

    private static string DefinitionLabel(string name, string id)
    {
        return (!string.IsNullOrWhiteSpace(name) ? name.Trim() : id) + "  ·  " + id;
    }

    private static string DisplayName(ScenarioTriggerRuleData rule)
    {
        return !string.IsNullOrWhiteSpace(rule?.DisplayNameKo)
            ? rule.DisplayNameKo.Trim()
            : (!string.IsNullOrWhiteSpace(rule?.RuleId) ? rule.RuleId : "이름 없는 규칙");
    }

    private static string ControlForType(string type)
    {
        string normalized = (type ?? string.Empty).Trim().ToLowerInvariant();
        if (normalized == "bool") return "toggle";
        if (normalized == "int" || normalized == "integer") return "number";
        if (normalized == "number" || normalized == "float" || normalized == "duration") return "number";
        if (normalized == "color") return "color";
        if (normalized == "vector2" || normalized == "vector3") return normalized;
        if (normalized == "json" || normalized == "object") return "json";
        if (normalized.EndsWith("ref", StringComparison.Ordinal))
        {
            return normalized.Substring(0, normalized.Length - 3).ToLowerInvariant();
        }
        return "text";
    }

    private static string NormalizeTriggerType(string type)
    {
        string value = (type ?? string.Empty).Trim().ToLowerInvariant();
        if (value == "integer") return "int";
        if (value == "ratio") return "number";
        if (value == "json") return "json";
        if (value.Contains("actor") || value.Contains("participant") || value.Contains("subject")) return "actorRef";
        if (value.Contains("module")) return "moduleRef";
        if (value.Contains("dialogue")) return "dialogueRef";
        if (value.Contains("audio")) return "audioRef";
        return value == "comparison_operator" ? "enum" : (string.IsNullOrEmpty(value) ? "string" : value);
    }

    private static string NormalizeTriggerControl(string control, string type)
    {
        string value = (control ?? string.Empty).Trim().ToLowerInvariant();
        if (value == "dropdown") return "enum";
        if (value == "typed_value") return "json";
        if (value.Contains("participant") || value.Contains("subject") || value.Contains("actor")) return "actor";
        if (value.Contains("module")) return "module";
        if (value == "number" || value == "integer") return "number";
        if (value.Contains("picker")) return "text";
        return string.IsNullOrEmpty(value) ? ControlForType(NormalizeTriggerType(type)) : value;
    }

    private static List<string> TimingLabels()
    {
        return new List<string> { "즉시", "현재 액션 종료 후", "현재 스킬 종료 후", "현재 모듈 종료 후", "이름 있는 체크포인트" };
    }

    private static string TimingLabel(ScenarioTriggerTiming timing)
    {
        switch (timing)
        {
            case ScenarioTriggerTiming.AfterCurrentAction: return "현재 액션 종료 후";
            case ScenarioTriggerTiming.AfterCurrentSkill: return "현재 스킬 종료 후";
            case ScenarioTriggerTiming.AfterCurrentModule: return "현재 모듈 종료 후";
            case ScenarioTriggerTiming.Checkpoint: return "이름 있는 체크포인트";
            default: return "즉시";
        }
    }

    private static ScenarioTriggerTiming ParseTiming(string value)
    {
        switch (value)
        {
            case "현재 액션 종료 후": return ScenarioTriggerTiming.AfterCurrentAction;
            case "현재 스킬 종료 후": return ScenarioTriggerTiming.AfterCurrentSkill;
            case "현재 모듈 종료 후": return ScenarioTriggerTiming.AfterCurrentModule;
            case "이름 있는 체크포인트": return ScenarioTriggerTiming.Checkpoint;
            default: return ScenarioTriggerTiming.Immediate;
        }
    }

    private static List<string> OnceLabels()
    {
        return new List<string> { "매번", "현재 실행에서 한 번", "이 만남에서 한 번", "저장 데이터에서 한 번" };
    }

    private static string OnceLabel(ScenarioTriggerOnceScope once)
    {
        return TriggerRuleSentenceFormatter.Once(once);
    }

    private static ScenarioTriggerOnceScope ParseOnce(string value)
    {
        switch (value)
        {
            case "매번": return ScenarioTriggerOnceScope.Always;
            case "이 만남에서 한 번": return ScenarioTriggerOnceScope.EncounterMemory;
            case "저장 데이터에서 한 번": return ScenarioTriggerOnceScope.Save;
            default: return ScenarioTriggerOnceScope.Session;
        }
    }

    private static Label Empty(string text)
    {
        var label = new Label(text ?? string.Empty);
        label.AddToClassList("sm-inspector-empty");
        return label;
    }

    private sealed class SimulationState
    {
        public JObject Payload = new JObject();
        public string ContextJson = "{}";
        public bool AlreadyFired;
    }
}
