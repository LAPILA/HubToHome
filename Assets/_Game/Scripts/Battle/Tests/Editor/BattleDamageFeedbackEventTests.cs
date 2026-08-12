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
