using UnityEngine;

public enum QTEType { None, Timing, Mashing }
public enum SkillCastType { MeleeDash, RangedStatic } // 돌진형인지 제자리형인지

[CreateAssetMenu(fileName = "NewSkill", menuName = "HubToHome/SkillData")]
public class SkillData : ScriptableObject
{
    [Header("Identity")]
    public string SkillName     = "Skill";
    public string SkillID       = "skill_001";
    public Sprite Icon;
    [TextArea] public string Description = "";

    [Header("Cost & Damage")]
    public int MPCost   = 10;
    public float DamageMultiplier = 1.5f;

    [Header("QTE")]
    public QTEType QTEType = QTEType.Timing;
    public float QTESuccessMultiplier = 1.5f;
    public float QTEFailMultiplier    = 0.3f;

    [Header("Animation & Timing")]
    [Tooltip("MeleeDash: 적에게 돌격, RangedStatic: 제자리 시전")]
    public SkillCastType CastType = SkillCastType.MeleeDash;
    
    [Tooltip("스킬 시전 후 몇 초 뒤에 VFX를 생성할 것인가?")]
    public float VFXSpawnDelay = 0.2f;
    
    [Tooltip("스킬 시전 후 몇 초 뒤에 데미지가 들어갈 것인가?")]
    public float DamageDelay = 0.25f;

    [Header("Visual")]
    [Tooltip("스킬 연출 프리팹 (ObjectPool 사용)")]
    public GameObject EffectPrefab;

    [Tooltip("이펙트가 적에게 터지나요(True), 내 무기/앞에서 터지나요(False)?")]
    public bool SpawnVFXOnTarget = true;
}