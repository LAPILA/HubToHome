/// <summary>
/// 게임 내 이벤트 플래그 키 상수 모음.
/// 오타로 인한 버그를 막기 위해 하드코딩 문자열 대신 상수를 사용하세요.
/// 예시들이니 삭제하고 적용해주세요
/// </summary>
public static class EventFlags
{
    // ── 메인 스토리 진행도 ──
    public const string Chapter1_Progress = "ch1_progress";

    // ── 평화/몰살 카운터 ──
    public const string EnemiesSpared = "enemies_spared"; 
    public const string EnemiesKilled = "enemies_killed";

    // ── NPC 만남 여부 ──
    public const string Met_Shopkeeper = "met_shopkeeper";
    public const string Talked_To_King = "talked_to_king";

    // ── 특정 아이템/상자 획득 여부 (0: 안열림, 1: 열림) ──
    public const string Chest_01_Opened = "chest_01_opened";
}