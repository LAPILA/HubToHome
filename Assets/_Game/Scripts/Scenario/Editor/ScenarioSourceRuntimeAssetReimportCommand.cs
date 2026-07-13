using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

public sealed class ScenarioSourceRuntimeAssetReimportCommand
{
    private const string UndoName = "시나리오 원본 YAML 런타임 반영";

    private readonly IScenarioSourceParser _parser;
    private readonly IScenarioDialogueReferenceResolver _dialogueResolver;
    private readonly IScenarioAudioReferenceResolver _audioResolver;

    public ScenarioSourceRuntimeAssetReimportCommand()
        : this(
            new ScenarioSourceYamlParser(),
            new AssetDatabaseScenarioDialogueReferenceResolver(),
            new AssetDatabaseScenarioDialogueReferenceResolver())
    {
    }

    public ScenarioSourceRuntimeAssetReimportCommand(
        IScenarioSourceParser parser,
        IScenarioDialogueReferenceResolver dialogueResolver = null,
        IScenarioAudioReferenceResolver audioResolver = null)
    {
        _parser = parser ?? new MissingYamlScenarioSourceParser();
        _dialogueResolver = dialogueResolver ?? new MissingScenarioDialogueReferenceResolver();
        _audioResolver = audioResolver ?? new MissingScenarioAudioReferenceResolver();
    }

    public ScenarioSourceRuntimeAssetReimportResult ReimportFromSourcePath(
        BattleScenarioData target,
        ActionCatalogAsset catalog = null,
        DateTime? importedAtUtc = null)
    {
        string sourcePath = target != null && target.Source != null
            ? target.Source.SourcePath
            : string.Empty;

        if (string.IsNullOrWhiteSpace(sourcePath))
        {
            var missingPath = new ScenarioSourceRuntimeAssetReimportResult();
            missingPath.Validation.AddError(
                "scenario.reimport.path.required",
                "Scenario Source YAML path is required before runtime asset reimport.",
                target != null ? target.ScenarioId : string.Empty);
            return missingPath;
        }

        try
        {
            if (!ScenarioSourcePathPolicy.TryNormalize(sourcePath, out string safePath, out string pathError))
                throw new InvalidOperationException(pathError);

            string sourceText = File.ReadAllText(ScenarioSourcePathPolicy.RequireAbsolute(safePath));
            return ReimportFromText(target, sourceText, safePath, catalog, importedAtUtc);
        }
        catch (Exception exception)
        {
            var result = new ScenarioSourceRuntimeAssetReimportResult();
            result.Validation.AddError(
                "scenario.reimport.read.failed",
                "Scenario Source YAML read failed: " + exception.Message,
                sourcePath);
            return result;
        }
    }

    public ScenarioSourceRuntimeAssetReimportResult ReimportFromText(
        BattleScenarioData target,
        string sourceText,
        string sourcePath,
        ActionCatalogAsset catalog = null,
        DateTime? importedAtUtc = null)
    {
        var result = new ScenarioSourceRuntimeAssetReimportResult
        {
            Target = target,
            SourcePath = string.IsNullOrWhiteSpace(sourcePath) ? string.Empty : sourcePath.Trim().Replace('\\', '/')
        };

        if (target == null)
        {
            result.Validation.AddError(
                "scenario.reimport.target.required",
                "BattleScenarioData target is required before runtime asset reimport.",
                string.Empty);
            return result;
        }

        var importer = new ScenarioSourceImporter(_parser, _dialogueResolver, _audioResolver);
        ScenarioSourceSyncResult importResult = importer.Import(sourceText, result.SourcePath, importedAtUtc);
        result.Validation.Merge(importResult.Validation);

        if (importResult.Scenario != null && catalog != null)
        {
            result.Validation.Merge(ScenarioCatalogValidator.ValidateBattleScenario(importResult.Scenario, catalog));
        }

        if (importResult.Scenario == null)
        {
            result.Validation.AddError(
                "scenario.reimport.import.empty",
                "Scenario Source YAML import did not produce a BattleScenarioData asset.",
                result.SourcePath);
        }

        if (result.Validation.HasErrors || importResult.Scenario == null)
        {
            DestroyTemporaryScenario(importResult.Scenario);
            return result;
        }

        ApplyImportedScenario(target, importResult.Scenario, result);
        DestroyTemporaryScenario(importResult.Scenario);
        return result;
    }

    private static void ApplyImportedScenario(
        BattleScenarioData target,
        BattleScenarioData imported,
        ScenarioSourceRuntimeAssetReimportResult result)
    {
        Undo.RecordObject(target, UndoName);

        target.ScenarioId = imported.ScenarioId;
        target.TitleKo = imported.TitleKo;
        target.PrimaryMode = imported.PrimaryMode;
        target.OpeningModule = imported.OpeningModule;
        target.MemoryKey = imported.MemoryKey;
        CopyMetadata(imported.Source, target.Source ?? (target.Source = new ScenarioSourceMetadata()));

        CopyStrings(imported.PartyIds, target.PartyIds);
        CopyStrings(imported.EnemyIds, target.EnemyIds);
        CopyRules(imported.Rules, target.Rules);
        CopyTriggerRules(imported.TriggerRules, target.TriggerRules);
        CopyDialogues(imported.Dialogues, target.Dialogues);
        CopyAudioClips(imported.AudioClips, target.AudioClips);
        ReplaceSequences(target, imported, result);

        EditorUtility.SetDirty(target);
    }

    private static void ReplaceSequences(
        BattleScenarioData target,
        BattleScenarioData imported,
        ScenarioSourceRuntimeAssetReimportResult result)
    {
        var existingById = new Dictionary<string, ActionSequenceAsset>();
        if (target.Sequences != null)
        {
            for (int i = 0; i < target.Sequences.Count; i++)
            {
                ActionSequenceAsset existing = target.Sequences[i];
                if (existing == null || string.IsNullOrWhiteSpace(existing.SequenceId))
                {
                    continue;
                }

                string key = existing.SequenceId.Trim();
                if (!existingById.ContainsKey(key))
                {
                    existingById.Add(key, existing);
                }
            }
        }

        var nextSequences = new List<ActionSequenceAsset>();
        if (imported.Sequences != null)
        {
            for (int i = 0; i < imported.Sequences.Count; i++)
            {
                ActionSequenceAsset importedSequence = imported.Sequences[i];
                if (importedSequence == null)
                {
                    continue;
                }

                string sequenceId = string.IsNullOrWhiteSpace(importedSequence.SequenceId)
                    ? "sequence_" + i
                    : importedSequence.SequenceId.Trim();
                ActionSequenceAsset targetSequence;
                if (existingById.TryGetValue(sequenceId, out targetSequence) && targetSequence != null)
                {
                    result.ReusedSequenceCount++;
                }
                else
                {
                    targetSequence = CreateSequenceAsset(target, sequenceId);
                    result.CreatedSequenceCount++;
                }

                // Avoid Undo serialization of recursive ScenarioActionData.Children.
                CopySequence(importedSequence, targetSequence);
                EditorUtility.SetDirty(targetSequence);
                nextSequences.Add(targetSequence);
            }
        }

        if (target.Sequences != null)
        {
            for (int i = 0; i < target.Sequences.Count; i++)
            {
                ActionSequenceAsset previous = target.Sequences[i];
                if (previous != null && !nextSequences.Contains(previous))
                {
                    result.DetachedSequenceCount++;
                }
            }

            target.Sequences.Clear();
        }
        else
        {
            target.Sequences = new List<ActionSequenceAsset>();
        }

        target.Sequences.AddRange(nextSequences);
    }

    private static ActionSequenceAsset CreateSequenceAsset(BattleScenarioData owner, string sequenceId)
    {
        ActionSequenceAsset sequence = ScriptableObject.CreateInstance<ActionSequenceAsset>();
        sequence.name = string.IsNullOrWhiteSpace(sequenceId) ? "ActionSequence" : sequenceId.Trim();

        string ownerPath = AssetDatabase.GetAssetPath(owner);
        if (!string.IsNullOrWhiteSpace(ownerPath))
        {
            AssetDatabase.AddObjectToAsset(sequence, owner);
        }

        return sequence;
    }

    private static void CopySequence(ActionSequenceAsset source, ActionSequenceAsset destination)
    {
        destination.SequenceId = source.SequenceId;
        destination.DisplayNameKo = source.DisplayNameKo;
        destination.Contract = ActionSequenceContractData.CopyOf(source.Contract);
        CopyMetadata(source.Source, destination.Source ?? (destination.Source = new ScenarioSourceMetadata()));
        destination.Actions = CloneActions(source.Actions);
    }

    private static void CopyMetadata(ScenarioSourceMetadata source, ScenarioSourceMetadata destination)
    {
        if (destination == null)
        {
            return;
        }

        if (source == null)
        {
            destination.SourcePath = string.Empty;
            destination.SourceHash = string.Empty;
            destination.ImportedAtIso8601 = string.Empty;
            return;
        }

        destination.SourcePath = source.SourcePath ?? string.Empty;
        destination.SourceHash = source.SourceHash ?? string.Empty;
        destination.ImportedAtIso8601 = source.ImportedAtIso8601 ?? string.Empty;
    }

    private static void CopyStrings(List<string> source, List<string> destination)
    {
        if (destination == null)
        {
            return;
        }

        destination.Clear();
        if (source == null)
        {
            return;
        }

        for (int i = 0; i < source.Count; i++)
        {
            destination.Add(source[i]);
        }
    }

    private static void CopyRules(List<BattleEventRuleData> source, List<BattleEventRuleData> destination)
    {
        if (destination == null)
        {
            return;
        }

        destination.Clear();
        if (source == null)
        {
            return;
        }

        for (int i = 0; i < source.Count; i++)
        {
            BattleEventRuleData rule = source[i];
            if (rule == null)
            {
                destination.Add(null);
                continue;
            }

            destination.Add(new BattleEventRuleData
            {
                RuleId = rule.RuleId,
                EventType = rule.EventType,
                Timing = rule.Timing,
                Once = rule.Once,
                SubjectId = rule.SubjectId,
                OutcomeId = rule.OutcomeId,
                ThresholdRatio = rule.ThresholdRatio,
                SequenceId = rule.SequenceId,
                Disabled = rule.Disabled
            });
        }
    }

    private static void CopyTriggerRules(
        List<ScenarioTriggerRuleData> source,
        List<ScenarioTriggerRuleData> destination)
    {
        if (destination == null)
        {
            return;
        }

        destination.Clear();
        if (source == null)
        {
            return;
        }

        for (int i = 0; i < source.Count; i++)
        {
            destination.Add(ScenarioTriggerIdentity.CloneRule(source[i]));
        }
    }

    private static void CopyDialogues(List<ScenarioDialogueReferenceData> source, List<ScenarioDialogueReferenceData> destination)
    {
        if (destination == null)
        {
            return;
        }

        destination.Clear();
        if (source == null)
        {
            return;
        }

        for (int i = 0; i < source.Count; i++)
        {
            ScenarioDialogueReferenceData dialogue = source[i];
            if (dialogue == null)
            {
                destination.Add(null);
                continue;
            }

            destination.Add(new ScenarioDialogueReferenceData
            {
                DialogueId = dialogue.DialogueId,
                DialogueDataId = dialogue.DialogueDataId,
                Dialogue = dialogue.Dialogue
            });
        }
    }

    private static void CopyAudioClips(List<ScenarioAudioReferenceData> source, List<ScenarioAudioReferenceData> destination)
    {
        if (destination == null)
        {
            return;
        }

        destination.Clear();
        if (source == null)
        {
            return;
        }

        for (int i = 0; i < source.Count; i++)
        {
            ScenarioAudioReferenceData audio = source[i];
            if (audio == null)
            {
                destination.Add(null);
                continue;
            }

            destination.Add(new ScenarioAudioReferenceData
            {
                AudioId = audio.AudioId,
                AudioClipId = audio.AudioClipId,
                Clip = audio.Clip
            });
        }
    }

    private static List<ScenarioActionData> CloneActions(List<ScenarioActionData> source)
    {
        var actions = new List<ScenarioActionData>();
        if (source == null)
        {
            return actions;
        }

        for (int i = 0; i < source.Count; i++)
        {
            actions.Add(CloneAction(source[i]));
        }

        return actions;
    }

    private static ScenarioActionData CloneAction(ScenarioActionData source)
    {
        return ScenarioBlockIdentity.ClonePreservingIds(source);
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
                if (scenario.Sequences[i] != null)
                {
                    UnityEngine.Object.DestroyImmediate(scenario.Sequences[i]);
                }
            }
        }

        UnityEngine.Object.DestroyImmediate(scenario);
    }
}

public sealed class ScenarioSourceRuntimeAssetReimportResult
{
    public BattleScenarioData Target;
    public string SourcePath = string.Empty;
    public int ReusedSequenceCount;
    public int CreatedSequenceCount;
    public int DetachedSequenceCount;
    public ScenarioValidationResult Validation = new ScenarioValidationResult();

    public bool Success
    {
        get { return Target != null && !Validation.HasErrors; }
    }
}
