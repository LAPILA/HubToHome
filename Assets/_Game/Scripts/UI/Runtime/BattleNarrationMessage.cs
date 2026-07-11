public enum BattleNarrationPriority { Low, Normal, High, Critical }
public enum BattleNarrationStyle { Normal, Damage, Heal, Warning, System }

public readonly struct BattleNarrationMessage
{
    public readonly string Text;
    public readonly BattleNarrationPriority Priority;
    public readonly BattleNarrationStyle Style;
    public readonly float HoldOverride;
    public readonly bool RequiresConfirm;

    public BattleNarrationMessage(string text, BattleNarrationStyle style = BattleNarrationStyle.Normal, BattleNarrationPriority priority = BattleNarrationPriority.Normal, float holdOverride = -1f, bool requiresConfirm = false)
    {
        Text = text;
        Style = style;
        Priority = priority;
        HoldOverride = holdOverride;
        RequiresConfirm = requiresConfirm;
    }
}
