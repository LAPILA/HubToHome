using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

public sealed class SequenceReferencePickerOption
{
    public string Id = string.Empty;
    public string DisplayNameKo = string.Empty;
    public string Category = string.Empty;
    public string DescriptionKo = string.Empty;
    public readonly List<string> Keywords = new List<string>();
    public bool Deprecated;
}

public sealed class SequenceReferencePickerPopup : PopupWindowContent
{
    private readonly string _title;
    private readonly List<SequenceReferencePickerOption> _options;
    private readonly string _currentId;
    private readonly Action<string> _selected;
    private TextField _search;
    private ScrollView _results;
    private List<SequenceReferencePickerOption> _visible;

    private SequenceReferencePickerPopup(
        string title,
        IEnumerable<SequenceReferencePickerOption> options,
        string currentId,
        Action<string> selected)
    {
        _title = title ?? "선택";
        _options = options != null
            ? new List<SequenceReferencePickerOption>(options)
            : new List<SequenceReferencePickerOption>();
        _currentId = currentId ?? string.Empty;
        _selected = selected;
    }

    public static void Show(
        VisualElement anchor,
        string title,
        IEnumerable<SequenceReferencePickerOption> options,
        string currentId,
        Action<string> selected)
    {
        if (anchor == null)
        {
            return;
        }
        UnityEditor.PopupWindow.Show(
            anchor.worldBound,
            new SequenceReferencePickerPopup(title, options, currentId, selected));
    }

    public override Vector2 GetWindowSize()
    {
        return new Vector2(430f, 460f);
    }

    public override void OnOpen()
    {
        VisualElement root = editorWindow.rootVisualElement;
        StyleSheet style = AssetDatabase.LoadAssetAtPath<StyleSheet>(SequenceMakerWindow.UssPath);
        if (style != null)
        {
            root.styleSheets.Add(style);
        }
        root.AddToClassList("sm-reference-popup");
        var header = new Label(_title);
        header.AddToClassList("sm-reference-popup-title");
        root.Add(header);
        _search = new TextField();
        _search.AddToClassList("sm-reference-popup-search");
        _search.tooltip = "이름, ID, 설명, 카테고리, 태그 검색";
        _search.RegisterValueChangedCallback(_ => Render());
        _search.RegisterCallback<KeyDownEvent>(OnKeyDown);
        root.Add(_search);
        _results = new ScrollView(ScrollViewMode.Vertical);
        _results.AddToClassList("sm-reference-popup-results");
        root.Add(_results);
        Render();
        _search.schedule.Execute(() => _search.Focus());
    }

    private void Render()
    {
        if (_results == null)
        {
            return;
        }
        _results.Clear();
        _visible = Search(_options, _search?.value);
        if (_visible.Count == 0)
        {
            var empty = new Label("검색 결과 없음");
            empty.AddToClassList("sm-empty-copy");
            _results.Add(empty);
            return;
        }

        string category = null;
        for (int i = 0; i < _visible.Count; i++)
        {
            SequenceReferencePickerOption option = _visible[i];
            string nextCategory = string.IsNullOrWhiteSpace(option.Category)
                ? "기타"
                : option.Category.Trim();
            if (!string.Equals(category, nextCategory, StringComparison.Ordinal))
            {
                category = nextCategory;
                var categoryLabel = new Label(category);
                categoryLabel.AddToClassList("sm-reference-popup-category");
                _results.Add(categoryLabel);
            }
            _results.Add(CreateRow(option));
        }
    }

    private VisualElement CreateRow(SequenceReferencePickerOption option)
    {
        var row = new Button(() => Choose(option));
        row.AddToClassList("sm-reference-popup-row");
        row.EnableInClassList("is-selected", option.Id == _currentId);
        row.EnableInClassList("is-deprecated", option.Deprecated);
        var heading = new VisualElement();
        heading.AddToClassList("sm-reference-popup-heading");
        var name = new Label(string.IsNullOrWhiteSpace(option.DisplayNameKo)
            ? option.Id
            : option.DisplayNameKo);
        name.AddToClassList("sm-reference-popup-name");
        heading.Add(name);
        var id = new Label(option.Id);
        id.AddToClassList("sm-reference-popup-id");
        heading.Add(id);
        row.Add(heading);
        if (!string.IsNullOrWhiteSpace(option.DescriptionKo))
        {
            var description = new Label(option.DescriptionKo);
            description.AddToClassList("sm-reference-popup-description");
            row.Add(description);
        }
        return row;
    }

    private void OnKeyDown(KeyDownEvent evt)
    {
        if (evt.keyCode == KeyCode.Escape)
        {
            editorWindow.Close();
            evt.StopPropagation();
        }
        else if (evt.keyCode == KeyCode.Return && _visible != null && _visible.Count > 0)
        {
            Choose(_visible[0]);
            evt.StopPropagation();
        }
    }

    private void Choose(SequenceReferencePickerOption option)
    {
        _selected?.Invoke(option?.Id ?? string.Empty);
        editorWindow.Close();
    }

    internal static List<SequenceReferencePickerOption> Search(
        IEnumerable<SequenceReferencePickerOption> options,
        string query)
    {
        string normalized = (query ?? string.Empty).Trim();
        var result = new List<SequenceReferencePickerOption>();
        if (options == null)
        {
            return result;
        }
        foreach (SequenceReferencePickerOption option in options)
        {
            if (option == null || string.IsNullOrWhiteSpace(option.Id)
                || (!string.IsNullOrEmpty(normalized) && !Matches(option, normalized)))
            {
                continue;
            }
            result.Add(option);
        }
        result.Sort((left, right) =>
        {
            int category = string.Compare(left.Category, right.Category, StringComparison.OrdinalIgnoreCase);
            return category != 0
                ? category
                : string.Compare(
                    string.IsNullOrWhiteSpace(left.DisplayNameKo) ? left.Id : left.DisplayNameKo,
                    string.IsNullOrWhiteSpace(right.DisplayNameKo) ? right.Id : right.DisplayNameKo,
                    StringComparison.OrdinalIgnoreCase);
        });
        return result;
    }

    private static bool Matches(SequenceReferencePickerOption option, string query)
    {
        if (Contains(option.Id, query)
            || Contains(option.DisplayNameKo, query)
            || Contains(option.Category, query)
            || Contains(option.DescriptionKo, query))
        {
            return true;
        }
        for (int i = 0; i < option.Keywords.Count; i++)
        {
            if (Contains(option.Keywords[i], query))
            {
                return true;
            }
        }
        return false;
    }

    private static bool Contains(string value, string query)
    {
        return (value ?? string.Empty).IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0;
    }
}
