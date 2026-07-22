using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public sealed class PlayerPreemptiveAttackDirectionTests
{
    private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
    private static readonly Vector2 TestOrigin = new Vector2(10000f, 10000f);

    [Test]
    public void FindPreemptiveAttackTarget_IgnoresTargetBehindFacingDirection()
    {
        GameObject playerObject = null;
        GameObject frontObject = null;
        GameObject backObject = null;

        try
        {
            PlayerController player = CreatePlayer(out playerObject);
            player.SetFacingDirection(3);
            SetPrivateField(player, "_attackRange", 2f);

            PreemptiveAttackTargetTestDouble front = CreateTarget("Front Target", TestOrigin + Vector2.right, out frontObject);
            CreateTarget("Back Target", TestOrigin + Vector2.left * 0.25f, out backObject);
            Physics2D.SyncTransforms();

            MethodInfo findTarget = typeof(PlayerController).GetMethod("FindPreemptiveAttackTarget", PrivateInstance);
            Assert.That(findTarget, Is.Not.Null);

            object selected = findTarget.Invoke(player, null);
            Assert.That(selected, Is.SameAs(front));
        }
        finally
        {
            DestroyImmediate(backObject);
            DestroyImmediate(frontObject);
            DestroyImmediate(playerObject);
        }
    }

    [Test]
    public void ResolvePreemptiveAttackHit_UsesCurrentTargetAndRunsOnce()
    {
        GameObject playerObject = null;
        GameObject targetObject = null;

        try
        {
            PlayerController player = CreatePlayer(out playerObject);
            player.SetFacingDirection(3);
            SetPrivateField(player, "_attackRange", 2f);
            SetPrivateField(player, "_preemptiveAttackInProgress", true);

            PreemptiveAttackTargetTestDouble target = CreateTarget(
                "Moving Target",
                TestOrigin + Vector2.left,
                out targetObject);

            MethodInfo resolveHit = typeof(PlayerController).GetMethod("ResolvePreemptiveAttackHit");
            Assert.That(resolveHit, Is.Not.Null, "Animation Event 판정 진입점이 필요합니다.");

            target.transform.position = TestOrigin + Vector2.right;
            Physics2D.SyncTransforms();
            resolveHit.Invoke(player, null);
            resolveHit.Invoke(player, null);

            Assert.That(target.AttemptCount, Is.EqualTo(1));
        }
        finally
        {
            DestroyImmediate(targetObject);
            DestroyImmediate(playerObject);
        }
    }

    private static PlayerController CreatePlayer(out GameObject playerObject)
    {
        playerObject = new GameObject("Test Player", typeof(Rigidbody2D), typeof(Animator), typeof(BoxCollider2D));
        playerObject.transform.position = TestOrigin;
        PlayerController player = playerObject.AddComponent<PlayerController>();
        InvokePrivate(player, "Awake");
        return player;
    }

    private static PreemptiveAttackTargetTestDouble CreateTarget(
        string name,
        Vector2 position,
        out GameObject targetObject)
    {
        targetObject = new GameObject(name, typeof(BoxCollider2D));
        targetObject.transform.position = position;
        return targetObject.AddComponent<PreemptiveAttackTargetTestDouble>();
    }

    private static void SetPrivateField<T>(PlayerController player, string fieldName, T value)
    {
        FieldInfo field = typeof(PlayerController).GetField(fieldName, PrivateInstance);
        Assert.That(field, Is.Not.Null, $"Field not found: {fieldName}");
        field.SetValue(player, value);
    }

    private static void InvokePrivate(PlayerController player, string methodName)
    {
        MethodInfo method = typeof(PlayerController).GetMethod(methodName, PrivateInstance);
        Assert.That(method, Is.Not.Null, $"Method not found: {methodName}");
        method.Invoke(player, null);
    }

    private static void DestroyImmediate(Object target)
    {
        if (target != null)
            Object.DestroyImmediate(target);
    }
}

public sealed class PreemptiveAttackTargetTestDouble : MonoBehaviour, IPreemptiveAttackTarget
{
    public int AttemptCount { get; private set; }

    public bool CanStartPreemptiveAttack(PlayerController player) => player != null;

    public bool TryStartPreemptiveAttack(PlayerController player)
    {
        AttemptCount++;
        return true;
    }
}
