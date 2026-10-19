using Sirenix.OdinInspector;
using UnityEngine;

/// <summary>저스트가드 배경 파장의 공유 설정. 재생 중 값은 별도 스냅샷에 보관합니다.</summary>
[CreateAssetMenu(menuName = "Hub To Home/연출/저스트가드 배경 파장", fileName = "ParryBackgroundRipple")]
public sealed class ParryBackgroundRippleProfile : ScriptableObject
{
    [Title("저스트가드 배경 파장")]
    [InfoBox("Background 정렬 레이어만 약하게 왜곡합니다. 크기 단위는 640×480 기준 픽셀입니다.")]
    [LabelText("재생 시간 (초)"), Range(.01f, 2f)] public float Duration = .30f;
    [LabelText("시작 반경"), Range(0f, 640f)] public float StartRadiusPixels = 16f;
    [LabelText("마지막 반경"), Range(0f, 640f)] public float EndRadiusPixels = 110f;
    [LabelText("고리 폭"), Range(.1f, 128f)] public float BandWidthPixels = 14f;
    [LabelText("일렁임 강도"), Range(0f, 10f)] public float DisplacementPixels = 1.25f;
    [LabelText("고리 색상"), ColorUsage(true, false)] public Color RingColor = new Color(.75f, .92f, 1f, .04f);

    [ShowInInspector, ReadOnly, LabelText("설정 유효")]
    public bool HasValidSettings => InRange(Duration, .01f, 2f)
        && InRange(StartRadiusPixels, 0f, 640f)
        && InRange(EndRadiusPixels, 0f, 640f) && EndRadiusPixels > StartRadiusPixels
        && InRange(BandWidthPixels, .1f, 128f)
        && InRange(DisplacementPixels, 0f, 10f)
        && InRange(RingColor.r, 0f, 1f) && InRange(RingColor.g, 0f, 1f)
        && InRange(RingColor.b, 0f, 1f) && InRange(RingColor.a, 0f, 1f);

    private static bool InRange(float value, float min, float max)
        => !float.IsNaN(value) && !float.IsInfinity(value) && value >= min && value <= max;
}
