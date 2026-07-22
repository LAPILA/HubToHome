using DG.Tweening;
using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEngine;

public class DefenseQTEUIPresentationTests
{
    private GameObject _root;
    private TextMeshProUGUI _resultLabel;
    private DefenseQTEUITestDouble _ui;

    [SetUp]
    public void SetUp()
    {
        _root = new GameObject(
            "DefenseQTEUIPresentationTests",
            typeof(RectTransform),
            typeof(CanvasGroup));
        _ui = _root.AddComponent<DefenseQTEUITestDouble>();

        var labelObject = new GameObject("ResultLabel", typeof(RectTransform));
        labelObject.transform.SetParent(_root.transform, false);
        _resultLabel = labelObject.AddComponent<TextMeshProUGUI>();
        SetPrivateField(_ui, "_resultLabel", _resultLabel);
    }

    [TearDown]
    public void TearDown()
    {
        _ui?.InvokeDisableLifecycleForTest();
        Object.DestroyImmediate(_root);
    }

    [Test]
    public void ShowResult_DistinguishesInvalidInputFromTimeout()
    {
        _ui.ShowResult(CreateResult(DefenseInputReadStatus.Ambiguous, DefenseOutcome.Invalid));
        Assert.That(_resultLabel.text, Is.EqualTo("INVALID"));

        _ui.ShowResult(CreateResult(DefenseInputReadStatus.None, DefenseOutcome.Failure));
        Assert.That(_resultLabel.text, Is.EqualTo("MISS"));
    }

    [Test]
    public void Disable_CancelsOwnedResultSequence()
    {
        _ui.ShowResult(CreateResult(DefenseInputReadStatus.Valid, DefenseOutcome.Success));
        Sequence sequence = GetPrivateField<Sequence>(_ui, "_resultSequence");

        Assert.That(sequence, Is.Not.Null);
        Assert.That(sequence.IsActive(), Is.True);

        _ui.InvokeDisableLifecycleForTest();

        Assert.That(GetPrivateField<Sequence>(_ui, "_resultSequence"), Is.Null,
            "Disabling DefenseQTEUI must release its owned sequence reference.");
        Assert.That(sequence.IsActive(), Is.False,
            "Disabling DefenseQTEUI must cancel its owned result sequence.");
    }

    private static DefenseQteResult CreateResult(
        DefenseInputReadStatus inputStatus,
        DefenseOutcome outcome)
    {
        return new DefenseQteResult(
            inputStatus,
            DefenseInput.None,
            QTEManager.QTEGrade.Miss,
            outcome,
            DefenseRequirement.Any,
            0f,
            false,
            false);
    }

    private static void SetPrivateField(object target, string fieldName, object value)
    {
        FieldInfo field = typeof(DefenseQTEUI).GetField(
            fieldName,
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null);
        field.SetValue(target, value);
    }

    private static T GetPrivateField<T>(object target, string fieldName)
    {
        FieldInfo field = typeof(DefenseQTEUI).GetField(
            fieldName,
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null);
        return (T)field.GetValue(target);
    }
}

public sealed class DefenseQTEUITestDouble : DefenseQTEUI
{
    public void InvokeDisableLifecycleForTest()
    {
        base.OnDisable();
    }
}
