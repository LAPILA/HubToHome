using NUnit.Framework;
using UnityEngine;

public sealed class AreaMarkerPersistenceTests
{
    private GameObject _globalObject;
    private GlobalDataManager _global;

    [SetUp]
    public void SetUp()
    {
        _globalObject = new GameObject("AreaMarkerPersistence_GlobalData");
        _globalObject.SetActive(false);
        _global = _globalObject.AddComponent<GlobalDataManager>();
    }

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(_globalObject);
    }

    [Test]
    public void OneShotMarkerWithoutExplicitFlagRestoresAsCompleted()
    {
        GameObject firstObject = new GameObject("First Marker");
        AreaMarkerPersistenceProbe first = firstObject.AddComponent<AreaMarkerPersistenceProbe>();
        first.GlobalData = _global;
        first.Configure("room.alpha", "item.unique", true);

        first.CompleteMarker();
        SaveData save = _global.ToSaveData();
        Object.DestroyImmediate(firstObject);
        _global.FromSaveData(save);

        GameObject restoredObject = new GameObject("Restored Marker");
        AreaMarkerPersistenceProbe restored =
            restoredObject.AddComponent<AreaMarkerPersistenceProbe>();
        restored.GlobalData = _global;
        restored.Configure("room.alpha", "item.unique", true);

        try
        {
            Assert.That(restored.CanInteract(), Is.False);
            Assert.That(restored.IsCompleted, Is.True);
            Assert.That(restoredObject.activeSelf, Is.False);
        }
        finally
        {
            Object.DestroyImmediate(restoredObject);
        }
    }

    [Test]
    public void CompletionKeyKeepsDifferentAreasIndependent()
    {
        GameObject firstObject = new GameObject("Room A Marker");
        AreaMarkerPersistenceProbe first = firstObject.AddComponent<AreaMarkerPersistenceProbe>();
        first.GlobalData = _global;
        first.Configure("room.alpha", "shared.marker", true);
        first.CompleteMarker();

        GameObject otherObject = new GameObject("Room B Marker");
        AreaMarkerPersistenceProbe other = otherObject.AddComponent<AreaMarkerPersistenceProbe>();
        other.GlobalData = _global;
        other.Configure("room.beta", "shared.marker", true);

        try
        {
            Assert.That(other.CanInteract(), Is.True);
            Assert.That(other.IsCompleted, Is.False);
            Assert.That(otherObject.activeSelf, Is.True);
        }
        finally
        {
            Object.DestroyImmediate(firstObject);
            Object.DestroyImmediate(otherObject);
        }
    }

    [Test]
    public void LegacyExplicitCompletionFlagMigratesToAutomaticMarkerState()
    {
        _global.SetFlag("legacy.item.collected", 1);
        GameObject markerObject = new GameObject("Legacy Marker");
        AreaMarkerPersistenceProbe marker =
            markerObject.AddComponent<AreaMarkerPersistenceProbe>();
        marker.GlobalData = _global;
        marker.Configure(
            "room.legacy",
            "legacy.marker",
            true,
            "legacy.item.collected");

        try
        {
            Assert.That(marker.CanInteract(), Is.False);
            Assert.That(marker.IsCompleted, Is.True);
            Assert.That(markerObject.activeSelf, Is.False);
        }
        finally
        {
            Object.DestroyImmediate(markerObject);
        }
    }
}

public sealed class AreaMarkerPersistenceProbe : AreaMarkerBase
{
    public GlobalDataManager GlobalData { get; set; }

    public void Configure(
        string configuredAreaId,
        string configuredMarkerId,
        bool oneShot,
        string completionFlag = "")
    {
        areaId = configuredAreaId;
        markerId = configuredMarkerId;
        isOneShot = oneShot;
        setFlagOnComplete = completionFlag;
    }

    protected override GlobalDataManager ResolveGlobalData()
    {
        return GlobalData;
    }
}
