using Febucci.TextAnimatorForUnity;

public static class DialogueTextAnimationPolicy
{
    public static void UsePlainTypewriter(TypewriterComponent typewriter)
    {
        if (typewriter == null)
            return;

        TextAnimatorComponentBase textAnimator = typewriter.TextAnimator;
        if (textAnimator != null)
            textAnimator.SetAppearancesActive(false);
    }
}