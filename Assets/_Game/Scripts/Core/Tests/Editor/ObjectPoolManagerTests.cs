using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public sealed class ObjectPoolManagerTests
{
    private readonly List<GameObject> _ownedObjects = new List<GameObject>();
    private GameObject _managerObject;
    private ObjectPoolManager _manager;

    [SetUp]
    public void SetUp()
    {
        SetSingleton(null);
        _managerObject = new GameObject("ObjectPoolManagerTests.Manager");
        _manager = _managerObject.AddComponent<ObjectPoolManager>();
        SetSingleton(_manager);
        Assert.That(ObjectPoolManager.Instance, Is.SameAs(_manager));
    }

    [TearDown]
    public void TearDown()
    {
        if (_managerObject != null)
            Object.DestroyImmediate(_managerObject);

        for (int i = 0; i < _ownedObjects.Count; i++)
        {
            if (_ownedObjects[i] != null)
                Object.DestroyImmediate(_ownedObjects[i]);
        }

        _ownedObjects.Clear();
        SetSingleton(null);
    }

    [Test]
    public void RegisterPool_DefaultPrewarmCreatesThreeInactiveInstances()
    {
        GameObject prefab = CreatePrefab("DefaultPrewarm");

        _manager.RegisterPool(prefab);

        Assert.That(_manager.transform.childCount, Is.EqualTo(3));
        for (int i = 0; i < _manager.transform.childCount; i++)
            Assert.That(_manager.transform.GetChild(i).gameObject.activeSelf, Is.False);
    }

    [Test]
    public void Spawn_SameNamePrefabsUseIndependentReferencePools()
    {
        GameObject boxPrefab = CreatePrefab("SharedName");
        boxPrefab.AddComponent<BoxCollider2D>();
        GameObject circlePrefab = CreatePrefab("SharedName");
        circlePrefab.AddComponent<CircleCollider2D>();
        _manager.RegisterPool(boxPrefab, 0);
        _manager.RegisterPool(circlePrefab, 0);

        GameObject box = _manager.Spawn(boxPrefab, Vector3.zero, Quaternion.identity);
        GameObject circle = _manager.Spawn(circlePrefab, Vector3.one, Quaternion.identity);
        _manager.Despawn(box);
        _manager.Despawn(circle);

        GameObject reusedBox = _manager.Spawn(boxPrefab, Vector3.zero, Quaternion.identity);
        GameObject reusedCircle = _manager.Spawn(circlePrefab, Vector3.zero, Quaternion.identity);

        Assert.That(reusedBox, Is.SameAs(box));
        Assert.That(reusedCircle, Is.SameAs(circle));
        Assert.That(reusedBox.GetComponent<BoxCollider2D>(), Is.Not.Null);
        Assert.That(reusedBox.GetComponent<CircleCollider2D>(), Is.Null);
        Assert.That(reusedCircle.GetComponent<CircleCollider2D>(), Is.Not.Null);
        Assert.That(reusedCircle.GetComponent<BoxCollider2D>(), Is.Null);
    }

    [Test]
    public void RetentionLimit_IsInspectorSerializable()
    {
        FieldInfo field = typeof(ObjectPoolManager).GetField(
            "_maxRetainedPerPool",
            BindingFlags.Instance | BindingFlags.NonPublic);

        Assert.That(field, Is.Not.Null);
        Assert.That(field.GetCustomAttribute<SerializeField>(), Is.Not.Null);
    }

    [Test]
    public void Despawn_DuplicateReturnDoesNotEnqueueInstanceTwice()
    {
        GameObject prefab = CreatePrefab("DuplicateReturn");
        _manager.RegisterPool(prefab, 0);
        GameObject first = _manager.Spawn(prefab, Vector3.zero, Quaternion.identity);

        _manager.Despawn(first);
        _manager.Despawn(first);

        GameObject reused = _manager.Spawn(prefab, Vector3.zero, Quaternion.identity);
        GameObject next = _manager.Spawn(prefab, Vector3.zero, Quaternion.identity);
        Assert.That(reused, Is.SameAs(first));
        Assert.That(next, Is.Not.SameAs(first));
    }

    [Test]
    public void Despawn_WhenRetentionLimitIsReachedDestroysOverflow()
    {
        SetPrivateField("_maxRetainedPerPool", 1);
        GameObject prefab = CreatePrefab("RetentionLimit");
        _manager.RegisterPool(prefab, 0);
        GameObject retained = _manager.Spawn(prefab, Vector3.zero, Quaternion.identity);
        GameObject overflow = _manager.Spawn(prefab, Vector3.zero, Quaternion.identity);

        _manager.Despawn(retained);
        _manager.Despawn(overflow);

        Assert.That(retained, Is.Not.Null);
        Assert.That(overflow == null, Is.True);
        Assert.That(_manager.transform.childCount, Is.EqualTo(1));
    }

    [Test]
    public void DestroyingManager_DestroysActiveInstanceReparentedOutsidePoolRoot()
    {
        GameObject prefab = CreatePrefab("DdolCleanup");
        _manager.RegisterPool(prefab, 0);
        GameObject spawned = _manager.Spawn(prefab, Vector3.zero, Quaternion.identity);
        GameObject externalParent = CreatePrefab("SceneOwnedParent");
        spawned.transform.SetParent(externalParent.transform);

        Object.DestroyImmediate(_managerObject);

        Assert.That(spawned == null, Is.True);
        _managerObject = null;
        _manager = null;
    }

    [Test]
    public void AudioNormalization_ReusesOriginalVolumeWithoutCumulativeMultiplication()
    {
        GameObject vfx = CreatePrefab("AudioVfx");
        GameObject audioObject = CreatePrefab("AudioSource", vfx.transform);
        AudioSource source = audioObject.AddComponent<AudioSource>();
        source.volume = 0.8f;
        source.spatialBlend = 0.75f;
        source.spread = 90f;
        source.dopplerLevel = 1.5f;

        CharacterVFX.ApplyRuntimeAudioNormalization(vfx, 0.5f, true);
        CharacterVFX.ApplyRuntimeAudioNormalization(vfx, 0.5f, true);

        Assert.That(source.volume, Is.EqualTo(0.4f).Within(0.0001f));
        Assert.That(source.spatialBlend, Is.Zero);
        Assert.That(source.spread, Is.Zero);
        Assert.That(source.dopplerLevel, Is.Zero);

        CharacterVFX.ApplyRuntimeAudioNormalization(vfx, 0.25f, false);

        Assert.That(source.volume, Is.EqualTo(0.2f).Within(0.0001f));
        Assert.That(source.spatialBlend, Is.EqualTo(0.75f).Within(0.0001f));
        Assert.That(source.spread, Is.EqualTo(90f).Within(0.0001f));
        Assert.That(source.dopplerLevel, Is.EqualTo(1.5f).Within(0.0001f));
    }

    private GameObject CreatePrefab(string name, Transform parent = null)
    {
        var gameObject = new GameObject(name);
        if (parent != null)
            gameObject.transform.SetParent(parent);
        _ownedObjects.Add(gameObject);
        return gameObject;
    }

    private void SetPrivateField(string fieldName, object value)
    {
        FieldInfo field = typeof(ObjectPoolManager).GetField(
            fieldName,
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null, $"Missing field: {fieldName}");
        field.SetValue(_manager, value);
    }

    private static void SetSingleton(ObjectPoolManager value)
    {
        PropertyInfo property = typeof(ObjectPoolManager).GetProperty(
            nameof(ObjectPoolManager.Instance),
            BindingFlags.Public | BindingFlags.Static);
        Assert.That(property, Is.Not.Null);
        property.SetValue(null, value);
    }

}
