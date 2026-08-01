/// <summary>
/// 모든 씬 이름을 상수로 관리하여 오타(Typo)로 인한 런타임 에러를 방지합니다.
/// 사용 예: SceneLoader.Instance.LoadScene(SceneName.Battle);
/// </summary>
public static class SceneName
{
    public const string Bootstrap = "BootstrapScene";
    public const string Title = "00_TitleScene";
    public const string Overworld = "OverworldScene";
    public const string Battle = "BattleScene";
}
