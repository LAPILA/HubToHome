using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

public static class SaveSchema
{
    public const int LegacyVersion = 0;
    public const int CurrentVersion = 3;
}

public enum SaveDecodeFailure
{
    None,
    EmptyContent,
    InvalidJson,
    InvalidRoot,
    UnsupportedVersion,
    UnsupportedFutureVersion
}

public sealed class SaveDecodeResult
{
    private SaveDecodeResult()
    {
    }

    public bool Success { get; private set; }
    public SaveData Data { get; private set; }
    public int SourceVersion { get; private set; }
    public bool WasMigrated { get; private set; }
    public SaveDecodeFailure Failure { get; private set; }
    public string Message { get; private set; }

    public static SaveDecodeResult Succeeded(
        SaveData data,
        int sourceVersion,
        bool wasMigrated)
    {
        return new SaveDecodeResult
        {
            Success = true,
            Data = data,
            SourceVersion = sourceVersion,
            WasMigrated = wasMigrated,
            Failure = SaveDecodeFailure.None,
            Message = string.Empty
        };
    }

    public static SaveDecodeResult Failed(
        SaveDecodeFailure failure,
        string message,
        int sourceVersion = SaveSchema.LegacyVersion)
    {
        return new SaveDecodeResult
        {
            Success = false,
            Data = null,
            SourceVersion = sourceVersion,
            WasMigrated = false,
            Failure = failure,
            Message = message ?? string.Empty
        };
    }
}

public sealed class SaveDataCodec
{
    private static readonly HashSet<string> LegacyPayloadFields =
        new HashSet<string>(StringComparer.Ordinal)
        {
            "currentScene",
            "currentRoomId",
            "spawnPointId",
            "playerX",
            "playerY",
            "lookingDirection",
            "PartyData",
            "InventoryDict",
            "EquipmentInventoryDict",
            "eventFlags",
            "EncounterMemory",
            "OverworldEnemies",
            "Money",
            "saveTime",
            "playtimeSeconds",
            "playerName"
        };

    public string Encode(SaveData data)
    {
        if (data == null)
            throw new ArgumentNullException(nameof(data));

        Normalize(data);
        data.schemaVersion = SaveSchema.CurrentVersion;
        return JsonConvert.SerializeObject(data, Formatting.Indented);
    }

    public SaveDecodeResult Decode(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return SaveDecodeResult.Failed(
                SaveDecodeFailure.EmptyContent,
                "저장 내용이 비어 있습니다.");
        }

        JObject root;
        try
        {
            root = JToken.Parse(json) as JObject;
        }
        catch (JsonException exception)
        {
            return SaveDecodeResult.Failed(
                SaveDecodeFailure.InvalidJson,
                "저장 JSON을 읽을 수 없습니다: " + exception.Message);
        }

        if (root == null)
        {
            return SaveDecodeResult.Failed(
                SaveDecodeFailure.InvalidRoot,
                "저장 JSON의 최상위 값은 객체여야 합니다.");
        }

        bool hasVersion = root.TryGetValue("schemaVersion", out JToken versionToken);
        if (!hasVersion && !HasLegacyPayload(root))
        {
            return SaveDecodeResult.Failed(
                SaveDecodeFailure.InvalidRoot,
                "저장 데이터로 식별할 수 있는 필드가 없습니다.");
        }

        if (!TryReadVersion(versionToken, hasVersion, out int sourceVersion))
        {
            return SaveDecodeResult.Failed(
                SaveDecodeFailure.UnsupportedVersion,
                "schemaVersion은 0 이상의 정수여야 합니다.");
        }

        if (sourceVersion > SaveSchema.CurrentVersion)
        {
            return SaveDecodeResult.Failed(
                SaveDecodeFailure.UnsupportedFutureVersion,
                "현재 게임보다 새로운 저장 버전입니다.",
                sourceVersion);
        }

        if (sourceVersion < SaveSchema.LegacyVersion)
        {
            return SaveDecodeResult.Failed(
                SaveDecodeFailure.UnsupportedVersion,
                "지원하지 않는 저장 버전입니다.",
                sourceVersion);
        }

        SaveData data;
        try
        {
            data = root.ToObject<SaveData>();
        }
        catch (JsonException exception)
        {
            return SaveDecodeResult.Failed(
                SaveDecodeFailure.InvalidJson,
                "저장 데이터를 변환할 수 없습니다: " + exception.Message,
                sourceVersion);
        }

        if (data == null)
        {
            return SaveDecodeResult.Failed(
                SaveDecodeFailure.InvalidRoot,
                "저장 데이터 객체를 만들 수 없습니다.",
                sourceVersion);
        }

        int workingVersion = sourceVersion;
        while (workingVersion < SaveSchema.CurrentVersion)
        {
            switch (workingVersion)
            {
                case SaveSchema.LegacyVersion:
                    MigrateLegacyToVersionOne(data);
                    workingVersion = 1;
                    break;
                case 1:
                    MigrateVersionOneToVersionTwo(data);
                    workingVersion = 2;
                    break;
                case 2:
                    MigrateVersionTwoToVersionThree(data);
                    workingVersion = 3;
                    break;
                default:
                    return SaveDecodeResult.Failed(
                        SaveDecodeFailure.UnsupportedVersion,
                        "마이그레이션 경로가 없는 저장 버전입니다.",
                        sourceVersion);
            }
        }

        Normalize(data);
        data.schemaVersion = SaveSchema.CurrentVersion;
        return SaveDecodeResult.Succeeded(
            data,
            sourceVersion,
            sourceVersion != SaveSchema.CurrentVersion);
    }

    private static bool TryReadVersion(
        JToken versionToken,
        bool hasVersion,
        out int version)
    {
        if (!hasVersion)
        {
            version = SaveSchema.LegacyVersion;
            return true;
        }

        if (versionToken == null || versionToken.Type != JTokenType.Integer)
        {
            version = SaveSchema.LegacyVersion;
            return false;
        }

        try
        {
            version = versionToken.Value<int>();
            return version >= SaveSchema.LegacyVersion;
        }
        catch (Exception)
        {
            version = SaveSchema.LegacyVersion;
            return false;
        }
    }

    private static bool HasLegacyPayload(JObject root)
    {
        foreach (JProperty property in root.Properties())
        {
            if (LegacyPayloadFields.Contains(property.Name))
                return true;
        }

        return false;
    }

    private static void MigrateLegacyToVersionOne(SaveData data)
    {
        Normalize(data);
        data.schemaVersion = 1;
    }

    private static void MigrateVersionOneToVersionTwo(SaveData data)
    {
        data.currentTrainStopId = NormalizeText(data.currentTrainStopId);
        data.schemaVersion = 2;
    }

    private static void MigrateVersionTwoToVersionThree(SaveData data)
    {
        data.EquipmentInventoryDict ??= new Dictionary<string, int>();
        data.schemaVersion = 3;
    }

    private static void Normalize(SaveData data)
    {
        data.currentScene = string.IsNullOrWhiteSpace(data.currentScene)
            ? SceneName.Overworld
            : data.currentScene.Trim();
        data.currentRoomId = NormalizeText(data.currentRoomId);
        data.spawnPointId = NormalizeText(data.spawnPointId);
        data.currentTrainStopId = NormalizeText(data.currentTrainStopId);
        data.playerName = NormalizeText(data.playerName);
        data.saveTime = NormalizeText(data.saveTime);

        data.PartyData = NormalizeParty(data.PartyData);
        data.InventoryDict = NormalizePositiveCounts(data.InventoryDict);
        data.EquipmentInventoryDict = NormalizePositiveCounts(data.EquipmentInventoryDict);
        data.eventFlags = data.eventFlags
            ?? new Dictionary<string, int>();
        data.EncounterMemory = NormalizeEncounterMemory(data.EncounterMemory);
        data.OverworldEnemies = NormalizeOverworldEnemies(data.OverworldEnemies);
    }

    private static List<CharacterSaveData> NormalizeParty(
        List<CharacterSaveData> source)
    {
        var result = new List<CharacterSaveData>();
        if (source == null)
            return result;

        for (int i = 0; i < source.Count; i++)
        {
            CharacterSaveData member = source[i];
            if (member == null)
                continue;

            member.CharacterDataID = NormalizeText(member.CharacterDataID);
            member.CharacterID = NormalizeText(member.CharacterID);
            member.EquippedSkillIDs = NormalizeUniqueIds(member.EquippedSkillIDs);
            member.UnlockedSkillIDs = NormalizeUniqueIds(member.UnlockedSkillIDs);
            member.EquippedEquipmentIDs = NormalizeEquipmentSlots(member.EquippedEquipmentIDs);
            result.Add(member);
        }

        return result;
    }

    private static Dictionary<string, EncounterMemorySaveData>
        NormalizeEncounterMemory(
            Dictionary<string, EncounterMemorySaveData> source)
    {
        var result = new Dictionary<string, EncounterMemorySaveData>(
            StringComparer.Ordinal);
        if (source == null)
            return result;

        foreach (KeyValuePair<string, EncounterMemorySaveData> entry in source)
        {
            string key = NormalizeText(entry.Key);
            EncounterMemorySaveData memory = entry.Value;
            string encounterId = !string.IsNullOrEmpty(key)
                ? key
                : NormalizeText(memory?.EncounterId);
            if (string.IsNullOrEmpty(encounterId))
                continue;

            if (memory == null)
                memory = new EncounterMemorySaveData();

            memory.EncounterId = encounterId;
            memory.MeetCount = Math.Max(0, memory.MeetCount);
            memory.LastOutcome = Enum.IsDefined(
                typeof(BattleEncounterOutcome),
                memory.LastOutcome)
                ? memory.LastOutcome
                : BattleEncounterOutcome.Unknown;
            memory.VictoryCount = Math.Max(0, memory.VictoryCount);
            memory.EscapeCount = Math.Max(0, memory.EscapeCount);
            memory.PartyDefeatCount = Math.Max(0, memory.PartyDefeatCount);
            memory.SeenBeatIds = NormalizeUniqueIds(memory.SeenBeatIds);
            result[encounterId] = memory;
        }

        return result;
    }

    private static Dictionary<string, OverworldEnemySaveData>
        NormalizeOverworldEnemies(
            Dictionary<string, OverworldEnemySaveData> source)
    {
        var result = new Dictionary<string, OverworldEnemySaveData>(
            StringComparer.Ordinal);
        if (source == null)
            return result;

        foreach (KeyValuePair<string, OverworldEnemySaveData> entry in source)
        {
            OverworldEnemySaveData state = entry.Value;
            string enemyId = NormalizeText(entry.Key);
            if (string.IsNullOrEmpty(enemyId))
                enemyId = NormalizeText(state?.EnemyId);
            if (string.IsNullOrEmpty(enemyId))
                continue;

            if (state == null)
                state = new OverworldEnemySaveData();

            state.EnemyId = string.IsNullOrWhiteSpace(state.EnemyId)
                ? enemyId
                : state.EnemyId.Trim();
            state.SceneName = NormalizeText(state.SceneName);
            result[enemyId] = state;
        }

        return result;
    }

    private static Dictionary<string, int> NormalizePositiveCounts(Dictionary<string, int> source)
    {
        var result = new Dictionary<string, int>(StringComparer.Ordinal);
        if (source == null)
            return result;

        foreach (KeyValuePair<string, int> entry in source)
        {
            string id = NormalizeText(entry.Key);
            if (!string.IsNullOrEmpty(id) && entry.Value > 0)
                result[id] = entry.Value;
        }

        return result;
    }

    private static List<string> NormalizeEquipmentSlots(List<string> source)
    {
        int slotCount = EquipmentLoadoutService.SlotCount;
        var result = new List<string>(slotCount);
        for (int i = 0; i < slotCount; i++)
        {
            string value = source != null && i < source.Count
                ? NormalizeText(source[i])
                : string.Empty;
            result.Add(value);
        }

        return result;
    }

    private static List<string> NormalizeUniqueIds(List<string> source)
    {
        var result = new List<string>();
        if (source == null)
            return result;

        var seen = new HashSet<string>(StringComparer.Ordinal);
        for (int i = 0; i < source.Count; i++)
        {
            string value = NormalizeText(source[i]);
            if (!string.IsNullOrEmpty(value) && seen.Add(value))
                result.Add(value);
        }

        return result;
    }

    private static string NormalizeText(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
    }
}
