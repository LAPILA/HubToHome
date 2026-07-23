using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

public interface ISaveFileSystem
{
    bool FileExists(string path);
    string ReadAllText(string path);
    void CreateDirectory(string path);
    void WriteAllTextDurable(string path, string content);
    void ReplaceFile(
        string sourcePath,
        string destinationPath,
        string backupPath);
    void MoveFile(string sourcePath, string destinationPath);
    void DeleteFile(string path);
}

public sealed class SystemSaveFileSystem : ISaveFileSystem
{
    private static readonly Encoding Utf8WithoutBom = new UTF8Encoding(false);

    public bool FileExists(string path)
    {
        return File.Exists(path);
    }

    public string ReadAllText(string path)
    {
        return File.ReadAllText(path, Encoding.UTF8);
    }

    public void CreateDirectory(string path)
    {
        if (!string.IsNullOrWhiteSpace(path))
            Directory.CreateDirectory(path);
    }

    public void WriteAllTextDurable(string path, string content)
    {
        using (var stream = new FileStream(
                   path,
                   FileMode.Create,
                   FileAccess.Write,
                   FileShare.None,
                   4096,
                   FileOptions.WriteThrough))
        using (var writer = new StreamWriter(
                   stream,
                   Utf8WithoutBom,
                   1024,
                   true))
        {
            writer.Write(content ?? string.Empty);
            writer.Flush();
            stream.Flush(true);
        }
    }

    public void ReplaceFile(
        string sourcePath,
        string destinationPath,
        string backupPath)
    {
        if (File.Exists(backupPath))
            File.Delete(backupPath);

        File.Replace(sourcePath, destinationPath, backupPath, true);
    }

    public void MoveFile(string sourcePath, string destinationPath)
    {
        File.Move(sourcePath, destinationPath);
    }

    public void DeleteFile(string path)
    {
        File.Delete(path);
    }
}

public sealed class SaveSlotPaths
{
    public SaveSlotPaths(string primaryPath)
    {
        PrimaryPath = primaryPath ?? string.Empty;
        BackupPath = PrimaryPath + ".bak";
        TemporaryPath = PrimaryPath + ".tmp";
        CorruptPath = PrimaryPath + ".corrupt";
    }

    public string PrimaryPath { get; }
    public string BackupPath { get; }
    public string TemporaryPath { get; }
    public string CorruptPath { get; }
}

public enum SaveStorageFailure
{
    None,
    InvalidArgument,
    Serialization,
    Verification,
    Io
}

public sealed class SaveStorageResult
{
    private SaveStorageResult()
    {
    }

    public bool Success { get; private set; }
    public SaveStorageFailure Failure { get; private set; }
    public string Message { get; private set; }

    public static SaveStorageResult Succeeded()
    {
        return new SaveStorageResult
        {
            Success = true,
            Failure = SaveStorageFailure.None,
            Message = string.Empty
        };
    }

    public static SaveStorageResult Failed(
        SaveStorageFailure failure,
        string message)
    {
        return new SaveStorageResult
        {
            Success = false,
            Failure = failure,
            Message = message ?? string.Empty
        };
    }
}

public enum SaveLoadSource
{
    None,
    Primary,
    Backup,
    Temporary
}

public enum SaveLoadFailure
{
    None,
    InvalidSlot,
    NotFound,
    NoValidCandidate
}

public sealed class SaveLoadResult
{
    private SaveLoadResult()
    {
    }

    public bool Success { get; private set; }
    public SaveData Data { get; private set; }
    public SaveLoadSource Source { get; private set; }
    public SaveLoadFailure Failure { get; private set; }
    public int SourceVersion { get; private set; }
    public bool WasMigrated { get; private set; }
    public string Message { get; private set; }

    public static SaveLoadResult Succeeded(
        SaveData data,
        SaveLoadSource source,
        SaveDecodeResult decode,
        string message)
    {
        return new SaveLoadResult
        {
            Success = true,
            Data = data,
            Source = source,
            Failure = SaveLoadFailure.None,
            SourceVersion = decode != null
                ? decode.SourceVersion
                : SaveSchema.CurrentVersion,
            WasMigrated = decode != null && decode.WasMigrated,
            Message = message ?? string.Empty
        };
    }

    public static SaveLoadResult Failed(
        SaveLoadFailure failure,
        string message)
    {
        return new SaveLoadResult
        {
            Success = false,
            Data = null,
            Source = SaveLoadSource.None,
            Failure = failure,
            SourceVersion = SaveSchema.LegacyVersion,
            WasMigrated = false,
            Message = message ?? string.Empty
        };
    }
}

public sealed class SaveCandidateInspection
{
    public SaveLoadSource Source { get; internal set; }
    public string Path { get; internal set; }
    public bool Exists { get; internal set; }
    public bool IsValid { get; internal set; }
    public int SourceVersion { get; internal set; }
    public bool WasMigrated { get; internal set; }
    public string Message { get; internal set; }
}

public sealed class SaveSlotInspection
{
    public int SlotIndex { get; internal set; }
    public SaveSlotPaths Paths { get; internal set; }
    public SaveCandidateInspection Primary { get; internal set; }
    public SaveCandidateInspection Backup { get; internal set; }
    public SaveCandidateInspection Temporary { get; internal set; }
    public bool CorruptExists { get; internal set; }
    public SaveLoadResult LoadResult { get; internal set; }
    public bool IsLoadable => LoadResult != null && LoadResult.Success;
}

public sealed class AtomicSaveStorage
{
    private readonly object _gate = new object();
    private readonly string _rootDirectory;
    private readonly ISaveFileSystem _fileSystem;
    private readonly SaveDataCodec _codec;

    public AtomicSaveStorage(
        string rootDirectory,
        ISaveFileSystem fileSystem = null,
        SaveDataCodec codec = null)
    {
        if (string.IsNullOrWhiteSpace(rootDirectory))
            throw new ArgumentException(
                "저장 루트 경로가 필요합니다.",
                nameof(rootDirectory));

        _rootDirectory = Path.GetFullPath(rootDirectory);
        _fileSystem = fileSystem ?? new SystemSaveFileSystem();
        _codec = codec ?? new SaveDataCodec();
    }

    public string RootDirectory => _rootDirectory;

    public SaveSlotPaths GetPaths(int slotIndex)
    {
        if (slotIndex < 0)
            throw new ArgumentOutOfRangeException(nameof(slotIndex));

        return new SaveSlotPaths(
            Path.Combine(
                _rootDirectory,
                "save_slot_" + slotIndex + ".json"));
    }

    public SaveStorageResult Save(SaveData data, int slotIndex)
    {
        if (data == null)
        {
            return SaveStorageResult.Failed(
                SaveStorageFailure.InvalidArgument,
                "저장 데이터가 없습니다.");
        }

        if (slotIndex < 0)
        {
            return SaveStorageResult.Failed(
                SaveStorageFailure.InvalidArgument,
                "슬롯 번호는 0 이상이어야 합니다.");
        }

        string json;
        try
        {
            json = _codec.Encode(data);
        }
        catch (Exception exception)
        {
            return SaveStorageResult.Failed(
                SaveStorageFailure.Serialization,
                "저장 데이터를 직렬화할 수 없습니다: " + exception.Message);
        }

        lock (_gate)
        {
            SaveSlotPaths paths = GetPaths(slotIndex);
            try
            {
                _fileSystem.CreateDirectory(_rootDirectory);
                DeleteIfExists(paths.TemporaryPath);
                _fileSystem.WriteAllTextDurable(paths.TemporaryPath, json);

                CandidateRead temporary = ReadCandidate(
                    paths.TemporaryPath,
                    SaveLoadSource.Temporary);
                if (!temporary.Success)
                {
                    return SaveStorageResult.Failed(
                        SaveStorageFailure.Verification,
                        "임시 저장 검증에 실패했습니다: " + temporary.Message);
                }

                bool primaryExists = _fileSystem.FileExists(paths.PrimaryPath);
                CandidateRead primary = primaryExists
                    ? ReadCandidate(paths.PrimaryPath, SaveLoadSource.Primary)
                    : CandidateRead.Missing(SaveLoadSource.Primary);

                if (primaryExists && primary.Success)
                {
                    _fileSystem.ReplaceFile(
                        paths.TemporaryPath,
                        paths.PrimaryPath,
                        paths.BackupPath);
                }
                else
                {
                    if (primaryExists)
                    {
                        DeleteIfExists(paths.CorruptPath);
                        _fileSystem.MoveFile(
                            paths.PrimaryPath,
                            paths.CorruptPath);
                    }

                    _fileSystem.MoveFile(
                        paths.TemporaryPath,
                        paths.PrimaryPath);
                }

                return SaveStorageResult.Succeeded();
            }
            catch (Exception exception)
            {
                return SaveStorageResult.Failed(
                    SaveStorageFailure.Io,
                    "저장 파일 교체에 실패했습니다: " + exception.Message);
            }
            finally
            {
                TryDeleteWithoutThrow(paths.TemporaryPath);
            }
        }
    }

    public SaveLoadResult Load(int slotIndex)
    {
        if (slotIndex < 0)
        {
            return SaveLoadResult.Failed(
                SaveLoadFailure.InvalidSlot,
                "슬롯 번호는 0 이상이어야 합니다.");
        }

        lock (_gate)
        {
            SaveSlotPaths paths = GetPaths(slotIndex);
            return LoadWithoutLock(paths);
        }
    }

    public SaveStorageResult Delete(int slotIndex)
    {
        if (slotIndex < 0)
        {
            return SaveStorageResult.Failed(
                SaveStorageFailure.InvalidArgument,
                "슬롯 번호는 0 이상이어야 합니다.");
        }

        lock (_gate)
        {
            SaveSlotPaths paths = GetPaths(slotIndex);
            try
            {
                DeleteIfExists(paths.PrimaryPath);
                DeleteIfExists(paths.BackupPath);
                DeleteIfExists(paths.TemporaryPath);
                DeleteIfExists(paths.CorruptPath);
                return SaveStorageResult.Succeeded();
            }
            catch (Exception exception)
            {
                return SaveStorageResult.Failed(
                    SaveStorageFailure.Io,
                    "저장 슬롯 삭제에 실패했습니다: " + exception.Message);
            }
        }
    }

    public SaveSlotInspection Inspect(int slotIndex)
    {
        if (slotIndex < 0)
            return CreateInvalidInspection(slotIndex);

        lock (_gate)
        {
            SaveSlotPaths paths = GetPaths(slotIndex);
            return new SaveSlotInspection
            {
                SlotIndex = slotIndex,
                Paths = paths,
                Primary = InspectCandidate(
                    paths.PrimaryPath,
                    SaveLoadSource.Primary),
                Backup = InspectCandidate(
                    paths.BackupPath,
                    SaveLoadSource.Backup),
                Temporary = InspectCandidate(
                    paths.TemporaryPath,
                    SaveLoadSource.Temporary),
                CorruptExists = _fileSystem.FileExists(paths.CorruptPath),
                LoadResult = LoadWithoutLock(paths)
            };
        }
    }

    private SaveLoadResult LoadWithoutLock(SaveSlotPaths paths)
    {
        var failures = new List<string>();
        bool anyCandidateExists = false;

        SaveLoadResult result = TryLoadCandidate(
            paths.PrimaryPath,
            SaveLoadSource.Primary,
            failures,
            ref anyCandidateExists);
        if (result != null)
            return result;

        result = TryLoadCandidate(
            paths.BackupPath,
            SaveLoadSource.Backup,
            failures,
            ref anyCandidateExists);
        if (result != null)
            return result;

        result = TryLoadCandidate(
            paths.TemporaryPath,
            SaveLoadSource.Temporary,
            failures,
            ref anyCandidateExists);
        if (result != null)
            return result;

        if (!anyCandidateExists)
        {
            return SaveLoadResult.Failed(
                SaveLoadFailure.NotFound,
                "저장 파일이 없습니다.");
        }

        return SaveLoadResult.Failed(
            SaveLoadFailure.NoValidCandidate,
            string.Join(" | ", failures));
    }

    private SaveLoadResult TryLoadCandidate(
        string path,
        SaveLoadSource source,
        List<string> failures,
        ref bool anyCandidateExists)
    {
        if (!_fileSystem.FileExists(path))
            return null;

        anyCandidateExists = true;
        CandidateRead candidate = ReadCandidate(path, source);
        if (!candidate.Success)
        {
            failures.Add(source + ": " + candidate.Message);
            return null;
        }

        string recoveryMessage = source == SaveLoadSource.Primary
            ? string.Empty
            : BuildRecoveryMessage(source, failures);
        return SaveLoadResult.Succeeded(
            candidate.Decode.Data,
            source,
            candidate.Decode,
            recoveryMessage);
    }

    private CandidateRead ReadCandidate(
        string path,
        SaveLoadSource source)
    {
        try
        {
            string json = _fileSystem.ReadAllText(path);
            SaveDecodeResult decode = _codec.Decode(json);
            return decode.Success
                ? CandidateRead.Succeeded(source, decode)
                : CandidateRead.Failed(source, decode.Message, decode);
        }
        catch (Exception exception)
        {
            return CandidateRead.Failed(
                source,
                "파일을 읽을 수 없습니다: " + exception.Message,
                null);
        }
    }

    private SaveCandidateInspection InspectCandidate(
        string path,
        SaveLoadSource source)
    {
        bool exists = _fileSystem.FileExists(path);
        if (!exists)
        {
            return new SaveCandidateInspection
            {
                Source = source,
                Path = path,
                Exists = false,
                IsValid = false,
                SourceVersion = SaveSchema.LegacyVersion,
                WasMigrated = false,
                Message = "파일 없음"
            };
        }

        CandidateRead candidate = ReadCandidate(path, source);
        return new SaveCandidateInspection
        {
            Source = source,
            Path = path,
            Exists = true,
            IsValid = candidate.Success,
            SourceVersion = candidate.Decode != null
                ? candidate.Decode.SourceVersion
                : SaveSchema.LegacyVersion,
            WasMigrated = candidate.Decode != null
                && candidate.Decode.WasMigrated,
            Message = candidate.Success ? string.Empty : candidate.Message
        };
    }

    private static string BuildRecoveryMessage(
        SaveLoadSource source,
        List<string> failures)
    {
        string prefix = failures != null && failures.Count > 0
            ? string.Join(" | ", failures) + " | "
            : string.Empty;
        return prefix + source + " 복구본을 사용했습니다.";
    }

    private void DeleteIfExists(string path)
    {
        if (_fileSystem.FileExists(path))
            _fileSystem.DeleteFile(path);
    }

    private void TryDeleteWithoutThrow(string path)
    {
        try
        {
            DeleteIfExists(path);
        }
        catch
        {
            // The committed primary or previous backup is still the recovery source.
        }
    }

    private static SaveSlotInspection CreateInvalidInspection(int slotIndex)
    {
        return new SaveSlotInspection
        {
            SlotIndex = slotIndex,
            Paths = null,
            Primary = null,
            Backup = null,
            Temporary = null,
            CorruptExists = false,
            LoadResult = SaveLoadResult.Failed(
                SaveLoadFailure.InvalidSlot,
                "슬롯 번호는 0 이상이어야 합니다.")
        };
    }

    private sealed class CandidateRead
    {
        private CandidateRead()
        {
        }

        public bool Success { get; private set; }
        public SaveLoadSource Source { get; private set; }
        public SaveDecodeResult Decode { get; private set; }
        public string Message { get; private set; }

        public static CandidateRead Succeeded(
            SaveLoadSource source,
            SaveDecodeResult decode)
        {
            return new CandidateRead
            {
                Success = true,
                Source = source,
                Decode = decode,
                Message = string.Empty
            };
        }

        public static CandidateRead Failed(
            SaveLoadSource source,
            string message,
            SaveDecodeResult decode)
        {
            return new CandidateRead
            {
                Success = false,
                Source = source,
                Decode = decode,
                Message = message ?? string.Empty
            };
        }

        public static CandidateRead Missing(SaveLoadSource source)
        {
            return new CandidateRead
            {
                Success = false,
                Source = source,
                Decode = null,
                Message = "파일 없음"
            };
        }
    }
}
