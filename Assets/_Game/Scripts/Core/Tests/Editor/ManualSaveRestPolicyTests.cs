using System.Collections.Generic;
using NUnit.Framework;

public sealed class ManualSaveRestPolicyTests
{
    [Test]
    public void RestedSnapshotRestoresHpApIncludingFallenMembers()
    {
        var snapshot = new SaveData
        {
            PartyData = new List<CharacterSaveData>
            {
                new CharacterSaveData { HP = 0, MaxHP = 240, AP = 2, MaxAP = 80 },
                new CharacterSaveData { HP = 35, MaxHP = 150, AP = 0, MaxAP = 0 },
                null
            }
        };
        ManualSaveRestPolicy.PrepareSnapshot(snapshot);
        Assert.That(snapshot.PartyData[0].HP, Is.EqualTo(240));
        Assert.That(snapshot.PartyData[0].AP, Is.EqualTo(80));
        Assert.That(snapshot.PartyData[1].HP, Is.EqualTo(150));
        Assert.That(snapshot.PartyData[1].AP, Is.Zero);
    }

    [Test]
    public void OnlyRespawnableEnemiesResetWhileMemoryFlagsAndInventoryRemain()
    {
        var snapshot = new SaveData();
        snapshot.OverworldEnemies["ordinary"] = new OverworldEnemySaveData
            { IsDefeated = true, RespawnOnManualSave = true };
        snapshot.OverworldEnemies["boss"] = new OverworldEnemySaveData
            { IsDefeated = true, RespawnOnManualSave = false };
        snapshot.eventFlags["chest.open"] = 1;
        snapshot.InventoryDict["potion"] = 7;
        snapshot.EncounterMemory["ordinary"] = new EncounterMemorySaveData { Defeated = true };

        ManualSaveRestPolicy.PrepareSnapshot(snapshot);

        Assert.That(snapshot.OverworldEnemies["ordinary"].IsDefeated, Is.False);
        Assert.That(snapshot.OverworldEnemies["boss"].IsDefeated, Is.True);
        Assert.That(snapshot.EncounterMemory["ordinary"].Defeated, Is.True);
        Assert.That(snapshot.eventFlags["chest.open"], Is.EqualTo(1));
        Assert.That(snapshot.InventoryDict["potion"], Is.EqualTo(7));
    }

    [Test]
    public void PreparingSeparateSaveCopyDoesNotHealOriginalOrResetItsEnemy()
    {
        var original = new SaveData();
        original.PartyData.Add(new CharacterSaveData { HP = 5, MaxHP = 100, AP = 2, MaxAP = 20 });
        original.OverworldEnemies["ordinary"] = new OverworldEnemySaveData
            { IsDefeated = true, RespawnOnManualSave = true };
        var codec = new SaveDataCodec();
        SaveData copy = codec.Decode(codec.Encode(original)).Data;

        ManualSaveRestPolicy.PrepareSnapshot(copy);

        Assert.That(original.PartyData[0].HP, Is.EqualTo(5));
        Assert.That(original.PartyData[0].AP, Is.EqualTo(2));
        Assert.That(original.OverworldEnemies["ordinary"].IsDefeated, Is.True);
        Assert.That(copy.PartyData[0].HP, Is.EqualTo(100));
        Assert.That(copy.OverworldEnemies["ordinary"].IsDefeated, Is.False);
    }
}
