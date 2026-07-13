using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

public enum SequenceDeletionKind
{
    BattleOwned,
    Standalone
}

public sealed class SequenceDeletionAnalysis
{
    internal SequenceDeletionAnalysis(
        SequenceDeletionKind kind,
        ActionSequenceAsset sequence,
        BattleScenarioData owningBattle,
        IList<SequenceUsageRecord> blockingUsages,
        IList<string> blockingReasons)
    {
        Kind = kind;
        Sequence = sequence;
        OwningBattle = owningBattle;
        BlockingUsages = new List<SequenceUsageRecord>(blockingUsages ?? Array.Empty<SequenceUsageRecord>());
        BlockingReasons = new List<string>(blockingReasons ?? Array.Empty<string>());
    }

    public SequenceDeletionKind Kind { get; }
    public ActionSequenceAsset Sequence { get; }
    public BattleScenarioData OwningBattle { get; }
    public IReadOnlyList<SequenceUsageRecord> BlockingUsages { get; }
    public IReadOnlyList<string> BlockingReasons { get; }
    public bool CanDelete => BlockingUsages.Count == 0 && BlockingReasons.Count == 0;
}

public enum SequenceDeletionStatus
{
    Succeeded,
    Blocked,
    ValidationFailed,
    SourceMissing,
    Conflict,
    RecoveryFailed,
    SaveFailed,
    SourceDeleteFailed,
    RuntimeAssetDeleteFailed,
    RollbackFailed
}

public sealed class SequenceDeletionResult
{
    public SequenceDeletionStatus Status;
    public SequenceDeletionAnalysis Analysis;
    public SequenceSaveResult SaveResult;
    public ScenarioValidationResult Validation = new ScenarioValidationResult();
    public string ErrorMessage = string.Empty;
    public string RecoveryError = string.Empty;
    public bool SourceCommitted;

    public bool Success => Status == SequenceDeletionStatus.Succeeded;
}

public sealed class SequenceDeletionSourceBackup
{
    public string SourcePath = string.Empty;
    public byte[] SourceBytes = Array.Empty<byte>();
    public byte[] MetaBytes;

    public string SourceText
    {
        get
        {
            byte[] bytes = SourceBytes ?? Array.Empty<byte>();
            int offset = bytes.Length >= 3
                && bytes[0] == 0xEF
                && bytes[1] == 0xBB
                && bytes[2] == 0xBF
                ? 3
                : 0;
            return Encoding.UTF8.GetString(bytes, offset, bytes.Length - offset);
        }
    }
}

public interface ISequenceDeletionSaveService
{
    SequenceSaveResult Save(ISequenceSaveTarget target);
}

public interface ISequenceDeletionRecovery
{
    void Capture(ISequenceSaveTarget target);
}

public interface ISequenceDeletionAssetStore
{
    string GetAssetPath(UnityEngine.Object asset);
    bool IsSubAsset(UnityEngine.Object asset);
    bool SourceExists(string path);
    SequenceDeletionSourceBackup CaptureSource(string path);
    bool DeleteSource(SequenceDeletionSourceBackup backup, out string error);
    bool RestoreSource(SequenceDeletionSourceBackup backup, out string error);
    bool DeleteRuntimeAsset(UnityEngine.Object asset, out string error);
}

public interface ISequenceDeletionService
{
    SequenceDeletionResult Delete(
        ActionSequenceAsset sequence,
        BattleScenarioData owningBattle,
        SequenceUsageIndex usage,
        ActionCatalogAsset catalog = null);
}

public sealed class SequenceDeletionCoordinator : ISequenceDeletionService
{
    private readonly ISequenceDeletionSaveService _saveService;
    private readonly ISequenceDeletionRecovery _recovery;
    private readonly ISequenceDeletionAssetStore _assets;

    public SequenceDeletionCoordinator()
        : this(
            new SequenceDeletionSaveService(),
            new SequenceDeletionRecovery(),
            new AssetDatabaseSequenceDeletionStore())
    {
    }

    public SequenceDeletionCoordinator(
        ISequenceDeletionSaveService saveService,
        ISequenceDeletionRecovery recovery,
        ISequenceDeletionAssetStore assets)
    {
        _saveService = saveService ?? throw new ArgumentNullException(nameof(saveService));
        _recovery = recovery ?? throw new ArgumentNullException(nameof(recovery));
        _assets = assets ?? throw new ArgumentNullException(nameof(assets));
    }

    public static SequenceDeletionAnalysis Analyze(
        ActionSequenceAsset sequence,
        BattleScenarioData owningBattle,
        SequenceUsageIndex usage)
    {
        var blockingUsages = new List<SequenceUsageRecord>();
        var blockingReasons = new List<string>();
        SequenceDeletionKind kind = owningBattle != null
            ? SequenceDeletionKind.BattleOwned
            : SequenceDeletionKind.Standalone;

        if (sequence == null)
        {
            blockingReasons.Add("삭제할 시퀀스가 없습니다.");
            return new SequenceDeletionAnalysis(
                kind,
                null,
                owningBattle,
                blockingUsages,
                blockingReasons);
        }

        string sequenceId = Normalize(sequence.SequenceId);
        if (string.IsNullOrEmpty(sequenceId))
        {
            blockingReasons.Add("Sequence ID가 비어 있습니다.");
        }

        string sourcePath = owningBattle != null
            ? owningBattle.Source?.SourcePath
            : sequence.Source?.SourcePath;
        if (string.IsNullOrWhiteSpace(sourcePath))
        {
            blockingReasons.Add("삭제할 YAML 원본 경로가 없습니다.");
        }

        if (usage == null)
        {
            blockingReasons.Add("시퀀스 사용 위치 인덱스를 확인할 수 없습니다.");
        }

        if (owningBattle?.Sequences != null)
        {
            int ownershipCount = 0;
            for (int i = 0; i < owningBattle.Sequences.Count; i++)
            {
                if (ReferenceEquals(owningBattle.Sequences[i], sequence))
                {
                    ownershipCount++;
                }
            }

            if (ownershipCount > 1)
            {
                blockingReasons.Add("같은 Battle Scenario에 동일한 시퀀스가 중복 등록되어 있습니다.");
            }
        }

        IReadOnlyList<SequenceUsageRecord> usages = usage?.GetUsages(sequenceId)
            ?? Array.Empty<SequenceUsageRecord>();
        for (int i = 0; i < usages.Count; i++)
        {
            SequenceUsageRecord record = usages[i];
            if (IsCurrentBattleOwnership(record, sequence, owningBattle))
            {
                continue;
            }

            blockingUsages.Add(record);
        }

        return new SequenceDeletionAnalysis(
            kind,
            sequence,
            owningBattle,
            blockingUsages,
            blockingReasons);
    }

    public SequenceDeletionResult Delete(
        ActionSequenceAsset sequence,
        BattleScenarioData owningBattle,
        SequenceUsageIndex usage,
        ActionCatalogAsset catalog = null)
    {
        SequenceDeletionAnalysis analysis = Analyze(sequence, owningBattle, usage);
        if (!analysis.CanDelete)
        {
            return Result(SequenceDeletionStatus.Blocked, analysis, "참조 또는 필수 정보 때문에 삭제할 수 없습니다.");
        }

        return analysis.Kind == SequenceDeletionKind.BattleOwned
            ? DeleteBattleOwned(analysis, catalog)
            : DeleteStandalone(analysis, catalog);
    }

    private SequenceDeletionResult DeleteBattleOwned(
        SequenceDeletionAnalysis analysis,
        ActionCatalogAsset catalog)
    {
        BattleScenarioData battle = analysis.OwningBattle;
        ActionSequenceAsset sequence = analysis.Sequence;
        int index = battle.Sequences.IndexOf(sequence);
        if (index < 0)
        {
            return Result(SequenceDeletionStatus.Blocked, analysis, "현재 Battle Scenario에서 시퀀스를 찾을 수 없습니다.");
        }

        var target = new BattleScenarioSaveTarget(battle, catalog);
        try
        {
            _recovery.Capture(target);
        }
        catch (Exception exception)
        {
            return Result(SequenceDeletionStatus.RecoveryFailed, analysis, "삭제 전 복구 기록을 만들지 못했습니다: " + exception.Message);
        }

        battle.Sequences.RemoveAt(index);
        EditorUtility.SetDirty(battle);
        SequenceSaveResult save = _saveService.Save(target);
        if (save == null || !save.Success)
        {
            battle.Sequences.Insert(index, sequence);
            EditorUtility.SetDirty(battle);
            SequenceDeletionResult failed = Result(
                SequenceDeletionStatus.SaveFailed,
                analysis,
                save?.ErrorMessage ?? "Battle YAML 저장에 실패했습니다.");
            failed.SaveResult = save;
            return failed;
        }

        if (!_assets.IsSubAsset(sequence))
        {
            SequenceDeletionResult detached = Result(
                SequenceDeletionStatus.Succeeded,
                analysis,
                string.Empty);
            detached.SaveResult = save;
            detached.SourceCommitted = true;
            return detached;
        }
        if (!_assets.DeleteRuntimeAsset(sequence, out string assetError))
        {
            SequenceDeletionResult failed = Result(
                SequenceDeletionStatus.RuntimeAssetDeleteFailed,
                analysis,
                "YAML은 저장됐지만 Runtime Asset 제거에 실패했습니다: " + assetError);
            failed.SaveResult = save;
            failed.SourceCommitted = true;
            return failed;
        }

        SequenceDeletionResult succeeded = Result(SequenceDeletionStatus.Succeeded, analysis, string.Empty);
        succeeded.SaveResult = save;
        succeeded.SourceCommitted = true;
        return succeeded;
    }

    private SequenceDeletionResult DeleteStandalone(
        SequenceDeletionAnalysis analysis,
        ActionCatalogAsset catalog)
    {
        ActionSequenceAsset sequence = analysis.Sequence;
        var target = new StandaloneSequenceSaveTarget(sequence, catalog);
        SequenceSaveExportResult export;
        try
        {
            export = target.Export() ?? new SequenceSaveExportResult();
        }
        catch (Exception exception)
        {
            return Result(SequenceDeletionStatus.ValidationFailed, analysis, "시퀀스 YAML 검증 준비에 실패했습니다: " + exception.Message);
        }

        var validation = new ScenarioValidationResult();
        validation.Merge(export.Validation);
        if (!validation.HasErrors)
        {
            validation.Merge(target.ValidateRoundTrip(export.Text ?? string.Empty, target.SourcePath));
        }

        if (validation.HasErrors)
        {
            SequenceDeletionResult invalid = Result(
                SequenceDeletionStatus.ValidationFailed,
                analysis,
                "현재 시퀀스를 YAML로 왕복 검증할 수 없습니다.");
            invalid.Validation.Merge(validation);
            return invalid;
        }

        if (!_assets.SourceExists(target.SourcePath))
        {
            return Result(SequenceDeletionStatus.SourceMissing, analysis, "삭제할 YAML 원본 파일이 없습니다.");
        }

        SequenceDeletionSourceBackup backup;
        try
        {
            backup = _assets.CaptureSource(target.SourcePath);
        }
        catch (Exception exception)
        {
            return Result(SequenceDeletionStatus.SourceMissing, analysis, "YAML 원본을 읽지 못했습니다: " + exception.Message);
        }

        string storedHash = target.StoredSourceHash ?? string.Empty;
        string diskHash = ScenarioSourceHash.Compute(backup?.SourceText ?? string.Empty);
        if (string.IsNullOrWhiteSpace(storedHash)
            || !string.Equals(storedHash, diskHash, StringComparison.Ordinal))
        {
            return Result(SequenceDeletionStatus.Conflict, analysis, "에디터가 읽은 뒤 YAML이 바뀌었거나 기준 해시가 없습니다. 다시 불러온 뒤 삭제하세요.");
        }

        try
        {
            _recovery.Capture(target);
        }
        catch (Exception exception)
        {
            return Result(SequenceDeletionStatus.RecoveryFailed, analysis, "삭제 전 복구 기록을 만들지 못했습니다: " + exception.Message);
        }

        if (!_assets.DeleteSource(backup, out string sourceError))
        {
            return Result(SequenceDeletionStatus.SourceDeleteFailed, analysis, "YAML 원본 제거에 실패했습니다: " + sourceError);
        }

        if (_assets.DeleteRuntimeAsset(sequence, out string runtimeError))
        {
            SequenceDeletionResult succeeded = Result(
                SequenceDeletionStatus.Succeeded,
                analysis,
                string.Empty);
            succeeded.SourceCommitted = true;
            return succeeded;
        }

        if (!_assets.RestoreSource(backup, out string restoreError))
        {
            SequenceDeletionResult rollbackFailed = Result(
                SequenceDeletionStatus.RollbackFailed,
                analysis,
                "Runtime Asset 제거와 YAML 복원에 모두 실패했습니다: " + runtimeError);
            rollbackFailed.RecoveryError = restoreError;
            return rollbackFailed;
        }

        return Result(
            SequenceDeletionStatus.RuntimeAssetDeleteFailed,
            analysis,
            "Runtime Asset 제거에 실패해 YAML 원본을 복원했습니다: " + runtimeError);
    }

    private static SequenceDeletionResult Result(
        SequenceDeletionStatus status,
        SequenceDeletionAnalysis analysis,
        string error)
    {
        return new SequenceDeletionResult
        {
            Status = status,
            Analysis = analysis,
            ErrorMessage = error ?? string.Empty
        };
    }

    private static bool IsCurrentBattleOwnership(
        SequenceUsageRecord record,
        ActionSequenceAsset sequence,
        BattleScenarioData owningBattle)
    {
        return owningBattle != null
            && record != null
            && record.Kind == SequenceUsageKind.ScenarioOwnership
            && ReferenceEquals(record.SourceScenario, owningBattle)
            && ReferenceEquals(record.SourceSequence, sequence);
    }

    private static string Normalize(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
    }
}

internal sealed class SequenceDeletionSaveService : ISequenceDeletionSaveService
{
    private readonly SequenceSaveCoordinator _coordinator = new SequenceSaveCoordinator();

    public SequenceSaveResult Save(ISequenceSaveTarget target)
    {
        return _coordinator.Save(target);
    }
}

internal sealed class SequenceDeletionRecovery : ISequenceDeletionRecovery
{
    private readonly SequenceRecoveryStore _store = new SequenceRecoveryStore();

    public void Capture(ISequenceSaveTarget target)
    {
        _store.Capture(target);
    }
}

internal sealed class AssetDatabaseSequenceDeletionStore : ISequenceDeletionAssetStore
{
    public string GetAssetPath(UnityEngine.Object asset)
    {
        return asset != null ? AssetDatabase.GetAssetPath(asset) ?? string.Empty : string.Empty;
    }

    public bool IsSubAsset(UnityEngine.Object asset)
    {
        return asset != null && AssetDatabase.IsSubAsset(asset);
    }

    public bool SourceExists(string path)
    {
        return !string.IsNullOrWhiteSpace(path) && File.Exists(Absolute(path));
    }

    public SequenceDeletionSourceBackup CaptureSource(string path)
    {
        string absolute = Absolute(path);
        string meta = absolute + ".meta";
        return new SequenceDeletionSourceBackup
        {
            SourcePath = Normalize(path),
            SourceBytes = File.ReadAllBytes(absolute),
            MetaBytes = File.Exists(meta) ? File.ReadAllBytes(meta) : null
        };
    }

    public bool DeleteSource(SequenceDeletionSourceBackup backup, out string error)
    {
        error = string.Empty;
        if (backup == null || string.IsNullOrWhiteSpace(backup.SourcePath))
        {
            error = "원본 백업이 없습니다.";
            return false;
        }

        try
        {
            string path = Normalize(backup.SourcePath);
            if (path.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase))
            {
                if (!AssetDatabase.DeleteAsset(path))
                {
                    error = "AssetDatabase가 YAML을 제거하지 못했습니다.";
                    return false;
                }
            }
            else
            {
                File.Delete(Absolute(path));
            }

            return true;
        }
        catch (Exception exception)
        {
            error = exception.Message;
            return false;
        }
    }

    public bool RestoreSource(SequenceDeletionSourceBackup backup, out string error)
    {
        error = string.Empty;
        try
        {
            string absolute = Absolute(backup.SourcePath);
            Directory.CreateDirectory(Path.GetDirectoryName(absolute) ?? string.Empty);
            File.WriteAllBytes(absolute, backup.SourceBytes ?? Array.Empty<byte>());
            if (backup.MetaBytes != null)
            {
                File.WriteAllBytes(absolute + ".meta", backup.MetaBytes);
            }

            string path = Normalize(backup.SourcePath);
            if (path.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase))
            {
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            }

            return true;
        }
        catch (Exception exception)
        {
            error = exception.Message;
            return false;
        }
    }

    public bool DeleteRuntimeAsset(UnityEngine.Object asset, out string error)
    {
        error = string.Empty;
        if (asset == null)
        {
            error = "Runtime Asset이 없습니다.";
            return false;
        }

        try
        {
            string path = GetAssetPath(asset);
            if (string.IsNullOrWhiteSpace(path))
            {
                error = "Runtime Asset 경로가 없습니다.";
                return false;
            }

            if (IsSubAsset(asset))
            {
                UnityEngine.Object.DestroyImmediate(asset, true);
                AssetDatabase.SaveAssets();
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
                return true;
            }

            if (!AssetDatabase.DeleteAsset(path))
            {
                error = "AssetDatabase가 Runtime Asset을 제거하지 못했습니다.";
                return false;
            }

            return true;
        }
        catch (Exception exception)
        {
            error = exception.Message;
            return false;
        }
    }

    private static string Absolute(string path)
    {
        return ScenarioSourcePathPolicy.RequireProjectYamlAbsolute(path);
    }

    private static string Normalize(string path)
    {
        return (path ?? string.Empty).Replace('\\', '/');
    }
}
