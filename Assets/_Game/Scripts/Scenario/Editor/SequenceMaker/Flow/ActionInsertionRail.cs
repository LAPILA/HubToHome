using System;
using UnityEditor;
using UnityEngine.UIElements;

public sealed class ActionInsertionRail : VisualElement
{
    public const string DragDataKey = "HubToHome.SequenceMaker.BlockId";

    public ActionInsertionRail(string parentBlockId, int insertionIndex, int depth)
    {
        ParentBlockId = parentBlockId ?? string.Empty;
        InsertionIndex = insertionIndex;
        AddToClassList("sm-insertion-rail");
        style.marginLeft = 12f + depth * 22f;

        var line = new VisualElement();
        line.AddToClassList("sm-insertion-line");
        Add(line);

        var add = new Button { text = "+", tooltip = "이 위치에 액션 추가" };
        add.AddToClassList("sm-insertion-button");
        add.clicked += () => InsertRequested?.Invoke(this);
        Add(add);

        RegisterCallback<DragUpdatedEvent>(OnDragUpdated);
        RegisterCallback<DragPerformEvent>(OnDragPerform);
        RegisterCallback<DragExitedEvent>(_ => RemoveFromClassList("is-drag-target"));
    }

    public string ParentBlockId { get; }
    public int InsertionIndex { get; }

    public event Action<ActionInsertionRail> InsertRequested;
    public event Action<ActionInsertionRail, string> BlockDropped;

    private void OnDragUpdated(DragUpdatedEvent evt)
    {
        string blockId = DragAndDrop.GetGenericData(DragDataKey) as string;
        if (string.IsNullOrWhiteSpace(blockId))
        {
            return;
        }

        DragAndDrop.visualMode = DragAndDropVisualMode.Move;
        AddToClassList("is-drag-target");
        evt.StopPropagation();
    }

    private void OnDragPerform(DragPerformEvent evt)
    {
        string blockId = DragAndDrop.GetGenericData(DragDataKey) as string;
        if (string.IsNullOrWhiteSpace(blockId))
        {
            return;
        }

        DragAndDrop.AcceptDrag();
        RemoveFromClassList("is-drag-target");
        BlockDropped?.Invoke(this, blockId);
        evt.StopPropagation();
    }
}
