using System;
using System.IO;
using NUnit.Framework;

public class AtomicSaveStorageTests
{
    [Test]
    public void ThirdCommitReplacesExistingBackupAndFailedFourthPreservesBoth()
    {
        AtomicSaveStorage storage = CreateStorage();
        Assert.That(storage.Save(CreateData("first"), 0).Success, Is.True);
        Assert.That(storage.Save(CreateData("second"), 0).Success, Is.True);
        Assert.That(storage.Save(CreateData("third"), 0).Success, Is.True);
        var paths = storage.GetPaths(0);
        Assert.That(Decode(paths.BackupPath).Data.playerName, Is.EqualTo("second"));
        var faulting = new FaultInjectingSaveFileSystem(new SystemSaveFileSystem()) { ThrowOnReplace = true };
        var failing = new AtomicSaveStorage(_directory, faulting, _codec);
        var data = CreateData("fourth");
        data.saveTime = "unchanged";
        data.Money = -4;
        Assert.That(failing.Save(data, 0, "new time").Success, Is.False);
        Assert.That(data.saveTime, Is.EqualTo("unchanged"));
        Assert.That(data.Money, Is.EqualTo(-4));
        Assert.That(Decode(paths.PrimaryPath).Data.playerName, Is.EqualTo("third"));
        Assert.That(Decode(paths.BackupPath).Data.playerName, Is.EqualTo("second"));
    }

    [Test]
    public void FuturePrimaryCannotBeSilentlyOverwritten()
    {
        var storage = CreateStorage();
        var paths = storage.GetPaths(0);
        const string future = "{\"schemaVersion\":999,\"currentScene\":\"Future\"}";
        File.WriteAllText(paths.PrimaryPath, future);
        Assert.That(storage.Save(CreateData("old game"), 0).Failure, Is.EqualTo(SaveStorageFailure.NotAllowed));
        Assert.That(File.ReadAllText(paths.PrimaryPath), Is.EqualTo(future));
        Assert.That(File.Exists(paths.TemporaryPath), Is.False);
    }

    [Test]
    public void Load_NonFinitePrimaryUsesHealthyBackupAndFailedSavePreservesBothFiles()
    {
        AtomicSaveStorage storage = CreateStorage();
        Assert.That(storage.Save(CreateData("healthy"), 0).Success, Is.True);
        Assert.That(storage.Save(CreateData("latest"), 0).Success, Is.True);
        SaveSlotPaths paths = storage.GetPaths(0);
        File.WriteAllText(paths.PrimaryPath, "{\"schemaVersion\":4,\"playerX\":\"NaN\"}");
        string primary = File.ReadAllText(paths.PrimaryPath);
        string backup = File.ReadAllText(paths.BackupPath);
        SaveLoadResult loaded = storage.Load(0);
        Assert.That(loaded.Success, Is.True);
        Assert.That(loaded.Source, Is.EqualTo(SaveLoadSource.Backup));
        Assert.That(loaded.Data.playerName, Is.EqualTo("healthy"));
        SaveData invalid = CreateData("invalid");
        invalid.playerY = float.PositiveInfinity;
        Assert.That(storage.Save(invalid, 0).Success, Is.False);
        Assert.That(File.ReadAllText(paths.PrimaryPath), Is.EqualTo(primary));
        Assert.That(File.ReadAllText(paths.BackupPath), Is.EqualTo(backup));
    }

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

    [TestCase(false)]
    [TestCase(true)]
    public void Save_WriteFailurePreservesOnlyValidTemporaryRecovery(bool corruptPrimaryExists)
    {
        AtomicSaveStorage storage = CreateStorage();
        SaveSlotPaths paths = storage.GetPaths(0);
        File.WriteAllText(paths.TemporaryPath, _codec.Encode(CreateData("recovered")));
        if (corruptPrimaryExists) File.WriteAllText(paths.PrimaryPath, "{broken");
        var faulting = new FaultInjectingSaveFileSystem(new SystemSaveFileSystem()) { ThrowOnWrite = true };
        var failing = new AtomicSaveStorage(_directory, faulting, _codec);

        Assert.That(failing.Load(0).Source, Is.EqualTo(SaveLoadSource.Temporary));
        Assert.That(failing.Save(CreateData("replacement"), 0).Failure, Is.EqualTo(SaveStorageFailure.Io));

        SaveLoadResult recovered = failing.Load(0);
        Assert.That(recovered.Success, Is.True, recovered.Message);
        Assert.That(recovered.Data.playerName, Is.EqualTo("recovered"));
        if (corruptPrimaryExists)
            Assert.That(File.ReadAllText(paths.CorruptPath), Is.EqualTo("{broken"));
    }

    [Test]
    public void Save_TemporaryPreservationMoveFailureDoesNotCleanRecoveryFile()
    {
        AtomicSaveStorage storage = CreateStorage();
        SaveSlotPaths paths = storage.GetPaths(0);
        string recovery = _codec.Encode(CreateData("recovered"));
        File.WriteAllText(paths.TemporaryPath, recovery);
        var faulting = new FaultInjectingSaveFileSystem(new SystemSaveFileSystem()) { ThrowOnMove = true };
        var failing = new AtomicSaveStorage(_directory, faulting, _codec);

        Assert.That(failing.Save(CreateData("replacement"), 0).Failure, Is.EqualTo(SaveStorageFailure.Io));
        Assert.That(File.ReadAllText(paths.TemporaryPath), Is.EqualTo(recovery));
        Assert.That(failing.Load(0).Source, Is.EqualTo(SaveLoadSource.Temporary));
        Assert.That(failing.Load(0).Data.playerName, Is.EqualTo("recovered"));
    }

    [Test]
    public void Save_OnlyValidTemporaryRecoveryBecomesBackupOfSuccessfulCommit()
    {
        AtomicSaveStorage storage = CreateStorage();
        SaveSlotPaths paths = storage.GetPaths(0);
        File.WriteAllText(paths.TemporaryPath, _codec.Encode(CreateData("recovered")));

        Assert.That(storage.Save(CreateData("replacement"), 0).Success, Is.True);
        Assert.That(Decode(paths.PrimaryPath).Data.playerName, Is.EqualTo("replacement"));
        Assert.That(Decode(paths.BackupPath).Data.playerName, Is.EqualTo("recovered"));
        Assert.That(File.Exists(paths.TemporaryPath), Is.False);
    }

    [Test]
    public void Save_HealthyBackupRemainsAuthoritativeWhenTemporaryExistsAndWriteFails()
    {
        AtomicSaveStorage storage = CreateStorage();
        SaveSlotPaths paths = storage.GetPaths(0);
        string backup = _codec.Encode(CreateData("backup"));
        File.WriteAllText(paths.BackupPath, backup);
        File.WriteAllText(paths.TemporaryPath, _codec.Encode(CreateData("temporary")));
        var faulting = new FaultInjectingSaveFileSystem(new SystemSaveFileSystem()) { ThrowOnWrite = true };
        var failing = new AtomicSaveStorage(_directory, faulting, _codec);

        Assert.That(failing.Save(CreateData("replacement"), 0).Success, Is.False);
        Assert.That(File.ReadAllText(paths.BackupPath), Is.EqualTo(backup));
        Assert.That(failing.Load(0).Source, Is.EqualTo(SaveLoadSource.Backup));
        Assert.That(failing.Load(0).Data.playerName, Is.EqualTo("backup"));
    }

    [Test]
    public void Save_FuturePrimaryIsRejectedBeforeChangingExistingTemporary()
    {
        AtomicSaveStorage storage = CreateStorage();
        SaveSlotPaths paths = storage.GetPaths(0);
        const string future = "{\"schemaVersion\":999,\"currentScene\":\"Future\"}";
        string temporary = _codec.Encode(CreateData("temporary"));
        File.WriteAllText(paths.PrimaryPath, future);
        File.WriteAllText(paths.TemporaryPath, temporary);

        Assert.That(storage.Save(CreateData("replacement"), 0).Failure, Is.EqualTo(SaveStorageFailure.NotAllowed));
        Assert.That(File.ReadAllText(paths.PrimaryPath), Is.EqualTo(future));
        Assert.That(File.ReadAllText(paths.TemporaryPath), Is.EqualTo(temporary));
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
        public bool ThrowOnWrite { get; set; }
        public bool ThrowOnMove { get; set; }

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
            if (ThrowOnWrite)
                throw new IOException("Injected write failure.");
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
            if (ThrowOnMove)
                throw new IOException("Injected move failure.");
            _inner.MoveFile(sourcePath, destinationPath);
        }

        public void DeleteFile(string path)
        {
            _inner.DeleteFile(path);
        }
    }
}
