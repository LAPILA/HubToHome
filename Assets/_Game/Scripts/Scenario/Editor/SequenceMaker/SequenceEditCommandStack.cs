using System;
using System.Collections.Generic;
using UnityEditor;

public enum SequenceEditChangeReason
{
    Selection,
    Execute,
    Undo,
    Redo,
    Saved,
    HistoryCleared
}

public sealed class SequenceEditChange
{
    public SequenceEditChange(
        SequenceEditChangeReason reason,
        string label,
        bool isDirty)
    {
        Reason = reason;
        Label = label ?? string.Empty;
        IsDirty = isDirty;
    }

    public SequenceEditChangeReason Reason { get; }
    public string Label { get; }
    public bool IsDirty { get; }
}

public interface ISequenceEditCommand
{
    string Name { get; }

    string PreferredSelectionBlockId { get; }

    void Execute(ActionSequenceAsset sequence);

    void Undo(ActionSequenceAsset sequence);
}

public sealed class SequenceEditCommandStack
{
    private readonly ActionSequenceAsset _sequence;
    private readonly List<HistoryEntry> _undo = new List<HistoryEntry>();
    private readonly List<HistoryEntry> _redo = new List<HistoryEntry>();
    private readonly List<string> _selectedBlockIds = new List<string>();
    private long _nextStateId;
    private long _currentStateId;
    private long _savedStateId;
    private TransactionState _transaction;

    public SequenceEditCommandStack(ActionSequenceAsset sequence)
    {
        _sequence = sequence ?? throw new ArgumentNullException(nameof(sequence));
        if (!SequenceBlockTree.TryValidateUniqueIds(sequence, out string error))
        {
            throw new InvalidOperationException(error);
        }
    }

    public event Action<SequenceEditChange> Changed;

    public bool CanUndo => _undo.Count > 0;
    public bool CanRedo => _redo.Count > 0;
    public bool IsDirty => _currentStateId != _savedStateId
        || (_transaction != null && _transaction.Commands.Count > 0);
    public string UndoLabel => CanUndo ? _undo[_undo.Count - 1].Name : string.Empty;
    public string RedoLabel => CanRedo ? _redo[_redo.Count - 1].Name : string.Empty;
    public IReadOnlyList<string> SelectedBlockIds => _selectedBlockIds;
    public string PrimarySelectionBlockId { get; private set; } = string.Empty;

    public void SetSelection(string blockId)
    {
        SetSelection(
            string.IsNullOrWhiteSpace(blockId)
                ? Array.Empty<string>()
                : new[] { blockId },
            blockId);
    }

    public void SetSelection(IEnumerable<string> blockIds, string primaryBlockId = "")
    {
        RestoreSelection(new SelectionSnapshot(blockIds, primaryBlockId));
        Raise(SequenceEditChangeReason.Selection, string.Empty);
    }

    public void Execute(ISequenceEditCommand command)
    {
        if (command == null)
        {
            throw new ArgumentNullException(nameof(command));
        }

        if (!SequenceBlockTree.TryValidateUniqueIds(_sequence, out string currentError))
        {
            throw new InvalidOperationException(currentError);
        }

        SelectionSnapshot beforeSelection = CaptureSelection();
        bool executed = false;
        try
        {
            command.Execute(_sequence);
            executed = true;
            if (!SequenceBlockTree.TryValidateUniqueIds(_sequence, out string error))
            {
                throw new InvalidOperationException(error);
            }

            ApplyPreferredSelection(command.PreferredSelectionBlockId);
            SelectionSnapshot afterSelection = CaptureSelection();
            EditorUtility.SetDirty(_sequence);

            if (_transaction != null)
            {
                _transaction.Commands.Add(command);
                _transaction.AfterSelection = afterSelection;
                Raise(SequenceEditChangeReason.Execute, command.Name);
                return;
            }

            long beforeStateId = _currentStateId;
            long afterStateId = ++_nextStateId;
            _undo.Add(new HistoryEntry(
                command.Name,
                new[] { command },
                beforeSelection,
                afterSelection,
                beforeStateId,
                afterStateId));
            _redo.Clear();
            _currentStateId = afterStateId;
            Raise(SequenceEditChangeReason.Execute, command.Name);
        }
        catch
        {
            if (executed)
            {
                TryUndo(command);
            }

            RestoreSelection(beforeSelection);
            if (_transaction != null)
            {
                RollbackTransaction();
            }

            EditorUtility.SetDirty(_sequence);
            throw;
        }
    }

    public bool Undo()
    {
        EnsureNoTransaction();
        if (!CanUndo)
        {
            return false;
        }

        HistoryEntry entry = _undo[_undo.Count - 1];
        _undo.RemoveAt(_undo.Count - 1);
        for (int i = entry.Commands.Count - 1; i >= 0; i--)
        {
            entry.Commands[i].Undo(_sequence);
        }

        RestoreSelection(entry.BeforeSelection);
        _currentStateId = entry.BeforeStateId;
        _redo.Add(entry);
        EditorUtility.SetDirty(_sequence);
        Raise(SequenceEditChangeReason.Undo, entry.Name);
        return true;
    }

    public bool Redo()
    {
        EnsureNoTransaction();
        if (!CanRedo)
        {
            return false;
        }

        HistoryEntry entry = _redo[_redo.Count - 1];
        _redo.RemoveAt(_redo.Count - 1);
        for (int i = 0; i < entry.Commands.Count; i++)
        {
            entry.Commands[i].Execute(_sequence);
        }

        RestoreSelection(entry.AfterSelection);
        _currentStateId = entry.AfterStateId;
        _undo.Add(entry);
        EditorUtility.SetDirty(_sequence);
        Raise(SequenceEditChangeReason.Redo, entry.Name);
        return true;
    }

    public SequenceEditTransaction BeginTransaction(string name)
    {
        if (_transaction != null)
        {
            throw new InvalidOperationException("Nested sequence edit transactions are not supported.");
        }

        _transaction = new TransactionState(
            string.IsNullOrWhiteSpace(name) ? "시퀀스 복합 편집" : name.Trim(),
            CaptureSelection(),
            _currentStateId);
        return new SequenceEditTransaction(this);
    }

    public void MarkSaved()
    {
        EnsureNoTransaction();
        _savedStateId = _currentStateId;
        Raise(SequenceEditChangeReason.Saved, string.Empty);
    }

    public void ClearHistory(bool markCurrentAsSaved = false)
    {
        EnsureNoTransaction();
        _undo.Clear();
        _redo.Clear();
        if (markCurrentAsSaved)
        {
            _savedStateId = _currentStateId;
        }

        Raise(SequenceEditChangeReason.HistoryCleared, string.Empty);
    }

    internal void CommitTransaction()
    {
        if (_transaction == null)
        {
            throw new InvalidOperationException("There is no active sequence edit transaction.");
        }

        TransactionState transaction = _transaction;
        _transaction = null;
        if (transaction.Commands.Count == 0)
        {
            RestoreSelection(transaction.BeforeSelection);
            return;
        }

        long afterStateId = ++_nextStateId;
        _undo.Add(new HistoryEntry(
            transaction.Name,
            transaction.Commands,
            transaction.BeforeSelection,
            transaction.AfterSelection,
            transaction.BeforeStateId,
            afterStateId));
        _redo.Clear();
        _currentStateId = afterStateId;
        Raise(SequenceEditChangeReason.Execute, transaction.Name);
    }

    internal void RollbackTransaction()
    {
        if (_transaction == null)
        {
            return;
        }

        TransactionState transaction = _transaction;
        _transaction = null;
        for (int i = transaction.Commands.Count - 1; i >= 0; i--)
        {
            TryUndo(transaction.Commands[i]);
        }

        RestoreSelection(transaction.BeforeSelection);
        _currentStateId = transaction.BeforeStateId;
        EditorUtility.SetDirty(_sequence);
    }

    private void ApplyPreferredSelection(string preferredBlockId)
    {
        if (!string.IsNullOrWhiteSpace(preferredBlockId)
            && SequenceBlockTree.Contains(_sequence, preferredBlockId))
        {
            RestoreSelection(new SelectionSnapshot(
                new[] { preferredBlockId },
                preferredBlockId));
            return;
        }

        RestoreSelection(CaptureSelection());
    }

    private SelectionSnapshot CaptureSelection()
    {
        return new SelectionSnapshot(_selectedBlockIds, PrimarySelectionBlockId);
    }

    private void RestoreSelection(SelectionSnapshot snapshot)
    {
        _selectedBlockIds.Clear();
        if (snapshot != null)
        {
            for (int i = 0; i < snapshot.BlockIds.Count; i++)
            {
                string blockId = Normalize(snapshot.BlockIds[i]);
                if (!string.IsNullOrEmpty(blockId)
                    && !_selectedBlockIds.Contains(blockId)
                    && SequenceBlockTree.Contains(_sequence, blockId))
                {
                    _selectedBlockIds.Add(blockId);
                }
            }
        }

        string primary = snapshot != null ? Normalize(snapshot.PrimaryBlockId) : string.Empty;
        PrimarySelectionBlockId = _selectedBlockIds.Contains(primary)
            ? primary
            : (_selectedBlockIds.Count > 0 ? _selectedBlockIds[0] : string.Empty);
    }

    private void Raise(SequenceEditChangeReason reason, string label)
    {
        Changed?.Invoke(new SequenceEditChange(reason, label, IsDirty));
    }

    private void EnsureNoTransaction()
    {
        if (_transaction != null)
        {
            throw new InvalidOperationException(
                "Finish the active sequence edit transaction before this operation.");
        }
    }

    private void TryUndo(ISequenceEditCommand command)
    {
        try
        {
            command?.Undo(_sequence);
        }
        catch
        {
            // The owning rollback path will still surface the original command failure.
        }
    }

    private static string Normalize(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
    }

    private sealed class HistoryEntry
    {
        public HistoryEntry(
            string name,
            IEnumerable<ISequenceEditCommand> commands,
            SelectionSnapshot beforeSelection,
            SelectionSnapshot afterSelection,
            long beforeStateId,
            long afterStateId)
        {
            Name = name ?? string.Empty;
            Commands = new List<ISequenceEditCommand>(commands);
            BeforeSelection = beforeSelection;
            AfterSelection = afterSelection;
            BeforeStateId = beforeStateId;
            AfterStateId = afterStateId;
        }

        public string Name { get; }
        public List<ISequenceEditCommand> Commands { get; }
        public SelectionSnapshot BeforeSelection { get; }
        public SelectionSnapshot AfterSelection { get; }
        public long BeforeStateId { get; }
        public long AfterStateId { get; }
    }

    private sealed class TransactionState
    {
        public TransactionState(
            string name,
            SelectionSnapshot beforeSelection,
            long beforeStateId)
        {
            Name = name;
            BeforeSelection = beforeSelection;
            AfterSelection = beforeSelection;
            BeforeStateId = beforeStateId;
        }

        public string Name { get; }
        public List<ISequenceEditCommand> Commands { get; } = new List<ISequenceEditCommand>();
        public SelectionSnapshot BeforeSelection { get; }
        public SelectionSnapshot AfterSelection { get; set; }
        public long BeforeStateId { get; }
    }

    private sealed class SelectionSnapshot
    {
        public SelectionSnapshot(IEnumerable<string> blockIds, string primaryBlockId)
        {
            BlockIds = blockIds != null
                ? new List<string>(blockIds)
                : new List<string>();
            PrimaryBlockId = primaryBlockId ?? string.Empty;
        }

        public List<string> BlockIds { get; }
        public string PrimaryBlockId { get; }
    }
}

public sealed class SequenceEditTransaction : IDisposable
{
    private SequenceEditCommandStack _owner;
    private bool _committed;

    internal SequenceEditTransaction(SequenceEditCommandStack owner)
    {
        _owner = owner;
    }

    public void Commit()
    {
        if (_owner == null || _committed)
        {
            return;
        }

        _owner.CommitTransaction();
        _committed = true;
    }

    public void Dispose()
    {
        if (_owner == null)
        {
            return;
        }

        if (!_committed)
        {
            _owner.RollbackTransaction();
        }

        _owner = null;
    }
}
