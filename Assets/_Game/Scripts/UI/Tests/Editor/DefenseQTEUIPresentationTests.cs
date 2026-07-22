using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEngine;

public class DefenseQTEUIPresentationTests
{
    private GameObject _root;
    private TextMeshProUGUI _resultLabel;
    private DefenseQTEUI _ui;

    [SetUp]
    public void SetUp()
    {
        _root = new GameObject(
            "DefenseQTEUIPresentationTests",
            typeof(RectTransform),
            typeof(CanvasGroup));
        _ui = _root.AddComponent<DefenseQTEUI>();

        var labelObject = new GameObject("ResultLabel", typeof(RectTransform));
        labelObject.transform.SetParent(_root.transform, false);
        _resultLabel = labelObject.AddComponent<TextMeshProUGUI>();
        SetPrivateField(_ui, "_resultLabel", _resultLabel);
    }

    [TearDown]
    public void TearDown()
    {
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
        FieldInfo field = target.GetType().GetField(
            fieldName,
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null);
        field.SetValue(target, value);
    }
}
