public static class BattleScenarioActionContextFactory
{
    public static ActionExecutionContext Create(
        BattleScenarioData scenarioData,
        DialogueManager dialogueManager = null,
        ISkillTimelineRunner skillTimelineRunner = null,
        IGameModuleActionRunner gameModuleActionRunner = null,
        IAudioActionRunner audioActionRunner = null,
        IScreenTransitionRunner screenTransitionRunner = null,
        ITimelineCutsceneRunner timelineCutsceneRunner = null,
        IBattleCinematicRunner battleCinematicRunner = null,
        IBattleTweenCinematicService battleTweenCinematicService = null,
        IBattleSessionStateReader battleSessionState = null,
        IBattleParticipantCommandRunner battleParticipantCommandRunner = null,
        IGameModuleEventSink gameModuleEventSink = null)
    {
        var context = new ActionExecutionContext(new ActionExecutionHandle("battle_scenario"));
        context.ScenarioId = scenarioData != null ? scenarioData.ScenarioId : string.Empty;
        context.PrimaryMode = scenarioData != null ? scenarioData.PrimaryMode : "battle";
        context.ModuleId = ResolveModuleId(scenarioData, gameModuleActionRunner, battleSessionState);
        if (scenarioData != null && scenarioData.Sequences != null)
        {
            context.SetService<IActionSequenceResolver>(new ActionSequenceListResolver(scenarioData.Sequences));
        }

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

        if (timelineCutsceneRunner != null)
        {
            context.SetService<ITimelineCutsceneRunner>(timelineCutsceneRunner);
        }

        if (battleCinematicRunner != null)
        {
            context.SetService<IBattleCinematicRunner>(battleCinematicRunner);
        }

        if (battleTweenCinematicService != null)
        {
            context.SetService<IBattleTweenCinematicService>(battleTweenCinematicService);
        }

        if (battleSessionState != null)
        {
            context.SetService<IBattleSessionStateReader>(battleSessionState);
            IBattleSessionFlagStore flagStore = battleSessionState as IBattleSessionFlagStore;
            if (flagStore != null)
            {
                context.SetService<IBattleSessionFlagStore>(flagStore);
            }
        }

        if (battleParticipantCommandRunner != null)
        {
            context.SetService<IBattleParticipantCommandRunner>(battleParticipantCommandRunner);
        }

        if (gameModuleEventSink != null)
        {
            context.SetService<IGameModuleEventSink>(gameModuleEventSink);
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
