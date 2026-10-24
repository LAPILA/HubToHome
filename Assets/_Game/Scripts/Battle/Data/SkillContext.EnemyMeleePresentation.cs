using System.Collections;

public partial class SkillContext
{
    private BattleEnemyMeleePresentationScope _enemyMeleePresentation;

    public BattleEnemyMeleePresentationScope BeginEnemyMeleePresentation(
        EnemyCharacter actor, PlayerCharacter target, Action_DefenseWindow block)
    {
        CancelEnemyMeleePresentation();
        var path = new BattleEnemyMeleeDashPath(actor.transform.position, target.transform.position,
            block.TimeWindow, block.MeleeDashDuration, block.MeleePassDistance, block.MeleeExitDuration);
        BattleEnemyMeleePresentationScope scope = null;
        scope = new BattleEnemyMeleePresentationScope(actor, target, path,
            () => CanContinueExecution, () => ReferenceEquals(_enemyMeleePresentation, scope));
        _enemyMeleePresentation = scope;
        return scope;
    }

    public void CancelEnemyMeleePresentation()
    {
        // 참조를 비우기 전에 해제해야 현재 소유자만 원위치를 복구할 수 있습니다.
        _enemyMeleePresentation?.Dispose();
        _enemyMeleePresentation = null;
    }

    private IEnumerator WaitForEnemyMeleePresentation()
    {
        BattleEnemyMeleePresentationScope scope = _enemyMeleePresentation;
        if (scope == null) yield break;
        try { yield return scope.Finish(); }
        finally
        {
            scope.Dispose();
            if (ReferenceEquals(_enemyMeleePresentation, scope)) _enemyMeleePresentation = null;
        }
    }
}
