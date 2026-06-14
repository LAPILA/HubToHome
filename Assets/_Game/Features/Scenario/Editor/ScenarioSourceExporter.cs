public sealed class ScenarioSourceExporter
{
    private readonly IScenarioDialogueReferenceIdProvider _dialogueIdProvider;
    private readonly IScenarioAudioReferenceIdProvider _audioIdProvider;

    public ScenarioSourceExporter(
        IScenarioDialogueReferenceIdProvider dialogueIdProvider = null,
        IScenarioAudioReferenceIdProvider audioIdProvider = null)
    {
        _dialogueIdProvider = dialogueIdProvider ?? new MissingScenarioDialogueReferenceIdProvider();
        _audioIdProvider = audioIdProvider ?? new MissingScenarioAudioReferenceIdProvider();
    }

    public ScenarioSourceExportResult Export(BattleScenarioData scenario)
    {
        var result = new ScenarioSourceExportResult();
        result.Document = CreateDocument(scenario, _dialogueIdProvider, _audioIdProvider, result.Validation);
        return result;
    }

    public static ScenarioSourceDocument CreateDocument(
        BattleScenarioData scenario,
        IScenarioDialogueReferenceIdProvider dialogueIdProvider = null,
        IScenarioAudioReferenceIdProvider audioIdProvider = null,
        ScenarioValidationResult validation = null)
    {
        if (scenario == null)
        {
            validation?.AddError(
                "scenario.export.scenario.required",
                "Cannot export a null battle scenario.",
                string.Empty);
            return null;
        }

        var document = new ScenarioSourceDocument
        {
            Id = scenario.ScenarioId,
            TitleKo = scenario.TitleKo,
            PrimaryMode = scenario.PrimaryMode,
            OpeningModule = scenario.OpeningModule,
            MemoryKey = scenario.MemoryKey
        };

        CopyStrings(scenario.PartyIds, document.PartyIds);
        CopyStrings(scenario.EnemyIds, document.EnemyIds);
        CopyDialogues(scenario, document, dialogueIdProvider, validation);
        CopyAudioClips(scenario, document, audioIdProvider, validation);
        CopyRules(scenario, document);
        CopySequences(scenario, document);

        return document;
    }

    private static void CopyStrings(
        System.Collections.Generic.List<string> source,
        System.Collections.Generic.List<string> destination)
    {
        if (source == null || destination == null)
        {
            return;
        }

        for (int i = 0; i < source.Count; i++)
        {
            destination.Add(source[i]);
        }
    }

    private static void CopyDialogues(
        BattleScenarioData scenario,
        ScenarioSourceDocument document,
        IScenarioDialogueReferenceIdProvider dialogueIdProvider,
        ScenarioValidationResult validation)
    {
        if (scenario.Dialogues == null)
        {
            return;
        }

        IScenarioDialogueReferenceIdProvider provider =
            dialogueIdProvider ?? new MissingScenarioDialogueReferenceIdProvider();

        for (int i = 0; i < scenario.Dialogues.Count; i++)
        {
            ScenarioDialogueReferenceData reference = scenario.Dialogues[i];
            if (reference == null)
            {
                continue;
            }

            string dialogueId = NormalizeId(reference.DialogueId);
            if (string.IsNullOrEmpty(dialogueId))
            {
                validation?.AddError(
                    "scenario.dialogue.id.required",
                    "Scenario dialogue mapping requires a dialogue id.",
                    string.Empty);
                continue;
            }

            string dialogueDataId = NormalizeId(reference.DialogueDataId);
            if (string.IsNullOrEmpty(dialogueDataId) &&
                reference.Dialogue != null &&
                provider.TryGetDialogueDataId(reference.Dialogue, out string resolvedDialogueDataId))
            {
                dialogueDataId = NormalizeId(resolvedDialogueDataId);
            }

            if (string.IsNullOrEmpty(dialogueDataId))
            {
                validation?.AddError(
                    "scenario.dialogue.reference.required",
                    "Scenario dialogue mapping requires a DialogueData id.",
                    dialogueId);
                continue;
            }

            document.Dialogues.Add(new ScenarioSourceDialogueDocument
            {
                DialogueId = dialogueId,
                DialogueDataId = dialogueDataId
            });
        }
    }

    private static void CopyAudioClips(
        BattleScenarioData scenario,
        ScenarioSourceDocument document,
        IScenarioAudioReferenceIdProvider audioIdProvider,
        ScenarioValidationResult validation)
    {
        if (scenario.AudioClips == null)
        {
            return;
        }

        IScenarioAudioReferenceIdProvider provider =
            audioIdProvider ?? new MissingScenarioAudioReferenceIdProvider();

        for (int i = 0; i < scenario.AudioClips.Count; i++)
        {
            ScenarioAudioReferenceData reference = scenario.AudioClips[i];
            if (reference == null)
            {
                continue;
            }

            string audioId = NormalizeId(reference.AudioId);
            if (string.IsNullOrEmpty(audioId))
            {
                validation?.AddError(
                    "scenario.audio.id.required",
                    "Scenario audio mapping requires an audio id.",
                    string.Empty);
                continue;
            }

            string audioClipId = NormalizeId(reference.AudioClipId);
            if (string.IsNullOrEmpty(audioClipId) &&
                reference.Clip != null &&
                provider.TryGetAudioClipId(reference.Clip, out string resolvedAudioClipId))
            {
                audioClipId = NormalizeId(resolvedAudioClipId);
            }

            if (string.IsNullOrEmpty(audioClipId))
            {
                validation?.AddError(
                    "scenario.audio.reference.required",
                    "Scenario audio mapping requires an AudioClip id.",
                    audioId);
                continue;
            }

            document.AudioClips.Add(new ScenarioSourceAudioDocument
            {
                AudioId = audioId,
                AudioClipId = audioClipId
            });
        }
    }

    private static void CopyRules(BattleScenarioData scenario, ScenarioSourceDocument document)
    {
        if (scenario.Rules == null)
        {
            return;
        }

        for (int i = 0; i < scenario.Rules.Count; i++)
        {
            BattleEventRuleData rule = scenario.Rules[i];
            if (rule == null)
            {
                continue;
            }

            document.Rules.Add(new ScenarioSourceRuleDocument
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

    private static void CopySequences(BattleScenarioData scenario, ScenarioSourceDocument document)
    {
        if (scenario.Sequences == null)
        {
            return;
        }

        for (int i = 0; i < scenario.Sequences.Count; i++)
        {
            ActionSequenceAsset sequence = scenario.Sequences[i];
            if (sequence == null)
            {
                continue;
            }

            document.Sequences.Add(new ScenarioSourceSequenceDocument
            {
                SequenceId = sequence.SequenceId,
                DisplayNameKo = sequence.DisplayNameKo,
                Actions = CloneActions(sequence.Actions)
            });
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

public sealed class ScenarioSourceExportResult
{
    public ScenarioSourceDocument Document;
    public ScenarioValidationResult Validation = new ScenarioValidationResult();

    public bool Success
    {
        get { return Document != null && !Validation.HasErrors; }
    }
}
