#if UNITY_EDITOR
using System;
using System.Collections.Generic;

internal static class ShopContentRules
{
    public static void Validate(ContentValidationRuleContext context)
    {
        var owners = new Dictionary<string, List<CounterOwner>>(StringComparer.Ordinal);
        foreach (ShopDefinition shop in context.Snapshot.Shops)
        {
            if (shop == null) continue;
            for (int index = 0; index < shop.Entries.Count; index++)
            {
                ShopEntry entry = shop.Entries[index];
                if (entry == null || string.IsNullOrEmpty(entry.PurchaseCounterFlag)) continue;
                if (!owners.TryGetValue(entry.PurchaseCounterFlag, out List<CounterOwner> uses))
                    owners.Add(entry.PurchaseCounterFlag, uses = new List<CounterOwner>());
                uses.Add(new CounterOwner(shop, entry.EntryId, index));
            }
        }

        foreach (KeyValuePair<string, List<CounterOwner>> pair in owners)
        {
            if (pair.Value.Count < 2) continue;
            for (int index = 0; index < pair.Value.Count; index++)
            {
                CounterOwner owner = pair.Value[index];
                CounterOwner other = pair.Value[index == 0 ? 1 : 0];
                string otherPath = context.Snapshot.GetAssetPath(other.Shop);
                context.Add(owner.Shop, "shop.purchase_counter.shared",
                    $"상품 #{owner.Index + 1} '{owner.EntryId}'의 구매 카운터 '{pair.Key}'를 {pair.Value.Count}개 상품이 공유합니다. "
                    + $"다른 사용처: {other.Shop.DisplayName} / 상품 #{other.Index + 1} '{other.EntryId}' ({otherPath}). "
                    + "구매 횟수와 한정 재고가 함께 바뀝니다. 의도한 공용 재고가 아니라면 복제한 상품의 Flag를 바꾸세요.",
                    ContentValidationSeverity.Warning);
            }
        }
    }

    private readonly struct CounterOwner
    {
        public readonly ShopDefinition Shop;
        public readonly string EntryId;
        public readonly int Index;

        public CounterOwner(ShopDefinition shop, string entryId, int index)
        {
            Shop = shop;
            EntryId = entryId;
            Index = index;
        }
    }
}
#endif
