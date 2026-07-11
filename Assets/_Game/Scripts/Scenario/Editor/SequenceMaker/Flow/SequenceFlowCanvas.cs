using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

public sealed class SequenceInsertionRequest
{
    public SequenceInsertionRequest(string parentBlockId, int insertionIndex)
    {
        ParentBlockId = parentBlockId ?? string.Empty;
        InsertionIndex = insertionIndex;
    }

    public string ParentBlockId { get; }
    public int InsertionIndex { get; }
}

public sealed class SequenceFlowCanvas : VisualElement
{
    private readonly HashSet<string> _collapsed = new HashSet<string>(StringComparer.Ordinal);
    private readonly HashSet<string> _bookmarks = new HashSet<string>(StringComparer.Ordinal);
    private readonly HashSet<string> _breakpoints = new HashSet<string>(StringComparer.Ordinal);
    private readonly Dictionary<string, ActionBlockView> _blockViews =
        new Dictionary<string, ActionBlockView>(StringComparer.Ordinal);

    private ActionSequenceAsset _sequence;
    private SequenceEditCommandStack _commands;
    private ActionCatalogAsset _catalog;
    private ScenarioValidationResult _validation;
    private SequenceFlowProjection _projection;
    private string _searchQuery = string.Empty;
    private string _selectionAnchorBlockId = string.Empty;

    public SequenceFlowCanvas()
    {
        AddToClassList("sm-flow-canvas");
        focusable = true;
        tabIndex = 0;
        RegisterCallback<KeyDownEvent>(OnKeyDown);
        RegisterCallback<PointerDownEvent>(_ => Focus());
    }

    public event Action<SequenceInsertionRequest> InsertRequested;
    public event Action<IReadOnlyList<string>> ExtractRequested;
    public event Action<string> InspectRequested;
    public event Action<string> Error;
    public event Action SaveRequested;
    public event Action Changed;

    public IReadOnlyCollection<string> Bookmarks => _bookmarks;
    public IReadOnlyCollection<string> Breakpoints => _breakpoints;
    public SequenceFlowProjection Projection => _projection;

    public void Bind(
        ActionSequenceAsset sequence,
        SequenceEditCommandStack commands,
        ActionCatalogAsset catalog,
        ScenarioValidationResult validation,
        string searchQuery)
    {
        if (_commands != commands)
        {
            if (_commands != null)
            {
                _commands.Changed -= OnCommandStackChanged;
            }

            _commands = commands;
            if (_commands != null)
            {
                _commands.Changed += OnCommandStackChanged;
            }
        }

        bool sequenceChanged = _sequence != sequence;
        _sequence = sequence;
        _catalog = catalog;
        _validation = validation;
        _searchQuery = Normalize(searchQuery);
        if (sequenceChanged)
        {
            _selectionAnchorBlockId = string.Empty;
        }

        Rebuild();
    }

    public void ClearBinding()
    {
        if (_commands != null)
        {
            _commands.Changed -= OnCommandStackChanged;
        }

        _sequence = null;
        _commands = null;
        _projection = null;
        Clear();
    }

    public void Refresh(
        ActionCatalogAsset catalog,
        ScenarioValidationResult validation,
        string searchQuery)
    {
        _catalog = catalog;
        _validation = validation;
        _searchQuery = Normalize(searchQuery);
        Rebuild();
    }

    public void SetExecutionState(
        string blockId,
        SequenceBlockExecutionVisualState state)
    {
        if (_blockViews.TryGetValue(Normalize(blockId), out ActionBlockView view))
        {
            view.SetExecutionState(state);
        }
    }

    public void ClearExecutionStates()
    {
        foreach (ActionBlockView view in _blockViews.Values)
        {
            view.SetExecutionState(SequenceBlockExecutionVisualState.None);
        }
    }

    public bool JumpToNextProblem()
    {
        if (_projection == null || _projection.VisibleNodes.Count == 0)
        {
            return false;
        }

        int start = -1;
        if (_commands != null
            && _projection.TryGetNode(
                _commands.PrimarySelectionBlockId,
                out SequenceFlowNode current))
        {
            start = current.VisibleIndex;
        }

        for (int offset = 1; offset <= _projection.VisibleNodes.Count; offset++)
        {
            int index = (start + offset) % _projection.VisibleNodes.Count;
            SequenceFlowNode candidate = _projection.VisibleNodes[index];
            if (candidate.ErrorCount > 0
                || candidate.WarningCount > 0
                || candidate.Summary.HasParameterError)
            {
                SelectOnly(candidate.BlockId);
                ScrollToBlock(candidate.BlockId);
                return true;
            }
        }

        return false;
    }

    public void CopySelection()
    {
        IReadOnlyList<string> ids = TopLevelSelection();
        if (ids.Count == 0)
        {
            return;
        }

        SequenceBlockClipboard.Copy(_sequence, ids);
    }

    public void CutSelection()
    {
        CopySelection();
        DeleteSelection();
    }

    public void PasteAfterSelection()
    {
        if (_sequence == null || _commands == null || !SequenceBlockClipboard.HasContent)
        {
            return;
        }

        string parentId = string.Empty;
        int index = _sequence.Actions != null ? _sequence.Actions.Count : 0;
        if (!string.IsNullOrWhiteSpace(_commands.PrimarySelectionBlockId)
            && SequenceBlockTree.TryFind(
                _sequence,
                _commands.PrimarySelectionBlockId,
                out SequenceBlockLocation location))
        {
            parentId = location.ParentBlockId;
            index = location.Index + 1;
        }

        IList<ScenarioActionData> clones = SequenceBlockClipboard.CreatePasteClones();
        var commands = new List<ISequenceEditCommand>();
        for (int i = 0; i < clones.Count; i++)
        {
            commands.Add(SequenceEditCommands.Insert(parentId, index + i, clones[i]));
        }

        ExecuteMany("블록 붙여넣기", commands);
    }

    public void DuplicateSelection()
    {
        IReadOnlyList<string> ids = TopLevelSelection();
        var commands = new List<ISequenceEditCommand>();
        for (int i = 0; i < ids.Count; i++)
        {
            commands.Add(SequenceEditCommands.Duplicate(ids[i]));
        }

        ExecuteMany("블록 복제", commands);
    }

    public void DeleteSelection()
    {
        IReadOnlyList<string> ids = TopLevelSelection();
        var commands = new List<ISequenceEditCommand>();
        for (int i = 0; i < ids.Count; i++)
        {
            commands.Add(SequenceEditCommands.Delete(ids[i]));
        }

        ExecuteMany("블록 삭제", commands);
    }

    public void WrapSelectionInParallel()
    {
        IReadOnlyList<string> ids = TopLevelSelection();
        if (ids.Count == 0)
        {
            return;
        }

        Execute(SequenceEditCommands.WrapInParallel(ids));
    }

    private void Rebuild()
    {
        Clear();
        _blockViews.Clear();
        if (_sequence == null || _commands == null)
        {
            AddEmpty("시퀀스 없음");
            return;
        }

        var selected = new HashSet<string>(
            _commands.SelectedBlockIds,
            StringComparer.Ordinal);
        _projection = SequenceFlowProjection.Build(
            _sequence,
            selected,
            _commands.PrimarySelectionBlockId,
            _collapsed,
            _validation,
            _catalog,
            _searchQuery);
        if (_projection.AllNodes.Count == 0)
        {
            AddEmpty("빈 시퀀스");
            if (string.IsNullOrEmpty(_searchQuery))
            {
                AddInsertionRail(string.Empty, 0, 0);
            }

            return;
        }

        RenderList(_sequence.Actions, string.Empty, 0);
    }

    private void RenderList(
        IList<ScenarioActionData> actions,
        string parentBlockId,
        int depth)
    {
        if (actions == null)
        {
            return;
        }

        bool showRails = string.IsNullOrEmpty(_searchQuery);
        if (showRails)
        {
            AddInsertionRail(parentBlockId, 0, depth);
        }

        for (int i = 0; i < actions.Count; i++)
        {
            ScenarioActionData action = actions[i];
            if (action == null
                || !_projection.TryGetNode(action.BlockId, out SequenceFlowNode node)
                || !node.IsVisible)
            {
                continue;
            }

            ActionBlockView view = node.IsStructural
                ? new StructuralBlockView(
                    node,
                    _bookmarks.Contains(node.BlockId),
                    _breakpoints.Contains(node.BlockId))
                : new ActionBlockView(
                    node,
                    _bookmarks.Contains(node.BlockId),
                    _breakpoints.Contains(node.BlockId));
            HookBlockView(view);
            Add(view);
            if (!_blockViews.ContainsKey(node.BlockId))
            {
                _blockViews.Add(node.BlockId, view);
            }

            if (action.Children != null
                && action.Children.Count > 0
                && (!node.IsCollapsed || !string.IsNullOrEmpty(_searchQuery)))
            {
                RenderList(action.Children, node.BlockId, depth + 1);
            }

            if (showRails)
            {
                AddInsertionRail(parentBlockId, i + 1, depth);
            }
        }
    }

    private void HookBlockView(ActionBlockView view)
    {
        view.SelectionRequested += Select;
        view.EnabledChanged += (node, enabled) =>
            Execute(SequenceEditCommands.SetEnabled(node.BlockId, enabled));
        view.CommandRequested += HandleBlockCommand;
        view.DragRequested += StartBlockDrag;
    }

    private void AddInsertionRail(string parentBlockId, int index, int depth)
    {
        var rail = new ActionInsertionRail(parentBlockId, index, depth);
        rail.InsertRequested += value => InsertRequested?.Invoke(
            new SequenceInsertionRequest(value.ParentBlockId, value.InsertionIndex));
        rail.BlockDropped += MoveDroppedBlock;
        Add(rail);
    }

    private void Select(SequenceFlowNode node, EventModifiers modifiers)
    {
        if (node == null || _commands == null || _projection == null)
        {
            return;
        }

        bool additive = (modifiers & EventModifiers.Control) != 0
            || (modifiers & EventModifiers.Command) != 0;
        bool range = (modifiers & EventModifiers.Shift) != 0;
        var selected = new List<string>(_commands.SelectedBlockIds);
        if (range)
        {
            int anchorIndex = FindVisibleIndex(_selectionAnchorBlockId);
            if (anchorIndex < 0)
            {
                anchorIndex = FindVisibleIndex(_commands.PrimarySelectionBlockId);
            }

            if (anchorIndex < 0)
            {
                anchorIndex = node.VisibleIndex;
            }

            if (!additive)
            {
                selected.Clear();
            }

            int start = Math.Min(anchorIndex, node.VisibleIndex);
            int end = Math.Max(anchorIndex, node.VisibleIndex);
            for (int i = start; i <= end; i++)
            {
                AddUnique(selected, _projection.VisibleNodes[i].BlockId);
            }
        }
        else if (additive)
        {
            if (selected.Contains(node.BlockId))
            {
                selected.Remove(node.BlockId);
            }
            else
            {
                selected.Add(node.BlockId);
            }

            _selectionAnchorBlockId = node.BlockId;
        }
        else
        {
            selected.Clear();
            selected.Add(node.BlockId);
            _selectionAnchorBlockId = node.BlockId;
        }

        _commands.SetSelection(selected, node.BlockId);
        InspectRequested?.Invoke(node.BlockId);
        Focus();
    }

    private void HandleBlockCommand(
        SequenceFlowNode node,
        SequenceBlockCommand command)
    {
        if (node == null || _commands == null)
        {
            return;
        }

        if (!node.IsSelected
            && command != SequenceBlockCommand.ToggleCollapse
            && command != SequenceBlockCommand.ToggleBookmark
            && command != SequenceBlockCommand.ToggleBreakpoint)
        {
            SelectOnly(node.BlockId);
        }

        switch (command)
        {
            case SequenceBlockCommand.MoveUp:
                MovePrimary(-1);
                break;
            case SequenceBlockCommand.MoveDown:
                MovePrimary(1);
                break;
            case SequenceBlockCommand.Duplicate:
                DuplicateSelection();
                break;
            case SequenceBlockCommand.Copy:
                CopySelection();
                break;
            case SequenceBlockCommand.Cut:
                CutSelection();
                break;
            case SequenceBlockCommand.PasteAfter:
                PasteAfterSelection();
                break;
            case SequenceBlockCommand.Delete:
                DeleteSelection();
                break;
            case SequenceBlockCommand.ToggleEnabled:
                ToggleSelectionEnabled();
                break;
            case SequenceBlockCommand.WrapParallel:
                WrapSelectionInParallel();
                break;
            case SequenceBlockCommand.ExtractSequence:
                ExtractRequested?.Invoke(TopLevelSelection());
                break;
            case SequenceBlockCommand.ToggleCollapse:
                ToggleSet(_collapsed, node.BlockId);
                Rebuild();
                break;
            case SequenceBlockCommand.ToggleBookmark:
                ToggleSet(_bookmarks, node.BlockId);
                Rebuild();
                break;
            case SequenceBlockCommand.ToggleBreakpoint:
                ToggleSet(_breakpoints, node.BlockId);
                Rebuild();
                break;
            case SequenceBlockCommand.EditNote:
                InspectRequested?.Invoke(node.BlockId);
                break;
        }
    }

    private void ToggleSelectionEnabled()
    {
        IReadOnlyList<string> ids = TopLevelSelection(false);
        if (ids.Count == 0)
        {
            return;
        }

        bool anyEnabled = false;
        for (int i = 0; i < ids.Count; i++)
        {
            if (SequenceBlockTree.TryFind(
                    _sequence,
                    ids[i],
                    out SequenceBlockLocation location)
                && !location.Action.Disabled)
            {
                anyEnabled = true;
                break;
            }
        }

        var commands = new List<ISequenceEditCommand>();
        for (int i = 0; i < ids.Count; i++)
        {
            commands.Add(SequenceEditCommands.SetEnabled(ids[i], !anyEnabled));
        }

        ExecuteMany(anyEnabled ? "블록 비활성화" : "블록 활성화", commands);
    }

    private void MovePrimary(int direction)
    {
        string blockId = _commands.PrimarySelectionBlockId;
        if (!SequenceBlockTree.TryFind(
                _sequence,
                blockId,
                out SequenceBlockLocation location))
        {
            return;
        }

        int targetIndex = location.Index + direction;
        if (targetIndex < 0 || targetIndex >= location.List.Count)
        {
            return;
        }

        Execute(SequenceEditCommands.Move(
            blockId,
            location.ParentBlockId,
            targetIndex));
    }

    private void StartBlockDrag(SequenceFlowNode node)
    {
        if (node == null)
        {
            return;
        }

        if (!node.IsSelected)
        {
            SelectOnly(node.BlockId);
        }

        DragAndDrop.PrepareStartDrag();
        DragAndDrop.SetGenericData(ActionInsertionRail.DragDataKey, node.BlockId);
        DragAndDrop.objectReferences = Array.Empty<UnityEngine.Object>();
        DragAndDrop.StartDrag(node.Summary.Title);
    }

    private void MoveDroppedBlock(ActionInsertionRail rail, string blockId)
    {
        if (!SequenceBlockTree.TryFind(
                _sequence,
                blockId,
                out SequenceBlockLocation source)
            || !SequenceBlockTree.TryResolveContainer(
                _sequence,
                rail.ParentBlockId,
                out List<ScenarioActionData> target))
        {
            return;
        }

        int targetIndex = rail.InsertionIndex;
        if (ReferenceEquals(source.List, target) && source.Index < targetIndex)
        {
            targetIndex--;
        }

        if (ReferenceEquals(source.List, target) && source.Index == targetIndex)
        {
            return;
        }

        Execute(SequenceEditCommands.Move(blockId, rail.ParentBlockId, targetIndex));
    }

    private void Execute(ISequenceEditCommand command)
    {
        if (_commands == null || command == null)
        {
            return;
        }

        try
        {
            _commands.Execute(command);
            Changed?.Invoke();
        }
        catch (Exception exception)
        {
            Error?.Invoke(exception.Message);
        }
    }

    private void ExecuteMany(
        string transactionName,
        IList<ISequenceEditCommand> commands)
    {
        if (_commands == null || commands == null || commands.Count == 0)
        {
            return;
        }

        if (commands.Count == 1)
        {
            Execute(commands[0]);
            return;
        }

        try
        {
            using (SequenceEditTransaction transaction = _commands.BeginTransaction(transactionName))
            {
                for (int i = 0; i < commands.Count; i++)
                {
                    _commands.Execute(commands[i]);
                }

                transaction.Commit();
            }

            Changed?.Invoke();
        }
        catch (Exception exception)
        {
            Error?.Invoke(exception.Message);
        }
    }

    private IReadOnlyList<string> TopLevelSelection(bool removeDescendants = true)
    {
        var selected = new HashSet<string>(
            _commands != null ? _commands.SelectedBlockIds : Array.Empty<string>(),
            StringComparer.Ordinal);
        var result = new List<string>();
        if (_projection == null)
        {
            return result;
        }

        for (int i = 0; i < _projection.AllNodes.Count; i++)
        {
            SequenceFlowNode node = _projection.AllNodes[i];
            if (!selected.Contains(node.BlockId))
            {
                continue;
            }

            if (removeDescendants && HasSelectedAncestor(node, selected))
            {
                continue;
            }

            result.Add(node.BlockId);
        }

        return result;
    }

    private bool HasSelectedAncestor(
        SequenceFlowNode node,
        ISet<string> selected)
    {
        string parentId = node.ParentBlockId;
        while (!string.IsNullOrEmpty(parentId))
        {
            if (selected.Contains(parentId))
            {
                return true;
            }

            parentId = _projection.TryGetNode(parentId, out SequenceFlowNode parent)
                ? parent.ParentBlockId
                : string.Empty;
        }

        return false;
    }

    private void SelectOnly(string blockId)
    {
        _selectionAnchorBlockId = blockId;
        _commands?.SetSelection(blockId);
        InspectRequested?.Invoke(blockId);
    }

    private void SelectRelative(int direction)
    {
        if (_projection == null || _projection.VisibleNodes.Count == 0)
        {
            return;
        }

        int current = FindVisibleIndex(_commands?.PrimarySelectionBlockId);
        int next = current < 0
            ? 0
            : Math.Max(0, Math.Min(_projection.VisibleNodes.Count - 1, current + direction));
        string blockId = _projection.VisibleNodes[next].BlockId;
        SelectOnly(blockId);
        ScrollToBlock(blockId);
    }

    private int FindVisibleIndex(string blockId)
    {
        if (_projection == null
            || !_projection.TryGetNode(blockId, out SequenceFlowNode node))
        {
            return -1;
        }

        return node.VisibleIndex;
    }

    private void ScrollToBlock(string blockId)
    {
        if (!_blockViews.TryGetValue(blockId, out ActionBlockView view))
        {
            return;
        }

        ScrollView scroll = GetFirstAncestorOfType<ScrollView>();
        scroll?.ScrollTo(view);
    }

    private void OnCommandStackChanged(SequenceEditChange change)
    {
        if (change.Reason != SequenceEditChangeReason.Saved)
        {
            Rebuild();
        }
    }

    private void OnKeyDown(KeyDownEvent evt)
    {
        bool command = evt.ctrlKey || evt.commandKey;
        if (command && evt.keyCode == KeyCode.S)
        {
            SaveRequested?.Invoke();
        }
        else if (command && evt.keyCode == KeyCode.Z && !evt.shiftKey)
        {
            _commands?.Undo();
        }
        else if (command
            && (evt.keyCode == KeyCode.Y
                || (evt.keyCode == KeyCode.Z && evt.shiftKey)))
        {
            _commands?.Redo();
        }
        else if (command && evt.keyCode == KeyCode.C)
        {
            CopySelection();
        }
        else if (command && evt.keyCode == KeyCode.X)
        {
            CutSelection();
        }
        else if (command && evt.keyCode == KeyCode.V)
        {
            PasteAfterSelection();
        }
        else if (command && evt.keyCode == KeyCode.D)
        {
            DuplicateSelection();
        }
        else if (command && evt.keyCode == KeyCode.A)
        {
            SelectAllVisible();
        }
        else if (evt.keyCode == KeyCode.UpArrow)
        {
            SelectRelative(-1);
        }
        else if (evt.keyCode == KeyCode.DownArrow)
        {
            SelectRelative(1);
        }
        else if (evt.keyCode == KeyCode.LeftArrow)
        {
            CollapseOrSelectParent();
        }
        else if (evt.keyCode == KeyCode.RightArrow)
        {
            ExpandOrSelectChild();
        }
        else if (evt.keyCode == KeyCode.Delete || evt.keyCode == KeyCode.Backspace)
        {
            DeleteSelection();
        }
        else if (evt.keyCode == KeyCode.Space)
        {
            ToggleSelectionEnabled();
        }
        else if (evt.keyCode == KeyCode.Return || evt.keyCode == KeyCode.KeypadEnter)
        {
            InspectRequested?.Invoke(_commands?.PrimarySelectionBlockId ?? string.Empty);
        }
        else if (evt.keyCode == KeyCode.F8)
        {
            JumpToNextProblem();
        }
        else
        {
            return;
        }

        evt.StopPropagation();
    }

    private void SelectAllVisible()
    {
        if (_projection == null || _commands == null)
        {
            return;
        }

        var ids = new List<string>();
        for (int i = 0; i < _projection.VisibleNodes.Count; i++)
        {
            ids.Add(_projection.VisibleNodes[i].BlockId);
        }

        string primary = ids.Count > 0 ? ids[0] : string.Empty;
        _commands.SetSelection(ids, primary);
    }

    private void CollapseOrSelectParent()
    {
        if (_projection == null
            || !_projection.TryGetNode(
                _commands?.PrimarySelectionBlockId,
                out SequenceFlowNode node))
        {
            return;
        }

        if (node.Children.Count > 0 && !_collapsed.Contains(node.BlockId))
        {
            _collapsed.Add(node.BlockId);
            Rebuild();
        }
        else if (!string.IsNullOrEmpty(node.ParentBlockId))
        {
            SelectOnly(node.ParentBlockId);
        }
    }

    private void ExpandOrSelectChild()
    {
        if (_projection == null
            || !_projection.TryGetNode(
                _commands?.PrimarySelectionBlockId,
                out SequenceFlowNode node)
            || node.Children.Count == 0)
        {
            return;
        }

        if (_collapsed.Remove(node.BlockId))
        {
            Rebuild();
        }
        else
        {
            SelectOnly(node.Children[0].BlockId);
        }
    }

    private void AddEmpty(string text)
    {
        var empty = new Label(text);
        empty.AddToClassList("sm-flow-empty");
        Add(empty);
    }

    private static void ToggleSet(ISet<string> values, string value)
    {
        if (!values.Add(value))
        {
            values.Remove(value);
        }
    }

    private static void AddUnique(List<string> values, string value)
    {
        if (!values.Contains(value))
        {
            values.Add(value);
        }
    }

    private static string Normalize(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
    }
}

internal static class SequenceBlockClipboard
{
    private static readonly List<ScenarioActionData> Blocks = new List<ScenarioActionData>();

    public static bool HasContent => Blocks.Count > 0;

    public static void Copy(ActionSequenceAsset sequence, IReadOnlyList<string> blockIds)
    {
        Blocks.Clear();
        if (sequence == null || blockIds == null)
        {
            return;
        }

        for (int i = 0; i < blockIds.Count; i++)
        {
            if (SequenceBlockTree.TryFind(
                    sequence,
                    blockIds[i],
                    out SequenceBlockLocation location))
            {
                Blocks.Add(ScenarioBlockIdentity.ClonePreservingIds(location.Action));
            }
        }
    }

    public static IList<ScenarioActionData> CreatePasteClones()
    {
        var result = new List<ScenarioActionData>(Blocks.Count);
        for (int i = 0; i < Blocks.Count; i++)
        {
            result.Add(ScenarioBlockIdentity.CloneWithNewIds(Blocks[i]));
        }

        return result;
    }
}
