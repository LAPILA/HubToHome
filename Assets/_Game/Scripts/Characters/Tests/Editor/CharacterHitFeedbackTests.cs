using System.Reflection;
using DG.Tweening;
using NUnit.Framework;
using UnityEngine;
using UnityEditor.Animations;

public sealed class CharacterHitFeedbackTests
{
    private const float TestPauseDuration = 0.045f;
    private GameObject _actor;
    private Animator _animator;
    private CharacterHitFeedback _feedback;

    [SetUp]
    public void SetUp()
    {
        _actor = new GameObject("HitFeedback Test", typeof(SpriteRenderer), typeof(Animator), typeof(CharacterHitFeedback));
        _animator = _actor.GetComponent<Animator>();
        _feedback = _actor.GetComponent<CharacterHitFeedback>();
        // Optional nonzero preset still has to obey all existing ownership contracts.
        SetPause("_normalPause", TestPauseDuration);
        SetPause("_strongPause", 0.065f);
        SetPause("_rapidPause", 0.016f);
    }

    [TearDown]
    public void TearDown()
    {
        if (_actor != null) Object.DestroyImmediate(_actor);
    }

    [Test]
    public void LocalPauseDefersImpactAndRestoresCapturedSpeedWithoutChangingGlobalTime()
    {
        float globalTime = Time.timeScale;
        _animator.speed = 1.6f;
        int impacts = 0;
        Begin(() => impacts++);
        Assert.That(_animator.speed, Is.Zero);
        Advance(0.02f);
        Assert.That(impacts, Is.Zero);
        Advance(TestPauseDuration - 0.02f + 0.001f);
        Assert.That(impacts, Is.EqualTo(1));
        Assert.That(_animator.speed, Is.EqualTo(1.6f));
        Assert.That(Time.timeScale, Is.EqualTo(globalTime));
    }

    [Test]
    public void ModalPauseConsumesNoHoldBudgetAndPausesOnlyTrackedReaction()
    {
        int impacts = 0;
        Begin(() => impacts++);
        Tween owned = DOVirtual.DelayedCall(1f, () => { }).SetRecyclable(false);
        Tween unrelated = DOVirtual.DelayedCall(1f, () => { }).SetRecyclable(false);
        try
        {
            _feedback.Track(owned);
            Advance(1f, true);
            Assert.That(impacts, Is.Zero);
            Assert.That(owned.IsPlaying(), Is.False);
            Assert.That(unrelated.IsPlaying(), Is.True);
            Advance(TestPauseDuration + 0.001f);
            Assert.That(impacts, Is.EqualTo(1));
            Assert.That(owned.IsPlaying(), Is.True);
        }
        finally { owned.Kill(false); unrelated.Kill(false); }
    }

    [Test]
    public void ExternalAnimatorSpeedOwnerIsNeverOverwrittenOrGivenStaleImpact()
    {
        int impacts = 0;
        Begin(() => impacts++);
        _animator.speed = 0.4f;
        Advance(0.04f);
        Assert.That(_animator.speed, Is.EqualTo(0.4f));
        Assert.That(impacts, Is.Zero);
        Assert.That(_feedback.IsHolding, Is.False);
    }

    [Test]
    public void RetriggerKeepsBaselineAndDisableCancelsCallbacksAndOwnedTweens()
    {
        int impacts = 0;
        _animator.speed = 1.3f;
        Begin(() => impacts++);
        Begin(() => impacts += 10);
        Tween tween = DOVirtual.DelayedCall(1f, () => impacts += 100).SetRecyclable(false);
        _feedback.Track(tween);
        _actor.SetActive(false);
        Assert.That(_animator.speed, Is.EqualTo(1.3f));
        Assert.That(_feedback.IsHolding, Is.False);
        Assert.That(tween.IsActive(), Is.False);
        Assert.That(impacts, Is.Zero);
    }

    [TestCase(0f, 0.045f)]
    [TestCase(0.099f, 0.045f)]
    [TestCase(0.1f, 0.065f)]
    [TestCase(1f, 0.065f)]
    public void FirstHitChoosesBoundedHoldFromDamageRatio(float damageRatio, float expectedPause)
    {
        int impacts = 0;
        float pause = _feedback.Begin(null, _animator, 0.1f, 1f, () => impacts++, damageRatio);
        Assert.That(pause, Is.EqualTo(expectedPause).Within(0.00001f));
        Advance(expectedPause - 0.001f);
        Assert.That(_feedback.IsHolding, Is.True);
        Assert.That(impacts, Is.Zero);
        Advance(0.002f);
        Assert.That(_feedback.IsHolding, Is.False);
        Assert.That(impacts, Is.EqualTo(1));
    }

    [Test]
    public void RepeatedHitsBeforeNextUpdateNeverExtendRemainingHoldAndOnlyLatestImpactRuns()
    {
        _animator.speed = 1.3f;
        int previousImpacts = 0;
        int latestImpacts = 0;
        Begin(() => previousImpacts++);
        Advance(0.03f);

        float remaining = TestPauseDuration - 0.03f;
        for (int i = 0; i < 20; i++)
        {
            float pause = _feedback.Begin(null, _animator, 0.1f, 1f,
                () => previousImpacts++, 1f);
            Assert.That(pause, Is.LessThanOrEqualTo(remaining + 0.00001f),
                "A same-frame multi-target/catch-up hit must not restart the hold deadline.");
            remaining = pause;
        }
        _feedback.Begin(null, _animator, 0.1f, 1f, () => latestImpacts++, 1f);
        Advance(remaining + 0.001f);

        Assert.That(previousImpacts, Is.Zero);
        Assert.That(latestImpacts, Is.EqualTo(1));
        Assert.That(_feedback.IsHolding, Is.False);
        Assert.That(_animator.speed, Is.EqualTo(1.3f));
    }

    [Test]
    public void ContactIsImmediateAndCoalescedUntilNextUpdate()
    {
        int contacts = 0;
        int releases = 0;
        _feedback.Begin(null, _animator, 0.1f, 1f, () => releases++, 0f, () => contacts++);
        Assert.That(contacts, Is.EqualTo(1));
        Assert.That(releases, Is.Zero);
        for (int i = 0; i < 20; i++)
            _feedback.Begin(null, _animator, 0.1f, 1f, () => releases++, 0f, () => contacts++);
        Assert.That(contacts, Is.EqualTo(1));
        Advance(0.02f);
        Assert.That(releases, Is.EqualTo(1));
        _feedback.Begin(null, _animator, 0.1f, 1f, null, 0f, () => contacts++);
        Assert.That(contacts, Is.EqualTo(2));
    }

    [Test]
    public void DisableResetsRapidCadenceAndRestoresMaterialForReactivation()
    {
        SpriteRenderer source = _actor.GetComponent<SpriteRenderer>();
        Material baseline = source.sharedMaterial;
        _animator.speed = 1.4f;
        _feedback.Begin(source, _animator, 0.1f, 1f, null);
        _actor.SetActive(false);
        Assert.That(_animator.speed, Is.EqualTo(1.4f));
        Assert.That(source.sharedMaterial, Is.SameAs(baseline));
        _actor.SetActive(true);
        Assert.That(_feedback.Begin(source, _animator, 0.1f, 1f, null),
            Is.EqualTo(TestPauseDuration));
    }

    [Test]
    public void ZeroConfiguredPauseNeverTakesAnimatorOwnershipOrAddsAFrameDelay()
    {
        typeof(CharacterHitFeedback).GetField("_normalPause", BindingFlags.NonPublic | BindingFlags.Instance)
            .SetValue(_feedback, 0f);
        _animator.speed = 1.7f;
        int releases = 0;
        Assert.That(_feedback.Begin(null, _animator, 0.1f, 1f, () => releases++), Is.Zero);
        Assert.That(_feedback.IsHolding, Is.False);
        Assert.That(_animator.speed, Is.EqualTo(1.7f));
        Assert.That(releases, Is.EqualTo(1));
    }

    [Test]
    public void RapidNextHitUsesShortHoldAndNormalHoldReturnsAfterRecoveryGap()
    {
        Begin(null);
        Advance(TestPauseDuration + 0.001f);
        float rapidPause = _feedback.Begin(null, _animator, 0.1f, 1f, null, 1f);
        Assert.That(rapidPause, Is.EqualTo(0.016f).Within(0.00001f),
            "Rapid hits stay light even when their damage ratio is large.");
        Advance(rapidPause + 0.001f);
        Assert.That(_feedback.IsHolding, Is.False);

        Advance(0.121f);
        float recoveredPause = _feedback.Begin(null, _animator, 0.1f, 1f, null);
        Assert.That(recoveredPause, Is.EqualTo(TestPauseDuration));
    }

    [Test]
    public void WhiteMaterialAppearsOnImpactPreservesGeometrySortAndSharedAssetAndIsReused()
    {
        SpriteRenderer source = _actor.GetComponent<SpriteRenderer>();
        Material originalMaterial = source.sharedMaterial;
        source.flipX = true;
        source.sortingOrder = 12;
        _feedback.Begin(source, _animator, 0.1f, 1f, null);
        Material whiteMaterial = source.sharedMaterial;
        Assert.That(_feedback.IsHolding, Is.True,
            "The white silhouette must already be visible during the contact hold.");
        Assert.That(whiteMaterial, Is.Not.SameAs(originalMaterial));
        Assert.That(whiteMaterial, Is.Not.SameAs(Resources.Load<Material>("HubToHome/CharacterHitSilhouette")));
        Assert.That(whiteMaterial.shader.name, Is.EqualTo("HubToHome/CharacterHitSilhouette"));
        Assert.That(whiteMaterial.GetFloat("_WhiteAmount"), Is.EqualTo(1f));
        Assert.That(source.flipX, Is.True);
        Assert.That(source.sortingOrder, Is.EqualTo(12));
        _feedback.Cancel();
        Assert.That(source.sharedMaterial, Is.SameAs(originalMaterial));
        _feedback.Begin(source, _animator, 0.1f, 0f, null);
        Assert.That(source.sharedMaterial, Is.SameAs(originalMaterial));
        _feedback.Begin(source, _animator, 0.1f, 1f, null);
        Assert.That(source.sharedMaterial, Is.SameAs(whiteMaterial));
        Assert.That(_actor.transform.childCount, Is.Zero, "Do not create a second renderer or alter Y-depth sorting.");
    }

    [Test]
    public void ExternalMaterialOwnerIsNotOverwrittenOnCancel()
    {
        SpriteRenderer source = _actor.GetComponent<SpriteRenderer>();
        _feedback.Begin(source, _animator, 0.1f, 1f, null);
        Advance(0.04f);
        Material external = Resources.Load<Material>("HubToHome/CharacterHitSilhouette");
        source.sharedMaterial = external;
        Advance(0.01f);
        _feedback.Cancel();
        Assert.That(source.sharedMaterial, Is.SameAs(external));
    }

    [Test]
    public void OptionalDirectHoldExcludesStatusDamage()
    {
        var target = _actor.AddComponent<FeedbackProbe>();
        target.Initialize();
        target.ApplyStatusDamage(2, ElementalStatusKind.Burn);
        Assert.That(target.Pause, Is.Zero);
        target.TakePureDamage(2);
        Assert.That(target.Pause, Is.EqualTo(TestPauseDuration));
        Advance(0.25f);
        target.TakeDamage(2, DamageElement.Physical);
        Assert.That(target.Pause, Is.EqualTo(TestPauseDuration));
    }

    [TestCase(0)]
    [TestCase(-1)]
    [TestCase(int.MinValue)]
    public void NonPositivePureDamageDoesNotChangeHpPublishOrReact(int amount)
    {
        var target = _actor.AddComponent<FeedbackProbe>();
        target.Initialize();
        int hpNotifications = 0;
        target.OnHPChanged += (_, _, _) => hpNotifications++;

        Assert.That(target.TakePureDamage(amount), Is.Zero);
        Assert.That(target.CurrentHP, Is.EqualTo(100));
        Assert.That(hpNotifications, Is.Zero);
        Assert.That(target.DamageCalls, Is.Zero);
        Assert.That(_feedback.IsHolding, Is.False);
    }

    [Test]
    public void ElectricReactionDamagePreservesProductionEnemyDirectHitHoldAndEvents()
    {
        var controller = new AnimatorController();
        controller.AddLayer("Base Layer");
        controller.AddParameter("Hurt", AnimatorControllerParameterType.Trigger);
        controller.AddParameter("BattleIdle", AnimatorControllerParameterType.Trigger);
        _animator.runtimeAnimatorController = controller;
        EnemyCharacter enemy = _actor.AddComponent<EnemyCharacter>();
        typeof(EnemyCharacter).GetMethod("Awake", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(enemy, null);
        EnemyData data = ScriptableObject.CreateInstance<EnemyData>();
        data.BaseStats = new StatBlock { MaxHP = 100, DEF = 0 };
        enemy.Setup(data);
        enemy.Elements.Add(ElementalStatusKind.Electric, 30);
        _animator.speed = 1.2f;
        int hpNotifications = 0;
        int reactionDamage = 0;
        enemy.OnHPChanged += (_, _, _) => hpNotifications++;
        enemy.OnElementalDamage += (_, amount, kind) =>
        {
            if (kind == ElementalStatusKind.Electric) reactionDamage += amount;
        };
        try
        {
            DamageResult result = enemy.TakeDamage(2, DamageElement.Fire);

            Assert.That(result.Applied, Is.True);
            Assert.That(reactionDamage, Is.GreaterThan(0));
            Assert.That(enemy.CurrentHP, Is.EqualTo(100 - result.FinalDamage - reactionDamage));
            Assert.That(hpNotifications, Is.EqualTo(2), "Direct and elemental HP events are both retained.");
            Assert.That(_feedback.IsHolding, Is.True,
                "Synchronous electric reaction must not cancel the direct contact hold.");
            Assert.That(_animator.speed, Is.Zero);
            Advance(TestPauseDuration + 0.001f);
            Assert.That(_feedback.IsHolding, Is.False);
            Assert.That(_animator.speed, Is.EqualTo(1.2f));
        }
        finally
        {
            _feedback.Cancel();
            _animator.runtimeAnimatorController = null;
            Object.DestroyImmediate(controller);
            Object.DestroyImmediate(data);
        }
    }

    [Test]
    public void StatusTickPreservesActiveDirectFlashWithoutRepeatingReactionCallback()
    {
        var target = _actor.AddComponent<FeedbackProbe>();
        target.Initialize();
        target.TakePureDamage(2);
        Advance(TestPauseDuration + 0.001f);
        Assert.That(_feedback.IsActive, Is.True);
        Assert.That(_feedback.IsHolding, Is.False);
        int directCalls = target.DamageCalls;
        int hpNotifications = 0;
        int statusNotifications = 0;
        target.OnHPChanged += (_, _, _) => hpNotifications++;
        target.OnElementalDamage += (_, _, _) => statusNotifications++;

        Assert.That(target.ApplyStatusDamage(3, ElementalStatusKind.Burn), Is.EqualTo(3));
        Assert.That(target.CurrentHP, Is.EqualTo(95));
        Assert.That(hpNotifications, Is.EqualTo(1));
        Assert.That(statusNotifications, Is.EqualTo(1));
        Assert.That(target.DamageCalls, Is.EqualTo(directCalls));
        Assert.That(_feedback.IsActive, Is.True);
    }

    [TestCase("burn", 1)]
    [TestCase("poison", 5)]
    [TestCase("bleed", 1)]
    public void LegacyStatusTicksKeepDamageRulesWithoutDirectHitPause(string kind, int expectedDamage)
    {
        var target = _actor.AddComponent<FeedbackProbe>();
        target.Initialize();
        StatusEffect effect = kind == "burn" ? (StatusEffect)new BurnEffect(2)
            : kind == "poison" ? new PoisonEffect(2) : new BleedEffect(2);
        effect.OnApply(target);
        try
        {
            if (kind == "bleed") target.NotifyActionExecuted(triggerElementalBleed: false);
            else effect.OnTick();
            Assert.That(target.CurrentHP, Is.EqualTo(100 - expectedDamage));
            Assert.That(target.Pause, Is.Zero);
            Assert.That(_feedback.IsHolding, Is.False);
            target.IsInvincible = true;
            if (kind == "bleed") target.NotifyActionExecuted(triggerElementalBleed: false);
            else effect.OnTick();
            Assert.That(target.CurrentHP, Is.EqualTo(100 - expectedDamage), "Preserve legacy invincibility rules.");
        }
        finally { effect.OnRemove(); }
    }

    [Test]
    public void PreferredEnemySkillTriggerCancelsPendingHurtAndOwnedSpeed()
    {
        var controller = new AnimatorController();
        controller.AddLayer("Base Layer");
        controller.AddParameter("PreferredSkill", AnimatorControllerParameterType.Trigger);
        _animator.runtimeAnimatorController = controller;
        EnemyCharacter enemy = _actor.AddComponent<EnemyCharacter>();
        typeof(EnemyCharacter).GetMethod("Awake", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(enemy, null);
        EnemyData data = ScriptableObject.CreateInstance<EnemyData>();
        data.BaseStats = new StatBlock { MaxHP = 100, DEF = 0 };
        enemy.Setup(data);
        SpriteRenderer source = _actor.GetComponent<SpriteRenderer>();
        Material originalMaterial = source.sharedMaterial;
        _animator.speed = 1.2f;
        try
        {
            Assert.That(enemy.TakePureDamage(2), Is.EqualTo(2));
            Assert.That(enemy.CurrentHP, Is.EqualTo(98));
            Assert.That(_feedback.IsHolding, Is.True, "Start through the production CharacterBase binding.");
            enemy.PlaySkillAnim("PreferredSkill", EnemyCharacter.HashSkill);
            Assert.That(_feedback.IsHolding, Is.False, "The new animation cancels the pending hit synchronously.");
            Advance(0.04f);
            Assert.That(_feedback.IsHolding, Is.False);
            Assert.That(_animator.speed, Is.EqualTo(1.2f));
            Assert.That(source.sharedMaterial, Is.SameAs(originalMaterial), "No delayed flash may replace the new skill.");
            Assert.That(DOTween.IsTweening(_actor.transform), Is.False);
        }
        finally
        {
            _animator.runtimeAnimatorController = null;
            Object.DestroyImmediate(controller);
            Object.DestroyImmediate(data);
        }
    }

    [Test]
    public void DefaultPresetKeepsDamagePoseImmediateAndDoesNotOwnAnimatorSpeed()
    {
        SetPause("_normalPause", CharacterHitFeedback.PauseDuration);
        SetPause("_strongPause", CharacterHitFeedback.StrongPauseDuration);
        SetPause("_rapidPause", CharacterHitFeedback.RapidPauseDuration);
        _animator.speed = 1.4f;
        int impacts = 0;
        Assert.That(_feedback.Begin(null, _animator, 0.1f, 1f, () => impacts++, 1f), Is.Zero);
        Assert.That(_feedback.Begin(null, _animator, 0.1f, 1f, () => impacts++, 1f), Is.Zero);
        Assert.That(impacts, Is.EqualTo(2));
        Assert.That(_feedback.IsHolding, Is.False);
        Assert.That(_animator.speed, Is.EqualTo(1.4f));
    }

    [Test]
    public void DefeatFlashNeverSchedulesHurtOrFreezesAnimatorAndReleasesMaterial()
    {
        SpriteRenderer source = _actor.GetComponent<SpriteRenderer>();
        Material original = source.sharedMaterial;
        _animator.speed = 1.4f;
        _feedback.ShowDefeat(source, 0.06f, 1f);
        Assert.That(_feedback.IsHolding, Is.False);
        Assert.That(_animator.speed, Is.EqualTo(1.4f));
        Assert.That(source.sharedMaterial, Is.Not.SameAs(original));
        Advance(0.061f);
        Assert.That(_feedback.IsActive, Is.False);
        Assert.That(source.sharedMaterial, Is.SameAs(original));
    }

    [TestCase(-1f, 1f)]
    [TestCase(1f, -1f)]
    public void DirectHitDirectionPointsAwayFromTheActualAttacker(float sourceX, float expectedDirection)
    {
        var target = _actor.AddComponent<FeedbackProbe>();
        target.Initialize();
        var other = new GameObject("Attacker", typeof(FeedbackProbe));
        try
        {
            FeedbackProbe source = other.GetComponent<FeedbackProbe>();
            source.Initialize();
            other.transform.position = new Vector3(sourceX, 0f, 0f);
            target.TakeDamage(2, DamageElement.Physical, source);
            Assert.That(target.Direction, Is.EqualTo(expectedDirection));
        }
        finally { Object.DestroyImmediate(other); }
    }

    private void SetPause(string field, float value) => typeof(CharacterHitFeedback)
        .GetField(field, BindingFlags.NonPublic | BindingFlags.Instance).SetValue(_feedback, value);
    private void Begin(System.Action impact) => _feedback.Begin(null, _animator, 0.1f, 1f, impact);
    private void Advance(float duration, bool paused = false) => typeof(CharacterHitFeedback)
        .GetMethod("Advance", BindingFlags.NonPublic | BindingFlags.Instance)
        .Invoke(_feedback, new object[] { duration, paused });

    public sealed class FeedbackProbe : CharacterBase
    {
        public float Pause;
        public int DamageCalls;
        public float Direction;
        public void Initialize()
        {
            SetBaseStats(new StatBlock { MaxHP = 100, DEF = 0 });
            SetCurrentHPValue(100);
        }
        protected override void OnDamageTaken(int damage)
        {
            DamageCalls++;
            Direction = ResolveHitReactionDirection(1f);
            CancelHitFeedback();
            Pause = BeginHitFeedback(null, null, 0.1f, null);
        }
        protected override void OnDie() => CancelHitFeedback();
    }
}
