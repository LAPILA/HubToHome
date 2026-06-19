using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 대화 진행 중 선택지 전투에 사용할 런타임 전투 컨텍스트.
/// DialogueData 에셋 자체를 런타임에 변형하지 않기 위해 사용합니다.
/// </summary>
public class DialogueEncounterContext
{
    public List<EnemyData> EncounterEnemies = new List<EnemyData>();
    public AudioClip OverrideBattleBGM;
    public BattleScenarioData BattleScenarioData;
    public bool UseDedicatedBattleScene;
    public string BattleSceneName = "BattleScene";
    public float BattleSceneFadeDuration = 0.08f;
    public string EncounterIdOverride;
    public bool DefeatEnemyOnVictory;
}
