using Sirenix.OdinInspector;
using UnityEngine;

public partial class Action_DefenseWindow
{
    // 난무처럼 이동 속도가 판정 시각을 소유할 때만 true. 일반 공격도 연속 이동하되 TimeWindow를 보존합니다.
    [System.NonSerialized] public bool ContinuousMeleeMotion;

    [BoxGroup("근접 돌파 연출"), LabelText("공중 접근 높이"), MinValue(0f)]
    public float MeleeApproachHeight;

    [HideInInspector] // 저장 호환용. 가드 성공에도 이동은 계속하며 피해/보상 규칙만 달라집니다.
    public bool MeleePassThroughOnGuard;
}
