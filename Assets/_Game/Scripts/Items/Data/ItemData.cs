using UnityEngine;
using Sirenix.OdinInspector;

// ── [모든 데이터가 공유하는 공용 Enum] ──
public enum EffectActionType { None = 0, Heal = 1, Damage = 2, ApplyStatus = 3, Cleanse = 4, Revive = 5 }
public enum TargetStatType   { None = 0, HP = 1, AP = 2 }
public enum ValueCalcType    { Flat, Percentage, Full } 
public enum ItemType         { Consumable, KeyItem, Equipment }
public enum TargetAreaType   { AllyOnly, EnemyOnly, Both, AoEAll }

[CreateAssetMenu(fileName = "NewItem", menuName = "Hub To Home/아이템/아이템 데이터")]
public class ItemData : ScriptableObject
{
    [BoxGroup("Identity"), HideLabel, PreviewField(50, ObjectFieldAlignment.Left)]
    public Sprite Icon;

    [BoxGroup("Identity")] public string ItemID = "item_001";
    [BoxGroup("Identity")] public string ItemName = "Item";
    [BoxGroup("Identity"), LabelText("이름 번역 키")] public string NameLocalizationKey;
    [BoxGroup("Identity"), LabelText("설명 번역 키")] public string DescriptionLocalizationKey;
    [BoxGroup("Identity"), TextArea(2, 4)] public string Description = "";

    public string LocalizedName => LocalizationManager.Localize(NameLocalizationKey, ItemName);
    public string LocalizedDescription => LocalizationManager.Localize(DescriptionLocalizationKey, Description);

    [BoxGroup("Type & Target")]
    [HorizontalGroup("Type & Target/Row1", LabelWidth = 70)] public ItemType Type = ItemType.Consumable;
    [HorizontalGroup("Type & Target/Row1", LabelWidth = 70)] public TargetAreaType TargetType = TargetAreaType.AllyOnly;
    [BoxGroup("Type & Target")] public bool IsAoE = false;

    [BoxGroup("Type & Target"), ShowIf("@Type == ItemType.Equipment"), LabelText("장비 데이터")]
    [Tooltip("장비 상품/상자/드롭은 이 장비의 보관함으로 지급합니다. Item ID는 상품 ID이며 장비 ID와 달라도 됩니다.")]
    public EquipmentData Equipment;

    // ── 사용 가능 위치 ──
    [BoxGroup("Usability")] public bool UsableInOverworld = true;
    [BoxGroup("Usability")] public bool UsableInBattle = true;

    // ── 상점 및 경제 시스템 ──
    [BoxGroup("Economy")] public bool IsSellable = true;
    [BoxGroup("Economy"), ShowIf("IsSellable")] public int Price = 100;

    // ── [핵심: 모듈화된 효과 시스템] ──
    [BoxGroup("Main Effect")]
    public EffectActionType ActionType = EffectActionType.Heal;
    
    [BoxGroup("Main Effect"), ShowIf("@ActionType == EffectActionType.Heal || ActionType == EffectActionType.Damage || ActionType == EffectActionType.Revive")]
    [Tooltip("Heal/Damage 시 대상이 되는 스탯")]
    public TargetStatType TargetStat = TargetStatType.HP;
    
    [BoxGroup("Main Effect"), ShowIf("@ActionType == EffectActionType.Heal || ActionType == EffectActionType.Damage || ActionType == EffectActionType.Revive")]
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

    [BoxGroup("정화"), ShowIf("@ActionType == EffectActionType.Cleanse"), LabelText("해로운 상태 전체 정화")]
    public bool CleanseAllHarmful = true;

    [BoxGroup("정화"), ShowIf("@ActionType == EffectActionType.Cleanse && !CleanseAllHarmful"), LabelText("정화할 누적 상태")]
    public ElementalStatusKind CleanseElement;

    [BoxGroup("전투 누적 효과"), LabelText("추가 누적 효과"), InlineProperty, HideReferenceObjectPicker]
    [Tooltip("비우거나 효과 없음을 선택하면 기존 아이템 그대로입니다. 부여/소비 효과가 있는 아이템은 전투에서만 사용할 수 있습니다.")]
    public ElementalItemPayload ElementalPayload;

    public bool HasElementalPayload => ElementalPayload != null && ElementalPayload.IsConfigured;

    [BoxGroup("Stack")]
    [HorizontalGroup("Stack/Row1", LabelWidth = 80)] public bool IsStackable = true;
    [HorizontalGroup("Stack/Row1", LabelWidth = 80), ShowIf("IsStackable")] public int MaxStackSize = 99;

    public int StackLimit => IsStackable ? Mathf.Clamp(MaxStackSize, 1, 99) : 1;
}
