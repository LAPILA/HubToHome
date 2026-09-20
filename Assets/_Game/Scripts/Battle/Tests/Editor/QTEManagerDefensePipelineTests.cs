using System.Collections;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

public class QTEManagerDefensePipelineTests
{
    [TestCase(false)]
    [TestCase(true)]
    public void AttackMotionScope_RestoresBaselineWithoutOverwritingNewSpeedOwner(bool externalChange)
    {
        var actor = new GameObject("AttackMotionScopeTest");
        actor.SetActive(false);
        try
        {
            var animator = actor.AddComponent<Animator>();
            var enemy = actor.AddComponent<EnemyCharacter>();
            animator.speed = 1.25f;
            animator.updateMode = AnimatorUpdateMode.Normal;
            using (var motion = new BattleAttackMotionScope(enemy))
            {
                motion.SetRate(0.5f);
                Assert.That(animator.speed, Is.EqualTo(0.625f));
                Assert.That(animator.updateMode, Is.EqualTo(AnimatorUpdateMode.UnscaledTime));
                if (externalChange) animator.speed = 2f;
            }
            Assert.That(animator.speed, Is.EqualTo(externalChange ? 2f : 1.25f));
            Assert.That(animator.updateMode, Is.EqualTo(AnimatorUpdateMode.Normal));
        }
        finally { Object.DestroyImmediate(actor); }
    }

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
    public void CreateDefenseRequest_DefaultsToActiveDefenseWithConfiguredBaseline()
    {
        DefenseQteRequest request = _manager.CreateDefenseRequest(1f, 1f, DefenseRequirement.JumpOnly);

        Assert.That(request.UseTimedGuard, Is.True);
        Assert.That(request.UseActiveDefense, Is.True);
        Assert.That(request.DodgeWindow, Is.EqualTo(0.22f));
        Assert.That(request.CounterWindow, Is.EqualTo(0.18f));
        Assert.That(request.GuardDuration, Is.EqualTo(0.4f));
        Assert.That(request.GuardDamageMultiplier, Is.EqualTo(0.5f));
        Assert.That(request.TimingProfile.PerfectWindow, Is.EqualTo(0.12f));
        Assert.That(request.Requirement, Is.EqualTo(DefenseRequirement.JumpOnly),
            "Timed guard must preserve authored requirement data for the legacy path.");
    }

    [Test]
    public void CreateDefenseRequest_ExplicitLegacySettingPreservesOldInputContract()
    {
        SetPrivateField(_manager, "_useActiveDefense", false);
        SetPrivateField(_manager, "_useTimedGuard", false);

        DefenseQteRequest request = _manager.CreateDefenseRequest(1f, 1f, DefenseRequirement.JumpOnly);

        Assert.That(request.UseTimedGuard, Is.False);
        Assert.That(request.UseActiveDefense, Is.False);
        Assert.That(DefenseJudgementPolicy.Matches(request.Requirement, DefenseInput.Jump), Is.True);
        Assert.That(DefenseJudgementPolicy.Matches(request.Requirement, DefenseInput.Parry), Is.False);
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
    public void ReplacementDuringClose_KeepsNewestRequestAndCancelsSupersededStart()
    {
        QteExecution original = _manager.StartDefenseQTEWithResult(CreateRequest(10f), _ => { });
        QteExecution newest = null;
        System.Action closeHandler = null;
        closeHandler = () =>
        {
            _manager.DefenseWindowClosed -= closeHandler;
            newest = _manager.StartDefenseQTEWithResult(CreateRequest(10f), _ => { });
        };
        _manager.DefenseWindowClosed += closeHandler;

        QteExecution superseded = _manager.StartDefenseQTEWithResult(CreateRequest(10f), _ => { });

        Assert.That(original.Termination, Is.EqualTo(QteTermination.Cancelled));
        Assert.That(superseded.Termination, Is.EqualTo(QteTermination.Cancelled));
        Assert.That(newest, Is.Not.Null);
        Assert.That(newest.Termination, Is.EqualTo(QteTermination.Running));
        Assert.That(_manager.IsActive, Is.True);
        Assert.That(_manager.Cancel(newest), Is.True, "Only the newest request owns cancellation.");
    }

    [Test]
    public void CancelDuringOpen_DoesNotLeaveRunningHandleOrPublishResult()
    {
        int resultCount = 0;
        _manager.DefenseWindowOpened += _ => _manager.ForceStop();
        _manager.DefenseResolved += _ => resultCount++;

        QteExecution execution = _manager.StartDefenseQTEWithResult(CreateRequest(10f), _ => resultCount++);

        Assert.That(execution.Termination, Is.EqualTo(QteTermination.Cancelled));
        Assert.That(_manager.IsActive, Is.False);
        Assert.That(resultCount, Is.Zero);
    }

    [Test]
    public void DisabledManager_RejectsNewDefenseWithoutStartingCoroutine()
    {
        _manager.enabled = false;
        QteExecution execution = _manager.StartDefenseQTEWithResult(CreateRequest(10f), _ => { });
        Assert.That(execution.Termination, Is.EqualTo(QteTermination.Cancelled));
        Assert.That(_manager.IsActive, Is.False);
    }

    [Test]
    public void DefenseResult_ClosesPreviousWindowBeforeResultStartsAnother()
    {
        SetPrivateField(_manager, "_useActiveDefense", false);
        SetPrivateField(_manager, "_useTimedGuard", false);
        var observed = new System.Collections.Generic.List<string>();
        _manager.DefenseWindowClosed += () => observed.Add("closed");
        _manager.DefenseResolved += _ => observed.Add("resolved");

        QteExecution execution = _manager.StartDefenseQTEWithResult(CreateRequest(1f),
            new ImmediateDefenseInput(), _ => observed.Add("callback"));

        Assert.That(execution.Termination, Is.EqualTo(QteTermination.Completed));
        CollectionAssert.AreEqual(new[] { "closed", "resolved", "callback" }, observed);
    }

    [Test]
    public void InactiveExplicitDefender_CancelsInsteadOfUsingGlobalInput()
    {
        var playerObject = new GameObject("Inactive Defense Target",
            typeof(Rigidbody2D), typeof(Animator), typeof(PlayerController));
        try
        {
            PlayerController controller = playerObject.GetComponent<PlayerController>();
            playerObject.SetActive(false);
            int resultCount = 0;
            QteExecution execution = _manager.StartDefenseQTEWithResult(CreateRequest(10f),
                controller, _ => resultCount++);

            Assert.That(execution.Termination, Is.EqualTo(QteTermination.Cancelled));
            Assert.That(resultCount, Is.Zero);
            Assert.That(_manager.IsActive, Is.False);
        }
        finally
        {
            Object.DestroyImmediate(playerObject);
        }
    }

    [TestCase("_qteZ", "<Gamepad>/buttonSouth")]
    [TestCase("_qteX", "<Gamepad>/buttonEast")]
    [TestCase("_qteC", "<Gamepad>/buttonNorth")]
    public void QteInputAction_PreservesKeyboardAndAddsLogicalGamepadBinding(string fieldName, string path)
    {
        typeof(GameInput).GetMethod("EnsureInitialized", BindingFlags.NonPublic | BindingFlags.Static)
            .Invoke(null, null);
        var action = (UnityEngine.InputSystem.InputAction)typeof(GameInput)
            .GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Static).GetValue(null);
        bool hasGamepad = false;
        bool hasKeyboard = false;
        for (int i = 0; i < action.bindings.Count; i++)
        {
            string bindingPath = action.bindings[i].path;
            hasGamepad |= bindingPath == path;
            hasKeyboard |= bindingPath.StartsWith("<Keyboard>/");
        }

        Assert.That(hasGamepad, Is.True);
        Assert.That(hasKeyboard, Is.True);
    }

    private sealed class ImmediateDefenseInput : IDefenseInputSource
    {
        public bool TryConsumeBufferedDefenseInput(out DefenseInput input, out float inputTime)
        {
            input = DefenseInput.Dodge;
            inputTime = Time.realtimeSinceStartup;
            return true;
        }

        public void PreviewDefenseInput(DefenseInput input) { }
    }

    [Test]
    public void DefenseQte_ConsumesExplicitTargetBufferWithoutBattleManager()
    {
        SetPrivateField(_manager, "_useActiveDefense", false);
        SetPrivateField(_manager, "_useTimedGuard", false);
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
            new DefenseTimingProfile(0.1f, 0.2f, 0.4f),
            useTimedGuard: false);
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
