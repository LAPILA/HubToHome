/// <summary>
/// 개발 및 QA 전용 콘텐츠의 에디터 경로를 한곳에서 관리합니다.
/// 런타임 콘텐츠는 이 경로 상수에 의존하지 않습니다.
/// </summary>
public static class DevelopmentContentPaths
{
    public const string Root = "Assets/_Game/Content/Maps/Development";
    public const string GameplayCameraRigPrefab = "Assets/_Game/Core/Prefabs/Camera/GameplayCameraRig.prefab";
    public const string RegionsRoot = Root + "/Regions";
    public const string SharedRoot = Root + "/Shared";
    public const string SharedArtRoot = SharedRoot + "/Art";
    public const string SharedMarkerSamplesRoot = SharedRoot + "/MarkerSamples";

    public const string TestMapRoot = Root + "/TestMap";
    public const string TestMapScene = TestMapRoot + "/TestMap.unity";

    public const string TemplatesRoot = Root + "/Templates";
    public const string MapFieldStarterRoot = TemplatesRoot + "/MapFieldStarter";
    public const string MapFieldStarterScene = MapFieldStarterRoot + "/Scenes/Region_MapFieldStarter.unity";

    public const string TitleRoot = RegionsRoot + "/Title";
    public const string TitleScene = TitleRoot + "/00_TitleScene.unity";
    public const string IntroScene = TitleRoot + "/01_IntroScene.unity";

    public const string PrologueSubwayRoot = RegionsRoot + "/PrologueSubway";
    public const string PrologueSubwayScene = PrologueSubwayRoot + "/Scenes/OverworldScene.unity";
    public const string PrologueSubwayCinematicRoot = PrologueSubwayRoot + "/Cinematics";

    public const string ShowcaseStationRoot = RegionsRoot + "/ShowcaseStation";
    public const string ShowcaseStationScene = ShowcaseStationRoot + "/Scenes/Region_ShowcaseStation.unity";
    public const string TravelTrainRoot = RegionsRoot + "/TravelTrain";
    public const string TravelTrainScene = TravelTrainRoot + "/Scenes/Region_TravelTrain.unity";
    public const string WideFieldRoot = RegionsRoot + "/WideField";
    public const string WideFieldScene = WideFieldRoot + "/Scenes/Region_WideField.unity";

    public const string TestNpcSprite = SharedArtRoot + "/TestNPC.png";
}
