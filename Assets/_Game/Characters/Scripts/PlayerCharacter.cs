using System.Collections.Generic;
using UnityEngine;

public class PlayerCharacter : CharacterBase
{
    [Header("Level & EXP")]
    public int Level = 1;
    public int EXP = 0;
    public int EXPToNextLevel = 100;
    public const int MaxLevel = 99;

    [Header("Equipment Slots")]
    public EquipmentData WeaponSlot;
    public EquipmentData Accessory1Slot;
    public EquipmentData Accessory2Slot;
    public EquipmentData HeadSlot;
    public EquipmentData BodySlot;
    public EquipmentData ShoesSlot;

    [Header("Skills")]
    public List<SkillData> Skills = new List<SkillData>();

    [Header("Identity")]
    public string CharacterID = "Player";

    protected override void Awake()
    {
        base.Awake();
        RecalculateStats();
        // 🚨 Base 스탯 확정 후 현재 체력/마나 채우기
        CurrentHP = MaxHP;
        CurrentMP = MaxMP;
    }

    // ── 스탯 아키텍처 (안전 보장) ───────────────────────────────────────────
    public void RecalculateStats()
    {
        // 1. 장비 보너스 산출
        int bATK = GetEquipBonus(e => e?.BonusATK ?? 0);
        int bDEF = GetEquipBonus(e => e?.BonusDEF ?? 0);
        int bSPD = GetEquipBonus(e => e?.BonusSPD ?? 0);
        int bHP  = GetEquipBonus(e => e?.BonusMaxHP ?? 0);
        int bMP  = GetEquipBonus(e => e?.BonusMaxMP ?? 0);

        // 2. 기본값 + 장비 합산
        ATK   = 10 + bATK;
        DEF   = 5  + bDEF;
        SPD   = 10 + bSPD;
        MaxHP = 100 + bHP;
        MaxMP = 100 + bMP;

        // 🚨 3. 덮어쓰기 방어: 현재 걸려있는 디버프/버프가 있다면 그만큼 다시 보정해줍니다.
        // (빙결이나 가속 등은 Apply 될 때 스탯을 조작하므로, 재계산 시 보정값을 다시 적용해줘야 무결성이 유지됩니다.)
        foreach (var effect in _activeEffects)
        {
            if (effect is FreezeEffect freeze) SPD -= (10 * freeze.Stacks);
            if (effect is SpeedUpEffect speed) SPD += (50 * speed.Stacks);
        }

        // 4. 오버플로우 방지
        CurrentHP = Mathf.Clamp(CurrentHP, 0, MaxHP);
        CurrentMP = Mathf.Clamp(CurrentMP, 0, MaxMP);
    }

    private int GetEquipBonus(System.Func<EquipmentData, int> selector)
    {
        return selector(WeaponSlot)      +
               selector(Accessory1Slot)  +
               selector(Accessory2Slot)  +
               selector(HeadSlot)        +
               selector(BodySlot)        +
               selector(ShoesSlot);
    }

    public void Equip(EquipmentData equipment)
    {
        if (equipment == null) return;

        switch (equipment.Slot)
        {
            case EquipmentSlot.Weapon:     WeaponSlot     = equipment; break;
            case EquipmentSlot.Accessory1: Accessory1Slot = equipment; break;
            case EquipmentSlot.Accessory2: Accessory2Slot = equipment; break;
            case EquipmentSlot.Head:       HeadSlot       = equipment; break;
            case EquipmentSlot.Body:       BodySlot       = equipment; break;
            case EquipmentSlot.Shoes:      ShoesSlot      = equipment; break;
        }

        RecalculateStats(); // 장비 착용 시 스탯 재계산

        if (!string.IsNullOrEmpty(equipment.EquipReactionDialogueID))
            Debug.Log($"[Equip] {CharacterID}: {equipment.EquipReactionDialogueID}");
    }

    public void GainEXP(int amount)
    {
        if (Level >= MaxLevel) return;

        EXP += amount;
        while (EXP >= EXPToNextLevel && Level < MaxLevel)
        {
            EXP -= EXPToNextLevel;
            LevelUp();
        }
    }

    private void LevelUp()
    {
        Level++;
        EXPToNextLevel = Mathf.RoundToInt(EXPToNextLevel * 1.2f);
        RecalculateStats();
        
        CurrentHP = MaxHP;
        CurrentMP = MaxMP;
    }

    protected override void OnDamageTaken(int damage)
    {
        base.OnDamageTaken(damage); // CharacterBase의 로직 수행
        // 플레이어 전용 추가 로직 (화면 흔들림 등)
    }

    protected override void OnDie()
    {
        Debug.Log($"<color=red>[Player] {CharacterID} 사망.</color>");
    }
}