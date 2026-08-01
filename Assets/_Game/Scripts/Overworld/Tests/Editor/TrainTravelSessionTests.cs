using System;
using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

public sealed class TrainTravelSessionTests
{
    private sealed class DepartureRunner : ITrainDepartureSequenceRunner
    {
        public ActionSequenceAsset Sequence { get; set; }
        public bool IsPlaying => Completion != null;
        public Action<ActionExecutionResult> Completion { get; private set; }
        public int StartCount { get; private set; }

        public bool TryPlay(Action<ActionExecutionResult> onFinished)
        {
            StartCount++;
            Completion = onFinished;
            return true;
        }

        public void Complete(ActionExecutionResult result)
        {
            Action<ActionExecutionResult> callback = Completion;
            Completion = null;
            callback?.Invoke(result);
        }
    }

    private sealed class TransitionRequester : ITrainTransitionRequester
    {
        public bool Accept { get; set; } = true;
        public MapTransitionRequest Request { get; private set; }
        public Action<SceneLoadResult> Completion { get; private set; }

        public bool TryRequest(
            MapTransitionRequest request,
            PlayerController player,
            Action<SceneLoadResult> onCompleted)
        {
            Request = request;
            Completion = onCompleted;
            return Accept;
        }

        public void Complete(SceneLoadResult result)
        {
            Action<SceneLoadResult> callback = Completion;
            Completion = null;
            callback?.Invoke(result);
        }
    }

    private sealed class Feedback : ITrainTravelFeedback
    {
        public int ShowCount { get; private set; }

        public bool Show(UnityEngine.Object owner, DialogueData dialogue, string fallbackText)
        {
            ShowCount++;
            return true;
        }
    }

    private sealed class AllowAllStops : ITrainStopAccessPolicy
    {
        public bool IsUnlocked(TrainStopDefinition stop) => true;
    }

    private GlobalDataManager _previousGlobal;
    private GameObject _globalObject;
    private GlobalDataManager _global;
    private TrainNetworkDefinition _network;
    private TrainStopDefinition _showcase;
    private TrainStopDefinition _wideField;
    private GlobalDataTrainTravelStateStore _stateStore;
    private DepartureRunner _runner;
    private TransitionRequester _transition;
    private Feedback _feedback;
    private TrainTravelSession _session;

    [SetUp]
    public void SetUp()
    {
        _previousGlobal = GlobalDataManager.Instance;
        SetGlobalInstance(null);
        _globalObject = new GameObject(nameof(TrainTravelSessionTests));
        _global = _globalObject.AddComponent<GlobalDataManager>();

        _network = AssetDatabase.LoadAssetAtPath<TrainNetworkDefinition>(
            TravelTrainPaths.Network);
        _showcase = AssetDatabase.LoadAssetAtPath<TrainStopDefinition>(
            TravelTrainPaths.ShowcaseStop);
        _wideField = AssetDatabase.LoadAssetAtPath<TrainStopDefinition>(
            TravelTrainPaths.WideFieldStop);
        Assert.That(_network, Is.Not.Null);
        Assert.That(_showcase, Is.Not.Null);
        Assert.That(_wideField, Is.Not.Null);

        _stateStore = new GlobalDataTrainTravelStateStore(_global);
        _stateStore.ApplyStop(_network, _showcase);
        _runner = new DepartureRunner { Sequence = _network.DepartureSequence };
        _transition = new TransitionRequester();
        _feedback = new Feedback();
        _session = new TrainTravelSession(
            _network,
            _runner,
            _transition,
            _stateStore,
            new AllowAllStops(),
            _feedback,
            0.2f,
            "travel failed");
    }

    [TearDown]
    public void TearDown()
    {
        UnityEngine.Object.DestroyImmediate(_globalObject);
        SetGlobalInstance(_previousGlobal);
    }

    [Test]
    public void LoadFailureRestoresPreviousStopAndDerivedFlags()
    {
        Assert.That(_session.TryTravel(_wideField, null, null), Is.True);
        _runner.Complete(ActionExecutionResult.Succeeded());

        Assert.That(_global.CurrentTrainStopId, Is.EqualTo(TravelTrainIds.WideFieldStop));
        Assert.That(_transition.Request.TargetSceneName, Is.EqualTo("Region_WideField"));
        Assert.That(_transition.Request.TargetRoomId, Is.EqualTo(WideFieldIds.Station));
        Assert.That(_transition.Request.TargetSpawnPointId, Is.EqualTo("from_train"));

        _transition.Complete(SceneLoadResult.LoadFailed);

        Assert.That(_session.IsBusy, Is.False);
        Assert.That(_global.CurrentTrainStopId, Is.EqualTo(TravelTrainIds.ShowcaseStop));
        Assert.That(_global.GetFlag(TravelTrainIds.ShowcaseCurrentFlag), Is.EqualTo(1));
        Assert.That(_global.GetFlag(TravelTrainIds.WideFieldCurrentFlag), Is.Zero);
        Assert.That(_feedback.ShowCount, Is.EqualTo(1));
    }

    [Test]
    public void ActivatedDestinationPreparationFailureKeepsNewStop()
    {
        Assert.That(_session.TryTravel(_wideField, null, null), Is.True);
        _runner.Complete(ActionExecutionResult.Succeeded());
        LogAssert.Expect(
            LogType.Error,
            new Regex("목적 Scene은 활성화됐지만 준비에 실패"));

        _transition.Complete(SceneLoadResult.DestinationPreparationFailed);

        Assert.That(_session.IsBusy, Is.False);
        Assert.That(_global.CurrentTrainStopId, Is.EqualTo(TravelTrainIds.WideFieldStop));
        Assert.That(_global.GetFlag(TravelTrainIds.ShowcaseCurrentFlag), Is.Zero);
        Assert.That(_global.GetFlag(TravelTrainIds.WideFieldCurrentFlag), Is.EqualTo(1));
        Assert.That(_feedback.ShowCount, Is.Zero);
    }

    [Test]
    public void SequenceFailureDoesNotChangeStopOrStartTransition()
    {
        Assert.That(_session.TryTravel(_wideField, null, null), Is.True);

        _runner.Complete(ActionExecutionResult.Failed("cinematic failed"));

        Assert.That(_session.IsBusy, Is.False);
        Assert.That(_transition.Request, Is.Null);
        Assert.That(_global.CurrentTrainStopId, Is.EqualTo(TravelTrainIds.ShowcaseStop));
        Assert.That(_feedback.ShowCount, Is.EqualTo(1));
    }

    [Test]
    public void SelectingCurrentStopDoesNotStartDeparture()
    {
        Assert.That(_session.TryTravel(_showcase, null, null), Is.False);

        Assert.That(_runner.StartCount, Is.Zero);
        Assert.That(_session.IsBusy, Is.False);
        Assert.That(_feedback.ShowCount, Is.EqualTo(1));
    }

    private static void SetGlobalInstance(GlobalDataManager instance)
    {
        PropertyInfo property = typeof(GlobalDataManager).GetProperty(
            nameof(GlobalDataManager.Instance),
            BindingFlags.Public | BindingFlags.Static);
        property.SetValue(null, instance);
    }
}