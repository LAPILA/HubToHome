using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

public static class SequenceEditCommands
{
    public static ISequenceEditCommand Insert(
        string parentBlockId,
        int index,
        ScenarioActionData action)
    {
        return new InsertSequenceBlockCommand(parentBlockId, index, action);
    }

    public static ISequenceEditCommand Move(
        string blockId,
        string targetParentBlockId,
        int targetIndex)
    {
        return new MoveSequenceBlockCommand(blockId, targetParentBlockId, targetIndex);
    }

    public static ISequenceEditCommand Duplicate(string blockId)
    {
        return new DuplicateSequenceBlockCommand(blockId);
    }

    public static ISequenceEditCommand Delete(string blockId)
    {
        return new DeleteSequenceBlockCommand(blockId);
    }

    public static ISequenceEditCommand SetEnabled(string blockId, bool enabled)
    {
        return new SetSequenceBlockEnabledCommand(blockId, enabled);
    }

    public static ISequenceEditCommand SetParameters(string blockId, string parametersJson)
    {
        return new SetSequenceBlockParametersCommand(blockId, parametersJson);
    }

    public static ISequenceEditCommand SetActionId(string blockId, string actionId)
    {
        return new SetSequenceBlockTextCommand(
            blockId,
            SequenceBlockTextField.ActionId,
            actionId,
            "블록 액션 변경");
    }

    public static ISequenceEditCommand SetDesignerLabel(string blockId, string label)
    {
        return new SetSequenceBlockTextCommand(
            blockId,
            SequenceBlockTextField.DesignerLabel,
            label,
            "블록 이름 변경");
    }

    public static ISequenceEditCommand SetNote(string blockId, string note)
    {
        return new SetSequenceBlockTextCommand(
            blockId,
            SequenceBlockTextField.Note,
            note,
            "블록 메모 변경");
    }

    public static ISequenceEditCommand WrapInParallel(
        IEnumerable<string> blockIds,
        string wrapperBlockId = "",
        string parametersJson = "{\"policy\":\"all\"}")
    {
        return new WrapSequenceBlocksInParallelCommand(
            blockIds,
            wrapperBlockId,
            parametersJson);
    }

    public static ISequenceEditCommand ReplaceWith(
        IEnumerable<string> blockIds,
        ScenarioActionData replacement,
        string commandName = "블록 교체")
    {
        return new ReplaceSequenceBlocksCommand(blockIds, replacement, commandName);
    }

    public static ISequenceEditCommand ExtractToSequence(
        IEnumerable<string> blockIds,
        ScenarioActionData callBlock,
        BattleScenarioData owner,
        ActionSequenceAsset extractedSequence)
    {
        return new ExtractSequenceEditCommand(
            blockIds,
            callBlock,
            owner,
            extractedSequence);
    }

    public static ISequenceEditCommand SetSequenceDisplayName(string displayNameKo)
    {
        return new SetSequenceDisplayNameCommand(displayNameKo);
    }

    public static ISequenceEditCommand SetSequenceContract(ActionSequenceContractData contract)
    {
        return new SetSequenceContractCommand(contract);
    }

    public static ISequenceEditCommand RenameSequenceInput(
        string previousInputId,
        string nextInputId,
        ActionSequenceContractData nextContract)
    {
        return new RenameSequenceInputCommand(
            previousInputId,
            nextInputId,
            nextContract);
    }
}

internal sealed class SetSequenceDisplayNameCommand : ISequenceEditCommand
{
    private readonly string _next;
    private string _previous;

    public SetSequenceDisplayNameCommand(string displayNameKo)
    {
        _next = displayNameKo ?? string.Empty;
    }

    public string Name => "시퀀스 이름 변경";
    public string PreferredSelectionBlockId => string.Empty;

    public void Execute(ActionSequenceAsset sequence)
    {
        _previous = sequence.DisplayNameKo ?? string.Empty;
        sequence.DisplayNameKo = _next;
    }

    public void Undo(ActionSequenceAsset sequence)
    {
        sequence.DisplayNameKo = _previous ?? string.Empty;
    }
}

internal sealed class SetSequenceContractCommand : ISequenceEditCommand
{
    private readonly ActionSequenceContractData _next;
    private ActionSequenceContractData _previous;

    public SetSequenceContractCommand(ActionSequenceContractData contract)
    {
        _next = ActionSequenceContractData.CopyOf(contract);
    }

    public string Name => "시퀀스 계약 변경";
    public string PreferredSelectionBlockId => string.Empty;

    public void Execute(ActionSequenceAsset sequence)
    {
        _previous = ActionSequenceContractData.CopyOf(sequence.Contract);
        sequence.Contract = ActionSequenceContractData.CopyOf(_next);
    }

    public void Undo(ActionSequenceAsset sequence)
    {
        sequence.Contract = ActionSequenceContractData.CopyOf(_previous);
    }
}

internal sealed class RenameSequenceInputCommand : ISequenceEditCommand
{
    private readonly string _previousInputId;
    private readonly string _nextInputId;
    private readonly ActionSequenceContractData _nextContract;
    private readonly List<ParameterSnapshot> _previousParameters =
        new List<ParameterSnapshot>();
    private ActionSequenceContractData _previousContract;
    private bool _captured;

    public RenameSequenceInputCommand(
        string previousInputId,
        string nextInputId,
        ActionSequenceContractData nextContract)
    {
        _previousInputId = Normalize(previousInputId);
        _nextInputId = Normalize(nextInputId);
        _nextContract = ActionSequenceContractData.CopyOf(nextContract);
    }

    public string Name => "시퀀스 입력 ID와 binding 변경";
    public string PreferredSelectionBlockId => string.Empty;

    public void Execute(ActionSequenceAsset sequence)
    {
        if (!_captured)
        {
            _previousContract = ActionSequenceContractData.CopyOf(sequence.Contract);
            Capture(sequence.Actions);
            _captured = true;
        }
        sequence.Contract = ActionSequenceContractData.CopyOf(_nextContract);
        RewriteBindings(sequence.Actions, "input." + _previousInputId, "input." + _nextInputId);
    }

    public void Undo(ActionSequenceAsset sequence)
    {
        sequence.Contract = ActionSequenceContractData.CopyOf(_previousContract);
        for (int i = 0; i < _previousParameters.Count; i++)
        {
            ParameterSnapshot snapshot = _previousParameters[i];
            if (SequenceBlockTree.TryFind(sequence, snapshot.BlockId, out SequenceBlockLocation location))
            {
                location.Action.ParametersJson = snapshot.ParametersJson;
            }
        }
    }

    private void Capture(IList<ScenarioActionData> actions)
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
                continue;
            }
            _previousParameters.Add(new ParameterSnapshot(
                action.BlockId,
                action.ParametersJson));
            Capture(action.Children);
        }
    }

    private static void RewriteBindings(
        IList<ScenarioActionData> actions,
        string previousPath,
        string nextPath)
    {
        if (actions == null || string.IsNullOrEmpty(previousPath)
            || string.IsNullOrEmpty(nextPath))
        {
            return;
        }
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
                if (Rewrite(root, previousPath, nextPath))
                {
                    action.ParametersJson = root.ToString(Formatting.None);
                }
            }
            catch
            {
                // Existing malformed JSON remains untouched and is reported by validation.
            }
            RewriteBindings(action.Children, previousPath, nextPath);
        }
    }

    private static bool Rewrite(JToken token, string previousPath, string nextPath)
    {
        bool changed = false;
        if (token is JObject objectValue)
        {
            JToken binding = objectValue["$bind"];
            if (binding?.Type == JTokenType.String
                && string.Equals(
                    binding.Value<string>(),
                    previousPath,
                    StringComparison.Ordinal))
            {
                objectValue["$bind"] = nextPath;
                changed = true;
            }
            foreach (JProperty property in objectValue.Properties())
            {
                changed |= Rewrite(property.Value, previousPath, nextPath);
            }
        }
        else if (token is JArray array)
        {
            for (int i = 0; i < array.Count; i++)
            {
                changed |= Rewrite(array[i], previousPath, nextPath);
            }
        }
        return changed;
    }

    private static string Normalize(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
    }

    private sealed class ParameterSnapshot
    {
        public ParameterSnapshot(string blockId, string parametersJson)
        {
            BlockId = blockId ?? string.Empty;
            ParametersJson = parametersJson ?? string.Empty;
        }

        public string BlockId { get; }
        public string ParametersJson { get; }
    }
}

public sealed class SequenceBlockLocation
{
    internal SequenceBlockLocation(
        string parentBlockId,
        List<ScenarioActionData> list,
        int index,
        ScenarioActionData action)
    {
        ParentBlockId = parentBlockId ?? string.Empty;
        List = list;
        Index = index;
        Action = action;
    }

    public string ParentBlockId { get; }
    public List<ScenarioActionData> List { get; }
    public int Index { get; }
    public ScenarioActionData Action { get; }
}

public static class SequenceBlockTree
{
    public static bool Contains(ActionSequenceAsset sequence, string blockId)
    {
        return TryFind(sequence, blockId, out SequenceBlockLocation _);
    }

    public static bool TryFind(
        ActionSequenceAsset sequence,
        string blockId,
        out SequenceBlockLocation location)
    {
        location = null;
        if (sequence == null || sequence.Actions == null)
        {
            return false;
        }

        return TryFind(sequence.Actions, string.Empty, Normalize(blockId), out location);
    }

    public static bool TryResolveContainer(
        ActionSequenceAsset sequence,
        string parentBlockId,
        out List<ScenarioActionData> list)
    {
        list = null;
        if (sequence == null || sequence.Actions == null)
        {
            return false;
        }

        string parentId = Normalize(parentBlockId);
        if (string.IsNullOrEmpty(parentId))
        {
            list = sequence.Actions;
            return true;
        }

        if (!TryFind(sequence, parentId, out SequenceBlockLocation parent)
            || parent.Action == null)
        {
            return false;
        }

        list = parent.Action.Children ?? (parent.Action.Children = new List<ScenarioActionData>());
        return true;
    }

    public static bool TryValidateUniqueIds(
        ActionSequenceAsset sequence,
        out string error)
    {
        if (sequence == null)
        {
            error = "Action Sequence is required for editing.";
            return false;
        }

        if (sequence.Actions == null)
        {
            error = "Action Sequence actions list is missing.";
            return false;
        }

        var ids = new HashSet<string>(StringComparer.Ordinal);
        return TryValidateUniqueIds(sequence.Actions, ids, "actions", out error);
    }

    internal static bool Contains(ScenarioActionData root, string blockId)
    {
        if (root == null)
        {
            return false;
        }

        string target = Normalize(blockId);
        if (Normalize(root.BlockId) == target)
        {
            return true;
        }

        if (root.Children == null)
        {
            return false;
        }

        for (int i = 0; i < root.Children.Count; i++)
        {
            if (Contains(root.Children[i], target))
            {
                return true;
            }
        }

        return false;
    }

    internal static bool TryFindReference(
        ActionSequenceAsset sequence,
        ScenarioActionData target,
        out SequenceBlockLocation location)
    {
        location = null;
        return sequence != null
            && target != null
            && TryFindReference(sequence.Actions, string.Empty, target, out location);
    }

    private static bool TryFind(
        List<ScenarioActionData> actions,
        string parentBlockId,
        string blockId,
        out SequenceBlockLocation location)
    {
        location = null;
        if (actions == null || string.IsNullOrEmpty(blockId))
        {
            return false;
        }

        for (int i = 0; i < actions.Count; i++)
        {
            ScenarioActionData action = actions[i];
            if (action == null)
            {
                continue;
            }

            if (Normalize(action.BlockId) == blockId)
            {
                location = new SequenceBlockLocation(parentBlockId, actions, i, action);
                return true;
            }

            if (TryFind(action.Children, action.BlockId, blockId, out location))
            {
                return true;
            }
        }

        return false;
    }

    private static bool TryFindReference(
        List<ScenarioActionData> actions,
        string parentBlockId,
        ScenarioActionData target,
        out SequenceBlockLocation location)
    {
        location = null;
        if (actions == null)
        {
            return false;
        }

        for (int i = 0; i < actions.Count; i++)
        {
            ScenarioActionData action = actions[i];
            if (ReferenceEquals(action, target))
            {
                location = new SequenceBlockLocation(parentBlockId, actions, i, action);
                return true;
            }

            if (action != null
                && TryFindReference(action.Children, action.BlockId, target, out location))
            {
                return true;
            }
        }

        return false;
    }

    private static bool TryValidateUniqueIds(
        IList<ScenarioActionData> actions,
        HashSet<string> ids,
        string path,
        out string error)
    {
        if (actions == null)
        {
            error = string.Empty;
            return true;
        }

        for (int i = 0; i < actions.Count; i++)
        {
            ScenarioActionData action = actions[i];
            string actionPath = path + "[" + i + "]";
            if (action == null)
            {
                error = "Sequence contains a missing block at " + actionPath + ".";
                return false;
            }

            string blockId = Normalize(action.BlockId);
            if (string.IsNullOrEmpty(blockId))
            {
                error = "Sequence block ID is missing at " + actionPath + ".";
                return false;
            }

            if (!ids.Add(blockId))
            {
                error = "Sequence contains a duplicate block ID: " + blockId;
                return false;
            }

            if (!TryValidateUniqueIds(action.Children, ids, actionPath + ".children", out error))
            {
                return false;
            }
        }

        error = string.Empty;
        return true;
    }

    private static string Normalize(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
    }
}

internal abstract class SequenceEditCommandBase : ISequenceEditCommand
{
    protected SequenceEditCommandBase(string name)
    {
        Name = name;
    }

    public string Name { get; }
    public virtual string PreferredSelectionBlockId => string.Empty;

    public abstract void Execute(ActionSequenceAsset sequence);
    public abstract void Undo(ActionSequenceAsset sequence);

    protected static SequenceBlockLocation RequireBlock(
        ActionSequenceAsset sequence,
        string blockId)
    {
        if (!SequenceBlockTree.TryFind(sequence, blockId, out SequenceBlockLocation location))
        {
            throw new InvalidOperationException("Sequence block was not found: " + blockId);
        }

        return location;
    }

    protected static List<ScenarioActionData> RequireContainer(
        ActionSequenceAsset sequence,
        string parentBlockId)
    {
        if (!SequenceBlockTree.TryResolveContainer(sequence, parentBlockId, out List<ScenarioActionData> list))
        {
            throw new InvalidOperationException(
                "Sequence block container was not found: " + parentBlockId);
        }

        return list;
    }
}

internal sealed class InsertSequenceBlockCommand : SequenceEditCommandBase
{
    private readonly string _parentBlockId;
    private readonly int _index;
    private readonly ScenarioActionData _action;

    public InsertSequenceBlockCommand(
        string parentBlockId,
        int index,
        ScenarioActionData action)
        : base("블록 추가")
    {
        _parentBlockId = parentBlockId ?? string.Empty;
        _index = index;
        _action = action ?? throw new ArgumentNullException(nameof(action));
    }

    public override string PreferredSelectionBlockId => _action.BlockId;

    public override void Execute(ActionSequenceAsset sequence)
    {
        List<ScenarioActionData> list = RequireContainer(sequence, _parentBlockId);
        if (_index < 0 || _index > list.Count)
        {
            throw new InvalidOperationException("Insert index is outside the target block list.");
        }

        if (SequenceBlockTree.TryFindReference(sequence, _action, out SequenceBlockLocation _))
        {
            throw new InvalidOperationException("The inserted block already belongs to this sequence.");
        }

        list.Insert(_index, _action);
    }

    public override void Undo(ActionSequenceAsset sequence)
    {
        if (SequenceBlockTree.TryFindReference(sequence, _action, out SequenceBlockLocation location))
        {
            location.List.RemoveAt(location.Index);
        }
    }
}

internal sealed class MoveSequenceBlockCommand : SequenceEditCommandBase
{
    private readonly string _blockId;
    private readonly string _targetParentBlockId;
    private readonly int _targetIndex;
    private string _originalParentBlockId;
    private int _originalIndex;
    private ScenarioActionData _action;
    private bool _captured;

    public MoveSequenceBlockCommand(
        string blockId,
        string targetParentBlockId,
        int targetIndex)
        : base("블록 이동")
    {
        _blockId = blockId ?? string.Empty;
        _targetParentBlockId = targetParentBlockId ?? string.Empty;
        _targetIndex = targetIndex;
    }

    public override void Execute(ActionSequenceAsset sequence)
    {
        SequenceBlockLocation source = _action != null
            && SequenceBlockTree.TryFindReference(sequence, _action, out SequenceBlockLocation current)
                ? current
                : RequireBlock(sequence, _blockId);
        _action = source.Action;
        if (!_captured)
        {
            _originalParentBlockId = source.ParentBlockId;
            _originalIndex = source.Index;
            _captured = true;
        }

        if (!string.IsNullOrWhiteSpace(_targetParentBlockId)
            && SequenceBlockTree.Contains(_action, _targetParentBlockId))
        {
            throw new InvalidOperationException("A block cannot be moved inside its own subtree.");
        }

        source.List.RemoveAt(source.Index);
        try
        {
            List<ScenarioActionData> target = RequireContainer(sequence, _targetParentBlockId);
            if (_targetIndex < 0 || _targetIndex > target.Count)
            {
                throw new InvalidOperationException("Move index is outside the target block list.");
            }

            target.Insert(_targetIndex, _action);
        }
        catch
        {
            source.List.Insert(Math.Min(source.Index, source.List.Count), _action);
            throw;
        }
    }

    public override void Undo(ActionSequenceAsset sequence)
    {
        if (_action == null
            || !SequenceBlockTree.TryFindReference(sequence, _action, out SequenceBlockLocation current))
        {
            return;
        }

        current.List.RemoveAt(current.Index);
        List<ScenarioActionData> original = RequireContainer(sequence, _originalParentBlockId);
        original.Insert(Math.Min(_originalIndex, original.Count), _action);
    }
}

internal sealed class DuplicateSequenceBlockCommand : SequenceEditCommandBase
{
    private readonly string _sourceBlockId;
    private ScenarioActionData _clone;

    public DuplicateSequenceBlockCommand(string sourceBlockId)
        : base("블록 복제")
    {
        _sourceBlockId = sourceBlockId ?? string.Empty;
    }

    public override string PreferredSelectionBlockId => _clone?.BlockId ?? string.Empty;

    public override void Execute(ActionSequenceAsset sequence)
    {
        SequenceBlockLocation source = RequireBlock(sequence, _sourceBlockId);
        _clone = _clone ?? ScenarioBlockIdentity.CloneWithNewIds(source.Action);
        if (SequenceBlockTree.TryFindReference(sequence, _clone, out SequenceBlockLocation _))
        {
            throw new InvalidOperationException("The duplicated block already belongs to this sequence.");
        }

        source.List.Insert(source.Index + 1, _clone);
    }

    public override void Undo(ActionSequenceAsset sequence)
    {
        if (_clone != null
            && SequenceBlockTree.TryFindReference(sequence, _clone, out SequenceBlockLocation location))
        {
            location.List.RemoveAt(location.Index);
        }
    }
}

internal sealed class DeleteSequenceBlockCommand : SequenceEditCommandBase
{
    private readonly string _blockId;
    private string _parentBlockId;
    private int _index;
    private ScenarioActionData _action;
    private string _fallbackSelection = string.Empty;
    private bool _captured;

    public DeleteSequenceBlockCommand(string blockId)
        : base("블록 삭제")
    {
        _blockId = blockId ?? string.Empty;
    }

    public override string PreferredSelectionBlockId => _fallbackSelection;

    public override void Execute(ActionSequenceAsset sequence)
    {
        SequenceBlockLocation location = _action != null
            && SequenceBlockTree.TryFindReference(sequence, _action, out SequenceBlockLocation current)
                ? current
                : RequireBlock(sequence, _blockId);
        if (!_captured)
        {
            _parentBlockId = location.ParentBlockId;
            _index = location.Index;
            _action = location.Action;
            _fallbackSelection = location.Index + 1 < location.List.Count
                ? location.List[location.Index + 1]?.BlockId
                : (location.Index > 0
                    ? location.List[location.Index - 1]?.BlockId
                    : location.ParentBlockId);
            _captured = true;
        }

        location.List.RemoveAt(location.Index);
    }

    public override void Undo(ActionSequenceAsset sequence)
    {
        if (_action == null)
        {
            return;
        }

        List<ScenarioActionData> list = RequireContainer(sequence, _parentBlockId);
        if (!SequenceBlockTree.TryFindReference(sequence, _action, out SequenceBlockLocation _))
        {
            list.Insert(Math.Min(_index, list.Count), _action);
        }
    }
}

internal sealed class SetSequenceBlockEnabledCommand : SequenceEditCommandBase
{
    private readonly string _blockId;
    private readonly bool _enabled;
    private bool _previousDisabled;
    private bool _captured;

    public SetSequenceBlockEnabledCommand(string blockId, bool enabled)
        : base(enabled ? "블록 활성화" : "블록 비활성화")
    {
        _blockId = blockId ?? string.Empty;
        _enabled = enabled;
    }

    public override void Execute(ActionSequenceAsset sequence)
    {
        ScenarioActionData action = RequireBlock(sequence, _blockId).Action;
        if (!_captured)
        {
            _previousDisabled = action.Disabled;
            _captured = true;
        }

        action.Disabled = !_enabled;
    }

    public override void Undo(ActionSequenceAsset sequence)
    {
        if (sequence != null && SequenceBlockTree.TryFind(sequence, _blockId, out SequenceBlockLocation location))
        {
            location.Action.Disabled = _previousDisabled;
        }
    }
}

internal sealed class SetSequenceBlockParametersCommand : SequenceEditCommandBase
{
    private readonly string _blockId;
    private readonly string _parametersJson;
    private string _previousParameters;
    private bool _captured;

    public SetSequenceBlockParametersCommand(string blockId, string parametersJson)
        : base("블록 파라미터 변경")
    {
        _blockId = blockId ?? string.Empty;
        _parametersJson = parametersJson ?? "{}";
    }

    public override void Execute(ActionSequenceAsset sequence)
    {
        ScenarioActionData action = RequireBlock(sequence, _blockId).Action;
        if (!_captured)
        {
            _previousParameters = action.ParametersJson ?? "{}";
            _captured = true;
        }

        action.ParametersJson = _parametersJson;
    }

    public override void Undo(ActionSequenceAsset sequence)
    {
        if (sequence != null && SequenceBlockTree.TryFind(sequence, _blockId, out SequenceBlockLocation location))
        {
            location.Action.ParametersJson = _previousParameters ?? "{}";
        }
    }
}

internal enum SequenceBlockTextField
{
    ActionId,
    DesignerLabel,
    Note
}

internal sealed class SetSequenceBlockTextCommand : SequenceEditCommandBase
{
    private readonly string _blockId;
    private readonly SequenceBlockTextField _field;
    private readonly string _value;
    private string _previousValue;
    private bool _captured;

    public SetSequenceBlockTextCommand(
        string blockId,
        SequenceBlockTextField field,
        string value,
        string commandName)
        : base(commandName)
    {
        _blockId = blockId ?? string.Empty;
        _field = field;
        _value = value ?? string.Empty;
    }

    public override void Execute(ActionSequenceAsset sequence)
    {
        ScenarioActionData action = RequireBlock(sequence, _blockId).Action;
        if (!_captured)
        {
            _previousValue = Read(action);
            _captured = true;
        }

        Write(action, _value);
    }

    public override void Undo(ActionSequenceAsset sequence)
    {
        if (sequence != null
            && SequenceBlockTree.TryFind(sequence, _blockId, out SequenceBlockLocation location))
        {
            Write(location.Action, _previousValue);
        }
    }

    private string Read(ScenarioActionData action)
    {
        switch (_field)
        {
            case SequenceBlockTextField.ActionId:
                return action.ActionId ?? string.Empty;
            case SequenceBlockTextField.DesignerLabel:
                return action.DesignerLabel ?? string.Empty;
            default:
                return action.Note ?? string.Empty;
        }
    }

    private void Write(ScenarioActionData action, string value)
    {
        switch (_field)
        {
            case SequenceBlockTextField.ActionId:
                action.ActionId = value ?? string.Empty;
                break;
            case SequenceBlockTextField.DesignerLabel:
                action.DesignerLabel = value ?? string.Empty;
                break;
            default:
                action.Note = value ?? string.Empty;
                break;
        }
    }
}

internal abstract class ReplaceSequenceBlockRangeCommandBase : SequenceEditCommandBase
{
    private readonly List<string> _blockIds;
    private readonly ScenarioActionData _replacement;
    private List<ScenarioActionData> _replaced;
    private string _parentBlockId = string.Empty;
    private int _firstIndex;
    private bool _captured;

    protected ReplaceSequenceBlockRangeCommandBase(
        IEnumerable<string> blockIds,
        ScenarioActionData replacement,
        string commandName)
        : base(commandName)
    {
        _blockIds = NormalizeIds(blockIds);
        _replacement = replacement ?? throw new ArgumentNullException(nameof(replacement));
        if (_blockIds.Count == 0)
        {
            throw new ArgumentException("At least one block ID is required.", nameof(blockIds));
        }
    }

    public override string PreferredSelectionBlockId => _replacement.BlockId;

    public override void Execute(ActionSequenceAsset sequence)
    {
        if (!_captured)
        {
            CaptureRange(sequence);
            _captured = true;
        }

        List<ScenarioActionData> list = RequireContainer(sequence, _parentBlockId);
        RemoveReplacedBlocks(sequence, list);
        if (SequenceBlockTree.TryFindReference(sequence, _replacement, out SequenceBlockLocation _))
        {
            throw new InvalidOperationException("The replacement block already belongs to this sequence.");
        }

        list.Insert(Math.Min(_firstIndex, list.Count), _replacement);
    }

    public override void Undo(ActionSequenceAsset sequence)
    {
        if (!_captured)
        {
            return;
        }

        if (SequenceBlockTree.TryFindReference(
                sequence,
                _replacement,
                out SequenceBlockLocation replacementLocation))
        {
            replacementLocation.List.RemoveAt(replacementLocation.Index);
        }

        List<ScenarioActionData> list = RequireContainer(sequence, _parentBlockId);
        int index = Math.Min(_firstIndex, list.Count);
        for (int i = 0; i < _replaced.Count; i++)
        {
            if (!SequenceBlockTree.TryFindReference(
                    sequence,
                    _replaced[i],
                    out SequenceBlockLocation _))
            {
                list.Insert(index++, _replaced[i]);
            }
        }
    }

    protected IReadOnlyList<ScenarioActionData> ReplacedBlocks => _replaced;
    protected ScenarioActionData Replacement => _replacement;

    private void CaptureRange(ActionSequenceAsset sequence)
    {
        var locations = new List<SequenceBlockLocation>();
        List<ScenarioActionData> commonList = null;
        string commonParent = string.Empty;
        for (int i = 0; i < _blockIds.Count; i++)
        {
            SequenceBlockLocation location = RequireBlock(sequence, _blockIds[i]);
            if (commonList == null)
            {
                commonList = location.List;
                commonParent = location.ParentBlockId;
            }
            else if (!ReferenceEquals(commonList, location.List))
            {
                throw new InvalidOperationException(
                    "묶거나 교체할 블록은 같은 부모 안에 있어야 합니다.");
            }

            locations.Add(location);
        }

        locations.Sort((left, right) => left.Index.CompareTo(right.Index));
        for (int i = 1; i < locations.Count; i++)
        {
            if (locations[i].Index != locations[i - 1].Index + 1)
            {
                throw new InvalidOperationException(
                    "묶거나 교체할 블록은 서로 이어져 있어야 합니다.");
            }
        }

        _parentBlockId = commonParent;
        _firstIndex = locations[0].Index;
        _replaced = new List<ScenarioActionData>(locations.Count);
        for (int i = 0; i < locations.Count; i++)
        {
            _replaced.Add(locations[i].Action);
        }
    }

    private void RemoveReplacedBlocks(
        ActionSequenceAsset sequence,
        List<ScenarioActionData> list)
    {
        for (int i = _replaced.Count - 1; i >= 0; i--)
        {
            if (SequenceBlockTree.TryFindReference(
                    sequence,
                    _replaced[i],
                    out SequenceBlockLocation location))
            {
                if (!ReferenceEquals(location.List, list))
                {
                    throw new InvalidOperationException(
                        "A replaced block moved to another parent before redo.");
                }

                location.List.RemoveAt(location.Index);
            }
        }
    }

    private static List<string> NormalizeIds(IEnumerable<string> blockIds)
    {
        var result = new List<string>();
        if (blockIds == null)
        {
            return result;
        }

        foreach (string blockId in blockIds)
        {
            string normalized = string.IsNullOrWhiteSpace(blockId)
                ? string.Empty
                : blockId.Trim();
            if (!string.IsNullOrEmpty(normalized) && !result.Contains(normalized))
            {
                result.Add(normalized);
            }
        }

        return result;
    }
}

internal sealed class WrapSequenceBlocksInParallelCommand :
    ReplaceSequenceBlockRangeCommandBase
{
    public WrapSequenceBlocksInParallelCommand(
        IEnumerable<string> blockIds,
        string wrapperBlockId,
        string parametersJson)
        : base(
            blockIds,
            new ScenarioActionData
            {
                BlockId = string.IsNullOrWhiteSpace(wrapperBlockId)
                    ? ScenarioBlockIdentity.Create()
                    : wrapperBlockId.Trim(),
                ActionId = ActionDirector.ParallelActionId,
                DesignerLabel = "동시 실행",
                ParametersJson = string.IsNullOrWhiteSpace(parametersJson)
                    ? "{\"policy\":\"all\"}"
                    : parametersJson
            },
            "블록을 동시 실행으로 묶기")
    {
    }

    public override void Execute(ActionSequenceAsset sequence)
    {
        base.Execute(sequence);
        Replacement.Children.Clear();
        for (int i = 0; i < ReplacedBlocks.Count; i++)
        {
            Replacement.Children.Add(ReplacedBlocks[i]);
        }
    }
}

internal sealed class ReplaceSequenceBlocksCommand :
    ReplaceSequenceBlockRangeCommandBase
{
    public ReplaceSequenceBlocksCommand(
        IEnumerable<string> blockIds,
        ScenarioActionData replacement,
        string commandName)
        : base(blockIds, replacement, commandName)
    {
    }
}

internal sealed class ExtractSequenceEditCommand : ISequenceEditCommand
{
    private readonly ReplaceSequenceBlocksCommand _replace;
    private readonly BattleScenarioData _owner;
    private readonly ActionSequenceAsset _extracted;

    public ExtractSequenceEditCommand(
        IEnumerable<string> blockIds,
        ScenarioActionData callBlock,
        BattleScenarioData owner,
        ActionSequenceAsset extracted)
    {
        _owner = owner ?? throw new ArgumentNullException(nameof(owner));
        _extracted = extracted ?? throw new ArgumentNullException(nameof(extracted));
        _replace = new ReplaceSequenceBlocksCommand(
            blockIds,
            callBlock,
            "새 시퀀스로 추출");
    }

    public string Name => "새 시퀀스로 추출";
    public string PreferredSelectionBlockId => _replace.PreferredSelectionBlockId;

    public void Execute(ActionSequenceAsset sequence)
    {
        _replace.Execute(sequence);
        if (_owner.Sequences == null)
        {
            _owner.Sequences = new List<ActionSequenceAsset>();
        }

        if (!_owner.Sequences.Contains(_extracted))
        {
            _owner.Sequences.Add(_extracted);
        }

        UnityEditor.EditorUtility.SetDirty(_owner);
        UnityEditor.EditorUtility.SetDirty(_extracted);
    }

    public void Undo(ActionSequenceAsset sequence)
    {
        _replace.Undo(sequence);
        _owner.Sequences?.Remove(_extracted);
        UnityEditor.EditorUtility.SetDirty(_owner);
    }
}
