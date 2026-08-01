using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

public enum FlagValueComparison
{
    Equal,
    NotEqual,
    GreaterOrEqual,
    LessOrEqual,
    Greater,
    Less
}

public static class FlagValueComparisonUtility
{
    public static bool Evaluate(int actual, FlagValueComparison comparison, int expected)
    {
        switch (comparison)
        {
            case FlagValueComparison.Equal: return actual == expected;
            case FlagValueComparison.NotEqual: return actual != expected;
            case FlagValueComparison.GreaterOrEqual: return actual >= expected;
            case FlagValueComparison.LessOrEqual: return actual <= expected;
            case FlagValueComparison.Greater: return actual > expected;
            case FlagValueComparison.Less: return actual < expected;
            default: return false;
        }
    }
}

[Serializable]
public sealed class FlagDialogueRule
{
    [HorizontalGroup("Condition", Width = 0.34f)]
    [SerializeField, LabelText("Flag ID")]
    private string _flagKey;

    [HorizontalGroup("Condition", Width = 0.28f)]
    [SerializeField, LabelText("비교")]
    private FlagValueComparison _comparison = FlagValueComparison.GreaterOrEqual;

    [HorizontalGroup("Condition", Width = 0.18f)]
    [SerializeField, LabelText("값")]
    private int _expectedValue = 1;

    [HorizontalGroup("Condition", Width = 0.2f)]
    [SerializeField, LabelText("우선순위")]
    private int _priority;

    [SerializeField, Required, LabelText("DialogueData")]
    private DialogueData _dialogue;

    public FlagDialogueRule()
    {
    }

    public FlagDialogueRule(
        string flagKey,
        FlagValueComparison comparison,
        int expectedValue,
        int priority,
        DialogueData dialogue)
    {
        _flagKey = Normalize(flagKey);
        _comparison = comparison;
        _expectedValue = expectedValue;
        _priority = priority;
        _dialogue = dialogue;
    }

    public string FlagKey => Normalize(_flagKey);
    public FlagValueComparison Comparison => _comparison;
    public int ExpectedValue => _expectedValue;
    public int Priority => _priority;
    public DialogueData Dialogue => _dialogue;

    public bool Matches(GlobalDataManager global)
    {
        if (global == null || string.IsNullOrEmpty(FlagKey))
            return false;

        return FlagValueComparisonUtility.Evaluate(
            global.GetFlag(FlagKey, 0),
            _comparison,
            _expectedValue);
    }

    public bool TryValidate(out string error)
    {
        if (string.IsNullOrEmpty(FlagKey))
        {
            error = "Flag ID가 비어 있습니다.";
            return false;
        }

        if (_dialogue == null)
        {
            error = $"{FlagKey} 조건의 DialogueData가 비어 있습니다.";
            return false;
        }

        error = string.Empty;
        return true;
    }

    private static string Normalize(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
    }
}

[CreateAssetMenu(
    fileName = "FlagDialogueSelector",
    menuName = "Hub To Home/Overworld/Flag Dialogue Selector")]
public sealed class FlagDialogueSelector : ScriptableObject
{
    [TitleGroup("조건별 대화")]
    [InfoBox("조건이 여러 개 맞으면 Priority가 가장 높은 대화를 선택합니다. 같은 Priority는 위쪽 항목이 우선합니다.")]
    [SerializeField, ListDrawerSettings(ShowIndexLabels = true), LabelText("Rules")]
    private List<FlagDialogueRule> _rules = new List<FlagDialogueRule>();

    [TitleGroup("Fallback")]
    [SerializeField, LabelText("Selector Fallback")]
    private DialogueData _fallbackDialogue;

    public IReadOnlyList<FlagDialogueRule> Rules => _rules;
    public DialogueData FallbackDialogue => _fallbackDialogue;
    public bool HasAnyDialogue => _fallbackDialogue != null || HasRuleDialogue();

    public void Configure(IEnumerable<FlagDialogueRule> rules, DialogueData fallbackDialogue)
    {
        _rules = rules != null
            ? new List<FlagDialogueRule>(rules)
            : new List<FlagDialogueRule>();
        _fallbackDialogue = fallbackDialogue;
    }

    public DialogueData Resolve(
        GlobalDataManager global,
        DialogueData callerFallback = null)
    {
        FlagDialogueRule selected = null;
        if (_rules != null && global != null)
        {
            for (int i = 0; i < _rules.Count; i++)
            {
                FlagDialogueRule candidate = _rules[i];
                if (candidate == null || candidate.Dialogue == null || !candidate.Matches(global))
                    continue;

                if (selected == null || candidate.Priority > selected.Priority)
                    selected = candidate;
            }
        }

        if (selected != null)
            return selected.Dialogue;
        return _fallbackDialogue != null ? _fallbackDialogue : callerFallback;
    }

    public bool TryValidate(out string error)
    {
        if ((_rules == null || _rules.Count == 0) && _fallbackDialogue == null)
        {
            error = "Rule 또는 Selector Fallback이 하나 이상 필요합니다.";
            return false;
        }

        if (_rules != null)
        {
            for (int i = 0; i < _rules.Count; i++)
            {
                if (_rules[i] == null)
                {
                    error = $"Rule #{i + 1}이 비어 있습니다.";
                    return false;
                }

                if (!_rules[i].TryValidate(out string ruleError))
                {
                    error = $"Rule #{i + 1}: {ruleError}";
                    return false;
                }
            }
        }

        error = string.Empty;
        return true;
    }

    [TitleGroup("검증")]
    [Button("Selector 검증")]
    private void ValidateAndLog()
    {
        if (TryValidate(out string error))
            Debug.Log("[FlagDialogueSelector] 검증 통과", this);
        else
            Debug.LogError("[FlagDialogueSelector] " + error, this);
    }

    private bool HasRuleDialogue()
    {
        if (_rules == null)
            return false;

        for (int i = 0; i < _rules.Count; i++)
        {
            if (_rules[i] != null && _rules[i].Dialogue != null)
                return true;
        }

        return false;
    }
}