using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public sealed class OverworldEnemyVisualAccessibilityTests
{
    private GameObject _enemyObject;

    [TearDown]
    public void TearDown()
    {
        FieldInfo globalLock = typeof(OverworldEnemy).GetField(
            "s_globalEncounterLockUntil",
            BindingFlags.Static | BindingFlags.NonPublic);
        globalLock?.SetValue(null, 0f);

        if (_enemyObject != null)
            Object.DestroyImmediate(_enemyObject);
    }

    [Test]
    public void InstantVictoryFlashKeepsOriginalColorWhenFlashScaleIsZero()
    {
        _enemyObject = new GameObject(
            "Accessible Field Enemy",
            typeof(SpriteRenderer),
            typeof(BoxCollider2D),
            typeof(Rigidbody2D),
            typeof(EnemyCharacter),
            typeof(OverworldEnemy));
        EnemyCharacter enemyCharacter = _enemyObject.GetComponent<EnemyCharacter>();
        OverworldEnemy overworldEnemy = _enemyObject.GetComponent<OverworldEnemy>();
        SpriteRenderer renderer = _enemyObject.GetComponent<SpriteRenderer>();
        InvokeAwake(enemyCharacter);
        InvokeAwake(overworldEnemy);

        Color original = new Color(0.35f, 0.45f, 0.55f, 0.65f);
        renderer.color = original;
        overworldEnemy.SetScreenFlashScaleProvider(new FixedFlashScaleProvider(0f));

        MethodInfo resolveRoutine = typeof(OverworldEnemy).GetMethod(
            "ResolveInstantVictoryRoutine",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(resolveRoutine, Is.Not.Null);
        var routine = (IEnumerator)resolveRoutine.Invoke(
            overworldEnemy,
            new object[] { null, new List<EnemyData>() });

        Assert.That(routine.MoveNext(), Is.True);
        AssertColor(renderer.color, original);
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
}
