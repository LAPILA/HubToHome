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
    private GlobalDataManager _previousGlobal;
    private BattleScenarioData _scenario;

    [SetUp]
    public void SetUp()
    {
        _previousGlobal = GlobalDataManager.Instance;
        _enemyObject = new GameObject(
            "OverworldEnemyEncounterPolicyTests_Enemy",
            typeof(BoxCollider2D),
            typeof(Rigidbody2D),
            typeof(EnemyCharacter),
            typeof(OverworldEnemy));
        _enemy = _enemyObject.GetComponent<OverworldEnemy>();
        _globalObject = new GameObject("OverworldEnemyEncounterPolicyTests_Global");
        _global = _globalObject.AddComponent<GlobalDataManager>();
        SetGlobalInstance(_global);
        _scenario = ScriptableObject.CreateInstance<BattleScenarioData>();
    }

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(_enemyObject);
        Object.DestroyImmediate(_globalObject);
        Object.DestroyImmediate(_scenario);
        SetGlobalInstance(_previousGlobal);
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

    [Test]
    public void DialogueBattleNpc_NewOptionsPreserveLegacyDefaults()
    {
        GameObject npcObject = new GameObject(
            "DialogueBattleNpc_Defaults",
            typeof(SpriteRenderer),
            typeof(BoxCollider2D),
            typeof(DialogueBattleNPC));
        try
        {
            DialogueBattleNPC npc = npcObject.GetComponent<DialogueBattleNPC>();
            var serialized = new SerializedObject(npc);

            Assert.That(serialized.FindProperty("_useStagedEncounter").boolValue, Is.False);
            Assert.That(serialized.FindProperty("_allowEscape").boolValue, Is.True);
            Assert.That(serialized.FindProperty("_defeatOnVictory").boolValue, Is.False);
            Assert.That(serialized.FindProperty("_requireSeamlessBattleHost").boolValue, Is.True);
            Assert.That(npc, Is.InstanceOf<IEncounterOutcomeSource>());
            Assert.That(npc, Is.InstanceOf<IEncounterAbortSource>());
        }
        finally
        {
            Object.DestroyImmediate(npcObject);
        }
    }

    [Test]
    public void DialogueBattleNpc_AbortRestoresCapturedPresentation()
    {
        GameObject playerObject = new GameObject(
            "DialogueBattleNpc_Abort_Player",
            typeof(PlayerController));
        GameObject npcObject = new GameObject(
            "DialogueBattleNpc_Abort",
            typeof(SpriteRenderer),
            typeof(BoxCollider2D),
            typeof(DialogueBattleNPC));
        try
        {
            DialogueBattleNPC npc = npcObject.GetComponent<DialogueBattleNPC>();
            SpriteRenderer renderer = npcObject.GetComponent<SpriteRenderer>();
            BoxCollider2D collider = npcObject.GetComponent<BoxCollider2D>();
            PlayerController player = playerObject.GetComponent<PlayerController>();
            Vector3 originalNpcPosition = new Vector3(4f, -2f, 0f);
            Vector3 originalPlayerPosition = new Vector3(-3f, 1f, 0f);
            npcObject.transform.position = originalNpcPosition;
            playerObject.transform.position = originalPlayerPosition;

            SetDialogueNpcPrivateField(npc, "_stagedPlayer", player);
            InvokeDialogueNpcMethod(npc, "CaptureStagedPose");

            InvokeDialogueNpcMethod(npc, "HideNpcPresentation");
            npcObject.transform.position = new Vector3(9f, 7f, 0f);
            playerObject.transform.position = new Vector3(8f, 7f, 0f);
            Assert.That(renderer.enabled, Is.False);
            Assert.That(collider.enabled, Is.False);

            npc.OnEncounterAborted(null);

            Assert.That(renderer.enabled, Is.True);
            Assert.That(collider.enabled, Is.True);
            Assert.That(npcObject.transform.position, Is.EqualTo(originalNpcPosition));
            Assert.That(playerObject.transform.position, Is.EqualTo(originalPlayerPosition));
            Assert.That(_global.SpawnX, Is.EqualTo(originalPlayerPosition.x));
            Assert.That(_global.SpawnY, Is.EqualTo(originalPlayerPosition.y));
        }
        finally
        {
            Object.DestroyImmediate(npcObject);
            Object.DestroyImmediate(playerObject);
        }
    }

    [Test]
    public void DialogueBattleNpc_VictoryPersistsStableIdAndKeepsPresentationHidden()
    {
        GameObject npcObject = new GameObject(
            "DialogueBattleNpc_Victory",
            typeof(SpriteRenderer),
            typeof(BoxCollider2D),
            typeof(DialogueBattleNPC));
        try
        {
            DialogueBattleNPC npc = npcObject.GetComponent<DialogueBattleNPC>();
            var serialized = new SerializedObject(npc);
            serialized.FindProperty("_encounterIdOverride").stringValue = "chapter01.windmill.zev";
            serialized.FindProperty("_defeatOnVictory").boolValue = true;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            Assert.That(GlobalDataManager.Instance, Is.SameAs(_global));

            npc.OnEncounterResolved(BattleEncounterOutcome.Victory, null);

            Assert.That(
                _global.TryGetOverworldEnemyState("chapter01.windmill.zev", out OverworldEnemyRuntimeState state),
                Is.True,
                "Victory must create the stable overworld encounter state.");
            Assert.That(state.IsDefeated, Is.True, "Victory must mark the stable encounter defeated.");
            Assert.That(
                npcObject.GetComponent<SpriteRenderer>().enabled,
                Is.False,
                "Victory must keep the original NPC renderer hidden.");
            Assert.That(
                npcObject.GetComponent<BoxCollider2D>().enabled,
                Is.False,
                "Victory must keep the original NPC collider disabled.");
        }
        finally
        {
            Object.DestroyImmediate(npcObject);
        }
    }

    private bool InvokeRecordedVictory(GlobalDataManager global)
    {
        MethodInfo method = typeof(OverworldEnemy).GetMethod(
            "HasRecordedEncounterVictory",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(method, Is.Not.Null);
        return (bool)method.Invoke(_enemy, new object[] { global });
    }

    private static void InvokeDialogueNpcMethod(DialogueBattleNPC npc, string methodName)
    {
        MethodInfo method = typeof(DialogueBattleNPC).GetMethod(
            methodName,
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(method, Is.Not.Null, methodName);
        method.Invoke(npc, null);
    }

    private static void SetDialogueNpcPrivateField(
        DialogueBattleNPC npc,
        string fieldName,
        object value)
    {
        FieldInfo field = typeof(DialogueBattleNPC).GetField(
            fieldName,
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null, fieldName);
        field.SetValue(npc, value);
    }

    private static void SetGlobalInstance(GlobalDataManager value)
    {
        PropertyInfo property = typeof(GlobalDataManager).GetProperty(
            "Instance",
            BindingFlags.Public | BindingFlags.Static);
        Assert.That(property, Is.Not.Null);
        property.GetSetMethod(true).Invoke(null, new object[] { value });
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
