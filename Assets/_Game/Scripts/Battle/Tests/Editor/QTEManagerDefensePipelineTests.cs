using System.Collections;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

public class QTEManagerDefensePipelineTests
{
    private const string TelegraphPath = "Assets/_Game/Presentation/Custom_VFX/Prefabs/Telegraph.prefab";

    [Test]
    public void TelegraphAsset_StartStateAnimatesThePrefabRenderer()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(TelegraphPath);
        Assert.That(prefab, Is.Not.Null);
        var controller = prefab.GetComponent<Animator>().runtimeAnimatorController as UnityEditor.Animations.AnimatorController;
        Assert.That(controller, Is.Not.Null);
        Assert.That(AssetDatabase.GetAssetPath(controller), Does.EndWith("Telegraph.aseprite"));
        var state = controller.layers[0].stateMachine.defaultState;
        Assert.That(state.name, Is.EqualTo("START"));
        var clip = state.motion as AnimationClip;
        Assert.That(clip, Is.Not.Null);
        Assert.That(clip.length, Is.InRange(0.22f, 0.25f));
        var bindings = AnimationUtility.GetObjectReferenceCurveBindings(clip);
        Assert.That(bindings.Length, Is.GreaterThan(0));
        foreach (EditorCurveBinding binding in bindings)
        {
            Assert.That(binding.path, Is.Empty, "단일 레이어 전조는 루트 SpriteRenderer를 사용합니다.");
            Assert.That(binding.type, Is.EqualTo(typeof(SpriteRenderer)));
            var frames = AnimationUtility.GetObjectReferenceCurve(clip, binding);
            Assert.That(frames.Length, Is.GreaterThanOrEqualTo(5));
            foreach (ObjectReferenceKeyframe frame in frames)
                Assert.That(frame.value, Is.Not.Null);
        }
        var settings = new SerializedObject(prefab.GetComponent<BattleTelegraphCue>());
        Assert.That(settings.FindProperty("_animationState").stringValue, Is.EqualTo("START"));
        Assert.That(settings.FindProperty("_worldOffset").vector3Value, Is.EqualTo(Vector3.zero));
        Assert.That(prefab.transform.localScale, Is.EqualTo(Vector3.one * 3f));
        Assert.That(settings.FindProperty("_sortingOrderOffset").intValue, Is.EqualTo(-1));
        Assert.That(settings.FindProperty("_pingClip").objectReferenceValue, Is.Not.Null);
    }

    [TestCase(0.10f)]
    [TestCase(0.30f)]
    public void TelegraphPlayback_UsesDefenseClockAndRestartsWithoutExtraScaling(float window)
    {
        GameObject instance = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(TelegraphPath));
        var target = new GameObject("TelegraphTarget", typeof(SpriteRenderer));
        try
        {
            BattleTelegraphCue cue = instance.GetComponent<BattleTelegraphCue>();
            cue.SendMessage("Awake");
            // 정지된 Animator의 state 길이에 의존하지 않고 클립을 평가해야 합니다.
            instance.GetComponent<Animator>().speed = 0f;
            SetPrivateField(cue, "_pingClip", null);
            SetPrivateField(cue, "_leased", true);
            var play = typeof(BattleTelegraphCue).GetMethod("Play", BindingFlags.Instance | BindingFlags.NonPublic);
            target.transform.position = new Vector3(4f, 7f, 0f);
            play.Invoke(cue, new object[] { target.transform, window, true, true });
            Assert.That(instance.transform.position, Is.EqualTo(target.transform.position));
            SpriteRenderer renderer = instance.GetComponent<SpriteRenderer>();
            Sprite firstFrame = renderer.sprite;
            cue.Emphasize(window);
            cue.SynchronizeToImpact(window * 0.5f);
            Sprite middleFrame = renderer.sprite;
            Assert.That(middleFrame, Is.Not.EqualTo(firstFrame));
            cue.SynchronizeToImpact(window * 0.5f);
            Assert.That(renderer.sprite, Is.EqualTo(middleFrame), "방어 시계가 멈추면 전조도 멈춥니다.");
            cue.Emphasize(window);
            Assert.That(renderer.sprite, Is.EqualTo(middleFrame), "중복 호출은 다시 재생하지 않습니다.");
            cue.SynchronizeToImpact(0f);
            Sprite finalFrame = renderer.sprite;
            cue.SynchronizeToImpact(-1f);
            Assert.That(renderer.sprite, Is.EqualTo(finalFrame));
            Assert.That(instance.transform.localScale, Is.EqualTo(Vector3.one * 3f));
            Assert.That(instance.transform.localRotation, Is.EqualTo(Quaternion.identity));
            Assert.That(renderer.sortingOrder, Is.EqualTo(target.GetComponent<SpriteRenderer>().sortingOrder - 1));
            instance.SetActive(false);
            Assert.That(instance.GetComponent<Animator>().enabled, Is.True);
            instance.SetActive(true);
            SetPrivateField(cue, "_leased", true);
            play.Invoke(cue, new object[] { target.transform, window, false, false });
            Assert.That(renderer.sprite, Is.EqualTo(firstFrame), "풀에서 재사용할 때 첫 프레임으로 복원합니다.");
        }
        finally
        {
            Object.DestroyImmediate(instance);
            Object.DestroyImmediate(target);
        }
    }

    [Test]
    public void TelegraphStandalone_AwakeDoesNotFreezeAnimator()
    {
        GameObject instance = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(TelegraphPath));
        try
        {
            instance.GetComponent<BattleTelegraphCue>().SendMessage("Awake");
            Assert.That(instance.GetComponent<Animator>().enabled, Is.True);
            Assert.That(instance.GetComponent<Animator>().speed, Is.EqualTo(1f));
        }
        finally { Object.DestroyImmediate(instance); }
    }

    [Test]
    public void TelegraphSkillDefaultsAndLabAssets_UseCenter()
    {
        Assert.That(new Action_DefenseWindow().ImpactCuePivotName, Is.EqualTo(CharacterPivotId.Center));
        string[] guids = AssetDatabase.FindAssets("t:SkillData", new[] {
            "Assets/_Game/Content/Maps/Development/BunnySlimeBattleLab/Data/Skills" });
        int checkedCues = 0;
        foreach (string guid in guids)
        {
            SkillData skill = AssetDatabase.LoadAssetAtPath<SkillData>(AssetDatabase.GUIDToAssetPath(guid));
            if (skill.ActionTimeline == null) continue;
            foreach (var block in skill.ActionTimeline)
                if (block is Action_DefenseWindow defense && defense.ImpactCuePrefab != null)
                {
                    Assert.That(defense.ImpactCuePivotName, Is.EqualTo(CharacterPivotId.Center), skill.name);
                    checkedCues++;
                }
        }
        Assert.That(checkedCues, Is.GreaterThanOrEqualTo(12));
    }

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
