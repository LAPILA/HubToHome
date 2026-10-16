using System;
using System.Collections.Generic;
using UnityEngine;

public enum EquipmentChangeStatus
{
    Success,
    InvalidCharacter,
    InvalidEquipment,
    NotRegistered,
    WrongSlot,
    CharacterRestricted,
    NotOwned,
    AlreadyEquipped,
    Blocked
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

        return character.EquippedEquipmentIDs != null && index < character.EquippedEquipmentIDs.Count
            ? NormalizeId(character.EquippedEquipmentIDs[index]) : string.Empty;
    }

    public static EquipmentData GetEquipped(CharacterSaveData character, EquipmentSlot slot)
    {
        // UI/회복 상한 사전 계산은 저장 데이터 자체를 수정하지 않습니다.
        if (character == null || !TryGetSlotIndex(slot, out int index)
            || character.EquippedEquipmentIDs == null || index >= character.EquippedEquipmentIDs.Count)
            return null;
        EquipmentData equipment = EquipmentDatabase.FindById(NormalizeId(character.EquippedEquipmentIDs[index]));
        return CanApply(equipment, slot, character.CharacterDataID) ? equipment : null;
    }

    public static bool CanApply(EquipmentData equipment, EquipmentSlot slot, string characterDataId)
        => equipment != null && TryGetSlotIndex(slot, out _) && equipment.Slot == slot
            && equipment.CanEquip(characterDataId);

    public static EquipmentChangeResult TryEquip(
        GlobalDataManager global,
        CharacterSaveData character,
        EquipmentSlot slot,
        EquipmentData equipment)
    {
        if (character == null || global == null || !global.Party.Contains(character))
            return Failed(EquipmentChangeStatus.InvalidCharacter, slot, "파티원이 없습니다.");
        if (IsBattleLocked)
            return Failed(EquipmentChangeStatus.Blocked, slot, "전투 중에는 장비를 변경할 수 없습니다.");
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
        if (GetAvailableCount(global, character, slot, equipmentId) <= 0)
            return Failed(EquipmentChangeStatus.NotOwned, slot, "사용 가능한 장비 수량이 없습니다.");

        NormalizeSlots(character);
        string previous = character.EquippedEquipmentIDs[index];
        character.HasInitializedEquipment = true;
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
        ClampSavedVitals(character);
        return new EquipmentChangeResult(
            EquipmentChangeStatus.Success,
            slot,
            previous,
            equipmentId,
            "장비를 변경했습니다.");
    }

    public static EquipmentChangeResult TryUnequip(CharacterSaveData character, EquipmentSlot slot)
    {
        if (IsBattleLocked)
            return Failed(EquipmentChangeStatus.Blocked, slot, "전투 중에는 장비를 변경할 수 없습니다.");
        if (character == null)
            return Failed(EquipmentChangeStatus.InvalidCharacter, slot, "파티원이 없습니다.");
        if (!TryGetSlotIndex(slot, out int index))
            return Failed(EquipmentChangeStatus.WrongSlot, slot, "장비 슬롯이 올바르지 않습니다.");

        NormalizeSlots(character);
        string previous = character.EquippedEquipmentIDs[index];
        character.EquippedEquipmentIDs[index] = string.Empty;
        character.HasInitializedEquipment = true;
        ClampSavedVitals(character);
        return new EquipmentChangeResult(
            EquipmentChangeStatus.Success,
            slot,
            previous,
            string.Empty,
            string.IsNullOrEmpty(previous) ? "비어 있는 슬롯입니다." : "장비를 해제했습니다.");
    }

    public static List<StatModifier> BuildStatModifiers(CharacterSaveData character)
    {
        var modifiers = new List<StatModifier>();
        if (character == null)
            return modifiers;

        // 조회/회복 사전 평가에서도 사용되므로 슬롯 데이터를 정규화하거나 수정하지 않습니다.
        IReadOnlyList<string> equippedIds = character.EquippedEquipmentIDs;
        int count = equippedIds != null ? Math.Min(SlotCount, equippedIds.Count) : 0;
        for (int i = 0; i < count; i++)
        {
            EquipmentData equipment = GetEquipped(character, (EquipmentSlot)i);
            if (equipment != null)
                equipment.AppendStatModifiers(modifiers);
        }

        return modifiers;
    }

    /// <summary>현재 캐릭터의 대상 슬롯은 반환 가능한 수량에 포함합니다.</summary>
    public static int GetAvailableCount(
        GlobalDataManager global,
        CharacterSaveData character,
        EquipmentSlot slot,
        string equipmentId)
    {
        if (global == null || character == null || !TryGetSlotIndex(slot, out _))
            return 0;

        string normalizedId = NormalizeId(equipmentId);
        if (string.IsNullOrEmpty(normalizedId))
            return 0;

        return Math.Max(0, global.GetEquipmentCount(normalizedId)
            - CountEquipped(global.Party, normalizedId, character, slot));
    }

    public static bool TryGetSlotIndex(EquipmentSlot slot, out int index)
    {
        index = (int)slot;
        return index >= 0 && index < SlotCount;
    }

    public static int CountEquipped(
        IReadOnlyList<CharacterSaveData> party,
        string equipmentId,
        CharacterSaveData targetCharacter = null,
        EquipmentSlot targetSlot = EquipmentSlot.Weapon)
    {
        int count = 0;
        equipmentId = NormalizeId(equipmentId);
        if (party == null || equipmentId.Length == 0)
            return count;

        for (int memberIndex = 0; memberIndex < party.Count; memberIndex++)
        {
            CharacterSaveData member = party[memberIndex];
            if (member == null)
                continue;

            for (int slotIndex = 0; slotIndex < SlotCount; slotIndex++)
            {
                if (ReferenceEquals(member, targetCharacter) && slotIndex == (int)targetSlot)
                    continue;
                if (string.Equals(GetEquippedId(member, (EquipmentSlot)slotIndex), equipmentId, StringComparison.Ordinal))
                    count++;
            }
        }

        return count;
    }

    private static void ClampSavedVitals(CharacterSaveData character)
    {
        CharacterStatsProjectionService.ResolveResourceCaps(character,
            CharacterDatabase.FindById(character.CharacterDataID), out int maxHP, out int maxAP);
        character.HP = Mathf.Clamp(character.HP, 0, maxHP);
        character.AP = Mathf.Clamp(character.AP, 0, maxAP);
    }

    public static EquipmentChangeResult TryUnequip(GlobalDataManager global, CharacterSaveData character, EquipmentSlot slot)
    {
        if (global == null || character == null || !global.Party.Contains(character))
            return Failed(EquipmentChangeStatus.InvalidCharacter, slot, "파티원이 없습니다.");
        return TryUnequip(character, slot);
    }

    private static bool IsBattleLocked => (BattleManager.Instance != null && BattleManager.Instance.IsBattleSessionActive)
        || (GameStateManager.Instance != null && GameStateManager.Instance.CurrentState == GameState.Battle);

    private static EquipmentChangeResult Failed(EquipmentChangeStatus status, EquipmentSlot slot, string message)
    {
        return new EquipmentChangeResult(status, slot, string.Empty, string.Empty, message);
    }

    private static string NormalizeId(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
    }
}
