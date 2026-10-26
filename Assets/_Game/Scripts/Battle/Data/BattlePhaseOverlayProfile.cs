using Sirenix.OdinInspector;
using UnityEngine;

[System.Flags]
public enum BattlePhaseOverlayMirror { None = 0, Horizontal = 1, Vertical = 2, Both = 3 }

public enum BattlePhaseOverlayPlacement
{
    [LabelText("전투 영역")] PlayfieldCorners = 0,
    [LabelText("전체 화면")] ScreenCorners = 1
}

/// <summary>전투 수명 scope가 읽는 테두리 표시 설정입니다. 플래그 판단과 전투 규칙은 소유하지 않습니다.</summary>
[CreateAssetMenu(menuName = "Hub To Home/연출/전투 페이즈 테두리", fileName = "BattlePhaseOverlay")]
public sealed class BattlePhaseOverlayProfile : ScriptableObject
{
    [Title("전투 페이즈 테두리")]
    [InfoBox("지정 플래그가 켜진 뒤 사슬을 네 모서리에 표시합니다. 640×480 기준입니다. 전체 화면은 모서리를 가로지르는 방향이며 정렬 순서 -100보다 크면 전투 HUD 위에 표시합니다.")]
    [LabelText("표시 플래그")] public string ActivationFlag = string.Empty;
    [LabelText("반복 그림"), PreviewField(64), AssetsOnly, Required] public Sprite ChainSprite;
    [LabelText("그림 색상")] public Color Tint = new Color(1f, 1f, 1f, .65f);
    [LabelText("배치 기준"), EnumToggleButtons] public BattlePhaseOverlayPlacement Placement;
    [LabelText("표시 정렬 순서"), Range(-200, 0)] public int CanvasSortingOrder = -200;
    [LabelText("그림 배율"), Range(1, 4)] public int SpriteScale = 1;
    [LabelText("두께 (픽셀)"), Range(4f, 32f)] public float WidthPixels = 16f;
    [LabelText("길이 (픽셀)"), Range(64f, 256f)] public float LengthPixels = 128f;
    [LabelText("모서리에서 중심까지 거리"), Range(0f, 160f)] public float InsetPixels = 18f;
    [LabelText("이동 속도 (픽셀/초)"), Range(-96f, 96f)] public float ScrollPixelsPerSecond = 24f;
    [FoldoutGroup("모서리별 반전"), LabelText("좌하"), EnumToggleButtons] public BattlePhaseOverlayMirror BottomLeftMirror;
    [FoldoutGroup("모서리별 반전"), LabelText("좌상"), EnumToggleButtons] public BattlePhaseOverlayMirror TopLeftMirror;
    [FoldoutGroup("모서리별 반전"), LabelText("우상"), EnumToggleButtons] public BattlePhaseOverlayMirror TopRightMirror;
    [FoldoutGroup("모서리별 반전"), LabelText("우하"), EnumToggleButtons] public BattlePhaseOverlayMirror BottomRightMirror;
    [LabelText("시작 효과음"), AssetsOnly] public AudioClip StartSfx;
    [LabelText("효과음 크기"), Range(0f, 1f)] public float StartVolume = .18f;
    [LabelText("효과음 감쇠 (초)"), Range(.05f, 5f)] public float SoundFadeSeconds = 1.2f;

    [ShowInInspector, ReadOnly, LabelText("설정 유효")]
    public bool HasValidSettings => !string.IsNullOrWhiteSpace(ActivationFlag) && ChainSprite != null
        && (Placement == BattlePhaseOverlayPlacement.PlayfieldCorners || Placement == BattlePhaseOverlayPlacement.ScreenCorners)
        && CanvasSortingOrder >= -200 && CanvasSortingOrder <= 0
        && SpriteScale >= 1 && SpriteScale <= 4
        && InRange(WidthPixels, 4f, 32f) && InRange(LengthPixels, 64f, 256f)
        && InRange(InsetPixels, 0f, 160f) && InRange(ScrollPixelsPerSecond, -96f, 96f)
        && InRange(StartVolume, 0f, 1f) && InRange(SoundFadeSeconds, .05f, 5f)
        && ValidMirror(BottomLeftMirror) && ValidMirror(TopLeftMirror)
        && ValidMirror(TopRightMirror) && ValidMirror(BottomRightMirror)
        && InRange(Tint.r, 0f, 1f) && InRange(Tint.g, 0f, 1f)
        && InRange(Tint.b, 0f, 1f) && InRange(Tint.a, 0f, 1f);

    private static bool InRange(float value, float min, float max)
        => !float.IsNaN(value) && !float.IsInfinity(value) && value >= min && value <= max;

    public BattlePhaseOverlayMirror GetCornerMirror(int index) => index switch
    {
        0 => BottomLeftMirror,
        1 => TopLeftMirror,
        2 => TopRightMirror,
        3 => BottomRightMirror,
        _ => BattlePhaseOverlayMirror.None
    };

    private static bool ValidMirror(BattlePhaseOverlayMirror value) => (int)value >= 0 && (int)value <= 3;
}
