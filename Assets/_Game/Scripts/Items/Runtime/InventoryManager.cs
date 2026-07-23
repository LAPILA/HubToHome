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
        if (_itemDatabase != null)
        {
            foreach (var item in _itemDatabase)
            {
                if (item == null || string.IsNullOrWhiteSpace(item.ItemID)) continue;
                string itemId = item.ItemID.Trim();
                if (!_itemDict.ContainsKey(itemId))
                    _itemDict.Add(itemId, item);
            }
        }
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    /// <summary>ID로 아이템 원본 데이터를 가져옵니다.</summary>
    public ItemData GetItemData(string itemID)
    {
        if (string.IsNullOrWhiteSpace(itemID)) return null;
        string normalizedId = itemID.Trim();
        if (_itemDict.TryGetValue(normalizedId, out ItemData localData))
            return localData;
        return ItemDatabase.FindById(normalizedId);
    }

    public bool UseItem(string itemID, CharacterBase target)
    {
        ItemData itemData = GetItemData(itemID);
        GlobalDataManager global = GlobalDataManager.Instance;
        if (itemData == null || global == null)
            return false;

        if (!ItemEffectService.CanApply(itemData, target, false, out string validationError))
        {
            Debug.LogWarning($"[Inventory] Item use failed: {validationError}");
            return false;
        }

        if (!global.RemoveItem(itemData.ItemID, 1))
        {
            Debug.LogWarning($"[Inventory] Item is not owned: {itemData.ItemID}");
            return false;
        }

        bool applied = false;
        string applyError = string.Empty;
        try
        {
            applied = ItemEffectService.TryApply(itemData, target, false, out applyError);
        }
        catch (System.Exception exception)
        {
            applyError = exception.ToString();
        }

        if (applied)
            return true;

        int restored = global.AddItemAndGetAddedAmount(itemData.ItemID, 1);
        if (restored != 1)
            Debug.LogError($"[Inventory] Failed to restore reserved item: {itemData.ItemID}");
        Debug.LogWarning($"[Inventory] Item use failed after reservation: {applyError}");
        return false;
    }

    // ── 오버월드 NPC 대상 아이템 사용 (키 아이템 건네주기 등) ──
    public bool TryUseItemOnNPC(string itemID, InteractableBase npc)
    {
        Debug.Log($"[Inventory] {npc.name} 앞에서 {itemID} 사용 시도!");
        return false;
    }
}