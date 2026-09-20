using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public sealed class EquipmentAndPowerServicesTests
{
    private readonly List<UnityEngine.Object> _created = new List<UnityEngine.Object>();
    private GlobalDataManager _previousGlobal;
    private GlobalDataManager _global;

    [SetUp]
    public void SetUp()
    {
        _previousGlobal = GlobalDataManager.Instance;
        SetGlobalInstance(null);
        GameObject globalObject = new GameObject("EquipmentAndPowerServicesTests_Global");
        _created.Add(globalObject);
        _global = globalObject.AddComponent<GlobalDataManager>();
    }

    [TearDown]
    public void TearDown()
    {
        EquipmentDatabase.InvalidateCache();
        CharacterDatabase.InvalidateCache();
        SkillDatabase.InvalidateCache();
        for (int i = _created.Count - 1; i >= 0; i--)
        {
            if (_created[i] != null)
                UnityEngine.Object.DestroyImmediate(_created[i]);
        }
        _created.Clear();
        SetGlobalInstance(_previousGlobal);
    }

    [Test]
    public void EquipRequiresOwnedRegisteredCompatibleSlotAndUpdatesBonus()
    {
        EquipmentData weapon = Equipment("equip.steam_blade", EquipmentSlot.Weapon, attack: 7);
        SetEquipmentDatabase(weapon);
        CharacterSaveData hero = Character("hero");
        _global.Party.Add(hero);
        _global.AddEquipmentAndGetAddedAmount(weapon.ItemID);

        EquipmentChangeResult result = EquipmentLoadoutService.TryEquip(
            _global,
            hero,
            EquipmentSlot.Weapon,
            weapon);

        Assert.That(result.Status, Is.EqualTo(EquipmentChangeStatus.Success));
        Assert.That(
            EquipmentLoadoutService.GetEquippedId(hero, EquipmentSlot.Weapon),
            Is.EqualTo(weapon.ItemID));
        Assert.That(
            CharacterStatsProjectionService.ResolveFromSave(
                hero,
                CreateCharacterData("hero"))
                .ATK,
            Is.EqualTo(7));
        Assert.That(
            EquipmentLoadoutService.TryEquip(_global, hero, EquipmentSlot.Head, weapon).Status,
            Is.EqualTo(EquipmentChangeStatus.WrongSlot));
    }

    [Test]
    public void OneOwnedCopyCannotBeEquippedByTwoPartyMembers()
    {
        EquipmentData weapon = Equipment("equip.shared", EquipmentSlot.Weapon, attack: 2);
        SetEquipmentDatabase(weapon);
        CharacterSaveData hero = Character("hero");
        CharacterSaveData ally = Character("ally");
        _global.Party.Add(hero);
        _global.Party.Add(ally);
        _global.AddEquipmentAndGetAddedAmount(weapon.ItemID);

        Assert.That(
            EquipmentLoadoutService.TryEquip(_global, hero, EquipmentSlot.Weapon, weapon).Succeeded,
            Is.True);
        Assert.That(
            EquipmentLoadoutService.TryEquip(_global, ally, EquipmentSlot.Weapon, weapon).Status,
            Is.EqualTo(EquipmentChangeStatus.NotOwned));

        EquipmentLoadoutService.TryUnequip(hero, EquipmentSlot.Weapon);
        Assert.That(
            EquipmentLoadoutService.TryEquip(_global, ally, EquipmentSlot.Weapon, weapon).Succeeded,
            Is.True);
    }

    [Test]
    public void CharacterRestrictionIsEnforcedByStableCharacterDataId()
    {
        EquipmentData weapon = Equipment("equip.hero_only", EquipmentSlot.Weapon, attack: 1);
        weapon.AllowedCharacterIDs.Add("hero");
        SetEquipmentDatabase(weapon);
        CharacterSaveData ally = Character("ally");
        _global.Party.Add(ally);
        _global.AddEquipmentAndGetAddedAmount(weapon.ItemID);

        EquipmentChangeResult result = EquipmentLoadoutService.TryEquip(
            _global,
            ally,
            EquipmentSlot.Weapon,
            weapon);

        Assert.That(result.Status, Is.EqualTo(EquipmentChangeStatus.CharacterRestricted));
    }

    [Test]
    public void GrowthAndSkillMenuSynchronizationDoNotReduceEquipmentRaisedVitals()
    {
        CharacterSaveData hero = CreateVitalityFixture(out CharacterData data);
        hero.HP = 140;
        hero.AP = 27;

        CharacterGrowthService.EnsureInitialized(hero, data);
        SkillTreeProgressionService.Synchronize(hero, data);
        CharacterStatsProjectionService.ResolveFromSave(hero, data);

        Assert.That(hero.MaxHP, Is.EqualTo(100), "Saved maxima remain growth base stats.");
        Assert.That(hero.MaxAP, Is.EqualTo(20));
        Assert.That(hero.HP, Is.EqualTo(140));
        Assert.That(hero.AP, Is.EqualTo(27));
    }

    [Test]
    public void GrowthInvestmentAndRefundPreserveDeficitsIncludingEquipment()
    {
        CharacterSaveData hero = CreateVitalityFixture(out CharacterData data);
        hero.Level = 3;
        hero.HP = 130;
        hero.AP = 25;

        Assert.That(CharacterGrowthService.TryInvest(hero, data, GrowthStat.Vitality).Succeeded, Is.True);
        Assert.That(CharacterGrowthService.TryInvest(hero, data, GrowthStat.ActionPoints).Succeeded, Is.True);
        Assert.That(hero.HP, Is.EqualTo(140));
        Assert.That(hero.AP, Is.EqualTo(26));

        Assert.That(CharacterGrowthService.TryRefund(hero, data, GrowthStat.Vitality), Is.True);
        Assert.That(CharacterGrowthService.TryRefund(hero, data, GrowthStat.ActionPoints), Is.True);
        Assert.That(hero.HP, Is.EqualTo(130));
        Assert.That(hero.AP, Is.EqualTo(25));
    }

    [Test]
    public void OverworldDamageUsesEquipmentRaisedMaximum()
    {
        CharacterSaveData hero = CreateVitalityFixture(out _);
        hero.HP = 150;

        bool applied = _global.TryApplyOverworldPartyDamage(10, out _, out int previous, out int current);

        Assert.That(applied, Is.True);
        Assert.That(previous, Is.EqualTo(150));
        Assert.That(current, Is.EqualTo(140));
        Assert.That(hero.HP, Is.EqualTo(140));
    }

    [Test]
    public void RestoreEvaluationIsReadOnlyAndUsesEquipmentCaps()
    {
        Assert.That(_global.EvaluatePartyVitalsRestore(false, false).Status,
            Is.EqualTo(PartyVitalsRestoreStatus.InvalidRequest));
        Assert.That(_global.EvaluatePartyVitalsRestore(true, true).Status,
            Is.EqualTo(PartyVitalsRestoreStatus.PartyMissing));
        CharacterSaveData hero = CreateVitalityFixture(out _);
        hero.HP = 140;
        hero.AP = 27;
        int slotCount = hero.EquippedEquipmentIDs.Count;

        PartyVitalsRestoreEvaluation evaluation = _global.EvaluatePartyVitalsRestore(true, true);

        Assert.That(evaluation.Status, Is.EqualTo(PartyVitalsRestoreStatus.Ready));
        Assert.That(evaluation.RecoverableMemberCount, Is.EqualTo(1));
        Assert.That(hero.HP, Is.EqualTo(140));
        Assert.That(hero.AP, Is.EqualTo(27));
        Assert.That(hero.EquippedEquipmentIDs.Count, Is.EqualTo(slotCount));

        hero.HP = 150;
        hero.AP = 30;
        Assert.That(_global.EvaluatePartyVitalsRestore(true, true).Status,
            Is.EqualTo(PartyVitalsRestoreStatus.AlreadyFull));
    }

    [Test]
    public void EquipmentCandidatesExcludeCopiesUsedByOtherMembersButKeepCurrentSlot()
    {
        EquipmentData current = Equipment("equip.a", EquipmentSlot.Weapon, 1);
        EquipmentData occupied = Equipment("equip.b", EquipmentSlot.Weapon, 2);
        EquipmentData available = Equipment("equip.c", EquipmentSlot.Weapon, 3);
        SetEquipmentDatabase(current, occupied, available);
        CharacterSaveData hero = Character("hero");
        CharacterSaveData ally = Character("ally");
        _global.Party.Add(hero);
        _global.Party.Add(ally);
        _global.AddEquipmentAndGetAddedAmount(current.ItemID);
        _global.AddEquipmentAndGetAddedAmount(occupied.ItemID);
        _global.AddEquipmentAndGetAddedAmount(available.ItemID);
        Assert.That(EquipmentLoadoutService.TryEquip(_global, hero, EquipmentSlot.Weapon, current).Succeeded, Is.True);
        Assert.That(EquipmentLoadoutService.TryEquip(_global, ally, EquipmentSlot.Weapon, occupied).Succeeded, Is.True);

        Assert.That(EquipmentLoadoutService.GetAvailableCount(_global, hero, EquipmentSlot.Weapon, current.ItemID), Is.EqualTo(1));
        Assert.That(EquipmentLoadoutService.GetAvailableCount(_global, hero, EquipmentSlot.Weapon, occupied.ItemID), Is.Zero);
        Assert.That(EquipmentLoadoutService.GetAvailableCount(_global, hero, EquipmentSlot.Weapon, available.ItemID), Is.EqualTo(1));
    }

    [Test]
    public void PowerProgressionUnlocksDefaultAndLevelQualifiedSkills()
    {
        SkillData basic = Skill("skill.basic", "Basic");
        SkillData advanced = Skill("skill.advanced", "Advanced");
        CharacterData data = ScriptableObject.CreateInstance<CharacterData>();
        _created.Add(data);
        data.DefaultSkills.Add(basic);
        data.PowerUnlocks.Add(new CharacterPowerUnlock
        {
            RequiredLevel = 3,
            Skill = advanced
        });
        CharacterSaveData hero = Character("hero");
        hero.Level = 2;
        hero.EquippedSkillIDs.Add(basic.SkillID);

        Assert.That(PowerProgressionService.SynchronizeUnlockedSkills(hero, data), Is.True);
        Assert.That(hero.UnlockedSkillIDs, Is.EqualTo(new[] { basic.SkillID }));
        List<CharacterPowerView> levelTwo = PowerProgressionService.BuildViews(hero, data);
        Assert.That(levelTwo, Has.Count.EqualTo(2));
        Assert.That(levelTwo[0].Unlocked, Is.True);
        Assert.That(levelTwo[0].Equipped, Is.True);
        Assert.That(levelTwo[1].Unlocked, Is.False);
        Assert.That(levelTwo[1].RequiredLevel, Is.EqualTo(3));

        hero.Level = 3;
        Assert.That(PowerProgressionService.SynchronizeUnlockedSkills(hero, data), Is.True);
        Assert.That(hero.UnlockedSkillIDs, Does.Contain(advanced.SkillID));
    }

    private EquipmentData Equipment(string id, EquipmentSlot slot, int attack)
    {
        EquipmentData equipment = ScriptableObject.CreateInstance<EquipmentData>();
        equipment.ItemID = id;
        equipment.ItemName = id;
        equipment.Slot = slot;
        equipment.StatBonuses.ATK = attack;
        _created.Add(equipment);
        return equipment;
    }

    private CharacterSaveData CreateVitalityFixture(out CharacterData data)
    {
        EquipmentData equipment = Equipment("equip.vitals", EquipmentSlot.Weapon, 0);
        equipment.StatBonuses.MaxHP = 50;
        equipment.StatBonuses.MaxAP = 10;
        SetEquipmentDatabase(equipment);
        data = CreateCharacterData("hero");
        data.BaseStats = new StatBlock { MaxHP = 100, MaxAP = 20, ATK = 10, DEF = 5, SPD = 7 };
        typeof(CharacterDatabase).GetField("_cache", BindingFlags.Static | BindingFlags.NonPublic)
            .SetValue(null, new Dictionary<string, CharacterData>(StringComparer.Ordinal) { { "hero", data } });
        CharacterSaveData hero = Character("hero");
        hero.MaxHP = hero.HP = 100;
        hero.MaxAP = hero.AP = 20;
        hero.EquippedEquipmentIDs = new List<string> { equipment.ItemID };
        _global.Party.Add(hero);
        _global.AddEquipmentAndGetAddedAmount(equipment.ItemID);
        return hero;
    }

    private CharacterData CreateCharacterData(string id)
    {
        CharacterData data = ScriptableObject.CreateInstance<CharacterData>();
        data.CharacterID = id;
        _created.Add(data);
        return data;
    }

    private SkillData Skill(string id, string name)
    {
        SkillData skill = ScriptableObject.CreateInstance<SkillData>();
        skill.SkillID = id;
        skill.SkillName = name;
        _created.Add(skill);
        return skill;
    }

    private static CharacterSaveData Character(string id)
    {
        return new CharacterSaveData
        {
            CharacterDataID = id,
            CharacterID = id,
            Level = 1,
            HP = 10,
            MaxHP = 10
        };
    }

    private static void SetEquipmentDatabase(params EquipmentData[] equipment)
    {
        var cache = new Dictionary<string, EquipmentData>(StringComparer.Ordinal);
        for (int i = 0; i < equipment.Length; i++)
            cache[equipment[i].ItemID] = equipment[i];
        FieldInfo field = typeof(EquipmentDatabase).GetField(
            "_cache",
            BindingFlags.Static | BindingFlags.NonPublic);
        field.SetValue(null, cache);
    }

    private static void SetGlobalInstance(GlobalDataManager value)
    {
        PropertyInfo property = typeof(GlobalDataManager).GetProperty(
            "Instance",
            BindingFlags.Public | BindingFlags.Static);
        property.GetSetMethod(true).Invoke(null, new object[] { value });
    }
}
