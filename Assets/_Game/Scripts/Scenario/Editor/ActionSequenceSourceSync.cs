using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

public sealed class ActionSequenceSourceExportResult
{
    public ActionSequenceSourceDocument Document;
    public string Text = string.Empty;
    public ScenarioValidationResult Validation = new ScenarioValidationResult();

    public bool Success
    {
        get { return !Validation.HasErrors; }
    }
}

public sealed class ActionSequenceSourceImportResult
{
    public ActionSequenceAsset Sequence;
    public ScenarioValidationResult Validation = new ScenarioValidationResult();

    public bool Success
    {
        get { return Sequence != null && !Validation.HasErrors; }
    }
}

public sealed class ActionSequenceSourceRuntimeAssetReimportResult
{
    public ActionSequenceAsset Target;
    public ScenarioValidationResult Validation = new ScenarioValidationResult();

    public bool Success
    {
        get { return Target != null && !Validation.HasErrors; }
    }
}

public static class ActionSequenceSourceSync
{
    public const string DefaultPrimaryMode = "overworld";

    public static ActionSequenceSourceExportResult Export(
        ActionSequenceAsset sequence,
        string primaryMode = DefaultPrimaryMode)
    {
        var result = new ActionSequenceSourceExportResult();
        if (sequence == null)
        {
            result.Validation.AddError(
                "sequence.source.sequence.required",
                "ActionSequenceAsset is required before source export.",
                string.Empty);
            return result;
        }

        string sequenceId = Normalize(sequence.SequenceId);
        if (string.IsNullOrEmpty(sequenceId))
        {
            result.Validation.AddError(
                "sequence.source.id.required",
                "Action Sequence requires SequenceId before source export.",
                string.Empty);
            return result;
        }

        var document = new ActionSequenceSourceDocument
        {
            SequenceId = sequenceId,
            DisplayNameKo = sequence.DisplayNameKo ?? string.Empty,
            PrimaryMode = Normalize(primaryMode)
        };
        if (string.IsNullOrEmpty(document.PrimaryMode))
        {
            document.PrimaryMode = DefaultPrimaryMode;
        }

        document.Actions = CloneActions(sequence.Actions);
        ScenarioSourceYamlWriteResult writeResult = new ScenarioSourceYamlWriter().Write(ToScenarioDocument(document));
        result.Document = document;
        result.Text = writeResult.Text ?? string.Empty;
        result.Validation.Merge(writeResult.Validation);
        return result;
    }

    public static ActionSequenceSourceImportResult Import(
        string sourceText,
        string sourcePath,
        DateTime? importedAtUtc = null)
    {
        var result = new ActionSequenceSourceImportResult();
        ScenarioSourceParseResult parseResult = new ScenarioSourceYamlParser().Parse(sourceText, sourcePath);
        result.Validation.Merge(parseResult.Validation);
        if (parseResult.Document == null || result.Validation.HasErrors)
        {
            return result;
        }

        ActionSequenceSourceDocument document = FromScenarioDocument(parseResult.Document, result.Validation, sourcePath);
        if (document == null || result.Validation.HasErrors)
        {
            return result;
        }

        ActionSequenceAsset sequence = ScriptableObject.CreateInstance<ActionSequenceAsset>();
        ApplyDocument(sequence, document, sourceText, sourcePath, importedAtUtc ?? DateTime.UtcNow);
        result.Sequence = sequence;
        return result;
    }

    public static ActionSequenceSourceRuntimeAssetReimportResult ReimportFromSourcePath(
        ActionSequenceAsset target,
        ActionCatalogAsset catalog = null,
        string primaryMode = DefaultPrimaryMode,
        DateTime? importedAtUtc = null)
    {
        var result = new ActionSequenceSourceRuntimeAssetReimportResult { Target = target };
        if (target == null)
        {
            result.Validation.AddError(
                "sequence.reimport.target.required",
                "ActionSequenceAsset target is required before source reimport.",
                string.Empty);
            return result;
        }

        string sourcePath = target.Source != null ? target.Source.SourcePath : string.Empty;
        if (string.IsNullOrWhiteSpace(sourcePath))
        {
            result.Validation.AddError(
                "sequence.reimport.path.required",
                "Action Sequence source YAML path is required before reimport.",
                target.SequenceId);
            return result;
        }

        try
        {
            string normalizedPath = NormalizePath(sourcePath);
            string sourceText = File.ReadAllText(Path.GetFullPath(normalizedPath));
            return ReimportFromText(target, sourceText, normalizedPath, catalog, primaryMode, importedAtUtc);
        }
        catch (Exception exception)
        {
            result.Validation.AddError(
                "sequence.reimport.read.failed",
                "Action Sequence source YAML read failed: " + exception.Message,
                sourcePath);
            return result;
        }
    }

    public static ActionSequenceSourceRuntimeAssetReimportResult ReimportFromText(
        ActionSequenceAsset target,
        string sourceText,
        string sourcePath,
        ActionCatalogAsset catalog = null,
        string primaryMode = DefaultPrimaryMode,
        DateTime? importedAtUtc = null)
    {
        var result = new ActionSequenceSourceRuntimeAssetReimportResult { Target = target };
        ActionSequenceSourceImportResult importResult = Import(sourceText, sourcePath, importedAtUtc);
        result.Validation.Merge(importResult.Validation);
        if (importResult.Sequence != null && catalog != null)
        {
            result.Validation.Merge(ScenarioCatalogValidator.ValidateSequence(importResult.Sequence, catalog));
        }

        if (result.Validation.HasErrors || importResult.Sequence == null)
        {
            DestroyTemporary(importResult.Sequence);
            return result;
        }

        Undo.RecordObject(target, "시퀀스 원본 YAML 런타임 반영");
        CopySequence(importResult.Sequence, target);
        EditorUtility.SetDirty(target);
        DestroyTemporary(importResult.Sequence);
        return result;
    }

    public static void SaveToSourcePath(ActionSequenceAsset sequence, string primaryMode = DefaultPrimaryMode)
    {
        if (sequence == null || sequence.Source == null || string.IsNullOrWhiteSpace(sequence.Source.SourcePath))
        {
            throw new InvalidOperationException("Action Sequence source path is required before save.");
        }

        ActionSequenceSourceExportResult exportResult = Export(sequence, primaryMode);
        if (!exportResult.Success)
        {
            throw new InvalidOperationException("Action Sequence source export validation failed.");
        }

        string sourcePath = NormalizePath(sequence.Source.SourcePath);
        File.WriteAllText(Path.GetFullPath(sourcePath), exportResult.Text);
        ApplySourceMetadata(sequence, exportResult.Text, sourcePath, DateTime.UtcNow);
        EditorUtility.SetDirty(sequence);
    }

    public static ActionSequenceSourceExportResult ExportToFile(
        ActionSequenceAsset sequence,
        string sourcePath,
        string primaryMode = DefaultPrimaryMode)
    {
        ActionSequenceSourceExportResult result = Export(sequence, primaryMode);
        if (!result.Success)
        {
            return result;
        }

        string normalizedPath = NormalizePath(sourcePath);
        if (string.IsNullOrWhiteSpace(normalizedPath))
        {
            result.Validation.AddError(
                "sequence.export.path.required",
                "Action Sequence export path is required.",
                sequence != null ? sequence.SequenceId : string.Empty);
            return result;
        }

        File.WriteAllText(Path.GetFullPath(normalizedPath), result.Text);
        ApplySourceMetadata(sequence, result.Text, normalizedPath, DateTime.UtcNow);
        EditorUtility.SetDirty(sequence);
        return result;
    }

    public static void CopySequence(ActionSequenceAsset source, ActionSequenceAsset target)
    {
        if (source == null || target == null)
        {
            return;
        }

        target.SequenceId = source.SequenceId ?? string.Empty;
        target.DisplayNameKo = source.DisplayNameKo ?? string.Empty;
        target.Actions = CloneActions(source.Actions);
        CopyMetadata(source.Source, target.Source ?? (target.Source = new ScenarioSourceMetadata()));
    }

    private static ScenarioSourceDocument ToScenarioDocument(ActionSequenceSourceDocument document)
    {
        var scenario = new ScenarioSourceDocument
        {
            Id = document.SequenceId,
            TitleKo = document.DisplayNameKo,
            PrimaryMode = document.PrimaryMode,
            OpeningModule = string.Empty,
            MemoryKey = string.Empty
        };
        scenario.Sequences.Add(new ScenarioSourceSequenceDocument
        {
            SequenceId = document.SequenceId,
            DisplayNameKo = document.DisplayNameKo,
            Actions = CloneActions(document.Actions)
        });
        return scenario;
    }

    private static ActionSequenceSourceDocument FromScenarioDocument(
        ScenarioSourceDocument scenario,
        ScenarioValidationResult validation,
        string sourcePath)
    {
        string sequenceId = Normalize(scenario != null ? scenario.Id : string.Empty);
        if (string.IsNullOrEmpty(sequenceId))
        {
            validation.AddError(
                "sequence.source.id.required",
                "Action Sequence YAML requires top-level 'id'.",
                sourcePath);
            return null;
        }

        ScenarioSourceSequenceDocument selected = null;
        if (scenario.Sequences != null)
        {
            for (int i = 0; i < scenario.Sequences.Count; i++)
            {
                ScenarioSourceSequenceDocument candidate = scenario.Sequences[i];
                if (candidate != null && string.Equals(Normalize(candidate.SequenceId), sequenceId, StringComparison.Ordinal))
                {
                    selected = candidate;
                    break;
                }
            }
        }

        if (selected == null)
        {
            validation.AddError(
                "sequence.source.actions.required",
                "Action Sequence YAML requires a matching sequence entry under 'sequences'.",
                sequenceId);
            return null;
        }

        return new ActionSequenceSourceDocument
        {
            SequenceId = sequenceId,
            DisplayNameKo = selected.DisplayNameKo ?? scenario.TitleKo ?? string.Empty,
            PrimaryMode = string.IsNullOrWhiteSpace(scenario.PrimaryMode) ? DefaultPrimaryMode : scenario.PrimaryMode.Trim(),
            Actions = CloneActions(selected.Actions)
        };
    }

    private static void ApplyDocument(
        ActionSequenceAsset target,
        ActionSequenceSourceDocument document,
        string sourceText,
        string sourcePath,
        DateTime importedAtUtc)
    {
        target.SequenceId = document.SequenceId;
        target.DisplayNameKo = document.DisplayNameKo;
        target.Actions = CloneActions(document.Actions);
        ApplySourceMetadata(target, sourceText, sourcePath, importedAtUtc);
    }

    private static void ApplySourceMetadata(
        ActionSequenceAsset sequence,
        string sourceText,
        string sourcePath,
        DateTime importedAtUtc)
    {
        if (sequence.Source == null)
        {
            sequence.Source = new ScenarioSourceMetadata();
        }

        sequence.Source.SourcePath = NormalizePath(sourcePath);
        sequence.Source.SourceHash = ScenarioSourceHash.Compute(sourceText ?? string.Empty);
        sequence.Source.ImportedAtIso8601 = importedAtUtc.ToUniversalTime().ToString("O");
    }

    private static void CopyMetadata(ScenarioSourceMetadata source, ScenarioSourceMetadata target)
    {
        if (target == null)
        {
            return;
        }

        target.SourcePath = source != null ? source.SourcePath ?? string.Empty : string.Empty;
        target.SourceHash = source != null ? source.SourceHash ?? string.Empty : string.Empty;
        target.ImportedAtIso8601 = source != null ? source.ImportedAtIso8601 ?? string.Empty : string.Empty;
    }

    private static List<ScenarioActionData> CloneActions(List<ScenarioActionData> source)
    {
        var copy = new List<ScenarioActionData>();
        if (source == null)
        {
            return copy;
        }

        for (int i = 0; i < source.Count; i++)
        {
            ScenarioActionData action = source[i];
            if (action == null)
            {
                copy.Add(null);
                continue;
            }

            copy.Add(ScenarioBlockIdentity.ClonePreservingIds(action));
        }

        return copy;
    }

    private static void DestroyTemporary(ActionSequenceAsset sequence)
    {
        if (sequence == null)
        {
            return;
        }

        if (Application.isPlaying)
        {
            UnityEngine.Object.Destroy(sequence);
            return;
        }

        UnityEngine.Object.DestroyImmediate(sequence);
    }

    private static string Normalize(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
    }

    private static string NormalizePath(string path)
    {
        return Normalize(path).Replace('\\', '/');
    }
}
