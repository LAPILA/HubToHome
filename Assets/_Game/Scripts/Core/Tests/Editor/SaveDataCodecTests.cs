using System.Collections.Generic;
using NUnit.Framework;

public class SaveDataCodecTests
{
    [Test]
    public void VersionFourMigratesHighestPartyProgressWithoutAddingMembersExperience()
    {
        SaveDecodeResult result = new SaveDataCodec().Decode(
            "{\"schemaVersion\":4,\"PartyData\":[{\"CharacterDataID\":\"wizel\",\"Level\":3,\"EXP\":20,\"HP\":0,\"AP\":2},"
            + "{\"CharacterDataID\":\"ally\",\"Level\":5,\"EXP\":12},{\"Level\":5,\"EXP\":30}],\"Money\":77}");
        Assert.That(result.Success, Is.True);
        Assert.That(result.Data.PartyProgression.Level, Is.EqualTo(5));
        Assert.That(result.Data.PartyProgression.EXP, Is.EqualTo(30));
        Assert.That(result.Data.PartyProgression.CurveCharacterId, Is.EqualTo("wizel"));
        Assert.That(result.Data.PartyData[0].HP, Is.Zero);
        Assert.That(result.Data.PartyData[0].AP, Is.EqualTo(2));
        Assert.That(result.Data.Money, Is.EqualTo(77));
    }

    [Test]
    public void VersionFivePreservesCanonicalPartyProgressAndRespawnPolicy()
    {
        var source = new SaveData
        {
            PartyProgression = new PartyProgressionSaveData { IsInitialized = true, Level = 8, EXP = 12, CurveCharacterId = "wizel" }
        };
        source.PartyData.Add(new CharacterSaveData { CharacterDataID = "wizel", Level = 2, EXP = 88 });
        source.OverworldEnemies["normal"] = new OverworldEnemySaveData { EnemyId = "normal", RespawnOnManualSave = true };
        var codec = new SaveDataCodec();
        var decoded = codec.Decode(codec.Encode(source));
        Assert.That(decoded.Success, Is.True);
        Assert.That(decoded.Data.PartyProgression.Level, Is.EqualTo(8));
        Assert.That(decoded.Data.PartyProgression.EXP, Is.EqualTo(12));
        Assert.That(decoded.Data.OverworldEnemies["normal"].RespawnOnManualSave, Is.True);
    }

    [Test]
    public void LegacyUninitializedGrowthPreservesSpentPointsUntilProfileMigration()
    {
        var result = new SaveDataCodec().Decode("{\"schemaVersion\":4,\"PartyData\":[{\"Level\":5,\"Growth\":{\"SkillPointsSpent\":3}}]}");
        Assert.That(result.Success, Is.True);
        Assert.That(result.Data.PartyData[0].Growth.SkillPointsSpent, Is.EqualTo(3));
    }

    [Test]
    public void EncodeDoesNotMutateCallerOrNestedCollections()
    {
        var data = new SaveData { schemaVersion = 0, saveTime = "original", Money = -7 };
        var member = new CharacterSaveData { EquippedEquipmentIDs = null };
        data.PartyData.Add(member);
        data.InventoryDict.Add("invalid", -1);
        var codec = new SaveDataCodec();
        SaveDecodeResult result = codec.Decode(codec.Encode(data, "new timestamp"));
        Assert.That(result.Success, Is.True);
        Assert.That(result.Data.saveTime, Is.EqualTo("new timestamp"));
        Assert.That(result.Data.Money, Is.Zero);
        Assert.That(data.schemaVersion, Is.Zero);
        Assert.That(data.saveTime, Is.EqualTo("original"));
        Assert.That(data.Money, Is.EqualTo(-7));
        Assert.That(member.EquippedEquipmentIDs, Is.Null);
        Assert.That(data.InventoryDict["invalid"], Is.EqualTo(-1));
    }

    [TestCase("NaN")]
    [TestCase("Infinity")]
    [TestCase("-Infinity")]
    public void Decode_NonFinitePositionRejectsCandidateForBackupRecovery(string value)
    {
        SaveDecodeResult result = new SaveDataCodec().Decode(
            "{\"schemaVersion\":4,\"playerX\":\"" + value + "\"}");
        Assert.That(result.Success, Is.False);
        Assert.That(result.Failure, Is.EqualTo(SaveDecodeFailure.InvalidJson));
    }

    [Test]
    public void Encode_NonFinitePositionCannotOverwriteHealthySave()
    {
        var data = new SaveData { playerY = float.PositiveInfinity };
        Assert.Throws<System.ArgumentException>(() => new SaveDataCodec().Encode(data));
    }

    [Test]
    public void Decode_RepairsNegativeEconomyAndFacingWithoutClampingEquipmentBoostedVitals()
    {
        SaveDecodeResult result = new SaveDataCodec().Decode(
            "{\"schemaVersion\":4,\"Money\":-4,\"playtimeSeconds\":-9,\"lookingDirection\":99," +
            "\"PartyData\":[{\"HP\":150,\"MaxHP\":100,\"AP\":30,\"MaxAP\":20}]}");
        Assert.That(result.Success, Is.True);
        Assert.That(result.Data.Money, Is.Zero);
        Assert.That(result.Data.playtimeSeconds, Is.Zero);
        Assert.That(result.Data.lookingDirection, Is.Zero);
        // 상한은 장비 카탈로그가 준비된 뒤 ProjectionService에서 계산합니다.
        Assert.That(result.Data.PartyData[0].HP, Is.EqualTo(150));
        Assert.That(result.Data.PartyData[0].AP, Is.EqualTo(30));
    }

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
    public void Decode_VersionOneSave_MigratesTrainStopStateToEmptyDefault()
    {
        const string json =
            "{\"schemaVersion\":1,\"currentScene\":\"Region_ShowcaseStation\","
            + "\"currentRoomId\":\"showcase.abandoned_train\"}";

        SaveDecodeResult result = new SaveDataCodec().Decode(json);

        Assert.That(result.Success, Is.True);
        Assert.That(result.SourceVersion, Is.EqualTo(1));
        Assert.That(result.WasMigrated, Is.True);
        Assert.That(result.Data.schemaVersion, Is.EqualTo(SaveSchema.CurrentVersion));
        Assert.That(result.Data.currentTrainStopId, Is.Empty);
    }

    [Test]
    public void Decode_MissingCollections_NormalizesDefaults()
    {
        const string json =
            "{\"currentScene\":\"TestMap\",\"PartyData\":null,\"InventoryDict\":null,"
            + "\"EquipmentInventoryDict\":null,\"eventFlags\":null,\"EncounterMemory\":null,\"OverworldEnemies\":null}";

        SaveDecodeResult result = new SaveDataCodec().Decode(json);

        Assert.That(result.Success, Is.True);
        Assert.That(result.Data.PartyData, Is.Not.Null);
        Assert.That(result.Data.InventoryDict, Is.Not.Null);
        Assert.That(result.Data.EquipmentInventoryDict, Is.Not.Null);
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
            currentTrainStopId = "train.stop.wide_field",
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
            AP = 17,
            MaxAP = 30,
            EquippedSkillIDs = new List<string> { "steam.slash" },
            UnlockedSkillIDs = new List<string> { "steam.slash", "steam.guard" },
            EquippedEquipmentIDs = new List<string> { "equip.steam_blade" },
            HasInitializedEquipment = true
        });
        data.InventoryDict["small_potion"] = 3;
        data.EquipmentInventoryDict["equip.steam_blade"] = 1;
        data.eventFlags["chapter.prologue.complete"] = 1;
        data.EncounterMemory["enemy.no.scenario"] = new EncounterMemorySaveData
        {
            EncounterId = "enemy.no.scenario",
            MeetCount = 2,
            Defeated = true,
            LastOutcome = BattleEncounterOutcome.Victory,
            VictoryCount = 1,
            EscapeCount = 2,
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
        Assert.That(
            result.Data.currentTrainStopId,
            Is.EqualTo("train.stop.wide_field"));
        Assert.That(result.Data.Money, Is.EqualTo(340));
        Assert.That(result.Data.InventoryDict["small_potion"], Is.EqualTo(3));
        Assert.That(result.Data.EquipmentInventoryDict["equip.steam_blade"], Is.EqualTo(1));
        Assert.That(result.Data.PartyData[0].Level, Is.EqualTo(4));
        Assert.That(
            result.Data.PartyData[0].EquippedSkillIDs,
            Is.EqualTo(new[] { "steam.slash" }));
        Assert.That(
            result.Data.PartyData[0].UnlockedSkillIDs,
            Is.EqualTo(new[] { "steam.slash", "steam.guard" }));
        Assert.That(result.Data.PartyData[0].EquippedEquipmentIDs, Has.Count.EqualTo(6));
        Assert.That(result.Data.PartyData[0].EquippedEquipmentIDs[0], Is.EqualTo("equip.steam_blade"));
        Assert.That(result.Data.PartyData[0].HasInitializedEquipment, Is.True);
        Assert.That(
            result.Data.EncounterMemory["enemy.no.scenario"].SeenBeatIds,
            Is.EqualTo(new[] { "first_meet" }));
        Assert.That(
            result.Data.EncounterMemory["enemy.no.scenario"].LastOutcome,
            Is.EqualTo(BattleEncounterOutcome.Victory));
        Assert.That(result.Data.EncounterMemory["enemy.no.scenario"].EscapeCount, Is.EqualTo(2));
        Assert.That(
            result.Data.OverworldEnemies["enemy.no.scenario"].EnemyId,
            Is.EqualTo("enemy.no.scenario"));
        Assert.That(
            result.Data.OverworldEnemies["enemy.no.scenario"].IsDefeated,
            Is.True);
    }
    [Test]
    public void Decode_VersionTwoSaveInitializesEquipmentAndPowerDomains()
    {
        const string json =
            "{\"schemaVersion\":2,\"currentScene\":\"TestMap\"," +
            "\"EquipmentInventoryDict\":null,\"PartyData\":[{" +
            "\"CharacterDataID\":\" hero \",\"UnlockedSkillIDs\":[\" steam \",\"steam\",\"\"]," +
            "\"EquippedEquipmentIDs\":[\" blade \"],\"HasInitializedEquipment\":false}]}";

        SaveDecodeResult result = new SaveDataCodec().Decode(json);

        Assert.That(result.Success, Is.True);
        Assert.That(result.SourceVersion, Is.EqualTo(2));
        Assert.That(result.WasMigrated, Is.True);
        Assert.That(result.Data.EquipmentInventoryDict, Is.Empty);
        Assert.That(result.Data.PartyData[0].CharacterDataID, Is.EqualTo("hero"));
        Assert.That(result.Data.PartyData[0].UnlockedSkillIDs, Is.EqualTo(new[] { "steam" }));
        Assert.That(result.Data.PartyData[0].EquippedEquipmentIDs, Has.Count.EqualTo(6));
        Assert.That(result.Data.PartyData[0].EquippedEquipmentIDs[0], Is.EqualTo("blade"));
    }

    [Test]
    public void Decode_VersionThreeSaveMigratesMpFieldsToActionPoints()
    {
        const string json =
            "{\"schemaVersion\":3,\"currentScene\":\"TestMap\",\"PartyData\":[{" +
            "\"CharacterDataID\":\"hero\",\"MP\":17,\"MaxMP\":42}]}";

        SaveDecodeResult result = new SaveDataCodec().Decode(json);

        Assert.That(result.Success, Is.True);
        Assert.That(result.SourceVersion, Is.EqualTo(3));
        Assert.That(result.WasMigrated, Is.True);
        Assert.That(result.Data.PartyData[0].AP, Is.EqualTo(17));
        Assert.That(result.Data.PartyData[0].MaxAP, Is.EqualTo(42));
    }
}
