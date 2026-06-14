public static class BattleScenarioActionContextFactory
{
    public static ActionExecutionContext Create(
        BattleScenarioData scenarioData,
        DialogueManager dialogueManager = null,
        ISkillTimelineRunner skillTimelineRunner = null,
        IGameModuleActionRunner gameModuleActionRunner = null,
        IAudioActionRunner audioActionRunner = null,
        IScreenTransitionRunner screenTransitionRunner = null,
        IBattleSessionStateReader battleSessionState = null)
    {
        var context = new ActionExecutionContext(new ActionExecutionHandle("battle_scenario"));
        context.ScenarioId = scenarioData != null ? scenarioData.ScenarioId : string.Empty;
        context.PrimaryMode = scenarioData != null ? scenarioData.PrimaryMode : "battle";
        context.ModuleId = ResolveModuleId(scenarioData, gameModuleActionRunner, battleSessionState);

        var dialogueRunner = new DialogueManagerRunner(dialogueManager);
        if (scenarioData != null)
        {
            new ScenarioDialogueRegistry(scenarioData.Dialogues).RegisterInto(dialogueRunner);
        }

        context.SetService<IDialogueRunner>(dialogueRunner);
        if (skillTimelineRunner != null)
        {
            context.SetService<ISkillTimelineRunner>(skillTimelineRunner);
        }

        if (gameModuleActionRunner != null)
        {
            context.SetService<IGameModuleActionRunner>(gameModuleActionRunner);
        }

        if (audioActionRunner != null)
        {
            context.SetService<IAudioActionRunner>(audioActionRunner);
        }

        if (screenTransitionRunner != null)
        {
            context.SetService<IScreenTransitionRunner>(screenTransitionRunner);
        }

        if (battleSessionState != null)
        {
            context.SetService<IBattleSessionStateReader>(battleSessionState);
        }

        return context;
    }

    private static string ResolveModuleId(
        BattleScenarioData scenarioData,
        IGameModuleActionRunner gameModuleActionRunner,
        IBattleSessionStateReader battleSessionState)
    {
        if (gameModuleActionRunner != null && !string.IsNullOrWhiteSpace(gameModuleActionRunner.CurrentModuleId))
        {
            return gameModuleActionRunner.CurrentModuleId.Trim();
        }

        if (battleSessionState != null && !string.IsNullOrWhiteSpace(battleSessionState.CurrentModuleId))
        {
            return battleSessionState.CurrentModuleId.Trim();
        }

        return scenarioData != null ? scenarioData.OpeningModule : string.Empty;
    }
}
