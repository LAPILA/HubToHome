public static class BattleScenarioActionRegistryFactory
{
    public static ActionAdapterRegistry CreateRegistry()
    {
        var registry = new ActionAdapterRegistry();
        registry.Register(new FlowWaitActionAdapter());
        registry.Register(new DialogueWaitActionAdapter());
        registry.Register(new BgmCrossfadeActionAdapter());
        registry.Register(new ScreenFadeActionAdapter());
        registry.Register(new CinematicLetterboxActionAdapter());
        registry.Register(new BattleCameraFocusActionAdapter());
        registry.Register(new BattleCameraResetActionAdapter());
        registry.Register(new BattleActorPoseActionAdapter());
        registry.Register(new BattleActorFlipActionAdapter());
        registry.Register(new BattleActorMoveActionAdapter());
        registry.Register(new BattleActorDropInActionAdapter());
        registry.Register(new BattleActorFakeAttackActionAdapter());
        registry.Register(new BattleActorReturnSlotsActionAdapter());
        registry.Register(new ModuleSwitchActionAdapter());
        registry.Register(new ModuleStartActionAdapter());
        registry.Register(new BattleSkillTimelineActionAdapter());
        registry.Register(new BattleParticipantDamageActionAdapter());
        registry.Register(new BattleParticipantHealHpActionAdapter());
        registry.Register(new BattleParticipantHealMpActionAdapter());
        registry.Register(new BattleParticipantConsumeMpActionAdapter());
        registry.Register(new BattleFlagSetActionAdapter());
        registry.Register(new BattleFlagClearActionAdapter());
        registry.Register(new TimelinePlayActionAdapter());
        registry.Register(new SequenceCallActionAdapter(registry));
        return registry;
    }

    public static ActionDirector CreateDirector()
    {
        return new ActionDirector(CreateRegistry());
    }
}
