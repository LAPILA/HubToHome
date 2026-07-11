using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

public sealed class SequenceNavigatorRequest
{
    public SequenceNavigatorRequest(
        UnityEngine.Object asset,
        string blockId = "")
    {
        Asset = asset;
        BlockId = blockId ?? string.Empty;
    }

    public UnityEngine.Object Asset { get; }
    public string BlockId { get; }
}

public sealed class SequenceNavigatorView : VisualElement
{
    private readonly ToolbarSearchField _search;
    private readonly Button _refreshButton;
    private readonly ScrollView _scroll;
    private SequenceAssetIndex _index;
    private SequenceUsageIndex _usage;
    private SequenceNavigatorHistory _history;
    private UnityEngine.Object _currentTarget;
    private ActionSequenceAsset _selectedSequence;

    public SequenceNavigatorView()
    {
        AddToClassList("sm-navigator-view");

        var toolbar = new VisualElement();
        toolbar.AddToClassList("sm-navigator-toolbar");
        _search = new ToolbarSearchField
        {
            tooltip = "전투 흐름과 시퀀스 검색"
        };
        _search.AddToClassList("sm-navigator-search");
        _search.RegisterValueChangedCallback(_ => Render());
        toolbar.Add(_search);

        _refreshButton = new Button
        {
            tooltip = "프로젝트 시퀀스 목록 다시 읽기"
        };
        _refreshButton.AddToClassList("sm-icon-button");
        _refreshButton.AddToClassList("sm-icon-button--small");
        SequenceMakerTheme.SetButtonIcon(_refreshButton, "Refresh", "R");
        _refreshButton.clicked += () => RefreshRequested?.Invoke();
        toolbar.Add(_refreshButton);
        Add(toolbar);

        _scroll = new ScrollView(ScrollViewMode.Vertical);
        _scroll.AddToClassList("sm-panel-scroll");
        Add(_scroll);
    }

    public event Action<SequenceNavigatorRequest> OpenRequested;
    public event Action RefreshRequested;

    public void Bind(
        SequenceAssetIndex index,
        SequenceUsageIndex usage,
        SequenceNavigatorHistory history,
        UnityEngine.Object currentTarget,
        ActionSequenceAsset selectedSequence)
    {
        _index = index;
        _usage = usage;
        _history = history;
        _currentTarget = currentTarget;
        _selectedSequence = selectedSequence;
        Render();
    }

    private void Render()
    {
        _scroll.Clear();
        if (_index == null)
        {
            AddEmpty("프로젝트 인덱스 없음");
            return;
        }

        string query = Normalize(_search.value);
        if (!string.IsNullOrEmpty(query))
        {
            IReadOnlyList<SequenceAssetIndexEntry> results = _index.Search(query);
            AddAssetGroup("검색 결과", results, false);
            if (results.Count == 0)
            {
                AddEmpty("검색 결과 없음");
            }

            return;
        }

        IReadOnlyList<SequenceAssetIndexEntry> recent = _history?.ResolveRecent(_index)
            ?? Array.Empty<SequenceAssetIndexEntry>();
        IReadOnlyList<SequenceAssetIndexEntry> favorites = _history?.ResolveFavorites(_index)
            ?? Array.Empty<SequenceAssetIndexEntry>();
        AddAssetGroup("최근 작업", recent, false);
        AddAssetGroup("즐겨찾기", favorites, true);
        AddAssetGroup("전투 흐름", _index.BattleFlows, true);
        AddAssetGroup("시퀀스", _index.Sequences, true);
        AddUsageGroup();
        AddBrokenReferenceGroup();
    }

    private void AddAssetGroup(
        string title,
        IReadOnlyList<SequenceAssetIndexEntry> entries,
        bool showFavorite)
    {
        if (entries == null || entries.Count == 0)
        {
            return;
        }

        AddSection(title, entries.Count);
        for (int i = 0; i < entries.Count; i++)
        {
            SequenceAssetIndexEntry entry = entries[i];
            _scroll.Add(CreateAssetRow(entry, showFavorite));
        }
    }

    private VisualElement CreateAssetRow(
        SequenceAssetIndexEntry entry,
        bool showFavorite)
    {
        var row = new VisualElement();
        row.AddToClassList("sm-nav-entry");
        bool selected = entry.Asset == _currentTarget
            || (entry.Sequence != null && entry.Sequence == _selectedSequence);
        row.EnableInClassList("is-selected", selected);

        var open = new Button();
        open.AddToClassList("sm-nav-entry-main");
        open.tooltip = BuildTooltip(entry);

        var icon = new Image
        {
            image = EditorGUIUtility.IconContent(
                entry.Kind == SequenceAssetIndexEntryKind.BattleFlow
                    ? "SceneAsset Icon"
                    : "ScriptableObject Icon")?.image,
            scaleMode = ScaleMode.ScaleToFit
        };
        icon.AddToClassList("sm-nav-entry-icon");
        open.Add(icon);

        var copy = new VisualElement();
        copy.AddToClassList("sm-nav-entry-copy");
        var title = new Label(DisplayName(entry));
        title.AddToClassList("sm-nav-entry-title");
        copy.Add(title);
        var id = new Label(entry.Id);
        id.AddToClassList("sm-nav-entry-id");
        copy.Add(id);
        open.Add(copy);
        open.clicked += () => OpenRequested?.Invoke(
            new SequenceNavigatorRequest(entry.Asset));
        row.Add(open);

        if (showFavorite && _history != null)
        {
            bool favorite = _history.IsFavorite(entry.StableKey);
            var favoriteButton = new Button { text = favorite ? "★" : "☆" };
            favoriteButton.AddToClassList("sm-nav-favorite");
            favoriteButton.tooltip = favorite ? "즐겨찾기 해제" : "즐겨찾기 추가";
            favoriteButton.clicked += () =>
            {
                _history.SetFavorite(entry.StableKey, !favorite);
                Render();
            };
            row.Add(favoriteButton);
        }

        return row;
    }

    private void AddUsageGroup()
    {
        if (_usage == null || _selectedSequence == null)
        {
            return;
        }

        IReadOnlyList<SequenceUsageRecord> usages =
            _usage.GetUsages(_selectedSequence.SequenceId);
        if (usages.Count == 0)
        {
            return;
        }

        AddSection("사용 위치", usages.Count);
        for (int i = 0; i < usages.Count; i++)
        {
            SequenceUsageRecord usage = usages[i];
            var row = new Button { text = FormatUsage(usage) };
            row.AddToClassList("sm-nav-usage-row");
            row.tooltip = UsageTooltip(usage);
            UnityEngine.Object source = usage.SourceSequence != null
                ? usage.SourceSequence
                : (UnityEngine.Object)usage.SourceScenario;
            if (source != null)
            {
                string blockId = usage.SourceBlockId;
                row.clicked += () => OpenRequested?.Invoke(
                    new SequenceNavigatorRequest(source, blockId));
            }
            else
            {
                row.SetEnabled(false);
            }

            _scroll.Add(row);
        }
    }

    private void AddBrokenReferenceGroup()
    {
        if (_usage == null || _usage.MissingTargets.Count == 0)
        {
            return;
        }

        AddSection("깨진 참조", _usage.MissingTargets.Count, true);
        for (int i = 0; i < _usage.MissingTargets.Count; i++)
        {
            SequenceUsageRecord usage = _usage.MissingTargets[i];
            var row = new Button
            {
                text = usage.TargetSequenceId + "  ·  " + FormatUsage(usage)
            };
            row.AddToClassList("sm-nav-usage-row");
            row.AddToClassList("is-error");
            UnityEngine.Object source = usage.SourceSequence != null
                ? usage.SourceSequence
                : (UnityEngine.Object)usage.SourceScenario;
            if (source != null)
            {
                string blockId = usage.SourceBlockId;
                row.clicked += () => OpenRequested?.Invoke(
                    new SequenceNavigatorRequest(source, blockId));
            }

            _scroll.Add(row);
        }
    }

    private void AddSection(string title, int count, bool error = false)
    {
        var header = new VisualElement();
        header.AddToClassList("sm-nav-section-header");
        header.EnableInClassList("is-error", error);
        var label = new Label(title);
        label.AddToClassList("sm-section-label");
        header.Add(label);
        var countLabel = new Label(count.ToString());
        countLabel.AddToClassList("sm-nav-section-count");
        header.Add(countLabel);
        _scroll.Add(header);
    }

    private void AddEmpty(string message)
    {
        var label = new Label(message);
        label.AddToClassList("sm-nav-empty");
        _scroll.Add(label);
    }

    private static string FormatUsage(SequenceUsageRecord usage)
    {
        string source = !string.IsNullOrWhiteSpace(usage.SourceScenarioId)
            ? usage.SourceScenarioId
            : usage.SourceSequenceId;
        string kind;
        switch (usage.Kind)
        {
            case SequenceUsageKind.LegacyBattleRule:
                kind = "기존 규칙";
                break;
            case SequenceUsageKind.TriggerRule:
                kind = "이벤트 규칙";
                break;
            case SequenceUsageKind.SequenceCall:
                kind = "시퀀스 호출";
                break;
            default:
                kind = "전투 흐름";
                break;
        }

        return source + "  ·  " + kind;
    }

    private static string UsageTooltip(SequenceUsageRecord usage)
    {
        if (!string.IsNullOrWhiteSpace(usage.SourceBlockId))
        {
            return "Block " + usage.SourceBlockId;
        }

        if (!string.IsNullOrWhiteSpace(usage.SourceRuleId))
        {
            return "Rule " + usage.SourceRuleId;
        }

        return usage.TargetSequenceId;
    }

    private static string DisplayName(SequenceAssetIndexEntry entry)
    {
        return string.IsNullOrWhiteSpace(entry.DisplayNameKo)
            ? entry.Id
            : entry.DisplayNameKo;
    }

    private static string BuildTooltip(SequenceAssetIndexEntry entry)
    {
        string owner = entry.OwningScenarioIds.Count > 0
            ? "\n전투 흐름: " + string.Join(", ", entry.OwningScenarioIds)
            : string.Empty;
        return entry.Id + owner + (!string.IsNullOrWhiteSpace(entry.AssetPath)
            ? "\n" + entry.AssetPath
            : string.Empty);
    }

    private static string Normalize(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
    }
}
