using Sirenix.OdinInspector;
using UnityEngine;

public sealed class TrainTravelController : MonoBehaviour
{
    [BoxGroup("Train Travel")]
    [SerializeField] private TrainNetworkDefinition _network;
    [BoxGroup("Train Travel")]
    [SerializeField] private SceneActionSequencePlayer _sequencePlayer;
    [BoxGroup("Train Travel")]
    [SerializeField, Min(0f)] private float _fadeDuration = 0.25f;
    [BoxGroup("Train Travel")]
    [SerializeField, TextArea] private string _requestFailedFallback = "* 열차가 지금은 출발할 수 없다.";

    private TrainTravelSession _session;

    public bool IsBusy => _session != null && _session.IsBusy;
    public TrainNetworkDefinition Network => _network;

    public void Configure(
        TrainNetworkDefinition network,
        SceneActionSequencePlayer sequencePlayer,
        float fadeDuration,
        string requestFailedFallback)
    {
        _network = network;
        _sequencePlayer = sequencePlayer;
        _fadeDuration = Mathf.Max(0f, fadeDuration);
        _requestFailedFallback = requestFailedFallback ?? string.Empty;
        _session = null;
    }

    public bool ContainsDestination(TrainStopDefinition destination)
    {
        return _network != null && _network.Contains(destination);
    }

    public bool TryTravel(
        TrainStopDefinition destination,
        PlayerController player,
        UnityEngine.Object feedbackOwner = null)
    {
        if (!TryEnsureSession(out string error))
        {
            Debug.LogError("[TrainTravelController] " + error, this);
            new AreaMarkerTrainTravelFeedback().Show(
                feedbackOwner != null ? feedbackOwner : this,
                null,
                _requestFailedFallback);
            return false;
        }

        return _session.TryTravel(
            destination,
            player,
            feedbackOwner != null ? feedbackOwner : this);
    }

    public bool TryValidateConfiguration(out string error)
    {
        if (_network == null)
        {
            error = "TrainNetworkDefinition이 없습니다.";
            return false;
        }
        if (!_network.TryValidateRuntime(out error))
            return false;
        if (_sequencePlayer == null)
        {
            error = "SceneActionSequencePlayer가 없습니다.";
            return false;
        }
        if (!ReferenceEquals(_sequencePlayer.Sequence, _network.DepartureSequence))
        {
            error = "SequencePlayer와 Network의 출발 Sequence가 다릅니다.";
            return false;
        }

        error = string.Empty;
        return true;
    }

    private bool TryEnsureSession(out string error)
    {
        if (_session != null)
        {
            error = string.Empty;
            return true;
        }
        if (!TryValidateConfiguration(out error))
            return false;

        GlobalDataManager global = GlobalDataManager.Instance;
        if (global == null)
        {
            error = "GlobalDataManager가 없습니다.";
            return false;
        }

        _session = new TrainTravelSession(
            _network,
            new SceneTrainDepartureSequenceRunner(_sequencePlayer),
            new MapTrainTransitionRequester(),
            new GlobalDataTrainTravelStateStore(global),
            new GlobalDataTrainStopAccessPolicy(global),
            new AreaMarkerTrainTravelFeedback(),
            _fadeDuration,
            _requestFailedFallback);
        error = string.Empty;
        return true;
    }

    private void OnValidate()
    {
        _fadeDuration = Mathf.Max(0f, _fadeDuration);
    }
}