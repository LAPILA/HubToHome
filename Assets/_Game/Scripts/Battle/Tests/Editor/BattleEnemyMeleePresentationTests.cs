using System.Collections.Generic;
using System.Reflection;
using DG.Tweening;
using NUnit.Framework;
using UnityEngine;

public sealed class BattleEnemyMeleePresentationTests
{
    [Test]
    public void DeferredImpact_SkipsRetiredPreparationWithoutCreatingTwoDefenseWindows()
    {
        var defense = new Action_DefenseWindow();
        var projectile = new Action_Projectile();
        Assert.That(SkillContext.HasSynchronizedImpact(defense, new SkillActionBlock[]
        {
            defense, new Action_Wait(), new Action_EnemyWindup(),
            new Action_PlayAnim { AnimTriggerName = "CrossCutReady" }, new Action_PresentationAccent(), projectile
        }), Is.True);
        Assert.That(SkillContext.HasSynchronizedImpact(defense, new SkillActionBlock[]
        {
            defense, new Action_Move { Destination = Action_Move.MoveDest.Center }, projectile
        }), Is.False, "실제 이동을 임의로 건너뛰지는 않습니다.");
    }

    private GameObject _enemyObject, _playerObject;
    private EnemyCharacter _enemy;
    private PlayerCharacter _player;
    private SkillContext _context;

    [Test]
    public void AssaultStage_InterpolatesTimingAndPreservesOrdinaryDefenseRules()
    {
        var combo = new Action_EnemyAssaultCombo();
        var stage = new EnemyAssaultStage { HitCount = 10 };
        combo.Stages.Add(stage);
        var defense = new Action_DefenseWindow();
        Assert.That(combo.HasValidSettings, Is.True);
        combo.ConfigureDefenseForHit(defense, stage, 9, .5f);
        Assert.That(defense.TimeWindow, Is.EqualTo(.42f));
        Assert.That(defense.ScreenPassDuration, Is.EqualTo(.18f));
        Assert.That(defense.ScreenEdgeApproachDuration, Is.EqualTo(.05f));
        Assert.That(defense.Requirement, Is.EqualTo(DefenseRequirement.Any));
        Assert.That(defense.MeleePassThroughOnGuard, Is.True);
        Assert.That(defense.MeleeApproachHeight, Is.Zero);
        stage.Kind = EnemyAssaultStageKind.Mixed;
        stage.MixedAerialChance = 1f;
        Assert.That(stage.IsAerialHit(1f), Is.True);
        stage.MixedAerialChance = 0f;
        Assert.That(stage.IsAerialHit(0f), Is.False);
        stage.EndTimeWindow = float.NaN;
        Assert.That(combo.HasValidSettings, Is.False);
    }

    [Test]
    public void AssaultStage_AcceleratesFallbackAndRetainsExactMinimumEndpoint()
    {
        var combo = new Action_EnemyAssaultCombo();
        var stage = new EnemyAssaultStage { HitCount = 20, StartPassDuration = .6f, EndPassDuration = .08f };
        var defense = new Action_DefenseWindow();
        combo.ConfigureDefenseForHit(defense, stage, 0, 0f);
        float firstDash = defense.MeleeDashDuration;
        combo.ConfigureDefenseForHit(defense, stage, 19, 0f);
        Assert.That(defense.ScreenPassDuration, Is.EqualTo(.08f), "The endpoint must not round below the valid ScreenWide minimum.");
        Assert.That(firstDash / defense.MeleeDashDuration, Is.EqualTo(7.5f).Within(.0001f));
        Assert.That(defense.MeleeExitDuration, Is.EqualTo(.04f));
    }

    [Test]
    public void ContinuousTiming_StartsAtZeroAndLeavesMotionDuringLateInputGrace()
    {
        var screen = new BattleScreenPassPath(new Vector3(-6, 0, 1), new Vector3(6, 0, 1), -1);
        Assert.That(BattleEnemyMeleeDashPath.TryScreenWide(new Vector3(3, 0, 1), screen, .65f, .08f, .04f,
            out var original), Is.True);
        var continuous = original.WithContinuousTiming(DefenseJudgementPolicy.BattleLateGrace + 1f / 30f);
        Assert.That(continuous.IsContinuous, Is.True);
        Assert.That(continuous.DashStartTime, Is.Zero);
        Assert.That(continuous.ImpactTime, Is.EqualTo(original.DashDuration));
        Assert.That(continuous.ExitDuration, Is.GreaterThan(DefenseJudgementPolicy.BattleLateGrace));
        Assert.That(continuous.Contact, Is.EqualTo(original.Contact));
        Assert.That(continuous.Exit, Is.EqualTo(original.Exit));
        Assert.That(original.IsContinuous, Is.False, "Pure geometry construction does not change its legacy sampling contract.");
    }

    [Test]
    public void OrdinaryScope_DefaultFlagsFlowFromTheStartAndRetainAuthoredImpactTime()
    {
        var block = new Action_DefenseWindow { TimeWindow = .6f, UseMeleePassThrough = false };
        var scope = _context.BeginEnemyMeleePresentation(_enemy, _player, block);
        Assert.That(scope.IsContinuous, Is.True);
        Assert.That(scope.ImpactTime, Is.EqualTo(.6f));
        Vector3 start = _enemy.transform.position;
        scope.AdvanceAttack(.01f);
        Assert.That(_enemy.transform.position.x, Is.LessThan(start.x));
        scope.AdvanceAttack(.6f);
        Assert.That(_enemy.transform.position.x, Is.EqualTo(-.65f).Within(.0001f));
    }

    [Test]
    public void StationaryAttackAnimation_AdvancesFromStartAndKeepsItsAuthoredHitFrame()
    {
        float rate = BattleImpactTiming.AnimationRateToImpact(.12f, .9f);
        Assert.That(rate, Is.GreaterThan(0f));
        Assert.That(rate * .9f, Is.EqualTo(.12f).Within(.0001f));
        Assert.That(BattleImpactTiming.AnimationRateToImpact(0f, .9f), Is.EqualTo(1f));
        Assert.That(BattleImpactTiming.AnimationRateToImpact(float.NaN, .9f), Is.EqualTo(1f));
        Assert.That(BattleImpactTiming.AnimationRateToImpact(.5f, .2f) * .2f, Is.EqualTo(.5f).Within(.0001f));
    }

    [Test]
    public void WarningForNextTurn_EndsImmediatelyAndDoesNotTurnIntoAnAttack()
    {
        var block = new Action_DefenseWindow
        {
            PatternMode = EnemyDefensePatternMode.TelegraphThenNextTurnWindow,
            UseTelegraph = false, TelegraphDuration = 10f, DelayAfter = 10f
        };
        var routine = block.Execute(_context);
        Assert.That(routine.MoveNext(), Is.False);
        Assert.That(_context.EnemyAttackImpactStarted, Is.False);
        Assert.That(_context.StopTimelineExecution, Is.False);
        (routine as System.IDisposable)?.Dispose();
    }

    [Test]
    public void ContinuousScope_LateResultDoesNotRewindItsOngoingPass()
    {
        var block = new Action_DefenseWindow
        {
            TimeWindow = .4f,
            ContinuousMeleeMotion = true,
            MeleePassThroughOnGuard = true
        };
        var clone = block.CopyForImpact(.5f);
        Assert.That(clone.ContinuousMeleeMotion, Is.True);
        var scope = _context.BeginEnemyMeleePresentation(_enemy, _player, block);
        Assert.That(scope.IsContinuous, Is.True);
        scope.AdvanceAttack(scope.ImpactTime + .02f);
        float beforeResult = _enemy.transform.position.x;
        scope.ResolveImpact(default);
        Assert.That(_enemy.transform.position.x, Is.EqualTo(beforeResult));
        scope.AdvanceAttack(scope.ImpactTime + .04f);
        Assert.That(_enemy.transform.position.x, Is.LessThan(beforeResult));
        _context.CancelPendingDefenseReaction();
        AssertPosition(new Vector3(3f, 2f, 1f));
    }

    [Test]
    public void ContinuousFallback_DoesNotClampMovementToUnusedLegacyStandingWindow()
    {
        var block = new Action_DefenseWindow
        {
            ContinuousMeleeMotion = true, MeleePassThroughOnGuard = true,
            TimeWindow = .01f, MeleeDashDuration = .2f
        };
        var scope = _context.BeginEnemyMeleePresentation(_enemy, _player, block);
        Assert.That(scope.IsContinuous, Is.True);
        Assert.That(scope.ImpactTime, Is.EqualTo(.2f));
    }

    [Test]
    public void AerialPath_RaisesEntryButLandsAtTheSameAuthoredImpact()
    {
        var screen = new BattleScreenPassPath(new Vector3(-6, 0, 1), new Vector3(6, 0, 1), -1);
        Assert.That(BattleEnemyMeleeDashPath.TryScreenWide(new Vector3(3, 0, 1), screen, .5f, .3f, .1f,
            out var ground), Is.True);
        var air = ground.WithApproachHeight(3f, .2f);
        Assert.That(air.Entry.y, Is.EqualTo(ground.Entry.y + 3f));
        Assert.That(air.Contact, Is.EqualTo(ground.Contact));
        Assert.That(air.Exit, Is.EqualTo(ground.Exit));
        Assert.That(air.SampleApproach(air.DashStartTime), Is.EqualTo(air.Entry));
        Assert.That(air.SampleApproach(air.ImpactTime).y, Is.EqualTo(ground.Contact.y).Within(.0001f));
    }

    [Test]
    public void GlobalGuardTraversal_ChangesOnlyMovementAndCloneRetainsLegacyData()
    {
        var block = new Action_DefenseWindow { TimeWindow = .4f, MeleeApproachHeight = 3f, MeleePassThroughOnGuard = true };
        var clone = block.CopyForImpact(.5f);
        Assert.That(clone.MeleeApproachHeight, Is.EqualTo(3f));
        Assert.That(clone.MeleePassThroughOnGuard, Is.True);
        block.MeleeApproachHeight = 0f;
        var scope = _context.BeginEnemyMeleePresentation(_enemy, _player, block);
        var result = new DefenseQteResult(DefenseInputReadStatus.Valid, DefenseInput.Parry,
            QTEManager.QTEGrade.Perfect, DefenseOutcome.Success, DefenseRequirement.Any, 0f, true, true, 0f);
        scope.ResolveImpact(result);
        Assert.That((bool)typeof(BattleEnemyMeleePresentationScope)
            .GetField("_passThrough", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(scope), Is.True);
        Assert.That(_player.CurrentHP, Is.EqualTo(100));
        Assert.That(_enemy.CurrentHP, Is.EqualTo(100));
    }

    [SetUp]
    public void SetUp()
    {
        _enemyObject = new GameObject("Melee Enemy Test");
        _enemy = _enemyObject.AddComponent<EnemyCharacter>();
        _playerObject = new GameObject("Melee Player Test");
        _player = _playerObject.AddComponent<PlayerCharacter>();
        SetHealth(_enemy, 100);
        SetHealth(_player, 100);
        _enemy.transform.position = new Vector3(3f, 2f, 1f);
        _player.transform.position = new Vector3(-1f, 4f, 0f);
        _context = new SkillContext { Actor = _enemy, Targets = new List<CharacterBase> { _player } };
    }

    [TearDown]
    public void TearDown()
    {
        _context?.CancelPendingDefenseReaction();
        if (_enemyObject != null) Object.DestroyImmediate(_enemyObject);
        if (_playerObject != null) Object.DestroyImmediate(_playerObject);
    }

    [Test]
    public void SharedAuthoredClock_MovesImmediatelyAndDoesNotTrackDodgingTarget()
    {
        var block = new Action_DefenseWindow { TimeWindow = 0.4f };
        var scope = _context.BeginEnemyMeleePresentation(_enemy, _player, block);
        var path = Path();
        scope.AdvanceAttack(0.28f);
        AssertPosition(path.SampleApproach(.28f));
        scope.AdvanceAttack(0.34f);
        Assert.That(_enemy.transform.position.x, Is.EqualTo(path.SampleApproach(0.34f).x).Within(0.0001f));
        _player.transform.position += Vector3.left * 4f;
        scope.AdvanceAttack(0.4f);
        AssertPosition(path.Contact);
        scope.AdvanceAttack(0.8f);
        AssertPosition(path.Exit);
        Assert.That(_enemy.CurrentHP, Is.EqualTo(100));
        Assert.That(_player.CurrentHP, Is.EqualTo(100), "Presentation does not apply or replay damage.");
    }

    [Test]
    public void ResolvedImpact_LeavesTheContinuousTailLiveWithoutRewinding()
    {
        var scope = _context.BeginEnemyMeleePresentation(_enemy, _player, new Action_DefenseWindow { TimeWindow = 0.4f });
        scope.AdvanceAttack(0.4f);
        scope.ResolveImpact(default);
        AssertPosition(Path().Contact);
        Tween movement = (Tween)typeof(BattleEnemyMeleePresentationScope)
            .GetField("_movement", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(scope);
        Assert.That(movement, Is.Not.Null);
        Assert.That(movement.IsPlaying(), Is.False);
        scope.AdvanceAttack(2f);
        AssertPosition(Path().Exit);
    }

    [TestCase(false)]
    [TestCase(true)]
    public void GuardAndPerfect_ContinueMovingWithoutChangingEitherCharactersHealth(bool perfect)
    {
        var scope = _context.BeginEnemyMeleePresentation(_enemy, _player, new Action_DefenseWindow { TimeWindow = 0.4f });
        scope.AdvanceAttack(.4f);
        var result = new DefenseQteResult(DefenseInputReadStatus.Valid, DefenseInput.Parry,
            perfect ? QTEManager.QTEGrade.Perfect : QTEManager.QTEGrade.Miss,
            perfect ? DefenseOutcome.Success : DefenseOutcome.Guarded,
            DefenseRequirement.Any, 0f, true, perfect, perfect ? 0f : 0.35f);
        scope.ResolveImpact(result);
        scope.AdvanceAttack(.4f + Path().ExitDuration);
        Assert.That(scope.Finish().MoveNext(), Is.False);
        scope.Dispose();
        AssertPosition(Path().Exit);
        Assert.That(_player.CurrentHP, Is.EqualTo(100));
        Assert.That(_enemy.CurrentHP, Is.EqualTo(100));
    }

    [Test]
    public void CancellationRestoresPreparation_AndDoesNotKillOtherTweens()
    {
        var path = Path();
        var scope = _context.BeginEnemyMeleePresentation(_enemy, _player, new Action_DefenseWindow { TimeWindow = 0.4f });
        Tween unrelated = DOVirtual.DelayedCall(2f, () => { }).SetRecyclable(false).Pause();
        try
        {
            scope.AdvanceAttack(0.36f);
            _context.StopTimelineExecution = true;
            _context.CancelPendingDefenseReaction();
            AssertPosition(path.Start);
            Assert.That(unrelated.IsActive(), Is.True);
            scope.Dispose();
            AssertPosition(path.Start);
        }
        finally { unrelated.Kill(); }
    }

    [Test]
    public void ReplacementCancelsOldMovementBeforeCapturingBaseline_StaleDisposeCannotOverwriteNewScope()
    {
        var block = new Action_DefenseWindow { TimeWindow = 0.4f };
        var first = _context.BeginEnemyMeleePresentation(_enemy, _player, block);
        first.AdvanceAttack(0.36f);
        var second = _context.BeginEnemyMeleePresentation(_enemy, _player, block);
        second.AdvanceAttack(0.4f);
        first.Dispose();
        AssertPosition(Path().Contact);
        _context.CancelPendingDefenseReaction();
        AssertPosition(Path().Start);
    }

    [Test]
    public void DestroyedTargetDoesNotInvalidateCapturedPath_DestroyedAttackerCancelsSafely()
    {
        var scope = _context.BeginEnemyMeleePresentation(_enemy, _player, new Action_DefenseWindow { TimeWindow = 0.4f });
        Object.DestroyImmediate(_playerObject);
        Assert.DoesNotThrow(() => scope.AdvanceAttack(0.4f));
        AssertPosition(Path().Contact);
        Object.DestroyImmediate(_enemyObject);
        Assert.DoesNotThrow(() => scope.AdvanceAttack(0.5f));
        Assert.DoesNotThrow(() => _context.CancelPendingDefenseReaction());
    }

    [TestCase(-1f)]
    [TestCase(1f)]
    public void PathKeepsHeightDepthAndEndsBeyondCapturedTarget(float direction)
    {
        var start = new Vector3(-3f * direction, 2f, 7f);
        var path = new BattleEnemyMeleeDashPath(start, Vector3.zero, 7f, 0.12f, 0.95f, 0.09f);
        Assert.That(path.ImpactTime, Is.EqualTo(7f));
        Assert.That(path.Contact.x, Is.EqualTo(-direction * 0.35f));
        Assert.That(path.Exit.x, Is.EqualTo(direction * 0.95f));
        Assert.That(path.Exit.y, Is.EqualTo(start.y));
        Assert.That(path.Exit.z, Is.EqualTo(start.z));
        Assert.That(path.SampleApproach(8f), Is.EqualTo(path.Contact));
    }

    [Test]
    public void GeometryPolicyRejectsRangedAreaCounterAndTelegraphOnly()
    {
        Assert.That(Allowed(), Is.True);
        Assert.That(Allowed(enabled: false), Is.False);
        Assert.That(Allowed(projectile: true), Is.False);
        Assert.That(Allowed(count: 3), Is.False);
        Assert.That(Allowed(player: false), Is.False);
        Assert.That(Allowed(requirement: DefenseRequirement.Counterable), Is.False);
        Assert.That(Allowed(pattern: EnemyDefensePatternMode.TelegraphThenNextTurnWindow), Is.False);
    }

    [TestCase(-1f)]
    [TestCase(1f)]
    public void ScreenWidePath_MatchesTargetCrossingAndUsesOneLinearSpeed(float direction)
    {
        Vector3 start = new Vector3(-3f * direction, 2f, 1f);
        var screen = new BattleScreenPassPath(new Vector3(-6f, 2f, 1f), new Vector3(6f, 2f, 1f), 1f);
        Assert.That(BattleEnemyMeleeDashPath.TryScreenWide(start, screen, 0.6f, 0.22f, 0.1f, out var path), Is.True);
        Assert.That(path.IsScreenWide, Is.True);
        Assert.That(path.SampleApproach(path.DashStartTime), Is.EqualTo(path.Entry));
        Assert.That(path.SampleApproach(path.ImpactTime), Is.EqualTo(path.Contact));
        Assert.That(path.Contact.x, Is.EqualTo(1f));
        Assert.That(path.ImpactTime, Is.EqualTo(0.6f));
        float beforeSpeed = Mathf.Abs(path.Contact.x - path.Entry.x) / path.DashDuration;
        float afterSpeed = Mathf.Abs(path.Exit.x - path.Contact.x) / path.ExitDuration;
        Assert.That(beforeSpeed, Is.EqualTo(afterSpeed).Within(0.001f));
    }

    [Test]
    public void ScreenWidePath_ShortAttackCapsMovementWithoutExtendingImpact()
    {
        var screen = new BattleScreenPassPath(new Vector3(-6f, 0f), new Vector3(6f, 0f), 4f);
        Assert.That(BattleEnemyMeleeDashPath.TryScreenWide(new Vector3(-3f, 0f), screen,
            0.02f, 0.22f, 0.1f, out var path), Is.True);
        Assert.That(path.DashDuration, Is.LessThanOrEqualTo(0.02f));
        Assert.That(path.ImpactTime, Is.EqualTo(0.02f));
        Assert.That(BattleEnemyMeleeDashPath.TryScreenWide(Vector3.zero, screen,
            0.6f, float.NaN, 0.1f, out _), Is.False);
    }

    [Test]
    public void ImpactCopy_PreservesScreenSettingsAndUsesCopiedAuthoredDuration()
    {
        var source = new Action_DefenseWindow
        {
            TimeWindow = 0.46f, UseMeleePassThrough = true,
            MeleePassExtent = EnemyMeleePassExtent.ScreenWide,
            ScreenPassDuration = 0.24f, ScreenEdgeApproachDuration = 0.11f, ScreenEdgeMarginPixels = 10f
        };
        Action_DefenseWindow copy = source.CopyForImpact(0.6f, "Attack");
        Assert.That(copy.MeleePassExtent, Is.EqualTo(EnemyMeleePassExtent.ScreenWide));
        Assert.That(copy.ScreenPassDuration, Is.EqualTo(0.24f));
        Assert.That(copy.ScreenEdgeApproachDuration, Is.EqualTo(0.11f));
        Assert.That(copy.ScreenEdgeMarginPixels, Is.EqualTo(10f));
        Assert.That(copy.TimeWindow, Is.EqualTo(0.6f));
        Assert.That(source.TimeWindow, Is.EqualTo(0.46f));
    }

    [Test]
    public void MissingScreenFraming_FlowsThroughLocalPathAtAuthoredImpactTime()
    {
        var scope = _context.BeginEnemyMeleePresentation(_enemy, _player,
            new Action_DefenseWindow { TimeWindow = 0.4f, MeleePassExtent = EnemyMeleePassExtent.ScreenWide });
        Assert.That(scope.IsScreenWide, Is.False);
        Assert.That(scope.IsContinuous, Is.True);
        Assert.That(scope.ImpactTime, Is.EqualTo(.4f));
        Assert.That(scope.IsPrepared, Is.True);
        scope.AdvanceAttack(0.4f);
        AssertPosition(Path().Contact);
    }

    [Test]
    public void ScreenWideTiming_AddsOnlyAuthoredEntryMinimum_AndRemainsVariable()
    {
        var block = new Action_DefenseWindow { UseMeleePassThrough = true, TimeWindow = 0.5f };
        SkillActionAuthoringTiming local = block.GetAuthoringTiming();
        block.MeleePassExtent = EnemyMeleePassExtent.ScreenWide;
        SkillActionAuthoringTiming wide = block.GetAuthoringTiming();
        Assert.That(wide.IsVariable, Is.True);
        Assert.That(wide.Duration - local.Duration, Is.EqualTo(block.ScreenEdgeApproachDuration).Within(0.0001f));
        Assert.That(block.TimeWindow, Is.EqualTo(0.5f));
        block.ScreenEdgeApproachDuration = float.NaN;
        Assert.That(block.GetAuthoringTiming().Duration, Is.EqualTo(local.Duration).Within(0.0001f));
    }

    private static bool Allowed(bool enabled = true, bool projectile = false, int count = 1,
        bool player = true, DefenseRequirement requirement = DefenseRequirement.Any,
        EnemyDefensePatternMode pattern = EnemyDefensePatternMode.ImmediateReaction)
        => BattleEnemyMeleeDashPath.CanUse(enabled, projectile, count, player, requirement, pattern);

    private static BattleEnemyMeleeDashPath Path()
        => new BattleEnemyMeleeDashPath(new Vector3(3f, 2f, 1f), new Vector3(-1f, 4f, 0f), 0.4f, 0.12f, 0.95f, 0.09f)
            .WithContinuousTiming(DefenseJudgementPolicy.BattleLateGrace + 1f / 30f, preserveImpactTime: true);

    private void AssertPosition(Vector3 expected)
        => Assert.That(Vector3.Distance(_enemy.transform.position, expected), Is.LessThan(0.0001f));

    private static void SetHealth(CharacterBase actor, int value)
        => typeof(CharacterBase).GetField("_currentHP", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(actor, value);
}
