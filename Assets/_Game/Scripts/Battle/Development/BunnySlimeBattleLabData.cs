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
    [LabelText("목록에서 임시 숨김"), Tooltip("전투 데이터는 유지하고 실험실 선택 목록에서만 제외합니다.")]
    public bool HiddenInMenu;
    [LabelText("적 데이터")] public EnemyData Enemy;
    [LabelText("전투 시나리오")] public BattleScenarioData Scenario;
    [LabelText("페이즈 UI (선택)")] public BattlePhaseOverlayProfile PhaseOverlay;
    [LabelText("반복 스킬 잠금 (선택)")] public BattleSkillLockSettings SkillLock;
    // 이전 후열 실습 자산의 직렬화 호환용입니다. 현재 실험실에서는 사용하지 않습니다.
    [HideInInspector] public bool WeakenFrontLine;
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
    [LabelText("시작 AP"), Min(-1)]
    [Tooltip("-1이면 최대치로 시작합니다. 0 이상이면 장비를 반영한 최대치 안에서 이 값으로 시작합니다. 본편 저장에는 적용하지 않습니다.")]
    public int InitialPartyAP = -1;
    [HideInInspector] public SkillData GuardSkill;
    [HideInInspector] public SkillData DodgeSkill;
    [HideInInspector] public SkillData CounterSkill;
    [HideInInspector] public SkillData WaveSkill;
    [HideInInspector] public SkillData[] ProjectileSkills;

    /// <summary>실험실은 위젤 한 명만 사용합니다. 기존 bool 호출도 후열을 생성하지 않습니다.</summary>
    public int GetPartyCount(bool waveExercise = false) => Party == null || Party.Length == 0 ? 0 : 1;

    public int ResolveInitialPartyAP(int maximum)
        => InitialPartyAP < 0 ? Mathf.Max(0, maximum) : Mathf.Clamp(InitialPartyAP, 0, Mathf.Max(0, maximum));

    /// <summary>표시 순서와 원래 조우 인덱스를 분리합니다. 숨긴 데이터는 삭제하거나 재정렬하지 않습니다.</summary>
    public int[] GetVisibleEncounterIndices()
    {
        if (Encounters == null || Encounters.Length == 0) return Array.Empty<int>();
        int count = 0;
        for (int i = 0; i < Encounters.Length; i++)
            if (Encounters[i] != null && !Encounters[i].HiddenInMenu) count++;
        if (count == 0) return Array.Empty<int>();
        var indices = new int[count];
        for (int i = 0, visible = 0; i < Encounters.Length; i++)
            if (Encounters[i] != null && !Encounters[i].HiddenInMenu) indices[visible++] = i;
        return indices;
    }
}
