using UnityEngine;

/// <summary>
/// 플레이어 캐릭터. CharacterBase를 상속하며 장비, 경험치, 레벨업 로직을 추가합니다.
/// </summary>
public class PlayerCharacter : CharacterBase
{
    // ── 레벨 / 경험치 ─────────────────────────────────────────
    [Header("Level & EXP")]
    public int Level    = 1;
    public int EXP      = 0;
    public int EXPToNextLevel = 100;

    public const int MaxLevel = 99;

    // ── 장비 슬롯 (6개) ───────────────────────────────────────
    [Header("Equipment")]
    public EquipmentData WeaponSlot;
    public EquipmentData Accessory1Slot;
    public EquipmentData Accessory2Slot;
    public EquipmentData HeadSlot;
    public EquipmentData BodySlot;
    public EquipmentData ShoesSlot;

    // ── 캐릭터 고유 ID (대사 트리거용) ───────────────────────
    [Header("Identity")]
    public string CharacterID = "Player";

    // ── 초기화 ────────────────────────────────────────────────
    protected override void Awake()
    {
        base.Awake();
        
        RecalculateStats();
        if (CurrentHP <= 0)
        {
            CurrentHP = MaxHP;
        }
    }

    // ── 경험치 / 레벨업 ───────────────────────────────────────
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
        // TODO: StatGrowthCurve SO를 참조하여 스탯 증가 적용
        EXPToNextLevel = Mathf.RoundToInt(EXPToNextLevel * 1.2f);
        Debug.Log($"[PlayerCharacter] {CharacterID} leveled up to {Level}!");
    }

    // ── 장비 장착 ─────────────────────────────────────────────
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

        // 특정 캐릭터 고유 반응 대사 트리거
        if (!string.IsNullOrEmpty(equipment.EquipReactionDialogueID))
        {
            // DialogueManager.Instance.StartDialogue(equipment.EquipReactionDialogueID);
            Debug.Log($"[PlayerCharacter] Equip reaction: {equipment.EquipReactionDialogueID}");
        }
    }

    // ── 스탯 재계산 ───────────────────────────────────────────
    private void RecalculateStats()
    {
        // 기본 스탯 + 장비 보너스 합산
        // TODO: StatGrowthCurve SO 기반 기본 스탯 참조
        int bonusATK = GetEquipBonus(e => e?.BonusATK ?? 0);
        int bonusDEF = GetEquipBonus(e => e?.BonusDEF ?? 0);
        int bonusSPD = GetEquipBonus(e => e?.BonusSPD ?? 0);
        int bonusHP  = GetEquipBonus(e => e?.BonusMaxHP ?? 0);

        ATK    = 10 + bonusATK;
        DEF    = 5  + bonusDEF;
        SPD    = 10 + bonusSPD;
        MaxHP  = 100 + bonusHP;
        if (CurrentHP > MaxHP)
        {
            CurrentHP = MaxHP;
        }
    }

    private int GetEquipBonus(System.Func<EquipmentData, int> selector)
    {
        return selector(WeaponSlot)     +
               selector(Accessory1Slot) +
               selector(Accessory2Slot) +
               selector(HeadSlot)       +
               selector(BodySlot)       +
               selector(ShoesSlot);
    }

    // ── 사망 처리 ─────────────────────────────────────────────
    protected override void OnDie()
    {
        Debug.Log($"[PlayerCharacter] {CharacterID} has died.");
        // TODO: 전투 패배 처리 → BattleManager에 통보
    }

    protected override void OnDamageTaken(int damage)
    {
        // TODO: Hurt 애니메이션 트리거
        Debug.Log($"[PlayerCharacter] {CharacterID} took {damage} damage. HP: {CurrentHP}/{MaxHP}");
    }
}
