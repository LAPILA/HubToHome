using System;
using System.Collections.Generic;

public sealed class SequenceFlowNode
{
    internal SequenceFlowNode(
        ScenarioActionData action,
        string parentBlockId,
        int depth,
        int siblingIndex,
        int displayIndex,
        ActionBlockSummary summary)
    {
        Action = action;
        BlockId = action?.BlockId ?? string.Empty;
        ParentBlockId = parentBlockId ?? string.Empty;
        Depth = depth;
        SiblingIndex = siblingIndex;
        DisplayIndex = displayIndex;
        Summary = summary;
        IsDisabled = action != null && action.Disabled;
        IsStructural = summary != null && summary.IsStructural;
    }

    public ScenarioActionData Action { get; }
    public string BlockId { get; }
    public string ParentBlockId { get; }
    public int Depth { get; }
    public int SiblingIndex { get; }
    public int DisplayIndex { get; }
    public int VisibleIndex { get; internal set; } = -1;
    public ActionBlockSummary Summary { get; }
    public bool IsVisible { get; internal set; }
    public bool IsContextOnly { get; internal set; }
    public bool IsSelected { get; internal set; }
    public bool IsPrimarySelection { get; internal set; }
    public bool IsDisabled { get; }
    public bool IsStructural { get; }
    public bool IsCollapsed { get; internal set; }
    public int ErrorCount { get; internal set; }
    public int WarningCount { get; internal set; }
    public int InfoCount { get; internal set; }
    public IReadOnlyList<SequenceFlowNode> Children => _children;

    private readonly List<SequenceFlowNode> _children = new List<SequenceFlowNode>();

    internal void AddChild(SequenceFlowNode child)
    {
        if (child != null)
        {
            _children.Add(child);
        }
    }
}

public sealed class SequenceFlowProjection
{
    private readonly List<SequenceFlowNode> _allNodes = new List<SequenceFlowNode>();
    private readonly List<SequenceFlowNode> _visibleNodes = new List<SequenceFlowNode>();
    private readonly List<SequenceFlowNode> _roots = new List<SequenceFlowNode>();
    private readonly Dictionary<string, SequenceFlowNode> _byBlockId =
        new Dictionary<string, SequenceFlowNode>(StringComparer.Ordinal);

    private SequenceFlowProjection()
    {
    }

    public IReadOnlyList<SequenceFlowNode> AllNodes => _allNodes;
    public IReadOnlyList<SequenceFlowNode> VisibleNodes => _visibleNodes;
    public IReadOnlyList<SequenceFlowNode> Roots => _roots;

    public static SequenceFlowProjection Build(
        ActionSequenceAsset sequence,
        ISet<string> selectedBlockIds = null,
        string primarySelectionBlockId = "",
        ISet<string> collapsedBlockIds = null,
        ScenarioValidationResult validation = null,
        ActionCatalogAsset catalog = null,
        string searchQuery = "")
    {
        var result = new SequenceFlowProjection();
        if (sequence == null || sequence.Actions == null)
        {
            return result;
        }

        int displayIndex = 0;
        result.BuildNodes(
            sequence.Actions,
            null,
            0,
            selectedBlockIds,
            Normalize(primarySelectionBlockId),
            collapsedBlockIds,
            validation,
            catalog,
            ref displayIndex);

        string normalizedSearch = Normalize(searchQuery);
        if (string.IsNullOrEmpty(normalizedSearch))
        {
            for (int i = 0; i < result._roots.Count; i++)
            {
                result.AddVisibleWithoutSearch(result._roots[i], true);
            }
        }
        else
        {
            var matchCache = new Dictionary<SequenceFlowNode, bool>();
            for (int i = 0; i < result._roots.Count; i++)
            {
                result.ComputeSubtreeMatch(
                    result._roots[i],
                    normalizedSearch,
                    catalog,
                    matchCache);
            }

            for (int i = 0; i < result._roots.Count; i++)
            {
                result.AddVisibleSearch(
                    result._roots[i],
                    normalizedSearch,
                    catalog,
                    matchCache);
            }
        }

        for (int i = 0; i < result._visibleNodes.Count; i++)
        {
            result._visibleNodes[i].VisibleIndex = i;
        }

        return result;
    }

    public bool TryGetNode(string blockId, out SequenceFlowNode node)
    {
        return _byBlockId.TryGetValue(Normalize(blockId), out node);
    }

    public SequenceFlowNode GetNode(string blockId)
    {
        TryGetNode(blockId, out SequenceFlowNode node);
        return node;
    }

    private void BuildNodes(
        IList<ScenarioActionData> actions,
        SequenceFlowNode parent,
        int depth,
        ISet<string> selectedBlockIds,
        string primarySelectionBlockId,
        ISet<string> collapsedBlockIds,
        ScenarioValidationResult validation,
        ActionCatalogAsset catalog,
        ref int displayIndex)
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

            string blockId = Normalize(action.BlockId);
            ActionCatalogEntry entry = catalog != null
                ? catalog.FindById(action.ActionId)
                : null;
            var node = new SequenceFlowNode(
                action,
                parent?.BlockId ?? string.Empty,
                depth,
                i,
                displayIndex++,
                ActionBlockSummary.Build(action, entry));
            node.IsSelected = selectedBlockIds != null && selectedBlockIds.Contains(blockId);
            node.IsPrimarySelection = string.Equals(
                blockId,
                primarySelectionBlockId,
                StringComparison.Ordinal);
            node.IsCollapsed = collapsedBlockIds != null && collapsedBlockIds.Contains(blockId);
            ApplyValidation(node, validation);

            _allNodes.Add(node);
            if (!string.IsNullOrEmpty(blockId) && !_byBlockId.ContainsKey(blockId))
            {
                _byBlockId.Add(blockId, node);
            }

            if (parent == null)
            {
                _roots.Add(node);
            }
            else
            {
                parent.AddChild(node);
            }

            BuildNodes(
                action.Children,
                node,
                depth + 1,
                selectedBlockIds,
                primarySelectionBlockId,
                collapsedBlockIds,
                validation,
                catalog,
                ref displayIndex);
        }
    }

    private void AddVisibleWithoutSearch(SequenceFlowNode node, bool ancestorsVisible)
    {
        if (node == null || !ancestorsVisible)
        {
            return;
        }

        node.IsVisible = true;
        _visibleNodes.Add(node);
        if (node.IsCollapsed)
        {
            return;
        }

        for (int i = 0; i < node.Children.Count; i++)
        {
            AddVisibleWithoutSearch(node.Children[i], true);
        }
    }

    private bool ComputeSubtreeMatch(
        SequenceFlowNode node,
        string search,
        ActionCatalogAsset catalog,
        Dictionary<SequenceFlowNode, bool> cache)
    {
        bool match = Matches(node, search, catalog);
        for (int i = 0; i < node.Children.Count; i++)
        {
            match |= ComputeSubtreeMatch(node.Children[i], search, catalog, cache);
        }

        cache[node] = match;
        return match;
    }

    private void AddVisibleSearch(
        SequenceFlowNode node,
        string search,
        ActionCatalogAsset catalog,
        IDictionary<SequenceFlowNode, bool> cache)
    {
        if (!cache.TryGetValue(node, out bool subtreeMatches) || !subtreeMatches)
        {
            return;
        }

        bool ownMatch = Matches(node, search, catalog);
        node.IsVisible = true;
        node.IsContextOnly = !ownMatch;
        _visibleNodes.Add(node);
        for (int i = 0; i < node.Children.Count; i++)
        {
            AddVisibleSearch(node.Children[i], search, catalog, cache);
        }
    }

    private static bool Matches(
        SequenceFlowNode node,
        string search,
        ActionCatalogAsset catalog)
    {
        if (node?.Action == null)
        {
            return false;
        }

        ActionCatalogEntry entry = catalog != null
            ? catalog.FindById(node.Action.ActionId)
            : null;
        string haystack = string.Join(" ", new[]
        {
            node.Action.BlockId,
            node.Action.ActionId,
            node.Action.DesignerLabel,
            node.Action.Note,
            node.Action.ParametersJson,
            entry?.DisplayNameKo,
            entry?.DescriptionKo,
            entry?.UsageKo,
            entry?.Category
        });
        return haystack.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static void ApplyValidation(
        SequenceFlowNode node,
        ScenarioValidationResult validation)
    {
        if (node == null || validation == null)
        {
            return;
        }

        for (int i = 0; i < validation.Messages.Count; i++)
        {
            ScenarioValidationMessage message = validation.Messages[i];
            if (!TargetsBlock(message?.ObjectId, node.BlockId))
            {
                continue;
            }

            switch (message.Severity)
            {
                case ScenarioValidationSeverity.Error:
                    node.ErrorCount++;
                    break;
                case ScenarioValidationSeverity.Warning:
                    node.WarningCount++;
                    break;
                default:
                    node.InfoCount++;
                    break;
            }
        }
    }

    private static bool TargetsBlock(string objectId, string blockId)
    {
        string source = Normalize(objectId);
        string id = Normalize(blockId);
        if (string.IsNullOrEmpty(source) || string.IsNullOrEmpty(id))
        {
            return false;
        }

        return string.Equals(source, id, StringComparison.Ordinal)
            || string.Equals(source, "block:" + id, StringComparison.Ordinal)
            || source.StartsWith("block:" + id + "/", StringComparison.Ordinal)
            || source.StartsWith("block:" + id + ".", StringComparison.Ordinal);
    }

    private static string Normalize(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
    }
}
