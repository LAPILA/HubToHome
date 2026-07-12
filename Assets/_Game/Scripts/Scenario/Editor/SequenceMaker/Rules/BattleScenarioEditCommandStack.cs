using System;
using System.Collections.Generic;
using UnityEditor;

public interface IBattleScenarioEditCommand
{
    string Name { get; }
    string PreferredRuleId { get; }
    void Execute(BattleScenarioData scenario);
    void Undo(BattleScenarioData scenario);
}

public sealed class BattleScenarioEditCommandStack : ISequenceMakerEditHistory
{
    private readonly BattleScenarioData _scenario;
    private readonly List<HistoryEntry> _undo = new List<HistoryEntry>();
    private readonly List<HistoryEntry> _redo = new List<HistoryEntry>();
    private long _nextStateId;
    private long _currentStateId;
    private long _savedStateId;

    public BattleScenarioEditCommandStack(BattleScenarioData scenario)
    {
        _scenario = scenario ?? throw new ArgumentNullException(nameof(scenario));
    }

    public event Action<SequenceEditChange> Changed;

    public bool CanUndo => _undo.Count > 0;
    public bool CanRedo => _redo.Count > 0;
    public bool IsDirty => _currentStateId != _savedStateId;
    public string UndoLabel => CanUndo ? _undo[_undo.Count - 1].Command.Name : string.Empty;
    public string RedoLabel => CanRedo ? _redo[_redo.Count - 1].Command.Name : string.Empty;
    public string PreferredRuleId { get; private set; } = string.Empty;

    public void Execute(IBattleScenarioEditCommand command)
    {
        if (command == null)
        {
            throw new ArgumentNullException(nameof(command));
        }

        command.Execute(_scenario);
        long before = _currentStateId;
        long after = ++_nextStateId;
        _undo.Add(new HistoryEntry(command, before, after));
        _redo.Clear();
        _currentStateId = after;
        PreferredRuleId = command.PreferredRuleId ?? string.Empty;
        EditorUtility.SetDirty(_scenario);
        Raise(SequenceEditChangeReason.Execute, command.Name);
    }

    public bool Undo()
    {
        if (!CanUndo)
        {
            return false;
        }

        HistoryEntry entry = _undo[_undo.Count - 1];
        _undo.RemoveAt(_undo.Count - 1);
        entry.Command.Undo(_scenario);
        _currentStateId = entry.BeforeStateId;
        _redo.Add(entry);
        PreferredRuleId = entry.Command.PreferredRuleId ?? string.Empty;
        EditorUtility.SetDirty(_scenario);
        Raise(SequenceEditChangeReason.Undo, entry.Command.Name);
        return true;
    }

    public bool Redo()
    {
        if (!CanRedo)
        {
            return false;
        }

        HistoryEntry entry = _redo[_redo.Count - 1];
        _redo.RemoveAt(_redo.Count - 1);
        entry.Command.Execute(_scenario);
        _currentStateId = entry.AfterStateId;
        _undo.Add(entry);
        PreferredRuleId = entry.Command.PreferredRuleId ?? string.Empty;
        EditorUtility.SetDirty(_scenario);
        Raise(SequenceEditChangeReason.Redo, entry.Command.Name);
        return true;
    }

    public void MarkSaved()
    {
        _savedStateId = _currentStateId;
        Raise(SequenceEditChangeReason.Saved, string.Empty);
    }

    private void Raise(SequenceEditChangeReason reason, string label)
    {
        Changed?.Invoke(new SequenceEditChange(reason, label, IsDirty));
    }

    private sealed class HistoryEntry
    {
        public HistoryEntry(
            IBattleScenarioEditCommand command,
            long beforeStateId,
            long afterStateId)
        {
            Command = command;
            BeforeStateId = beforeStateId;
            AfterStateId = afterStateId;
        }

        public IBattleScenarioEditCommand Command { get; }
        public long BeforeStateId { get; }
        public long AfterStateId { get; }
    }
}

public static class BattleScenarioEditCommands
{
    public static IBattleScenarioEditCommand AddTriggerRule(
        ScenarioTriggerRuleData rule,
        int index = -1)
    {
        return new AddTriggerRuleCommand(rule, index);
    }

    public static IBattleScenarioEditCommand ReplaceTriggerRule(
        string ruleId,
        ScenarioTriggerRuleData replacement)
    {
        return new ReplaceTriggerRuleCommand(ruleId, replacement);
    }

    public static IBattleScenarioEditCommand DeleteTriggerRule(string ruleId)
    {
        return new DeleteTriggerRuleCommand(ruleId);
    }

    public static IBattleScenarioEditCommand MoveTriggerRule(string ruleId, int targetIndex)
    {
        return new MoveTriggerRuleCommand(ruleId, targetIndex);
    }

    public static IBattleScenarioEditCommand ConvertLegacyRule(
        int legacyIndex,
        ScenarioTriggerRuleData mapped)
    {
        return new ConvertLegacyTriggerRuleCommand(legacyIndex, mapped);
    }
}

public static class ScenarioTriggerRuleFactory
{
    public static ScenarioTriggerRuleData Create(string eventId = "", string sequenceId = "")
    {
        string suffix = ScenarioTriggerIdentity.Create().Substring(0, 8);
        var rule = new ScenarioTriggerRuleData
        {
            RuleId = "rule." + suffix,
            DisplayNameKo = "새 규칙",
            EventId = eventId ?? string.Empty,
            SequenceId = sequenceId ?? string.Empty,
            Timing = ScenarioTriggerTiming.Immediate,
            Once = ScenarioTriggerOnceScope.Session,
            TargetInputsJson = "{}",
            Conditions = new ScenarioTriggerConditionNodeData
            {
                NodeId = ScenarioTriggerIdentity.Create(),
                Kind = ScenarioConditionNodeKind.Group,
                GroupMode = ScenarioConditionGroupMode.All
            }
        };
        return rule;
    }

    public static ScenarioTriggerRuleData CloneWithNewIds(
        ScenarioTriggerRuleData source,
        string ruleId)
    {
        ScenarioTriggerRuleData clone = ScenarioTriggerIdentity.CloneRule(source)
            ?? Create();
        clone.RuleId = string.IsNullOrWhiteSpace(ruleId)
            ? "rule." + ScenarioTriggerIdentity.Create().Substring(0, 8)
            : ruleId.Trim();
        clone.DisplayNameKo = string.IsNullOrWhiteSpace(clone.DisplayNameKo)
            ? "복제 규칙"
            : clone.DisplayNameKo + " 복사본";
        clone.Conditions = ScenarioTriggerIdentity.CloneWithNewIds(clone.Conditions);
        return clone;
    }
}

internal sealed class AddTriggerRuleCommand : IBattleScenarioEditCommand
{
    private readonly ScenarioTriggerRuleData _rule;
    private readonly int _requestedIndex;
    private int _actualIndex;

    public AddTriggerRuleCommand(ScenarioTriggerRuleData rule, int index)
    {
        _rule = ScenarioTriggerIdentity.CloneRule(rule)
            ?? throw new ArgumentNullException(nameof(rule));
        _requestedIndex = index;
    }

    public string Name => "Trigger Rule 추가";
    public string PreferredRuleId => _rule.RuleId;

    public void Execute(BattleScenarioData scenario)
    {
        scenario.TriggerRules = scenario.TriggerRules ?? new List<ScenarioTriggerRuleData>();
        _actualIndex = _requestedIndex < 0
            ? scenario.TriggerRules.Count
            : Math.Max(0, Math.Min(_requestedIndex, scenario.TriggerRules.Count));
        ScenarioTriggerIdentity.EnsureUnique(
            _rule.Conditions,
            scenario.ScenarioId + "|" + _rule.RuleId);
        scenario.TriggerRules.Insert(_actualIndex, _rule);
    }

    public void Undo(BattleScenarioData scenario)
    {
        RemoveRule(scenario.TriggerRules, _rule.RuleId, _rule);
    }

    internal static void RemoveRule(
        List<ScenarioTriggerRuleData> rules,
        string ruleId,
        ScenarioTriggerRuleData instance = null)
    {
        if (rules == null)
        {
            return;
        }
        for (int i = rules.Count - 1; i >= 0; i--)
        {
            if ((instance != null && ReferenceEquals(rules[i], instance))
                || string.Equals(rules[i]?.RuleId, ruleId, StringComparison.Ordinal))
            {
                rules.RemoveAt(i);
                return;
            }
        }
    }
}

internal sealed class ReplaceTriggerRuleCommand : IBattleScenarioEditCommand
{
    private readonly string _ruleId;
    private readonly ScenarioTriggerRuleData _replacement;
    private ScenarioTriggerRuleData _previous;
    private int _index = -1;

    public ReplaceTriggerRuleCommand(string ruleId, ScenarioTriggerRuleData replacement)
    {
        _ruleId = ruleId ?? string.Empty;
        _replacement = ScenarioTriggerIdentity.CloneRule(replacement)
            ?? throw new ArgumentNullException(nameof(replacement));
    }

    public string Name => "Trigger Rule 변경";
    public string PreferredRuleId => _replacement.RuleId;

    public void Execute(BattleScenarioData scenario)
    {
        if (_index < 0)
        {
            _index = FindRuleIndex(scenario.TriggerRules, _ruleId);
            if (_index < 0)
            {
                throw new InvalidOperationException("Trigger Rule을 찾지 못했습니다: " + _ruleId);
            }
            _previous = ScenarioTriggerIdentity.CloneRule(scenario.TriggerRules[_index]);
        }
        if (_index >= scenario.TriggerRules.Count)
        {
            throw new InvalidOperationException("Trigger Rule 위치가 변경되어 적용할 수 없습니다.");
        }
        ScenarioTriggerRuleData next = ScenarioTriggerIdentity.CloneRule(_replacement);
        ScenarioTriggerIdentity.EnsureUnique(
            next.Conditions,
            scenario.ScenarioId + "|" + next.RuleId);
        scenario.TriggerRules[_index] = next;
    }

    public void Undo(BattleScenarioData scenario)
    {
        scenario.TriggerRules[_index] = ScenarioTriggerIdentity.CloneRule(_previous);
    }

    internal static int FindRuleIndex(List<ScenarioTriggerRuleData> rules, string ruleId)
    {
        if (rules == null)
        {
            return -1;
        }
        for (int i = 0; i < rules.Count; i++)
        {
            if (string.Equals(rules[i]?.RuleId, ruleId, StringComparison.Ordinal))
            {
                return i;
            }
        }
        return -1;
    }
}

internal sealed class DeleteTriggerRuleCommand : IBattleScenarioEditCommand
{
    private readonly string _ruleId;
    private ScenarioTriggerRuleData _removed;
    private int _index = -1;

    public DeleteTriggerRuleCommand(string ruleId)
    {
        _ruleId = ruleId ?? string.Empty;
    }

    public string Name => "Trigger Rule 삭제";
    public string PreferredRuleId => _ruleId;

    public void Execute(BattleScenarioData scenario)
    {
        _index = ReplaceTriggerRuleCommand.FindRuleIndex(scenario.TriggerRules, _ruleId);
        if (_index < 0)
        {
            throw new InvalidOperationException("Trigger Rule을 찾지 못했습니다: " + _ruleId);
        }
        _removed = ScenarioTriggerIdentity.CloneRule(scenario.TriggerRules[_index]);
        scenario.TriggerRules.RemoveAt(_index);
    }

    public void Undo(BattleScenarioData scenario)
    {
        scenario.TriggerRules.Insert(
            Math.Max(0, Math.Min(_index, scenario.TriggerRules.Count)),
            ScenarioTriggerIdentity.CloneRule(_removed));
    }
}

internal sealed class MoveTriggerRuleCommand : IBattleScenarioEditCommand
{
    private readonly string _ruleId;
    private readonly int _targetIndex;
    private int _sourceIndex;

    public MoveTriggerRuleCommand(string ruleId, int targetIndex)
    {
        _ruleId = ruleId ?? string.Empty;
        _targetIndex = targetIndex;
    }

    public string Name => "Trigger Rule 순서 변경";
    public string PreferredRuleId => _ruleId;

    public void Execute(BattleScenarioData scenario)
    {
        _sourceIndex = ReplaceTriggerRuleCommand.FindRuleIndex(scenario.TriggerRules, _ruleId);
        Move(scenario.TriggerRules, _sourceIndex, _targetIndex);
    }

    public void Undo(BattleScenarioData scenario)
    {
        int current = ReplaceTriggerRuleCommand.FindRuleIndex(scenario.TriggerRules, _ruleId);
        Move(scenario.TriggerRules, current, _sourceIndex);
    }

    private static void Move(List<ScenarioTriggerRuleData> rules, int source, int target)
    {
        if (rules == null || source < 0 || source >= rules.Count)
        {
            throw new InvalidOperationException("이동할 Trigger Rule을 찾지 못했습니다.");
        }
        ScenarioTriggerRuleData rule = rules[source];
        rules.RemoveAt(source);
        rules.Insert(Math.Max(0, Math.Min(target, rules.Count)), rule);
    }
}

internal sealed class ConvertLegacyTriggerRuleCommand : IBattleScenarioEditCommand
{
    private readonly int _legacyIndex;
    private readonly ScenarioTriggerRuleData _mapped;
    private BattleEventRuleData _legacy;

    public ConvertLegacyTriggerRuleCommand(int legacyIndex, ScenarioTriggerRuleData mapped)
    {
        _legacyIndex = legacyIndex;
        _mapped = ScenarioTriggerIdentity.CloneRule(mapped)
            ?? throw new ArgumentNullException(nameof(mapped));
    }

    public string Name => "기존 규칙 변환";
    public string PreferredRuleId => _mapped.RuleId;

    public void Execute(BattleScenarioData scenario)
    {
        if (scenario.Rules == null || _legacyIndex < 0 || _legacyIndex >= scenario.Rules.Count)
        {
            throw new InvalidOperationException("변환할 기존 규칙을 찾지 못했습니다.");
        }
        _legacy = CopyLegacy(scenario.Rules[_legacyIndex]);
        scenario.Rules.RemoveAt(_legacyIndex);
        scenario.TriggerRules = scenario.TriggerRules ?? new List<ScenarioTriggerRuleData>();
        ScenarioTriggerRuleData mapped = ScenarioTriggerIdentity.CloneRule(_mapped);
        ScenarioTriggerIdentity.EnsureUnique(
            mapped.Conditions,
            scenario.ScenarioId + "|" + mapped.RuleId);
        scenario.TriggerRules.Add(mapped);
    }

    public void Undo(BattleScenarioData scenario)
    {
        AddTriggerRuleCommand.RemoveRule(scenario.TriggerRules, _mapped.RuleId);
        scenario.Rules.Insert(
            Math.Max(0, Math.Min(_legacyIndex, scenario.Rules.Count)),
            CopyLegacy(_legacy));
    }

    private static BattleEventRuleData CopyLegacy(BattleEventRuleData source)
    {
        if (source == null)
        {
            return new BattleEventRuleData();
        }
        return new BattleEventRuleData
        {
            RuleId = source.RuleId ?? string.Empty,
            EventType = source.EventType,
            Timing = source.Timing,
            Once = source.Once,
            SubjectId = source.SubjectId ?? string.Empty,
            OutcomeId = source.OutcomeId ?? string.Empty,
            ThresholdRatio = source.ThresholdRatio,
            SequenceId = source.SequenceId ?? string.Empty,
            Disabled = source.Disabled
        };
    }
}
