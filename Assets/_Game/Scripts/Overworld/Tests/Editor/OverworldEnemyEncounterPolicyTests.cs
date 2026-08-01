using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public sealed class OverworldEnemyEncounterPolicyTests
{
    private GameObject _enemyObject;
    private GameObject _globalObject;
    private OverworldEnemy _enemy;
    private GlobalDataManager _global;
    private BattleScenarioData _scenario;

    [SetUp]
    public void SetUp()
    {
        _enemyObject = new GameObject(
            "OverworldEnemyEncounterPolicyTests_Enemy",
            typeof(BoxCollider2D),
            typeof(Rigidbody2D),
            typeof(EnemyCharacter),
            typeof(OverworldEnemy));
        _enemy = _enemyObject.GetComponent<OverworldEnemy>();
        _globalObject = new GameObject("OverworldEnemyEncounterPolicyTests_Global");
        _global = _globalObject.AddComponent<GlobalDataManager>();
        _scenario = ScriptableObject.CreateInstance<BattleScenarioData>();
    }

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(_enemyObject);
        Object.DestroyImmediate(_globalObject);
        Object.DestroyImmediate(_scenario);
    }

    [Test]
    public void RecordedVictory_UsesScenarioMemoryKeyInsteadOfOverworldObjectId()
    {
        SetString("_enemyId", "world.enemy.instance");
        _scenario.MemoryKey = "scenario.encounter.memory";
        SetObject("_battleScenarioData", _scenario);
        _global.MarkEncounterDefeated("scenario.encounter.memory");

        bool recordedVictory = InvokeRecordedVictory(_global);

        Assert.That(_enemy.EncounterMemoryKey, Is.EqualTo("scenario.encounter.memory"));
        Assert.That(recordedVictory, Is.True);
        Assert.That(_global.TryGetEncounterMemory("world.enemy.instance", out _), Is.False);
    }

    [TestCase(0, 0, false)]
    [TestCase(1, 0, true)]
    [TestCase(0, 2, true)]
    [TestCase(1, 1, false)]
    public void InstantVictoryPersistence_IsIndependentWhenExplicitlyConfigured(
        int victoryHandling,
        int instantHandling,
        bool expectedPermanentDefeat)
    {
        SerializedObject serialized = new SerializedObject(_enemy);
        serialized.FindProperty("_victoryHandling").enumValueIndex = victoryHandling;
        serialized.FindProperty("_instantVictoryHandling").enumValueIndex = instantHandling;
        serialized.ApplyModifiedPropertiesWithoutUndo();

        Assert.That(_enemy.InstantVictoryDefeatsPermanently, Is.EqualTo(expectedPermanentDefeat));
    }

    private bool InvokeRecordedVictory(GlobalDataManager global)
    {
        MethodInfo method = typeof(OverworldEnemy).GetMethod(
            "HasRecordedEncounterVictory",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(method, Is.Not.Null);
        return (bool)method.Invoke(_enemy, new object[] { global });
    }

    private void SetString(string propertyName, string value)
    {
        SerializedObject serialized = new SerializedObject(_enemy);
        SerializedProperty property = serialized.FindProperty(propertyName);
        Assert.That(property, Is.Not.Null, propertyName);
        property.stringValue = value;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private void SetObject(string propertyName, Object value)
    {
        SerializedObject serialized = new SerializedObject(_enemy);
        SerializedProperty property = serialized.FindProperty(propertyName);
        Assert.That(property, Is.Not.Null, propertyName);
        property.objectReferenceValue = value;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }
}