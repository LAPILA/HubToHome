using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

public class SequenceMakerWorkspaceStateTests
{
    private readonly List<UnityEngine.Object> _created = new List<UnityEngine.Object>();

    [TearDown]
    public void TearDown()
    {
        for (int i = 0; i < _created.Count; i++)
        {
            UnityEngine.Object.DestroyImmediate(_created[i]);
        }

        _created.Clear();
    }

    [Test]
    public void NewWorkspaceHasExplicitEmptyTargetState()
    {
        var state = new SequenceMakerWorkspaceState();

        Assert.That(state.TargetKind, Is.EqualTo(SequenceMakerTargetKind.None));
        Assert.That(state.HasTarget, Is.False);
        Assert.That(state.BattleScenario, Is.Null);
        Assert.That(state.StandaloneSequence, Is.Null);
        Assert.That(state.SelectedSequence, Is.Null);
        Assert.That(state.SelectedBlockId, Is.Empty);
        Assert.That(state.IsDirty, Is.False);
    }

    [Test]
    public void BattleTargetClearsStandaloneAndSelectsFirstAvailableSequence()
    {
        ActionSequenceAsset standalone = CreateSequence("standalone");
        ActionSequenceAsset first = CreateSequence("battle.first");
        BattleScenarioData battle = CreateBattle("battle", null, first);
        var state = new SequenceMakerWorkspaceState();
        state.SetStandaloneSequence(standalone);
        state.SetDirty(true);

        state.SetBattleScenario(battle);

        Assert.That(state.TargetKind, Is.EqualTo(SequenceMakerTargetKind.BattleScenario));
        Assert.That(state.BattleScenario, Is.SameAs(battle));
        Assert.That(state.StandaloneSequence, Is.Null);
        Assert.That(state.SelectedSequence, Is.SameAs(first));
        Assert.That(state.IsDirty, Is.False);
    }

    [Test]
    public void StandaloneTargetClearsBattleAndBecomesSelectedSequence()
    {
        BattleScenarioData battle = CreateBattle("battle", CreateSequence("battle.first"));
        ActionSequenceAsset standalone = CreateSequence("standalone");
        var state = new SequenceMakerWorkspaceState();
        state.SetBattleScenario(battle);

        state.SetStandaloneSequence(standalone);

        Assert.That(state.TargetKind, Is.EqualTo(SequenceMakerTargetKind.StandaloneSequence));
        Assert.That(state.BattleScenario, Is.Null);
        Assert.That(state.StandaloneSequence, Is.SameAs(standalone));
        Assert.That(state.SelectedSequence, Is.SameAs(standalone));
    }

    [Test]
    public void BattleSelectionRejectsSequenceOutsideCurrentScenario()
    {
        ActionSequenceAsset owned = CreateSequence("owned");
        ActionSequenceAsset outside = CreateSequence("outside");
        var state = new SequenceMakerWorkspaceState();
        state.SetBattleScenario(CreateBattle("battle", owned));

        bool changed = state.TrySelectSequence(outside);

        Assert.That(changed, Is.False);
        Assert.That(state.SelectedSequence, Is.SameAs(owned));
    }

    [Test]
    public void ChangingSequenceClearsBlockSelection()
    {
        ActionSequenceAsset first = CreateSequence("first");
        ActionSequenceAsset second = CreateSequence("second");
        var state = new SequenceMakerWorkspaceState();
        state.SetBattleScenario(CreateBattle("battle", first, second));
        state.SelectBlock("  block-a  ");

        bool changed = state.TrySelectSequence(second);

        Assert.That(changed, Is.True);
        Assert.That(state.SelectedSequence, Is.SameAs(second));
        Assert.That(state.SelectedBlockId, Is.Empty);
    }

    [Test]
    public void BlockSelectionIsNormalizedAndRaisesOneChange()
    {
        var state = new SequenceMakerWorkspaceState();
        state.SetStandaloneSequence(CreateSequence("standalone"));
        int changes = 0;
        state.Changed += () => changes++;

        state.SelectBlock("  block-a  ");
        state.SelectBlock("block-a");

        Assert.That(state.SelectedBlockId, Is.EqualTo("block-a"));
        Assert.That(changes, Is.EqualTo(1));
    }

    [Test]
    public void TriggerRuleSelectionClearsBlockAndReportsRuleSelectionKind()
    {
        BattleScenarioData battle = CreateBattle("battle", CreateSequence("opening"));
        battle.TriggerRules.Add(new ScenarioTriggerRuleData { RuleId = "phase.two" });
        var state = new SequenceMakerWorkspaceState();
        state.SetBattleScenario(battle);
        state.SelectBlock("intro.block");

        bool selected = state.SelectTriggerRule(" phase.two ");

        Assert.That(selected, Is.True);
        Assert.That(state.SelectedTriggerRuleId, Is.EqualTo("phase.two"));
        Assert.That(state.SelectedBlockId, Is.Empty);
        Assert.That(state.SelectionKind, Is.EqualTo(SequenceMakerSelectionKind.TriggerRule));
    }

    [Test]
    public void SelectingSequenceAfterRuleReturnsToSequenceSelection()
    {
        ActionSequenceAsset sequence = CreateSequence("opening");
        BattleScenarioData battle = CreateBattle("battle", sequence);
        battle.TriggerRules.Add(new ScenarioTriggerRuleData { RuleId = "phase.two" });
        var state = new SequenceMakerWorkspaceState();
        state.SetBattleScenario(battle);
        state.SelectTriggerRule("phase.two");

        bool selected = state.TrySelectSequence(sequence);

        Assert.That(selected, Is.True);
        Assert.That(state.SelectedTriggerRuleId, Is.Empty);
        Assert.That(state.SelectionKind, Is.EqualTo(SequenceMakerSelectionKind.Sequence));
    }

    [Test]
    public void LegacyRuleSelectionRejectsOutOfRangeIndex()
    {
        BattleScenarioData battle = CreateBattle("battle", CreateSequence("opening"));
        battle.Rules.Add(new BattleEventRuleData { RuleId = "legacy" });
        var state = new SequenceMakerWorkspaceState();
        state.SetBattleScenario(battle);

        Assert.That(state.SelectLegacyRule(1), Is.False);
        Assert.That(state.SelectLegacyRule(0), Is.True);
        Assert.That(state.SelectionKind, Is.EqualTo(SequenceMakerSelectionKind.LegacyRule));
    }

    [Test]
    public void DirtyStateCanReturnToCleanAfterSave()
    {
        var state = new SequenceMakerWorkspaceState();
        state.SetStandaloneSequence(CreateSequence("standalone"));

        state.SetDirty(true);
        Assert.That(state.IsDirty, Is.True);

        state.SetDirty(false);
        Assert.That(state.IsDirty, Is.False);
    }

    [Test]
    public void DrawerTabAndVisibilityAreIndependent()
    {
        var state = new SequenceMakerWorkspaceState();

        state.SetDrawer(SequenceMakerDrawerTab.Yaml, true);
        Assert.That(state.DrawerTab, Is.EqualTo(SequenceMakerDrawerTab.Yaml));
        Assert.That(state.IsDrawerOpen, Is.True);

        state.SetDrawerOpen(false);
        Assert.That(state.DrawerTab, Is.EqualTo(SequenceMakerDrawerTab.Yaml));
        Assert.That(state.IsDrawerOpen, Is.False);
    }

    [Test]
    public void LayoutSettingsRoundTripThroughStablePreferenceKeys()
    {
        var preferences = new MemoryPreferences();
        var source = new SequenceMakerWorkspaceState();
        source.SetLayout(310f, 430f, 245f);
        source.SetDrawer(SequenceMakerDrawerTab.Trace, true);
        source.SetDensity(SequenceMakerDensity.Compact);
        source.SavePreferences(preferences);

        var restored = new SequenceMakerWorkspaceState();
        restored.LoadPreferences(preferences);

        Assert.That(restored.NavigatorWidth, Is.EqualTo(310f));
        Assert.That(restored.InspectorWidth, Is.EqualTo(430f));
        Assert.That(restored.DrawerHeight, Is.EqualTo(245f));
        Assert.That(restored.DrawerTab, Is.EqualTo(SequenceMakerDrawerTab.Trace));
        Assert.That(restored.IsDrawerOpen, Is.True);
        Assert.That(restored.Density, Is.EqualTo(SequenceMakerDensity.Compact));
        Assert.That(preferences.WrittenKeys, Does.Contain(SequenceMakerWorkspaceState.NavigatorWidthKey));
        Assert.That(preferences.WrittenKeys, Does.Contain(SequenceMakerWorkspaceState.DrawerTabKey));
    }

    [Test]
    public void LoadedLayoutValuesAreClampedToUsableBounds()
    {
        var preferences = new MemoryPreferences();
        preferences.SetFloat(SequenceMakerWorkspaceState.NavigatorWidthKey, 2f);
        preferences.SetFloat(SequenceMakerWorkspaceState.InspectorWidthKey, 9000f);
        preferences.SetFloat(SequenceMakerWorkspaceState.DrawerHeightKey, -10f);
        var state = new SequenceMakerWorkspaceState();

        state.LoadPreferences(preferences);

        Assert.That(state.NavigatorWidth, Is.EqualTo(SequenceMakerWorkspaceState.MinNavigatorWidth));
        Assert.That(state.InspectorWidth, Is.EqualTo(SequenceMakerWorkspaceState.MaxInspectorWidth));
        Assert.That(state.DrawerHeight, Is.EqualTo(SequenceMakerWorkspaceState.MinDrawerHeight));
    }

    private ActionSequenceAsset CreateSequence(string id)
    {
        ActionSequenceAsset sequence = ScriptableObject.CreateInstance<ActionSequenceAsset>();
        sequence.SequenceId = id;
        _created.Add(sequence);
        return sequence;
    }

    private BattleScenarioData CreateBattle(string id, params ActionSequenceAsset[] sequences)
    {
        BattleScenarioData battle = ScriptableObject.CreateInstance<BattleScenarioData>();
        battle.ScenarioId = id;
        battle.Sequences.AddRange(sequences);
        _created.Add(battle);
        return battle;
    }

    private sealed class MemoryPreferences : ISequenceMakerPreferences
    {
        private readonly Dictionary<string, object> _values =
            new Dictionary<string, object>(StringComparer.Ordinal);

        public List<string> WrittenKeys { get; } = new List<string>();

        public float GetFloat(string key, float defaultValue)
        {
            return _values.TryGetValue(key, out object value) && value is float typed
                ? typed
                : defaultValue;
        }

        public bool GetBool(string key, bool defaultValue)
        {
            return _values.TryGetValue(key, out object value) && value is bool typed
                ? typed
                : defaultValue;
        }

        public string GetString(string key, string defaultValue)
        {
            return _values.TryGetValue(key, out object value) && value is string typed
                ? typed
                : defaultValue;
        }

        public void SetFloat(string key, float value)
        {
            _values[key] = value;
            WrittenKeys.Add(key);
        }

        public void SetBool(string key, bool value)
        {
            _values[key] = value;
            WrittenKeys.Add(key);
        }

        public void SetString(string key, string value)
        {
            _values[key] = value;
            WrittenKeys.Add(key);
        }
    }
}
