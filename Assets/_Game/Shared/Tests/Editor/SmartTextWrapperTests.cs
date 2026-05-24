using NUnit.Framework;

public class SmartTextWrapperTests
{
    [Test]
    public void WrapMovesWholeTokenToNextLineWhenCandidateExceedsWidth()
    {
        string wrapped = SmartTextWrapper.Wrap(
            "지금부터 A 공격을 사용할거야.",
            maxWidth: 12f,
            MeasureByCharacters);

        Assert.That(wrapped, Is.EqualTo("지금부터 A 공격을\n사용할거야."));
    }

    [Test]
    public void WrapPreservesExplicitLineBreaks()
    {
        string wrapped = SmartTextWrapper.Wrap(
            "첫번째 줄\n지금부터 A 공격을 사용할거야.",
            maxWidth: 12f,
            MeasureByCharacters);

        Assert.That(wrapped, Is.EqualTo("첫번째 줄\n지금부터 A 공격을\n사용할거야."));
    }

    [Test]
    public void WrapFallsBackToCharacterUnitsForLongUnspacedToken()
    {
        string wrapped = SmartTextWrapper.Wrap(
            "엄청긴공격이름입니다",
            maxWidth: 5f,
            MeasureByCharacters);

        Assert.That(wrapped, Is.EqualTo("엄청긴공격\n이름입니다"));
    }

    [Test]
    public void WrapMeasuresWholeCandidateLineInsteadOfAddingTokenWidths()
    {
        string wrapped = SmartTextWrapper.Wrap(
            "AA BB",
            maxWidth: 6f,
            text => text.Contains(" ") ? 10f : text.Length);

        Assert.That(wrapped, Is.EqualTo("AA\nBB"));
    }

    [Test]
    public void WrapReusesMeasurementsForRepeatedTokens()
    {
        int calls = 0;
        SmartTextWrapper.Wrap(
            "공격 공격 공격 공격",
            maxWidth: 5f,
            text =>
            {
                calls++;
                return MeasureByCharacters(text);
            });

        Assert.That(calls, Is.LessThanOrEqualTo(3));
    }

    private static float MeasureByCharacters(string text)
    {
        return string.IsNullOrEmpty(text) ? 0f : text.Length;
    }
}
