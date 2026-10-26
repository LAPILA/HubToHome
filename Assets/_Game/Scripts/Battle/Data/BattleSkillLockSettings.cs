using System;
using Sirenix.OdinInspector;
using UnityEngine;

/// <summary>특정 전투 플래그에서 시작하는 선택형 스킬 잠금. 저장 데이터가 아닙니다.</summary>
[Serializable]
public sealed class BattleSkillLockSettings
{
    [LabelText("활성화 전투 플래그"), Tooltip("이 플래그가 true일 때 즉시 추첨하고 전투 동안 반복합니다.")]
    public string ActivationFlag;
    [LabelText("잠글 스킬 수"), Min(1)] public int SkillCount = 3;
    [LabelText("재추첨까지 아군 행동 수"), Min(1)] public int RefreshAfterActions = 2;
}
