#if UNITY_EDITOR
using System;
using System.Collections.Generic;

/// <summary>기존 목표 투영을 사용합니다. 퀘스트 상태나 보상을 새로 만들지 않습니다.</summary>
internal static class PrologueTutorialJournalFactory
{
    internal static void Build()
    {
        JournalCatalog catalog = PrologueTutorialContentBuilder.Require<JournalCatalog>(
            "Assets/_Game/Resources/HubToHome/JournalCatalog.asset");
        if (catalog.Entries == null) catalog.Entries = new List<JournalEntry>();
        // 재생성해도 다른 작성자가 만든 기록과 목록 순서는 보존합니다.
        catalog.Entries.RemoveAll(entry => entry != null
            && entry.Id != null && entry.Id.StartsWith("prologue.tutorial.objective.", StringComparison.Ordinal));
        var entries = new List<JournalEntry>
        {
            Entry("repair", "REPAIR", "prologue.opening.completed", PrologueTutorialProgress.InspectCompleted),
            Entry("valve", "VALVE", PrologueTutorialProgress.InspectCompleted, PrologueTutorialProgress.ValveCompleted),
            Entry("test", "TEST", PrologueTutorialProgress.ValveCompleted, PrologueTutorialProgress.RepairCompleted),
            Entry("rest", "REST", PrologueTutorialProgress.RepairCompleted, PrologueTutorialProgress.IncidentCompleted),
            Entry("rear", "REAR", PrologueTutorialProgress.IncidentCompleted, PrologueTutorialProgress.RearCompleted),
            Entry("battle", "BATTLE", PrologueTutorialProgress.RearCompleted, PrologueTutorialProgress.WithdrawalCompleted),
            Entry("seal", "SEAL", PrologueTutorialProgress.WithdrawalCompleted, PrologueTutorialProgress.SealCompleted),
            Entry("control", "CONTROL", PrologueTutorialProgress.SealCompleted, PrologueTutorialProgress.ControlCompleted),
            Entry("complete", "COMPLETE", PrologueTutorialProgress.Completed, null)
        };
        catalog.Entries.InsertRange(0, entries);
        PrologueTutorialContentBuilder.Save(catalog);
    }

    private static JournalEntry Entry(string id, string suffix, string required, string completed)
    {
        string key = "HTH_PROLOGUE_OBJECTIVE_" + suffix;
        return new JournalEntry
        {
            Id = "prologue.tutorial.objective." + id,
            TitleKey = key,
            Title = PrologueTutorialAssetFactory.Korean(key),
            BodyKey = key,
            Body = PrologueTutorialAssetFactory.Korean(key),
            IsObjective = true,
            CompletedFlag = completed,
            Conditions = new List<JournalFlagCondition>
            {
                new JournalFlagCondition { Flag = required, Minimum = 1 }
            }
        };
    }
}
#endif
