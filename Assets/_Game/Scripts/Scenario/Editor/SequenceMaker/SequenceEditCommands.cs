using System;
using System.Collections.Generic;

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
