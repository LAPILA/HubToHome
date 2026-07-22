#if UNITY_EDITOR
using NUnit.Framework;
using UnityEngine;

public sealed class ContentIdAssignmentTests
{
    [Test]
    public void AssignmentFillsOnlyMissingIdsAndAvoidsGeneratedCollisions()
    {
        ItemData existing = ScriptableObject.CreateInstance<ItemData>();
        ItemData duplicate = ScriptableObject.CreateInstance<ItemData>();
        ItemData firstMissing = ScriptableObject.CreateInstance<ItemData>();
        ItemData secondMissing = ScriptableObject.CreateInstance<ItemData>();
        try
        {
            existing.name = "Potion";
            duplicate.name = "Potion";
            firstMissing.name = "Potion";
            secondMissing.name = "Potion";
            existing.ItemID = "item.same";
            duplicate.ItemID = "item.same";
            firstMissing.ItemID = string.Empty;
            secondMissing.ItemID = string.Empty;

            ItemData[] assets = { existing, duplicate, firstMissing, secondMissing };
            int assigned = ContentIdAssignment.AssignMissingIds(
                assets,
                item => item.ItemID,
                (item, id) => item.ItemID = id,
                "item",
                _ => "12345678");

            Assert.That(assigned, Is.EqualTo(2));
            Assert.That(existing.ItemID, Is.EqualTo("item.same"));
            Assert.That(duplicate.ItemID, Is.EqualTo("item.same"));
            Assert.That(firstMissing.ItemID, Is.EqualTo("item_potion_12345678"));
            Assert.That(secondMissing.ItemID, Is.EqualTo("item_potion_12345678_2"));
        }
        finally
        {
            Object.DestroyImmediate(existing);
            Object.DestroyImmediate(duplicate);
            Object.DestroyImmediate(firstMissing);
            Object.DestroyImmediate(secondMissing);
        }
    }
}
#endif
