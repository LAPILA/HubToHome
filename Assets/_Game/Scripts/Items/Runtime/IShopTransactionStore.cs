public interface IShopTransactionStore
{
    int Money { get; }

    bool IsItemRegistered(ItemData item);
    int GetItemCount(string itemId);
    int GetAdditionalItemCapacity(ItemData item);

    bool TrySpendMoneyExact(int amount);
    bool TryRefundMoneyExact(int amount);
    bool TryAddItemExact(string itemId, int amount);
    bool TryRemoveItemExact(string itemId, int amount);

    bool TryGetFlag(string key, out int value);

    /// <summary>
    /// Applies the value atomically. False or an exception must leave the flag unchanged.
    /// </summary>
    bool TrySetFlag(string key, int value);
}

/// <summary>회복과 결제를 같은 저장소에 적용하는 상점 서비스 경계입니다.</summary>
public interface IShopRecoveryStore : IShopTransactionStore
{
    PartyVitalsRestoreEvaluation EvaluatePartyVitalsRestore(bool restoreHp, bool restoreAp);

    /// <summary>0 또는 예외면 자원을 변경하지 않아야 합니다. 표시 갱신 실패는 성공을 되돌리지 않습니다.</summary>
    int RestorePartyVitals(bool restoreHp, bool restoreAp);
}
