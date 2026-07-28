using System.Collections.Generic;
using UnityEngine;

public static class TravelTrainIds
{
    public const string Room = "travel_train.main_car";
    public const string Network = "train.network.main";
    public const string ShowcaseStop = "train.stop.showcase_station";
    public const string WideFieldStop = "train.stop.wide_field";
    public const string ShowcaseCurrentFlag = "train.current.showcase_station";
    public const string WideFieldCurrentFlag = "train.current.wide_field";
    public const string DepartureSequence = "travel_train.departure";
    public const string DepartureShot = "travel_train.departure.run";
}

public static class TravelTrainPaths
{
    public const string Root = "Assets/_Game/Content/Maps/Regions/TravelTrain";
    public const string SceneRoot = Root + "/Scenes";
    public const string PrefabRoot = Root + "/Prefabs/Rooms";
    public const string DataRoot = Root + "/Data";
    public const string RoomDataRoot = DataRoot + "/Rooms";
    public const string StopDataRoot = DataRoot + "/Stops";
    public const string DialogueRoot = DataRoot + "/Dialogue";

    public const string Scene = SceneRoot + "/Region_TravelTrain.unity";
    public const string Prefab = PrefabRoot + "/Room_TravelTrainInterior.prefab";
    public const string RoomDefinition = RoomDataRoot + "/Room_TravelTrainInterior_Definition.asset";
    public const string AreaDefinition = RoomDataRoot + "/Room_TravelTrainInterior_Area.asset";
    public const string Network = StopDataRoot + "/TrainNetwork_Main.asset";
    public const string ShowcaseStop = StopDataRoot + "/TrainStop_ShowcaseStation.asset";
    public const string WideFieldStop = StopDataRoot + "/TrainStop_WideField.asset";

    public const string SourceRoot = "Assets/_Game/Content/Scenarios/Source/Overworld/TravelTrain";
    public const string RuntimeRoot = "Assets/_Game/Content/Scenarios/Runtime/Overworld/TravelTrain";
    public const string CinematicRoot = "Assets/_Game/Content/Cinematics/Overworld/TravelTrain";
    public const string DepartureSource = SourceRoot + "/travel_train_departure.sequence.yaml";
    public const string DepartureRuntime = RuntimeRoot + "/travel_train_departure.asset";
    public const string DepartureShot = CinematicRoot + "/travel_train_departure.asset";
}

public sealed class TravelTrainCoreAssetBundle
{
    public GameObject TrainRoomPrefab;
    public RoomDefinition TrainRoom;
    public AreaDefinition TrainArea;
}

public sealed class TravelTrainDataBundle
{
    public TravelTrainCoreAssetBundle Core;
    public TrainStopDefinition ShowcaseStop;
    public TrainStopDefinition WideFieldStop;
    public TrainNetworkDefinition Network;
    public ActionSequenceAsset DepartureSequence;
    public CinematicShotAsset DepartureShot;
    public FlagDialogueSelector ConductorDialogue;
    public FlagDialogueSelector WindowDialogue;
    public readonly Dictionary<string, DialogueData> Dialogues =
        new Dictionary<string, DialogueData>(System.StringComparer.Ordinal);
}

public sealed class WideFieldDataBundle
{
    public RoomDefinition StationRoom;
    public AreaDefinition StationArea;
    public RoomDefinition ExpanseRoom;
    public AreaDefinition ExpanseArea;
    public DialogueData RouteSignDialogue;
}

public sealed class TravelWorldBuildResult
{
    public ShowcaseStationDataBundle Showcase;
    public WideFieldDataBundle WideField;
    public TravelTrainDataBundle Train;
    public TrainTravelValidationReport Validation;
}