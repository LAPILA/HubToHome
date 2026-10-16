using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public sealed class SaveManagerRestTransactionTests
{
    [TestCase(false)]
    [TestCase(true)]
    public void RuntimeRestIsCommittedOnlyAfterStorageSucceeds(bool storageSucceeds)
    {
        string path = Path.Combine(Path.GetTempPath(), "HTH-Rest-" + Guid.NewGuid().ToString("N"));
        FieldInfo storageField = typeof(SaveManager).GetField("_storage", BindingFlags.Static | BindingFlags.NonPublic);
        object previousStorage = storageField.GetValue(null);
        FieldInfo cacheField = typeof(CharacterDatabase).GetField("_cache", BindingFlags.Static | BindingFlags.NonPublic);
        object previousCache = cacheField.GetValue(null);
        GlobalDataManager previousGlobal = GlobalDataManager.Instance;
        GameStateManager previousState = GameStateManager.Instance;
        BattleManager previousBattle = BattleManager.Instance;
        SceneLoader previousLoader = SceneLoader.Instance;
        GameObject globalObject = null;
        GameObject playerObject = null;
        try
        {
            SetInstance(typeof(GlobalDataManager), null);
            SetInstance(typeof(GameStateManager), null);
            SetInstance(typeof(BattleManager), null);
            SetInstance(typeof(SceneLoader), null);
            cacheField.SetValue(null, new Dictionary<string, CharacterData>(StringComparer.Ordinal));
            if (!storageSucceeds) File.WriteAllText(path, "occupied-by-file");
            storageField.SetValue(null, new AtomicSaveStorage(path));
            globalObject = new GameObject("RestTest.Global");
            GlobalDataManager global = globalObject.AddComponent<GlobalDataManager>();
            SetInstance(typeof(GlobalDataManager), global);
            playerObject = new GameObject("RestTest.Player");
            PlayerController player = playerObject.AddComponent<PlayerController>();
            var member = new CharacterSaveData { CharacterDataID = "rest.test.hero", HP = 5, MaxHP = 140, AP = 2, MaxAP = 30 };
            global.Party.Add(member);
            global.SetOverworldEnemyRespawnPolicy("ordinary", "TestMap", true);
            global.MarkOverworldEnemyDefeated("ordinary", "TestMap");

            SaveStorageResult result = SaveManager.TrySaveProgress(0, player);

            Assert.That(result.Success, Is.EqualTo(storageSucceeds));
            Assert.That(member.HP, Is.EqualTo(storageSucceeds ? 140 : 5));
            Assert.That(member.AP, Is.EqualTo(storageSucceeds ? 30 : 2));
            Assert.That(global.TryGetOverworldEnemyState("ordinary", out OverworldEnemyRuntimeState state), Is.True);
            Assert.That(state.IsDefeated, Is.EqualTo(!storageSucceeds));
            if (storageSucceeds)
            {
                SaveLoadResult loaded = SaveManager.TryLoad(0);
                Assert.That(loaded.Success, Is.True);
                Assert.That(loaded.Data.PartyData[0].HP, Is.EqualTo(140));
                Assert.That(loaded.Data.OverworldEnemies["ordinary"].IsDefeated, Is.False);
            }
        }
        finally
        {
            if (playerObject != null) UnityEngine.Object.DestroyImmediate(playerObject);
            if (globalObject != null) UnityEngine.Object.DestroyImmediate(globalObject);
            storageField.SetValue(null, previousStorage);
            cacheField.SetValue(null, previousCache);
            SetInstance(typeof(GlobalDataManager), previousGlobal);
            SetInstance(typeof(GameStateManager), previousState);
            SetInstance(typeof(BattleManager), previousBattle);
            SetInstance(typeof(SceneLoader), previousLoader);
            if (File.Exists(path)) File.Delete(path);
            if (Directory.Exists(path)) Directory.Delete(path, true);
        }
    }

    private static void SetInstance(Type owner, object value)
    {
        PropertyInfo property = owner.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static);
        property.SetValue(null, value);
    }
}
