using UnityEngine;

[DefaultExecutionOrder(-100)]
public sealed class TrainStopStateSynchronizer : MonoBehaviour
{
    [SerializeField] private TrainNetworkDefinition _network;

    private bool _didSynchronizeOnAwake;

    public TrainNetworkDefinition Network => _network;

    public void Configure(TrainNetworkDefinition network)
    {
        _network = network;
    }

    private void Awake()
    {
        if (_didSynchronizeOnAwake)
            return;

        _didSynchronizeOnAwake = true;
        if (!SynchronizeNow(out string error))
            Debug.LogError("[TrainStopStateSynchronizer] " + error, this);
    }

    public bool SynchronizeNow(out string error)
    {
        if (_network == null)
        {
            error = "TrainNetworkDefinition이 없습니다.";
            return false;
        }
        if (!_network.TryValidateRuntime(out error))
            return false;

        GlobalDataManager global = GlobalDataManager.Instance;
        if (global == null)
        {
            error = "GlobalDataManager가 없습니다.";
            return false;
        }

        string currentStopId = global.CurrentTrainStopId;
        if (!string.IsNullOrWhiteSpace(currentStopId)
            && !_network.TryGetStop(currentStopId, out _))
        {
            error = "저장된 현재 정류소가 Network에 없습니다. Stop=" + currentStopId;
            return false;
        }

        new GlobalDataTrainTravelStateStore(global).Synchronize(_network);
        error = string.Empty;
        return true;
    }
}