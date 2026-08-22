using System;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public sealed class DialogueStateRestoreTests
{
    private GameStateManager _previousStateManager;
    private DialogueManager _previousDialogueManager;
    private GameObject _stateObject;
    private GameObject _managerObject;
    private GameObject _panelObject;
    private GameStateManager _state;
    private DialogueManager _manager;
    private DialogueUI _panel;
    private DialogueData _dialogue;

    [SetUp]
    public void SetUp()
    {
        _previousStateManager = GameStateManager.Instance;
        _previousDialogueManager = DialogueManager.Instance;
        SetStaticInstance(typeof(GameStateManager), null);
        SetStaticInstance(typeof(DialogueManager), null);

        _stateObject = new GameObject("GameStateManager_DialogueStateRestoreTests");
        _state = _stateObject.AddComponent<GameStateManager>();
        SetStaticInstance(typeof(GameStateManager), _state);

        _panelObject = new GameObject("DialoguePanel_DialogueStateRestoreTests");
        _panel = _panelObject.AddComponent<DialogueUI>();

        _managerObject = new GameObject("DialogueManager_DialogueStateRestoreTests");
        _manager = _managerObject.AddComponent<DialogueManager>();
        SetStaticInstance(typeof(DialogueManager), _manager);
        SetPrivateField(_manager, "_overworldPanel", _panel);
        SetPrivateField(_manager, "_cinematicPanel", _panel);

        _dialogue = ScriptableObject.CreateInstance<DialogueData>();
        _dialogue.Nodes.Add(new DialogueNode { DefaultText = "hello" });
    }

    [TearDown]
    public void TearDown()
    {
        UnityEngine.Object.DestroyImmediate(_managerObject);
        UnityEngine.Object.DestroyImmediate(_panelObject);
        UnityEngine.Object.DestroyImmediate(_stateObject);
        UnityEngine.Object.DestroyImmediate(_dialogue);
        SetStaticInstance(typeof(DialogueManager), _previousDialogueManager);
        SetStaticInstance(typeof(GameStateManager), _previousStateManager);
    }

    [TestCase(GameState.Exploration)]
    [TestCase(GameState.Battle)]
    [TestCase(GameState.Cutscene)]
    public void EndDialogue_RestoresStateCapturedAtStart(GameState previous)
    {
        _state.ChangeState(previous);

        _manager.StartDialogue(_dialogue);
        Assert.That(_state.CurrentState, Is.EqualTo(GameState.Dialogue));
        _manager.EndDialogue();

        Assert.That(_state.CurrentState, Is.EqualTo(previous));
    }

    [Test]
    public void EndDialogue_WhenExternalOwnerChangedState_DoesNotOverwriteIt()
    {
        _state.ChangeState(GameState.Cutscene);
        _manager.StartDialogue(_dialogue);

        _state.ChangeState(GameState.Battle);
        _manager.EndDialogue();

        Assert.That(_state.CurrentState, Is.EqualTo(GameState.Battle));
    }

    [Test]
    public void CancelDialogue_ClosesPlaybackWithoutSuccessCallback()
    {
        int completed = 0;
        int cancelled = 0;
        bool started = _manager.TryStartDialogue(
            _dialogue,
            () => completed++,
            () => cancelled++,
            null,
            out int generation);

        bool result = _manager.CancelDialogue(generation);

        Assert.That(started, Is.True);
        Assert.That(result, Is.True);
        Assert.That(_manager.IsPlaying, Is.False);
        Assert.That(completed, Is.Zero);
        Assert.That(cancelled, Is.EqualTo(1));
        Assert.That(_state.CurrentState, Is.EqualTo(GameState.Exploration));
    }

    [Test]
    public void CancelDialogue_WithStaleGeneration_DoesNotCancelNewPlayback()
    {
        Assert.That(_manager.TryStartDialogue(_dialogue, null, null, null, out int first), Is.True);
        Assert.That(_manager.CancelDialogue(first), Is.True);
        Assert.That(_manager.TryStartDialogue(_dialogue, null, null, null, out int second), Is.True);

        bool staleResult = _manager.CancelDialogue(first);

        Assert.That(second, Is.Not.EqualTo(first));
        Assert.That(staleResult, Is.False);
        Assert.That(_manager.IsPlaying, Is.True);
        _manager.CancelDialogue(second);
    }

    [Test]
    public void DialogueRunnerCancel_ClearsBusyWithoutSuccessCallback()
    {
        var runner = new DialogueManagerRunner(_manager);
        runner.Register("test.dialogue", _dialogue);
        int completionCount = 0;

        runner.ShowAndWait("test.dialogue", () => completionCount++);
        runner.Cancel();

        Assert.That(runner.IsBusy, Is.False);
        Assert.That(_manager.IsPlaying, Is.False);
        Assert.That(completionCount, Is.Zero);
        Assert.That(_state.CurrentState, Is.EqualTo(GameState.Exploration));
    }

    [Test]
    public void NameInputCancelImmediate_DoesNotInvokeCompletion()
    {
        var inputObject = new GameObject("NameInputUI_DialogueStateRestoreTests");
        NameInputUI input = inputObject.AddComponent<NameInputUI>();
        int completionCount = 0;

        try
        {
            input.Open(_ => completionCount++);
            input.CancelImmediate();

            Assert.That(completionCount, Is.Zero);
            Assert.That(inputObject.activeSelf, Is.False);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(inputObject);
        }
    }

    [Test]
    public void DialogueBattleNpc_DefaultModePreservesLegacyDialoguePlayback()
    {
        GameObject playerObject = new GameObject("Legacy Dialogue Player", typeof(PlayerController));
        GameObject npcObject = new GameObject("Legacy Dialogue NPC", typeof(DialogueBattleNPC));
        try
        {
            DialogueBattleNPC npc = npcObject.GetComponent<DialogueBattleNPC>();
            PlayerController player = playerObject.GetComponent<PlayerController>();
            var serialized = new SerializedObject(npc);
            serialized.FindProperty("_dialogue").objectReferenceValue = _dialogue;
            serialized.FindProperty("_encounterIdOverride").stringValue = "legacy.dialogue.encounter";
            serialized.FindProperty("_allowEscape").boolValue = false;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            npc.Interact(player);

            Assert.That(_manager.IsPlaying, Is.True);
            Assert.That(serialized.FindProperty("_useStagedEncounter").boolValue, Is.False);
            Assert.That(GetPrivateBool(npc, "_stagedEncounterInProgress"), Is.False);
            DialogueEncounterContext context = GetPrivateField<DialogueEncounterContext>(
                _manager,
                "_encounterContext");
            Assert.That(context, Is.Not.Null);
            Assert.That(context.EncounterIdOverride, Is.EqualTo("legacy.dialogue.encounter"));
            Assert.That(context.DefeatEnemyOnVictory, Is.False);
            Assert.That(context.AllowEscape, Is.False);
            _manager.EndDialogue();
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(npcObject);
            UnityEngine.Object.DestroyImmediate(playerObject);
        }
    }

    [Test]
    public void DialogueBattleNpc_DefaultModeWithoutExplicitIdKeepsLegacyNullEncounterKey()
    {
        GameObject playerObject = new GameObject("Legacy Dialogue Player Without Id", typeof(PlayerController));
        GameObject npcObject = new GameObject("Legacy Dialogue NPC Without Id", typeof(DialogueBattleNPC));
        EnemyData fallbackEnemy = ScriptableObject.CreateInstance<EnemyData>();
        try
        {
            DialogueBattleNPC npc = npcObject.GetComponent<DialogueBattleNPC>();
            PlayerController player = playerObject.GetComponent<PlayerController>();
            var serialized = new SerializedObject(npc);
            serialized.FindProperty("_dialogue").objectReferenceValue = _dialogue;
            SerializedProperty enemies = serialized.FindProperty("_fallbackEncounterEnemies");
            enemies.arraySize = 1;
            enemies.GetArrayElementAtIndex(0).objectReferenceValue = fallbackEnemy;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            npc.Interact(player);

            DialogueEncounterContext context = GetPrivateField<DialogueEncounterContext>(
                _manager,
                "_encounterContext");
            Assert.That(context, Is.Not.Null);
            Assert.That(
                context.EncounterIdOverride,
                Is.Null,
                "기존 일반 대화 전투는 EnemyData ID를 새 글로벌 상태 키로 암묵 승격하면 안 됩니다.");
            _manager.EndDialogue();
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(fallbackEnemy);
            UnityEngine.Object.DestroyImmediate(npcObject);
            UnityEngine.Object.DestroyImmediate(playerObject);
        }
    }

    [Test]
    public void DialogueBattleNpc_DisableCancelsOnlyItsOwnedStagedDialogueAndRestoresState()
    {
        GameObject playerObject = new GameObject("Staged Dialogue Player", typeof(PlayerController));
        GameObject npcObject = new GameObject("Staged Dialogue NPC");
        npcObject.AddComponent<SpriteRenderer>();
        EnemyCharacter npcEnemy = npcObject.AddComponent<EnemyCharacter>();
        DialogueBattleNPC npc = npcObject.AddComponent<DialogueBattleNPC>();
        DialogueData postDialogue = ScriptableObject.CreateInstance<DialogueData>();
        postDialogue.Nodes.Add(new DialogueNode { DefaultText = "post" });
        EnemyData enemy = ScriptableObject.CreateInstance<EnemyData>();
        try
        {
            PlayerController player = playerObject.GetComponent<PlayerController>();
            Assert.That(npcEnemy, Is.Not.Null);
            Assert.That(npcObject.GetComponent<EnemyCharacter>(), Is.SameAs(npcEnemy));
            var serialized = new SerializedObject(npc);
            serialized.FindProperty("_dialogue").objectReferenceValue = _dialogue;
            serialized.FindProperty("_postClashDialogue").objectReferenceValue = postDialogue;
            serialized.FindProperty("_useStagedEncounter").boolValue = true;
            SerializedProperty enemies = serialized.FindProperty("_fallbackEncounterEnemies");
            enemies.arraySize = 1;
            enemies.GetArrayElementAtIndex(0).objectReferenceValue = enemy;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            npc.Interact(player);
            int ownedGeneration = _manager.PlaybackGeneration;

            Assert.That(_manager.IsPlaying, Is.True);
            Assert.That(GetPrivateBool(npc, "_stagedEncounterInProgress"), Is.True);
            Assert.That(npc.CanStartPreemptiveAttack(player), Is.False);

            InvokePrivateLifecycle(npc, "OnDisable");

            Assert.That(_manager.IsPlaying, Is.False, "OnDisable must cancel the NPC-owned dialogue generation.");
            Assert.That(
                _manager.CancelDialogue(ownedGeneration),
                Is.False,
                "The owned generation must already be closed after OnDisable.");
            Assert.That(
                GetPrivateBool(npc, "_stagedEncounterInProgress"),
                Is.False,
                "OnDisable must clear the staged encounter guard.");
            Assert.That(_state.CurrentState, Is.EqualTo(GameState.Exploration));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(enemy);
            UnityEngine.Object.DestroyImmediate(postDialogue);
            UnityEngine.Object.DestroyImmediate(npcObject);
            UnityEngine.Object.DestroyImmediate(playerObject);
        }
    }

    private static void SetPrivateField<T>(DialogueManager target, string fieldName, T value)
    {
        FieldInfo field = typeof(DialogueManager).GetField(
            fieldName,
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null, "Missing private field: " + fieldName);
        field.SetValue(target, value);
    }

    private static void SetStaticInstance(Type type, object value)
    {
        PropertyInfo property = type.GetProperty(
            "Instance",
            BindingFlags.Public | BindingFlags.Static);
        Assert.That(property, Is.Not.Null, "Missing singleton property on " + type.Name);
        property.SetValue(null, value);
    }

    private static bool GetPrivateBool(object target, string fieldName)
    {
        FieldInfo field = target.GetType().GetField(
            fieldName,
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null, "Missing private field: " + fieldName);
        return (bool)field.GetValue(target);
    }

    private static T GetPrivateField<T>(object target, string fieldName)
    {
        FieldInfo field = target.GetType().GetField(
            fieldName,
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null, "Missing private field: " + fieldName);
        return (T)field.GetValue(target);
    }

    private static void InvokePrivateLifecycle(object target, string methodName)
    {
        MethodInfo method = target.GetType().GetMethod(
            methodName,
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(method, Is.Not.Null, "Missing private method: " + methodName);
        method.Invoke(target, null);
    }
}
