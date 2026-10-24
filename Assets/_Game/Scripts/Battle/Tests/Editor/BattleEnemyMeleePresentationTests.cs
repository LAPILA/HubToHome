using System.Collections.Generic;
using System.Reflection;
using DG.Tweening;
using NUnit.Framework;
using UnityEngine;

public sealed class BattleEnemyMeleePresentationTests
{
    private GameObject _enemyObject, _playerObject;
    private EnemyCharacter _enemy;
    private PlayerCharacter _player;
    private SkillContext _context;

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
    public void SharedAuthoredClock_HoldsPreparationAndContactWithoutTrackingDodgingTarget()
    {
        var block = new Action_DefenseWindow { TimeWindow = 0.4f };
        var scope = _context.BeginEnemyMeleePresentation(_enemy, _player, block);
        var path = Path();
        scope.AdvanceAttack(0.28f);
        AssertPosition(path.Start);
        scope.AdvanceAttack(0.34f);
        Assert.That(_enemy.transform.position.x, Is.EqualTo(path.SampleApproach(0.34f).x).Within(0.0001f));
        _player.transform.position += Vector3.left * 4f;
        scope.AdvanceAttack(0.4f);
        AssertPosition(path.Contact);
        scope.AdvanceAttack(0.8f);
        AssertPosition(path.Contact);
        Assert.That(_enemy.CurrentHP, Is.EqualTo(100));
        Assert.That(_player.CurrentHP, Is.EqualTo(100), "Presentation does not apply or replay damage.");
    }

    [Test]
    public void ResultPinsContact_AndTailRemainsPausedUntilDamageConsumerRuns()
    {
        var scope = _context.BeginEnemyMeleePresentation(_enemy, _player, new Action_DefenseWindow { TimeWindow = 0.4f });
        scope.AdvanceAttack(0.3f);
        scope.ResolveImpact(default);
        AssertPosition(Path().Contact);
        Tween movement = (Tween)typeof(BattleEnemyMeleePresentationScope)
            .GetField("_movement", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(scope);
        Assert.That(movement, Is.Not.Null);
        Assert.That(movement.IsPlaying(), Is.False);
        scope.AdvanceAttack(2f);
        AssertPosition(Path().Contact);
    }

    [TestCase(false)]
    [TestCase(true)]
    public void GuardStopsAtContact_WithoutPassThrough(bool perfect)
    {
        var scope = _context.BeginEnemyMeleePresentation(_enemy, _player, new Action_DefenseWindow { TimeWindow = 0.4f });
        var result = new DefenseQteResult(DefenseInputReadStatus.Valid, DefenseInput.Parry,
            perfect ? QTEManager.QTEGrade.Perfect : QTEManager.QTEGrade.Miss,
            perfect ? DefenseOutcome.Success : DefenseOutcome.Guarded,
            DefenseRequirement.Any, 0f, true, perfect, perfect ? 0f : 0.35f);
        scope.ResolveImpact(result);
        Assert.That(scope.Finish().MoveNext(), Is.False);
        scope.Dispose();
        AssertPosition(Path().Contact);
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
    public void PolicyRejectsRangedAreaCounterAndTelegraphOnly_WithoutChangingExistingDefaults()
    {
        Assert.That(Allowed(), Is.True);
        Assert.That(Allowed(enabled: false), Is.False);
        Assert.That(Allowed(projectile: true), Is.False);
        Assert.That(Allowed(count: 3), Is.False);
        Assert.That(Allowed(player: false), Is.False);
        Assert.That(Allowed(requirement: DefenseRequirement.Counterable), Is.False);
        Assert.That(Allowed(pattern: EnemyDefensePatternMode.TelegraphThenNextTurnWindow), Is.False);
    }

    private static bool Allowed(bool enabled = true, bool projectile = false, int count = 1,
        bool player = true, DefenseRequirement requirement = DefenseRequirement.Any,
        EnemyDefensePatternMode pattern = EnemyDefensePatternMode.ImmediateReaction)
        => BattleEnemyMeleeDashPath.CanUse(enabled, projectile, count, player, requirement, pattern);

    private static BattleEnemyMeleeDashPath Path()
        => new BattleEnemyMeleeDashPath(new Vector3(3f, 2f, 1f), new Vector3(-1f, 4f, 0f), 0.4f, 0.12f, 0.95f, 0.09f);

    private void AssertPosition(Vector3 expected)
        => Assert.That(Vector3.Distance(_enemy.transform.position, expected), Is.LessThan(0.0001f));

    private static void SetHealth(CharacterBase actor, int value)
        => typeof(CharacterBase).GetField("_currentHP", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(actor, value);
}
