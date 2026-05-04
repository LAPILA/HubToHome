using System.Collections.Generic;
using UnityEngine;
using Sirenix.OdinInspector;

[CreateAssetMenu(fileName = "NewEnemyData", menuName = "HubToHome/EnemyData")]
public class EnemyData : SerializedScriptableObject // Odin Serialize 지원
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

    [BoxGroup("Rewards")]
    [ListDrawerSettings(ShowIndexLabels = true)] // 배열 대신 리스트 사용 권장
    public List<string> DropItemIDs = new List<string>();

    [BoxGroup("Rewards")]
    [HorizontalGroup("Rewards/R1", LabelWidth = 60)] public int EXPReward  = 20;
    [HorizontalGroup("Rewards/R1", LabelWidth = 60)] public int GoldReward = 10;
}