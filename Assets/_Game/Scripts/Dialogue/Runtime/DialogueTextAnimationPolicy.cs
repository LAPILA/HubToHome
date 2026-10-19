using System;
using Febucci.TextAnimatorCore.Settings;
using Febucci.TextAnimatorForUnity;

public static class DialogueTextAnimationPolicy
{
    /// <summary>
    /// 한 글자씩 표시하는 타이핑과 음성 이벤트는 유지하고 글자의 등장/퇴장 효과를 제거합니다.
    /// TA 3.x의 SetAppearancesActive는 localSettings만 바꾸므로 공유 설정을 사용하는 창에는
    /// 먼저 유효 설정의 개별 복사본을 적용해야 합니다. 원본 ScriptableObject는 수정하지 않습니다.
    /// </summary>
    public static void UsePlainTypewriter(TypewriterComponent typewriter)
    {
        if (typewriter == null || !typewriter.TryGetComponent(out TextAnimatorComponentBase textAnimator))
            return;

        AnimatorSettings source = textAnimator.Settings;
        if (textAnimator.sharedSettings == null && IsPlain(source)) return;

        source ??= new AnimatorSettings();
        textAnimator.localSettings = new AnimatorSettings
        {
            isResettingTimeOnNewText = source.isResettingTimeOnNewText,
            defaultEffectsMode = source.defaultEffectsMode,
            timeScale = source.timeScale,
            defaultBehaviorTags = source.defaultBehaviorTags != null
                ? (string[])source.defaultBehaviorTags.Clone() : Array.Empty<string>(),
            defaultAppearanceTags = Array.Empty<string>(),
            defaultDisappearanceTags = Array.Empty<string>(),
            isAnimatingBehaviors = source.isAnimatingBehaviors,
            isAnimatingAppearances = false,
            isAnimatingDisappearances = false,
            useDynamicScaling = source.useDynamicScaling,
            referenceFontSize = source.referenceFontSize
        };
        textAnimator.sharedSettings = null;
        // Existing parsed text can still contain a shared default 'incr' region. Refresh does not
        // disable the typewriter or remove its OnCharacterVisible/OnTextShowed subscriptions.
        textAnimator.ForceDatabaseRefresh();
    }

    private static bool IsPlain(AnimatorSettings settings)
        => settings != null && !settings.isAnimatingAppearances && !settings.isAnimatingDisappearances
            && (settings.defaultAppearanceTags == null || settings.defaultAppearanceTags.Length == 0)
            && (settings.defaultDisappearanceTags == null || settings.defaultDisappearanceTags.Length == 0);
}
