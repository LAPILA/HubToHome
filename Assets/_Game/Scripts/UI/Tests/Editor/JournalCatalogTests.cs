using NUnit.Framework;
using UnityEngine;

public sealed class JournalCatalogTests
{
    [Test]
    public void ObjectiveUsesAuthoredOrderAndSkipsCompletedOrLockedEntries()
    {
        JournalCatalog catalog = ScriptableObject.CreateInstance<JournalCatalog>();
        try
        {
            var locked = new JournalEntry { Id = "locked", IsObjective = true };
            locked.Conditions.Add(new JournalFlagCondition { Flag = "chapter", Minimum = 2 });
            var completed = new JournalEntry { Id = "done", IsObjective = true, CompletedFlag = "done" };
            var current = new JournalEntry { Id = "current", IsObjective = true };
            catalog.Entries.Add(locked); catalog.Entries.Add(completed); catalog.Entries.Add(current);
            Assert.That(catalog.GetCurrentObjective(key => key == "done" ? 1 : 0), Is.SameAs(current));
            Assert.That(catalog.GetCurrentObjective(key => key == "chapter" ? 2 : 0), Is.SameAs(locked));
        }
        finally { Object.DestroyImmediate(catalog); }
    }

    [Test]
    public void EmptyCatalogDoesNotInventObjective()
    {
        JournalCatalog catalog = ScriptableObject.CreateInstance<JournalCatalog>();
        try { Assert.That(catalog.GetCurrentObjective(_ => 0), Is.Null); }
        finally { Object.DestroyImmediate(catalog); }
    }

    [Test]
    public void AllVisibilityConditionsMustBeMet()
    {
        var entry = new JournalEntry();
        entry.Conditions.Add(new JournalFlagCondition { Flag = "chapter", Minimum = 1, Maximum = 1 });
        entry.Conditions.Add(new JournalFlagCondition { Flag = "note", Minimum = 1 });
        Assert.That(entry.IsVisible(key => key == "chapter" ? 1 : 0), Is.False);
        Assert.That(entry.IsVisible(_ => 1), Is.True);
        Assert.That(entry.IsVisible(_ => 2), Is.False);
    }
}
