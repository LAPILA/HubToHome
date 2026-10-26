/// <summary>실제 완료된 아군 행동입니다. 원래 입력/대상/피해/아이템 소유권을 바꾸지 않습니다.</summary>
public readonly struct BattlePlayerActionResult
{
    public PlayerCharacter Actor { get; }
    public PlayerMenuAction Action { get; }
    public SkillData Skill { get; }
    public ItemData Item { get; }

    public BattlePlayerActionResult(PlayerCharacter actor, PlayerMenuAction action,
        SkillData skill = null, ItemData item = null)
    {
        Actor = actor;
        Action = action;
        Skill = skill;
        Item = item;
    }
}

/// <summary>기존 Turn QTE 호스트를 깨지 않는 선택적 완료/안전 종료 연결부입니다.</summary>
public interface IBattlePlayerActionCompletionHost
{
    void NotifyPlayerActionCompleted(BattlePlayerActionResult result);
    bool TryCompleteStoryWithdrawalAtBoundary();
    void CancelPendingStoryWithdrawal();
}
