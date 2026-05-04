using UnityEngine;

// ── [모든 데이터가 공유하는 공용 Enum] ──
public enum EffectActionType { None, Heal, Damage, ApplyStatus }
public enum TargetStatType   { None, HP, MP }
public enum ValueCalcType    { Flat, Percentage, Full } 
public enum ItemType         { Consumable, KeyItem, Equipment }
public enum TargetAreaType   { AllyOnly, EnemyOnly, Both, AoEAll } // Item/Skill 공용 타겟팅

[CreateAssetMenu(fileName = "NewItem", menuName = "HubToHome/ItemData")]
public class ItemData : ScriptableObject
{
    [Header("Identity")]
    public string ItemID = "item_001";
    public string ItemName = "Item";
    public Sprite Icon;
    [TextArea] public string Description = "";

    [Header("Type & Target")]
    public ItemType Type = ItemType.Consumable;
    public TargetAreaType TargetType = TargetAreaType.AllyOnly;
    public bool IsAoE = false;

    // ── [핵심: 모듈화된 효과 시스템] ──
    [Header("Main Effect")]
    public EffectActionType ActionType = EffectActionType.Heal;
    
    [Tooltip("Heal/Damage 시 대상이 되는 스탯")]
    public TargetStatType TargetStat = TargetStatType.HP;
    
    [Tooltip("수치 계산 방식 (고정값 / 퍼센트 / 100% 꽉채움)")]
    public ValueCalcType CalcType = ValueCalcType.Flat;
    
    [Tooltip("계산에 사용될 수치 (Full일 경우 무시됨, 퍼센트면 50은 50%)")]
    public int EffectValue = 0;

    [Header("Status Effect (디버프/버프)")]
    [Tooltip("ApplyStatus일 경우 부여할 상태이상")]
    public StatusEffectType StatusEffect = StatusEffectType.None;
    
    [Tooltip("상태이상 지속 턴 수")]
    public int StatusDurationTurns = 0;

    [Header("Stack")]
    public bool IsStackable = true;
    public int MaxStackSize = 99;
}