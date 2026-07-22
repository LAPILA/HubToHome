#if UNITY_EDITOR
using System.Linq;
using NUnit.Framework;
using UnityEngine;

public sealed class EnemyDropContentValidationTests
{
    [Test]
    public void ValidatorReportsStructuredAndLegacyDropProblems()
    {
        EnemyData enemy = ScriptableObject.CreateInstance<EnemyData>();
        ItemData knownItem = ScriptableObject.CreateInstance<ItemData>();
        try
        {
            enemy.EnemyId = "enemy.drop_test";
            knownItem.ItemID = "item.known";
            enemy.Drops.Add(new EnemyDropEntry
            {
                ItemId = "item.unknown",
                MinAmount = 2,
                MaxAmount = 1,
                DropChance = 1.5f
            });
            enemy.DropItemIDs.Add(string.Empty);
            enemy.DropItemIDs.Add("item.legacy_unknown");

            var snapshot = new ProjectContentSnapshot();
            snapshot.Enemies.Add(enemy);
            snapshot.Items.Add(knownItem);

            ContentValidationReport report = ProjectContentValidator.Validate(snapshot);
            string[] codes = report.Issues.Select(issue => issue.Code).ToArray();

            Assert.That(codes, Does.Contain("enemy.drop.item.unknown"));
            Assert.That(codes, Does.Contain("enemy.drop.amount.invalid"));
            Assert.That(codes, Does.Contain("enemy.drop.chance.invalid"));
            Assert.That(codes, Does.Contain("enemy.legacy_drop.item.missing"));
            Assert.That(codes, Does.Contain("enemy.legacy_drop.item.unknown"));
        }
        finally
        {
            Object.DestroyImmediate(enemy);
            Object.DestroyImmediate(knownItem);
        }
    }
}
#endif
