using NUnit.Framework;

public class DefenseJudgementPolicyTests
{
    [Test]
    public void InputSelection_RequiresExactlyOnePressedInput()
    {
        AssertSelection(false, false, false, DefenseInputReadStatus.None, DefenseInput.None);
        AssertSelection(true, false, false, DefenseInputReadStatus.Valid, DefenseInput.Parry);
        AssertSelection(false, true, false, DefenseInputReadStatus.Valid, DefenseInput.Dodge);
        AssertSelection(false, false, true, DefenseInputReadStatus.Valid, DefenseInput.Jump);
        AssertSelection(true, true, false, DefenseInputReadStatus.Ambiguous, DefenseInput.None);
        AssertSelection(true, true, true, DefenseInputReadStatus.Ambiguous, DefenseInput.None);
    }

    [TestCase(DefenseRequirement.Any, DefenseInput.Parry, true)]
    [TestCase(DefenseRequirement.Any, DefenseInput.Dodge, true)]
    [TestCase(DefenseRequirement.Any, DefenseInput.Jump, true)]
    [TestCase(DefenseRequirement.ParryOrDodge, DefenseInput.Parry, true)]
    [TestCase(DefenseRequirement.ParryOrDodge, DefenseInput.Dodge, true)]
    [TestCase(DefenseRequirement.ParryOrDodge, DefenseInput.Jump, false)]
    [TestCase(DefenseRequirement.JumpOnly, DefenseInput.Jump, true)]
    [TestCase(DefenseRequirement.JumpOnly, DefenseInput.Parry, false)]
    [TestCase(DefenseRequirement.ParryOnly, DefenseInput.Parry, true)]
    [TestCase(DefenseRequirement.DodgeOnly, DefenseInput.Dodge, true)]
    [TestCase(DefenseRequirement.DodgeOrJump, DefenseInput.Dodge, true)]
    [TestCase(DefenseRequirement.DodgeOrJump, DefenseInput.Jump, true)]
    [TestCase(DefenseRequirement.DodgeOrJump, DefenseInput.Parry, false)]
    public void RequirementMatching_IsDataDriven(
        DefenseRequirement requirement,
        DefenseInput input,
        bool expected)
    {
        Assert.That(DefenseJudgementPolicy.Matches(requirement, input), Is.EqualTo(expected));
    }

    [TestCase(0.05f, QTEManager.QTEGrade.Perfect, DefenseOutcome.Success)]
    [TestCase(0.15f, QTEManager.QTEGrade.Great, DefenseOutcome.Success)]
    [TestCase(0.30f, QTEManager.QTEGrade.Good, DefenseOutcome.Success)]
    [TestCase(0.60f, QTEManager.QTEGrade.Bad, DefenseOutcome.NearSuccess)]
    public void Evaluate_UsesSecondsBeforeImpactForGrade(
        float secondsBeforeImpact,
        QTEManager.QTEGrade expectedGrade,
        DefenseOutcome expectedOutcome)
    {
        DefenseQteRequest request = CreateRequest(DefenseRequirement.Any, true);

        DefenseQteResult result = DefenseJudgementPolicy.Evaluate(
            request,
            DefenseInputReadStatus.Valid,
            DefenseInput.Parry,
            secondsBeforeImpact);

        Assert.That(result.Grade, Is.EqualTo(expectedGrade));
        Assert.That(result.Outcome, Is.EqualTo(expectedOutcome));
        Assert.That(result.PreventsDamage, Is.True);
    }

    [Test]
    public void Evaluate_WrongInputIsInvalidEvenWithPerfectTiming()
    {
        DefenseQteRequest request = CreateRequest(DefenseRequirement.JumpOnly, true);

        DefenseQteResult result = DefenseJudgementPolicy.Evaluate(
            request,
            DefenseInputReadStatus.Valid,
            DefenseInput.Parry,
            0.05f);

        Assert.That(result.Outcome, Is.EqualTo(DefenseOutcome.Invalid));
        Assert.That(result.Grade, Is.EqualTo(QTEManager.QTEGrade.Perfect));
        Assert.That(result.InputMatched, Is.False);
        Assert.That(result.PreventsDamage, Is.False);
    }

    [Test]
    public void Evaluate_TimeoutAndAmbiguousInputHaveDistinctOutcomes()
    {
        DefenseQteRequest request = CreateRequest(DefenseRequirement.Any, true);

        DefenseQteResult timeout = DefenseJudgementPolicy.Evaluate(
            request,
            DefenseInputReadStatus.None,
            DefenseInput.None,
            0f);
        DefenseQteResult ambiguous = DefenseJudgementPolicy.Evaluate(
            request,
            DefenseInputReadStatus.Ambiguous,
            DefenseInput.None,
            0.2f);

        Assert.That(timeout.Outcome, Is.EqualTo(DefenseOutcome.Failure));
        Assert.That(ambiguous.Outcome, Is.EqualTo(DefenseOutcome.Invalid));
        Assert.That(timeout.Grade, Is.EqualTo(QTEManager.QTEGrade.Miss));
        Assert.That(ambiguous.Grade, Is.EqualTo(QTEManager.QTEGrade.Miss));
    }

    [Test]
    public void Evaluate_NearSuccessCanBeConfiguredNotToPreventDamage()
    {
        DefenseQteRequest request = CreateRequest(DefenseRequirement.Any, false);

        DefenseQteResult result = DefenseJudgementPolicy.Evaluate(
            request,
            DefenseInputReadStatus.Valid,
            DefenseInput.Dodge,
            0.8f);

        Assert.That(result.Outcome, Is.EqualTo(DefenseOutcome.NearSuccess));
        Assert.That(result.PreventsDamage, Is.False);
    }

    [Test]
    public void TimingProfile_NormalizesDifficultyOrderAndDuration()
    {
        var profile = new DefenseTimingProfile(0.8f, 0.1f, -1f);

        DefenseTimingProfile normalized = profile.Normalize(0.3f, 2f);

        Assert.That(normalized.PerfectWindow, Is.InRange(0f, 0.3f));
        Assert.That(normalized.GreatWindow, Is.GreaterThanOrEqualTo(normalized.PerfectWindow));
        Assert.That(normalized.GoodWindow, Is.GreaterThanOrEqualTo(normalized.GreatWindow));
        Assert.That(normalized.GoodWindow, Is.LessThanOrEqualTo(0.3f));
    }

    [Test]
    public void DefenseRequirement_PreservesExistingSerializedValues()
    {
        Assert.That((int)DefenseRequirement.ParryOrDodge, Is.EqualTo(0));
        Assert.That((int)DefenseRequirement.JumpOnly, Is.EqualTo(1));
    }

    [Test]
    public void TimedGuard_RequestDefaults_PreserveExplicitLegacyRequests()
    {
        DefenseQteRequest request = CreateRequest(DefenseRequirement.Any, true);

        Assert.That(request.UseTimedGuard, Is.False);
        Assert.That(request.GuardDuration, Is.EqualTo(0.4f));
        Assert.That(request.GuardDamageMultiplier, Is.EqualTo(0.5f));
    }

    [TestCase(0.30f, DefenseOutcome.Guarded, 0.5f, QTEManager.QTEGrade.Good)]
    [TestCase(0.12f, DefenseOutcome.Success, 0f, QTEManager.QTEGrade.Perfect)]
    [TestCase(0.121f, DefenseOutcome.Guarded, 0.5f, QTEManager.QTEGrade.Good)]
    [TestCase(0.40f, DefenseOutcome.Guarded, 0.5f, QTEManager.QTEGrade.Good)]
    [TestCase(0.401f, DefenseOutcome.Failure, 1f, QTEManager.QTEGrade.Miss)]
    public void EvaluateTimedGuard_HeldInputUsesPerfectAndGuardBoundaries(
        float secondsBeforeImpact,
        DefenseOutcome expectedOutcome,
        float expectedDamageMultiplier,
        QTEManager.QTEGrade expectedGrade)
    {
        DefenseQteRequest request = CreateTimedGuardRequest();
        TimedGuardAttempt attempt = CreateGuardAttempt(secondsBeforeImpact);
        attempt.ObserveHeld(true);

        DefenseQteResult result = DefenseJudgementPolicy.EvaluateTimedGuard(request, attempt, 0f);

        Assert.That(result.Input, Is.EqualTo(DefenseInput.Parry));
        Assert.That(result.InputStatus, Is.EqualTo(DefenseInputReadStatus.Valid));
        Assert.That(result.InputMatched, Is.True);
        Assert.That(result.SecondsBeforeImpact, Is.EqualTo(secondsBeforeImpact));
        Assert.That(result.Outcome, Is.EqualTo(expectedOutcome));
        Assert.That(result.Grade, Is.EqualTo(expectedGrade));
        Assert.That(result.DamageMultiplier, Is.EqualTo(expectedDamageMultiplier));
        Assert.That(result.PreventsDamage, Is.EqualTo(expectedDamageMultiplier == 0f));
        Assert.That(result.IsGuard, Is.EqualTo(expectedOutcome == DefenseOutcome.Guarded));
        Assert.That(result.IsPerfectParry, Is.EqualTo(expectedGrade == QTEManager.QTEGrade.Perfect));
    }

    [Test]
    public void EvaluateTimedGuard_ReleasingOrdinaryGuardRestoresFullDamage()
    {
        TimedGuardAttempt attempt = CreateGuardAttempt(0.3f);
        attempt.ObserveHeld(false);
        attempt.ObserveHeld(true);

        DefenseQteResult result = DefenseJudgementPolicy.EvaluateTimedGuard(
            CreateTimedGuardRequest(), attempt, 0f);

        Assert.That(attempt.WasReleased, Is.True, "Holding again must not revive a released attempt.");
        Assert.That(result.Outcome, Is.EqualTo(DefenseOutcome.Failure));
        Assert.That(result.DamageMultiplier, Is.EqualTo(1f));
        Assert.That(result.PreventsDamage, Is.False);
        Assert.That(result.IsGuard, Is.False);
    }

    [Test]
    public void EvaluateTimedGuard_PerfectTapCanBeReleasedBeforeImpact()
    {
        TimedGuardAttempt attempt = CreateGuardAttempt(0.1f);
        attempt.ObserveHeld(false);

        DefenseQteResult result = DefenseJudgementPolicy.EvaluateTimedGuard(
            CreateTimedGuardRequest(), attempt, 0f);

        Assert.That(result.Outcome, Is.EqualTo(DefenseOutcome.Success));
        Assert.That(result.IsPerfectParry, Is.True);
        Assert.That(result.PreventsDamage, Is.True);
        Assert.That(result.DamageMultiplier, Is.Zero);
        Assert.That(attempt.RemainingAt(-0.05f, 0.4f), Is.Zero);
    }

    [Test]
    public void TimedGuardAttempt_SecondPressCannotRefreshOrUpgradeFirstInput()
    {
        TimedGuardAttempt attempt = CreateGuardAttempt(0.5f);
        attempt.ObserveHeld(false);

        bool acceptedSecondPress = attempt.TryPress(-0.05f, -1f, 0f);
        attempt.ObserveHeld(true);
        DefenseQteResult result = DefenseJudgementPolicy.EvaluateTimedGuard(
            CreateTimedGuardRequest(), attempt, 0f);

        Assert.That(acceptedSecondPress, Is.False);
        Assert.That(attempt.PressedAt, Is.EqualTo(-0.5f));
        Assert.That(attempt.WasReleased, Is.True);
        Assert.That(result.Outcome, Is.EqualTo(DefenseOutcome.Failure));
        Assert.That(result.IsPerfectParry, Is.False);
        Assert.That(result.DamageMultiplier, Is.EqualTo(1f));
    }

    [Test]
    public void TimedGuardAttempt_HoldingDoesNotExtendRemainingTime()
    {
        TimedGuardAttempt attempt = CreateGuardAttempt(0.5f);
        attempt.ObserveHeld(true);
        Assert.That(attempt.RemainingAt(-0.3f, 0.4f), Is.EqualTo(0.2f).Within(0.0001f));

        attempt.ObserveHeld(true);

        Assert.That(attempt.RemainingAt(0f, 0.4f), Is.Zero);
        Assert.That(attempt.PressedAt, Is.EqualTo(-0.5f));
        Assert.That(attempt.RemainingAt(1f, 0.4f), Is.Zero);
    }

    [TestCase(-1.01f)]
    [TestCase(0f)]
    [TestCase(0.01f)]
    public void TimedGuardAttempt_RejectsInputBeforeWindowOrAtAndAfterImpact(float pressedAt)
    {
        var attempt = new TimedGuardAttempt();

        Assert.That(attempt.TryPress(pressedAt, -1f, 0f), Is.False);
        Assert.That(attempt.HasAttempt, Is.False);
        Assert.That(attempt.RemainingAt(0f, 0.4f), Is.Zero);
        Assert.That(attempt.TryPress(-0.3f, -1f, 0f), Is.True,
            "Rejected input must not consume the window's one allowed attempt.");
    }

    [Test]
    public void TimedGuardAttempt_WindowOpeningIsInclusiveAndPriorReleaseDoesNotConsumeAttempt()
    {
        var attempt = new TimedGuardAttempt();
        attempt.ObserveHeld(false);

        Assert.That(attempt.TryPress(-1f, -1f, 0f), Is.True);
        Assert.That(attempt.HasAttempt, Is.True);
        Assert.That(attempt.WasReleased, Is.False);
    }

    [Test]
    public void EvaluateTimedGuard_NoFreshInputDoesNotGuard()
    {
        var attempt = new TimedGuardAttempt();
        attempt.ObserveHeld(true);

        DefenseQteResult result = DefenseJudgementPolicy.EvaluateTimedGuard(
            CreateTimedGuardRequest(), attempt, 0f);

        Assert.That(result.InputStatus, Is.EqualTo(DefenseInputReadStatus.None));
        Assert.That(result.Input, Is.EqualTo(DefenseInput.None));
        Assert.That(result.Outcome, Is.EqualTo(DefenseOutcome.Failure));
        Assert.That(result.DamageMultiplier, Is.EqualTo(1f));
        Assert.That(result.IsGuard, Is.False);
        Assert.That(result.IsPerfectParry, Is.False);
    }

    [TestCase(0.3f, DefenseOutcome.Guarded)]
    [TestCase(0.1f, DefenseOutcome.Success)]
    public void EvaluateTimedGuard_UnifiesLegacyJumpRequirementWithoutChangingLegacyMatching(
        float secondsBeforeImpact,
        DefenseOutcome expectedOutcome)
    {
        DefenseQteRequest request = CreateTimedGuardRequest(DefenseRequirement.JumpOnly);
        TimedGuardAttempt attempt = CreateGuardAttempt(secondsBeforeImpact);

        DefenseQteResult result = DefenseJudgementPolicy.EvaluateTimedGuard(request, attempt, 0f);

        Assert.That(result.Requirement, Is.EqualTo(DefenseRequirement.JumpOnly));
        Assert.That(result.Input, Is.EqualTo(DefenseInput.Parry));
        Assert.That(result.InputMatched, Is.True);
        Assert.That(result.Outcome, Is.EqualTo(expectedOutcome));
        Assert.That(DefenseJudgementPolicy.Matches(DefenseRequirement.JumpOnly, DefenseInput.Parry), Is.False);
        Assert.That(DefenseJudgementPolicy.Matches(DefenseRequirement.JumpOnly, DefenseInput.Jump), Is.True);
    }

    [TestCase(0.5f, 0.20f, DefenseOutcome.Success)]
    [TestCase(1f, 0.20f, DefenseOutcome.Guarded)]
    [TestCase(2f, 0.10f, DefenseOutcome.Guarded)]
    [TestCase(2f, 0.06f, DefenseOutcome.Success)]
    [TestCase(0.5f, 0.30f, DefenseOutcome.Guarded)]
    [TestCase(2f, 0.30f, DefenseOutcome.Guarded)]
    [TestCase(0.5f, 0.401f, DefenseOutcome.Failure)]
    [TestCase(2f, 0.401f, DefenseOutcome.Failure)]
    public void EvaluateTimedGuard_DifficultyChangesPerfectWindowButNotGuardDuration(
        float difficulty,
        float secondsBeforeImpact,
        DefenseOutcome expectedOutcome)
    {
        DefenseQteRequest request = CreateTimedGuardRequest(difficulty: difficulty);
        TimedGuardAttempt attempt = CreateGuardAttempt(secondsBeforeImpact);

        DefenseQteResult result = DefenseJudgementPolicy.EvaluateTimedGuard(request, attempt, 0f);

        Assert.That(request.GuardDuration, Is.EqualTo(0.4f));
        Assert.That(result.Outcome, Is.EqualTo(expectedOutcome));
        Assert.That(result.DamageMultiplier, Is.EqualTo(expectedOutcome == DefenseOutcome.Success
            ? 0f : expectedOutcome == DefenseOutcome.Guarded ? 0.5f : 1f));
    }

    [Test]
    public void EvaluateTimedGuard_UsesConfiguredPartialDamageWithoutLegacyNearSuccess()
    {
        var request = new DefenseQteRequest(
            1f, 1f, DefenseRequirement.Any, new DefenseTimingProfile(0.12f, 0.22f, 0.4f),
            allowNearSuccess: false, useTimedGuard: true, guardDuration: 0.4f, guardDamageMultiplier: 0.35f);

        DefenseQteResult result = DefenseJudgementPolicy.EvaluateTimedGuard(
            request, CreateGuardAttempt(0.3f), 0f);

        Assert.That(result.Outcome, Is.EqualTo(DefenseOutcome.Guarded));
        Assert.That(result.PreventsDamage, Is.False);
        Assert.That(result.IsPerfectParry, Is.False);
        Assert.That(result.DamageMultiplier, Is.EqualTo(0.35f));
    }

    private static DefenseQteRequest CreateTimedGuardRequest(
        DefenseRequirement requirement = DefenseRequirement.Any,
        float difficulty = 1f)
    {
        return new DefenseQteRequest(
            1f, difficulty, requirement, new DefenseTimingProfile(0.12f, 0.22f, 0.4f),
            useTimedGuard: true);
    }

    private static TimedGuardAttempt CreateGuardAttempt(float secondsBeforeImpact)
    {
        // Place impact at zero so subtraction does not round the exact 0.12 / 0.4 boundaries.
        var attempt = new TimedGuardAttempt();
        Assert.That(attempt.TryPress(-secondsBeforeImpact, -1f, 0f), Is.True);
        return attempt;
    }

    private static DefenseQteRequest CreateRequest(
        DefenseRequirement requirement,
        bool allowNearSuccess)
    {
        return new DefenseQteRequest(
            1f,
            1f,
            requirement,
            new DefenseTimingProfile(0.1f, 0.2f, 0.4f),
            allowNearSuccess);
    }

    private static void AssertSelection(
        bool parry,
        bool dodge,
        bool jump,
        DefenseInputReadStatus expectedStatus,
        DefenseInput expectedInput)
    {
        DefenseInputReadStatus status = DefenseInputSelectionPolicy.Resolve(
            parry,
            dodge,
            jump,
            out DefenseInput input);

        Assert.That(status, Is.EqualTo(expectedStatus));
        Assert.That(input, Is.EqualTo(expectedInput));
    }
}
