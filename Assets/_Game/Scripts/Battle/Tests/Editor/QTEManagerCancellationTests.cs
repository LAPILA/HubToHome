using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public class QTEManagerCancellationTests
{
    private GameObject _gameObject;
    private QTEManager _manager;

    [SetUp]
    public void SetUp()
    {
        PropertyInfo property = typeof(QTEManager).GetProperty(
            nameof(QTEManager.Instance),
            BindingFlags.Public | BindingFlags.Static);
        property.SetValue(null, null);

        _gameObject = new GameObject("QTEManagerCancellationTests");
        _manager = _gameObject.AddComponent<QTEManager>();
    }

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(_gameObject);
    }

    [Test]
    public void ForceStop_CancelsWithoutInvokingMissGameplayCallback()
    {
        bool callbackInvoked = false;
        QteExecution execution = _manager.StartDefenseQTEWithResult(
            10f,
            1f,
            (_, _) => callbackInvoked = true);

        _manager.ForceStop();

        Assert.That(execution.IsDone, Is.True);
        Assert.That(execution.Termination, Is.EqualTo(QteTermination.Cancelled));
        Assert.That(callbackInvoked, Is.False);
        Assert.That(_manager.IsActive, Is.False);
    }

    [Test]
    public void StartingAnotherQte_CancelsPreviousExecution()
    {
        QteExecution first = _manager.StartDefenseQTEWithResult(10f, 1f, null);
        QteExecution second = _manager.StartDefenseQTEWithResult(10f, 1f, null);

        Assert.That(first.Termination, Is.EqualTo(QteTermination.Cancelled));
        Assert.That(second.IsDone, Is.False);
    }

    [Test]
    public void EmptySequence_FailsImmediately()
    {
        QteExecution execution = _manager.StartSequenceQTEWithResult(
            null,
            1f,
            null);

        Assert.That(execution.Termination, Is.EqualTo(QteTermination.Failed));
        Assert.That(_manager.IsActive, Is.False);
    }
}
