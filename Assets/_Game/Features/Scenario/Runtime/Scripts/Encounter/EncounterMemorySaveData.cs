using System;
using System.Collections.Generic;

[Serializable]
public sealed class EncounterMemorySaveData
{
    public string EncounterId = string.Empty;
    public int MeetCount = 0;
    public bool Defeated = false;
    public List<string> SeenBeatIds = new List<string>();
}
