#if UNITY_EDITOR
using System;
using System.Collections.Generic;

internal static class ScenarioContentRules
{
    public static void Validate(ContentValidationRuleContext context)
    {
        ProjectContentSnapshot snapshot = context.Snapshot;
        HashSet<string> characterIds = BuildCharacterIds(snapshot.Characters);
        HashSet<string> enemyIds = BuildEnemyIds(snapshot.Enemies);

        for (int i = 0; i < snapshot.Scenarios.Count; i++)
        {
            BattleScenarioData scenario = snapshot.Scenarios[i];
            if (scenario == null)
                continue;

            ValidateParticipantIds(
                scenario.PartyIds,
                characterIds,
                true,
                scenario,
                "scenario.party_id",
                "PartyIds",
                context);
            ValidateParticipantIds(
                scenario.EnemyIds,
                enemyIds,
                false,
                scenario,
                "scenario.enemy_id",
                "EnemyIds",
                context);
            ValidateSequenceReferences(scenario, context);
            ValidateDialogueReferences(scenario, context);
            ValidateAudioReferences(scenario, context);
            ValidateScenarioContract(scenario, context);
        }
    }

    private static HashSet<string> BuildCharacterIds(IReadOnlyList<CharacterData> characters)
    {
        var ids = new HashSet<string>(StringComparer.Ordinal);
        for (int i = 0; i < characters.Count; i++)
        {
            CharacterData character = characters[i];
            if (character != null && !string.IsNullOrWhiteSpace(character.CharacterID))
                ids.Add(character.CharacterID.Trim());
        }

        return ids;
    }

    private static HashSet<string> BuildEnemyIds(IReadOnlyList<EnemyData> enemies)
    {
        var ids = new HashSet<string>(StringComparer.Ordinal);
        for (int i = 0; i < enemies.Count; i++)
        {
            EnemyData enemy = enemies[i];
            if (enemy != null && !string.IsNullOrWhiteSpace(enemy.EnemyId))
                ids.Add(enemy.EnemyId.Trim());
        }

        return ids;
    }

    private static void ValidateScenarioContract(
        BattleScenarioData scenario,
        ContentValidationRuleContext context)
    {
        IReadOnlyList<ActionCatalogAsset> catalogs = context.Snapshot.ActionCatalogs;
        if (catalogs.Count == 0)
            return;

        if (catalogs.Count > 1)
        {
            context.Add(
                scenario,
                "scenario.action_catalog.ambiguous",
                "More than one Action Catalog was found; scenario contract validation was skipped.",
                ContentValidationSeverity.Warning);
            return;
        }

        ScenarioValidationResult scenarioResult;
        try
        {
            scenarioResult = ScenarioCatalogValidator.ValidateBattleScenario(scenario, catalogs[0]);
        }
        catch (Exception exception)
        {
            context.Add(
                scenario,
                "scenario.contract.exception",
                "Scenario contract validation failed: " + exception.Message);
            return;
        }

        for (int i = 0; i < scenarioResult.Messages.Count; i++)
        {
            ScenarioValidationMessage message = scenarioResult.Messages[i];
            if (message == null || message.Severity == ScenarioValidationSeverity.Info)
                continue;

            string code = string.IsNullOrWhiteSpace(message.Code)
                ? "unknown"
                : message.Code.Trim();
            string detail = string.IsNullOrWhiteSpace(message.ObjectId)
                ? message.Message
                : message.Message + " [" + message.ObjectId + "]";
            context.Add(
                scenario,
                "scenario.contract." + code,
                detail,
                message.Severity == ScenarioValidationSeverity.Error
                    ? ContentValidationSeverity.Error
                    : ContentValidationSeverity.Warning);
        }
    }

    private static void ValidateSequenceReferences(
        BattleScenarioData scenario,
        ContentValidationRuleContext context)
    {
        if (scenario.Sequences == null)
        {
            context.Add(scenario, "scenario.sequence.list_missing", "Sequences list is missing.");
            return;
        }

        var seenIds = new HashSet<string>(StringComparer.Ordinal);
        for (int i = 0; i < scenario.Sequences.Count; i++)
        {
            ActionSequenceAsset sequence = scenario.Sequences[i];
            if (sequence == null)
            {
                context.Add(scenario, "scenario.sequence.missing", "Sequences[" + i + "] is missing.");
                continue;
            }

            string id = sequence.SequenceId?.Trim() ?? string.Empty;
            if (string.IsNullOrEmpty(id))
            {
                context.Add(scenario, "scenario.sequence.id.missing", "Sequences[" + i + "] ID is missing.");
            }
            else if (!seenIds.Add(id))
            {
                context.Add(
                    scenario,
                    "scenario.sequence.id.duplicate",
                    "Sequences contains duplicate ID '" + id + "'.");
            }
        }
    }

    private static void ValidateDialogueReferences(
        BattleScenarioData scenario,
        ContentValidationRuleContext context)
    {
        if (scenario.Dialogues == null)
            return;

        var seenIds = new HashSet<string>(StringComparer.Ordinal);
        for (int i = 0; i < scenario.Dialogues.Count; i++)
        {
            ScenarioDialogueReferenceData reference = scenario.Dialogues[i];
            if (reference == null)
            {
                context.Add(
                    scenario,
                    "scenario.dialogue.reference.missing",
                    "Dialogues[" + i + "] is missing.");
                continue;
            }

            string id = reference.DialogueId?.Trim() ?? string.Empty;
            if (string.IsNullOrEmpty(id))
            {
                context.Add(scenario, "scenario.dialogue.id.missing", "Dialogues[" + i + "] ID is missing.");
            }
            else if (!seenIds.Add(id))
            {
                context.Add(
                    scenario,
                    "scenario.dialogue.id.duplicate",
                    "Dialogues contains duplicate ID '" + id + "'.");
            }

            if (reference.Dialogue == null)
            {
                context.Add(
                    scenario,
                    "scenario.dialogue.asset.missing",
                    "Dialogues[" + i + "] DialogueData is missing.");
            }
        }
    }

    private static void ValidateAudioReferences(
        BattleScenarioData scenario,
        ContentValidationRuleContext context)
    {
        if (scenario.AudioClips == null)
            return;

        var seenIds = new HashSet<string>(StringComparer.Ordinal);
        for (int i = 0; i < scenario.AudioClips.Count; i++)
        {
            ScenarioAudioReferenceData reference = scenario.AudioClips[i];
            if (reference == null)
            {
                context.Add(
                    scenario,
                    "scenario.audio.reference.missing",
                    "AudioClips[" + i + "] is missing.");
                continue;
            }

            string id = reference.AudioId?.Trim() ?? string.Empty;
            if (string.IsNullOrEmpty(id))
            {
                context.Add(scenario, "scenario.audio.id.missing", "AudioClips[" + i + "] ID is missing.");
            }
            else if (!seenIds.Add(id))
            {
                context.Add(
                    scenario,
                    "scenario.audio.id.duplicate",
                    "AudioClips contains duplicate ID '" + id + "'.");
            }

            if (reference.Clip == null)
            {
                context.Add(
                    scenario,
                    "scenario.audio.asset.missing",
                    "AudioClips[" + i + "] AudioClip is missing.");
            }
        }
    }

    private static void ValidateParticipantIds(
        IReadOnlyList<string> ids,
        HashSet<string> knownIds,
        bool allowCanonicalPlayer,
        BattleScenarioData scenario,
        string codePrefix,
        string fieldName,
        ContentValidationRuleContext context)
    {
        if (ids == null)
        {
            context.Add(scenario, codePrefix + ".list_missing", fieldName + " list is missing.");
            return;
        }

        var seen = new HashSet<string>(StringComparer.Ordinal);
        for (int i = 0; i < ids.Count; i++)
        {
            string rawId = ids[i];
            if (string.IsNullOrWhiteSpace(rawId))
            {
                context.Add(scenario, codePrefix + ".missing", fieldName + "[" + i + "] is missing.");
                continue;
            }

            string id = rawId.Trim();
            if (!seen.Add(id))
            {
                context.Add(
                    scenario,
                    codePrefix + ".duplicate",
                    fieldName + " contains duplicate ID '" + id + "'.");
                continue;
            }

            if (allowCanonicalPlayer && id == "player")
                continue;

            if (!knownIds.Contains(id))
            {
                context.Add(
                    scenario,
                    codePrefix + ".unknown",
                    fieldName + " references unknown ID '" + id + "'.");
            }
        }
    }
}
#endif
