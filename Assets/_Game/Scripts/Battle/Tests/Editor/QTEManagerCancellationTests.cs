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

    [Test]
    public void SkillStream_CancelsThroughExistingOwner_WithoutInvokingResult()
    {
        int impacts = 0;
        QteExecution execution = _manager.StartSkillInputStream(5f, .65f, .45f,
            _ => impacts++, () => true);
        Assert.That(_manager.IsSkillQteActive, Is.True);
        _manager.ForceStop();
        Assert.That(execution.Termination, Is.EqualTo(QteTermination.Cancelled));
        Assert.That(impacts, Is.Zero);
        Assert.That(_manager.IsActive, Is.False);
    }

    [Test]
    public void SkillStream_ReplacementInvalidatesOnlyThePreviousHandle()
    {
        QteExecution first = _manager.StartSkillInputStream(5f, .65f, .45f, null, () => true);
        QteExecution second = _manager.StartDefenseQTEWithResult(10f, 1f, null);
        Assert.That(first.Termination, Is.EqualTo(QteTermination.Cancelled));
        Assert.That(_manager.Cancel(first), Is.False);
        Assert.That(second.IsDone, Is.False);
        _manager.ForceStop();
    }

    [Test]
    public void RapidStrikes_FiftyDeadlinesFitWithinFiveSeconds_IndependentOfInputCadence()
    {
        for (int i = 1; i <= 50; i++)
        {
            Assert.That(Action_RapidStrikes.CountDueHits(i * .1f, 5f, .1f), Is.EqualTo(i));
        }
        Assert.That(Action_RapidStrikes.CountDueHits(.35f, 5f, .1f), Is.EqualTo(3));
        Assert.That(Action_RapidStrikes.CountDueHits(6f, 5f, .1f), Is.EqualTo(50));
        Assert.That(Action_RapidStrikes.CountDueHits(0f, 5f, .1f), Is.Zero);
    }

    [Test]
    public void SkillStream_RejectsNonFiniteTime()
    {
        QteExecution execution = _manager.StartSkillInputStream(float.NaN, .65f, .45f, null, () => true);
        Assert.That(execution.Termination, Is.EqualTo(QteTermination.Failed));
        Assert.That(_manager.IsActive, Is.False);
    }

    [Test]
    public void SkillStream_UsesThreeInputKeys()
    {
        Assert.That(QTEManager.SkillPromptInput(0), Is.EqualTo(DefenseInput.Parry));
        Assert.That(QTEManager.SkillPromptInput(1), Is.EqualTo(DefenseInput.Dodge));
        Assert.That(QTEManager.SkillPromptInput(2), Is.EqualTo(DefenseInput.Jump));
        Assert.That(QTEManager.SkillPromptInput(3), Is.EqualTo(DefenseInput.Parry));
    }
}
