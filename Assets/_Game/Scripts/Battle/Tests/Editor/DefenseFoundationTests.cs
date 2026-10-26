using NUnit.Framework;

public class DefenseFoundationTests
{
    private static DefenseQteRequest Request(DefenseRequirement requirement = DefenseRequirement.Any)
        => DefenseJudgementPolicy.WithBattleAssistance(new DefenseQteRequest(1f, 1f,
            requirement, new DefenseTimingProfile(.12f, .22f, .4f), useActiveDefense: true), 1f, true);

    [Test]
    public void CueLengthIsPreservedWithoutEnlargingSuccessWindows()
    {
        var request = Request();
        Assert.That(DefenseJudgementPolicy.GetActiveCueWindow(request), Is.EqualTo(.4f));
        Assert.That(DefenseJudgementPolicy.GetActiveSuccessWindow(request, DefenseInput.Parry), Is.EqualTo(.12f));
        Assert.That(DefenseJudgementPolicy.GetActiveSuccessWindow(request, DefenseInput.Dodge), Is.EqualTo(.22f));
        Assert.That(DefenseJudgementPolicy.GetActiveSuccessWindow(Request(DefenseRequirement.CounterOnly),
            DefenseInput.Counter), Is.EqualTo(.18f));
    }

    [Test]
    public void EarlyCuePressCannotBeUpgradedButHeldGuardSurvivesEveryHit()
    {
        var request = Request();
        var attempt = new ActiveDefenseAttempt();
        Assert.That(DefenseJudgementPolicy.TryCommitBattleInput(ref attempt, request,
            DefenseInputReadStatus.Valid, DefenseInput.Parry, .7f, 1f), Is.True);
        Assert.That(DefenseJudgementPolicy.TryCommitBattleInput(ref attempt, request,
            DefenseInputReadStatus.Valid, DefenseInput.Parry, .95f, 1f), Is.False);
        Assert.That(DefenseJudgementPolicy.EvaluateActiveDefense(request, attempt, true, 1f).IsGuard, Is.True);
        for (int hit = 2; hit <= 20; hit++)
        {
            var result = DefenseJudgementPolicy.EvaluateActiveDefense(request, default, true, hit);
            Assert.That(result.IsGuard, Is.True);
            Assert.That(result.DamageMultiplier, Is.EqualTo(.35f));
        }
    }

    [TestCase(DefenseRequirement.Counterable)]
    [TestCase(DefenseRequirement.CounterOnly)]
    public void WrongCounterInputCommitsFailureAndCannotBecomeLaterCounter(DefenseRequirement requirement)
    {
        var request = Request(requirement);
        var attempt = new ActiveDefenseAttempt();
        Assert.That(DefenseJudgementPolicy.TryCommitBattleInput(ref attempt, request,
            DefenseInputReadStatus.Valid, DefenseInput.Dodge, .85f, 1f), Is.True);
        Assert.That(DefenseJudgementPolicy.TryCommitBattleInput(ref attempt, request,
            DefenseInputReadStatus.Valid, DefenseInput.Counter, .95f, 1f), Is.False);
        Assert.That(DefenseJudgementPolicy.EvaluateActiveDefense(request, attempt, true, 1f).PreventsDamage, Is.False);
        Assert.That(DefenseJudgementPolicy.MatchesActive(requirement, DefenseInput.Dodge), Is.False);
    }

    [Test]
    public void SimultaneousButtonsCommitInvalidAttempt()
    {
        var request = Request();
        var attempt = new ActiveDefenseAttempt();
        Assert.That(DefenseJudgementPolicy.TryCommitBattleInput(ref attempt, request,
            DefenseInputReadStatus.Ambiguous, DefenseInput.None, .9f, 1f), Is.True);
        Assert.That(DefenseJudgementPolicy.TryCommitBattleInput(ref attempt, request,
            DefenseInputReadStatus.Valid, DefenseInput.Parry, .95f, 1f), Is.False);
        Assert.That(DefenseJudgementPolicy.EvaluateActiveDefense(request, attempt, true, 1f).Outcome,
            Is.EqualTo(DefenseOutcome.Invalid));
    }

    [TestCase(DefenseInput.Parry, DefenseRequirement.Any, true)]
    [TestCase(DefenseInput.Dodge, DefenseRequirement.Any, false)]
    [TestCase(DefenseInput.Counter, DefenseRequirement.CounterOnly, false)]
    public void UnrearmedPressCannotAvoidDamageButHeldZRetainsNormalGuard(
        DefenseInput input, DefenseRequirement requirement, bool expectedGuard)
    {
        var request = Request(requirement);
        var attempt = new ActiveDefenseAttempt();
        Assert.That(DefenseJudgementPolicy.TryCommitBattleInput(ref attempt, request,
            DefenseInputReadStatus.Valid, input, .95f, 1f, allowsPrecision: false), Is.True);
        var result = DefenseJudgementPolicy.EvaluateActiveDefense(request, attempt, true, 1f);
        Assert.That(result.PreventsDamage, Is.False);
        Assert.That(result.IsGuard, Is.EqualTo(expectedGuard));
        Assert.That(result.DamageMultiplier, Is.EqualTo(expectedGuard ? .35f : 1f));
    }

    [Test]
    public void SwitchingBetweenDefenseKeysUsesTheSameRearmHistory()
    {
        var state = new ParryRearmState();
        var keys = new[] { DefenseInput.Parry, DefenseInput.Dodge, DefenseInput.Counter };
        for (int press = 0; press < 60; press++)
        {
            float time = press * .05f;
            state.ObservePress(time);
            bool ready = state.IsPressRearmed(time, ParryRearmState.DefaultInterval);
            Assert.That(ready, Is.EqualTo(press == 0), "Shared history must not reset when key changes: " + keys[press % 3]);
        }
        state.ObservePress(3.2f);
        Assert.That(state.IsPressRearmed(3.2f, ParryRearmState.DefaultInterval), Is.True);
    }
}
