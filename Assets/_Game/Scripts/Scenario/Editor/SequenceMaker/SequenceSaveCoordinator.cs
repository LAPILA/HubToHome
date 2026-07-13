using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

public enum SequenceSaveStatus
{
    Succeeded,
    InvalidTarget,
    ExportFailed,
    ValidationFailed,
    ReadFailed,
    Conflict,
    TempVerificationFailed,
    RoundTripFailed,
    WriteFailed,
    MetadataUpdateFailed
}

public sealed class SequenceSaveOptions
{
    public bool OverwriteExternalChanges;
}

public sealed class SequenceSaveExportResult
{
    public string Text = string.Empty;
    public ScenarioValidationResult Validation = new ScenarioValidationResult();

    public bool Success => !Validation.HasErrors;
}

public sealed class SequenceSaveResult
{
    public SequenceSaveStatus Status;
    public string SourcePath = string.Empty;
    public string SourceHash = string.Empty;
    public DateTime WrittenAtUtc;
    public string ErrorMessage = string.Empty;
    public Exception Exception;
    public SequenceSourceConflict Conflict;
    public ScenarioValidationResult Validation = new ScenarioValidationResult();

    public bool Success => Status == SequenceSaveStatus.Succeeded;
}

public interface ISequenceSaveTarget
{
    UnityEngine.Object RuntimeAsset { get; }
    string TargetId { get; }
    string SourcePath { get; }
    string StoredSourceHash { get; }

    SequenceSaveExportResult Export();
    ScenarioValidationResult ValidateRoundTrip(string sourceText, string sourcePath);
    void ApplySourceMetadata(string sourcePath, string sourceHash, DateTime writtenAtUtc);
}

public interface ISequenceSourceFileSystem
{
    bool FileExists(string path);
    string ReadAllText(string path);
    void WriteAllText(string path, string text);
    void ReplaceFile(string sourcePath, string destinationPath);
    void MoveFile(string sourcePath, string destinationPath);
    void DeleteFile(string path);
}

public interface ISequenceSaveClock
{
    DateTime UtcNow { get; }
}

public interface ISequenceTemporaryPathProvider
{
    string Create(string destinationPath);
}

public sealed class SequenceSaveCoordinator
{
    private readonly ISequenceSourceFileSystem _fileSystem;
    private readonly ISequenceSaveClock _clock;
    private readonly ISequenceTemporaryPathProvider _temporaryPathProvider;

    public SequenceSaveCoordinator()
        : this(
            new SystemSequenceSourceFileSystem(),
            new SystemSequenceSaveClock(),
            new SameDirectorySequenceTemporaryPathProvider())
    {
    }

    public SequenceSaveCoordinator(
        ISequenceSourceFileSystem fileSystem,
        ISequenceSaveClock clock,
        ISequenceTemporaryPathProvider temporaryPathProvider)
    {
        _fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _temporaryPathProvider = temporaryPathProvider
            ?? throw new ArgumentNullException(nameof(temporaryPathProvider));
    }

    public SequenceSaveResult Save(
        ISequenceSaveTarget target,
        SequenceSaveOptions options = null)
    {
        var result = new SequenceSaveResult();
        options = options ?? new SequenceSaveOptions();
        if (target == null)
        {
            return Fail(
                result,
                SequenceSaveStatus.InvalidTarget,
                "저장할 시퀀스 대상이 없습니다.");
        }

        result.SourcePath = NormalizePath(target.SourcePath);
        if (string.IsNullOrEmpty(result.SourcePath))
        {
            result.Validation.AddError(
                "sequence.save.path.required",
                "저장할 YAML 경로가 필요합니다.",
                target.TargetId);
            return Fail(
                result,
                SequenceSaveStatus.InvalidTarget,
                "저장할 YAML 경로가 필요합니다.");
        }

        if (_fileSystem is SystemSequenceSourceFileSystem)
        {
            if (!ScenarioSourcePathPolicy.TryNormalize(
                    result.SourcePath,
                    out string safeSourcePath,
                    out string pathError))
            {
                result.Validation.AddError(
                    "sequence.save.path.unsafe",
                    pathError,
                    target.TargetId);
                return Fail(result, SequenceSaveStatus.InvalidTarget, pathError);
            }

            result.SourcePath = safeSourcePath;
        }
        SequenceSaveExportResult export;
        try
        {
            export = target.Export() ?? new SequenceSaveExportResult();
        }
        catch (Exception exception)
        {
            return Fail(
                result,
                SequenceSaveStatus.ExportFailed,
                "YAML 생성 중 오류가 발생했습니다: " + exception.Message,
                exception);
        }

        result.Validation.Merge(export.Validation);
        if (!export.Success)
        {
            return Fail(
                result,
                SequenceSaveStatus.ValidationFailed,
                "현재 시퀀스를 YAML로 저장할 수 없습니다.");
        }

        string exportText = export.Text ?? string.Empty;
        bool destinationExisted;
        string observedDiskHash = string.Empty;
        try
        {
            destinationExisted = _fileSystem.FileExists(result.SourcePath);
            if (destinationExisted)
            {
                string currentText = _fileSystem.ReadAllText(result.SourcePath);
                observedDiskHash = ScenarioSourceHash.Compute(currentText);
                result.Conflict = SequenceSourceConflict.Detect(
                    result.SourcePath,
                    target.StoredSourceHash,
                    currentText);
                if (result.Conflict != null && !options.OverwriteExternalChanges)
                {
                    return Fail(
                        result,
                        SequenceSaveStatus.Conflict,
                        result.Conflict.Message);
                }
            }
        }
        catch (Exception exception)
        {
            return Fail(
                result,
                SequenceSaveStatus.ReadFailed,
                "기존 YAML을 확인하지 못했습니다: " + exception.Message,
                exception);
        }

        string temporaryPath = string.Empty;
        try
        {
            temporaryPath = NormalizePath(_temporaryPathProvider.Create(result.SourcePath));
            if (string.IsNullOrEmpty(temporaryPath)
                || string.Equals(temporaryPath, result.SourcePath, StringComparison.OrdinalIgnoreCase))
            {
                return Fail(
                    result,
                    SequenceSaveStatus.WriteFailed,
                    "안전 저장용 임시 파일 경로를 만들지 못했습니다.");
            }

            _fileSystem.WriteAllText(temporaryPath, exportText);
            string verifiedText = _fileSystem.ReadAllText(temporaryPath);
            if (!string.Equals(exportText, verifiedText, StringComparison.Ordinal))
            {
                return Fail(
                    result,
                    SequenceSaveStatus.TempVerificationFailed,
                    "임시 YAML을 다시 읽은 결과가 생성한 내용과 다릅니다.");
            }

            ScenarioValidationResult roundTrip = target.ValidateRoundTrip(
                verifiedText,
                temporaryPath) ?? new ScenarioValidationResult();
            result.Validation.Merge(roundTrip);
            if (roundTrip.HasErrors)
            {
                return Fail(
                    result,
                    SequenceSaveStatus.RoundTripFailed,
                    "임시 YAML을 다시 불러오는 검증에 실패했습니다.");
            }

            if (destinationExisted)
            {
                SequenceSourceConflict lateConflict = DetectLateConflict(
                    result.SourcePath,
                    observedDiskHash);
                if (lateConflict != null)
                {
                    result.Conflict = lateConflict;
                    return Fail(
                        result,
                        SequenceSaveStatus.Conflict,
                        lateConflict.Message);
                }

                _fileSystem.ReplaceFile(temporaryPath, result.SourcePath);
            }
            else
            {
                if (_fileSystem.FileExists(result.SourcePath))
                {
                    result.Conflict = SequenceSourceConflict.ChangedAfterValidation(
                        result.SourcePath,
                        string.Empty,
                        ScenarioSourceHash.Compute(_fileSystem.ReadAllText(result.SourcePath)));
                    return Fail(
                        result,
                        SequenceSaveStatus.Conflict,
                        result.Conflict.Message);
                }

                _fileSystem.MoveFile(temporaryPath, result.SourcePath);
            }

            result.SourceHash = ScenarioSourceHash.Compute(verifiedText);
            result.WrittenAtUtc = NormalizeUtc(_clock.UtcNow);
            try
            {
                target.ApplySourceMetadata(
                    result.SourcePath,
                    result.SourceHash,
                    result.WrittenAtUtc);
                if (target.RuntimeAsset != null)
                {
                    EditorUtility.SetDirty(target.RuntimeAsset);
                }
            }
            catch (Exception exception)
            {
                return Fail(
                    result,
                    SequenceSaveStatus.MetadataUpdateFailed,
                    "YAML은 저장됐지만 런타임 에셋 메타데이터 갱신에 실패했습니다: "
                    + exception.Message,
                    exception);
            }

            result.Status = SequenceSaveStatus.Succeeded;
            result.ErrorMessage = string.Empty;
            result.Conflict = null;
            return result;
        }
        catch (Exception exception)
        {
            return Fail(
                result,
                SequenceSaveStatus.WriteFailed,
                "YAML 안전 저장에 실패했습니다: " + exception.Message,
                exception);
        }
        finally
        {
            TryDeleteTemporary(temporaryPath, result);
        }
    }

    private SequenceSourceConflict DetectLateConflict(
        string sourcePath,
        string observedDiskHash)
    {
        if (!_fileSystem.FileExists(sourcePath))
        {
            return SequenceSourceConflict.ChangedAfterValidation(
                sourcePath,
                observedDiskHash,
                string.Empty);
        }

        string currentHash = ScenarioSourceHash.Compute(_fileSystem.ReadAllText(sourcePath));
        return string.Equals(currentHash, observedDiskHash, StringComparison.OrdinalIgnoreCase)
            ? null
            : SequenceSourceConflict.ChangedAfterValidation(
                sourcePath,
                observedDiskHash,
                currentHash);
    }

    private void TryDeleteTemporary(string temporaryPath, SequenceSaveResult result)
    {
        if (string.IsNullOrEmpty(temporaryPath))
        {
            return;
        }

        try
        {
            if (_fileSystem.FileExists(temporaryPath))
            {
                _fileSystem.DeleteFile(temporaryPath);
            }
        }
        catch (Exception exception)
        {
            result.Validation.AddWarning(
                "sequence.save.temp.cleanup.failed",
                "임시 YAML 정리에 실패했습니다: " + exception.Message,
                temporaryPath);
        }
    }

    private static SequenceSaveResult Fail(
        SequenceSaveResult result,
        SequenceSaveStatus status,
        string message,
        Exception exception = null)
    {
        result.Status = status;
        result.ErrorMessage = message ?? string.Empty;
        result.Exception = exception;
        return result;
    }

    private static DateTime NormalizeUtc(DateTime value)
    {
        if (value.Kind == DateTimeKind.Utc)
        {
            return value;
        }

        return value.Kind == DateTimeKind.Unspecified
            ? DateTime.SpecifyKind(value, DateTimeKind.Utc)
            : value.ToUniversalTime();
    }

    private static string NormalizePath(string path)
    {
        return string.IsNullOrWhiteSpace(path)
            ? string.Empty
            : path.Trim().Replace('\\', '/');
    }
}

public sealed class SystemSequenceSourceFileSystem : ISequenceSourceFileSystem
{
    private static readonly Encoding Utf8WithoutBom = new UTF8Encoding(false);

    public bool FileExists(string path)
    {
        return File.Exists(ToFullPath(path));
    }

    public string ReadAllText(string path)
    {
        return File.ReadAllText(ToFullPath(path), Encoding.UTF8);
    }

    public void WriteAllText(string path, string text)
    {
        string fullPath = ToFullPath(path);
        string directory = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(fullPath, text ?? string.Empty, Utf8WithoutBom);
    }

    public void ReplaceFile(string sourcePath, string destinationPath)
    {
        string source = ToFullPath(sourcePath);
        string destination = ToFullPath(destinationPath);
        string backup = destination + ".sequence-save-backup-" + Guid.NewGuid().ToString("N");
        try
        {
            File.Replace(source, destination, backup, true);
        }
        finally
        {
            TryDeleteBackup(backup);
        }
    }

    public void MoveFile(string sourcePath, string destinationPath)
    {
        File.Move(ToFullPath(sourcePath), ToFullPath(destinationPath));
    }

    public void DeleteFile(string path)
    {
        File.Delete(ToFullPath(path));
    }

    private static string ToFullPath(string path)
    {
        return Path.GetFullPath(path ?? string.Empty);
    }

    private static void TryDeleteBackup(string backup)
    {
        try
        {
            if (File.Exists(backup))
            {
                File.Delete(backup);
            }
        }
        catch
        {
            // The destination is already committed; a backup is safer than failing metadata sync.
        }
    }
}

public sealed class SystemSequenceSaveClock : ISequenceSaveClock
{
    public DateTime UtcNow => DateTime.UtcNow;
}

public sealed class SameDirectorySequenceTemporaryPathProvider : ISequenceTemporaryPathProvider
{
    public string Create(string destinationPath)
    {
        string normalized = string.IsNullOrWhiteSpace(destinationPath)
            ? string.Empty
            : destinationPath.Trim().Replace('\\', '/');
        string directory = Path.GetDirectoryName(normalized)?.Replace('\\', '/') ?? string.Empty;
        string fileName = Path.GetFileName(normalized);
        string temporaryName = "." + fileName + "." + Guid.NewGuid().ToString("N") + ".tmp";
        return string.IsNullOrEmpty(directory)
            ? temporaryName
            : directory.TrimEnd('/') + "/" + temporaryName;
    }
}

public sealed class StandaloneSequenceSaveTarget : ISequenceSaveTarget
{
    private readonly ActionSequenceAsset _sequence;
    private readonly ActionCatalogAsset _catalog;
    private readonly string _primaryMode;

    public StandaloneSequenceSaveTarget(
        ActionSequenceAsset sequence,
        ActionCatalogAsset catalog = null,
        string primaryMode = ActionSequenceSourceSync.DefaultPrimaryMode)
    {
        _sequence = sequence ?? throw new ArgumentNullException(nameof(sequence));
        _catalog = catalog;
        _primaryMode = primaryMode;
    }

    public UnityEngine.Object RuntimeAsset => _sequence;
    public string TargetId => _sequence.SequenceId ?? string.Empty;
    public string SourcePath => _sequence.Source != null
        ? _sequence.Source.SourcePath ?? string.Empty
        : string.Empty;
    public string StoredSourceHash => _sequence.Source != null
        ? _sequence.Source.SourceHash ?? string.Empty
        : string.Empty;

    public SequenceSaveExportResult Export()
    {
        ActionSequenceSourceExportResult exported = ActionSequenceSourceSync.Export(
            _sequence,
            _primaryMode);
        var result = new SequenceSaveExportResult { Text = exported.Text ?? string.Empty };
        result.Validation.Merge(exported.Validation);
        return result;
    }

    public ScenarioValidationResult ValidateRoundTrip(string sourceText, string sourcePath)
    {
        ActionSequenceSourceImportResult imported = ActionSequenceSourceSync.Import(
            sourceText,
            sourcePath);
        var validation = new ScenarioValidationResult();
        validation.Merge(imported.Validation);
        try
        {
            if (imported.Sequence != null && _catalog != null)
            {
                validation.Merge(ScenarioCatalogValidator.ValidateSequence(imported.Sequence, _catalog));
            }
        }
        finally
        {
            DestroyTemporary(imported.Sequence);
        }

        return validation;
    }

    public void ApplySourceMetadata(string sourcePath, string sourceHash, DateTime writtenAtUtc)
    {
        Undo.RecordObject(_sequence, "시퀀스 YAML 저장 메타데이터 갱신");
        ApplyMetadata(
            _sequence.Source ?? (_sequence.Source = new ScenarioSourceMetadata()),
            sourcePath,
            sourceHash,
            writtenAtUtc);
        EditorUtility.SetDirty(_sequence);
    }

    internal static void ApplyMetadata(
        ScenarioSourceMetadata metadata,
        string sourcePath,
        string sourceHash,
        DateTime writtenAtUtc)
    {
        metadata.SourcePath = string.IsNullOrWhiteSpace(sourcePath)
            ? string.Empty
            : sourcePath.Trim().Replace('\\', '/');
        metadata.SourceHash = sourceHash ?? string.Empty;
        metadata.ImportedAtIso8601 = NormalizeUtc(writtenAtUtc).ToString("O");
    }

    internal static void DestroyTemporary(UnityEngine.Object temporary)
    {
        if (temporary == null)
        {
            return;
        }

        if (Application.isPlaying)
        {
            UnityEngine.Object.Destroy(temporary);
        }
        else
        {
            UnityEngine.Object.DestroyImmediate(temporary);
        }
    }

    private static DateTime NormalizeUtc(DateTime value)
    {
        if (value.Kind == DateTimeKind.Utc)
        {
            return value;
        }

        return value.Kind == DateTimeKind.Unspecified
            ? DateTime.SpecifyKind(value, DateTimeKind.Utc)
            : value.ToUniversalTime();
    }
}

public sealed class BattleScenarioSaveTarget : ISequenceSaveTarget
{
    private readonly BattleScenarioData _scenario;
    private readonly ActionCatalogAsset _catalog;

    public BattleScenarioSaveTarget(
        BattleScenarioData scenario,
        ActionCatalogAsset catalog = null)
    {
        _scenario = scenario ?? throw new ArgumentNullException(nameof(scenario));
        _catalog = catalog;
    }

    public UnityEngine.Object RuntimeAsset => _scenario;
    public string TargetId => _scenario.ScenarioId ?? string.Empty;
    public string SourcePath => _scenario.Source != null
        ? _scenario.Source.SourcePath ?? string.Empty
        : string.Empty;
    public string StoredSourceHash => _scenario.Source != null
        ? _scenario.Source.SourceHash ?? string.Empty
        : string.Empty;

    public SequenceSaveExportResult Export()
    {
        ScenarioSourceYamlExportResult exported =
            new ScenarioSourceYamlExportCommand().ExportToText(_scenario);
        var result = new SequenceSaveExportResult { Text = exported.Text ?? string.Empty };
        result.Validation.Merge(exported.Validation);
        return result;
    }

    public ScenarioValidationResult ValidateRoundTrip(string sourceText, string sourcePath)
    {
        var resolver = new AssetDatabaseScenarioDialogueReferenceResolver();
        var importer = new ScenarioSourceImporter(
            new ScenarioSourceYamlParser(),
            resolver,
            resolver);
        ScenarioSourceSyncResult imported = importer.Import(sourceText, sourcePath);
        var validation = new ScenarioValidationResult();
        validation.Merge(imported.Validation);
        try
        {
            if (imported.Scenario != null && _catalog != null)
            {
                validation.Merge(
                    ScenarioCatalogValidator.ValidateBattleScenario(imported.Scenario, _catalog));
            }
        }
        finally
        {
            DestroyTemporaryScenario(imported.Scenario);
        }

        return validation;
    }

    public void ApplySourceMetadata(string sourcePath, string sourceHash, DateTime writtenAtUtc)
    {
        Undo.RecordObject(_scenario, "전투 시나리오 YAML 저장 메타데이터 갱신");
        StandaloneSequenceSaveTarget.ApplyMetadata(
            _scenario.Source ?? (_scenario.Source = new ScenarioSourceMetadata()),
            sourcePath,
            sourceHash,
            writtenAtUtc);
        EditorUtility.SetDirty(_scenario);

        if (_scenario.Sequences == null)
        {
            return;
        }

        for (int i = 0; i < _scenario.Sequences.Count; i++)
        {
            ActionSequenceAsset sequence = _scenario.Sequences[i];
            if (sequence == null)
            {
                continue;
            }

            Undo.RecordObject(sequence, "전투 시나리오 YAML 저장 메타데이터 갱신");
            StandaloneSequenceSaveTarget.ApplyMetadata(
                sequence.Source ?? (sequence.Source = new ScenarioSourceMetadata()),
                sourcePath,
                sourceHash,
                writtenAtUtc);
            EditorUtility.SetDirty(sequence);
        }
    }

    private static void DestroyTemporaryScenario(BattleScenarioData scenario)
    {
        if (scenario == null)
        {
            return;
        }

        if (scenario.Sequences != null)
        {
            for (int i = 0; i < scenario.Sequences.Count; i++)
            {
                StandaloneSequenceSaveTarget.DestroyTemporary(scenario.Sequences[i]);
            }
        }

        StandaloneSequenceSaveTarget.DestroyTemporary(scenario);
    }
}
