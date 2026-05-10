using System.Collections.Generic;
using UnityEngine;
using Sirenix.OdinInspector;

[CreateAssetMenu(fileName = "NewEnemyData", menuName = "HubToHome/EnemyData")]
public class EnemyData : SerializedScriptableObject 
{
    [BoxGroup("Identity"), HideLabel, PreviewField(60, ObjectFieldAlignment.Left)]
    public Sprite Portrait;
    
    [BoxGroup("Identity")] public string EnemyName = "Enemy";

    [BoxGroup("Audio")]
    [Tooltip("이 적과 전투 시작 시 우선 재생할 전투 BGM입니다. 비워두면 맵 기본 전투 BGM을 사용합니다.")]
    public AudioClip BattleBGM;

    [BoxGroup("Battle Presentation")]
    [Required, AssetsOnly]
    [Tooltip("BattleScene 또는 심리스 전투에서 실제로 생성할 전투용 적 프리팹입니다. 비워두면 BattleManager의 기본 Enemy Base Prefab을 사용합니다.")]
    public GameObject BattlePrefab;

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
    [InfoBox("적 스킬 패턴을 사용하려면 SkillList에 1개 이상 넣고 SkillUseChance를 조절하세요.")]
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