using System.Collections.Generic;
using UnityEngine;
using Sirenix.OdinInspector;

[System.Serializable]
public sealed class EnemyDropEntry
{
    public string ItemId = "";
    [MinValue(1)] public int MinAmount = 1;
    [MinValue(1)] public int MaxAmount = 1;
    [Range(0f, 1f)] public float DropChance = 1f;
}

[CreateAssetMenu(fileName = "NewEnemyData", menuName = "HubToHome/EnemyData")]
public class EnemyData : SerializedScriptableObject 
{
    [BoxGroup("Identity"), HideLabel, PreviewField(60, ObjectFieldAlignment.Left)]
    public Sprite Portrait;
    [BoxGroup("Identity"), PreviewField(60, ObjectFieldAlignment.Left)]
    public Sprite TurnOrderPortrait;
    
    [BoxGroup("Identity"), Tooltip("Scenario Source와 Battle Event Rule에서 사용하는 안정적인 적 ID입니다. 예: zev")]
    public string EnemyId = "";

    [BoxGroup("Identity")] public string EnemyName = "Enemy";

    [BoxGroup("Audio")]
    [Tooltip("이 적과 전투 시작 시 우선 재생할 전투 BGM입니다. 비워두면 맵 기본 전투 BGM을 사용합니다.")]
    public AudioClip BattleBGM;

    [BoxGroup("Battle Presentation")]
    [Required, AssetsOnly]
    [Tooltip("BattleScene 또는 심리스 전투에서 실제로 생성할 전투용 적 프리팹입니다. 비워두면 BattleManager의 기본 Enemy Base Prefab을 사용합니다.")]
    public GameObject BattlePrefab;
    [BoxGroup("Battle Presentation")]
    [Tooltip("공격 후 원래 자리로 돌아갈 때 사용할 애니메이션 Trigger 이름입니다. 기본값은 BattleMove 입니다.")]
    public string ReturnMoveTrigger = "BattleMove";

    [BoxGroup("Base Stats")] 
    [HorizontalGroup("Base Stats/R1", LabelWidth = 40)] public int MaxHP = 100;
    [HorizontalGroup("Base Stats/R1", LabelWidth = 40)] public int ATK = 8;
    
    [BoxGroup("Base Stats")] 
    [HorizontalGroup("Base Stats/R2", LabelWidth = 40)] public int DEF = 3;
    [HorizontalGroup("Base Stats/R2", LabelWidth = 40)] public int SPD = 8;

    [BoxGroup("Overworld Encounter"), MinValue(1)]
    public int ThreatLevel = 1;
    [BoxGroup("Overworld Encounter")]
    public bool AllowInstantKillAfterDefeat = false;
    [BoxGroup("Overworld Encounter"), MinValue(0)]
    public int InstantKillLevelGap = 5;

    [BoxGroup("AI & Pattern")]
    [Range(0f, 1f)] public float SkillUseChance = 0.3f;
    [BoxGroup("AI & Pattern")]
    [Range(0f, 1f), Tooltip("강한 공격 후보를 고를 확률입니다. 선택되면 TelegraphStrongSkill 규칙에 따라 예고 후 실행됩니다.")]
    public float StrongSkillUseChance = 0.25f;
    [BoxGroup("AI & Pattern")]
    [Tooltip("강한 공격/스킬을 예고한 뒤 플레이어 행동 1번 후 실제 실행할지 여부")]
    public bool TelegraphStrongSkill = true;
    [BoxGroup("AI & Pattern")]
    [MinValue(1), Tooltip("강한 공격 예고 후 몇 번째 자기 턴에 실행할지. 기본 1 = 다음 자기 턴")]
    public int TelegraphTurns = 1;
    [BoxGroup("AI & Pattern")] public bool HasEnragedPattern = false;
    [BoxGroup("AI & Pattern")] 
    [InfoBox("대형 적은 중앙으로 이동하지 않고 제자리에서 공격합니다.", InfoMessageType.Info, "IsLargeEnemy")]
    public bool IsLargeEnemy = false;

    [BoxGroup("Combat Logic")]
    [InfoBox("일반 적 스킬입니다. 선택되면 예고 없이 바로 사용합니다.")]
    [ListDrawerSettings(ShowIndexLabels = true)]
    public List<SkillData> SkillList = new List<SkillData>();

    [BoxGroup("Combat Logic")]
    [InfoBox("강한 적 스킬입니다. TelegraphStrongSkill이 켜져 있으면 예고 후 다음 자기 턴에 사용합니다.")]
    [ListDrawerSettings(ShowIndexLabels = true)]
    public List<SkillData> StrongSkillList = new List<SkillData>();

    // ── 🚨 추가됨: 상태이상 내성 (배율) ──
    [BoxGroup("Resistances (상태이상 내성)")]
    [InfoBox("1.0은 기본 확률, 0.0이면 완전 면역, 2.0이면 2배로 잘 걸림")]
    [DictionaryDrawerSettings(KeyLabel = "상태이상", ValueLabel = "걸릴 확률 배율")]
    public Dictionary<string, float> StatusResistances = new Dictionary<string, float>()
    {
        { "Burn", 1.0f }, { "Freeze", 1.0f }, { "Poison", 1.0f }, 
        { "Bleed", 1.0f }, { "Stun", 1.0f }, { "Bind", 1.0f }
    };

    [BoxGroup("Rewards")]
    [ListDrawerSettings(ShowIndexLabels = true)]
    public List<EnemyDropEntry> Drops = new List<EnemyDropEntry>();

    [BoxGroup("Rewards")]
    [ListDrawerSettings(ShowIndexLabels = true)]
    [Tooltip("Legacy guaranteed drops. Used only when Drops is empty.")]
    public List<string> DropItemIDs = new List<string>();

    [BoxGroup("Rewards")]
    [HorizontalGroup("Rewards/R1", LabelWidth = 60)] public int EXPReward  = 20;
    [HorizontalGroup("Rewards/R1", LabelWidth = 60)] public int GoldReward = 10;
}
