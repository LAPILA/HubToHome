using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 플레이어 캐릭터 클래스. 
/// CharacterBase를 상속하며 레벨링, 장비, 스킬 시스템을 관리합니다.
/// </summary>
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

    // ── 초기화 ────────────────────────────────────────────────
    protected override void Awake()
    {
        base.Awake();
        RecalculateStats();
        CurrentHP = MaxHP;
        CurrentMP = MaxMP;
    }

    // ── 스탯 재계산 로직 ───────────────────────────────────────────
    public void RecalculateStats()
    {
        // 장비 보너스 합산
        int bonusATK = GetEquipBonus(e => e?.BonusATK ?? 0);
        int bonusDEF = GetEquipBonus(e => e?.BonusDEF ?? 0);
        int bonusSPD = GetEquipBonus(e => e?.BonusSPD ?? 0);
        int bonusHP  = GetEquipBonus(e => e?.BonusMaxHP ?? 0);
        int bonusMP  = GetEquipBonus(e => e?.BonusMaxMP ?? 0);

        // 기본값(Base) + 보너스 적용
        // TODO: 나중에 Level별 StatGrowthCurve SO를 참조하도록 확장 가능
        ATK   = 10 + bonusATK;
        DEF   = 5  + bonusDEF;
        SPD   = 10 + bonusSPD;
        MaxHP = 100 + bonusHP;
        MaxMP = 100 + bonusMP;

        // 현재 수치가 최대치를 넘지 않도록 보정
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

    // ── 장비 시스템 ─────────────────────────────────────────────
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

        RecalculateStats();

        if (!string.IsNullOrEmpty(equipment.EquipReactionDialogueID))
        {
            Debug.Log($"[Equip] {CharacterID} 반응: {equipment.EquipReactionDialogueID}");
        }
    }

    // ── 레벨업 시스템 ─────────────────────────────────────────────
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
        RecalculateStats(); // 레벨업 시 스탯 갱신
        
        // 레벨업 시 HP/MP 전 회복 (전형적인 RPG 룰)
        CurrentHP = MaxHP;
        CurrentMP = MaxMP;
        
        Debug.Log($"[LevelUp] {CharacterID} 레벨 {Level} 달성!");
    }

    // ── 전투 이벤트 핸들러 ──────────────────────────────────────────
    protected override void OnDamageTaken(int damage)
    {
        base.OnDamageTaken(damage);
        Debug.Log($"[Player] {CharacterID} 피격: {damage} 데미지 (잔여 HP: {CurrentHP})");
    }

    protected override void OnDie()
    {
        Debug.Log($"[Player] {CharacterID} 사망.");
        // BattleManager.Instance.OnPlayerDefeated(this);
    }
}