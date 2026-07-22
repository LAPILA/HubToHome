using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public sealed class PlayerPreemptiveAttackStateTests
{
    private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;

    [Test]
    public void TryStartPreemptiveAttack_WhenAttackIsInProgressRejectsReentry()
    {
        GameObject playerObject = null;

        try
        {
            PlayerController player = CreatePlayer(out playerObject);
            SetPrivateField(player, "_preemptiveAttackInProgress", true);

            Assert.That(player.TryStartPreemptiveAttack(), Is.False);
        }
        finally
        {
            if (playerObject != null)
                Object.DestroyImmediate(playerObject);
        }
    }

    [Test]
    public void RestoreAfterFailedPreemptiveAttack_ClearsAttackState()
    {
        GameObject playerObject = null;

        try
        {
            PlayerController player = CreatePlayer(out playerObject);
            SetPrivateField(player, "_preemptiveAttackInProgress", true);
            SetPrivateField(player, "_preemptiveAttackHitResolved", true);
            SetPrivateField(player, "_preemptiveAttackStartedEncounter", true);
            SetPrivateField<Animator>(player, "_anim", null);

            MethodInfo restore = typeof(PlayerController).GetMethod(
                "RestoreAfterFailedPreemptiveAttack",
                PrivateInstance);
            Assert.That(restore, Is.Not.Null);
            restore.Invoke(player, new object[] { GameState.Exploration });

            Assert.That(GetPrivateField<bool>(player, "_preemptiveAttackInProgress"), Is.False);
            Assert.That(GetPrivateField<bool>(player, "_preemptiveAttackHitResolved"), Is.False);
            Assert.That(GetPrivateField<bool>(player, "_preemptiveAttackStartedEncounter"), Is.False);
            Assert.That(player.State, Is.EqualTo(PlayerController.PlayerState.Idle));
        }
        finally
        {
            if (playerObject != null)
                Object.DestroyImmediate(playerObject);
        }
    }

    private static PlayerController CreatePlayer(out GameObject playerObject)
    {
        playerObject = new GameObject("Test Player", typeof(Rigidbody2D), typeof(Animator));
        PlayerController player = playerObject.AddComponent<PlayerController>();
        MethodInfo awake = typeof(PlayerController).GetMethod("Awake", PrivateInstance);
        Assert.That(awake, Is.Not.Null);
        awake.Invoke(player, null);
        return player;
    }

    private static void SetPrivateField<T>(PlayerController player, string fieldName, T value)
    {
        FieldInfo field = typeof(PlayerController).GetField(fieldName, PrivateInstance);
        Assert.That(field, Is.Not.Null, $"Field not found: {fieldName}");
        field.SetValue(player, value);
    }

    private static T GetPrivateField<T>(PlayerController player, string fieldName)
    {
        FieldInfo field = typeof(PlayerController).GetField(fieldName, PrivateInstance);
        Assert.That(field, Is.Not.Null, $"Field not found: {fieldName}");
        return (T)field.GetValue(player);
    }
}
