using UnityEngine;

/// <summary>개발 전투 샘플의 자산 참조 묶음. 실제 규칙은 기존 전투/스킬/시나리오가 소유합니다.</summary>
public sealed class BunnySlimeBattleLabData : ScriptableObject
{
    public EnemyData Enemy;
    public BattleScenarioData Scenario;
    public CharacterData[] Party;
    public ItemData[] Items;
    public EquipmentData[] Equipment;
    public SkillData GuardSkill;
    public SkillData DodgeSkill;
    public SkillData CounterSkill;
    public SkillData WaveSkill;
    public SkillData[] ProjectileSkills;
}
