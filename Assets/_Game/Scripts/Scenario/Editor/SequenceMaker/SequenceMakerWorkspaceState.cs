using System;
using UnityEditor;
using UnityEngine;

public enum SequenceMakerTargetKind
{
    None,
    BattleScenario,
    StandaloneSequence
}

public enum SequenceMakerDrawerTab
{
    Problems,
    Trace,
    Yaml
}

public enum SequenceMakerDensity
{
    Comfortable,
    Compact
}

public interface ISequenceMakerPreferences
{
    float GetFloat(string key, float defaultValue);
    bool GetBool(string key, bool defaultValue);
    string GetString(string key, string defaultValue);
    void SetFloat(string key, float value);
    void SetBool(string key, bool value);
    void SetString(string key, string value);
}

public sealed class SequenceMakerWorkspaceState
{
    public const string NavigatorWidthKey = "HubToHome.SequenceMaker.NavigatorWidth";
    public const string InspectorWidthKey = "HubToHome.SequenceMaker.InspectorWidth";
    public const string DrawerHeightKey = "HubToHome.SequenceMaker.DrawerHeight";
    public const string DrawerOpenKey = "HubToHome.SequenceMaker.DrawerOpen";
    public const string DrawerTabKey = "HubToHome.SequenceMaker.DrawerTab";
    public const string DensityKey = "HubToHome.SequenceMaker.Density";

    public const float MinNavigatorWidth = 200f;
    public const float MaxNavigatorWidth = 420f;
    public const float MinInspectorWidth = 280f;
    public const float MaxInspectorWidth = 560f;
    public const float MinDrawerHeight = 120f;
    public const float MaxDrawerHeight = 420f;

    public event Action Changed;

    public SequenceMakerTargetKind TargetKind { get; private set; }
    public BattleScenarioData BattleScenario { get; private set; }
    public ActionSequenceAsset StandaloneSequence { get; private set; }
    public ActionSequenceAsset SelectedSequence { get; private set; }
    public string SelectedBlockId { get; private set; } = string.Empty;
    public bool IsDirty { get; private set; }
    public bool IsDrawerOpen { get; private set; }
    public SequenceMakerDrawerTab DrawerTab { get; private set; } = SequenceMakerDrawerTab.Problems;
    public SequenceMakerDensity Density { get; private set; } = SequenceMakerDensity.Comfortable;
    public float NavigatorWidth { get; private set; } = 248f;
    public float InspectorWidth { get; private set; } = 340f;
    public float DrawerHeight { get; private set; } = 190f;

    public bool HasTarget => TargetKind != SequenceMakerTargetKind.None;
    public UnityEngine.Object ActiveTarget => TargetKind == SequenceMakerTargetKind.BattleScenario
        ? BattleScenario
        : (UnityEngine.Object)StandaloneSequence;

    public void SetBattleScenario(BattleScenarioData scenario)
    {
        ActionSequenceAsset selected = FirstSequence(scenario);
        bool changed = TargetKind != (scenario != null
                ? SequenceMakerTargetKind.BattleScenario
                : SequenceMakerTargetKind.None)
            || BattleScenario != scenario
            || StandaloneSequence != null
            || SelectedSequence != selected
            || !string.IsNullOrEmpty(SelectedBlockId)
            || IsDirty;
        if (!changed)
        {
            return;
        }

        TargetKind = scenario != null
            ? SequenceMakerTargetKind.BattleScenario
            : SequenceMakerTargetKind.None;
        BattleScenario = scenario;
        StandaloneSequence = null;
        SelectedSequence = selected;
        SelectedBlockId = string.Empty;
        IsDirty = false;
        RaiseChanged();
    }

    public void SetStandaloneSequence(ActionSequenceAsset sequence)
    {
        bool changed = TargetKind != (sequence != null
                ? SequenceMakerTargetKind.StandaloneSequence
                : SequenceMakerTargetKind.None)
            || StandaloneSequence != sequence
            || BattleScenario != null
            || SelectedSequence != sequence
            || !string.IsNullOrEmpty(SelectedBlockId)
            || IsDirty;
        if (!changed)
        {
            return;
        }

        TargetKind = sequence != null
            ? SequenceMakerTargetKind.StandaloneSequence
            : SequenceMakerTargetKind.None;
        BattleScenario = null;
        StandaloneSequence = sequence;
        SelectedSequence = sequence;
        SelectedBlockId = string.Empty;
        IsDirty = false;
        RaiseChanged();
    }

    public bool TrySelectSequence(ActionSequenceAsset sequence)
    {
        if (!CanSelect(sequence))
        {
            return false;
        }

        if (SelectedSequence == sequence)
        {
            return true;
        }

        SelectedSequence = sequence;
        SelectedBlockId = string.Empty;
        RaiseChanged();
        return true;
    }

    public void SelectBlock(string blockId)
    {
        string normalized = Normalize(blockId);
        if (string.Equals(SelectedBlockId, normalized, StringComparison.Ordinal))
        {
            return;
        }

        SelectedBlockId = normalized;
        RaiseChanged();
    }

    public void SetDirty(bool dirty)
    {
        if (IsDirty == dirty)
        {
            return;
        }

        IsDirty = dirty;
        RaiseChanged();
    }

    public void SetDrawer(SequenceMakerDrawerTab tab, bool isOpen)
    {
        if (DrawerTab == tab && IsDrawerOpen == isOpen)
        {
            return;
        }

        DrawerTab = tab;
        IsDrawerOpen = isOpen;
        RaiseChanged();
    }

    public void SetDrawerOpen(bool isOpen)
    {
        if (IsDrawerOpen == isOpen)
        {
            return;
        }

        IsDrawerOpen = isOpen;
        RaiseChanged();
    }

    public void SetDensity(SequenceMakerDensity density)
    {
        if (Density == density)
        {
            return;
        }

        Density = density;
        RaiseChanged();
    }

    public void SetLayout(float navigatorWidth, float inspectorWidth, float drawerHeight)
    {
        float nextNavigator = Mathf.Clamp(navigatorWidth, MinNavigatorWidth, MaxNavigatorWidth);
        float nextInspector = Mathf.Clamp(inspectorWidth, MinInspectorWidth, MaxInspectorWidth);
        float nextDrawer = Mathf.Clamp(drawerHeight, MinDrawerHeight, MaxDrawerHeight);
        if (Mathf.Approximately(NavigatorWidth, nextNavigator)
            && Mathf.Approximately(InspectorWidth, nextInspector)
            && Mathf.Approximately(DrawerHeight, nextDrawer))
        {
            return;
        }

        NavigatorWidth = nextNavigator;
        InspectorWidth = nextInspector;
        DrawerHeight = nextDrawer;
        RaiseChanged();
    }

    public void LoadPreferences(ISequenceMakerPreferences preferences)
    {
        if (preferences == null)
        {
            throw new ArgumentNullException(nameof(preferences));
        }

        NavigatorWidth = Mathf.Clamp(
            preferences.GetFloat(NavigatorWidthKey, NavigatorWidth),
            MinNavigatorWidth,
            MaxNavigatorWidth);
        InspectorWidth = Mathf.Clamp(
            preferences.GetFloat(InspectorWidthKey, InspectorWidth),
            MinInspectorWidth,
            MaxInspectorWidth);
        DrawerHeight = Mathf.Clamp(
            preferences.GetFloat(DrawerHeightKey, DrawerHeight),
            MinDrawerHeight,
            MaxDrawerHeight);
        IsDrawerOpen = preferences.GetBool(DrawerOpenKey, IsDrawerOpen);
        DrawerTab = ParseEnum(
            preferences.GetString(DrawerTabKey, DrawerTab.ToString()),
            DrawerTab);
        Density = ParseEnum(
            preferences.GetString(DensityKey, Density.ToString()),
            Density);
        RaiseChanged();
    }

    public void SavePreferences(ISequenceMakerPreferences preferences)
    {
        if (preferences == null)
        {
            throw new ArgumentNullException(nameof(preferences));
        }

        preferences.SetFloat(NavigatorWidthKey, NavigatorWidth);
        preferences.SetFloat(InspectorWidthKey, InspectorWidth);
        preferences.SetFloat(DrawerHeightKey, DrawerHeight);
        preferences.SetBool(DrawerOpenKey, IsDrawerOpen);
        preferences.SetString(DrawerTabKey, DrawerTab.ToString());
        preferences.SetString(DensityKey, Density.ToString());
    }

    private bool CanSelect(ActionSequenceAsset sequence)
    {
        if (sequence == null)
        {
            return TargetKind == SequenceMakerTargetKind.BattleScenario;
        }

        if (TargetKind == SequenceMakerTargetKind.StandaloneSequence)
        {
            return sequence == StandaloneSequence;
        }

        if (TargetKind != SequenceMakerTargetKind.BattleScenario
            || BattleScenario == null
            || BattleScenario.Sequences == null)
        {
            return false;
        }

        return BattleScenario.Sequences.Contains(sequence);
    }

    private static ActionSequenceAsset FirstSequence(BattleScenarioData scenario)
    {
        if (scenario == null || scenario.Sequences == null)
        {
            return null;
        }

        for (int i = 0; i < scenario.Sequences.Count; i++)
        {
            if (scenario.Sequences[i] != null)
            {
                return scenario.Sequences[i];
            }
        }

        return null;
    }

    private static T ParseEnum<T>(string value, T fallback) where T : struct
    {
        return Enum.TryParse(value, true, out T parsed) ? parsed : fallback;
    }

    private static string Normalize(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
    }

    private void RaiseChanged()
    {
        Changed?.Invoke();
    }
}

public sealed class EditorSequenceMakerPreferences : ISequenceMakerPreferences
{
    public float GetFloat(string key, float defaultValue)
    {
        return EditorPrefs.GetFloat(key, defaultValue);
    }

    public bool GetBool(string key, bool defaultValue)
    {
        return EditorPrefs.GetBool(key, defaultValue);
    }

    public string GetString(string key, string defaultValue)
    {
        return EditorPrefs.GetString(key, defaultValue);
    }

    public void SetFloat(string key, float value)
    {
        EditorPrefs.SetFloat(key, value);
    }

    public void SetBool(string key, bool value)
    {
        EditorPrefs.SetBool(key, value);
    }

    public void SetString(string key, string value)
    {
        EditorPrefs.SetString(key, value ?? string.Empty);
    }
}
