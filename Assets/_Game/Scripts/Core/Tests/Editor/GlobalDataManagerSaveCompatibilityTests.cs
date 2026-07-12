using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public class GlobalDataManagerSaveCompatibilityTests
{
    private GlobalDataManager _previousInstance;
    private GameObject _gameObject;
    private GlobalDataManager _global;

    [SetUp]
    public void SetUp()
    {
        _previousInstance = GlobalDataManager.Instance;
        SetInstance(null);
        _gameObject = new GameObject("GlobalDataManagerSaveCompatibilityTests");
        _global = _gameObject.AddComponent<GlobalDataManager>();
    }

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(_gameObject);
        SetInstance(_previousInstance);
    }

    [Test]
    public void FromSaveDataAcceptsMissingOptionalCollections()
    {
        var data = new SaveData
        {
            currentScene = " ",
            InventoryDict = null,
            eventFlags = null,
            EncounterMemory = null,
            PartyData = null
        };

        Assert.DoesNotThrow(() => _global.FromSaveData(data));
        Assert.That(_global.SpawnScene, Is.EqualTo(SceneName.Overworld));
        Assert.That(_global.GetInventory(), Is.Empty);
        Assert.That(_global.Party, Is.Empty);
    }

    [Test]
    public void FromSaveDataIgnoresNullInput()
    {
        string originalScene = _global.SpawnScene;

        Assert.DoesNotThrow(() => _global.FromSaveData(null));
        Assert.That(_global.SpawnScene, Is.EqualTo(originalScene));
    }

    private static void SetInstance(GlobalDataManager instance)
    {
        PropertyInfo property = typeof(GlobalDataManager).GetProperty(
            nameof(GlobalDataManager.Instance),
            BindingFlags.Public | BindingFlags.Static);
        property.SetValue(null, instance);
    }
}

