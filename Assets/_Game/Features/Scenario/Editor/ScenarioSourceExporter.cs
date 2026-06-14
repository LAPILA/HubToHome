using System.Globalization;
using System.IO;
using System.Text;
using Newtonsoft.Json.Linq;

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

public sealed class ScenarioSourceYamlWriteResult
{
    public string Text = string.Empty;
    public ScenarioValidationResult Validation = new ScenarioValidationResult();

    public bool Success
    {
        get { return !Validation.HasErrors; }
    }
}

public sealed class ScenarioSourceYamlWriter
{
    private const int IndentSize = 2;

    public ScenarioSourceYamlWriteResult Write(ScenarioSourceDocument document)
    {
        var result = new ScenarioSourceYamlWriteResult();
        if (document == null)
        {
            result.Validation.AddError(
                "scenario.yaml.document.required",
                "Cannot write a null ScenarioSourceDocument.",
                string.Empty);
            return result;
        }

        var builder = new StringBuilder();
        WriteHeader(builder, document);
        WriteParticipants(builder, document);
        WriteDialogues(builder, document);
        WriteAudioClips(builder, document);
        WriteRules(builder, document);
        WriteSequences(builder, document, result.Validation);

        result.Text = builder.ToString();
        return result;
    }

    private static void WriteHeader(StringBuilder builder, ScenarioSourceDocument document)
    {
        AppendKeyValue(builder, 0, "id", document.Id);
        AppendKeyValue(builder, 0, "title", document.TitleKo);
        AppendKeyValue(builder, 0, "primaryMode", document.PrimaryMode);
        AppendKeyValue(builder, 0, "openingModule", document.OpeningModule);
        AppendKeyValue(builder, 0, "memoryKey", document.MemoryKey);
        builder.AppendLine();
    }

    private static void WriteParticipants(StringBuilder builder, ScenarioSourceDocument document)
    {
        builder.AppendLine("participants:");
        AppendInlineList(builder, 1, "party", document.PartyIds);
        AppendInlineList(builder, 1, "enemies", document.EnemyIds);
        builder.AppendLine();
    }

    private static void WriteDialogues(StringBuilder builder, ScenarioSourceDocument document)
    {
        if (document.Dialogues == null || document.Dialogues.Count == 0)
        {
            return;
        }

        builder.AppendLine("dialogues:");
        for (int i = 0; i < document.Dialogues.Count; i++)
        {
            ScenarioSourceDialogueDocument dialogue = document.Dialogues[i];
            if (dialogue == null)
            {
                continue;
            }

            AppendListItemKeyValue(builder, 1, "id", dialogue.DialogueId);
            AppendKeyValue(builder, 2, "dialogueData", dialogue.DialogueDataId);
        }

        builder.AppendLine();
    }

    private static void WriteAudioClips(StringBuilder builder, ScenarioSourceDocument document)
    {
        if (document.AudioClips == null || document.AudioClips.Count == 0)
        {
            return;
        }

        builder.AppendLine("audioClips:");
        for (int i = 0; i < document.AudioClips.Count; i++)
        {
            ScenarioSourceAudioDocument audio = document.AudioClips[i];
            if (audio == null)
            {
                continue;
            }

            AppendListItemKeyValue(builder, 1, "id", audio.AudioId);
            AppendKeyValue(builder, 2, "audioClip", audio.AudioClipId);
        }

        builder.AppendLine();
    }

    private static void WriteRules(StringBuilder builder, ScenarioSourceDocument document)
    {
        if (document.Rules == null || document.Rules.Count == 0)
        {
            return;
        }

        builder.AppendLine("rules:");
        for (int i = 0; i < document.Rules.Count; i++)
        {
            ScenarioSourceRuleDocument rule = document.Rules[i];
            if (rule == null)
            {
                continue;
            }

            AppendListItemKeyValue(builder, 1, "id", rule.RuleId);
            AppendKeyOnly(builder, 2, "when");
            AppendKeyValue(builder, 3, "event", FormatEventType(rule.EventType));
            AppendKeyValue(builder, 3, "enemy", rule.SubjectId);
            AppendKeyValue(builder, 3, "threshold", rule.ThresholdRatio);
            AppendKeyValue(builder, 3, "timing", FormatTiming(rule.Timing));
            AppendKeyValue(builder, 3, "once", FormatOnce(rule.Once));
            AppendKeyOnly(builder, 2, "do");
            AppendKeyValue(builder, 3, "sequence", rule.SequenceId);
            if (rule.Disabled)
            {
                AppendKeyValue(builder, 2, "disabled", true);
            }
        }

        builder.AppendLine();
    }

    private static void WriteSequences(
        StringBuilder builder,
        ScenarioSourceDocument document,
        ScenarioValidationResult validation)
    {
        if (document.Sequences == null || document.Sequences.Count == 0)
        {
            return;
        }

        builder.AppendLine("sequences:");
        for (int i = 0; i < document.Sequences.Count; i++)
        {
            ScenarioSourceSequenceDocument sequence = document.Sequences[i];
            if (sequence == null)
            {
                continue;
            }

            AppendKeyOnly(builder, 1, sequence.SequenceId);
            WriteActions(builder, sequence.Actions, 2, validation, sequence.SequenceId);
        }
    }

    private static void WriteActions(
        StringBuilder builder,
        System.Collections.Generic.List<ScenarioActionData> actions,
        int indentLevel,
        ScenarioValidationResult validation,
        string ownerId)
    {
        if (actions == null)
        {
            return;
        }

        for (int i = 0; i < actions.Count; i++)
        {
            ScenarioActionData action = actions[i];
            if (action == null)
            {
                continue;
            }

            string actionId = Normalize(action.ActionId);
            if (actionId == ActionDirector.ParallelActionId)
            {
                AppendListItemKeyOnly(builder, indentLevel, "parallel");
                WriteActions(builder, action.Children, indentLevel + 1, validation, ownerId);
                continue;
            }

            AppendListItemKeyOnly(builder, indentLevel, actionId);
            if (action.Disabled)
            {
                AppendKeyValue(builder, indentLevel + 1, "disabled", true);
            }

            JObject parameters = ParseParameters(action, validation, ownerId);
            if (parameters == null || parameters.Count == 0)
            {
                continue;
            }

            foreach (JProperty property in parameters.Properties())
            {
                WriteParameter(builder, indentLevel + 1, property.Name, property.Value);
            }
        }
    }

    private static JObject ParseParameters(
        ScenarioActionData action,
        ScenarioValidationResult validation,
        string ownerId)
    {
        if (action == null || string.IsNullOrWhiteSpace(action.ParametersJson))
        {
            return null;
        }

        try
        {
            return JObject.Parse(action.ParametersJson);
        }
        catch (System.Exception exception)
        {
            validation.AddError(
                "scenario.yaml.action.parameters.invalid",
                "Action parameters must be a JSON object before YAML export: " + exception.Message,
                ownerId);
            return null;
        }
    }

    private static void WriteParameter(StringBuilder builder, int indentLevel, string key, JToken value)
    {
        if (value == null || value.Type == JTokenType.Null)
        {
            AppendKeyValue(builder, indentLevel, key, string.Empty);
            return;
        }

        if (value.Type == JTokenType.Array && IsPrimitiveArray((JArray)value))
        {
            AppendRawKeyValue(builder, indentLevel, key, FormatInlineJsonArray((JArray)value));
            return;
        }

        if (value.Type == JTokenType.Object || value.Type == JTokenType.Array)
        {
            AppendRawKeyValue(builder, indentLevel, key, value.ToString(Newtonsoft.Json.Formatting.None));
            return;
        }

        if (value.Type == JTokenType.String)
        {
            AppendRawKeyValue(builder, indentLevel, key, FormatScalar(value));
            return;
        }

        AppendRawKeyValue(builder, indentLevel, key, FormatScalar(value));
    }

    private static bool IsPrimitiveArray(JArray array)
    {
        for (int i = 0; i < array.Count; i++)
        {
            JTokenType type = array[i].Type;
            if (type != JTokenType.String
                && type != JTokenType.Integer
                && type != JTokenType.Float
                && type != JTokenType.Boolean)
            {
                return false;
            }
        }

        return true;
    }

    private static string FormatInlineJsonArray(JArray array)
    {
        var items = new System.Collections.Generic.List<string>();
        for (int i = 0; i < array.Count; i++)
        {
            items.Add(FormatScalar(array[i]));
        }

        return "[" + string.Join(", ", items) + "]";
    }

    private static string FormatScalar(JToken token)
    {
        switch (token.Type)
        {
            case JTokenType.Integer:
            case JTokenType.Float:
                return System.Convert.ToString(((JValue)token).Value, CultureInfo.InvariantCulture);
            case JTokenType.Boolean:
                return token.Value<bool>() ? "true" : "false";
            default:
                return FormatYamlString(token.Value<string>() ?? string.Empty);
        }
    }

    private static void AppendKeyOnly(StringBuilder builder, int indentLevel, string key)
    {
        AppendIndent(builder, indentLevel);
        builder.Append(FormatYamlKey(key));
        builder.AppendLine(":");
    }

    private static void AppendListItemKeyOnly(StringBuilder builder, int indentLevel, string key)
    {
        AppendIndent(builder, indentLevel);
        builder.Append("- ");
        builder.Append(FormatYamlKey(key));
        builder.AppendLine(":");
    }

    private static void AppendKeyValue(StringBuilder builder, int indentLevel, string key, string value)
    {
        AppendIndent(builder, indentLevel);
        builder.Append(FormatYamlKey(key));
        builder.Append(": ");
        builder.AppendLine(FormatYamlString(value));
    }

    private static void AppendKeyValue(StringBuilder builder, int indentLevel, string key, float value)
    {
        AppendRawKeyValue(builder, indentLevel, key, value.ToString("0.###", CultureInfo.InvariantCulture));
    }

    private static void AppendKeyValue(StringBuilder builder, int indentLevel, string key, bool value)
    {
        AppendRawKeyValue(builder, indentLevel, key, value ? "true" : "false");
    }

    private static void AppendRawKeyValue(StringBuilder builder, int indentLevel, string key, string value)
    {
        AppendIndent(builder, indentLevel);
        builder.Append(FormatYamlKey(key));
        builder.Append(": ");
        builder.AppendLine(value ?? string.Empty);
    }

    private static void AppendListItemKeyValue(StringBuilder builder, int indentLevel, string key, string value)
    {
        AppendIndent(builder, indentLevel);
        builder.Append("- ");
        builder.Append(FormatYamlKey(key));
        builder.Append(": ");
        builder.AppendLine(FormatYamlString(value));
    }

    private static void AppendInlineList(
        StringBuilder builder,
        int indentLevel,
        string key,
        System.Collections.Generic.List<string> values)
    {
        var items = new System.Collections.Generic.List<string>();
        if (values != null)
        {
            for (int i = 0; i < values.Count; i++)
            {
                items.Add(FormatYamlString(values[i]));
            }
        }

        AppendIndent(builder, indentLevel);
        builder.Append(key);
        builder.Append(": [");
        builder.Append(string.Join(", ", items));
        builder.AppendLine("]");
    }

    private static void AppendIndent(StringBuilder builder, int indentLevel)
    {
        builder.Append(' ', indentLevel * IndentSize);
    }

    private static string FormatYamlKey(string key)
    {
        return string.IsNullOrWhiteSpace(key) ? "\"\"" : key.Trim();
    }

    private static string FormatYamlString(string value)
    {
        string normalized = value ?? string.Empty;
        if (ShouldQuote(normalized))
        {
            return "\"" + normalized.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
        }

        return normalized;
    }

    private static bool ShouldQuote(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return true;
        }

        if (value == "true" || value == "false" || value == "null")
        {
            return true;
        }

        for (int i = 0; i < value.Length; i++)
        {
            char c = value[i];
            if (char.IsWhiteSpace(c)
                || c == ':'
                || c == '#'
                || c == '['
                || c == ']'
                || c == '{'
                || c == '}'
                || c == ',')
            {
                return true;
            }
        }

        return false;
    }

    private static string FormatEventType(BattleEventType eventType)
    {
        switch (eventType)
        {
            case BattleEventType.EnemyHpCrossedBelow:
                return "enemy.hp_crossed_below";
            case BattleEventType.EnemyDefeated:
                return "enemy.defeated";
            case BattleEventType.SkillCompleted:
                return "skill.completed";
            case BattleEventType.GameModuleCompleted:
                return "game_module.completed";
            default:
                return "none";
        }
    }

    private static string FormatTiming(BattleRuleTiming timing)
    {
        switch (timing)
        {
            case BattleRuleTiming.AfterCurrentAction:
                return "after_current_action";
            case BattleRuleTiming.AfterCurrentSkill:
                return "after_current_skill";
            case BattleRuleTiming.AfterCurrentModule:
                return "after_current_module";
            default:
                return "immediate";
        }
    }

    private static string FormatOnce(BattleRuleOnceMode once)
    {
        switch (once)
        {
            case BattleRuleOnceMode.Always:
                return "always";
            case BattleRuleOnceMode.PerEncounterMemory:
                return "encounter";
            default:
                return "battle";
        }
    }

    private static string Normalize(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
    }
}

public interface IScenarioSourceTextFileWriter
{
    void WriteAllText(string path, string text);
}

public sealed class SystemScenarioSourceTextFileWriter : IScenarioSourceTextFileWriter
{
    public void WriteAllText(string path, string text)
    {
        string directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(path, text ?? string.Empty, Encoding.UTF8);
    }
}

public sealed class ScenarioSourceYamlExportCommand
{
    private readonly ScenarioSourceExporter _exporter;
    private readonly ScenarioSourceYamlWriter _writer;
    private readonly IScenarioSourceTextFileWriter _fileWriter;

    public ScenarioSourceYamlExportCommand()
        : this(
            new ScenarioSourceExporter(new AssetDatabaseScenarioDialogueReferenceResolver()),
            new ScenarioSourceYamlWriter(),
            new SystemScenarioSourceTextFileWriter())
    {
    }

    public ScenarioSourceYamlExportCommand(
        ScenarioSourceExporter exporter,
        ScenarioSourceYamlWriter writer,
        IScenarioSourceTextFileWriter fileWriter = null)
    {
        _exporter = exporter ?? new ScenarioSourceExporter();
        _writer = writer ?? new ScenarioSourceYamlWriter();
        _fileWriter = fileWriter ?? new SystemScenarioSourceTextFileWriter();
    }

    public ScenarioSourceYamlExportResult ExportToText(BattleScenarioData scenario)
    {
        var result = new ScenarioSourceYamlExportResult();
        if (scenario == null)
        {
            result.Validation.AddError(
                "scenario.yaml.export.scenario.required",
                "BattleScenarioData is required for YAML export.",
                string.Empty);
            return result;
        }

        ScenarioSourceExportResult exportResult = _exporter.Export(scenario);
        result.Validation.Merge(exportResult.Validation);
        result.Document = exportResult.Document;
        if (!exportResult.Success)
        {
            return result;
        }

        ScenarioSourceYamlWriteResult writeResult = _writer.Write(exportResult.Document);
        result.Validation.Merge(writeResult.Validation);
        result.Text = writeResult.Text;
        return result;
    }

    public ScenarioSourceYamlExportResult ExportToFile(BattleScenarioData scenario, string targetPath)
    {
        ScenarioSourceYamlExportResult result = ExportToText(scenario);
        result.TargetPath = NormalizePath(targetPath);
        if (!result.Success)
        {
            return result;
        }

        if (string.IsNullOrEmpty(result.TargetPath))
        {
            result.Validation.AddError(
                "scenario.yaml.export.path.required",
                "A target .scenario.yaml path is required.",
                scenario != null ? scenario.ScenarioId : string.Empty);
            return result;
        }

        try
        {
            _fileWriter.WriteAllText(result.TargetPath, result.Text);
        }
        catch (System.Exception exception)
        {
            result.Validation.AddError(
                "scenario.yaml.export.write.failed",
                "Scenario YAML export failed: " + exception.Message,
                result.TargetPath);
        }

        return result;
    }

    public ScenarioSourceYamlExportResult ExportToSourcePath(BattleScenarioData scenario)
    {
        string sourcePath = scenario != null && scenario.Source != null
            ? scenario.Source.SourcePath
            : string.Empty;
        return ExportToFile(scenario, sourcePath);
    }

    private static string NormalizePath(string path)
    {
        return string.IsNullOrWhiteSpace(path) ? string.Empty : path.Trim().Replace('\\', '/');
    }
}

public sealed class ScenarioSourceYamlExportResult
{
    public ScenarioSourceDocument Document;
    public string Text = string.Empty;
    public string TargetPath = string.Empty;
    public ScenarioValidationResult Validation = new ScenarioValidationResult();

    public bool Success
    {
        get { return !Validation.HasErrors && !string.IsNullOrEmpty(Text); }
    }
}
