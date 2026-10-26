using System.Collections;
using Sirenix.OdinInspector;
using UnityEngine;

/// <summary>이전 스킬 데이터의 참조를 보존하는 호환 블록입니다. 공격 전 정지 구도는 실행하지 않습니다.</summary>
[System.Serializable]
[TypeInfoBox("구형 준비 구도 호환용입니다. 현재는 기다리거나 카메라를 조작하지 않고 바로 다음 공격으로 진행합니다. 전조와 판정은 방어 대응 블록이 실제 이동·공격 중에 실행합니다.")]
public sealed class Action_EnemyWindup : SkillActionBlock
{
    [FoldoutGroup("구형 설정 · 사용 안 함"), ReadOnly, LabelText("클로즈업 줌 비율"), Range(0.5f, 1f)] public float ZoomRatio = 0.65f;
    [FoldoutGroup("구형 설정 · 사용 안 함"), ReadOnly, LabelText("확대 시간 (초)"), MinValue(0.05f)] public float FocusDuration = 0.16f;
    [FoldoutGroup("구형 설정 · 사용 안 함"), ReadOnly, LabelText("준비 자세 유지 (초)"), MinValue(0f)] public float HoldDuration = 0.25f;
    [FoldoutGroup("구형 설정 · 사용 안 함"), ReadOnly, LabelText("공격 구도 복귀 시간 (초)"), MinValue(0.05f)] public float ReleaseDuration = 0.14f;

    public bool HasValidSettings => Finite(ZoomRatio) && ZoomRatio >= 0.5f && ZoomRatio <= 1f
        && Finite(FocusDuration) && FocusDuration >= 0.05f
        && Finite(HoldDuration) && HoldDuration >= 0f
        && Finite(ReleaseDuration) && ReleaseDuration >= 0.05f;

    private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

    public override SkillActionAuthoringTiming GetAuthoringTiming()
        => SkillActionAuthoringTiming.Fixed("구형 공격 준비 · 사용 안 함", 0f);

    public override IEnumerator Execute(SkillContext context)
    {
        // 필드 이름/형식은 SerializeReference 에셋 호환용으로 남깁니다.
        // 구형 준비 수치가 남아 있어도 공격 흐름을 지연하거나 중단하지 않습니다.
        yield break;
    }
}
