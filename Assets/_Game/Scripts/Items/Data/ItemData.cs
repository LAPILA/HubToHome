using UnityEngine;
using Sirenix.OdinInspector;

// ── [모든 데이터가 공유하는 공용 Enum] ──
public enum EffectActionType { None, Heal, Damage, ApplyStatus }
public enum TargetStatType   { None, HP, MP }
public enum ValueCalcType    { Flat, Percentage, Full } 
public enum ItemType         { Consumable, KeyItem, Equipment }
public enum TargetAreaType   { AllyOnly, EnemyOnly, Both, AoEAll }

[CreateAssetMenu(fileName = "NewItem", menuName = "HubToHome/ItemData")]
public class ItemData : ScriptableObject
{
    [BoxGroup("Identity"), HideLabel, PreviewField(50, ObjectFieldAlignment.Left)]
    public Sprite Icon;

    [BoxGroup("Identity")] public string ItemID = "item_001";
    [BoxGroup("Identity")] public string ItemName = "Item";
    [BoxGroup("Identity"), TextArea(2, 4)] public string Description = "";

    [BoxGroup("Type & Target")]
    [HorizontalGroup("Type & Target/Row1", LabelWidth = 70)] public ItemType Type = ItemType.Consumable;
    [HorizontalGroup("Type & Target/Row1", LabelWidth = 70)] public TargetAreaType TargetType = TargetAreaType.AllyOnly;
    [BoxGroup("Type & Target")] public bool IsAoE = false;

    // ── 사용 가능 위치 ──
    [BoxGroup("Usability")] public bool UsableInOverworld = true;
    [BoxGroup("Usability")] public bool UsableInBattle = true;

    // ── 상점 및 경제 시스템 ──
    [BoxGroup("Economy")] public bool IsSellable = true;
    [BoxGroup("Economy"), ShowIf("IsSellable")] public int Price = 100;

    // ── [핵심: 모듈화된 효과 시스템] ──
    [BoxGroup("Main Effect")]
    public EffectActionType ActionType = EffectActionType.Heal;
    
    [BoxGroup("Main Effect"), ShowIf("@ActionType == EffectActionType.Heal || ActionType == EffectActionType.Damage")]
    [Tooltip("Heal/Damage 시 대상이 되는 스탯")]
    public TargetStatType TargetStat = TargetStatType.HP;
    
    [BoxGroup("Main Effect"), ShowIf("@ActionType == EffectActionType.Heal || ActionType == EffectActionType.Damage")]
    [Tooltip("수치 계산 방식 (고정값 / 퍼센트 / 100% 꽉채움)")]
    public ValueCalcType CalcType = ValueCalcType.Flat;
    [BoxGroup("Main Effect"), ShowIf("@CalcType == ValueCalcType.Flat || CalcType == ValueCalcType.Percentage")]
    [Tooltip("계산에 사용될 수치 (Full일 경우 무시됨, 퍼센트면 50은 50%)")]
    public int EffectValue = 0;

    [BoxGroup("Status Effect (디버프/버프)")]
    [Tooltip("ApplyStatus일 경우 부여할 상태이상")]
    [ValueDropdown("@StatusEffectFactory.KnownIds")]
    [ValidateInput("@string.IsNullOrEmpty(StatusEffectID) || StatusEffectFactory.IsKnown(StatusEffectID)", "등록되지 않은 상태이상 ID입니다.")]
    public string StatusEffectID = "";
    
    [BoxGroup("Status Effect (디버프/버프)")]
    [Tooltip("상태이상 지속 턴 수")]
    public int StatusDurationTurns = 0;

    [BoxGroup("Stack")]
    [HorizontalGroup("Stack/Row1", LabelWidth = 80)] public bool IsStackable = true;
    [HorizontalGroup("Stack/Row1", LabelWidth = 80), ShowIf("IsStackable")] public int MaxStackSize = 99;
}