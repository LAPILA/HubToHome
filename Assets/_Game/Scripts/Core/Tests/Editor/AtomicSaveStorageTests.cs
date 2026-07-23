using System;
using System.IO;
using NUnit.Framework;

public class AtomicSaveStorageTests
{
    private string _directory;
    private SaveDataCodec _codec;

    [SetUp]
    public void SetUp()
    {
        _directory = Path.Combine(
            Path.GetTempPath(),
            "HubToHome-AtomicSaveTests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_directory);
        _codec = new SaveDataCodec();
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_directory))
            Directory.Delete(_directory, true);
    }

    [Test]
    public void GetPaths_UsesSameDirectoryForAtomicCandidates()
    {
        AtomicSaveStorage storage = CreateStorage();

        SaveSlotPaths paths = storage.GetPaths(2);

        Assert.That(paths.PrimaryPath, Does.EndWith("save_slot_2.json"));
        Assert.That(paths.BackupPath, Is.EqualTo(paths.PrimaryPath + ".bak"));
        Assert.That(paths.TemporaryPath, Is.EqualTo(paths.PrimaryPath + ".tmp"));
        Assert.That(paths.CorruptPath, Is.EqualTo(paths.PrimaryPath + ".corrupt"));
        Assert.That(
            Path.GetDirectoryName(paths.PrimaryPath),
            Is.EqualTo(Path.GetDirectoryName(paths.TemporaryPath)));
    }

    [Test]
    public void Save_FirstCommitCreatesPrimaryAndCleansTemporaryFile()
    {
        AtomicSaveStorage storage = CreateStorage();

        SaveStorageResult result = storage.Save(CreateData("first"), 0);

        SaveSlotPaths paths = storage.GetPaths(0);
        Assert.That(result.Success, Is.True, result.Message);
        Assert.That(File.Exists(paths.PrimaryPath), Is.True);
        Assert.That(File.Exists(paths.BackupPath), Is.False);
        Assert.That(File.Exists(paths.TemporaryPath), Is.False);
        Assert.That(Decode(paths.PrimaryPath).Data.playerName, Is.EqualTo("first"));
    }

    [Test]
    public void Save_SecondCommitKeepsPreviousValidSnapshotAsBackup()
    {
        AtomicSaveStorage storage = CreateStorage();
        Assert.That(storage.Save(CreateData("first"), 0).Success, Is.True);

        SaveStorageResult result = storage.Save(CreateData("second"), 0);

        SaveSlotPaths paths = storage.GetPaths(0);
        Assert.That(result.Success, Is.True, result.Message);
        Assert.That(Decode(paths.PrimaryPath).Data.playerName, Is.EqualTo("second"));
        Assert.That(Decode(paths.BackupPath).Data.playerName, Is.EqualTo("first"));
        Assert.That(File.Exists(paths.TemporaryPath), Is.False);
    }

    [Test]
    public void Load_CorruptPrimary_UsesPreviousValidBackup()
    {
        AtomicSaveStorage storage = CreateStorage();
        storage.Save(CreateData("first"), 0);
        storage.Save(CreateData("second"), 0);
        File.WriteAllText(storage.GetPaths(0).PrimaryPath, "{broken");

        SaveLoadResult result = storage.Load(0);

        Assert.That(result.Success, Is.True, result.Message);
        Assert.That(result.Source, Is.EqualTo(SaveLoadSource.Backup));
        Assert.That(result.Data.playerName, Is.EqualTo("first"));
        Assert.That(result.Message, Does.Contain("Primary"));
    }

    [Test]
    public void Load_FuturePrimary_UsesSupportedBackup()
    {
        AtomicSaveStorage storage = CreateStorage();
        storage.Save(CreateData("first"), 0);
        storage.Save(CreateData("second"), 0);
        File.WriteAllText(
            storage.GetPaths(0).PrimaryPath,
            "{\"schemaVersion\":999,\"currentScene\":\"Future\"}");

        SaveLoadResult result = storage.Load(0);

        Assert.That(result.Success, Is.True, result.Message);
        Assert.That(result.Source, Is.EqualTo(SaveLoadSource.Backup));
        Assert.That(result.Data.playerName, Is.EqualTo("first"));
    }

    [Test]
    public void Load_OnlyValidTemporaryFile_RecoversInterruptedFirstSave()
    {
        AtomicSaveStorage storage = CreateStorage();
        SaveSlotPaths paths = storage.GetPaths(0);
        File.WriteAllText(paths.TemporaryPath, _codec.Encode(CreateData("pending")));

        SaveLoadResult result = storage.Load(0);

        Assert.That(result.Success, Is.True, result.Message);
        Assert.That(result.Source, Is.EqualTo(SaveLoadSource.Temporary));
        Assert.That(result.Data.playerName, Is.EqualTo("pending"));
    }

    [Test]
    public void Save_ReplaceFailureKeepsExistingPrimaryLoadable()
    {
        AtomicSaveStorage initialStorage = CreateStorage();
        initialStorage.Save(CreateData("first"), 0);
        var faulting = new FaultInjectingSaveFileSystem(
            new SystemSaveFileSystem())
        {
            ThrowOnReplace = true
        };
        var failingStorage = new AtomicSaveStorage(
            _directory,
            faulting,
            _codec);

        SaveStorageResult result = failingStorage.Save(CreateData("second"), 0);

        SaveSlotPaths paths = failingStorage.GetPaths(0);
        Assert.That(result.Success, Is.False);
        Assert.That(Decode(paths.PrimaryPath).Data.playerName, Is.EqualTo("first"));
        Assert.That(File.Exists(paths.TemporaryPath), Is.False);
    }

    [Test]
    public void Save_CorruptPrimaryPreservesValidBackupAndQuarantinesPrimary()
    {
        AtomicSaveStorage storage = CreateStorage();
        storage.Save(CreateData("first"), 0);
        storage.Save(CreateData("second"), 0);
        SaveSlotPaths paths = storage.GetPaths(0);
        File.WriteAllText(paths.PrimaryPath, "{broken");

        SaveStorageResult result = storage.Save(CreateData("third"), 0);

        Assert.That(result.Success, Is.True, result.Message);
        Assert.That(Decode(paths.PrimaryPath).Data.playerName, Is.EqualTo("third"));
        Assert.That(Decode(paths.BackupPath).Data.playerName, Is.EqualTo("first"));
        Assert.That(File.ReadAllText(paths.CorruptPath), Is.EqualTo("{broken"));
    }

    [Test]
    public void Delete_RemovesEveryCandidateForSlot()
    {
        AtomicSaveStorage storage = CreateStorage();
        SaveSlotPaths paths = storage.GetPaths(0);
        File.WriteAllText(paths.PrimaryPath, "primary");
        File.WriteAllText(paths.BackupPath, "backup");
        File.WriteAllText(paths.TemporaryPath, "temporary");
        File.WriteAllText(paths.CorruptPath, "corrupt");

        SaveStorageResult result = storage.Delete(0);

        Assert.That(result.Success, Is.True, result.Message);
        Assert.That(File.Exists(paths.PrimaryPath), Is.False);
        Assert.That(File.Exists(paths.BackupPath), Is.False);
        Assert.That(File.Exists(paths.TemporaryPath), Is.False);
        Assert.That(File.Exists(paths.CorruptPath), Is.False);
    }

    private AtomicSaveStorage CreateStorage()
    {
        return new AtomicSaveStorage(
            _directory,
            new SystemSaveFileSystem(),
            _codec);
    }

    private SaveDecodeResult Decode(string path)
    {
        SaveDecodeResult result = _codec.Decode(File.ReadAllText(path));
        Assert.That(result.Success, Is.True, result.Message);
        return result;
    }

    private static SaveData CreateData(string playerName)
    {
        return new SaveData
        {
            playerName = playerName,
            currentScene = "TestMap",
            currentRoomId = "test.room"
        };
    }

    private sealed class FaultInjectingSaveFileSystem : ISaveFileSystem
    {
        private readonly ISaveFileSystem _inner;

        public FaultInjectingSaveFileSystem(ISaveFileSystem inner)
        {
            _inner = inner;
        }

        public bool ThrowOnReplace { get; set; }

        public bool FileExists(string path)
        {
            return _inner.FileExists(path);
        }

        public string ReadAllText(string path)
        {
            return _inner.ReadAllText(path);
        }

        public void CreateDirectory(string path)
        {
            _inner.CreateDirectory(path);
        }

        public void WriteAllTextDurable(string path, string content)
        {
            _inner.WriteAllTextDurable(path, content);
        }

        public void ReplaceFile(
            string sourcePath,
            string destinationPath,
            string backupPath)
        {
            if (ThrowOnReplace)
                throw new IOException("Injected replace failure.");

            _inner.ReplaceFile(sourcePath, destinationPath, backupPath);
        }

        public void MoveFile(string sourcePath, string destinationPath)
        {
            _inner.MoveFile(sourcePath, destinationPath);
        }

        public void DeleteFile(string path)
        {
            _inner.DeleteFile(path);
        }
    }
}
