using NUnit.Framework;
using UnityEngine;

public sealed class MapReturnBookmarkTests
{
    [Test]
    public void EntryFailure_RollsBackOnlyPendingBookmark()
    {
        var stack = new MapReturnBookmarkStack();
        MapReturnBookmarkToken olderToken = stack.PushCommitted(Bookmark("older"));
        MapReturnBookmarkToken pendingToken = stack.PushPending(Bookmark("new"));

        Assert.That(stack.Rollback(pendingToken), Is.True);
        Assert.That(stack.TryPeek(out MapReturnBookmark remaining, out MapReturnBookmarkToken remainingToken), Is.True);
        Assert.That(remaining.RoomId, Is.EqualTo("older"));
        Assert.That(remainingToken, Is.EqualTo(olderToken));
    }

    [Test]
    public void EntrySuccess_CommitsPendingBookmarkAndReturnPopsExpectedTop()
    {
        var stack = new MapReturnBookmarkStack();
        MapReturnBookmarkToken token = stack.PushPending(Bookmark("room"));

        Assert.That(stack.TryPeek(out _), Is.False, "Pending bookmark must not be used for return.");
        Assert.That(stack.Commit(token), Is.True);
        Assert.That(stack.TryPeek(out MapReturnBookmark committed), Is.True);
        Assert.That(committed.RoomId, Is.EqualTo("room"));
        Assert.That(stack.TryPop(token, out MapReturnBookmark popped), Is.True);
        Assert.That(popped.RoomId, Is.EqualTo("room"));
        Assert.That(stack.Count, Is.Zero);
    }

    [Test]
    public void StaleToken_CannotMutateNewerBookmark()
    {
        var stack = new MapReturnBookmarkStack();
        MapReturnBookmarkToken older = stack.PushCommitted(Bookmark("older"));
        MapReturnBookmarkToken newer = stack.PushPending(Bookmark("newer"));

        Assert.That(stack.Rollback(older), Is.False);
        Assert.That(stack.Commit(older), Is.False);
        Assert.That(stack.Rollback(newer), Is.True);
        Assert.That(stack.TryPeek(out MapReturnBookmark remaining), Is.True);
        Assert.That(remaining.RoomId, Is.EqualTo("older"));
    }

    [Test]
    public void FromSaveData_ClearsRuntimeOnlyBookmarkStack()
    {
        GameObject gameObject = new GameObject("GlobalData_MapReturnBookmarkTests");
        GlobalDataManager global = gameObject.AddComponent<GlobalDataManager>();
        try
        {
            global.PushPendingMapReturnBookmark(Bookmark("pending"));
            global.FromSaveData(new SaveData());

            Assert.That(global.MapReturnBookmarkCount, Is.Zero);
            Assert.That(global.TryPeekMapReturnBookmark(out _, out _), Is.False);
        }
        finally
        {
            Object.DestroyImmediate(gameObject);
        }
    }

    [Test]
    public void SublocationCompletionReceiptPersistsAfterSourceMarkerLifetime()
    {
        GameObject gameObject = new GameObject("GlobalData_SublocationCompletionReceiptTests");
        GlobalDataManager global = gameObject.AddComponent<GlobalDataManager>();
        try
        {
            var receipt = new SublocationCompletionReceipt(
                true,
                "RegionScene",
                "showcase.train",
                "showcase.train.optional_cabin",
                "showcase.train.optional_cabin.completed");

            Assert.That(receipt.Apply(global), Is.True);
            Assert.That(
                AreaMarkerStateService.IsCompleted(
                    global,
                    "RegionScene",
                    "showcase.train",
                    "showcase.train.optional_cabin"),
                Is.True);
            Assert.That(
                global.GetFlag("showcase.train.optional_cabin.completed", 0),
                Is.EqualTo(1));
        }
        finally
        {
            Object.DestroyImmediate(gameObject);
        }
    }

    [Test]
    public void ReusableSublocationReceiptDoesNotPersistCompletion()
    {
        GameObject gameObject = new GameObject("GlobalData_SublocationReusableTests");
        GlobalDataManager global = gameObject.AddComponent<GlobalDataManager>();
        try
        {
            var receipt = new SublocationCompletionReceipt(
                false,
                "RegionScene",
                "showcase.train",
                "showcase.train.optional_cabin",
                "showcase.train.optional_cabin.completed");

            Assert.That(receipt.Apply(global), Is.False);
            Assert.That(
                AreaMarkerStateService.IsCompleted(
                    global,
                    "RegionScene",
                    "showcase.train",
                    "showcase.train.optional_cabin"),
                Is.False);
            Assert.That(
                global.GetFlag("showcase.train.optional_cabin.completed", 0),
                Is.Zero);
        }
        finally
        {
            Object.DestroyImmediate(gameObject);
        }
    }
    private static MapReturnBookmark Bookmark(string roomId)
    {
        return new MapReturnBookmark(
            "RegionScene",
            roomId,
            "return.spawn",
            new Vector2(2f, 3f),
            FacingDirection.Left);
    }
}