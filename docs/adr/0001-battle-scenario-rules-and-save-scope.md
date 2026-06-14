# Battle scenario rules own battle events

HubToHome battle phase changes, encounter-specific dialogue, module switches, and victory return behavior are authored in Encounter Definition / Battle Scenario Data rather than Skill Data, Enemy Data, or a specific Game Module. Battle Event Rules provide the When side, Action Sequences provide the Do side, Game Modules share Battle Session State, and the Save Scope remains outside battle: Encounter Memory and battle results may persist, but in-progress Battle Session State is not restored from a save.
