/// <summary>
/// 모든 씬 이름을 상수로 관리하여 오타(Typo)로 인한 런타임 에러를 방지합니다.
/// 사용 예: SceneLoader.Instance.LoadScene(SceneName.Battle);
/// </summary>
public static class SceneName
{
    public const string Bootstrap = "BootstrapScene"; // 가장 먼저 실행되는 코어 씬
    public const string Title     = "TitleScene";     // 메인 메뉴
    public const string Overworld = "OverworldScene"; // 기본 필드 맵
    public const string Battle    = "BattleScene";    // 전용 전투 씬 (심리스가 아닐 경우)
    
    // 추가 씬이 생기면 아래에 계속 작성하세요
    // public const string Shop = "ShopScene";
    // public const string Dungeon_01 = "Dungeon01_Scene";
}