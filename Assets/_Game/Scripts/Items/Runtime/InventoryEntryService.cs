using System;

/// <summary>상품/상자/드롭의 ItemData를 실제 아이템 또는 장비 보관함으로 연결합니다.</summary>
public static class InventoryEntryService
{
    public static bool IsRegistered(ItemData item)
    {
        if (item == null || string.IsNullOrWhiteSpace(item.ItemID)
            || !ReferenceEquals(ItemDatabase.FindById(item.ItemID), item)) return false;
        return item.Type != ItemType.Equipment || (item.Equipment != null
            && !string.IsNullOrWhiteSpace(item.Equipment.ItemID)
            && ReferenceEquals(EquipmentDatabase.FindById(item.Equipment.ItemID), item.Equipment));
    }

    public static int GetOwnedCount(GlobalDataManager global, ItemData item)
    {
        if (global == null || item == null) return 0;
        return item.Type == ItemType.Equipment
            ? item.Equipment != null ? global.GetEquipmentCount(item.Equipment.ItemID) : 0
            : global.GetItemCount(item.ItemID);
    }

    public static int GetRemovableCount(GlobalDataManager global, ItemData item)
    {
        int owned = GetOwnedCount(global, item);
        if (global == null || item == null || item.Type != ItemType.Equipment || item.Equipment == null)
            return owned;
        return Math.Max(0, owned - EquipmentLoadoutService.CountEquipped(global.Party, item.Equipment.ItemID));
    }

    public static int GetAdditionalCapacity(GlobalDataManager global, ItemData item)
        => global != null && IsRegistered(item) ? Math.Max(0, item.StackLimit - GetOwnedCount(global, item)) : 0;

    public static int Add(GlobalDataManager global, ItemData item, int amount)
    {
        if (global == null || !IsRegistered(item) || amount <= 0) return 0;
        int accepted = Math.Min(amount, GetAdditionalCapacity(global, item));
        if (accepted <= 0) return 0;
        return item.Type == ItemType.Equipment
            ? global.AddEquipmentAndGetAddedAmount(item.Equipment.ItemID, accepted)
            : global.AddItemAndGetAddedAmount(item.ItemID, accepted);
    }

    public static bool Remove(GlobalDataManager global, ItemData item, int amount)
    {
        if (global == null || !IsRegistered(item) || amount <= 0 || GetRemovableCount(global, item) < amount)
            return false;
        return item.Type == ItemType.Equipment
            ? global.RemoveEquipment(item.Equipment.ItemID, amount)
            : global.RemoveItem(item.ItemID, amount);
    }

    public static bool CanSell(ItemData item)
        => item != null && item.IsSellable && item.Price >= 0 && item.Type != ItemType.KeyItem
            && (item.Type != ItemType.Equipment || (item.Equipment != null && item.Equipment.IsSellable));
}
