using System;
using System.Collections.Generic;
using UnityEngine;

public enum SequenceMakerLeaveIntent
{
    SaveAndLeave,
    Cancel,
    KeepLocalChangesAndLeave
}

public static class SequenceMakerLeavePrompt
{
    public const string Title = "YAML에 저장되지 않은 변경";
    public const string Message =
        "현재 변경은 Runtime Asset에 적용되어 있지만 YAML에는 아직 저장되지 않았습니다.\n\n"
        + "다른 편집 대상으로 이동해도 변경은 로컬 복구 기록에 남습니다.";
    public const string SaveLabel = "YAML 저장 후 이동";
    public const string CancelLabel = "취소";
    public const string KeepLocalLabel = "저장하지 않고 이동";

    public static SequenceMakerLeaveIntent FromDialogChoice(int choice)
    {
        switch (choice)
        {
            case 0:
                return SequenceMakerLeaveIntent.SaveAndLeave;
            case 2:
                return SequenceMakerLeaveIntent.KeepLocalChangesAndLeave;
            default:
                return SequenceMakerLeaveIntent.Cancel;
        }
    }
}

public sealed class SequenceMakerDocumentSession
{
    private readonly Dictionary<int, SequenceEditCommandStack> _sequenceHistories =
        new Dictionary<int, SequenceEditCommandStack>();
    private readonly Dictionary<int, BattleScenarioEditCommandStack> _battleHistories =
        new Dictionary<int, BattleScenarioEditCommandStack>();
    private readonly HashSet<int> _externalDirtyTargets = new HashSet<int>();

    public event Action Changed;

    public SequenceEditCommandStack GetSequenceHistory(ActionSequenceAsset sequence)
    {
        if (sequence == null)
        {
            return null;
        }

        int id = sequence.GetInstanceID();
        if (_sequenceHistories.TryGetValue(id, out SequenceEditCommandStack history))
        {
            return history;
        }

        history = new SequenceEditCommandStack(sequence);
        history.Changed += OnHistoryChanged;
        _sequenceHistories.Add(id, history);
        return history;
    }

    public BattleScenarioEditCommandStack GetBattleHistory(BattleScenarioData battle)
    {
        if (battle == null)
        {
            return null;
        }

        int id = battle.GetInstanceID();
        if (_battleHistories.TryGetValue(id, out BattleScenarioEditCommandStack history))
        {
            return history;
        }

        history = new BattleScenarioEditCommandStack(battle);
        history.Changed += OnHistoryChanged;
        _battleHistories.Add(id, history);
        return history;
    }

    public bool IsDirty(SequenceMakerWorkspaceState workspace)
    {
        if (workspace == null || workspace.ActiveTarget == null)
        {
            return false;
        }

        if (_externalDirtyTargets.Contains(workspace.ActiveTarget.GetInstanceID()))
        {
            return true;
        }

        if (workspace.TargetKind == SequenceMakerTargetKind.StandaloneSequence)
        {
            return IsSequenceDirty(workspace.StandaloneSequence);
        }

        BattleScenarioData battle = workspace.BattleScenario;
        if (battle == null)
        {
            return false;
        }

        if (_battleHistories.TryGetValue(
                battle.GetInstanceID(),
                out BattleScenarioEditCommandStack battleHistory)
            && battleHistory.IsDirty)
        {
            return true;
        }

        if (battle.Sequences == null)
        {
            return false;
        }

        for (int i = 0; i < battle.Sequences.Count; i++)
        {
            if (IsSequenceDirty(battle.Sequences[i]))
            {
                return true;
            }
        }
        return false;
    }

    public void MarkSaved(SequenceMakerWorkspaceState workspace)
    {
        if (workspace == null)
        {
            return;
        }

        UnityEngine.Object target = workspace.ActiveTarget;
        if (target != null)
        {
            _externalDirtyTargets.Remove(target.GetInstanceID());
        }

        if (workspace.TargetKind == SequenceMakerTargetKind.StandaloneSequence)
        {
            MarkSequenceSaved(workspace.StandaloneSequence);
            Changed?.Invoke();
            return;
        }

        BattleScenarioData battle = workspace.BattleScenario;
        if (battle == null)
        {
            Changed?.Invoke();
            return;
        }

        if (_battleHistories.TryGetValue(
                battle.GetInstanceID(),
                out BattleScenarioEditCommandStack battleHistory))
        {
            battleHistory.MarkSaved();
        }

        if (battle.Sequences != null)
        {
            for (int i = 0; i < battle.Sequences.Count; i++)
            {
                MarkSequenceSaved(battle.Sequences[i]);
            }
        }
        Changed?.Invoke();
    }

    public void SetExternalChanges(UnityEngine.Object target, bool dirty)
    {
        if (target == null)
        {
            return;
        }

        int id = target.GetInstanceID();
        bool changed = dirty
            ? _externalDirtyTargets.Add(id)
            : _externalDirtyTargets.Remove(id);
        if (changed)
        {
            Changed?.Invoke();
        }
    }

    public void Reset(SequenceMakerWorkspaceState workspace)
    {
        if (workspace == null)
        {
            return;
        }

        UnityEngine.Object target = workspace.ActiveTarget;
        if (target != null)
        {
            _externalDirtyTargets.Remove(target.GetInstanceID());
        }

        BattleScenarioData battle = workspace.BattleScenario;
        if (battle != null)
        {
            RemoveBattleHistory(battle);
            if (battle.Sequences != null)
            {
                for (int i = 0; i < battle.Sequences.Count; i++)
                {
                    RemoveSequenceHistory(battle.Sequences[i]);
                }
            }
        }
        else
        {
            RemoveSequenceHistory(workspace.StandaloneSequence);
        }
        Changed?.Invoke();
    }

    private bool IsSequenceDirty(ActionSequenceAsset sequence)
    {
        return sequence != null
            && _sequenceHistories.TryGetValue(
                sequence.GetInstanceID(),
                out SequenceEditCommandStack history)
            && history.IsDirty;
    }

    private void MarkSequenceSaved(ActionSequenceAsset sequence)
    {
        if (sequence != null
            && _sequenceHistories.TryGetValue(
                sequence.GetInstanceID(),
                out SequenceEditCommandStack history))
        {
            history.MarkSaved();
        }
    }

    private void RemoveSequenceHistory(ActionSequenceAsset sequence)
    {
        if (sequence == null)
        {
            return;
        }

        int id = sequence.GetInstanceID();
        if (_sequenceHistories.TryGetValue(id, out SequenceEditCommandStack history))
        {
            history.Changed -= OnHistoryChanged;
            _sequenceHistories.Remove(id);
        }
    }

    private void RemoveBattleHistory(BattleScenarioData battle)
    {
        if (battle == null)
        {
            return;
        }

        int id = battle.GetInstanceID();
        if (_battleHistories.TryGetValue(id, out BattleScenarioEditCommandStack history))
        {
            history.Changed -= OnHistoryChanged;
            _battleHistories.Remove(id);
        }
    }

    private void OnHistoryChanged(SequenceEditChange change)
    {
        Changed?.Invoke();
    }
}
