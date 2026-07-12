using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

public enum SequenceBlockCommand
{
    MoveUp,
    MoveDown,
    Duplicate,
    Copy,
    Cut,
    PasteAfter,
    Delete,
    ToggleEnabled,
    WrapParallel,
    ExtractSequence,
    ToggleCollapse,
    ToggleBookmark,
    ToggleBreakpoint,
    EditNote
}

public enum SequenceBlockExecutionVisualState
{
    None,
    Current,
    Waiting,
    Completed,
    Failed,
    Canceled,
    Skipped
}

public class ActionBlockView : VisualElement
{
    private readonly Button _selectionSurface;
    private readonly Button _collapseButton;
    private readonly Toggle _enabledToggle;
    private readonly Button _breakpointButton;
    private readonly Button _bookmarkButton;

    public ActionBlockView(
        SequenceFlowNode node,
        bool bookmarked,
        bool breakpoint)
    {
        Node = node ?? throw new ArgumentNullException(nameof(node));
        name = "block-" + node.BlockId;
        AddToClassList("sm-action-block");
        EnableInClassList("is-selected", node.IsSelected);
        EnableInClassList("is-primary", node.IsPrimarySelection);
        EnableInClassList("is-disabled", node.IsDisabled);
        EnableInClassList("is-context-only", node.IsContextOnly);
        EnableInClassList("has-error", node.ErrorCount > 0 || node.Summary.HasParameterError);
        EnableInClassList("has-warning", node.WarningCount > 0);
        style.marginLeft = 12f + node.Depth * 22f;

        var dragHandle = new Label("⋮⋮")
        {
            tooltip = "드래그해서 블록 이동"
        };
        dragHandle.AddToClassList("sm-block-drag-handle");
        dragHandle.RegisterCallback<PointerDownEvent>(evt =>
        {
            if (evt.button == 0)
            {
                DragRequested?.Invoke(Node);
                evt.StopPropagation();
            }
        });
        Add(dragHandle);

        _collapseButton = new Button
        {
            text = node.IsCollapsed ? "▶" : "▼",
            tooltip = node.IsCollapsed ? "하위 블록 펼치기" : "하위 블록 접기"
        };
        _collapseButton.AddToClassList("sm-block-collapse");
        _collapseButton.style.display = node.Children.Count > 0
            ? DisplayStyle.Flex
            : DisplayStyle.None;
        _collapseButton.clicked += () => CommandRequested?.Invoke(
            Node,
            SequenceBlockCommand.ToggleCollapse);
        Add(_collapseButton);

        var accent = new VisualElement();
        accent.AddToClassList("sm-block-accent");
        if (!string.IsNullOrWhiteSpace(node.Summary.AccentHex)
            && ColorUtility.TryParseHtmlString(node.Summary.AccentHex, out Color color))
        {
            accent.style.backgroundColor = color;
        }
        Add(accent);

        _selectionSurface = new Button();
        _selectionSurface.AddToClassList("sm-block-selection");
        _selectionSurface.RegisterCallback<ClickEvent>(evt =>
        {
            SelectionRequested?.Invoke(Node, evt.modifiers);
            evt.StopPropagation();
        });

        var icon = new Image
        {
            image = ResolveIcon(node.Summary),
            scaleMode = ScaleMode.ScaleToFit
        };
        icon.AddToClassList("sm-block-icon");
        _selectionSurface.Add(icon);

        var copy = new VisualElement();
        copy.AddToClassList("sm-block-copy");
        var titleRow = new VisualElement();
        titleRow.AddToClassList("sm-block-title-row");
        var title = new Label(node.Summary.Title);
        title.AddToClassList("sm-block-title");
        titleRow.Add(title);
        if (!string.IsNullOrWhiteSpace(node.Summary.Note))
        {
            var note = new Label("메모");
            note.AddToClassList("sm-block-note-badge");
            note.tooltip = node.Summary.Note;
            titleRow.Add(note);
        }

        copy.Add(titleRow);
        var summary = new Label(node.Summary.Summary);
        summary.AddToClassList("sm-block-summary");
        copy.Add(summary);
        if (node.Summary.QuickValues.Count > 0)
        {
            var quickValues = new VisualElement();
            quickValues.AddToClassList("sm-block-quick-values");
            for (int i = 0; i < node.Summary.QuickValues.Count; i++)
            {
                ActionBlockQuickValue quick = node.Summary.QuickValues[i];
                var chip = new Label(quick.Label + "  " + quick.Value);
                chip.AddToClassList("sm-block-quick-chip");
                chip.tooltip = quick.ParameterName;
                quickValues.Add(chip);
            }

            copy.Add(quickValues);
        }

        _selectionSurface.Add(copy);

        var badges = new VisualElement();
        badges.AddToClassList("sm-block-badges");
        var category = new Label(string.IsNullOrWhiteSpace(node.Summary.Category)
            ? "action"
            : node.Summary.Category);
        category.AddToClassList("sm-block-category");
        badges.Add(category);
        int problemCount = node.ErrorCount + node.WarningCount
            + (node.Summary.HasParameterError ? 1 : 0);
        if (problemCount > 0)
        {
            var problem = new Label(problemCount.ToString());
            problem.AddToClassList("sm-block-problem-badge");
            problem.EnableInClassList(
                "is-error",
                node.ErrorCount > 0 || node.Summary.HasParameterError);
            problem.tooltip = "이 블록의 문제 " + problemCount + "개";
            badges.Add(problem);
        }

        _selectionSurface.Add(badges);
        Add(_selectionSurface);

        _enabledToggle = new Toggle
        {
            value = !node.IsDisabled,
            tooltip = node.IsDisabled ? "블록 활성화" : "블록 비활성화"
        };
        _enabledToggle.AddToClassList("sm-block-enabled-toggle");
        _enabledToggle.RegisterValueChangedCallback(evt =>
            EnabledChanged?.Invoke(Node, evt.newValue));
        Add(_enabledToggle);

        _breakpointButton = new Button { text = breakpoint ? "●" : "○" };
        _breakpointButton.AddToClassList("sm-block-tool-button");
        _breakpointButton.EnableInClassList("is-active", breakpoint);
        _breakpointButton.tooltip = breakpoint ? "중단점 해제" : "이 블록에서 일시정지";
        _breakpointButton.clicked += () => CommandRequested?.Invoke(
            Node,
            SequenceBlockCommand.ToggleBreakpoint);
        Add(_breakpointButton);

        _bookmarkButton = new Button { text = bookmarked ? "★" : "☆" };
        _bookmarkButton.AddToClassList("sm-block-tool-button");
        _bookmarkButton.EnableInClassList("is-active", bookmarked);
        _bookmarkButton.tooltip = bookmarked ? "북마크 해제" : "블록 북마크";
        _bookmarkButton.clicked += () => CommandRequested?.Invoke(
            Node,
            SequenceBlockCommand.ToggleBookmark);
        Add(_bookmarkButton);

        this.AddManipulator(new ContextualMenuManipulator(BuildContextMenu));
    }

    public SequenceFlowNode Node { get; }

    public event Action<SequenceFlowNode, EventModifiers> SelectionRequested;
    public event Action<SequenceFlowNode, bool> EnabledChanged;
    public event Action<SequenceFlowNode, SequenceBlockCommand> CommandRequested;
    public event Action<SequenceFlowNode> DragRequested;

    public void SetExecutionState(SequenceBlockExecutionVisualState state)
    {
        foreach (SequenceBlockExecutionVisualState value in
                 (SequenceBlockExecutionVisualState[])Enum.GetValues(
                     typeof(SequenceBlockExecutionVisualState)))
        {
            if (value != SequenceBlockExecutionVisualState.None)
            {
                EnableInClassList(
                    "execution-" + value.ToString().ToLowerInvariant(),
                    value == state);
            }
        }
    }

    private void BuildContextMenu(ContextualMenuPopulateEvent evt)
    {
        evt.menu.AppendAction("위로 이동", _ => Request(SequenceBlockCommand.MoveUp));
        evt.menu.AppendAction("아래로 이동", _ => Request(SequenceBlockCommand.MoveDown));
        evt.menu.AppendSeparator();
        evt.menu.AppendAction("복제", _ => Request(SequenceBlockCommand.Duplicate));
        evt.menu.AppendAction("복사", _ => Request(SequenceBlockCommand.Copy));
        evt.menu.AppendAction("잘라내기", _ => Request(SequenceBlockCommand.Cut));
        evt.menu.AppendAction("뒤에 붙여넣기", _ => Request(SequenceBlockCommand.PasteAfter));
        evt.menu.AppendSeparator();
        evt.menu.AppendAction("동시 실행으로 묶기", _ => Request(SequenceBlockCommand.WrapParallel));
        evt.menu.AppendAction("새 시퀀스로 추출", _ => Request(SequenceBlockCommand.ExtractSequence));
        if (Node.Children.Count > 0)
        {
            evt.menu.AppendAction(
                Node.IsCollapsed ? "하위 블록 펼치기" : "하위 블록 접기",
                _ => Request(SequenceBlockCommand.ToggleCollapse));
        }

        evt.menu.AppendAction("메모 편집", _ => Request(SequenceBlockCommand.EditNote));
        evt.menu.AppendSeparator();
        evt.menu.AppendAction(
            Node.IsDisabled ? "활성화" : "비활성화",
            _ => Request(SequenceBlockCommand.ToggleEnabled));
        evt.menu.AppendAction("삭제", _ => Request(SequenceBlockCommand.Delete));
    }

    private void Request(SequenceBlockCommand command)
    {
        CommandRequested?.Invoke(Node, command);
    }

    private static Texture ResolveIcon(ActionBlockSummary summary)
    {
        string iconName = ResolveUnityIconName(summary.IconId, summary.IsStructural);
        return EditorGUIUtility.IconContent(iconName)?.image;
    }

    private static string ResolveUnityIconName(string stableIconId, bool structural)
    {
        switch ((stableIconId ?? string.Empty).Trim().ToLowerInvariant())
        {
            case "music":
                return "AudioClip Icon";
            case "message-square":
                return "console.infoicon";
            case "play":
                return "d_PlayButton";
            case "move":
            case "arrow-down-to-line":
                return "MoveTool";
            default:
                return structural ? "UnityEditor.HierarchyWindow" : "ScriptableObject Icon";
        }
    }
}
