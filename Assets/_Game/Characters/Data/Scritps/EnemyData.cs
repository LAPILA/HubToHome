using System.Collections.Generic;
using UnityEngine;
using Sirenix.OdinInspector;

[System.Serializable]
public struct EnemyActCommand
{
    [HorizontalGroup("Act", 0.3f), HideLabel] 
    public string ActName; // 예: "말 걸기", "위협하기", "안아주기"

    [HorizontalGroup("Act", 0.2f), LabelText("Mercy증가")] 
    public float MercyAmount; // 이 행동을 했을 때 오르는 자비 수치 (0.0 ~ 1.0)

    [HorizontalGroup("Act", 0.5f), LabelText("대화 ID")] 
    public string ActDialogueID; // DialogueManager에서 호출할 대화 분기 ID
}

[CreateAssetMenu(fileName = "NewEnemyData", menuName = "HubToHome/EnemyData")]
public class EnemyData : SerializedScriptableObject 
{
    [BoxGroup("Identity"), HideLabel, PreviewField(60, ObjectFieldAlignment.Left)]
    public Sprite Portrait;
    
    [BoxGroup("Identity")] public string EnemyName = "Enemy";

    [BoxGroup("Base Stats")] 
    [HorizontalGroup("Base Stats/R1", LabelWidth = 40)] public int MaxHP = 100;
    [HorizontalGroup("Base Stats/R1", LabelWidth = 40)] public int ATK = 8;
    
    [BoxGroup("Base Stats")] 
    [HorizontalGroup("Base Stats/R2", LabelWidth = 40)] public int DEF = 3;
    [HorizontalGroup("Base Stats/R2", LabelWidth = 40)] public int SPD = 8;

    // ── 🚨 추가됨: 행동(ACT) 시스템 ──
    [BoxGroup("Deltarune Mercy System")]
    [InfoBox("이 적에게 취할 수 있는 '행동' 리스트입니다. 자비(Mercy)가 1.0(100%)이 되면 Spare가 가능해집니다.")]
    [ListDrawerSettings(ShowIndexLabels = true)]
    public List<EnemyActCommand> ActCommands = new List<EnemyActCommand>();

    [BoxGroup("AI & Pattern")]
    [Range(0f, 1f)] public float SkillUseChance = 0.3f;
    [BoxGroup("AI & Pattern")] public bool HasEnragedPattern = false;
    [BoxGroup("AI & Pattern")] 
    [InfoBox("대형 적은 중앙으로 이동하지 않고 제자리에서 공격합니다.", InfoMessageType.Info, "IsLargeEnemy")]
    public bool IsLargeEnemy = false;

    [BoxGroup("Combat Logic")]
    [ListDrawerSettings(ShowIndexLabels = true)]
    public List<SkillData> SkillList = new List<SkillData>();

    [BoxGroup("Combat Logic")]
    [Range(0.5f, 2f), InfoBox("1.0 = 기본 / 2.0 = 판정 구간 절반으로 좁아짐")]
    public float QTEDifficultyMultiplier = 1f;

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
    public List<string> DropItemIDs = new List<string>();

    [BoxGroup("Rewards")]
    [HorizontalGroup("Rewards/R1", LabelWidth = 60)] public int EXPReward  = 20;
    [HorizontalGroup("Rewards/R1", LabelWidth = 60)] public int GoldReward = 10;
}