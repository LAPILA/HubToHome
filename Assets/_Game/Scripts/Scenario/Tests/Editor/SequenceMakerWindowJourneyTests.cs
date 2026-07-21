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
            string relativePath = _createdFiles[i].Replace('\\', '/');
            if (relativePath.StartsWith("Assets/", StringComparison.Ordinal))
                UnityEditor.AssetDatabase.DeleteAsset(relativePath);

            string fullPath = Path.GetFullPath(relativePath);
            if (File.Exists(fullPath))
                File.Delete(fullPath);
            if (File.Exists(fullPath + ".meta"))
                File.Delete(fullPath + ".meta");
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
    public void SequenceInspectorShowsEnabledDangerCommandWhenDeletionIsSafe()
    {
        ActionSequenceAsset sequence = CreateSequence("qa.delete.safe", "삭제 가능");
        sequence.Source.SourcePath = "Assets/qa.delete.safe.sequence.yaml";

        _window.SetTargetForTests(sequence);

        Button delete = _window.rootVisualElement.Q<Button>("delete-sequence-button");
        Assert.That(delete, Is.Not.Null);
        Assert.That(delete.text, Is.EqualTo("시퀀스 완전 삭제"));
        Assert.That(delete.enabledSelf, Is.True);
        Assert.That(_window.rootVisualElement.Q<Label>(className: "sm-sequence-danger-title")?.text,
            Is.EqualTo("위험 작업"));
    }

    [Test]
    public void SequenceInspectorDisablesDeletionAndShowsReferenceCount()
    {
        ActionSequenceAsset target = CreateSequence("qa.delete.target", "삭제 대상");
        target.Source.SourcePath = "Assets/qa.delete.target.sequence.yaml";
        ActionSequenceAsset caller = CreateSequence("qa.delete.caller", "호출자");
        caller.Actions.Add(new ScenarioActionData
        {
            BlockId = "call-target",
            ActionId = SequenceCallActionAdapter.Id,
            ParametersJson = "{\"sequence\":\"qa.delete.target\"}"
        });
        SequenceUsageIndex usage = SequenceUsageIndex.Build(SequenceAssetIndex.Build(
            Array.Empty<BattleScenarioData>(),
            new[] { target, caller }));

        _window.SetTargetForTests(target);
        _window.SetUsageIndexForTests(usage);

        Button delete = _window.rootVisualElement.Q<Button>("delete-sequence-button");
        Assert.That(delete.enabledSelf, Is.False);
        Assert.That(_window.rootVisualElement.Q<Label>(className: "sm-sequence-danger-blocked")?.text,
            Is.EqualTo("1개 문제 때문에 삭제할 수 없음"));
    }

    [Test]
    public void CancelledDeletionKeepsCurrentSequenceAndDoesNotCallService()
    {
        ActionSequenceAsset sequence = CreateSequence("qa.delete.cancel", "취소 대상");
        sequence.Source.SourcePath = "Assets/qa.delete.cancel.sequence.yaml";
        var service = new FakeDeletionService { ResultStatus = SequenceDeletionStatus.Succeeded };
        _window.SetTargetForTests(sequence);
        _window.SetDeletionServiceForTests(service);
        _window.SetDeletionConfirmationForTests((_, __) => false);

        _window.DeleteSelectedSequenceForTests();

        Assert.That(service.CallCount, Is.EqualTo(0));
        Assert.That(_window.WorkspaceForTests.SelectedSequence, Is.SameAs(sequence));
        Assert.That(_window.StatusForTests, Is.EqualTo("시퀀스 삭제 취소"));
    }

    [Test]
    public void FailedDeletionKeepsCurrentSequenceAndShowsError()
    {
        ActionSequenceAsset sequence = CreateSequence("qa.delete.failure", "실패 대상");
        sequence.Source.SourcePath = "Assets/qa.delete.failure.sequence.yaml";
        var service = new FakeDeletionService
        {
            ResultStatus = SequenceDeletionStatus.RuntimeAssetDeleteFailed,
            Error = "asset failed"
        };
        _window.SetTargetForTests(sequence);
        _window.SetDeletionServiceForTests(service);
        _window.SetDeletionConfirmationForTests((_, __) => true);

        _window.DeleteSelectedSequenceForTests();

        Assert.That(service.CallCount, Is.EqualTo(1));
        Assert.That(_window.WorkspaceForTests.SelectedSequence, Is.SameAs(sequence));
        Assert.That(_window.StatusHasErrorForTests, Is.True);
        Assert.That(_window.StatusForTests, Does.Contain("asset failed"));
    }

    [Test]
    public void SuccessfulStandaloneDeletionClearsWorkspace()
    {
        ActionSequenceAsset sequence = CreateSequence("qa.delete.success", "성공 대상");
        sequence.Source.SourcePath = "Assets/qa.delete.success.sequence.yaml";
        var service = new FakeDeletionService { ResultStatus = SequenceDeletionStatus.Succeeded };
        _window.SetTargetForTests(sequence);
        _window.SetDeletionServiceForTests(service);
        _window.SetDeletionConfirmationForTests((_, __) => true);

        _window.DeleteSelectedSequenceForTests();

        Assert.That(service.CallCount, Is.EqualTo(1));
        Assert.That(_window.WorkspaceForTests.HasTarget, Is.False);
        Assert.That(_window.StatusForTests, Is.EqualTo("시퀀스 삭제 완료: qa.delete.success"));
    }

    [Test]
    public void BattlePartialDeletionRefreshesSelectionAfterYamlWasCommitted()
    {
        ActionSequenceAsset target = CreateSequence("qa.delete.partial", "부분 삭제 대상");
        ActionSequenceAsset remaining = CreateSequence("qa.delete.remaining", "남은 시퀀스");
        BattleScenarioData battle = CreateBattle("qa.delete.battle", target, remaining);
        battle.Source.SourcePath = "Assets/qa.delete.battle.scenario.yaml";
        var service = new FakeDeletionService
        {
            ResultStatus = SequenceDeletionStatus.RuntimeAssetDeleteFailed,
            Error = "sub-asset failed",
            SourceCommitted = true,
            RemoveFromBattle = true
        };
        _window.SetTargetForTests(battle);
        _window.SetDeletionServiceForTests(service);
        _window.SetDeletionConfirmationForTests((_, __) => true);

        _window.DeleteSelectedSequenceForTests();

        Assert.That(_window.WorkspaceForTests.BattleScenario, Is.SameAs(battle));
        Assert.That(_window.WorkspaceForTests.SelectedSequence, Is.SameAs(remaining));
        Assert.That(_window.StatusHasErrorForTests, Is.True);
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
    public void TriggerRuleEditorShowsDirectDeleteCommand()
    {
        ActionSequenceAsset sequence = CreateSequence("qa.rule.trigger.sequence", "규칙 대상");
        BattleScenarioData battle = CreateBattle("qa.rule.trigger.battle", sequence);
        battle.TriggerRules.Add(new ScenarioTriggerRuleData
        {
            RuleId = "qa.rule.trigger",
            DisplayNameKo = "직접 삭제할 이벤트 규칙",
            SequenceId = sequence.SequenceId
        });
        _window.SetTargetForTests(battle);

        _window.SelectTriggerRuleForTests("qa.rule.trigger");

        Button delete = _window.rootVisualElement.Q<Button>("rule-delete-button");
        Assert.That(delete, Is.Not.Null);
        Assert.That(delete.text, Is.EqualTo("규칙 삭제"));
        Assert.That(delete.enabledSelf, Is.True);
    }

    [Test]
    public void LegacyRuleEditorShowsDirectDeleteCommand()
    {
        ActionSequenceAsset sequence = CreateSequence("qa.rule.legacy.sequence", "기존 규칙 대상");
        BattleScenarioData battle = CreateBattle("qa.rule.legacy.battle", sequence);
        battle.Rules.Add(new BattleEventRuleData
        {
            RuleId = "qa.rule.legacy",
            SequenceId = sequence.SequenceId
        });
        _window.SetTargetForTests(battle);

        _window.SelectLegacyRuleForTests(0);

        Button delete = _window.rootVisualElement.Q<Button>("rule-delete-button");
        Assert.That(delete, Is.Not.Null);
        Assert.That(delete.text, Is.EqualTo("규칙 삭제"));
        Assert.That(delete.enabledSelf, Is.True);
    }

    [Test]
    public void CancelledRuleDeletionKeepsRuleAndSelection()
    {
        ActionSequenceAsset sequence = CreateSequence("qa.rule.cancel.sequence", "취소 대상");
        BattleScenarioData battle = CreateBattle("qa.rule.cancel.battle", sequence);
        battle.TriggerRules.Add(new ScenarioTriggerRuleData
        {
            RuleId = "qa.rule.cancel",
            DisplayNameKo = "삭제 취소 규칙",
            SequenceId = sequence.SequenceId
        });
        _window.SetTargetForTests(battle);
        _window.SelectTriggerRuleForTests("qa.rule.cancel");
        _window.SetRuleDeletionConfirmationForTests((_, __) => false);

        _window.DeleteTriggerRuleForTests("qa.rule.cancel");

        Assert.That(battle.TriggerRules, Has.Count.EqualTo(1));
        Assert.That(_window.WorkspaceForTests.SelectedTriggerRuleId, Is.EqualTo("qa.rule.cancel"));
        Assert.That(_window.WorkspaceForTests.IsDirty, Is.False);
    }

    [Test]
    public void TriggerRuleDeletionReturnsToSequenceAndRemovesDeleteBlocker()
    {
        ActionSequenceAsset sequence = CreateSequence("qa.rule.delete.trigger.sequence", "이벤트 규칙 대상");
        sequence.Source.SourcePath = "Assets/qa.rule.delete.trigger.sequence.yaml";
        BattleScenarioData battle = CreateBattle("qa.rule.delete.trigger.battle", sequence);
        battle.Source.SourcePath = "Assets/qa.rule.delete.trigger.battle.scenario.yaml";
        battle.TriggerRules.Add(new ScenarioTriggerRuleData
        {
            RuleId = "qa.rule.delete.trigger",
            DisplayNameKo = "삭제할 이벤트 규칙",
            SequenceId = sequence.SequenceId
        });
        _window.SetTargetForTests(battle);
        _window.SetUsageIndexForTests(SequenceUsageIndex.Build(SequenceAssetIndex.Build(
            new[] { battle },
            new[] { sequence })));
        _window.SelectTriggerRuleForTests("qa.rule.delete.trigger");
        _window.SetRuleDeletionConfirmationForTests((_, __) => true);

        _window.DeleteTriggerRuleForTests("qa.rule.delete.trigger");

        Assert.That(battle.TriggerRules, Is.Empty);
        Assert.That(_window.WorkspaceForTests.SelectedSequence, Is.SameAs(sequence));
        Assert.That(_window.WorkspaceForTests.SelectionKind, Is.EqualTo(SequenceMakerSelectionKind.Sequence));
        Assert.That(_window.WorkspaceForTests.IsDirty, Is.True);
        Assert.That(_window.rootVisualElement.Q<Button>("delete-sequence-button").enabledSelf, Is.True);
        Assert.That(_window.GetBattleHistoryForTests().Undo(), Is.True);
        Assert.That(battle.TriggerRules, Has.Count.EqualTo(1));
    }

    [Test]
    public void LegacyRuleDeletionReturnsToSequenceAndCanUndo()
    {
        ActionSequenceAsset sequence = CreateSequence("qa.rule.delete.legacy.sequence", "기존 규칙 대상");
        sequence.Source.SourcePath = "Assets/qa.rule.delete.legacy.sequence.yaml";
        BattleScenarioData battle = CreateBattle("qa.rule.delete.legacy.battle", sequence);
        battle.Source.SourcePath = "Assets/qa.rule.delete.legacy.battle.scenario.yaml";
        battle.Rules.Add(new BattleEventRuleData
        {
            RuleId = "qa.rule.delete.legacy",
            SequenceId = sequence.SequenceId
        });
        _window.SetTargetForTests(battle);
        _window.SetUsageIndexForTests(SequenceUsageIndex.Build(SequenceAssetIndex.Build(
            new[] { battle },
            new[] { sequence })));
        _window.SelectLegacyRuleForTests(0);
        _window.SetRuleDeletionConfirmationForTests((_, __) => true);

        _window.DeleteLegacyRuleForTests(0);

        Assert.That(battle.Rules, Is.Empty);
        Assert.That(_window.WorkspaceForTests.SelectedSequence, Is.SameAs(sequence));
        Assert.That(_window.WorkspaceForTests.IsDirty, Is.True);
        Assert.That(_window.rootVisualElement.Q<Button>("delete-sequence-button").enabledSelf, Is.True);
        Assert.That(_window.GetBattleHistoryForTests().Undo(), Is.True);
        Assert.That(battle.Rules, Has.Count.EqualTo(1));
        Assert.That(battle.Rules[0].RuleId, Is.EqualTo("qa.rule.delete.legacy"));
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
            "Assets",
            "_Game",
            "Content",
            "Scenarios",
            "__SequenceMakerQA_" + Guid.NewGuid().ToString("N") + ".sequence.yaml")
            .Replace('\\', '/');
        _createdFiles.Add(path);
        return path;
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

    private sealed class FakeDeletionService : ISequenceDeletionService
    {
        public int CallCount { get; private set; }
        public SequenceDeletionStatus ResultStatus { get; set; }
        public string Error { get; set; } = string.Empty;
        public bool SourceCommitted { get; set; }
        public bool RemoveFromBattle { get; set; }

        public SequenceDeletionResult Delete(
            ActionSequenceAsset sequence,
            BattleScenarioData owningBattle,
            SequenceUsageIndex usage,
            ActionCatalogAsset catalog = null)
        {
            CallCount++;
            if (RemoveFromBattle && owningBattle != null)
            {
                owningBattle.Sequences.Remove(sequence);
            }
            return new SequenceDeletionResult
            {
                Status = ResultStatus,
                ErrorMessage = Error,
                SourceCommitted = SourceCommitted,
                Analysis = SequenceDeletionCoordinator.Analyze(sequence, owningBattle, usage)
            };
        }
    }
}
