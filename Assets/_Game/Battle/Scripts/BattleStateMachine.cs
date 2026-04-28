/// <summary>
/// 전투 상태 열거형.
/// BattleManager가 이 상태를 기반으로 흐름을 제어합니다.
/// </summary>
public enum BattleState
{
    Idle,           // 초기 대기
    Intro,          // 전투 진입 연출
    PlayerTurn,     // 플레이어 메뉴 선택
    ActionPhase,    // 공격/스킬 실행 및 QTE
    EnemyTurn,      // 적 행동 및 방어 QTE
    Result,         // 전투 결과 (승리/패배)
}

/// <summary>
/// 플레이어 메뉴 선택 열거형
/// </summary>
public enum PlayerMenuAction
{
    Attack,
    Skill,
    Item,
    Run,
}

/// <summary>
/// 방어 QTE 입력 열거형
/// </summary>
public enum DefenseInput
{
    None,
    Parry,   // Z키 - 패링
    Dodge,   // C키 - 회피
    Jump,    // Space - 점프
}
