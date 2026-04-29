/// <summary>
/// 전투 상태 열거형 (battle.clinerules 기반)
/// </summary>
public enum BattleState
{
    Init,               // 초기화 (씬 로드 직후)
    TurnCalc,           // 턴 대기열 정렬 (SPD 기반)
    PlayerActionSelect, // 플레이어 커맨드 입력 대기
    ActionExecute,      // 공격/스킬 연출 및 QTE
    EnemyAction,        // 적 행동 및 방어 QTE
    BattleEnd,          // 전투 종료 (승리/패배)
}

/// <summary>
/// 플레이어 커맨드 열거형 (화살표 키 전용)
/// </summary>
public enum PlayerMenuAction
{
    Attack, // Fight
    Skill,  // Magic
    Item,
    Run,
}

/// <summary>
/// 방어 QTE 입력 열거형
/// </summary>
public enum DefenseInput
{
    None,
    Parry,  // Z — 패링 (MP 회복 + 데미지 0)
    Dodge,  // C — 회피
    Jump,   // Space — 점프
}

/// <summary>
/// 적 공격 유형 — 방어 시스템 분기에 사용
/// </summary>
public enum EnemyAttackType
{
    MeleeClose,     // 근거리 단일: 적이 EnemyAttackPos로 이동, 1x1 격자, QTE
    RangedAoE,      // 원거리/장판: 격자 NxM 확장, 화살표 이동 회피
    AoEAll,         // 전체 공격: 1x1 고정, 전원 동시 QTE
}
