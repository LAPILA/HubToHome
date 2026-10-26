using NUnit.Framework;

public class ParryRearmTests
{
    [Test]
    public void FirstPressIsGenerousButRepeatedPressesKeepRearming()
    {
        var state = new ParryRearmState();
        state.ObservePress(1f);
        Assert.That(state.IsPressRearmed(1f, .18f), Is.True);
        for (int i = 1; i <= 20; i++)
        {
            float now = 1f + i * .05f;
            state.ObservePress(now);
            Assert.That(state.IsPressRearmed(now, .18f), Is.False);
            Assert.That(state.Remaining(now, .18f), Is.EqualTo(.18f).Within(.00001f));
        }
        state.ObservePress(2.18f);
        Assert.That(state.IsPressRearmed(2.18f, .18f), Is.True);
    }

    [Test]
    public void UpdateAndQteDuplicateDoNotBecomeTwoPresses()
    {
        var state = new ParryRearmState();
        state.ObservePress(1f);
        state.ObservePress(1f);
        state.ObservePress(.9f);
        Assert.That(state.IsPressRearmed(1f, .18f), Is.True);
        state.ObservePress(1.05f);
        state.ObservePress(1.05f);
        Assert.That(state.IsPressRearmed(1.05f, .18f), Is.False);
        state.Reset();
        state.ObservePress(1.06f);
        Assert.That(state.IsPressRearmed(1.06f, .18f), Is.True);
    }

    [TestCase(.1f, .05f)]
    [TestCase(.2f, .1f)]
    [TestCase(.5f, .18f)]
    public void FastAttacksClampRearmInterval(float duration, float expected)
        => Assert.That(ParryRearmState.IntervalFor(duration), Is.EqualTo(expected).Within(.00001f));

    [Test]
    public void SpamOutsideCueDoesNotTurnIntoAutomaticPerfectAtCueOpening()
    {
        var request = DefenseJudgementPolicy.WithBattleAssistance(
            new DefenseQteRequest(1f, 1f, DefenseRequirement.Any,
                new DefenseTimingProfile(.12f, .22f, .4f), useActiveDefense: true), 1f, true);
        var state = new ParryRearmState();
        var attempt = new ActiveDefenseAttempt();
        for (int i = 0; i < 20; i++)
        {
            float time = i * .05f;
            state.ObservePress(time);
            if (state.IsPressRearmed(time, ParryRearmState.IntervalFor(request.Duration)))
                DefenseJudgementPolicy.TryCommitBattleInput(ref attempt, request,
                    DefenseInputReadStatus.Valid, DefenseInput.Parry, time, 1f);
        }
        Assert.That(attempt.HasAttempt, Is.False);
        var result = DefenseJudgementPolicy.EvaluateActiveDefense(request, attempt, true, 1f);
        Assert.That(result.IsGuard, Is.True);
        Assert.That(result.DamageMultiplier, Is.EqualTo(.35f));
        state.ObservePress(1.2f);
        Assert.That(state.IsPressRearmed(1.2f, .18f), Is.True);
    }
}
