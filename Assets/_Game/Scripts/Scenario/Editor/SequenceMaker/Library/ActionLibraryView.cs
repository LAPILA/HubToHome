using System;
using System.Collections.Generic;
using UnityEngine.UIElements;

public sealed class ActionLibraryView : VisualElement
{
    private const string AllCategory = "__all";
    private const string FavoritesCategory = "__favorites";
    private const string RecentCategory = "__recent";

    private readonly TextField _searchField;
    private readonly VisualElement _categoryList;
    private readonly Label _resultCount;
    private readonly ScrollView _resultList;
    private readonly ScrollView _detail;
    private readonly List<ActionPickerSearchResult> _visibleResults =
        new List<ActionPickerSearchResult>();

    private ActionCatalogAsset _catalog;
    private ActionPickerContext _context;
    private ActionPickerHistory _history;
    private string _selectedCategory = AllCategory;
    private ActionPickerSearchResult _selected;
    private string _commandLabel = "액션 추가";
    private bool _canPick;
    private Func<string, int> _usageCount;

    public ActionLibraryView()
    {
        AddToClassList("sm-action-library");
        focusable = true;
        RegisterCallback<KeyDownEvent>(OnKeyDown);

        var top = new VisualElement();
        top.AddToClassList("sm-library-top");
        var searchIcon = new Label("⌕");
        searchIcon.AddToClassList("sm-library-search-icon");
        top.Add(searchIcon);
        _searchField = new TextField
        {
            name = "action-search",
            tooltip = "한국어 이름, Action ID, 설명, 태그, 별칭, 파라미터 검색"
        };
        _searchField.AddToClassList("sm-library-search");
        _searchField.RegisterValueChangedCallback(_ => RefreshResults());
        top.Add(_searchField);
        _resultCount = new Label();
        _resultCount.AddToClassList("sm-library-count");
        top.Add(_resultCount);
        Add(top);

        var content = new VisualElement();
        content.AddToClassList("sm-library-content");

        _categoryList = new VisualElement();
        _categoryList.AddToClassList("sm-library-categories");
        content.Add(_categoryList);

        _resultList = new ScrollView(ScrollViewMode.Vertical);
        _resultList.AddToClassList("sm-library-results");
        content.Add(_resultList);

        _detail = new ScrollView(ScrollViewMode.Vertical);
        _detail.AddToClassList("sm-library-detail");
        content.Add(_detail);
        Add(content);
    }

    public event Action<ActionCatalogEntry> Picked;

    public void Bind(
        ActionCatalogAsset catalog,
        ActionPickerContext context,
        ActionPickerHistory history,
        bool canPick,
        string commandLabel,
        Func<string, int> usageCount = null)
    {
        _catalog = catalog;
        _context = context ?? new ActionPickerContext(string.Empty);
        _history = history;
        _canPick = canPick;
        _commandLabel = string.IsNullOrWhiteSpace(commandLabel)
            ? "액션 선택"
            : commandLabel.Trim();
        _usageCount = usageCount;
        BuildCategories();
        RefreshResults();
        schedule.Execute(() => _searchField.Focus());
    }

    private void BuildCategories()
    {
        _categoryList.Clear();
        AddCategory(AllCategory, "모든 액션", "전체");
        AddCategory(FavoritesCategory, "즐겨찾기", "★");
        AddCategory(RecentCategory, "최근 사용", "↺");

        var categories = new List<string>();
        if (_catalog?.Entries != null)
        {
            for (int i = 0; i < _catalog.Entries.Count; i++)
            {
                string category = Normalize(_catalog.Entries[i]?.Category);
                if (!string.IsNullOrEmpty(category) && !categories.Contains(category))
                {
                    categories.Add(category);
                }
            }
        }
        categories.Sort(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < categories.Count; i++)
        {
            string category = categories[i];
            AddCategory(category, CategoryName(category), CategoryGlyph(category));
        }
    }

    private void AddCategory(string id, string label, string glyph)
    {
        var button = new Button(() =>
        {
            _selectedCategory = id;
            BuildCategories();
            RefreshResults();
        });
        button.AddToClassList("sm-library-category");
        button.EnableInClassList("is-selected", id == _selectedCategory);
        var icon = new Label(glyph ?? string.Empty);
        icon.AddToClassList("sm-library-category-icon");
        button.Add(icon);
        var text = new Label(label ?? string.Empty);
        text.AddToClassList("sm-library-category-label");
        button.Add(text);
        _categoryList.Add(button);
    }

    private void RefreshResults()
    {
        _resultList.Clear();
        _visibleResults.Clear();
        IReadOnlyList<ActionPickerSearchResult> searched = ActionPickerSearch.Search(
            _catalog,
            _searchField.value,
            _context);
        for (int i = 0; i < searched.Count; i++)
        {
            ActionPickerSearchResult result = searched[i];
            if (MatchesCategory(result.Entry))
            {
                _visibleResults.Add(result);
            }
        }
        if (_selectedCategory == RecentCategory)
        {
            _visibleResults.Sort(CompareRecent);
        }

        _resultCount.text = _visibleResults.Count + "개";
        for (int i = 0; i < _visibleResults.Count; i++)
        {
            AddResultRow(_visibleResults[i]);
        }

        if (_visibleResults.Count == 0)
        {
            var empty = new VisualElement();
            empty.AddToClassList("sm-library-empty");
            var emptyTitle = new Label("검색 결과가 없습니다.");
            emptyTitle.AddToClassList("sm-library-empty-title");
            empty.Add(emptyTitle);
            empty.Add(new Label("다른 이름, ID, 태그로 검색해보세요."));
            _resultList.Add(empty);
            _selected = null;
        }
        else if (_selected == null || !_visibleResults.Contains(_selected))
        {
            _selected = _visibleResults[0];
        }

        RenderDetail();
    }

    private void AddResultRow(ActionPickerSearchResult result)
    {
        var row = new VisualElement();
        row.AddToClassList("sm-action-result");
        row.EnableInClassList("is-selected", result == _selected);
        row.EnableInClassList("is-unavailable", !result.CanSelect);

        var main = new Button(() => Select(result));
        main.AddToClassList("sm-action-result-main");
        main.RegisterCallback<ClickEvent>(evt =>
        {
            if (evt.clickCount >= 2)
            {
                TryPick(result);
                evt.StopPropagation();
            }
        });
        var titleRow = new VisualElement();
        titleRow.AddToClassList("sm-action-result-title-row");
        var accent = new VisualElement();
        accent.AddToClassList("sm-action-result-accent");
        titleRow.Add(accent);
        var title = new Label(DisplayName(result.Entry));
        title.AddToClassList("sm-action-result-title");
        titleRow.Add(title);
        titleRow.Add(ResultBadge(result));
        main.Add(titleRow);
        var id = new Label(result.Entry.ActionId);
        id.AddToClassList("sm-action-result-id");
        main.Add(id);
        string description = !string.IsNullOrWhiteSpace(result.Entry.UsageKo)
            ? result.Entry.UsageKo
            : result.Entry.DescriptionKo;
        if (!string.IsNullOrWhiteSpace(description))
        {
            var copy = new Label(Short(description, 92));
            copy.AddToClassList("sm-action-result-copy");
            main.Add(copy);
        }
        row.Add(main);

        var favorite = new Button(() =>
        {
            _history?.ToggleFavorite(result.Entry.ActionId);
            BuildCategories();
            RefreshResults();
        })
        {
            text = _history != null && _history.IsFavorite(result.Entry.ActionId) ? "★" : "☆",
            tooltip = "즐겨찾기 전환"
        };
        favorite.AddToClassList("sm-action-favorite");
        row.Add(favorite);
        _resultList.Add(row);
    }

    private void Select(ActionPickerSearchResult result)
    {
        _selected = result;
        RefreshResultSelectionClasses();
        RenderDetail();
    }

    private void RefreshResultSelectionClasses()
    {
        List<VisualElement> rows = _resultList.Query<VisualElement>(className: "sm-action-result").ToList();
        for (int i = 0; i < rows.Count && i < _visibleResults.Count; i++)
        {
            rows[i].EnableInClassList("is-selected", _visibleResults[i] == _selected);
        }
    }

    private void RenderDetail()
    {
        _detail.Clear();
        if (_selected == null)
        {
            _detail.Add(new Label("액션을 선택하면 상세 정보가 표시됩니다."));
            return;
        }

        ActionCatalogEntry entry = _selected.Entry;
        var eyebrow = new Label(CategoryName(entry.Category)
            + (string.IsNullOrWhiteSpace(entry.Subcategory) ? string.Empty : " / " + entry.Subcategory));
        eyebrow.AddToClassList("sm-library-detail-eyebrow");
        _detail.Add(eyebrow);
        var title = new Label(DisplayName(entry));
        title.AddToClassList("sm-library-detail-title");
        _detail.Add(title);
        var id = new Label(entry.ActionId);
        id.AddToClassList("sm-library-detail-id");
        _detail.Add(id);

        if (!_selected.CanSelect)
        {
            var reason = new Label(_selected.CompatibilityReason);
            reason.AddToClassList("sm-library-compatibility-reason");
            _detail.Add(reason);
        }
        if (!string.IsNullOrWhiteSpace(entry.DescriptionKo))
        {
            var description = new Label(entry.DescriptionKo.Trim());
            description.AddToClassList("sm-library-detail-description");
            _detail.Add(description);
        }
        if (!string.IsNullOrWhiteSpace(entry.UsageKo))
        {
            _detail.Add(DetailSection("언제 사용", entry.UsageKo));
        }
        if (_usageCount != null)
        {
            int count = Math.Max(0, _usageCount(entry.ActionId));
            _detail.Add(DetailSection("현재 프로젝트", count + "개 블록에서 사용"));
        }

        if (entry.Parameters != null && entry.Parameters.Count > 0)
        {
            var parameterSection = new VisualElement();
            parameterSection.AddToClassList("sm-library-parameter-section");
            parameterSection.Add(DetailHeading("설정 값"));
            for (int i = 0; i < entry.Parameters.Count; i++)
            {
                ActionCatalogParameter parameter = entry.Parameters[i];
                if (parameter == null)
                {
                    continue;
                }
                var row = new VisualElement();
                row.AddToClassList("sm-library-parameter-row");
                var name = new Label(ParameterName(parameter));
                name.AddToClassList("sm-library-parameter-name");
                row.Add(name);
                var description = new Label(parameter.DescriptionKo ?? string.Empty);
                description.AddToClassList("sm-library-parameter-copy");
                row.Add(description);
                parameterSection.Add(row);
            }
            _detail.Add(parameterSection);
        }

        if (!string.IsNullOrWhiteSpace(entry.ExampleYaml))
        {
            var exampleSection = new VisualElement();
            exampleSection.AddToClassList("sm-library-example-section");
            exampleSection.Add(DetailHeading("예시"));
            var example = new TextField
            {
                value = entry.ExampleYaml,
                multiline = true,
                isReadOnly = true
            };
            example.AddToClassList("sm-library-example");
            exampleSection.Add(example);
            _detail.Add(exampleSection);
        }

        if (_canPick)
        {
            var command = new Button(() => TryPick(_selected)) { text = _commandLabel };
            command.AddToClassList("sm-library-primary-command");
            command.SetEnabled(_selected.CanSelect);
            command.tooltip = !_selected.CanSelect ? _selected.CompatibilityReason : _commandLabel;
            _detail.Add(command);
        }
    }

    private void TryPick(ActionPickerSearchResult result)
    {
        if (!_canPick || result == null || !result.CanSelect)
        {
            return;
        }
        _history?.RecordRecent(result.Entry.ActionId);
        Picked?.Invoke(result.Entry);
    }

    private bool MatchesCategory(ActionCatalogEntry entry)
    {
        switch (_selectedCategory)
        {
            case AllCategory: return true;
            case FavoritesCategory: return _history != null && _history.IsFavorite(entry.ActionId);
            case RecentCategory: return _history != null
                && IndexOf(_history.Recent, entry.ActionId) != int.MaxValue;
            default: return string.Equals(
                Normalize(entry.Category),
                _selectedCategory,
                StringComparison.OrdinalIgnoreCase);
        }
    }

    private int CompareRecent(ActionPickerSearchResult left, ActionPickerSearchResult right)
    {
        if (_history == null)
        {
            return 0;
        }
        return IndexOf(_history.Recent, left.Entry.ActionId)
            .CompareTo(IndexOf(_history.Recent, right.Entry.ActionId));
    }

    private void OnKeyDown(KeyDownEvent evt)
    {
        if (_visibleResults.Count == 0)
        {
            return;
        }
        int index = Math.Max(0, _visibleResults.IndexOf(_selected));
        if (evt.keyCode == UnityEngine.KeyCode.DownArrow)
        {
            _selected = _visibleResults[Math.Min(_visibleResults.Count - 1, index + 1)];
        }
        else if (evt.keyCode == UnityEngine.KeyCode.UpArrow)
        {
            _selected = _visibleResults[Math.Max(0, index - 1)];
        }
        else if (evt.keyCode == UnityEngine.KeyCode.Return
            || evt.keyCode == UnityEngine.KeyCode.KeypadEnter)
        {
            TryPick(_selected);
        }
        else
        {
            return;
        }
        RefreshResultSelectionClasses();
        RenderDetail();
        evt.StopPropagation();
    }

    private static VisualElement DetailSection(string title, string copy)
    {
        var section = new VisualElement();
        section.AddToClassList("sm-library-detail-section");
        section.Add(DetailHeading(title));
        var value = new Label(copy ?? string.Empty);
        value.AddToClassList("sm-library-detail-copy");
        section.Add(value);
        return section;
    }

    private static Label DetailHeading(string text)
    {
        var heading = new Label(text ?? string.Empty);
        heading.AddToClassList("sm-library-detail-heading");
        return heading;
    }

    private static Label ResultBadge(ActionPickerSearchResult result)
    {
        string text;
        string modifier;
        switch (result.Compatibility)
        {
            case ActionPickerCompatibility.Deprecated:
                text = "호환용";
                modifier = "is-warning";
                break;
            case ActionPickerCompatibility.Unavailable:
                text = "사용 불가";
                modifier = "is-error";
                break;
            default:
                text = CategoryName(result.Entry.Category);
                modifier = "is-ok";
                break;
        }
        var badge = new Label(text);
        badge.AddToClassList("sm-action-result-badge");
        badge.AddToClassList(modifier);
        return badge;
    }

    private static int IndexOf(IReadOnlyList<string> values, string value)
    {
        for (int i = 0; i < values.Count; i++)
        {
            if (values[i] == value)
            {
                return i;
            }
        }
        return int.MaxValue;
    }

    private static string ParameterName(ActionCatalogParameter parameter)
    {
        string display = !string.IsNullOrWhiteSpace(parameter.DisplayNameKo)
            ? parameter.DisplayNameKo.Trim()
            : parameter.Name;
        return display + (parameter.Required ? " *" : string.Empty);
    }

    private static string DisplayName(ActionCatalogEntry entry)
    {
        return !string.IsNullOrWhiteSpace(entry?.DisplayNameKo)
            ? entry.DisplayNameKo.Trim()
            : entry?.ActionId ?? "액션";
    }

    private static string CategoryName(string category)
    {
        switch (Normalize(category).ToLowerInvariant())
        {
            case "flow": return "흐름";
            case "dialogue": return "대화";
            case "screen": return "화면";
            case "audio": return "오디오";
            case "module": return "게임 모듈";
            case "actor": return "캐릭터";
            case "battle": return "전투";
            case "camera": return "카메라";
            case "cinematic": return "시네마틱";
            case "timeline": return "타임라인";
            default: return string.IsNullOrWhiteSpace(category) ? "기타" : category;
        }
    }

    private static string CategoryGlyph(string category)
    {
        switch (Normalize(category).ToLowerInvariant())
        {
            case "flow": return "⇢";
            case "dialogue": return "…";
            case "screen": return "▣";
            case "audio": return "♪";
            case "module": return "⌘";
            case "actor": return "●";
            case "battle": return "✦";
            case "camera": return "◎";
            case "cinematic": return "▰";
            case "timeline": return "▥";
            default: return "·";
        }
    }

    private static string Short(string value, int max)
    {
        string normalized = Normalize(value);
        return normalized.Length <= max ? normalized : normalized.Substring(0, max - 3) + "...";
    }

    private static string Normalize(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
    }
}
