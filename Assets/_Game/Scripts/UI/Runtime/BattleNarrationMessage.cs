public enum BattleNarrationPriority { Low, Normal, High, Critical }
public enum BattleNarrationStyle { Normal, Damage, Heal, Warning, System }

public readonly struct BattleNarrationMessage
{
    public readonly string Text;
    public readonly BattleNarrationPriority Priority;
    public readonly BattleNarrationStyle Style;
    public readonly float HoldOverride;
    public readonly bool RequiresConfirm;
    // 현재 시각적 한 줄을 즉시 표시합니다. 전체 기록을 한꺼번에 펼치지 않습니다.
    public readonly bool RevealImmediately;

    public BattleNarrationMessage(string text, BattleNarrationStyle style = BattleNarrationStyle.Normal, BattleNarrationPriority priority = BattleNarrationPriority.Normal, float holdOverride = -1f, bool requiresConfirm = false, bool revealImmediately = false)
    {
        Text = text;
        Style = style;
        Priority = priority;
        HoldOverride = holdOverride;
        RequiresConfirm = requiresConfirm;
        RevealImmediately = revealImmediately;
    }
}
