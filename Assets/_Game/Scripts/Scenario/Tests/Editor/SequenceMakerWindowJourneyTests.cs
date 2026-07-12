using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

public sealed class SequenceMakerWindowJourneyTests
{
    private readonly List<Object> _created = new List<Object>();
    private readonly List<string> _createdFiles = new List<string>();
    private readonly List<string> _createdDirectories = new List<string>();
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
        for (int i = 0; i < _createdFiles.Count; i++)
        {
            if (File.Exists(_createdFiles[i]))
            {
                File.Delete(_createdFiles[i]);
            }
        }
        _createdFiles.Clear();
        for (int i = 0; i < _createdDirectories.Count; i++)
        {
            if (Directory.Exists(_createdDirectories[i]))
            {
                Directory.Delete(_createdDirectories[i], true);
            }
        }
        _createdDirectories.Clear();
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
    public void UndoRedoButtonsUseDirectionalArrowIcons()
    {
        Button undo = _window.rootVisualElement.Q<Button>("undo-button");
        Button redo = _window.rootVisualElement.Q<Button>("redo-button");

        Assert.That(HasIconOrFallback(undo, "←"), Is.True);
        Assert.That(HasIconOrFallback(redo, "→"), Is.True);
        Assert.That(undo.tooltip, Is.EqualTo("실행 취소"));
        Assert.That(redo.tooltip, Is.EqualTo("다시 실행"));
    }

    [TestCase("validate-button", "검증")]
    [TestCase("save-button", "저장")]
    [TestCase("library-button", "액션")]
    public void LabeledCommandButtonsRenderAsClearTextCommands(
        string buttonName,
        string expectedLabel)
    {
        Button button = _window.rootVisualElement.Q<Button>(buttonName);

        Assert.That(button, Is.Not.Null);
        Assert.That(button.text, Is.EqualTo(expectedLabel));
        Assert.That(button.Q<Image>(className: "sm-button-icon"), Is.Null);
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

    [Test]
    public void SwitchingToCleanTargetDoesNotInheritDirtyHistoryFromPreviousTarget()
    {
        ActionSequenceAsset first = CreateSequence("qa.first", "첫 번째");
        ActionSequenceAsset second = CreateSequence("qa.second", "두 번째");
        _window.SetTargetForTests(first);
        SequenceEditCommandStack firstHistory = _window.GetSequenceHistoryForTests();
        firstHistory.Execute(SequenceEditCommands.SetSequenceDisplayName("수정된 첫 번째"));
        _window.RefreshForTests();
        Assert.That(_window.WorkspaceForTests.IsDirty, Is.True);

        _window.SetTargetForTests(second);

        Assert.That(_window.WorkspaceForTests.ActiveTarget, Is.SameAs(second));
        Assert.That(_window.WorkspaceForTests.IsDirty, Is.False,
            "활성 대상의 dirty 상태는 이전에 열었던 다른 target history에서 상속되면 안 됩니다.");
    }

    [Test]
    public void SavingCurrentTargetDoesNotMarkOtherTargetHistoryAsSaved()
    {
        ActionSequenceAsset first = CreateSequence("qa.save.first", "저장 대상 A");
        ActionSequenceAsset second = CreateSequence("qa.save.second", "저장 대상 B");
        second.Source.SourcePath = CreateTemporarySourcePath();

        _window.SetTargetForTests(first);
        _window.GetSequenceHistoryForTests().Execute(
            SequenceEditCommands.SetSequenceDisplayName("수정된 저장 대상 A"));
        _window.SetTargetForTests(second);
        _window.GetSequenceHistoryForTests().Execute(
            SequenceEditCommands.SetSequenceDisplayName("수정된 저장 대상 B"));
        _window.RefreshForTests();

        Assert.That(_window.SaveCurrentForTests(), Is.True, _window.StatusForTests);
        Assert.That(_window.WorkspaceForTests.IsDirty, Is.False);

        _window.SetTargetForTests(first);

        Assert.That(_window.WorkspaceForTests.IsDirty, Is.True,
            "현재 target 저장이 다른 target의 미저장 history까지 저장 완료로 바꾸면 안 됩니다.");
    }

    [Test]
    public void FailedSaveKeepsCurrentTargetDirty()
    {
        ActionSequenceAsset sequence = CreateSequence("qa.save.failure", "저장 실패");
        _window.SetTargetForTests(sequence);
        _window.GetSequenceHistoryForTests().Execute(
            SequenceEditCommands.SetSequenceDisplayName("저장되지 않은 변경"));
        _window.RefreshForTests();

        Assert.That(_window.SaveCurrentForTests(), Is.False);
        Assert.That(_window.WorkspaceForTests.IsDirty, Is.True);
        Assert.That(_window.StatusHasErrorForTests, Is.True);
    }

    [Test]
    public void ExternalYamlModificationBlocksSaveAndKeepsDirty()
    {
        ActionSequenceAsset sequence = CreateSequence("qa.save.conflict", "충돌 테스트");
        sequence.Source.SourcePath = CreateTemporarySourcePath();
        _window.SetTargetForTests(sequence);
        Assert.That(_window.SaveCurrentForTests(), Is.True, _window.StatusForTests);
        File.AppendAllText(Path.GetFullPath(sequence.Source.SourcePath), "\n# external change");

        _window.GetSequenceHistoryForTests().Execute(
            SequenceEditCommands.SetSequenceDisplayName("로컬 변경"));
        _window.RefreshForTests();

        Assert.That(_window.SaveCurrentForTests(), Is.False);
        Assert.That(_window.StatusForTests, Is.EqualTo("YAML 외부 변경 충돌"));
        Assert.That(_window.WorkspaceForTests.IsDirty, Is.True);
    }

    [Test]
    public void ExplicitOverwriteAfterConflictSavesCurrentTarget()
    {
        ActionSequenceAsset sequence = CreateSequence("qa.save.overwrite", "덮어쓰기 테스트");
        sequence.Source.SourcePath = CreateTemporarySourcePath();
        _window.SetTargetForTests(sequence);
        Assert.That(_window.SaveCurrentForTests(), Is.True, _window.StatusForTests);
        File.AppendAllText(Path.GetFullPath(sequence.Source.SourcePath), "\n# external change");
        _window.GetSequenceHistoryForTests().Execute(
            SequenceEditCommands.SetSequenceDisplayName("덮어쓸 로컬 변경"));
        _window.RefreshForTests();
        Assert.That(_window.SaveCurrentForTests(), Is.False);

        Assert.That(_window.SaveCurrentForTests(true), Is.True, _window.StatusForTests);
        Assert.That(_window.WorkspaceForTests.IsDirty, Is.False);
        Assert.That(
            File.ReadAllText(Path.GetFullPath(sequence.Source.SourcePath)),
            Does.Contain("덮어쓸 로컬 변경"));
    }

    [Test]
    public void RecreatingVisualTreeKeepsDirtyTargetSession()
    {
        ActionSequenceAsset sequence = CreateSequence("qa.recreate", "재생성 테스트");
        _window.SetTargetForTests(sequence);
        _window.GetSequenceHistoryForTests().Execute(
            SequenceEditCommands.SetSequenceDisplayName("재생성 뒤에도 미저장"));
        _window.RefreshForTests();

        _window.CreateGUI();

        Assert.That(_window.WorkspaceForTests.ActiveTarget, Is.SameAs(sequence));
        Assert.That(_window.WorkspaceForTests.IsDirty, Is.True);
        VisualElement flowPanel =
            _window.rootVisualElement.Q<VisualElement>(className: "sm-panel--flow");
        Assert.That(flowPanel.Q<Label>(className: "sm-panel-title")?.text,
            Is.EqualTo("재생성 뒤에도 미저장"));
    }

    [Test]
    public void FailedSaveKeepsRecoverySnapshot()
    {
        string recoveryRoot = CreateTemporaryDirectory();
        ActionSequenceAsset sequence = CreateSequence("qa.recovery.failed", "실패 복구");
        _window.SetRecoveryStoreForTests(new SequenceRecoveryStore(recoveryRoot));
        _window.SetTargetForTests(sequence);
        _window.GetSequenceHistoryForTests().Execute(
            SequenceEditCommands.SetSequenceDisplayName("복구할 변경"));
        _window.CaptureRecoveryForTests();
        Assert.That(_window.RecoveryCountForTests, Is.EqualTo(1));

        Assert.That(_window.SaveCurrentForTests(), Is.False);

        Assert.That(_window.RecoveryCountForTests, Is.EqualTo(1));
        Assert.That(_window.WorkspaceForTests.IsDirty, Is.True);
    }

    [Test]
    public void SuccessfulSaveClearsCurrentRecoverySnapshots()
    {
        string recoveryRoot = CreateTemporaryDirectory();
        ActionSequenceAsset sequence = CreateSequence("qa.recovery.saved", "성공 복구");
        sequence.Source.SourcePath = CreateTemporarySourcePath();
        _window.SetRecoveryStoreForTests(new SequenceRecoveryStore(recoveryRoot));
        _window.SetTargetForTests(sequence);
        _window.GetSequenceHistoryForTests().Execute(
            SequenceEditCommands.SetSequenceDisplayName("저장할 변경"));
        _window.CaptureRecoveryForTests();
        Assert.That(_window.RecoveryCountForTests, Is.EqualTo(1));

        Assert.That(_window.SaveCurrentForTests(), Is.True, _window.StatusForTests);

        Assert.That(_window.RecoveryCountForTests, Is.EqualTo(0));
        Assert.That(_window.WorkspaceForTests.IsDirty, Is.False);
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

    private string CreateTemporarySourcePath()
    {
        string path = Path.Combine(
            "Library",
            "HubToHome",
            "SequenceMakerQA",
            Guid.NewGuid().ToString("N") + ".sequence.yaml");
        _createdFiles.Add(Path.GetFullPath(path));
        return path.Replace('\\', '/');
    }

    private string CreateTemporaryDirectory()
    {
        string path = Path.GetFullPath(Path.Combine(
            "Library",
            "HubToHome",
            "SequenceMakerQA",
            Guid.NewGuid().ToString("N")));
        _createdDirectories.Add(path);
        return path;
    }

    private static bool HasIconOrFallback(Button button, string fallback)
    {
        return button != null
            && (button.Q<Image>(className: "sm-button-icon") != null
                || string.Equals(button.text, fallback, StringComparison.Ordinal));
    }
}
