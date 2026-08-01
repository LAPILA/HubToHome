using System;
using System.Collections.Generic;

public enum EquipmentChangeStatus
{
    Success,
    InvalidCharacter,
    InvalidEquipment,
    NotRegistered,
    WrongSlot,
    CharacterRestricted,
    NotOwned,
    AlreadyEquipped
}

public readonly struct EquipmentChangeResult
{
    public EquipmentChangeResult(
        EquipmentChangeStatus status,
        EquipmentSlot slot,
        string previousEquipmentId,
        string currentEquipmentId,
        string message)
    {
        Status = status;
        Slot = slot;
        PreviousEquipmentId = previousEquipmentId ?? string.Empty;
        CurrentEquipmentId = currentEquipmentId ?? string.Empty;
        Message = message ?? string.Empty;
    }

    public EquipmentChangeStatus Status { get; }
    public EquipmentSlot Slot { get; }
    public string PreviousEquipmentId { get; }
    public string CurrentEquipmentId { get; }
    public string Message { get; }
    public bool Succeeded => Status == EquipmentChangeStatus.Success;
}

public static class EquipmentLoadoutService
{
    public const int SlotCount = 6;

    public static readonly EquipmentSlot[] OrderedSlots =
    {
        EquipmentSlot.Weapon,
        EquipmentSlot.Accessory1,
        EquipmentSlot.Accessory2,
        EquipmentSlot.Head,
        EquipmentSlot.Body,
        EquipmentSlot.Shoes
    };

    public static void NormalizeSlots(CharacterSaveData character)
    {
        if (character == null)
            return;

        character.EquippedEquipmentIDs ??= new List<string>();
        while (character.EquippedEquipmentIDs.Count < SlotCount)
            character.EquippedEquipmentIDs.Add(string.Empty);
        if (character.EquippedEquipmentIDs.Count > SlotCount)
            character.EquippedEquipmentIDs.RemoveRange(SlotCount, character.EquippedEquipmentIDs.Count - SlotCount);

        for (int i = 0; i < character.EquippedEquipmentIDs.Count; i++)
            character.EquippedEquipmentIDs[i] = NormalizeId(character.EquippedEquipmentIDs[i]);
    }

    public static string GetEquippedId(CharacterSaveData character, EquipmentSlot slot)
    {
        if (character == null || !TryGetSlotIndex(slot, out int index))
            return string.Empty;

        NormalizeSlots(character);
        return character.EquippedEquipmentIDs[index];
    }

    public static EquipmentData GetEquipped(CharacterSaveData character, EquipmentSlot slot)
    {
        return EquipmentDatabase.FindById(GetEquippedId(character, slot));
    }

    public static EquipmentChangeResult TryEquip(
        GlobalDataManager global,
        CharacterSaveData character,
        EquipmentSlot slot,
        EquipmentData equipment)
    {
        if (character == null)
            return Failed(EquipmentChangeStatus.InvalidCharacter, slot, "파티원이 없습니다.");
        if (equipment == null || string.IsNullOrWhiteSpace(equipment.ItemID))
            return Failed(EquipmentChangeStatus.InvalidEquipment, slot, "장비 데이터가 올바르지 않습니다.");
        if (!TryGetSlotIndex(slot, out int index) || equipment.Slot != slot)
            return Failed(EquipmentChangeStatus.WrongSlot, slot, "선택한 슬롯과 장비 종류가 다릅니다.");

        string equipmentId = equipment.ItemID.Trim();
        EquipmentData registered = EquipmentDatabase.FindById(equipmentId);
        if (registered == null || !ReferenceEquals(registered, equipment))
            return Failed(EquipmentChangeStatus.NotRegistered, slot, "장비가 콘텐츠 카탈로그에 등록되지 않았습니다.");
        if (!equipment.CanEquip(character.CharacterDataID))
            return Failed(EquipmentChangeStatus.CharacterRestricted, slot, "이 캐릭터는 해당 장비를 사용할 수 없습니다.");
        if (global == null || global.GetEquipmentCount(equipmentId) <= CountEquipped(global.Party, equipmentId, character, slot))
            return Failed(EquipmentChangeStatus.NotOwned, slot, "사용 가능한 장비 수량이 없습니다.");

        NormalizeSlots(character);
        string previous = character.EquippedEquipmentIDs[index];
        if (string.Equals(previous, equipmentId, StringComparison.Ordinal))
        {
            return new EquipmentChangeResult(
                EquipmentChangeStatus.AlreadyEquipped,
                slot,
                previous,
                previous,
                "이미 장착 중입니다.");
        }

        character.EquippedEquipmentIDs[index] = equipmentId;
        return new EquipmentChangeResult(
            EquipmentChangeStatus.Success,
            slot,
            previous,
            equipmentId,
            "장비를 변경했습니다.");
    }

    public static EquipmentChangeResult TryUnequip(CharacterSaveData character, EquipmentSlot slot)
    {
        if (character == null)
            return Failed(EquipmentChangeStatus.InvalidCharacter, slot, "파티원이 없습니다.");
        if (!TryGetSlotIndex(slot, out int index))
            return Failed(EquipmentChangeStatus.WrongSlot, slot, "장비 슬롯이 올바르지 않습니다.");

        NormalizeSlots(character);
        string previous = character.EquippedEquipmentIDs[index];
        character.EquippedEquipmentIDs[index] = string.Empty;
        return new EquipmentChangeResult(
            EquipmentChangeStatus.Success,
            slot,
            previous,
            string.Empty,
            string.IsNullOrEmpty(previous) ? "비어 있는 슬롯입니다." : "장비를 해제했습니다.");
    }

    public static int GetFlatBonus(CharacterSaveData character, Func<EquipmentData, int> selector)
    {
        if (character == null || selector == null)
            return 0;

        NormalizeSlots(character);
        int total = 0;
        for (int i = 0; i < SlotCount; i++)
        {
            EquipmentData equipment = EquipmentDatabase.FindById(character.EquippedEquipmentIDs[i]);
            if (equipment != null)
                total += selector(equipment);
        }

        return total;
    }

    public static bool TryGetSlotIndex(EquipmentSlot slot, out int index)
    {
        index = (int)slot;
        return index >= 0 && index < SlotCount;
    }

    private static int CountEquipped(
        IReadOnlyList<CharacterSaveData> party,
        string equipmentId,
        CharacterSaveData targetCharacter,
        EquipmentSlot targetSlot)
    {
        int count = 0;
        if (party == null)
            return count;

        for (int memberIndex = 0; memberIndex < party.Count; memberIndex++)
        {
            CharacterSaveData member = party[memberIndex];
            if (member == null)
                continue;

            NormalizeSlots(member);
            for (int slotIndex = 0; slotIndex < SlotCount; slotIndex++)
            {
                if (ReferenceEquals(member, targetCharacter) && slotIndex == (int)targetSlot)
                    continue;
                if (string.Equals(member.EquippedEquipmentIDs[slotIndex], equipmentId, StringComparison.Ordinal))
                    count++;
            }
        }

        return count;
    }

    private static EquipmentChangeResult Failed(EquipmentChangeStatus status, EquipmentSlot slot, string message)
    {
        return new EquipmentChangeResult(status, slot, string.Empty, string.Empty, message);
    }

    private static string NormalizeId(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
    }
}