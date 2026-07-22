using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public sealed class PlayerPreemptiveAttackInputGuardTests
{
    private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;

    [Test]
    public void TryStartPreemptiveAttack_WhenControllerIsDisabledDoesNotConsumeCooldown()
    {
        GameObject playerObject = new GameObject("Disabled Attack Player", typeof(Rigidbody2D), typeof(Animator));

        try
        {
            PlayerController player = playerObject.AddComponent<PlayerController>();
            player.enabled = false;
            SetPrivateField(player, "_lastActionTime", -123f);

            Assert.That(player.TryStartPreemptiveAttack(), Is.False);
            Assert.That(GetPrivateField<float>(player, "_lastActionTime"), Is.EqualTo(-123f));
        }
        finally
        {
            Object.DestroyImmediate(playerObject);
        }
    }

    private static void SetPrivateField<T>(PlayerController player, string fieldName, T value)
    {
        FieldInfo field = typeof(PlayerController).GetField(fieldName, PrivateInstance);
        Assert.That(field, Is.Not.Null);
        field.SetValue(player, value);
    }

    private static T GetPrivateField<T>(PlayerController player, string fieldName)
    {
        FieldInfo field = typeof(PlayerController).GetField(fieldName, PrivateInstance);
        Assert.That(field, Is.Not.Null);
        return (T)field.GetValue(player);
    }
}
