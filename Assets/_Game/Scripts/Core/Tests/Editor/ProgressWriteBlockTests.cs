using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public sealed class ProgressWriteBlockTests
{
    private static readonly BindingFlags StaticPrivate = BindingFlags.Static | BindingFlags.NonPublic;
    private object _previousStorage;
    private bool _previousPending;
    private int _previousGeneration;
    private MemoryFileSystem _files;

    [SetUp]
    public void SetUp()
    {
        if (Application.isPlaying || SaveManager.AreProgressWritesBlocked)
            Assert.Ignore("실행 중인 세션의 저장 차단/예약을 변경하지 않도록 Edit Mode에서만 검사합니다.");
        _previousStorage = Field("_storage").GetValue(null);
        _previousPending = SaveManager.HasPendingAutoSave;
        _previousGeneration = SaveManager.AutoSaveRequestVersion;
        _files = new MemoryFileSystem();
        // 경로는 키 이름일 뿐이며 실제 디렉터리/사용자 저장 파일에는 접근하지 않습니다.
        Field("_storage").SetValue(null, new AtomicSaveStorage(
            Path.Combine(Path.GetTempPath(), "HubToHome-MemoryOnly-WriteBlock"), _files));
    }

    [TearDown]
    public void TearDown()
    {
        if (_files == null) return;
        Field("_storage").SetValue(null, _previousStorage);
        Field("_autoSavePending").SetValue(null, _previousPending);
        Field("_autoSaveGeneration").SetValue(null, _previousGeneration);
        _files = null;
    }

    [Test]
    public void NestedOwnersStayBlockedUntilLastRelease()
    {
        var registry = new ProgressWriteBlockRegistry();
        Assert.That(registry.IsBlocked, Is.False);
        IDisposable first = registry.Acquire();
        IDisposable second = registry.Acquire();
        Assert.That(registry.IsBlocked, Is.True);
        first.Dispose();
        Assert.That(registry.IsBlocked, Is.True);
        second.Dispose();
        Assert.That(registry.IsBlocked, Is.False);
    }

    [Test]
    public void RepeatedDisposeDoesNotReleaseAnotherOwner()
    {
        var registry = new ProgressWriteBlockRegistry();
        IDisposable first = registry.Acquire();
        using (registry.Acquire())
        {
            first.Dispose();
            first.Dispose();
            Assert.That(registry.IsBlocked, Is.True);
        }
        Assert.That(registry.IsBlocked, Is.False);
    }

    [Test]
    public void ResetMakesOldHandlesHarmlessToTheNextGeneration()
    {
        var registry = new ProgressWriteBlockRegistry();
        IDisposable staleFirst = registry.Acquire();
        IDisposable staleSecond = registry.Acquire();
        registry.Reset();
        Assert.That(registry.IsBlocked, Is.False);
        using (registry.Acquire())
        {
            staleFirst.Dispose();
            staleSecond.Dispose();
            staleSecond.Dispose();
            Assert.That(registry.IsBlocked, Is.True);
        }
        Assert.That(registry.IsBlocked, Is.False);
    }

    [Test]
    public void EveryWriteEntryFailsBeforeAnyFileSystemAccess()
    {
        using (SaveManager.AcquireProgressWriteBlock())
        {
            Assert.That(SaveManager.AreProgressWritesBlocked, Is.True);
            Assert.That(SaveManager.CanSaveCurrentProgress, Is.False);
            AssertBlocked(SaveManager.TrySave(new SaveData(), 0));
            AssertBlocked(SaveManager.TrySaveProgress(0, null));
            AssertBlocked(SaveManager.TrySaveProgress(SaveManager.AutoSlotIndex, null));
            AssertBlocked(SaveManager.TryDelete(0));
            AssertBlocked(SaveManager.TryDelete(SaveManager.AutoSlotIndex));
            Assert.That(_files.AccessCount, Is.Zero);
        }
        Assert.That(SaveManager.AreProgressWritesBlocked, Is.False);
    }

    [Test]
    public void BlockedCallsDoNotEvenInitializeTheDefaultStorage()
    {
        Field("_storage").SetValue(null, null);
        using (SaveManager.AcquireProgressWriteBlock())
        {
            AssertBlocked(SaveManager.TrySave(new SaveData(), 0));
            AssertBlocked(SaveManager.TrySaveProgress(0, null));
            AssertBlocked(SaveManager.TryDelete(0));
            Assert.That(SaveManager.RequestAutoSave(), Is.False);
            Assert.That(Field("_storage").GetValue(null), Is.Null);
        }
    }

    [Test]
    public void ReadsStayAvailableAndWritesResumeOnlyAfterEveryOwnerReleases()
    {
        if (!SaveManager.CanSaveCurrentProgress)
            Assert.Ignore("기존 전투/로드 상태를 변경하지 않고 안정 상태에서만 정상 저장 위임을 검사합니다.");
        var data = new SaveData { playerName = "memory-only", currentScene = "FollowerGuardTest" };
        Assert.That(SaveManager.TrySave(data, 0).Success, Is.True);
        int writesBeforeBlock = _files.WriteCount;
        using (SaveManager.AcquireProgressWriteBlock())
        {
            using (SaveManager.AcquireProgressWriteBlock())
                Assert.That(SaveManager.TryLoad(0).Success, Is.True);
            Assert.That(SaveManager.InspectSlot(0).IsLoadable, Is.True);
            Assert.That(SaveManager.Exists(0), Is.True);
            AssertBlocked(SaveManager.TrySave(data, 0));
            AssertBlocked(SaveManager.TryDelete(0));
            Assert.That(_files.WriteCount, Is.EqualTo(writesBeforeBlock));
        }
        Assert.That(SaveManager.TrySave(data, 0).Success, Is.True);
        Assert.That(_files.WriteCount, Is.EqualTo(writesBeforeBlock + 1));
        Assert.That(SaveManager.TryDelete(0).Success, Is.True);
        Assert.That(SaveManager.Exists(0), Is.False);
    }

    [Test]
    public void BlockedAutoRequestNeverLeavesADeferredReservation()
    {
        Field("_autoSavePending").SetValue(null, false);
        using (SaveManager.AcquireProgressWriteBlock())
        {
            int version = SaveManager.AutoSaveRequestVersion;
            Assert.That(SaveManager.RequestAutoSave(), Is.False);
            Assert.That(SaveManager.HasPendingAutoSave, Is.False);
            Assert.That(SaveManager.AutoSaveRequestVersion, Is.EqualTo(version));
        }
        Assert.That(SaveManager.HasPendingAutoSave, Is.False);
        Assert.That(_files.AccessCount, Is.Zero);
    }

    [Test]
    public void AcquisitionInvalidatesAnExistingReservationBeforeRelease()
    {
        Field("_autoSavePending").SetValue(null, true);
        int previousVersion = SaveManager.AutoSaveRequestVersion;
        using (SaveManager.AcquireProgressWriteBlock())
        {
            Assert.That(SaveManager.HasPendingAutoSave, Is.False);
            Assert.That(SaveManager.AutoSaveRequestVersion, Is.Not.EqualTo(previousVersion));
            Assert.That(SaveManager.RequestAutoSave(), Is.False);
        }
        Assert.That(SaveManager.HasPendingAutoSave, Is.False);
        Assert.That(_files.AccessCount, Is.Zero);
    }

    private static FieldInfo Field(string name) => typeof(SaveManager).GetField(name, StaticPrivate);

    private static void AssertBlocked(SaveStorageResult result)
    {
        Assert.That(result.Success, Is.False);
        Assert.That(result.Failure, Is.EqualTo(SaveStorageFailure.NotAllowed));
        Assert.That(result.Message, Does.Contain("진행 저장 차단"));
    }

    private sealed class MemoryFileSystem : ISaveFileSystem
    {
        private readonly Dictionary<string, string> _contents = new Dictionary<string, string>(StringComparer.Ordinal);
        public int AccessCount { get; private set; }
        public int WriteCount { get; private set; }

        public bool FileExists(string path) { AccessCount++; return _contents.ContainsKey(path); }
        public string ReadAllText(string path) { AccessCount++; return _contents[path]; }
        public void CreateDirectory(string path) { AccessCount++; }
        public void WriteAllTextDurable(string path, string content)
        { AccessCount++; WriteCount++; _contents[path] = content; }
        public void ReplaceFile(string sourcePath, string destinationPath, string backupPath)
        {
            AccessCount++;
            _contents[backupPath] = _contents[destinationPath];
            _contents[destinationPath] = _contents[sourcePath];
            _contents.Remove(sourcePath);
        }
        public void MoveFile(string sourcePath, string destinationPath)
        {
            AccessCount++;
            _contents.Add(destinationPath, _contents[sourcePath]);
            _contents.Remove(sourcePath);
        }
        public void DeleteFile(string path) { AccessCount++; _contents.Remove(path); }
    }
}
