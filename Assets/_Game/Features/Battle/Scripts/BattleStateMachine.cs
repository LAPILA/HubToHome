/// <summary>
/// 전투 상태 머신의 상태(State) 열거형
/// </summary>
public enum BattleState
{
    Init,               // 전투 초기화 및 연출 (심리스 진입 등)
    TurnCalc,           // 턴 대기열 정렬 (SPD 기반)
    PlayerActionSelect, // 플레이어 커맨드 입력 대기
    BattleDialogue,     // 🚨 [추가됨] 전투 중 대화 (ACT 실행 결과, 적의 턴 시작 전 도발 대사 등)
    ActionExecute,      // 아군 공격/스킬 연출 및 QTE
    EnemyAction,        // 적 행동 및 방어/탄막 회피 페이즈
    BattleEnd,          // 전투 종료 (승리/도주/패배)
}

/// <summary>
/// 플레이어 메인 커맨드 (UI 매핑용)
/// </summary>
public enum PlayerMenuAction
{
    Attack,
    Act,     // 🚨 [추가됨] (적에게 말걸기 등)
    Skill,   // 마법/특수기 (TP/MP 소모)
    Item,
    Defend,  // 🚨 [추가됨] 방어 (받는 피해 절반 감소 + TP 회복)
    Run
}

/// <summary>
/// 적 공격에 대한 방어/QTE 입력 (액션성 강화용)
/// </summary>
public enum DefenseInput
{
    None,
    Parry,  // Z — 패링 (타이밍 맞출 시 데미지 무효화 + MP 회복)
    Dodge,  // C — 회피 (무적 프레임)
    Jump,   // Space — 점프 (하단 판정 공격 회피)
}

/// <summary>
/// 적 공격 유형 및 회피 방식
/// </summary>
public enum EnemyAttackType
{
    MeleeClose,     // 근거리 단일 (적이 코앞까지 와서 공격, 타이밍 가드 필요)
    RangedAoE,      // 원거리/장판 (위치 지정 공격, 방향키로 범위 밖으로 피해야 함)
    AoEAll,         // 전체 공격 (회피 불가, 패링이나 방어만 가능)
    BulletHell,
    JumpOnly
}