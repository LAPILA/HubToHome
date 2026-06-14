using System;
using UnityEngine;

public sealed class ScenarioSourceImporter
{
    private readonly IScenarioSourceParser _parser;
    private readonly IScenarioDialogueReferenceResolver _dialogueResolver;

    public ScenarioSourceImporter(
        IScenarioSourceParser parser,
        IScenarioDialogueReferenceResolver dialogueResolver = null)
    {
        _parser = parser ?? new MissingYamlScenarioSourceParser();
        _dialogueResolver = dialogueResolver ?? new MissingScenarioDialogueReferenceResolver();
    }

    public ScenarioSourceSyncResult Import(
        string sourceText,
        string sourcePath,
        DateTime? importedAtUtc = null)
    {
        ScenarioSourceParseResult parseResult = _parser.Parse(sourceText, sourcePath);
        var result = new ScenarioSourceSyncResult();
        result.Validation.Merge(parseResult.Validation);

        if (!parseResult.Success)
        {
            return result;
        }

        result.Scenario = CreateBattleScenario(
            parseResult.Document,
            sourceText,
            sourcePath,
            importedAtUtc,
            _dialogueResolver,
            result.Validation);
        return result;
    }

    public static BattleScenarioData CreateBattleScenario(
        ScenarioSourceDocument document,
        string sourceText,
        string sourcePath,
        DateTime? importedAtUtc = null,
        IScenarioDialogueReferenceResolver dialogueResolver = null,
        ScenarioValidationResult validation = null)
    {
        if (document == null)
        {
            return null;
        }

        ScenarioSourceMetadata metadata = CreateMetadata(sourceText, sourcePath, importedAtUtc);
        BattleScenarioData scenario = ScriptableObject.CreateInstance<BattleScenarioData>();
        scenario.ScenarioId = document.Id;
        scenario.TitleKo = document.TitleKo;
        scenario.PrimaryMode = document.PrimaryMode;
        scenario.OpeningModule = document.OpeningModule;
        scenario.MemoryKey = document.MemoryKey;
        scenario.Source = metadata;

        CopyStrings(document.PartyIds, scenario.PartyIds);
        CopyStrings(document.EnemyIds, scenario.EnemyIds);
        CopyDialogues(document, scenario, dialogueResolver, validation);
        CopyRules(document, scenario);
        CopySequences(document, scenario, metadata);

        return scenario;
    }

    private static ScenarioSourceMetadata CreateMetadata(
        string sourceText,
        string sourcePath,
        DateTime? importedAtUtc)
    {
        DateTime importedAt = importedAtUtc ?? DateTime.UtcNow;
        if (importedAt.Kind != DateTimeKind.Utc)
        {
            importedAt = importedAt.ToUniversalTime();
        }

        return new ScenarioSourceMetadata
        {
            SourcePath = sourcePath ?? string.Empty,
            SourceHash = ScenarioSourceHash.Compute(sourceText),
            ImportedAtIso8601 = importedAt.ToString("O")
        };
    }

    private static void CopyStrings(
        System.Collections.Generic.List<string> source,
        System.Collections.Generic.List<string> destination)
    {
        if (source == null || destination == null)
        {
            return;
        }

        destination.Clear();
        for (int i = 0; i < source.Count; i++)
        {
            destination.Add(source[i]);
        }
    }

    private static void CopyRules(ScenarioSourceDocument document, BattleScenarioData scenario)
    {
        scenario.Rules.Clear();
        if (document.Rules == null)
        {
            return;
        }

        for (int i = 0; i < document.Rules.Count; i++)
        {
            ScenarioSourceRuleDocument rule = document.Rules[i];
            if (rule == null)
            {
                continue;
            }

            scenario.Rules.Add(new BattleEventRuleData
            {
                RuleId = rule.RuleId,
                EventType = rule.EventType,
                Timing = rule.Timing,
                Once = rule.Once,
                SubjectId = rule.SubjectId,
                ThresholdRatio = rule.ThresholdRatio,
                SequenceId = rule.SequenceId,
                Disabled = rule.Disabled
            });
        }
    }

    private static void CopyDialogues(
        ScenarioSourceDocument document,
        BattleScenarioData scenario,
        IScenarioDialogueReferenceResolver dialogueResolver,
        ScenarioValidationResult validation)
    {
        scenario.Dialogues.Clear();
        if (document.Dialogues == null)
        {
            return;
        }

        IScenarioDialogueReferenceResolver resolver = dialogueResolver ?? new MissingScenarioDialogueReferenceResolver();
        for (int i = 0; i < document.Dialogues.Count; i++)
        {
            ScenarioSourceDialogueDocument sourceDialogue = document.Dialogues[i];
            if (sourceDialogue == null)
            {
                continue;
            }

            string dialogueId = NormalizeId(sourceDialogue.DialogueId);
            string dialogueDataId = NormalizeId(sourceDialogue.DialogueDataId);
            if (string.IsNullOrEmpty(dialogueId))
            {
                validation?.AddError(
                    "scenario.dialogue.id.required",
                    "Scenario dialogue mapping requires a dialogue id.",
                    dialogueDataId);
                continue;
            }

            if (string.IsNullOrEmpty(dialogueDataId))
            {
                validation?.AddError(
                    "scenario.dialogue.reference.required",
                    "Scenario dialogue mapping requires a DialogueData id.",
                    dialogueId);
                continue;
            }

            if (!resolver.TryResolveDialogue(dialogueDataId, out DialogueData dialogue) || dialogue == null)
            {
                validation?.AddError(
                    "scenario.dialogue.unresolved",
                    $"DialogueData id '{dialogueDataId}' could not be resolved for dialogue '{dialogueId}'.",
                    dialogueId);
                continue;
            }

            scenario.Dialogues.Add(new ScenarioDialogueReferenceData
            {
                DialogueId = dialogueId,
                DialogueDataId = dialogueDataId,
                Dialogue = dialogue
            });
        }
    }

    private static void CopySequences(
        ScenarioSourceDocument document,
        BattleScenarioData scenario,
        ScenarioSourceMetadata metadata)
    {
        scenario.Sequences.Clear();
        if (document.Sequences == null)
        {
            return;
        }

        for (int i = 0; i < document.Sequences.Count; i++)
        {
            ScenarioSourceSequenceDocument sourceSequence = document.Sequences[i];
            if (sourceSequence == null)
            {
                continue;
            }

            ActionSequenceAsset sequence = ScriptableObject.CreateInstance<ActionSequenceAsset>();
            sequence.SequenceId = sourceSequence.SequenceId;
            sequence.DisplayNameKo = sourceSequence.DisplayNameKo;
            sequence.Source = metadata;
            sequence.Actions = CloneActions(sourceSequence.Actions);
            scenario.Sequences.Add(sequence);
        }
    }

    private static System.Collections.Generic.List<ScenarioActionData> CloneActions(
        System.Collections.Generic.List<ScenarioActionData> source)
    {
        var actions = new System.Collections.Generic.List<ScenarioActionData>();
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
        if (source == null)
        {
            return null;
        }

        return new ScenarioActionData
        {
            ActionId = source.ActionId,
            ParametersJson = source.ParametersJson,
            Disabled = source.Disabled,
            Children = CloneActions(source.Children)
        };
    }

    private static string NormalizeId(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
    }
}

public sealed class ScenarioSourceSyncResult
{
    public BattleScenarioData Scenario;
    public ScenarioValidationResult Validation = new ScenarioValidationResult();

    public bool Success
    {
        get { return Scenario != null && !Validation.HasErrors; }
    }
}
