using System;
using System.Collections.Generic;

public enum BattleEncounterOutcome
{
    Unknown = 0,
    Victory = 1,
    Escaped = 2,
    PartyDefeated = 3
}

[Serializable]
public sealed class EncounterMemorySaveData
{
    public string EncounterId = string.Empty;
    public int MeetCount = 0;
    public bool Defeated = false;
    public BattleEncounterOutcome LastOutcome = BattleEncounterOutcome.Unknown;
    public int VictoryCount = 0;
    public int EscapeCount = 0;
    public int PartyDefeatCount = 0;
    public List<string> SeenBeatIds = new List<string>();
}
