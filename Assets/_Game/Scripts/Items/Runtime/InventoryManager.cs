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

    public bool TryUseItemOnPartyMember(string itemId, CharacterSaveData target, out string error)
        => TryUsePartyItem(itemId, target, out error);

    public static bool TryUsePartyItem(string itemId, CharacterSaveData target, out string error)
    {
        GlobalDataManager global = GlobalDataManager.Instance;
        ItemData item = Instance != null ? Instance.GetItemData(itemId) : ItemDatabase.FindById(itemId);
        if (global == null || target == null || !global.Party.Contains(target))
        {
            error = "파티원을 찾을 수 없습니다.";
            return false;
        }
        if (BattleManager.Instance != null && BattleManager.Instance.IsBattleSessionActive)
        {
            error = "전투 아이템 메뉴를 사용하세요.";
            return false;
        }

        var targets = new List<CharacterSaveData>();
        if (item != null && (item.IsAoE || item.TargetType == TargetAreaType.AoEAll))
        {
            for (int i = 0; i < global.Party.Count; i++)
                if (ItemEffectService.CanApplyToSavedMember(item, global.Party[i], out _)) targets.Add(global.Party[i]);
        }
        else if (ItemEffectService.CanApplyToSavedMember(item, target, out _)) targets.Add(target);
        if (targets.Count == 0)
        {
            ItemEffectService.CanApplyToSavedMember(item, target, out error);
            return false;
        }
        if (!global.RemoveItem(item.ItemID, 1))
        {
            error = "보유 수량이 부족합니다.";
            return false;
        }

        int[] hp = new int[targets.Count];
        int[] ap = new int[targets.Count];
        for (int i = 0; i < targets.Count; i++) { hp[i] = targets[i].HP; ap[i] = targets[i].AP; }
        error = string.Empty;
        bool succeeded = false;
        try
        {
            succeeded = true;
            for (int i = 0; i < targets.Count; i++)
                if (!ItemEffectService.TryApplyToSavedMember(item, targets[i], out error)) { succeeded = false; break; }
        }
        catch (System.Exception exception) { error = exception.Message; succeeded = false; }
        if (!succeeded)
        {
            for (int i = 0; i < targets.Count; i++) { targets[i].HP = hp[i]; targets[i].AP = ap[i]; }
            if (global.AddItemAndGetAddedAmount(item.ItemID, 1) != 1)
                Debug.LogError($"[Inventory] Failed to restore reserved item: {item.ItemID}");
            return false;
        }

        // 사용 시에만 동기화합니다. 비생성 동료를 위해 임시 GameObject를 만들지 않습니다.
        SynchronizePartyItemTargets(targets);
        return true;
    }

    private static void SynchronizePartyItemTargets(List<CharacterSaveData> targets)
    {
        PlayerCharacter[] players;
        try { players = FindObjectsByType<PlayerCharacter>(FindObjectsInactive.Exclude, FindObjectsSortMode.None); }
        catch (System.Exception exception) { Debug.LogException(exception); return; }
        for (int i = 0; i < players.Length; i++)
        {
            if (players[i] == null || players[i].CharacterData == null) continue;
            for (int j = 0; j < targets.Count; j++)
            {
                // DB ID is shared by sample clones; only the bound save owns these vitals.
                if (!players[i].IsBoundToSaveData(targets[j])) continue;
                try { players[i].SynchronizePersistentVitals(targets[j]); }
                catch (System.Exception exception) { Debug.LogException(exception, players[i]); }
                break;
            }
        }
    }

    public bool TryUseItemOnNPC(string itemID, InteractableBase npc)
    {
        Debug.Log($"[Inventory] {npc.name} 앞에서 {itemID} 사용 시도!");
        return false;
    }
}
