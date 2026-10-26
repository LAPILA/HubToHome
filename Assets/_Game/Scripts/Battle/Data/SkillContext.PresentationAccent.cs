/// <summary>다음 유효한 스킬 명중에 한 번만 쓰는 표시 예약입니다. 피해나 시간을 계산하지 않습니다.</summary>
public sealed class SkillAccentImpactRequest
{
    private Action_PresentationAccent _pending;

    public bool Queue(Action_PresentationAccent settings)
    {
        if (settings == null || settings.Mode != Action_PresentationAccent.AccentMode.Flash
            || !settings.HasValidSettings) return false;
        _pending = settings;
        return true;
    }

    public bool TryTake(DamageResult result, out Action_PresentationAccent settings)
    {
        settings = null;
        if (_pending == null || !result.Applied || result.FinalDamage <= 0) return false;
        settings = _pending;
        _pending = null; // 먼저 소비하므로 광역/연타/동기 상태 반응도 중복 표시하지 않습니다.
        return true;
    }

    public void Clear() => _pending = null;
}

public partial class SkillContext
{
    private readonly SkillAccentImpactRequest _impactFlashRequest = new();
    private BattleSkillAccentPresentation.Lease _impactFlashLease;
    private bool _presentationAccentCompleted;

    public bool QueueImpactFlash(Action_PresentationAccent settings)
        => CanContinueExecution && _impactFlashRequest.Queue(settings);

    public bool PlayConcurrentAccent(Action_PresentationAccent settings)
    {
        if (!CanContinueExecution || settings == null
            || settings.Mode != Action_PresentationAccent.AccentMode.Silhouette || !settings.HasValidSettings)
            return false;
        // 실루엣과 명중 점멸은 같은 표시 lease를 교체합니다. 이전 소유자가 새 효과를 지울 수 없습니다.
        _impactFlashLease.Dispose();
        _presentationAccentCompleted = false;
        _impactFlashLease = BattleSkillAccentPresentation.Begin(Actor, settings,
            () => _presentationAccentCompleted || CanContinueExecution);
        return true;
    }

    private void NotifyAccentImpact(DamageResult result)
    {
        if (!CanContinueExecution)
        {
            CancelPresentationAccent();
            return;
        }
        if (!_impactFlashRequest.TryTake(result, out Action_PresentationAccent settings)) return;
        _impactFlashLease.Dispose();
        _impactFlashLease = BattleSkillAccentPresentation.Begin(Actor, settings,
            () => _presentationAccentCompleted || CanContinueExecution);
    }

    // 정상 종료 직후의 명중도 한 프레임 이전에 지워지지 않게 짧은 표시 꼬리를 허용합니다.
    public void CompletePresentationAccent()
    {
        if (!CanContinueExecution) return;
        _impactFlashRequest.Clear();
        _presentationAccentCompleted = true;
    }

    public void CancelPresentationAccent()
    {
        _impactFlashRequest.Clear();
        if (!_presentationAccentCompleted) _impactFlashLease.Dispose();
        _impactFlashLease = default;
    }
}
