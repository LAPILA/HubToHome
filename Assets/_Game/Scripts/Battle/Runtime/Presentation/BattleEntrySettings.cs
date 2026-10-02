using System;
using Sirenix.OdinInspector;
using UnityEngine;

/// <summary>일반 심리스 조우의 화면 연출 값. 전투 규칙과 판정 시간은 변경하지 않습니다.</summary>
[Serializable]
public sealed class BattleEntrySettings
{
    [LabelText("빠른 진입 연출 사용")] public bool Enabled = true;
    [LabelText("경고 (초)"), MinValue(0f)] public float WarningDuration = 0.20f;
    [LabelText("줌 (초)"), MinValue(0f)] public float ZoomDuration = 0.50f;
    [LabelText("사선 절단 (초)"), MinValue(0f)] public float SlashDuration = 0.20f;
    [LabelText("암전 최소 유지 (초)"), MinValue(0f), Tooltip("전투 세팅과 동시에 진행하며, 준비가 끝난 뒤 남은 시간만 기다립니다.")]
    public float BlackDuration = 0.35f;
    [LabelText("상하 개방 (초)"), MinValue(0f)] public float RevealDuration = 0.55f;
    [LabelText("개방 시작 → UI 지연 (초)"), MinValue(0f)] public float HudDelay = 0.45f;
    [LabelText("UI 등장 (초)"), MinValue(0f)] public float HudDuration = 0.30f;
    [LabelText("줌 크기 비율"), Range(0.8f, 1f), Tooltip("0.9면 기존 화면보다 약 11% 확대합니다.")]
    public float ZoomRatio = 0.9f;
    [LabelText("UI 진입 거리 (기준 픽셀)"), Range(0f, 80f)] public float HudDistance = 24f;
    [LabelText("절단선 색")] public Color SlashColor = new Color(0.82f, 1f, 1f, 1f);
    [LabelText("경고 색")] public Color WarningColor = new Color(1f, 0.9f, 0.4f, 1f);
    [LabelText("경고 효과음 (선택)")] public AudioClip WarningSfx;
    [LabelText("절단 효과음 (선택)")] public AudioClip SlashSfx;

    public static float SafeDuration(float value) => FiniteClamp(value, 0f, 2f, 0f);
    public static float FiniteClamp(float value, float min, float max, float fallback)
        => float.IsNaN(value) || float.IsInfinity(value) ? fallback : Mathf.Clamp(value, min, max);
    public float NominalDuration => SafeDuration(WarningDuration) + SafeDuration(ZoomDuration)
        + SafeDuration(SlashDuration) + SafeDuration(BlackDuration)
        + Mathf.Max(SafeDuration(RevealDuration), SafeDuration(HudDelay) + SafeDuration(HudDuration));
}
