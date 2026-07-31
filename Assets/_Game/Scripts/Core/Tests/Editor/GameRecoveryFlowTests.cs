using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

public sealed class GameRecoveryFlowTests
{
    [UnityTest]
    public IEnumerator ContinueLoad_ActivatesSavedSceneAndRollsBackRejectedTransitions()
    {
        yield return new EnterPlayMode();
        yield return null;

        string directory = Path.Combine(
            Path.GetTempPath(),
            "HubToHome-GameRecoveryFlowTests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);

        FieldInfo storageField = typeof(SaveManager).GetField(
            "_storage",
            BindingFlags.Static | BindingFlags.NonPublic);
        Assert.That(storageField, Is.Not.Null);
        AtomicSaveStorage previousStorage = (AtomicSaveStorage)storageField.GetValue(null);
        storageField.SetValue(
            null,
            new AtomicSaveStorage(directory, new SystemSaveFileSystem(), new SaveDataCodec()));

        GlobalDataManager global = GlobalDataManager.Instance;
        GameObject globalObject = null;
        if (global == null)
        {
            globalObject = new GameObject("GameRecoveryFlowTests_GlobalData");
            global = globalObject.AddComponent<GlobalDataManager>();
        }

        SaveData originalData = global.ToSaveData();
        SceneLoader previousLoader = SceneLoader.Instance;
        GameObject loaderObject = null;
        float originalTimeScale = Time.timeScale;

        try
        {
            SetSceneLoaderInstance(null);
            loaderObject = new GameObject("GameRecoveryFlowTests_SceneLoader");
            SceneLoaderTestDouble loader = loaderObject.AddComponent<SceneLoaderTestDouble>();
            loader.SceneIsLoadable = true;

            SaveData loadedData = CreateSave("loaded-player", "TestMap", 42);
            SaveStorageResult saveResult = SaveManager.TrySave(loadedData, 0);
            Assert.That(saveResult.Success, Is.True, saveResult.Message);

            global.PlayerName = "live-before-continue";
            global.SetFlag("recovery.flow.flag", 7);

            GameLoadStartResult started = GameLoadCoordinator.LoadSlot(0);
            Assert.That(started.Accepted, Is.True, started.Message);
            yield return WaitForCompletion(started.Operation, 600);

            Assert.That(
                SceneLoadResultUtility.WasDestinationActivated(started.Operation.Result),
                Is.True,
                started.Operation.Result.ToString());
            Assert.That(global.PlayerName, Is.EqualTo("loaded-player"));
            Assert.That(global.GetFlag("recovery.flow.flag"), Is.EqualTo(42));
            Assert.That(global.GetItemCount("test.item"), Is.EqualTo(3));
            Assert.That(global.Party, Has.Count.EqualTo(1));
            Assert.That(global.Party[0].CharacterDataID, Is.EqualTo("hero"));
            Assert.That(GameLoadCoordinator.IsLoading, Is.False);

            SetPrivateField(loader, "_isLoading", true);
            global.PlayerName = "busy-request-must-not-apply";
            GameLoadStartResult busy = GameLoadCoordinator.LoadSlot(0);
            Assert.That(busy.Accepted, Is.False);
            Assert.That(busy.Failure, Is.EqualTo(GameLoadStartFailure.Busy));
            Assert.That(global.PlayerName, Is.EqualTo("busy-request-must-not-apply"));
            SetPrivateField(loader, "_isLoading", false);

            SaveStorageResult rejectedSave = SaveManager.TrySave(
                CreateSave("must-be-rolled-back", "TestMap", 99),
                1);
            Assert.That(rejectedSave.Success, Is.True, rejectedSave.Message);

            global.PlayerName = "rollback-source";
            global.SetFlag("recovery.flow.flag", 11);
            Time.timeScale = 0.75f;
            loader.ReturnNullLoadOperation = true;

            GameLoadStartResult rejected = GameLoadCoordinator.LoadSlot(1);
            Assert.That(rejected.Accepted, Is.False);
            Assert.That(rejected.Failure, Is.EqualTo(GameLoadStartFailure.NoLoadableScene));
            Assert.That(global.PlayerName, Is.EqualTo("rollback-source"));
            Assert.That(global.GetFlag("recovery.flow.flag"), Is.EqualTo(11));
            Assert.That(Time.timeScale, Is.EqualTo(0.75f).Within(0.001f));
            Assert.That(GameLoadCoordinator.IsLoading, Is.False);
        }
        finally
        {
            Time.timeScale = originalTimeScale;
            global.FromSaveData(originalData);
            storageField.SetValue(null, previousStorage);
            SetSceneLoaderInstance(previousLoader);
            if (loaderObject != null)
                UnityEngine.Object.Destroy(loaderObject);
            if (globalObject != null)
                UnityEngine.Object.Destroy(globalObject);
            if (Directory.Exists(directory))
                Directory.Delete(directory, true);
        }

        yield return null;
        yield return new ExitPlayMode();
    }

    [UnityTest]
    public IEnumerator FatalHazard_ShowsGameOverAndRestoresItAfterAsyncTitleFailure()
    {
        yield return new EnterPlayMode();
        yield return null;

        SceneLoader previousLoader = SceneLoader.Instance;
        GameObject loaderObject = null;
        GameObject markerObject = null;
        GameObject playerObject = null;
        GameOverUI gameOver = null;

        try
        {
            SetSceneLoaderInstance(null);
            loaderObject = new GameObject(
                "GameRecoveryFlowTests_FailingSceneLoader",
                typeof(CanvasGroup));
            CanvasGroup fadeCanvas = loaderObject.GetComponent<CanvasGroup>();
            fadeCanvas.alpha = 0f;
            SceneLoaderTestDouble loader = loaderObject.AddComponent<SceneLoaderTestDouble>();
            loader.SceneIsLoadable = true;
            loader.ReturnNullLoadOperation = true;
            SetPrivateField(loader, "_fadeCanvas", fadeCanvas);

            markerObject = new GameObject("GameRecoveryFlowTests_Hazard");
            markerObject.AddComponent<CircleCollider2D>().isTrigger = true;
            HazardMarker marker = markerObject.AddComponent<HazardMarker>();
            marker.SetRuntimeServices(new LethalHealthService(), new FixedTimeSource());

            playerObject = new GameObject(
                "GameRecoveryFlowTests_Player",
                typeof(Rigidbody2D),
                typeof(Animator),
                typeof(PlayerController));
            PlayerController player = playerObject.GetComponent<PlayerController>();

            Assert.That(marker.TryApplyHazard(player), Is.True);
            yield return null;

            gameOver = GameOverUI.Instance;
            Assert.That(gameOver, Is.Not.Null);
            Assert.That(gameOver.IsVisible, Is.True);

            InvokePrivate(gameOver, "ReturnToTitle");
            yield return new WaitForSecondsRealtime(0.65f);

            CanvasGroup gameOverCanvas = GetPrivateField<CanvasGroup>(gameOver, "_canvasGroup");
            Assert.That(gameOver.IsVisible, Is.True);
            Assert.That(gameOverCanvas.alpha, Is.EqualTo(1f).Within(0.001f));
            Assert.That(gameOverCanvas.blocksRaycasts, Is.True);
            Assert.That(gameOverCanvas.interactable, Is.True);
        }
        finally
        {
            SetSceneLoaderInstance(previousLoader);
            if (gameOver != null)
                UnityEngine.Object.Destroy(gameOver.gameObject);
            if (playerObject != null)
                UnityEngine.Object.Destroy(playerObject);
            if (markerObject != null)
                UnityEngine.Object.Destroy(markerObject);
            if (loaderObject != null)
                UnityEngine.Object.Destroy(loaderObject);
        }

        yield return null;
        yield return new ExitPlayMode();
    }

    private static SaveData CreateSave(string playerName, string sceneName, int flagValue)
    {
        return new SaveData
        {
            playerName = playerName,
            currentScene = sceneName,
            spawnPointId = "qa.testmap.spawn.origin",
            eventFlags = new Dictionary<string, int>
            {
                ["recovery.flow.flag"] = flagValue
            },
            InventoryDict = new Dictionary<string, int>
            {
                ["test.item"] = 3
            },
            PartyData = new List<CharacterSaveData>
            {
                new CharacterSaveData
                {
                    CharacterDataID = "hero",
                    CharacterID = "Hero",
                    Level = 3,
                    HP = 18,
                    MaxHP = 20,
                    AP = 7,
                    MaxAP = 10
                }
            }
        };
    }

    private static IEnumerator WaitForCompletion(SceneLoadOperation operation, int maxFrames)
    {
        Assert.That(operation, Is.Not.Null);
        int frame = 0;
        while (!operation.IsDone && frame++ < maxFrames)
            yield return null;

        Assert.That(operation.IsDone, Is.True, "Scene load operation did not complete in time.");
    }

    private static void SetSceneLoaderInstance(SceneLoader instance)
    {
        PropertyInfo property = typeof(SceneLoader).GetProperty(
            nameof(SceneLoader.Instance),
            BindingFlags.Public | BindingFlags.Static);
        Assert.That(property, Is.Not.Null);
        property.SetValue(null, instance);
    }

    private static void SetPrivateField(object target, string fieldName, object value)
    {
        FieldInfo field = target.GetType().BaseType?.GetField(
            fieldName,
            BindingFlags.Instance | BindingFlags.NonPublic)
            ?? target.GetType().GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null, fieldName);
        field.SetValue(target, value);
    }

    private static T GetPrivateField<T>(object target, string fieldName)
    {
        FieldInfo field = target.GetType().GetField(
            fieldName,
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null, fieldName);
        return (T)field.GetValue(target);
    }

    private static void InvokePrivate(object target, string methodName)
    {
        MethodInfo method = target.GetType().GetMethod(
            methodName,
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(method, Is.Not.Null, methodName);
        method.Invoke(target, null);
    }

    private sealed class FixedTimeSource : IOverworldTimeSource
    {
        public float UnscaledTime => 0f;
    }

    private sealed class LethalHealthService : IOverworldPartyHealthService
    {
        public OverworldPartyDamageResult ApplyDamage(
            int damage,
            PlayerCharacter scenePlayer = null)
        {
            return new OverworldPartyDamageResult(
                OverworldPartyDamageStatus.Applied,
                damage,
                damage,
                damage,
                0);
        }
    }
}