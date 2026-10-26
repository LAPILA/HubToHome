using System.Collections;
using UnityEngine;

public partial class SkillContext
{
    private BattleEnemyMeleePresentationScope _enemyMeleePresentation;
    public bool DeferDefenseReactionCompletion;
    // ExecuteBlock이 전투 피해 타임라인일 때만 설정합니다. 이야기용 순수 애니메이션/대기는 보존합니다.
    public bool IsContinuousEnemyAction;

    public BattleEnemyMeleePresentationScope BeginEnemyMeleePresentation(
        EnemyCharacter actor, PlayerCharacter target, Action_DefenseWindow block)
    {
        CancelEnemyMeleePresentation();
        // 일반 공격은 기존 명중 시간을 유지하고 접근 전체에 나눕니다. 난무만 이동 속도로 접점을 계산합니다.
        bool movementDriven = block.ContinuousMeleeMotion;
        PlayerController controller = target.GetComponent<PlayerController>();
        Vector3 targetPosition = controller != null ? controller.BattleDefenseAnchorPosition : target.transform.position;
        var path = new BattleEnemyMeleeDashPath(actor.transform.position, targetPosition,
            movementDriven ? block.MeleeDashDuration : block.TimeWindow,
            block.MeleeDashDuration, block.MeleePassDistance, block.MeleeExitDuration);
        CameraController.ScreenPassFramingScope framing = null;
        CameraController camera = CameraController.Instance;
        if (block.MeleePassExtent == EnemyMeleePassExtent.ScreenWide && ValidScreenSettings(block)
            && camera != null && camera.TryBeginScreenPassFraming(actor, out framing))
        {
            if (BattleScreenPassGeometry.TryCreate(framing.SafeWorld, framing.BodyOffset, framing.BodyHalfSize,
                block.ScreenEdgeMarginPixels * framing.WorldUnitsPerReferencePixel, framing.PixelsPerUnit,
                targetPosition.y, actor.transform.position.z, targetPosition.x, out var screen)
                && BattleEnemyMeleeDashPath.TryScreenWide(actor.transform.position, screen,
                    movementDriven ? block.ScreenPassDuration : block.TimeWindow,
                    block.ScreenPassDuration, block.ScreenEdgeApproachDuration, out var wide)) path = wide;
            else { framing.Dispose(); framing = null; }
        }
        float airHeight = FiniteRange(block.MeleeApproachHeight, 0f, 8f) ? block.MeleeApproachHeight : 0f;
        if (airHeight > 0f)
        {
            if (framing != null)
            {
                float headroom = framing.SafeWorld.yMax - framing.BodyOffset.y - framing.BodyHalfSize.y
                    - path.Entry.y - block.ScreenEdgeMarginPixels * framing.WorldUnitsPerReferencePixel;
                airHeight = Mathf.Min(airHeight, Mathf.Max(0f, headroom));
                airHeight = Mathf.Floor(airHeight * framing.PixelsPerUnit) / framing.PixelsPerUnit;
            }
            path = path.WithApproachHeight(airHeight, Mathf.Clamp(block.ScreenEdgeApproachDuration, .04f, .3f));
        }
        path = path.WithContinuousTiming(DefenseJudgementPolicy.BattleLateGrace + 1f / 30f,
            preserveImpactTime: !movementDriven);
        BattleEnemyMeleePresentationScope scope = null;
        scope = new BattleEnemyMeleePresentationScope(actor, target, path,
            () => CanContinueExecution, () => ReferenceEquals(_enemyMeleePresentation, scope), framing,
            passThroughOnGuard: true);
        _enemyMeleePresentation = scope;
        return scope;
    }

    private static bool ValidScreenSettings(Action_DefenseWindow block)
        => FiniteRange(block.ScreenPassDuration, 0.08f, 0.6f)
            && FiniteRange(block.ScreenEdgeApproachDuration, 0.04f, 0.3f)
            && FiniteRange(block.ScreenEdgeMarginPixels, 0f, 64f);

    private static bool FiniteRange(float value, float min, float max)
        => !float.IsNaN(value) && !float.IsInfinity(value) && value >= min && value <= max;

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
