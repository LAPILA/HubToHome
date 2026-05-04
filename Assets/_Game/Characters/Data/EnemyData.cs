using System.Collections.Generic;
using UnityEngine;
using Sirenix.OdinInspector;

/// <summary>
/// 적 캐릭터 데이터 ScriptableObject.
/// 에디터에서 Create > HubToHome > EnemyData 로 생성하세요.
/// </summary>
[CreateAssetMenu(fileName = "NewEnemyData", menuName = "HubToHome/EnemyData")]
public class EnemyData : SerializedScriptableObject
{
    // ── 기본 정보 ─────────────────────────────────────────────
    [BoxGroup("Identity"), LabelWidth(100)]
    public string EnemyName = "Enemy";

    [BoxGroup("Identity"), PreviewField(60, ObjectFieldAlignment.Left)]
    public Sprite Portrait;

    // ── 스탯 ──────────────────────────────────────────────────
    [BoxGroup("Base Stats"), LabelWidth(60)]
    [HorizontalGroup("Base Stats/Row1")]
    public int MaxHP = 100;

    [HorizontalGroup("Base Stats/Row1"), LabelWidth(30)]
    public int ATK = 8;

    [HorizontalGroup("Base Stats/Row2"), LabelWidth(30)]
    public int DEF = 3;

    [HorizontalGroup("Base Stats/Row2"), LabelWidth(30)]
    public int SPD = 8;

    // ── AI 패턴 ───────────────────────────────────────────────
    [FoldoutGroup("AI Pattern")]
    [Range(0f, 1f), LabelText("스킬 사용 확률")]
    public float SkillUseChance = 0.3f;

    [FoldoutGroup("AI Pattern"), LabelText("분노 패턴 보유")]
    public bool HasEnragedPattern = false;

    [FoldoutGroup("AI Pattern"), LabelText("대형 적 (제자리 공격)")]
    [InfoBox("대형 적은 중앙으로 이동하지 않고 제자리에서 공격합니다.", InfoMessageType.Info, "IsLargeEnemy")]
    public bool IsLargeEnemy = false;

    // ── 스킬 ──────────────────────────────────────────────────
    [FoldoutGroup("Skills")]
    [ListDrawerSettings(ShowIndexLabels = true, DraggableItems = true)]
    public List<SkillData> SkillList = new List<SkillData>();

    // ── QTE ───────────────────────────────────────────────────
    [BoxGroup("QTE")]
    [Range(0f, 2f), LabelText("QTE 난이도 배율")]
    [InfoBox("1.0 = 기본 / 2.0 = 판정 구간 절반으로 좁아짐")]
    public float QTEDifficultyMultiplier = 1f;

    // ── 드롭 / 보상 ───────────────────────────────────────────
    [FoldoutGroup("Drops & Rewards")]
    [LabelText("드롭 아이템 ID 목록")]
    public string[] DropItemIDs = new string[0];

    [FoldoutGroup("Drops & Rewards"), HorizontalGroup("Drops & Rewards/Reward")]
    [LabelWidth(60)]
    public int EXPReward  = 20;

    [HorizontalGroup("Drops & Rewards/Reward"), LabelWidth(60)]
    public int GoldReward = 10;
}
