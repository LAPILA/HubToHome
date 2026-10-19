using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

/// <summary>삽화와 기존 대화 데이터를 순서대로 보여 주는 표시 전용 데이터입니다.</summary>
[CreateAssetMenu(fileName = "IllustratedStory", menuName = "Hub To Home/연출/삽화 이야기")]
public sealed class IllustratedStoryAsset : ScriptableObject
{
    [LabelText("내레이션 초당 글자 수"), Min(0.01f), Tooltip("설정 메뉴의 글자 속도와 분리된 삽화 본문의 고정 표시 속도입니다.")]
    public float NarrationCharactersPerSecond = 10f;

    [LabelText("페이지"), ListDrawerSettings(ShowIndexLabels = true, ShowFoldout = false)]
    public List<IllustratedStoryPage> Pages = new List<IllustratedStoryPage>();

    public bool TryValidate(out string error)
    {
        if (Pages == null || Pages.Count == 0)
        {
            error = "illustrated_story_pages_missing";
            return false;
        }
        if (!IsNonNegativeFinite(NarrationCharactersPerSecond) || NarrationCharactersPerSecond <= 0f)
        {
            error = "illustrated_story_narration_speed_invalid";
            return false;
        }

        for (int i = 0; i < Pages.Count; i++)
        {
            IllustratedStoryPage page = Pages[i];
            if (page == null || (page.Picture == null && page.Animation == null && !page.IsCaptionPage))
            {
                error = "illustrated_story_art_missing: " + i;
                return false;
            }
            if (page.PixelScale < 1 || (page.Animation != null
                && (page.CanvasSize.x <= 0 || page.CanvasSize.y <= 0
                    || !IsNonNegativeFinite(page.AnimationDuration) || page.AnimationDuration <= 0f)))
            {
                error = "illustrated_story_art_size_invalid: " + i;
                return false;
            }
            if (page.Animation != null && !TryValidateFrames(page, i, out error)) return false;
            if (!IsNonNegativeFinite(page.MinimumDuration) || !IsNonNegativeFinite(page.FadeInDuration)
                || !IsNonNegativeFinite(page.FadeOutDuration))
            {
                error = "illustrated_story_duration_invalid: " + i;
                return false;
            }
            if (!IsNonNegativeFinite(page.CaptionCharactersPerSecond) || page.CaptionCharactersPerSecond <= 0f
                || !IsNonNegativeFinite(page.CaptionHoldDuration))
            {
                error = "illustrated_story_caption_timing_invalid: " + i;
                return false;
            }
        }

        error = null;
        return true;
    }

    private static bool IsNonNegativeFinite(float value)
        => value >= 0f && !float.IsNaN(value) && !float.IsInfinity(value);

    private static bool TryValidateFrames(IllustratedStoryPage page, int pageIndex, out string error)
    {
        IllustratedStoryFrame[] frames = page.Frames;
        if (frames == null || frames.Length == 0)
        {
            error = "illustrated_story_frames_missing: " + pageIndex;
            return false;
        }

        float previousTime = -1f;
        for (int i = 0; i < frames.Length; i++)
        {
            IllustratedStoryFrame frame = frames[i];
            if (frame == null || frame.Sprite == null || !IsNonNegativeFinite(frame.Time)
                || frame.Time >= page.AnimationDuration || frame.Time <= previousTime
                || (i == 0 && frame.Time != 0f))
            {
                error = "illustrated_story_frame_invalid: " + pageIndex + "/" + i;
                return false;
            }
            previousTime = frame.Time;
        }

        error = null;
        return true;
    }

#if UNITY_EDITOR
    [NonSerialized] private bool _frameRefreshQueued;
    [NonSerialized] private bool _bakingFrames;

    [Button("원본 클립에서 UI 프레임 갱신")]
    public void BakeAnimationFrames()
    {
        if (UnityEditor.EditorApplication.isPlayingOrWillChangePlaymode || _bakingFrames) return;
        RefreshAnimationFrames(onlyChanged: false, recordUndo: true, save: true);
    }

    private void OnValidate()
    {
        if (_bakingFrames || _frameRefreshQueued) return;
        // OnValidate can run while an importer is loading. Read editor curves on the next editor tick.
        _frameRefreshQueued = true;
        UnityEditor.EditorApplication.delayCall += RefreshChangedAnimationFrames;
    }

    private void OnDisable()
    {
        UnityEditor.EditorApplication.delayCall -= RefreshChangedAnimationFrames;
        _frameRefreshQueued = false;
    }

    private void RefreshChangedAnimationFrames()
    {
        _frameRefreshQueued = false;
        if (this == null || _bakingFrames || UnityEditor.EditorApplication.isPlayingOrWillChangePlaymode) return;
        RefreshAnimationFrames(onlyChanged: true, recordUndo: false, save: false);
    }

    private void RefreshAnimationFrames(bool onlyChanged, bool recordUndo, bool save)
    {
        if (Pages == null || _bakingFrames) return;
        _bakingFrames = true;
        try
        {
            var baked = new IllustratedStoryFrame[Pages.Count][];
            bool changed = false;
            for (int i = 0; i < Pages.Count; i++)
            {
                IllustratedStoryPage page = Pages[i];
                if (page == null || (onlyChanged && !page.NeedsAnimationFrameRefresh)) continue;
                if (!TryBakeFrames(page.Animation, out baked[i], out string error))
                {
                    Debug.LogError("[IllustratedStoryAsset] " + error + " Page=" + i, this);
                    return;
                }
                changed = true;
            }
            if (!changed) return;

            // A malformed source must not leave some earlier pages updated and others stale.
            if (recordUndo) UnityEditor.Undo.RecordObject(this, "Bake illustrated story frames");
            for (int i = 0; i < Pages.Count; i++)
                if (baked[i] != null) Pages[i].SetAnimationFrames(baked[i]);
            UnityEditor.EditorUtility.SetDirty(this);
            if (save) UnityEditor.AssetDatabase.SaveAssetIfDirty(this);
        }
        finally { _bakingFrames = false; }
    }

    private static bool TryBakeFrames(AnimationClip clip, out IllustratedStoryFrame[] frames, out string error)
    {
        frames = Array.Empty<IllustratedStoryFrame>();
        error = null;
        if (clip == null) return true;
        float duration = clip.length;
        if (!IsNonNegativeFinite(duration) || duration <= 0f)
        {
            error = "illustrated_story_clip_duration_invalid: " + clip.name;
            return false;
        }

        UnityEditor.ObjectReferenceKeyframe[] keys = null;
        bool foundBinding = false;
        foreach (UnityEditor.EditorCurveBinding binding in UnityEditor.AnimationUtility.GetObjectReferenceCurveBindings(clip))
        {
            if (binding.type != typeof(SpriteRenderer) || binding.propertyName != "m_Sprite") continue;
            if (foundBinding)
            {
                error = "illustrated_story_multiple_sprite_bindings: " + clip.name;
                return false;
            }
            foundBinding = true;
            keys = UnityEditor.AnimationUtility.GetObjectReferenceCurve(clip, binding);
        }
        if (keys == null || keys.Length == 0 || keys[0].time != 0f)
        {
            error = "illustrated_story_clip_frames_missing: " + clip.name;
            return false;
        }

        var result = new List<IllustratedStoryFrame>(keys.Length);
        float previousKeyTime = -1f;
        for (int i = 0; i < keys.Length; i++)
        {
            float time = keys[i].time;
            Sprite sprite = keys[i].value as Sprite;
            if (!IsNonNegativeFinite(time) || time < previousKeyTime || sprite == null
                || (time > duration && !Mathf.Approximately(time, duration)))
            {
                error = "illustrated_story_clip_frame_invalid: " + clip.name + "/" + i;
                return false;
            }
            previousKeyTime = time;
            // Aseprite's terminal hold is not a new frame. Duration remains the source clip's length.
            if (time >= duration) continue;
            int last = result.Count - 1;
            if (last >= 0 && result[last].Time == time)
                result[last].Sprite = sprite;
            else if (last < 0 || result[last].Sprite != sprite)
                result.Add(new IllustratedStoryFrame { Sprite = sprite, Time = time });
        }
        if (result.Count == 0)
        {
            error = "illustrated_story_clip_frames_missing: " + clip.name;
            return false;
        }
        frames = result.ToArray();
        return true;
    }
#endif
}

[Serializable]
public sealed class IllustratedStoryPage
{
    [LabelText("정지 그림"), PreviewField(72)] public Sprite Picture;
    [LabelText("스프라이트 애니메이션"), Tooltip("설정하면 정지 그림 대신 재생합니다. Aseprite의 SpriteRenderer 클립을 사용합니다.")]
    public AnimationClip Animation;
    [LabelText("UI 프레임 캐시"), ReadOnly]
    public IllustratedStoryFrame[] Frames = Array.Empty<IllustratedStoryFrame>();
    [LabelText("텍스트 전용 페이지"), TextArea(2, 4), Tooltip("그림과 애니메이션이 없으면 화면 중앙에 이 문구만 표시합니다.")]
    public string Caption;
    [LabelText("텍스트 번역 키")] public string CaptionLocalizationKey;
    [LabelText("중앙 자막 초당 글자 수"), Min(0.01f), Tooltip("설정 메뉴의 글자 속도와 무관하게 한 글자씩 표시합니다.")]
    public float CaptionCharactersPerSecond = 4f;
    [LabelText("중앙 자막 완성 후 유지 시간"), Min(0f), Tooltip("전체 문구가 나타난 뒤 사라지기 전까지 유지할 시간입니다.")]
    public float CaptionHoldDuration = 1.2f;
    [LabelText("애니메이션 원본 캔버스"), Tooltip("Aseprite 원본 캔버스 크기입니다. 캔버스 밖으로 움직이는 프레임도 이 영역에서 잘라 표시합니다.")]
    public Vector2Int CanvasSize = new Vector2Int(128, 96);
    [LabelText("픽셀 확대 배수"), Min(1)] public int PixelScale = 3;
    [LabelText("전체 화면"), Tooltip("켜면 원본 비율을 유지해 640×480 화면을 채웁니다. 원본 캔버스와 프레임별 pivot은 유지합니다.")]
    public bool FullScreen;
    [LabelText("애니메이션 반복")] public bool LoopAnimation;
    [LabelText("최소 표시 시간"), Min(0f)] public float MinimumDuration = 0.2f;
    [LabelText("나타나는 시간"), Min(0f)] public float FadeInDuration = 0.3f;
    [LabelText("사라지는 시간"), Min(0f)] public float FadeOutDuration = 0.3f;
    [LabelText("대화 데이터")] public DialogueData Dialogue;
    [LabelText("글자 소리 끄기"), Tooltip("이 페이지의 타이핑 효과음만 끕니다. 다음 페이지와 일반 대화의 소리는 변경하지 않습니다.")]
    public bool MuteTypingSound;
    [LabelText("대사가 없으면 확인 입력 대기"), Tooltip("마지막 정지 그림처럼 Z 또는 패드 확인을 누를 때까지 유지합니다.")]
    public bool WaitForConfirmWithoutDialogue;

    public bool IsCaptionPage => Picture == null && Animation == null && !string.IsNullOrWhiteSpace(Caption);
    public float AnimationDuration => Animation != null ? Animation.length : 0f;

    public Sprite ResolveAnimationSprite(float elapsed)
    {
        if (Frames == null || Frames.Length == 0 || Frames[0] == null) return null;
        float duration = AnimationDuration;
        if (duration <= 0f || float.IsNaN(duration) || float.IsInfinity(duration)) return null;
        if (float.IsNaN(elapsed) || elapsed < 0f) elapsed = 0f;
        if (float.IsInfinity(elapsed)) elapsed = LoopAnimation ? 0f : duration;
        float position = LoopAnimation ? Mathf.Repeat(elapsed, duration) : Mathf.Min(elapsed, duration);
        Sprite selected = Frames[0].Sprite;
        for (int i = 1; i < Frames.Length; i++)
        {
            IllustratedStoryFrame frame = Frames[i];
            if (frame == null) return null;
            // Treat only float-rounding differences as the same key boundary; do not add a wide early window.
            if (frame.Time > position && !Mathf.Approximately(frame.Time, position)) break;
            selected = frame.Sprite;
        }
        return selected;
    }

#if UNITY_EDITOR
    [SerializeField, HideInInspector] private AnimationClip _framesSourceClip;
    internal bool NeedsAnimationFrameRefresh => _framesSourceClip != Animation
        || (Animation != null && (Frames == null || Frames.Length == 0));
    internal void SetAnimationFrames(IllustratedStoryFrame[] frames)
    {
        Frames = frames;
        _framesSourceClip = Animation;
    }
#endif
}

[Serializable]
public sealed class IllustratedStoryFrame
{
    [LabelText("스프라이트"), PreviewField(32)] public Sprite Sprite;
    [LabelText("시작 시간"), Min(0f)] public float Time;
}
