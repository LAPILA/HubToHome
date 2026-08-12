using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public sealed class BattleUIControllerPartyWaveTests
{
    private GameObject _root;

    [SetUp]
    public void SetUp()
    {
        _root = new GameObject("Battle UI Party Wave Test", typeof(RectTransform));
        _root.SetActive(false);
    }

    [TearDown]
    public void TearDown()
    {
        if (_root != null)
            Object.DestroyImmediate(_root);
    }

    [Test]
    public void HandlePlayerPartyChanged_RebindsPartyAndClearsTargetingState()
    {
        BattleUIController controller = _root.AddComponent<BattleUIController>();
        var nextParty = new List<PlayerCharacter>();
        SetPrivateField(controller, "_isTargetingMode", true);
        SetPrivateField(controller, "_selectedTargetIndex", 2);

        MethodInfo handler = typeof(BattleUIController).GetMethod(
            "HandlePlayerPartyChanged",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(handler, Is.Not.Null);

        handler.Invoke(controller, new object[] { nextParty });

        Assert.That(GetPrivateField<List<PlayerCharacter>>(controller, "_party"), Is.SameAs(nextParty));
        Assert.That(GetPrivateField<bool>(controller, "_isTargetingMode"), Is.False);
        Assert.That(GetPrivateField<int>(controller, "_selectedTargetIndex"), Is.Zero);
    }

    private static void SetPrivateField(object target, string fieldName, object value)
    {
        FieldInfo field = target.GetType().GetField(
            fieldName,
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null);
        field.SetValue(target, value);
    }

    private static T GetPrivateField<T>(object target, string fieldName)
    {
        FieldInfo field = target.GetType().GetField(
            fieldName,
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null);
        return (T)field.GetValue(target);
    }
}
