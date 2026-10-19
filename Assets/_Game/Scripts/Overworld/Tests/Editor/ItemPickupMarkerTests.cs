using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

public sealed class ItemPickupMarkerTests
{
    private const string AreaId = "room.train.chest-tests";
    private const string MarkerId = "chest.snack";
    private const string MissingDialogueWarning =
        "[AreaMarkerRuntimeService] DialogueManager가 없어 대화를 시작할 수 없습니다.";

    private readonly List<UnityEngine.Object> _created = new List<UnityEngine.Object>();
    private object _previousItemCache;
    private GlobalDataManager _previousGlobal;
    private DialogueManager _previousDialogue;
    private GameStateManager _previousState;
    private GlobalDataManager _global;
    private ItemData _item;
    private Sprite _closed;
    private Sprite _opened;

    [SetUp]
    public void SetUp()
    {
        _previousItemCache = ItemCache.GetValue(null);
        ItemCache.SetValue(null, new Dictionary<string, ItemData>(StringComparer.Ordinal));
        _previousGlobal = GlobalDataManager.Instance;
        _previousDialogue = DialogueManager.Instance;
        _previousState = GameStateManager.Instance;
        SetSingleton<GlobalDataManager>(null);
        SetSingleton<DialogueManager>(null);
        SetSingleton<GameStateManager>(null);

        GameObject globalObject = CreateObject("ItemPickupTests_Global", false);
        _global = globalObject.AddComponent<GlobalDataManager>();
        SetSingleton(_global);

        _item = ScriptableObject.CreateInstance<ItemData>();
        _created.Add(_item);
        _item.ItemID = "food.test_snack";
        _item.ItemName = "과자";
        _item.IsStackable = true;
        _item.MaxStackSize = 99;
        ((Dictionary<string, ItemData>)ItemCache.GetValue(null)).Add(_item.ItemID, _item);

        var texture = new Texture2D(2, 1);
        _created.Add(texture);
        _closed = Sprite.Create(texture, new Rect(0f, 0f, 1f, 1f), Vector2.one * .5f);
        _opened = Sprite.Create(texture, new Rect(1f, 0f, 1f, 1f), Vector2.one * .5f);
        _created.Add(_closed);
        _created.Add(_opened);
    }

    [TearDown]
    public void TearDown()
    {
        for (int i = _created.Count - 1; i >= 0; i--)
            if (_created[i] != null) UnityEngine.Object.DestroyImmediate(_created[i]);
        _created.Clear();
        SetSingleton(_previousGlobal);
        SetSingleton(_previousDialogue);
        SetSingleton(_previousState);
        ItemCache.SetValue(null, _previousItemCache);
    }

    [Test]
    public void ChestCommitsRewardAndOpenedSpriteBeforeDialogueAndRejectsRepeatInput()
    {
        ItemPickupMarkerProbe marker = CreateMarker();
        // 플래그 알림에서 재진입해도 동일 보상을 한 번만 지급해야 합니다.
        _global.FlagChanged += (_, _, _) => marker.Interact((PlayerController)null);

        InteractWithoutDialogue(marker);
        marker.Interact((PlayerController)null);
        marker.Interact((PlayerController)null);

        Assert.That(_global.GetItemCount(_item.ItemID), Is.EqualTo(1));
        AssertOpened(marker);
        Assert.That(marker.GetComponent<BoxCollider2D>().enabled, Is.True);
    }

    [TestCase(99, 1)]
    [TestCase(98, 2)]
    public void FullOrInsufficientStackCapacityCannotPartiallyEmptyChest(int owned, int reward)
    {
        _global.AddItem(_item.ItemID, owned);
        ItemPickupMarkerProbe marker = CreateMarker(reward: reward);

        InteractWithoutDialogue(marker);

        Assert.That(_global.GetItemCount(_item.ItemID), Is.EqualTo(owned));
        AssertClosed(marker);
    }

    [Test]
    public void MissingItemDoesNotGrantOrOpenChest()
    {
        ItemPickupMarkerProbe marker = CreateMarker(itemId: "missing.item");

        InteractWithoutDialogue(marker);

        Assert.That(_global.GetInventory(), Is.Empty);
        AssertClosed(marker);
    }

    [TestCase(0)]
    [TestCase(-1)]
    public void InvalidRewardAmountDoesNotGrantOrOpenChest(int reward)
    {
        ItemPickupMarkerProbe marker = CreateMarker(reward: reward);

        InteractWithoutDialogue(marker);

        Assert.That(_global.GetInventory(), Is.Empty);
        AssertClosed(marker);
    }

    [Test]
    public void NewRoomInstanceRestoresOpenedChestWithoutAwardingAgain()
    {
        ItemPickupMarkerProbe first = CreateMarker();
        InteractWithoutDialogue(first);
        SaveData saved = _global.ToSaveData();
        UnityEngine.Object.DestroyImmediate(first.gameObject);

        _global.FromSaveData(saved);
        ItemPickupMarkerProbe restored = CreateMarker();
        restored.Interact((PlayerController)null);

        AssertOpened(restored);
        Assert.That(_global.GetItemCount(_item.ItemID), Is.EqualTo(1));
    }

    [Test]
    public void LoadingOlderSnapshotRewindsSameVisibleChestAndInventoryTogether()
    {
        ItemPickupMarkerProbe marker = CreateMarker();
        SaveData beforeOpening = _global.ToSaveData();
        InteractWithoutDialogue(marker);
        AssertOpened(marker);

        _global.FromSaveData(beforeOpening);

        Assert.That(_global.GetItemCount(_item.ItemID), Is.Zero);
        AssertClosed(marker);
        InteractWithoutDialogue(marker);
        AssertOpened(marker);
        Assert.That(_global.GetItemCount(_item.ItemID), Is.EqualTo(1));
    }

    [Test]
    public void ReenabledChestReadsSnapshotChangedWhileItWasInactive()
    {
        ItemPickupMarkerProbe marker = CreateMarker();
        SaveData beforeOpening = _global.ToSaveData();
        InteractWithoutDialogue(marker);
        marker.gameObject.SetActive(false);

        _global.FromSaveData(beforeOpening);
        marker.gameObject.SetActive(true);
        marker.BeginRuntime();

        AssertClosed(marker);
        Assert.That(_global.GetItemCount(_item.ItemID), Is.Zero);
    }

    [Test]
    public void OrdinaryOneShotPickupStillHidesAfterSuccessfulGrant()
    {
        ItemPickupMarkerProbe marker = CreateMarker(keepVisible: false);

        InteractWithoutDialogue(marker);

        Assert.That(marker.IsCompleted, Is.True);
        Assert.That(marker.gameObject.activeSelf, Is.False);
        Assert.That(marker.CanInteract((PlayerController)null), Is.False);
        Assert.That(_global.GetItemCount(_item.ItemID), Is.EqualTo(1));
    }

    [Test]
    public void CancelledAcquisitionDialogueCannotReopenOrRegrantChest()
    {
        GameObject managerObject = CreateObject("ItemPickupTests_DialogueManager");
        DialogueManager manager = managerObject.AddComponent<DialogueManager>();
        SetSingleton(manager);
        DialogueUI panel = CreateObject("ItemPickupTests_DialoguePanel").AddComponent<DialogueUI>();
        SetPrivateField(typeof(DialogueManager), manager, "_overworldPanel", panel);
        ItemPickupMarkerProbe marker = CreateMarker();

        marker.Interact((PlayerController)null);
        Assert.That(manager.IsPlaying, Is.True);
        AssertOpened(marker);
        manager.CancelDialogue();
        marker.Interact((PlayerController)null);

        Assert.That(manager.IsPlaying, Is.False);
        AssertOpened(marker);
        Assert.That(_global.GetItemCount(_item.ItemID), Is.EqualTo(1));
    }

    [Test]
    public void MissingGlobalStoreCannotGrantReward()
    {
        ItemPickupMarkerProbe marker = CreateMarker();

        bool granted = AreaMarkerRuntimeService.TryGrantItem(marker, null, _item.ItemID, 1,
            out _, out string error);

        Assert.That(granted, Is.False);
        Assert.That(error, Is.Not.Empty);
        Assert.That(_global.GetInventory(), Is.Empty);
        AssertClosed(marker);
    }

    private ItemPickupMarkerProbe CreateMarker(bool keepVisible = true, int reward = 1, string itemId = null)
    {
        GameObject markerObject = CreateObject("ItemPickupTests_Chest", false);
        SpriteRenderer renderer = markerObject.AddComponent<SpriteRenderer>();
        renderer.sprite = _closed;
        markerObject.AddComponent<BoxCollider2D>();
        ItemPickupMarkerProbe marker = markerObject.AddComponent<ItemPickupMarkerProbe>();
        marker.Configure(_global, AreaId, MarkerId);
        SetPickupField(marker, "itemId", itemId ?? _item.ItemID);
        SetPickupField(marker, "amount", reward);
        SetPickupField(marker, "keepVisibleAfterPickup", keepVisible);
        SetPickupField(marker, "pickupRenderer", renderer);
        SetPickupField(marker, "closedSprite", _closed);
        SetPickupField(marker, "openedSprite", _opened);
        markerObject.SetActive(true);
        marker.BeginRuntime();
        return marker;
    }

    private GameObject CreateObject(string name, bool active = true)
    {
        var created = new GameObject(name);
        created.SetActive(active);
        _created.Add(created);
        return created;
    }

    private static void InteractWithoutDialogue(ItemPickupMarker marker)
    {
        LogAssert.Expect(LogType.Warning, MissingDialogueWarning);
        marker.Interact((PlayerController)null);
    }

    private void AssertOpened(ItemPickupMarker marker)
    {
        Assert.That(marker.IsCompleted, Is.True);
        Assert.That(marker.gameObject.activeSelf, Is.True);
        Assert.That(marker.GetComponent<SpriteRenderer>().sprite, Is.SameAs(_opened));
        Assert.That(marker.CanInteract((PlayerController)null), Is.False);
        Assert.That(_global.GetFlag(CompletionFlag(marker)), Is.EqualTo(1));
    }

    private void AssertClosed(ItemPickupMarker marker)
    {
        Assert.That(marker.IsCompleted, Is.False);
        Assert.That(marker.gameObject.activeSelf, Is.True);
        Assert.That(marker.GetComponent<SpriteRenderer>().sprite, Is.SameAs(_closed));
        Assert.That(marker.CanInteract((PlayerController)null), Is.True);
        Assert.That(_global.GetFlag(CompletionFlag(marker)), Is.Zero);
    }

    private static string CompletionFlag(ItemPickupMarker marker) =>
        AreaMarkerStateService.BuildCompletionFlag(marker.gameObject.scene.name, AreaId, MarkerId);

    private static FieldInfo ItemCache => typeof(ItemDatabase).GetField("_cache", BindingFlags.Static | BindingFlags.NonPublic);

    private static void SetSingleton<T>(T value) where T : MonoBehaviour => typeof(T)
        .GetProperty("Instance", BindingFlags.Static | BindingFlags.Public)
        .GetSetMethod(true).Invoke(null, new object[] { value });

    private static void SetPickupField<T>(ItemPickupMarker marker, string name, T value) =>
        SetPrivateField(typeof(ItemPickupMarker), marker, name, value);

    private static void SetPrivateField<T>(Type declaringType, object target, string name, T value)
    {
        FieldInfo field = declaringType.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null, "Missing serialized field: " + name);
        field.SetValue(target, value);
    }
}

public sealed class ItemPickupMarkerProbe : ItemPickupMarker
{
    private GlobalDataManager _global;

    public void Configure(GlobalDataManager global, string roomId, string id)
    {
        _global = global;
        areaId = roomId;
        markerId = id;
        isOneShot = true;
    }

    public void BeginRuntime() => base.Start();
    protected override GlobalDataManager ResolveGlobalData() => _global;
}
