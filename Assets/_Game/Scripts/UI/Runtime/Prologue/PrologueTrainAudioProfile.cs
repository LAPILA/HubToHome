using Sirenix.OdinInspector;
using UnityEngine;

/// <summary>열차 프롤로그에서만 사용하는 음원과 기본 음량입니다.</summary>
[CreateAssetMenu(fileName = "Prologue_TrainAudio", menuName = "Hub To Home/연출/열차 프롤로그 음향")]
public sealed class PrologueTrainAudioProfile : ScriptableObject
{
    [TitleGroup("열차 주행"), LabelText("주행 반복음")] public AudioClip TrainLoop;
    [TitleGroup("열차 주행"), LabelText("음량"), Range(0f, 1f)] public float TrainVolume = 0.85f;
    [TitleGroup("열차 주행"), LabelText("시작 페이드"), Min(0f)] public float TrainFadeIn = 0.6f;
    [TitleGroup("열차 주행"), LabelText("후미 진입 페이드"), Min(0f)] public float TrainFadeOut = 1.8f;

    [TitleGroup("정전"), LabelText("파워다운")] public AudioClip PowerDown;
    [TitleGroup("정전"), LabelText("음량"), Range(0f, 1f)] public float PowerDownVolume = 0.22f;

    [TitleGroup("후미 걷기"), LabelText("연속 발걸음")] public AudioClip Footsteps;
    [TitleGroup("후미 걷기"), LabelText("음량"), Range(0f, 1f)] public float FootstepsVolume = 0.35f;
    [TitleGroup("후미 걷기"), LabelText("끝 페이드"), Min(0f)] public float FootstepsFadeOut = 0.12f;
}
