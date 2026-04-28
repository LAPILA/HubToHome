using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 적 캐릭터 데이터 ScriptableObject.
/// 에디터에서 Create > HubToHome > EnemyData 로 생성하세요.
/// </summary>
[CreateAssetMenu(fileName = "NewEnemyData", menuName = "HubToHome/EnemyData")]
public class EnemyData : ScriptableObject
{
    [Header("Identity")]
    public string EnemyName     = "Enemy";
    public Sprite Portrait;

    [Header("Base Stats")]
    public int MaxHP  = 50;
    public int ATK    = 8;
    public int DEF    = 3;
    public int SPD    = 8;

    [Header("AI Pattern")]
    [Range(0f, 1f)]
    public float SkillUseChance     = 0.3f;
    public bool  HasEnragedPattern  = false;
    public bool  IsLargeEnemy       = false; // 대형 적: 중앙 이동 없이 제자리 공격

    [Header("Skills")]
    public List<SkillData> SkillList;

    [Header("QTE")]
    [Range(0f, 2f)]
    public float QTEDifficultyMultiplier = 1f;

    [Header("Drops")]
    public string[] DropItemIDs;
    public int      EXPReward = 20;
    public int      GoldReward = 10;
}
