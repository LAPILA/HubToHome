using System;
using System.IO;
using System.Reflection;
using NUnit.Framework;

public sealed class SaveManagerMostRecentTests
{
    private string _directory;
    private AtomicSaveStorage _storage;
    private AtomicSaveStorage _previousStorage;

    [SetUp]
    public void SetUp()
    {
        _directory = Path.Combine(
            Path.GetTempPath(),
            "HubToHome-SaveManagerTests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_directory);
        _storage = new AtomicSaveStorage(
            _directory,
            new SystemSaveFileSystem(),
            new SaveDataCodec());
        FieldInfo field = StorageField();
        _previousStorage = (AtomicSaveStorage)field.GetValue(null);
        field.SetValue(null, _storage);
    }

    [TearDown]
    public void TearDown()
    {
        StorageField().SetValue(null, _previousStorage);
        if (Directory.Exists(_directory))
            Directory.Delete(_directory, true);
    }

    [Test]
    public void TryLoadMostRecentConsidersManualAndAutoSlotsBySaveTimestamp()
    {
        Save(0, "manual-old", "2026-07-20 12:00:00");
        Save(2, "manual-new", "2026-07-22 12:00:00");
        Save(SaveManager.AutoSlotIndex, "auto-newest", "2026-07-23 12:00:00");

        bool loaded = SaveManager.TryLoadMostRecent(out int slot, out SaveLoadResult result);

        Assert.That(loaded, Is.True);
        Assert.That(slot, Is.EqualTo(SaveManager.AutoSlotIndex));
        Assert.That(result.Success, Is.True);
        Assert.That(result.Data.playerName, Is.EqualTo("auto-newest"));
        Assert.That(SaveManager.HasAnySave(), Is.True);
    }

    [Test]
    public void TryLoadMostRecentReturnsExplicitNotFoundWhenNoSlotIsLoadable()
    {
        bool loaded = SaveManager.TryLoadMostRecent(out int slot, out SaveLoadResult result);

        Assert.That(loaded, Is.False);
        Assert.That(slot, Is.EqualTo(-1));
        Assert.That(result, Is.Not.Null);
        Assert.That(result.Failure, Is.EqualTo(SaveLoadFailure.NotFound));
    }

    [Test]
    public void EqualOrMissingTimestampsUseLaterCandidateDeterministically()
    {
        Save(0, "manual-zero", string.Empty);
        Save(1, "manual-one", string.Empty);

        bool loaded = SaveManager.TryLoadMostRecent(out int slot, out SaveLoadResult result);

        Assert.That(loaded, Is.True);
        Assert.That(slot, Is.EqualTo(1));
        Assert.That(result.Data.playerName, Is.EqualTo("manual-one"));
    }

    private void Save(int slot, string playerName, string saveTime)
    {
        SaveStorageResult result = _storage.Save(
            new SaveData
            {
                playerName = playerName,
                currentScene = "TestMap",
                saveTime = saveTime
            },
            slot);
        Assert.That(result.Success, Is.True, result.Message);
    }

    private static FieldInfo StorageField()
    {
        FieldInfo field = typeof(SaveManager).GetField(
            "_storage",
            BindingFlags.Static | BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null);
        return field;
    }
}