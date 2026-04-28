using UnityEngine;

/// <summary>QTE 타입 열거형</summary>
public enum QTEType
{
    None,       // QTE 없음 (기본 공격)
    Timing,     // 타이밍 바 (정확한 타이밍에 입력)
    Mashing,    // 연타 (일정 횟수 이상 입력)
}

/// <summary>
/// 스킬 데이터 ScriptableObject.
/// 에디터에서 Create > HubToHome > SkillData 로 생성하세요.
/// </summary>
[CreateAssetMenu(fileName = "NewSkill", menuName = "HubToHome/SkillData")]
public class SkillData : ScriptableObject
{
    [Header("Identity")]
    public string SkillName     = "Skill";
    public string SkillID       = "skill_001";
    public Sprite Icon;
    [TextArea] public string Description = "";

    [Header("Cost")]
    public int MPCost   = 10;

    [Header("Damage")]
    [Tooltip("ATK에 곱해지는 배율")]
    public float DamageMultiplier = 1.5f;

    [Header("QTE")]
    public QTEType QTEType = QTEType.Timing;
    [Tooltip("QTE 성공 시 추가 데미지 배율")]
    public float QTESuccessMultiplier = 1.5f;
    [Tooltip("QTE 실패 시 데미지 배율")]
    public float QTEFailMultiplier    = 0.3f;

    [Header("Visual")]
    [Tooltip("스킬 연출 프리팹 (ObjectPool 사용)")]
    public GameObject EffectPrefab;
}
