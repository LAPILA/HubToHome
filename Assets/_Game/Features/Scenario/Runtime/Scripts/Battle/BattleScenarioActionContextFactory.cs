public static class BattleScenarioActionContextFactory
{
    public static ActionExecutionContext Create(
        BattleScenarioData scenarioData,
        DialogueManager dialogueManager = null)
    {
        var context = new ActionExecutionContext(new ActionExecutionHandle("battle_scenario"));
        context.ScenarioId = scenarioData != null ? scenarioData.ScenarioId : string.Empty;
        context.PrimaryMode = scenarioData != null ? scenarioData.PrimaryMode : "battle";
        context.ModuleId = scenarioData != null ? scenarioData.OpeningModule : string.Empty;

        var dialogueRunner = new DialogueManagerRunner(dialogueManager);
        if (scenarioData != null)
        {
            new ScenarioDialogueRegistry(scenarioData.Dialogues).RegisterInto(dialogueRunner);
        }

        context.SetService<IDialogueRunner>(dialogueRunner);
        return context;
    }
}
