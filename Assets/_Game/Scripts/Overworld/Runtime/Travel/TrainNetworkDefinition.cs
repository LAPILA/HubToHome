using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(
    fileName = "TrainNetworkDefinition",
    menuName = "HubToHome/Overworld/Travel/Train Network")]
public sealed class TrainNetworkDefinition : ScriptableObject
{
    [SerializeField] private string _networkId = string.Empty;
    [SerializeField] private string _trainSceneName = string.Empty;
    [SerializeField] private RoomDefinition _trainRoom;
    [SerializeField] private string _trainEntrySpawnPointId = string.Empty;
    [SerializeField] private List<TrainStopDefinition> _stops = new List<TrainStopDefinition>();
    [SerializeField] private ActionSequenceAsset _departureSequence;

    public string NetworkId => Normalize(_networkId);
    public string TrainSceneName => Normalize(_trainSceneName);
    public RoomDefinition TrainRoom => _trainRoom;
    public string TrainEntrySpawnPointId => Normalize(_trainEntrySpawnPointId);
    public ActionSequenceAsset DepartureSequence => _departureSequence;
    public IReadOnlyList<TrainStopDefinition> Stops => _stops;

    public void Configure(
        string networkId,
        string trainSceneName,
        RoomDefinition trainRoom,
        string trainEntrySpawnPointId,
        IEnumerable<TrainStopDefinition> stops,
        ActionSequenceAsset departureSequence)
    {
        _networkId = Normalize(networkId);
        _trainSceneName = Normalize(trainSceneName);
        _trainRoom = trainRoom;
        _trainEntrySpawnPointId = Normalize(trainEntrySpawnPointId);
        _stops.Clear();
        if (stops != null)
            _stops.AddRange(stops);
        _departureSequence = departureSequence;
    }

    public bool TryGetStop(string stopId, out TrainStopDefinition stop)
    {
        string normalized = Normalize(stopId);
        for (int i = 0; i < _stops.Count; i++)
        {
            TrainStopDefinition candidate = _stops[i];
            if (candidate != null
                && string.Equals(candidate.StopId, normalized, StringComparison.Ordinal))
            {
                stop = candidate;
                return true;
            }
        }

        stop = null;
        return false;
    }

    public bool Contains(TrainStopDefinition stop)
    {
        if (stop == null)
            return false;

        for (int i = 0; i < _stops.Count; i++)
        {
            if (ReferenceEquals(_stops[i], stop))
                return true;
        }

        return false;
    }

    public bool TryValidateRuntime(out string error)
    {
        if (string.IsNullOrEmpty(NetworkId))
            return Fail("Network ID가 비어 있습니다.", out error);
        if (string.IsNullOrEmpty(TrainSceneName))
            return Fail($"열차 Scene이 비어 있습니다. Network={NetworkId}", out error);
        if (_trainRoom == null || !_trainRoom.IsValid)
            return Fail($"열차 RoomDefinition이 유효하지 않습니다. Network={NetworkId}", out error);
        if (string.IsNullOrEmpty(TrainEntrySpawnPointId))
            return Fail($"열차 진입 Spawn ID가 비어 있습니다. Network={NetworkId}", out error);
        if (_departureSequence == null)
            return Fail($"출발 Sequence가 없습니다. Network={NetworkId}", out error);
        if (_stops == null || _stops.Count == 0)
            return Fail($"정류소가 없습니다. Network={NetworkId}", out error);

        var stopIds = new HashSet<string>(StringComparer.Ordinal);
        var currentFlags = new HashSet<string>(StringComparer.Ordinal);
        for (int i = 0; i < _stops.Count; i++)
        {
            TrainStopDefinition stop = _stops[i];
            if (stop == null)
                return Fail($"null 정류소가 있습니다. Index={i}", out error);
            if (!stop.TryValidateRuntime(out error))
                return false;
            if (!stopIds.Add(stop.StopId))
                return Fail($"중복 Stop ID가 있습니다. Stop={stop.StopId}", out error);
            if (!currentFlags.Add(stop.CurrentStopFlagId))
                return Fail($"중복 현재 정류소 Flag가 있습니다. Flag={stop.CurrentStopFlagId}", out error);
        }

        error = string.Empty;
        return true;
    }

    private static bool Fail(string message, out string error)
    {
        error = message;
        return false;
    }

    private static string Normalize(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
    }
}