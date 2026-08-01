using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

public sealed class TrainTravelStateSnapshot
{
    internal TrainTravelStateSnapshot(
        TrainNetworkDefinition network,
        string stopId,
        IDictionary<string, int> derivedFlagValues)
    {
        Network = network;
        StopId = Normalize(stopId);
        var copy = new Dictionary<string, int>(StringComparer.Ordinal);
        if (derivedFlagValues != null)
        {
            foreach (KeyValuePair<string, int> entry in derivedFlagValues)
                copy[entry.Key] = entry.Value;
        }

        DerivedFlagValues = new ReadOnlyDictionary<string, int>(copy);
    }

    internal TrainNetworkDefinition Network { get; }
    public string StopId { get; }
    public IReadOnlyDictionary<string, int> DerivedFlagValues { get; }

    private static string Normalize(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
    }
}

public interface ITrainTravelStateStore
{
    TrainTravelStateSnapshot Capture(TrainNetworkDefinition network);
    void ApplyStop(TrainNetworkDefinition network, TrainStopDefinition stop);
    void Restore(TrainTravelStateSnapshot snapshot);
    void Synchronize(TrainNetworkDefinition network);
}

public sealed class GlobalDataTrainTravelStateStore : ITrainTravelStateStore
{
    private readonly GlobalDataManager _global;

    public GlobalDataTrainTravelStateStore(GlobalDataManager global)
    {
        _global = global != null
            ? global
            : throw new ArgumentNullException(nameof(global));
    }

    public TrainTravelStateSnapshot Capture(TrainNetworkDefinition network)
    {
        RequireNetwork(network);
        var values = new Dictionary<string, int>(StringComparer.Ordinal);
        IReadOnlyList<TrainStopDefinition> stops = network.Stops;
        for (int i = 0; i < stops.Count; i++)
        {
            string flagId = stops[i].CurrentStopFlagId;
            values[flagId] = _global.GetFlag(flagId, 0);
        }

        return new TrainTravelStateSnapshot(
            network,
            _global.CurrentTrainStopId,
            values);
    }

    public void ApplyStop(
        TrainNetworkDefinition network,
        TrainStopDefinition stop)
    {
        RequireNetwork(network);
        if (!network.Contains(stop))
            throw new ArgumentException("정류소가 Network에 등록되지 않았습니다.", nameof(stop));

        SetAllDerivedFlags(network, stop);
        _global.CurrentTrainStopId = stop.StopId;
    }

    public void Restore(TrainTravelStateSnapshot snapshot)
    {
        if (snapshot == null)
            throw new ArgumentNullException(nameof(snapshot));

        _global.CurrentTrainStopId = snapshot.StopId;
        foreach (KeyValuePair<string, int> entry in snapshot.DerivedFlagValues)
            _global.SetFlag(entry.Key, entry.Value);
    }

    public void Synchronize(TrainNetworkDefinition network)
    {
        RequireNetwork(network);
        network.TryGetStop(_global.CurrentTrainStopId, out TrainStopDefinition currentStop);
        SetAllDerivedFlags(network, currentStop);
    }

    private void SetAllDerivedFlags(
        TrainNetworkDefinition network,
        TrainStopDefinition currentStop)
    {
        IReadOnlyList<TrainStopDefinition> stops = network.Stops;
        for (int i = 0; i < stops.Count; i++)
        {
            TrainStopDefinition stop = stops[i];
            _global.SetFlag(
                stop.CurrentStopFlagId,
                ReferenceEquals(stop, currentStop) ? 1 : 0);
        }
    }

    private static void RequireNetwork(TrainNetworkDefinition network)
    {
        if (network == null)
            throw new ArgumentNullException(nameof(network));
        if (!network.TryValidateRuntime(out string error))
            throw new InvalidOperationException(error);
    }
}