public static class ShowcaseStationPaths
{
    public const string Root = "Assets/_Game/Content/Maps/Regions/ShowcaseStation";
    public const string PrefabRoot = Root + "/Prefabs/Rooms";
    public const string DataRoot = Root + "/Data";
    public const string RoomDataRoot = DataRoot + "/Rooms";
    public const string DialogueRoot = DataRoot + "/Dialogue";
    public const string ShopRoot = DataRoot + "/Shops";
    public const string PuzzleRoot = DataRoot + "/Puzzles";
    public const string EncounterRoot = DataRoot + "/Encounters";
    public const string RuntimeSequenceRoot = "Assets/_Game/Content/Scenarios/Runtime/Overworld/ShowcaseStation";
    public const string SourceSequenceRoot = "Assets/_Game/Content/Scenarios/Source/Overworld/ShowcaseStation";
    public const string CinematicRoot = "Assets/_Game/Content/Cinematics/Overworld/ShowcaseStation";

    public const string SharedWhiteSprite = "Assets/_Game/Content/Maps/Shared/Generated/RoomMap_WhiteSquare.png";
    public const string TestNpcSprite = "Assets/_Game/Content/Art/Samples/TestNPC.png";
    public const string SmallPotion = "Assets/_Game/Content/Items/Consumables/SmallPotion.asset";
    public const string SlimeEnemy = "Assets/_Game/Content/Characters/EnemyDB/DB_Slime.asset";
    public const string EnemyBasePrefab = "Assets/_Game/Content/Characters/Prefabs/Enemy/Enemy_Base.prefab";
    public const string PlayerBasePrefab = "Assets/_Game/Content/Characters/Prefabs/Player/Player_Base.prefab";
    public const string SeamlessBattleHostPrefab = "Assets/_Game/Content/Battle/Prefabs/System/SeamlessBattleHost.prefab";

    public const string IntroSource = SourceSequenceRoot + "/showcase_station_intro.sequence.yaml";
    public const string FinaleSource = SourceSequenceRoot + "/showcase_station_finale.sequence.yaml";
    public const string IntroRuntime = RuntimeSequenceRoot + "/showcase_station_intro.asset";
    public const string FinaleRuntime = RuntimeSequenceRoot + "/showcase_station_finale.asset";
    public const string FinalePowerShot = CinematicRoot + "/showcase_station_finale_power.asset";
    public const string FinaleDepartureShot = CinematicRoot + "/showcase_station_finale_departure.asset";
}

public static class ShowcaseStationIds
{
    public const string Arrival = "showcase.arrival_platform";
    public const string Square = "showcase.lantern_square";
    public const string Workshop = "showcase.workshop";
    public const string Passage = "showcase.steam_passage";
    public const string Train = "showcase.abandoned_train";

    public static readonly string[] RoomIds =
    {
        Arrival,
        Square,
        Workshop,
        Passage,
        Train
    };

    public static readonly string[] GeneratedRoomIds =
    {
        Arrival,
        Square,
        Workshop,
        Passage,
        Train
    };
}

public static class ShowcaseStationBuilder
{
    public static void BuildDataAndRooms()
    {
        TravelWorldBuilder.BuildOrUpdate();
    }
}