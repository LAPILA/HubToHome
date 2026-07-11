public static class BuiltInScenarioEventIds
{
    public const string BattleStarted = "battle.started";
    public const string BattleCheckpoint = "battle.checkpoint";
    public const string ParticipantHpChanged = "participant.hp_changed";
    public const string ParticipantDefeated = "participant.defeated";
    public const string SkillCompleted = "skill.completed";
    public const string ModuleCompleted = "module.completed";
}

public static class BuiltInTriggerConditionIds
{
    public const string ValueEquals = "value.equals";
    public const string NumberCompare = "number.compare";
    public const string NumberCrossedBelow = "number.crossed_below";
    public const string EventParticipant = "event.participant";
    public const string ModuleOutcome = "module.outcome";
    public const string EncounterMeetCount = "memory.meet_count";
    public const string FlagState = "flag.state";
}
