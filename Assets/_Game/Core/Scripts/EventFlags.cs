/// <summary>
/// 게임 내 이벤트 플래그 키 상수 모음.
/// 코드나 인스펙터에서 하드코딩된 문자열 대신 이 상수를 사용하세요.
/// </summary>
public static class EventFlags
{
    // ── 메인 스토리 진행도 (0: 시작안함, 1: 진행중, 2: 완료 등) ──
    public const string Chapter1_Progress = "ch1_progress";

    // ── 평화/몰살 카운터 (AddFlag 로 누적) ──
    public const string EnemiesSpared = "enemies_spared"; 
    public const string EnemiesKilled = "enemies_killed";

    // ── NPC 만남 및 대화 여부 (0: 안만남, 1: 만남) ──
    public const string Met_Shopkeeper = "met_shopkeeper";
    public const string Talked_To_King = "talked_to_king";
    public const string Rabbit_Happy = "rabbit_happy"; // 토끼 텍스트 실험 결과

    // ── 오버월드 상자/문/트리거 상태 (0: 닫힘, 1: 열림) ──
    public const string Chest_01_Opened = "chest_01_opened";
    public const string Door_Boss_Unlocked = "door_boss_unlocked";
}