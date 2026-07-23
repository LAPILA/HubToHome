using System.Collections.Generic;
using System.Reflection;
using DG.Tweening;
using NUnit.Framework;
using UnityEngine;

public sealed class CharacterVisualAccessibilityTests
{
    private readonly List<GameObject> _createdObjects = new List<GameObject>();

    [SetUp]
    public void SetUp()
    {
        DOTween.KillAll(false);
    }

    [TearDown]
    public void TearDown()
    {
        DOTween.KillAll(false);
        for (int i = _createdObjects.Count - 1; i >= 0; i--)
        {
            if (_createdObjects[i] != null)
                Object.DestroyImmediate(_createdObjects[i]);
        }
        _createdObjects.Clear();
    }

    [Test]
    public void PlayerHurtEffectHasNoFlashOrShakeWhenAccessibilityScalesAreZero()
    {
        GameObject playerObject = CreateObject(
            "Accessible Player",
            typeof(SpriteRenderer),
            typeof(Rigidbody2D),
            typeof(Animator),
            typeof(PlayerController));
        PlayerController player = playerObject.GetComponent<PlayerController>();
        SpriteRenderer renderer = playerObject.GetComponent<SpriteRenderer>();
        InvokeAwake(player);
        player.SetScreenFlashScaleProvider(new FixedFlashScaleProvider(0f));
        player.SetScreenShakeScaleProvider(new FixedShakeScaleProvider(0f));
        Vector3 origin = playerObject.transform.position;

        player.PlayHurtEffect();
        AdvanceTweens(renderer, 0.04f);
        AdvanceTweens(playerObject.transform, 0.15f);

        AssertColor(renderer.color, Color.white);
        Assert.That(playerObject.transform.position, Is.EqualTo(origin));
    }

    [Test]
    public void BattlePlayerHurtEffectHasNoFlashOrShakeWhenAccessibilityScalesAreZero()
    {
        GameObject playerObject = CreateObject(
            "Accessible Battle Player",
            typeof(SpriteRenderer),
            typeof(PlayerCharacter));
        PlayerCharacter player = playerObject.GetComponent<PlayerCharacter>();
        SpriteRenderer renderer = playerObject.GetComponent<SpriteRenderer>();
        InvokeAwake(player);
        player.SetScreenFlashScaleProvider(new FixedFlashScaleProvider(0f));
        player.SetScreenShakeScaleProvider(new FixedShakeScaleProvider(0f));
        Vector3 origin = playerObject.transform.position;

        int damage = player.TakePureDamage(1);
        AdvanceTweens(renderer, 0.06f);
        AdvanceTweens(playerObject.transform, 0.1f);

        Assert.That(damage, Is.EqualTo(1));
        AssertColor(renderer.color, Color.white);
        Assert.That(playerObject.transform.position, Is.EqualTo(origin));
    }

    [Test]
    public void EnemyHurtEffectHasNoFlashOrShakeWhenAccessibilityScalesAreZero()
    {
        GameObject enemyObject = CreateObject(
            "Accessible Enemy",
            typeof(SpriteRenderer),
            typeof(EnemyCharacter));
        EnemyCharacter enemy = enemyObject.GetComponent<EnemyCharacter>();
        SpriteRenderer renderer = enemyObject.GetComponent<SpriteRenderer>();
        InvokeAwake(enemy);
        enemy.SetScreenFlashScaleProvider(new FixedFlashScaleProvider(0f));
        enemy.SetScreenShakeScaleProvider(new FixedShakeScaleProvider(0f));
        Vector3 origin = enemyObject.transform.position;

        MethodInfo onDamageTaken = typeof(EnemyCharacter).GetMethod(
            "OnDamageTaken",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(onDamageTaken, Is.Not.Null);
        onDamageTaken.Invoke(enemy, new object[] { 1 });
        AdvanceTweens(renderer, 0.06f);
        AdvanceTweens(enemyObject.transform, 0.1f);

        AssertColor(renderer.color, Color.white);
        Assert.That(enemyObject.transform.position, Is.EqualTo(origin));
    }

    private GameObject CreateObject(string name, params System.Type[] components)
    {
        var gameObject = new GameObject(name, components);
        _createdObjects.Add(gameObject);
        return gameObject;
    }

    private static void AdvanceTweens(object target, float position)
    {
        List<Tween> tweens = DOTween.TweensByTarget(target, false);
        Assert.That(tweens, Is.Not.Null.And.Not.Empty);
        for (int i = 0; i < tweens.Count; i++)
            tweens[i].Goto(position, false);
    }

    private static void InvokeAwake(object target)
    {
        MethodInfo awake = target.GetType().GetMethod(
            "Awake",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(awake, Is.Not.Null);
        awake.Invoke(target, null);
    }

    private static void AssertColor(Color actual, Color expected)
    {
        Assert.That(actual.r, Is.EqualTo(expected.r).Within(0.001f));
        Assert.That(actual.g, Is.EqualTo(expected.g).Within(0.001f));
        Assert.That(actual.b, Is.EqualTo(expected.b).Within(0.001f));
        Assert.That(actual.a, Is.EqualTo(expected.a).Within(0.001f));
    }

    private sealed class FixedFlashScaleProvider : IScreenFlashScaleProvider
    {
        public FixedFlashScaleProvider(float scale)
        {
            Scale = scale;
        }

        public float Scale { get; }
    }

    private sealed class FixedShakeScaleProvider : IScreenShakeScaleProvider
    {
        public FixedShakeScaleProvider(float scale)
        {
            Scale = scale;
        }

        public float Scale { get; }
    }
}
