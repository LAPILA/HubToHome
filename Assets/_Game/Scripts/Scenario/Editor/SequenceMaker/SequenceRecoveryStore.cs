using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Newtonsoft.Json;
using UnityEditor;

public sealed class SequenceRecoverySnapshot
{
    public string SnapshotId = string.Empty;
    public string TargetId = string.Empty;
    public string TargetType = string.Empty;
    public string AssetPath = string.Empty;
    public string SourcePath = string.Empty;
    public string BaselineSourceHash = string.Empty;
    public string ContentHash = string.Empty;
    public string CreatedAtUtc = string.Empty;
    public string YamlFilePath = string.Empty;

    [JsonIgnore]
    public DateTime CreatedAt
    {
        get
        {
            return DateTime.TryParse(
                CreatedAtUtc,
                null,
                System.Globalization.DateTimeStyles.RoundtripKind,
                out DateTime parsed)
                ? parsed.ToUniversalTime()
                : DateTime.MinValue;
        }
    }
}

public sealed class SequenceRecoveryResult
{
    public bool Success;
    public SequenceRecoverySnapshot Snapshot;
    public ScenarioValidationResult Validation = new ScenarioValidationResult();
    public string Error = string.Empty;
}

public sealed class SequenceRecoveryStore
{
    public const string DefaultRoot = "Library/HubToHome/SequenceMakerRecovery";
    private static readonly Encoding Utf8WithoutBom = new UTF8Encoding(false);
    private readonly string _root;
    private readonly int _maxSnapshotsPerTarget;

    public SequenceRecoveryStore(string root = DefaultRoot, int maxSnapshotsPerTarget = 5)
    {
        _root = string.IsNullOrWhiteSpace(root) ? DefaultRoot : root.Trim();
        _maxSnapshotsPerTarget = Math.Max(1, maxSnapshotsPerTarget);
    }

    public SequenceRecoverySnapshot Capture(ISequenceSaveTarget target)
    {
        if (target?.RuntimeAsset == null)
        {
            return null;
        }
        SequenceSaveExportResult exported = target.Export();
        string text = exported?.Text ?? string.Empty;
        string contentHash = ScenarioSourceHash.Compute(text);
        List<SequenceRecoverySnapshot> existing = List(target);
        if (existing.Count > 0 && existing[0].ContentHash == contentHash)
        {
            return existing[0];
        }

        string targetDirectory = TargetDirectory(target);
        Directory.CreateDirectory(targetDirectory);
        DateTime now = DateTime.UtcNow;
        string snapshotId = now.ToString("yyyyMMdd-HHmmss-fff")
            + "-" + contentHash.Substring(0, Math.Min(8, contentHash.Length));
        string yamlPath = Path.Combine(targetDirectory, snapshotId + ".yaml");
        string metadataPath = Path.Combine(targetDirectory, snapshotId + ".json");
        var snapshot = new SequenceRecoverySnapshot
        {
            SnapshotId = snapshotId,
            TargetId = target.TargetId ?? string.Empty,
            TargetType = target.RuntimeAsset is BattleScenarioData ? "battle" : "sequence",
            AssetPath = AssetDatabase.GetAssetPath(target.RuntimeAsset) ?? string.Empty,
            SourcePath = target.SourcePath ?? string.Empty,
            BaselineSourceHash = target.StoredSourceHash ?? string.Empty,
            ContentHash = contentHash,
            CreatedAtUtc = now.ToString("O"),
            YamlFilePath = yamlPath.Replace('\\', '/')
        };
        WriteAtomic(yamlPath, text);
        WriteAtomic(metadataPath, JsonConvert.SerializeObject(snapshot, Formatting.Indented));
        Trim(target);
        return snapshot;
    }

    public List<SequenceRecoverySnapshot> List(ISequenceSaveTarget target)
    {
        var snapshots = new List<SequenceRecoverySnapshot>();
        if (target?.RuntimeAsset == null)
        {
            return snapshots;
        }
        string directory = TargetDirectory(target);
        if (!Directory.Exists(directory))
        {
            return snapshots;
        }
        string[] files = Directory.GetFiles(directory, "*.json", SearchOption.TopDirectoryOnly);
        for (int i = 0; i < files.Length; i++)
        {
            try
            {
                SequenceRecoverySnapshot snapshot =
                    JsonConvert.DeserializeObject<SequenceRecoverySnapshot>(
                        File.ReadAllText(files[i], Encoding.UTF8));
                string expectedYamlPath = Path.ChangeExtension(files[i], ".yaml");
                if (snapshot != null
                    && string.Equals(
                        snapshot.SnapshotId,
                        Path.GetFileNameWithoutExtension(files[i]),
                        StringComparison.Ordinal)
                    && PathsEqual(snapshot.YamlFilePath, expectedYamlPath)
                    && IsManagedYamlPath(expectedYamlPath)
                    && MatchesTarget(snapshot, target.RuntimeAsset)
                    && File.Exists(expectedYamlPath))
                {
                    snapshot.YamlFilePath = Path.GetFullPath(expectedYamlPath)
                        .Replace('\\', '/');
                    snapshots.Add(snapshot);
                }
            }
            catch
            {
                // A partial/corrupt recovery entry is ignored; other snapshots stay usable.
            }
        }
        snapshots.Sort((left, right) => right.CreatedAt.CompareTo(left.CreatedAt));
        return snapshots;
    }

    public SequenceRecoveryResult Restore(
        SequenceRecoverySnapshot snapshot,
        UnityEngine.Object target,
        ActionCatalogAsset catalog)
    {
        var result = new SequenceRecoveryResult { Snapshot = snapshot };
        if (snapshot == null || target == null)
        {
            result.Error = "복구 스냅샷 또는 대상 에셋을 찾지 못했습니다.";
            return result;
        }
        if (!IsManagedYamlPath(snapshot.YamlFilePath))
        {
            result.Error = "복구 스냅샷이 Sequence Maker 복구 저장소 밖을 가리킵니다.";
            return result;
        }
        if (!File.Exists(snapshot.YamlFilePath))
        {
            result.Error = "복구 스냅샷 또는 대상 에셋을 찾지 못했습니다.";
            return result;
        }
        if (!MatchesTarget(snapshot, target))
        {
            result.Error = "복구 스냅샷의 대상 정보가 현재 에셋과 일치하지 않습니다.";
            return result;
        }
        try
        {
            string text = File.ReadAllText(snapshot.YamlFilePath, Encoding.UTF8);
            if (!string.Equals(
                    ScenarioSourceHash.Compute(text),
                    snapshot.ContentHash,
                    StringComparison.OrdinalIgnoreCase))
            {
                result.Error = "복구 스냅샷 내용 해시가 맞지 않습니다.";
                return result;
            }
            if (target is BattleScenarioData battle)
            {
                ScenarioSourceRuntimeAssetReimportResult restored =
                    new ScenarioSourceRuntimeAssetReimportCommand().ReimportFromText(
                        battle,
                        text,
                        snapshot.SourcePath,
                        catalog);
                result.Validation.Merge(restored.Validation);
            }
            else if (target is ActionSequenceAsset sequence)
            {
                ActionSequenceSourceRuntimeAssetReimportResult restored =
                    ActionSequenceSourceSync.ReimportFromText(
                        sequence,
                        text,
                        snapshot.SourcePath,
                        catalog,
                        PrimaryMode(sequence));
                result.Validation.Merge(restored.Validation);
            }
            else
            {
                result.Error = "지원하지 않는 복구 대상입니다: " + target.GetType().Name;
                return result;
            }
            result.Success = !result.Validation.HasErrors;
            if (!result.Success && string.IsNullOrWhiteSpace(result.Error))
            {
                result.Error = "복구 YAML 검증에 실패했습니다.";
            }
        }
        catch (Exception exception)
        {
            result.Error = exception.Message;
        }
        return result;
    }

    private static bool MatchesTarget(
        SequenceRecoverySnapshot snapshot,
        UnityEngine.Object target)
    {
        string targetType;
        string targetId;
        if (target is BattleScenarioData battle)
        {
            targetType = "battle";
            targetId = battle.ScenarioId ?? string.Empty;
        }
        else if (target is ActionSequenceAsset sequence)
        {
            targetType = "sequence";
            targetId = sequence.SequenceId ?? string.Empty;
        }
        else
        {
            return false;
        }

        if (!string.Equals(
                snapshot.TargetType,
                targetType,
                StringComparison.Ordinal)
            || !string.Equals(
                snapshot.TargetId,
                targetId,
                StringComparison.Ordinal))
        {
            return false;
        }

        string currentAssetPath = AssetDatabase.GetAssetPath(target) ?? string.Empty;
        return string.IsNullOrWhiteSpace(snapshot.AssetPath)
            || string.IsNullOrWhiteSpace(currentAssetPath)
            || string.Equals(
                snapshot.AssetPath.Replace('\\', '/'),
                currentAssetPath.Replace('\\', '/'),
                StringComparison.OrdinalIgnoreCase);
    }

    public void Delete(SequenceRecoverySnapshot snapshot)
    {
        if (snapshot == null || !IsManagedYamlPath(snapshot.YamlFilePath))
        {
            return;
        }
        TryDelete(snapshot.YamlFilePath);
        string metadata = Path.ChangeExtension(snapshot.YamlFilePath, ".json");
        TryDelete(metadata);
    }

    public void Clear(ISequenceSaveTarget target)
    {
        string directory = target != null ? TargetDirectory(target) : string.Empty;
        if (Directory.Exists(directory))
        {
            Directory.Delete(directory, true);
        }
    }

    private void Trim(ISequenceSaveTarget target)
    {
        List<SequenceRecoverySnapshot> snapshots = List(target);
        for (int i = _maxSnapshotsPerTarget; i < snapshots.Count; i++)
        {
            Delete(snapshots[i]);
        }
    }

    private string TargetDirectory(ISequenceSaveTarget target)
    {
        string key = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(target.RuntimeAsset));
        if (string.IsNullOrWhiteSpace(key))
        {
            key = ScenarioSourceHash.Compute(
                target.RuntimeAsset.GetType().FullName + "|" + (target.TargetId ?? string.Empty));
        }
        return Path.GetFullPath(Path.Combine(_root, key));
    }

    private bool IsManagedYamlPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path)
            || !string.Equals(
                Path.GetExtension(path),
                ".yaml",
                StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        string root = Path.GetFullPath(_root)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            + Path.DirectorySeparatorChar;
        string fullPath;
        try
        {
            fullPath = Path.GetFullPath(path);
        }
        catch
        {
            return false;
        }
        return fullPath.StartsWith(root, StringComparison.OrdinalIgnoreCase);
    }

    private static bool PathsEqual(string left, string right)
    {
        if (string.IsNullOrWhiteSpace(left) || string.IsNullOrWhiteSpace(right))
        {
            return false;
        }
        try
        {
            return string.Equals(
                Path.GetFullPath(left),
                Path.GetFullPath(right),
                StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    private static void WriteAtomic(string path, string text)
    {
        string temporary = path + ".tmp";
        File.WriteAllText(temporary, text ?? string.Empty, Utf8WithoutBom);
        if (File.Exists(path))
        {
            File.Delete(path);
        }
        File.Move(temporary, path);
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
        }
    }

    private static string PrimaryMode(ActionSequenceAsset sequence)
    {
        if (sequence?.Contract?.AllowedPrimaryModes != null
            && sequence.Contract.AllowedPrimaryModes.Count > 0)
        {
            return sequence.Contract.AllowedPrimaryModes[0];
        }
        return ActionSequenceSourceSync.DefaultPrimaryMode;
    }
}
