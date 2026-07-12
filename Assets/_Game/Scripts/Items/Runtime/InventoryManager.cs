using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 아이템 데이터베이스 로드 및 아이템 사용(효과 적용)을 담당하는 매니저.
/// </summary>
public class InventoryManager : MonoBehaviour
{
    public static InventoryManager Instance { get; private set; }

    [Header("Item Database")]
    [Tooltip("게임 내 모든 아이템 데이터를 배열에 연결해 둡니다.")]
    [SerializeField] private ItemData[] _itemDatabase;
    
    private readonly Dictionary<string, ItemData> _itemDict = new Dictionary<string, ItemData>();

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        // 빠른 검색을 위해 Dictionary로 캐싱
        foreach (var item in _itemDatabase)
        {
            if (item != null && !_itemDict.ContainsKey(item.ItemID))
                _itemDict.Add(item.ItemID, item);
        }
    }

    /// <summary>ID로 아이템 원본 데이터를 가져옵니다.</summary>
    public ItemData GetItemData(string itemID)
    {
        return _itemDict.TryGetValue(itemID, out var data) ? data : null;
    }

    // ── 🚨 아이템 사용 (소비) 로직 ──
    public bool UseItem(string itemID, CharacterBase target)
    {
        var itemData = GetItemData(itemID);
        if (itemData == null || itemData.Type != ItemType.Consumable) 
        {
            Debug.LogWarning($"[Inventory] 사용할 수 없는 아이템입니다: {itemID}");
            return false;
        }

        // 인벤토리에 아이템이 있는지 확인
        if (GlobalDataManager.Instance.GetItemCount(itemID) <= 0) 
        {
            Debug.LogWarning($"[Inventory] 소지하지 않은 아이템입니다: {itemID}");
            return false;
        }

        // 효과 적용 (Heal, Damage 등)
        ApplyItemEffect(itemData, target);

        // 사용 완료 후 소비
        GlobalDataManager.Instance.RemoveItem(itemID, 1);
        Debug.Log($"<color=green>[Inventory]</color> {itemData.ItemName}을(를) 사용했습니다.");
        return true;
    }

    private void ApplyItemEffect(ItemData item, CharacterBase target)
    {
        if (target == null || !target.IsAlive) return;

        // 1. 회복 및 데미지 로직
        if (item.ActionType == EffectActionType.Heal || item.ActionType == EffectActionType.Damage)
        {
            int calculatedValue = CalculateValue(item, target);

            if (item.ActionType == EffectActionType.Heal)
            {
                if (item.TargetStat == TargetStatType.HP) target.HealHP(calculatedValue);
                if (item.TargetStat == TargetStatType.MP) target.HealMP(calculatedValue);
            }
            else if (item.ActionType == EffectActionType.Damage)
            {
                // 아이템 데미지는 방어력을 무시하는 고정 데미지로 처리
                target.TakePureDamage(calculatedValue); 
            }
        }

        // 2. 상태이상 로직 (ApplyStatus)
        if (item.ActionType == EffectActionType.ApplyStatus && !string.IsNullOrEmpty(item.StatusEffectID))
        {
            if (!StatusEffectFactory.TryCreate(item.StatusEffectID, item.StatusDurationTurns, out StatusEffect effect))
            {
                Debug.LogWarning($"[Inventory] 등록되지 않은 상태이상 ID입니다: {item.StatusEffectID}");
                return;
            }

            target.AddEffect(effect);
        }
    }

    private int CalculateValue(ItemData item, CharacterBase target)
    {
        int maxValue = (item.TargetStat == TargetStatType.HP) ? target.MaxHP : target.MaxMP;

        switch (item.CalcType)
        {
            case ValueCalcType.Flat:
                return item.EffectValue;

            case ValueCalcType.Percentage:
                return Mathf.RoundToInt(maxValue * (item.EffectValue / 100f));

            case ValueCalcType.Full:
                return maxValue;

            default:
                return 0;
        }
    }

    // ── 오버월드 NPC 대상 아이템 사용 (키 아이템 건네주기 등) ──
    public bool TryUseItemOnNPC(string itemID, InteractableBase npc)
    {
        Debug.Log($"[Inventory] {npc.name} 앞에서 {itemID} 사용 시도!");
        return false;
    }
}