using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

public sealed class SaveManagerAutoSaveNotificationTests
{
    private static readonly BindingFlags StaticPrivate = BindingFlags.Static | BindingFlags.NonPublic;

    [Test]
    public void FinishedVersionIsStableWhenAnEarlierSubscriberChangesTheCurrentVersion()
    {
        FieldInfo generation = typeof(SaveManager).GetField("_autoSaveGeneration", StaticPrivate);
        int previous = (int)generation.GetValue(null);
        int observed = -1;
        Action<int, SaveStorageResult> first = (_, __) => generation.SetValue(null, 42);
        Action<int, SaveStorageResult> second = (version, _) => observed = version;
        SaveManager.AutoSaveCompletedWithVersion += first;
        SaveManager.AutoSaveCompletedWithVersion += second;
        try
        {
            generation.SetValue(null, 41);
            Notify(41, SaveStorageResult.Succeeded());
            Assert.That(observed, Is.EqualTo(41));
            Assert.That(SaveManager.AutoSaveRequestVersion, Is.EqualTo(42));
        }
        finally
        {
            SaveManager.AutoSaveCompletedWithVersion -= first;
            SaveManager.AutoSaveCompletedWithVersion -= second;
            generation.SetValue(null, previous);
        }
    }

    [Test]
    public void AThrowingVersionedSubscriberDoesNotBlockAnotherOrTheLegacyEvent()
    {
        int versionedCount = 0;
        int legacyCount = 0;
        Action<int, SaveStorageResult> failing = (_, __) => throw new InvalidOperationException("test autosave observer");
        Action<int, SaveStorageResult> following = (_, __) => versionedCount++;
        Action<SaveStorageResult> legacy = _ => legacyCount++;
        SaveManager.AutoSaveCompletedWithVersion += failing;
        SaveManager.AutoSaveCompletedWithVersion += following;
        SaveManager.AutoSaveCompleted += legacy;
        try
        {
            LogAssert.Expect(LogType.Exception, new System.Text.RegularExpressions.Regex("test autosave observer"));
            Notify(7, SaveStorageResult.Succeeded());
            Assert.That(versionedCount, Is.EqualTo(1));
            Assert.That(legacyCount, Is.EqualTo(1));
        }
        finally
        {
            SaveManager.AutoSaveCompletedWithVersion -= failing;
            SaveManager.AutoSaveCompletedWithVersion -= following;
            SaveManager.AutoSaveCompleted -= legacy;
        }
    }

    [Test]
    public void AThrowingLegacySubscriberDoesNotBlockTheFollowingSubscriber()
    {
        int count = 0;
        Action<SaveStorageResult> failing = _ => throw new InvalidOperationException("test legacy autosave observer");
        Action<SaveStorageResult> following = _ => count++;
        SaveManager.AutoSaveCompleted += failing;
        SaveManager.AutoSaveCompleted += following;
        try
        {
            LogAssert.Expect(LogType.Exception, new System.Text.RegularExpressions.Regex("test legacy autosave observer"));
            Notify(9, SaveStorageResult.Succeeded());
            Assert.That(count, Is.EqualTo(1));
        }
        finally
        {
            SaveManager.AutoSaveCompleted -= failing;
            SaveManager.AutoSaveCompleted -= following;
        }
    }

    private static void Notify(int version, SaveStorageResult result)
    {
        MethodInfo notify = typeof(SaveManager).GetMethod("NotifyAutoSaveCompleted", StaticPrivate);
        Assert.That(notify, Is.Not.Null);
        notify.Invoke(null, new object[] { version, result });
    }
}
