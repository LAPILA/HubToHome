using System.Collections.Generic;
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

    [TestCase(null)]
    [TestCase("")]
    [TestCase("   ")]
    public void CollectionRequiresAnAuthoredItemId(string itemId)
    {
        var entry = new JournalEntry { RequiredItemId = itemId };
        Assert.That(entry.IsCollected(_ => 1, _ => 1), Is.False);
    }

    [TestCase(0, false)]
    [TestCase(-1, false)]
    [TestCase(1, true)]
    [TestCase(3, true)]
    public void CollectionRequiresPositiveOwnedCount(int owned, bool expected)
    {
        var entry = new JournalEntry { RequiredItemId = "note.workshop" };
        Assert.That(entry.IsCollected(null, _ => owned), Is.EqualTo(expected));
    }

    [Test]
    public void CollectionExcludesObjectivesEvenWhenTheirItemIsOwned()
    {
        var entry = new JournalEntry { IsObjective = true, RequiredItemId = "note.workshop" };
        Assert.That(entry.IsCollected(_ => 1, _ => 1), Is.False);
        Assert.That(entry.IsVisible(_ => 1), Is.True);
    }

    [Test]
    public void CollectionRequiresAnItemReaderWithoutQueryingFlags()
    {
        var entry = new JournalEntry { RequiredItemId = "note.workshop" };
        bool flagRead = false;
        Assert.That(entry.IsCollected(_ => { flagRead = true; return 1; }, null), Is.False);
        Assert.That(flagRead, Is.False);
    }

    [Test]
    public void CollectionReusesFlagConditionsAndRetainsCompletedNotes()
    {
        var entry = new JournalEntry { RequiredItemId = "note.workshop", CompletedFlag = "read" };
        entry.Conditions.Add(new JournalFlagCondition { Flag = "chapter", Minimum = 2, Maximum = 2 });
        Assert.That(entry.IsCollected(null, _ => 1), Is.False);
        Assert.That(entry.IsCollected(key => key == "read" ? 1 : 0, _ => 1), Is.False);
        Assert.That(entry.IsCollected(key => key == "chapter" ? 2 : 1, _ => 1), Is.True);
        Assert.That(entry.IsCompleted(key => key == "read" ? 1 : 0), Is.True);
    }

    [Test]
    public void CollectedEntriesClearDestinationAndPreserveAuthoredOrder()
    {
        JournalCatalog catalog = ScriptableObject.CreateInstance<JournalCatalog>();
        try
        {
            var first = new JournalEntry { Id = "first", RequiredItemId = "note.first" };
            var last = new JournalEntry { Id = "last", RequiredItemId = "note.last" };
            var locked = new JournalEntry { Id = "locked", RequiredItemId = "note.locked" };
            locked.Conditions.Add(new JournalFlagCondition { Flag = "chapter", Minimum = 2 });
            catalog.Entries.Add(new JournalEntry { Id = "help" });
            catalog.Entries.Add(first);
            catalog.Entries.Add(null);
            catalog.Entries.Add(new JournalEntry { Id = "objective", IsObjective = true, RequiredItemId = "note.goal" });
            catalog.Entries.Add(new JournalEntry { Id = "unowned", RequiredItemId = "note.missing" });
            catalog.Entries.Add(locked);
            catalog.Entries.Add(last);
            var destination = new List<JournalEntry> { new JournalEntry { Id = "stale" } };
            var inventory = new Dictionary<string, int>
            {
                ["note.first"] = 1, ["note.last"] = 1, ["note.locked"] = 1, ["note.goal"] = 1
            };

            catalog.GetCollectedEntries(destination, _ => 0,
                id => inventory.TryGetValue(id, out int count) ? count : 0);

            Assert.That(destination, Is.EqualTo(new[] { first, last }));
            Assert.That(inventory.Count, Is.EqualTo(4));
            Assert.That(inventory["note.first"], Is.EqualTo(1));
            Assert.That(catalog.Entries.Count, Is.EqualTo(7));
        }
        finally { Object.DestroyImmediate(catalog); }
    }

    [Test]
    public void MissingEntriesOrReaderClearPreviouslyCollectedEntries()
    {
        JournalCatalog catalog = ScriptableObject.CreateInstance<JournalCatalog>();
        try
        {
            var entry = new JournalEntry { RequiredItemId = "note.workshop" };
            catalog.Entries.Add(entry);
            var destination = new List<JournalEntry> { entry };
            catalog.GetCollectedEntries(destination, null, null);
            Assert.That(destination, Is.Empty);

            destination.Add(entry);
            catalog.Entries = null;
            catalog.GetCollectedEntries(destination, null, _ => 1);
            Assert.That(destination, Is.Empty);
        }
        finally { Object.DestroyImmediate(catalog); }
    }

    [Test]
    public void CollectionDoesNotChangeLegacyObjectiveSelection()
    {
        JournalCatalog catalog = ScriptableObject.CreateInstance<JournalCatalog>();
        try
        {
            var objective = new JournalEntry { Id = "goal", IsObjective = true };
            catalog.Entries.Add(new JournalEntry { Id = "help" });
            catalog.Entries.Add(objective);
            catalog.Entries.Add(new JournalEntry { Id = "owned", RequiredItemId = "note.workshop" });

            catalog.GetCollectedEntries(new List<JournalEntry>(), _ => 0, _ => 1);

            Assert.That(catalog.GetCurrentObjective(_ => 0), Is.SameAs(objective));
            Assert.That(catalog.Entries[0].IsVisible(null), Is.True);
        }
        finally { Object.DestroyImmediate(catalog); }
    }

    [Test]
    public void CollectionRejectsMissingDestinationAndCatalogListAlias()
    {
        JournalCatalog catalog = ScriptableObject.CreateInstance<JournalCatalog>();
        try
        {
            catalog.Entries.Add(new JournalEntry { RequiredItemId = "note.workshop" });
            Assert.Throws<System.ArgumentNullException>(() => catalog.GetCollectedEntries(null, null, _ => 1));
            Assert.Throws<System.ArgumentException>(() => catalog.GetCollectedEntries(catalog.Entries, null, _ => 1));
            Assert.That(catalog.Entries.Count, Is.EqualTo(1));
        }
        finally { Object.DestroyImmediate(catalog); }
    }
}
