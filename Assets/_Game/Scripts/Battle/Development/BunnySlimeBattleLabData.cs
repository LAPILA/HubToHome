using UnityEngine;
using System;
using Sirenix.OdinInspector;

[Serializable]
public sealed class BattleLabEncounterEntry
{
    [LabelText("조우 ID")] public string Id;
    [LabelText("메뉴 제목")] public string Title;
    [LabelText("한 줄 요약")] public string Summary;
    [LabelText("설명"), TextArea(3, 10)] public string Description;
    [LabelText("적 데이터")] public EnemyData Enemy;
    [LabelText("전투 시나리오")] public BattleScenarioData Scenario;
    [LabelText("전열 HP 1로 시작")] public bool WeakenFrontLine;
}

/// <summary>개발 전투 샘플의 자산 참조 묶음. 실제 규칙은 기존 전투/스킬/시나리오가 소유합니다.</summary>
public sealed class BunnySlimeBattleLabData : ScriptableObject
{
    [LabelText("전투 목록"), ListDrawerSettings(ShowIndexLabels = true)]
    public BattleLabEncounterEntry[] Encounters = Array.Empty<BattleLabEncounterEntry>();
    // 기존 생성기/직렬화 호환 참조. 실행은 Encounters만 읽습니다.
    [HideInInspector]
    public EnemyData Enemy;
    [HideInInspector]
    public BattleScenarioData Scenario;
    public CharacterData[] Party;
    public ItemData[] Items;
    public EquipmentData[] Equipment;
    [HideInInspector] public SkillData GuardSkill;
    [HideInInspector] public SkillData DodgeSkill;
    [HideInInspector] public SkillData CounterSkill;
    [HideInInspector] public SkillData WaveSkill;
    [HideInInspector] public SkillData[] ProjectileSkills;
}
