using System;
using UnityEngine.UIElements;

public sealed class TriggerRuleListView : VisualElement
{
    private readonly VisualElement _header;
    private readonly Label _count;
    private readonly ScrollView _list;
    private BattleScenarioData _battle;
    private TriggerLibraryAsset _library;
    private string _selectedRuleId = string.Empty;
    private int _selectedLegacyIndex = -1;

    public TriggerRuleListView()
    {
        AddToClassList("sm-rule-list");
        _header = new VisualElement();
        _header.AddToClassList("sm-rule-list-header");
        var title = new Label("이벤트 규칙");
        title.AddToClassList("sm-section-label");
        _header.Add(title);
        _count = new Label();
        _count.AddToClassList("sm-nav-section-count");
        _header.Add(_count);
        var add = new Button(() => AddRequested?.Invoke())
        {
            text = "+",
            tooltip = "새 when -> do 규칙 추가"
        };
        add.AddToClassList("sm-rule-add");
        _header.Add(add);
        Add(_header);

        _list = new ScrollView(ScrollViewMode.Vertical);
        _list.AddToClassList("sm-rule-list-scroll");
        Add(_list);
    }

    public event Action AddRequested;
    public event Action<string> TriggerRuleSelected;
    public event Action<int> LegacyRuleSelected;
    public event Action<string> DuplicateRequested;
    public event Action<string> DeleteRequested;
    public event Action<string, int> MoveRequested;
    public event Action<int> ConvertLegacyRequested;

    public void Bind(
        BattleScenarioData battle,
        TriggerLibraryAsset library,
        string selectedRuleId,
        int selectedLegacyIndex)
    {
        _battle = battle;
        _library = library;
        _selectedRuleId = selectedRuleId ?? string.Empty;
        _selectedLegacyIndex = selectedLegacyIndex;
        Render();
    }

    private void Render()
    {
        _list.Clear();
        int nativeCount = _battle?.TriggerRules?.Count ?? 0;
        int legacyCount = _battle?.Rules?.Count ?? 0;
        _count.text = (nativeCount + legacyCount).ToString();
        style.display = _battle == null ? DisplayStyle.None : DisplayStyle.Flex;
        if (_battle == null)
        {
            return;
        }

        if (nativeCount == 0 && legacyCount == 0)
        {
            var empty = new Label("규칙 없음");
            empty.AddToClassList("sm-nav-empty");
            _list.Add(empty);
            return;
        }

        for (int i = 0; i < nativeCount; i++)
        {
            _list.Add(CreateNativeRow(_battle.TriggerRules[i], i, nativeCount));
        }

        if (legacyCount > 0)
        {
            var legacyHeader = new Label("기존 호환 규칙");
            legacyHeader.AddToClassList("sm-rule-legacy-header");
            _list.Add(legacyHeader);
            for (int i = 0; i < legacyCount; i++)
            {
                _list.Add(CreateLegacyRow(_battle.Rules[i], i));
            }
        }
    }

    private VisualElement CreateNativeRow(
        ScenarioTriggerRuleData rule,
        int index,
        int count)
    {
        var row = new VisualElement();
        row.AddToClassList("sm-rule-row");
        row.EnableInClassList("is-selected", rule?.RuleId == _selectedRuleId);
        row.EnableInClassList("is-disabled", rule != null && rule.Disabled);

        var main = new Button(() => TriggerRuleSelected?.Invoke(rule?.RuleId ?? string.Empty));
        main.AddToClassList("sm-rule-row-main");
        var titleLine = new VisualElement();
        titleLine.AddToClassList("sm-rule-row-title-line");
        var title = new Label(DisplayName(rule));
        title.AddToClassList("sm-rule-row-title");
        titleLine.Add(title);
        if (rule != null && rule.Disabled)
        {
            var disabled = new Label("꺼짐");
            disabled.AddToClassList("sm-rule-badge");
            titleLine.Add(disabled);
        }
        main.Add(titleLine);
        var sentence = new Label(TriggerRuleSentenceFormatter.FormatCompact(rule, _library));
        sentence.AddToClassList("sm-rule-row-sentence");
        main.Add(sentence);
        row.Add(main);

        var commands = new VisualElement();
        commands.AddToClassList("sm-rule-row-commands");
        commands.Add(Command("↑", "위로", () => MoveRequested?.Invoke(rule.RuleId, index - 1), index > 0));
        commands.Add(Command("↓", "아래로", () => MoveRequested?.Invoke(rule.RuleId, index + 1), index < count - 1));
        commands.Add(Command("⧉", "복제", () => DuplicateRequested?.Invoke(rule.RuleId), true));
        commands.Add(Command("×", "삭제", () => DeleteRequested?.Invoke(rule.RuleId), true));
        row.Add(commands);
        return row;
    }

    private VisualElement CreateLegacyRow(BattleEventRuleData rule, int index)
    {
        var row = new VisualElement();
        row.AddToClassList("sm-rule-row");
        row.AddToClassList("is-legacy");
        row.EnableInClassList("is-selected", index == _selectedLegacyIndex);
        var main = new Button(() => LegacyRuleSelected?.Invoke(index));
        main.AddToClassList("sm-rule-row-main");
        var titleLine = new VisualElement();
        titleLine.AddToClassList("sm-rule-row-title-line");
        var title = new Label(string.IsNullOrWhiteSpace(rule?.RuleId)
            ? "기존 규칙 " + (index + 1)
            : rule.RuleId);
        title.AddToClassList("sm-rule-row-title");
        titleLine.Add(title);
        var badge = new Label("호환");
        badge.AddToClassList("sm-rule-badge");
        badge.AddToClassList("is-warning");
        titleLine.Add(badge);
        main.Add(titleLine);
        string sentence;
        if (BattleTriggerRuleCompatibilityMapper.TryMap(
                rule,
                out ScenarioTriggerRuleData mapped,
                out string error))
        {
            sentence = TriggerRuleSentenceFormatter.FormatCompact(mapped, _library);
        }
        else
        {
            sentence = error;
        }
        var copy = new Label(sentence);
        copy.AddToClassList("sm-rule-row-sentence");
        main.Add(copy);
        row.Add(main);
        row.Add(Command("변환", "확장 Trigger Rule로 변환", () => ConvertLegacyRequested?.Invoke(index), true));
        return row;
    }

    private static Button Command(string text, string tooltip, Action clicked, bool enabled)
    {
        var button = new Button(clicked) { text = text, tooltip = tooltip };
        button.AddToClassList("sm-rule-command");
        button.SetEnabled(enabled);
        return button;
    }

    private static string DisplayName(ScenarioTriggerRuleData rule)
    {
        return !string.IsNullOrWhiteSpace(rule?.DisplayNameKo)
            ? rule.DisplayNameKo.Trim()
            : (!string.IsNullOrWhiteSpace(rule?.RuleId) ? rule.RuleId : "이름 없는 규칙");
    }
}
