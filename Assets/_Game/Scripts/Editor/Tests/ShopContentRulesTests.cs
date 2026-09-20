#if UNITY_EDITOR
using System.Linq;
using NUnit.Framework;
using UnityEngine;

public sealed class ShopContentRulesTests
{
    [TestCase(true)]
    [TestCase(false)]
    public void SharedPurchaseCounterIsWarningWithinOneShopOrAcrossShops(bool sameShop)
    {
        ShopDefinition first = ScriptableObject.CreateInstance<ShopDefinition>();
        ShopDefinition second = ScriptableObject.CreateInstance<ShopDefinition>();
        try
        {
            var firstEntry = new ShopEntry("first", null, 0, 1, 1, " shop.counter ");
            var secondEntry = new ShopEntry("second", null, 0, 1, 0, "shop.counter");
            first.Configure("shop.first", "첫 상점", sameShop ? new[] { firstEntry, secondEntry } : new[] { firstEntry });
            second.Configure("shop.second", "다른 상점", new[] { secondEntry });
            var snapshot = new ProjectContentSnapshot();
            snapshot.Shops.Add(first);
            if (!sameShop) snapshot.Shops.Add(second);
            var report = new ContentValidationReport();
            ShopContentRules.Validate(new ContentValidationRuleContext(snapshot, report));

            Assert.That(report.ErrorCount, Is.Zero);
            Assert.That(report.WarningCount, Is.EqualTo(2));
            Assert.That(report.Issues.All(issue => issue.Code == "shop.purchase_counter.shared"), Is.True);
            Assert.That(firstEntry.PurchaseCounterFlag, Is.EqualTo("shop.counter"));
            Assert.That(secondEntry.PurchaseCounterFlag, Is.EqualTo("shop.counter"));
            if (!sameShop) Assert.That(report.Issues.Any(issue => issue.Context == second), Is.True);
        }
        finally
        {
            Object.DestroyImmediate(first);
            Object.DestroyImmediate(second);
        }
    }

    [Test]
    public void EmptyAndIndependentCountersAreNotReportedAsShared()
    {
        ShopDefinition shop = ScriptableObject.CreateInstance<ShopDefinition>();
        try
        {
            shop.Configure("shop.single", "상점", new[]
            {
                new ShopEntry("one", null, 0, 1, 0, "counter.one"),
                new ShopEntry("two", null, 0, 1, 0, "counter.two"),
                new ShopEntry("empty", null, 0, 1, 0, string.Empty),
                new ShopEntry("empty2", null, 0, 1, 0, string.Empty)
            });
            var snapshot = new ProjectContentSnapshot();
            snapshot.Shops.Add(shop);
            var report = new ContentValidationReport();
            ShopContentRules.Validate(new ContentValidationRuleContext(snapshot, report));
            Assert.That(report.Issues, Is.Empty);
        }
        finally { Object.DestroyImmediate(shop); }
    }
}
#endif
