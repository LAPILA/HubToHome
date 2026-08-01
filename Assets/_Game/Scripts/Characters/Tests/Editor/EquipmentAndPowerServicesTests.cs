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
            EquipmentLoadoutService.GetFlatBonus(hero, item => item.BonusATK),
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
        equipment.BonusATK = attack;
        _created.Add(equipment);
        return equipment;
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