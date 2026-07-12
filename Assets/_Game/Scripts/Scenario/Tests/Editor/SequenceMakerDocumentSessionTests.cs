using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

public sealed class SequenceMakerDocumentSessionTests
{
    private readonly List<Object> _created = new List<Object>();

    [TearDown]
    public void TearDown()
    {
        for (int i = 0; i < _created.Count; i++)
        {
            Object.DestroyImmediate(_created[i]);
        }
        _created.Clear();
    }

    [Test]
    public void StandaloneDirtyStateBelongsOnlyToActiveTarget()
    {
        SequenceMakerDocumentSession session = new SequenceMakerDocumentSession();
        SequenceMakerWorkspaceState workspace = new SequenceMakerWorkspaceState();
        ActionSequenceAsset first = CreateSequence("session.first");
        ActionSequenceAsset second = CreateSequence("session.second");
        session.GetSequenceHistory(first).Execute(
            SequenceEditCommands.SetSequenceDisplayName("수정됨"));

        workspace.SetStandaloneSequence(first);
        Assert.That(session.IsDirty(workspace), Is.True);

        workspace.SetStandaloneSequence(second);
        Assert.That(session.IsDirty(workspace), Is.False);
    }

    [Test]
    public void SavingStandaloneDoesNotMarkUnrelatedHistorySaved()
    {
        SequenceMakerDocumentSession session = new SequenceMakerDocumentSession();
        SequenceMakerWorkspaceState workspace = new SequenceMakerWorkspaceState();
        ActionSequenceAsset first = CreateSequence("session.save.first");
        ActionSequenceAsset second = CreateSequence("session.save.second");
        session.GetSequenceHistory(first).Execute(
            SequenceEditCommands.SetSequenceDisplayName("A 수정"));
        session.GetSequenceHistory(second).Execute(
            SequenceEditCommands.SetSequenceDisplayName("B 수정"));

        workspace.SetStandaloneSequence(second);
        session.MarkSaved(workspace);
        Assert.That(session.IsDirty(workspace), Is.False);

        workspace.SetStandaloneSequence(first);
        Assert.That(session.IsDirty(workspace), Is.True);
    }

    [Test]
    public void BattleOwnsItsRuleAndContainedSequenceHistories()
    {
        SequenceMakerDocumentSession session = new SequenceMakerDocumentSession();
        SequenceMakerWorkspaceState workspace = new SequenceMakerWorkspaceState();
        ActionSequenceAsset sequence = CreateSequence("session.battle.sequence");
        BattleScenarioData battle = CreateBattle("session.battle", sequence);
        workspace.SetBattleScenario(battle);

        session.GetSequenceHistory(sequence).Execute(
            SequenceEditCommands.SetSequenceDisplayName("전투 시퀀스 수정"));
        Assert.That(session.IsDirty(workspace), Is.True);

        session.MarkSaved(workspace);
        Assert.That(session.IsDirty(workspace), Is.False);

        session.GetBattleHistory(battle).Execute(
            BattleScenarioEditCommands.AddTriggerRule(ScenarioTriggerRuleFactory.Create()));
        Assert.That(session.IsDirty(workspace), Is.True);
    }

    [Test]
    public void ExternalDirtyStateSurvivesTargetSwitch()
    {
        SequenceMakerDocumentSession session = new SequenceMakerDocumentSession();
        SequenceMakerWorkspaceState workspace = new SequenceMakerWorkspaceState();
        ActionSequenceAsset first = CreateSequence("session.external.first");
        ActionSequenceAsset second = CreateSequence("session.external.second");

        workspace.SetStandaloneSequence(first);
        session.SetExternalChanges(first, true);
        workspace.SetStandaloneSequence(second);
        Assert.That(session.IsDirty(workspace), Is.False);

        workspace.SetStandaloneSequence(first);
        Assert.That(session.IsDirty(workspace), Is.True);
    }

    [Test]
    public void ResetDropsOnlyCurrentTargetState()
    {
        SequenceMakerDocumentSession session = new SequenceMakerDocumentSession();
        SequenceMakerWorkspaceState workspace = new SequenceMakerWorkspaceState();
        ActionSequenceAsset first = CreateSequence("session.reset.first");
        ActionSequenceAsset second = CreateSequence("session.reset.second");
        session.GetSequenceHistory(first).Execute(
            SequenceEditCommands.SetSequenceDisplayName("A 수정"));
        session.GetSequenceHistory(second).Execute(
            SequenceEditCommands.SetSequenceDisplayName("B 수정"));

        workspace.SetStandaloneSequence(first);
        session.SetExternalChanges(first, true);
        session.Reset(workspace);
        Assert.That(session.IsDirty(workspace), Is.False);

        workspace.SetStandaloneSequence(second);
        Assert.That(session.IsDirty(workspace), Is.True);
    }

    [TestCase(0, SequenceMakerLeaveIntent.SaveAndLeave)]
    [TestCase(1, SequenceMakerLeaveIntent.Cancel)]
    [TestCase(2, SequenceMakerLeaveIntent.KeepLocalChangesAndLeave)]
    public void LeaveDialogChoicesHaveExplicitIntent(
        int choice,
        SequenceMakerLeaveIntent expected)
    {
        Assert.That(SequenceMakerLeavePrompt.FromDialogChoice(choice), Is.EqualTo(expected));
        Assert.That(SequenceMakerLeavePrompt.SaveLabel, Is.EqualTo("YAML 저장 후 이동"));
        Assert.That(SequenceMakerLeavePrompt.KeepLocalLabel, Is.EqualTo("저장하지 않고 이동"));
        Assert.That(SequenceMakerLeavePrompt.Title, Does.Contain("YAML"));
        Assert.That(SequenceMakerLeavePrompt.Message, Does.Contain("Runtime Asset"));
        Assert.That(SequenceMakerLeavePrompt.Message, Does.Contain("복구 기록"));
    }

    private ActionSequenceAsset CreateSequence(string id)
    {
        ActionSequenceAsset sequence = ScriptableObject.CreateInstance<ActionSequenceAsset>();
        sequence.SequenceId = id;
        sequence.DisplayNameKo = id;
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
        battle.Sequences.AddRange(sequences);
        _created.Add(battle);
        return battle;
    }
}
