using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;
using Sirenix.OdinInspector;

public enum CharacterDisplayNameMode
{
    StaticData,
    GlobalPlayerName
}

[System.Serializable]
public sealed class CharacterPowerUnlock
{
    [MinValue(1)] public int RequiredLevel = 1;
    [Required] public SkillData Skill;
}

[CreateAssetMenu(fileName = "NewCharacterData", menuName = "HubToHome/CharacterData")]
public class CharacterData : SerializedScriptableObject
{
    [BoxGroup("Identity"), HideLabel, PreviewField(60, ObjectFieldAlignment.Left)]
    public Sprite Portrait;

    [BoxGroup("Identity"), PreviewField(60, ObjectFieldAlignment.Left)]
    public Sprite TurnOrderPortrait;

    [BoxGroup("Battle Presentation"), AssetsOnly]
    [Tooltip("Character-specific prefab instantiated in battle. BattleManager fallback is used when empty.")]
    public GameObject BattlePrefab;

    [BoxGroup("Battle Presentation")]
    public Color BattleSymbolColor = Color.white;

    [BoxGroup("Identity")] public string CharacterID = "player_001";
    [BoxGroup("Identity")] public string DisplayName = "Player";
    [BoxGroup("Identity"), LabelText("이름 소스")]
    [Tooltip("주인공처럼 런타임 입력 이름을 써야 하면 GlobalPlayerName으로 설정하세요. 나머지 아군은 StaticData 그대로 두면 됩니다.")]
    public CharacterDisplayNameMode DisplayNameMode = CharacterDisplayNameMode.StaticData;

    [BoxGroup("Base Stats")]
    [HorizontalGroup("Base Stats/R1", LabelWidth = 60)] public int BaseMaxHP = 100;
    [FormerlySerializedAs("BaseMaxMP")]
    [HorizontalGroup("Base Stats/R1", LabelWidth = 60)] public int BaseMaxAP = 50;

    [BoxGroup("Base Stats")]
    [HorizontalGroup("Base Stats/R2", LabelWidth = 60)] public int BaseATK = 10;
    [HorizontalGroup("Base Stats/R2", LabelWidth = 60)] public int BaseDEF = 5;
    [HorizontalGroup("Base Stats/R2", LabelWidth = 60)] public int BaseSPD = 10;


    [BoxGroup("Progression"), Required]
    [Tooltip("레벨 제한, EXP 곡선, 레벨 보상, 능력치 환산 규칙을 공유합니다.")]
    public GrowthBalanceProfile GrowthProfile;

    [BoxGroup("Progression")]
    [Tooltip("캐릭터별 스킬 트리입니다. 비워 두면 기존 기본/레벨 해금 목록만 사용합니다.")]
    public SkillTreeDefinition SkillTree;

    [HideInInspector] public int MaxLevel = 99;
    [HideInInspector] public int BaseExperienceToLevel = 100;
    [HideInInspector] public float ExperienceGrowth = 1.18f;
    [HideInInspector] public int MaxHpPerLevel = 5;
    [HideInInspector] public int MaxMpPerLevel = 2;
    [HideInInspector] public int AttackPerLevel = 1;
    [HideInInspector] public int DefensePerLevel = 1;
    [HideInInspector] public int SpeedPerLevel = 0;

    [BoxGroup("Battle Loadout")]
    [ListDrawerSettings(ShowIndexLabels = true)]
    public List<SkillData> DefaultSkills = new List<SkillData>();

    [BoxGroup("Battle Loadout")]
    [ListDrawerSettings(ShowIndexLabels = true)]
    public List<CharacterPowerUnlock> PowerUnlocks = new List<CharacterPowerUnlock>();

    [System.Obsolete("Use BaseMaxAP. This property only preserves source compatibility.")]
    public int BaseMaxMP
    {
        get => BaseMaxAP;
        set => BaseMaxAP = value;
    }

    public string ResolveDisplayName(string runtimePlayerName = null)
    {
        if (DisplayNameMode == CharacterDisplayNameMode.GlobalPlayerName && !string.IsNullOrWhiteSpace(runtimePlayerName))
            return runtimePlayerName;

        if (!string.IsNullOrWhiteSpace(DisplayName))
            return DisplayName;

        return CharacterID;
    }
}