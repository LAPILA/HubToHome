using NUnit.Framework;
using UnityEngine;

public sealed class BattleDamageFeedbackEventTests
{
    private GameObject _managerObject;
    private GameObject _sourceObject;
    private GameObject _targetObject;
    private BattleManager _manager;
    private EnemyCharacter _source;
    private PlayerCharacter _target;
    private CharacterData _targetData;

    [SetUp]
    public void SetUp()
    {
        _managerObject = new GameObject("Battle Manager Test");
        _manager = _managerObject.AddComponent<BattleManager>();
        _sourceObject = new GameObject("Enemy Source");
        _source = _sourceObject.AddComponent<EnemyCharacter>();
        _targetObject = new GameObject("Player Target");
        _target = _targetObject.AddComponent<PlayerCharacter>();
        _targetData = ScriptableObject.CreateInstance<CharacterData>();
        _targetData.BaseStats = new StatBlock
        {
            MaxHP = 100,
            MaxAP = 50,
            ATK = 10,
            DEF = 5,
            SPD = 10,
        };
        // 테스트도 런타임과 동일하게 CharacterData가 StatBlock을 주입한다.
        _target.SetCharacterData(_targetData);
    }

    [TearDown]
    public void TearDown()
    {
        if (_targetData != null) Object.DestroyImmediate(_targetData);
        if (_targetObject != null) Object.DestroyImmediate(_targetObject);
        if (_sourceObject != null) Object.DestroyImmediate(_sourceObject);
        if (_managerObject != null) Object.DestroyImmediate(_managerObject);
    }

    [Test]
    public void SourceAwareDamagePreservesLegacyEventAndPublishesFeedback()
    {
        int legacyCount = 0;
        BattleDamageFeedback received = default;
        _manager.OnDamageDealt += (_, _, _) => legacyCount++;
        _manager.OnDamageFeedbackRequested += feedback => received = feedback;

        _manager.InvokeDamageEvent(_source, _target, 12, true, 100);

        Assert.That(legacyCount, Is.EqualTo(1));
        Assert.That(received.Source, Is.SameAs(_source));
        Assert.That(received.Target, Is.SameAs(_target));
        Assert.That(received.Amount, Is.EqualTo(12));
        Assert.That(received.IsCritical, Is.True);
        Assert.That(received.Kind, Is.EqualTo(BattleDamageFeedbackKind.Damage));
    }

    [Test]
    public void NonPositiveDamageDoesNotPublishPopupFeedback()
    {
        int feedbackCount = 0;
        int legacyCount = 0;
        _manager.OnDamageFeedbackRequested += _ => feedbackCount++;
        _manager.OnDamageDealt += (_, _, _) => legacyCount++;

        _manager.InvokeDamageEvent(_target, 0, false, 100);
        _manager.InvokeDamageEvent(_target, -5, false, 95);

        Assert.That(feedbackCount, Is.Zero);
        Assert.That(legacyCount, Is.EqualTo(2));
    }

    [Test]
    public void ElementAndBatchAreForwardedWithoutChangingLegacyDamage()
    {
        int legacyTotal = 0;
        BattleDamageFeedback received = default;
        _manager.OnDamageDealt += (_, amount, _) => legacyTotal += amount;
        _manager.OnDamageFeedbackRequested += feedback => received = feedback;
        using var action = _manager.DamageBatches.Begin();
        _manager.InvokeDamageEvent(_source, _target, 12, true, 100, DamageElement.Ice);
        Assert.That(received.Element, Is.EqualTo(DamageElement.Ice));
        Assert.That(received.BatchId, Is.EqualTo(_manager.DamageBatches.CurrentId));
        Assert.That(legacyTotal, Is.EqualTo(12));
    }

    [Test]
    public void DirectHitDoesNotPublishElectricReactionTwice()
    {
        int electric = 0;
        BattleDamageFeedback received = default;
        _target.OnElementalDamage += (_, amount, kind) => { if (kind == ElementalStatusKind.Electric) electric += amount; };
        _manager.OnDamageFeedbackRequested += feedback => received = feedback;
        _target.Elements.Add(ElementalStatusKind.Electric, 30);
        using var action = _manager.DamageBatches.Begin();
        var context = new SkillContext { Actor = _source, DamageBatchId = _manager.DamageBatches.CurrentId };
        int hp = _target.CurrentHP;
        // Use the production direct-hit path. Source need not own a SkillData for this check.
        DamageResult result = _target.TakeDamage(10, DamageElement.Fire);
        _manager.InvokeDamageEvent(_source, _target, result.FinalDamage, false, hp, result.Element, context.DamageBatchId);
        Assert.That(electric, Is.GreaterThan(0));
        Assert.That(hp - _target.CurrentHP, Is.EqualTo(result.FinalDamage + electric));
        Assert.That(received.Amount, Is.EqualTo(result.FinalDamage));
    }

    [Test]
    public void DamageBatchesHandleNestedOutOfOrderAndStaleDisposal()
    {
        var tracker = new BattleDamageBatchTracker();
        var ended = new System.Collections.Generic.List<long>();
        tracker.Ended += ended.Add;
        var first = tracker.Begin();
        long firstId = tracker.CurrentId;
        var second = tracker.Begin();
        long secondId = tracker.CurrentId;
        using (tracker.Activate(firstId)) Assert.That(tracker.CurrentId, Is.EqualTo(firstId));
        Assert.That(tracker.CurrentId, Is.EqualTo(secondId));
        first.Dispose();
        Assert.That(tracker.CurrentId, Is.EqualTo(secondId));
        tracker.Clear();
        using var third = tracker.Begin();
        long thirdId = tracker.CurrentId;
        second.Dispose();
        Assert.That(tracker.CurrentId, Is.EqualTo(thirdId));
        Assert.That(ended, Is.EqualTo(new[] { firstId, secondId }));
    }

    [Test]
    public void ClosingActivatedBatchStopsUsingItsIdBeforeEndCallbacks()
    {
        var tracker = new BattleDamageBatchTracker();
        using var first = tracker.Begin();
        long firstId = tracker.CurrentId;
        var second = tracker.Begin();
        long secondId = tracker.CurrentId;
        long callbackId = -1;
        tracker.Ended += _ => callbackId = tracker.CurrentId;
        using (tracker.Activate(secondId))
        {
            second.Dispose();
            Assert.That(tracker.CurrentId, Is.EqualTo(firstId));
            Assert.That(callbackId, Is.EqualTo(firstId));
        }
        Assert.That(tracker.CurrentId, Is.EqualTo(firstId));
    }

    [Test]
    public void MissPublishesWhiteNonCriticalFeedbackWithoutLegacyDamageEvent()
    {
        int legacyCount = 0;
        BattleDamageFeedback received = default;
        _manager.OnDamageDealt += (_, _, _) => legacyCount++;
        _manager.OnDamageFeedbackRequested += feedback => received = feedback;

        _manager.InvokeMissFeedback(_source, _target);

        Assert.That(legacyCount, Is.Zero);
        Assert.That(received.Source, Is.SameAs(_source));
        Assert.That(received.Target, Is.SameAs(_target));
        Assert.That(received.Amount, Is.Zero);
        Assert.That(received.IsCritical, Is.False);
        Assert.That(received.Kind, Is.EqualTo(BattleDamageFeedbackKind.Miss));
        Assert.That(received.ResolveColor(), Is.EqualTo(Color.white));
    }
}
