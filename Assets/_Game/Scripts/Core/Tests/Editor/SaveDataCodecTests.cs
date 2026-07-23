using System.Collections.Generic;
using NUnit.Framework;

public class SaveDataCodecTests
{
    [Test]
    public void Decode_LegacyJsonWithoutVersion_MigratesToCurrentVersion()
    {
        const string json =
            "{\"currentScene\":\"TestMap\",\"InventoryDict\":{\"potion\":2}}";

        SaveDecodeResult result = new SaveDataCodec().Decode(json);

        Assert.That(result.Success, Is.True);
        Assert.That(result.SourceVersion, Is.EqualTo(SaveSchema.LegacyVersion));
        Assert.That(result.WasMigrated, Is.True);
        Assert.That(result.Data.schemaVersion, Is.EqualTo(SaveSchema.CurrentVersion));
        Assert.That(result.Data.currentScene, Is.EqualTo("TestMap"));
        Assert.That(result.Data.InventoryDict["potion"], Is.EqualTo(2));
    }

    [Test]
    public void Decode_MissingCollections_NormalizesDefaults()
    {
        const string json =
            "{\"currentScene\":\"TestMap\",\"PartyData\":null,\"InventoryDict\":null,"
            + "\"eventFlags\":null,\"EncounterMemory\":null,\"OverworldEnemies\":null}";

        SaveDecodeResult result = new SaveDataCodec().Decode(json);

        Assert.That(result.Success, Is.True);
        Assert.That(result.Data.PartyData, Is.Not.Null);
        Assert.That(result.Data.InventoryDict, Is.Not.Null);
        Assert.That(result.Data.eventFlags, Is.Not.Null);
        Assert.That(result.Data.EncounterMemory, Is.Not.Null);
        Assert.That(result.Data.OverworldEnemies, Is.Not.Null);
    }

    [Test]
    public void Decode_FutureVersion_IsRejectedExplicitly()
    {
        string json =
            "{\"schemaVersion\":" + (SaveSchema.CurrentVersion + 1)
            + ",\"currentScene\":\"TestMap\"}";

        SaveDecodeResult result = new SaveDataCodec().Decode(json);

        Assert.That(result.Success, Is.False);
        Assert.That(
            result.Failure,
            Is.EqualTo(SaveDecodeFailure.UnsupportedFutureVersion));
        Assert.That(result.Data, Is.Null);
    }

    [Test]
    public void Decode_EmptyLegacyObject_IsRejectedAsInvalidRoot()
    {
        SaveDecodeResult result = new SaveDataCodec().Decode("{}");

        Assert.That(result.Success, Is.False);
        Assert.That(result.Failure, Is.EqualTo(SaveDecodeFailure.InvalidRoot));
    }

    [Test]
    public void EncodeDecode_PreservesCurrentSaveDomains()
    {
        var data = new SaveData
        {
            currentScene = "MapField",
            currentRoomId = "village.square",
            spawnPointId = "west_gate",
            playerX = 12.5f,
            playerY = -4.25f,
            lookingDirection = 2,
            playerName = "Rapley",
            Money = 340,
            saveTime = "2026-07-23 20:00:00",
            playtimeSeconds = 912
        };
        data.PartyData.Add(new CharacterSaveData
        {
            CharacterDataID = "hero",
            CharacterID = "Rapley",
            Level = 4,
            EXP = 55,
            HP = 81,
            MaxHP = 100,
            MP = 17,
            MaxMP = 30,
            EquippedSkillIDs = new List<string> { "steam.slash" }
        });
        data.InventoryDict["small_potion"] = 3;
        data.eventFlags["chapter.prologue.complete"] = 1;
        data.EncounterMemory["enemy.no.scenario"] = new EncounterMemorySaveData
        {
            EncounterId = "enemy.no.scenario",
            MeetCount = 2,
            Defeated = true,
            SeenBeatIds = new List<string> { "first_meet" }
        };
        data.OverworldEnemies["enemy.no.scenario"] = new OverworldEnemySaveData
        {
            EnemyId = string.Empty,
            SceneName = "MapField",
            IsDefeated = true
        };

        var codec = new SaveDataCodec();
        SaveDecodeResult result = codec.Decode(codec.Encode(data));

        Assert.That(result.Success, Is.True);
        Assert.That(result.SourceVersion, Is.EqualTo(SaveSchema.CurrentVersion));
        Assert.That(result.WasMigrated, Is.False);
        Assert.That(result.Data.currentRoomId, Is.EqualTo("village.square"));
        Assert.That(result.Data.spawnPointId, Is.EqualTo("west_gate"));
        Assert.That(result.Data.Money, Is.EqualTo(340));
        Assert.That(result.Data.InventoryDict["small_potion"], Is.EqualTo(3));
        Assert.That(result.Data.PartyData[0].Level, Is.EqualTo(4));
        Assert.That(
            result.Data.PartyData[0].EquippedSkillIDs,
            Is.EqualTo(new[] { "steam.slash" }));
        Assert.That(
            result.Data.EncounterMemory["enemy.no.scenario"].SeenBeatIds,
            Is.EqualTo(new[] { "first_meet" }));
        Assert.That(
            result.Data.OverworldEnemies["enemy.no.scenario"].EnemyId,
            Is.EqualTo("enemy.no.scenario"));
        Assert.That(
            result.Data.OverworldEnemies["enemy.no.scenario"].IsDefeated,
            Is.True);
    }
}
