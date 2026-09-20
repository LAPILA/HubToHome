using System;

public enum ShopServiceStatus
{
    Succeeded,
    InvalidRequest,
    UnsupportedStore,
    SessionClosed,
    PartyMissing,
    Blocked,
    AlreadyFull,
    InsufficientMoney,
    RecoveryFailed,
    StoreException,
    RefundFailed
}

public readonly struct ShopServiceResult
{
    public ShopServiceResult(ShopServiceStatus status, string message = "", int recoveredMembers = 0, int price = 0)
    {
        Status = status;
        Message = message ?? string.Empty;
        RecoveredMembers = recoveredMembers;
        Price = price;
    }

    public ShopServiceStatus Status { get; }
    public string Message { get; }
    public int RecoveredMembers { get; }
    public int Price { get; }
    public bool Succeeded => Status == ShopServiceStatus.Succeeded;
}

/// <summary>이용 가능 여부 확인 → 정확한 차감 → 회복. 미적용/예외 때는 차감액을 환불합니다.</summary>
public static class ShopServiceTransactionService
{
    public static ShopServiceResult TryUse(IShopTransactionStore store, ShopServiceEntry service)
    {
        if (service == null || !service.TryValidate(out _))
            return new ShopServiceResult(ShopServiceStatus.InvalidRequest, "회복 서비스 설정이 올바르지 않습니다.");
        if (!(store is IShopRecoveryStore recovery))
            return new ShopServiceResult(ShopServiceStatus.UnsupportedStore, "파티 회복 저장소가 준비되지 않았습니다.");

        bool paid = false;
        try
        {
            PartyVitalsRestoreEvaluation evaluation = recovery.EvaluatePartyVitalsRestore(service.RestoreHp, service.RestoreAp);
            if (evaluation.Status != PartyVitalsRestoreStatus.Ready)
                return FromEvaluation(evaluation.Status);

            if (service.Price > 0)
            {
                if (!store.TrySpendMoneyExact(service.Price))
                    return new ShopServiceResult(ShopServiceStatus.InsufficientMoney, "돈이 부족하거나 결제할 수 없습니다.");
                paid = true;
            }

            int changed = recovery.RestorePartyVitals(service.RestoreHp, service.RestoreAp);
            if (changed > 0)
                return new ShopServiceResult(ShopServiceStatus.Succeeded, recoveredMembers: changed, price: service.Price);

            ShopServiceResult failure = FromEvaluation(
                recovery.EvaluatePartyVitalsRestore(service.RestoreHp, service.RestoreAp).Status);
            return paid ? Refund(store, service.Price, failure) : failure;
        }
        catch (Exception exception)
        {
            var failure = new ShopServiceResult(ShopServiceStatus.StoreException, "회복 처리에 실패했습니다: " + exception.Message);
            return paid ? Refund(store, service.Price, failure) : failure;
        }
    }

    private static ShopServiceResult FromEvaluation(PartyVitalsRestoreStatus status)
    {
        switch (status)
        {
            case PartyVitalsRestoreStatus.PartyMissing:
                return new ShopServiceResult(ShopServiceStatus.PartyMissing, "회복할 파티원이 없습니다.");
            case PartyVitalsRestoreStatus.Blocked:
                return new ShopServiceResult(ShopServiceStatus.Blocked, "전투 중에는 회복 서비스를 이용할 수 없습니다.");
            case PartyVitalsRestoreStatus.AlreadyFull:
                return new ShopServiceResult(ShopServiceStatus.AlreadyFull, "파티원이 이미 모두 회복되어 있습니다.");
            case PartyVitalsRestoreStatus.InvalidRequest:
                return new ShopServiceResult(ShopServiceStatus.InvalidRequest, "회복할 자원이 설정되지 않았습니다.");
            default:
                return new ShopServiceResult(ShopServiceStatus.RecoveryFailed, "파티 회복을 적용하지 못했습니다.");
        }
    }

    private static ShopServiceResult Refund(IShopTransactionStore store, int price, ShopServiceResult failure)
    {
        try
        {
            if (store.TryRefundMoneyExact(price))
                return failure;
        }
        catch
        {
            // 환불 실패를 원래 회복 실패와 구분해 UI와 호출자가 확인할 수 있게 합니다.
        }

        return new ShopServiceResult(ShopServiceStatus.RefundFailed,
            failure.Message + $" 결제한 {price}G의 환불에도 실패했습니다.", price: price);
    }
}
