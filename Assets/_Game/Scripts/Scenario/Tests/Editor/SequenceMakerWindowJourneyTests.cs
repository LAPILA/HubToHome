using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;

public sealed class SequenceMakerWindowJourneyTests
{
    private readonly List<Object> _created = new List<Object>();
    private SequenceMakerWindow _window;
    private Object _previousSelection;

    [SetUp]
    public void SetUp()
    {
        _previousSelection = UnityEditor.Selection.activeObject;
        UnityEditor.Selection.activeObject = null;
        _window = ScriptableObject.CreateInstance<SequenceMakerWindow>();
        _window.CreateGUI();
    }

    [TearDown]
    public void TearDown()
    {
        if (_window != null)
        {
            Object.DestroyImmediate(_window);
        }

        for (int i = 0; i < _created.Count; i++)
        {
            Object.DestroyImmediate(_created[i]);
        }
        _created.Clear();
        UnityEditor.Selection.activeObject = _previousSelection;
    }

    [Test]
    public void NoTargetRendersExplicitEmptyWorkspace()
    {
        Assert.That(_window.WorkspaceForTests.HasTarget, Is.False);
        Assert.That(_window.rootVisualElement.Q<Label>("breadcrumb-label").text, Is.EqualTo("편집 대상을 선택"));
        Assert.That(_window.rootVisualElement.Q<Button>("save-button").enabledSelf, Is.False);
        Assert.That(_window.rootVisualElement.Q<Label>(className: "sm-flow-empty")?.text, Is.EqualTo("시퀀스 없음"));
        Assert.That(_window.rootVisualElement.Q<Label>(className: "sm-empty-title")?.text, Is.EqualTo("속성 없음"));
    }

    [Test]
    public void StandaloneTargetRendersSequenceFlowAndInspector()
    {
        ActionSequenceAsset sequence = CreateSequence("qa.standalone", "QA 독립 시퀀스");

        _window.SetTargetForTests(sequence);

        Assert.That(_window.WorkspaceForTests.StandaloneSequence, Is.SameAs(sequence));
        Assert.That(_window.WorkspaceForTests.SelectedSequence, Is.SameAs(sequence));
        VisualElement flowPanel = _window.rootVisualElement.Q<VisualElement>(className: "sm-panel--flow");
        Assert.That(flowPanel.Q<Label>(className: "sm-panel-title")?.text, Is.EqualTo("QA 독립 시퀀스"));
        SequenceInspectorView inspector = _window.rootVisualElement.Q<SequenceInspectorView>();
        Assert.That(inspector, Is.Not.Null);
        Assert.That(inspector.Q<Label>(className: "sm-inspector-action-title")?.text, Is.EqualTo("시퀀스 설정"));
        Assert.That(_window.rootVisualElement.Q<Button>("validate-button").enabledSelf, Is.True);
    }

    [Test]
    public void BattleTargetSelectsFirstSequenceAndShowsRuleNavigator()
    {
        ActionSequenceAsset sequence = CreateSequence("qa.opening", "오프닝");
        BattleScenarioData battle = CreateBattle("qa.battle", sequence);
        battle.TriggerRules.Add(new ScenarioTriggerRuleData
        {
            RuleId = "qa.phase.two",
            DisplayNameKo = "2페이즈 진입"
        });

        _window.SetTargetForTests(battle);

        Assert.That(_window.WorkspaceForTests.BattleScenario, Is.SameAs(battle));
        Assert.That(_window.WorkspaceForTests.SelectedSequence, Is.SameAs(sequence));
        Assert.That(_window.rootVisualElement.Q<VisualElement>(className: "sm-rule-list"), Is.Not.Null);
        Assert.That(_window.rootVisualElement.Query<Label>(className: "sm-rule-row-title").ToList()
            .Exists(label => label.text == "2페이즈 진입"), Is.True);
    }

    [Test]
    public void TriggerRuleSelectionSwitchesCenterAndInspectorContext()
    {
        ActionSequenceAsset sequence = CreateSequence("qa.opening", "오프닝");
        BattleScenarioData battle = CreateBattle("qa.battle", sequence);
        battle.TriggerRules.Add(new ScenarioTriggerRuleData
        {
            RuleId = "qa.phase.two",
            DisplayNameKo = "2페이즈 진입"
        });
        _window.SetTargetForTests(battle);

        _window.SelectTriggerRuleForTests("qa.phase.two");

        Assert.That(_window.WorkspaceForTests.SelectionKind, Is.EqualTo(SequenceMakerSelectionKind.TriggerRule));
        VisualElement flowPanel = _window.rootVisualElement.Q<VisualElement>(className: "sm-panel--flow");
        Assert.That(flowPanel.Q<Label>(className: "sm-panel-title")?.text, Is.EqualTo("2페이즈 진입"));
        Assert.That(_window.rootVisualElement.Q<TriggerRuleEditorView>(), Is.Not.Null);
    }

    private ActionSequenceAsset CreateSequence(string id, string displayName)
    {
        ActionSequenceAsset sequence = ScriptableObject.CreateInstance<ActionSequenceAsset>();
        sequence.SequenceId = id;
        sequence.DisplayNameKo = displayName;
        sequence.Actions.Add(new ScenarioActionData
        {
            BlockId = ScenarioBlockIdentity.Create(),
            ActionId = FlowWaitActionAdapter.Id,
            ParametersJson = "{\"duration\":0.1}"
        });
        _created.Add(sequence);
        return sequence;
    }

    private BattleScenarioData CreateBattle(string id, params ActionSequenceAsset[] sequences)
    {
        BattleScenarioData battle = ScriptableObject.CreateInstance<BattleScenarioData>();
        battle.ScenarioId = id;
        battle.TitleKo = "QA 전투";
        battle.Sequences.AddRange(sequences);
        _created.Add(battle);
        return battle;
    }
}
