using System.Collections;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

public class QTEManagerDefensePipelineTests
{
    private QTEManager _previousInstance;
    private GameObject _gameObject;
    private QTEManager _manager;

    [SetUp]
    public void SetUp()
    {
        _previousInstance = QTEManager.Instance;
        SetInstance(null);
        _gameObject = new GameObject("QTEManagerDefensePipelineTests");
        _manager = _gameObject.AddComponent<QTEManager>();
    }

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(_gameObject);
        SetInstance(_previousInstance);
    }

    [Test]
    public void ForceStop_DoesNotPublishStructuredDefenseResult()
    {
        int eventCount = 0;
        int callbackCount = 0;
        _manager.DefenseResolved += _ => eventCount++;
        QteExecution execution = _manager.StartDefenseQTEWithResult(
            CreateRequest(10f),
            _ => callbackCount++);

        _manager.ForceStop();

        Assert.That(execution.Termination, Is.EqualTo(QteTermination.Cancelled));
        Assert.That(eventCount, Is.Zero);
        Assert.That(callbackCount, Is.Zero);
    }

    [Test]
    public void ForceStop_PublishesCloseAfterExecutionBecomesInactive()
    {
        QteExecution execution = _manager.StartDefenseQTEWithResult(
            CreateRequest(10f),
            _ => { });
        QteTermination observedTermination = QteTermination.Running;
        bool observedIsActive = true;
        _manager.DefenseWindowClosed += () =>
        {
            observedTermination = execution.Termination;
            observedIsActive = _manager.IsActive;
        };

        _manager.ForceStop();

        Assert.That(observedTermination, Is.EqualTo(QteTermination.Cancelled));
        Assert.That(observedIsActive, Is.False);
    }

    [Test]
    public void DefenseQte_ConsumesExplicitTargetBufferWithoutBattleManager()
    {
        var playerObject = new GameObject(
            "Explicit Defense Target",
            typeof(Rigidbody2D),
            typeof(Animator),
            typeof(PlayerController));
        try
        {
            PlayerController controller = playerObject.GetComponent<PlayerController>();
            SetPrivateField(controller, "_bufferedDefenseInput", DefenseInput.Jump);
            SetPrivateField(controller, "_bufferedDefenseInputTime", Time.realtimeSinceStartup);
            DefenseQteResult finalResult = default;

            QteExecution execution = _manager.StartDefenseQTEWithResult(
                CreateRequest(1f),
                controller,
                result => finalResult = result);

            Assert.That(execution.Termination, Is.EqualTo(QteTermination.Completed));
            Assert.That(finalResult.Input, Is.EqualTo(DefenseInput.Jump));
            Assert.That(finalResult.InputStatus, Is.EqualTo(DefenseInputReadStatus.Valid));
        }
        finally
        {
            Object.DestroyImmediate(playerObject);
        }
    }

    private static void SetPrivateField(object target, string fieldName, object value)
    {
        FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null);
        field.SetValue(target, value);
    }


    internal static DefenseQteRequest CreateRequest(float duration)
    {
        return new DefenseQteRequest(
            duration,
            1f,
            DefenseRequirement.Any,
            new DefenseTimingProfile(0.1f, 0.2f, 0.4f));
    }

    internal static void SetInstance(QTEManager instance)
    {
        PropertyInfo property = typeof(QTEManager).GetProperty(
            nameof(QTEManager.Instance),
            BindingFlags.Public | BindingFlags.Static);
        property.SetValue(null, instance);
    }
}

public class QTEManagerDefensePipelinePlayModeTests
{
    private bool _hadBackupScenes;

    [SetUp]
    public void SetUp()
    {
        _hadBackupScenes = Directory.Exists("Temp/__Backupscenes");
    }

    [UnityTearDown]
    public IEnumerator TearDown()
    {
        Time.timeScale = 1f;
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            yield return new ExitPlayMode();

        if (!_hadBackupScenes && Directory.Exists("Temp/__Backupscenes"))
            FileUtil.DeleteFileOrDirectory("Temp/__Backupscenes");
    }

    [UnityTest]
    public IEnumerator DefenseQte_TimeScaleZero_StillTimesOutAndPublishesOneResult()
    {
        yield return new EnterPlayMode();

        QTEManager previousInstance = QTEManager.Instance;
        QTEManagerDefensePipelineTests.SetInstance(null);
        var gameObject = new GameObject("QTEManagerDefensePipelinePlayModeTests");
        QTEManager manager = gameObject.AddComponent<QTEManager>();

        Time.timeScale = 0f;
        int eventCount = 0;
        int callbackCount = 0;
        DefenseQteResult eventResult = default;
        manager.DefenseResolved += result =>
        {
            eventCount++;
            eventResult = result;
        };

        QteExecution execution = manager.StartDefenseQTEWithResult(
            QTEManagerDefensePipelineTests.CreateRequest(0.05f),
            _ => callbackCount++);

        float startedAt = Time.realtimeSinceStartup;
        while (!execution.IsDone && Time.realtimeSinceStartup < startedAt + 0.5f)
            yield return null;

        Assert.That(execution.IsDone, Is.True);
        Assert.That(execution.Termination, Is.EqualTo(QteTermination.TimedOut));
        Assert.That(eventCount, Is.EqualTo(1));
        Assert.That(callbackCount, Is.EqualTo(1));
        Assert.That(eventResult.Outcome, Is.EqualTo(DefenseOutcome.Failure));
        Assert.That(manager.IsActive, Is.False);

        Time.timeScale = 1f;
        Object.Destroy(gameObject);
        yield return null;
        QTEManagerDefensePipelineTests.SetInstance(previousInstance);
        yield return new ExitPlayMode();
    }
}
