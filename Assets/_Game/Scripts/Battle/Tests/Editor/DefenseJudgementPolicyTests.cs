using NUnit.Framework;

public class DefenseJudgementPolicyTests
{
    [TestCase(0.5f)]
    [TestCase(1f)]
    [TestCase(2f)]
    public void BattleAssistance_ProvidesRealTimeMinimumWindows(float difficulty)
    {
        var original = new DefenseQteRequest(1f, difficulty, DefenseRequirement.Any,
            new DefenseTimingProfile(0.12f, 0.22f, 0.4f), useActiveDefense: true);
        DefenseQteRequest request = DefenseJudgementPolicy.WithBattleAssistance(original, 1.05f);
        Assert.That(DefenseJudgementPolicy.GetActiveSuccessWindow(request, DefenseInput.Parry),
            Is.GreaterThanOrEqualTo(0.24f));
        Assert.That(DefenseJudgementPolicy.GetPreparationWindow(request), Is.GreaterThanOrEqualTo(0.34f));
        Assert.That(original.TimingProfile.PerfectWindow, Is.EqualTo(0.12f), "Authored data must not change.");
    }

    [TestCase(DefenseRequirement.Any, DefenseInput.Parry, 0.24f)]
    [TestCase(DefenseRequirement.Any, DefenseInput.Dodge, 0.34f)]
    [TestCase(DefenseRequirement.Counterable, DefenseInput.Counter, 0.28f)]
    public void BattleInput_EarlyPressDoesNotConsumeValidPress(DefenseRequirement requirement,
        DefenseInput input, float window)
    {
        DefenseQteRequest request = DefenseJudgementPolicy.WithBattleAssistance(CreateActiveRequest(requirement), 1f);
        var attempt = new ActiveDefenseAttempt();
        Assert.That(DefenseJudgementPolicy.TryCommitBattleInput(ref attempt, request,
            DefenseInputReadStatus.Valid, input, -window - 0.01f, 0f), Is.False);
        Assert.That(DefenseJudgementPolicy.TryCommitBattleInput(ref attempt, request,
            DefenseInputReadStatus.Valid, input, -window, 0f), Is.True);
        Assert.That(DefenseJudgementPolicy.EvaluateActiveDefense(request, attempt, false, 0f).PreventsDamage, Is.True);
        Assert.That(DefenseJudgementPolicy.TryCommitBattleInput(ref attempt, request,
            DefenseInputReadStatus.Valid, input, -0.01f, 0f), Is.False, "Accepted input cannot refresh.");
    }

    [Test]
    public void BattleInput_DodgePreparationPrecedesJustGuardPing()
    {
        DefenseQteRequest request = DefenseJudgementPolicy.WithBattleAssistance(CreateActiveRequest(), 1f);
        var dodge = new ActiveDefenseAttempt();
        var parry = new ActiveDefenseAttempt();
        Assert.That(DefenseJudgementPolicy.TryCommitBattleInput(ref dodge, request,
            DefenseInputReadStatus.Valid, DefenseInput.Dodge, -0.25f, 0f), Is.True);
        Assert.That(DefenseJudgementPolicy.TryCommitBattleInput(ref parry, request,
            DefenseInputReadStatus.Valid, DefenseInput.Parry, -0.25f, 0f), Is.False);
        Assert.That(DefenseJudgementPolicy.EvaluateActiveDefense(request, parry, true, 0f).IsGuard, Is.True);
    }

    [TestCase(DefenseRequirement.Any, DefenseInput.Parry)]
    [TestCase(DefenseRequirement.Any, DefenseInput.Dodge)]
    [TestCase(DefenseRequirement.Counterable, DefenseInput.Counter)]
    public void BattleInput_LateGraceAcceptsFreshInputWithoutDamageRollback(DefenseRequirement requirement, DefenseInput input)
    {
        var request = DefenseJudgementPolicy.WithBattleAssistance(CreateActiveRequest(requirement), 1f);
        foreach (float impactAt in new[] { 0f, 10f, 1000f })
        foreach (float offset in new[] { 0f, .01f, .03f, .059f })
        {
            var attempt = new ActiveDefenseAttempt();
            Assert.That(DefenseJudgementPolicy.TryCommitBattleInput(ref attempt, request,
                DefenseInputReadStatus.Valid, input, impactAt + offset, impactAt), Is.True);
            Assert.That(DefenseJudgementPolicy.EvaluateActiveDefense(request, attempt, false, impactAt,
                DefenseJudgementPolicy.BattleLateGrace).PreventsDamage, Is.True);
            Assert.That(DefenseJudgementPolicy.ShouldResolveBattleImpact(impactAt + offset, impactAt, attempt), Is.True);
            Assert.That(DefenseJudgementPolicy.TryCommitBattleInput(ref attempt, request,
                DefenseInputReadStatus.Valid, input, impactAt + .04f, impactAt), Is.False);
        }
    }

    [Test]
    public void BattleInput_GraceIsBoundedAndDoesNotChangeLegacyOrWrongInput()
    {
        var request = DefenseJudgementPolicy.WithBattleAssistance(CreateActiveRequest(), 1f);
        var attempt = new ActiveDefenseAttempt();
        Assert.That(DefenseJudgementPolicy.ShouldResolveBattleImpact(.03f, 0f, attempt), Is.False);
        Assert.That(DefenseJudgementPolicy.ShouldResolveBattleImpact(.06f, 0f, attempt), Is.True);
        Assert.That(DefenseJudgementPolicy.TryCommitBattleInput(ref attempt, request,
            DefenseInputReadStatus.Valid, DefenseInput.Parry, .06f, 0f), Is.False);
        Assert.That(DefenseJudgementPolicy.TryCommitBattleInput(ref attempt, request,
            DefenseInputReadStatus.Ambiguous, DefenseInput.None, .01f, 0f), Is.False);
        Assert.That(DefenseJudgementPolicy.TryCommitBattleInput(ref attempt, request,
            DefenseInputReadStatus.Valid, DefenseInput.Counter, .01f, 0f), Is.False);
        Assert.That(attempt.TryCommit(DefenseInputReadStatus.Valid, DefenseInput.Parry, .03f, -1f, 0f), Is.False);
        Assert.That(DefenseJudgementPolicy.TryCommitBattleInput(ref attempt, request,
            DefenseInputReadStatus.Valid, DefenseInput.Parry, -.01f, 0f), Is.True);
        Assert.That(DefenseJudgementPolicy.ShouldResolveBattleImpact(0f, 0f, attempt), Is.True);
        Assert.That(DefenseJudgementPolicy.ShouldResolveBattleImpact(-.001f, 0f, attempt), Is.False);
    }

    [Test]
    public void DefensePresentationGate_RepeatedInputCannotRestartBusyOrCommittedReaction()
    {
        var gate = new DefensePresentationGate();
        Assert.That(gate.CanPreview(false), Is.False, "Non-target allies must stay in place.");
        gate.Open();
        Assert.That(gate.CanPreview(false), Is.True);
        for (int i = 0; i < 100; i++) Assert.That(gate.CanPreview(true), Is.False);
        Assert.That(gate.CanPreview(false), Is.True, "An early preview must not consume a later valid input.");
        gate.Commit();
        Assert.That(gate.CanPreview(false), Is.False);
        gate.Close();
        Assert.That(gate.CanPreview(false), Is.False, "Counter/hurt animation owns presentation after impact.");
        gate.Open();
        Assert.That(gate.CanPreview(false), Is.True);
    }

    [TestCase(0.01f)]
    [TestCase(0.05f)]
    [TestCase(0.3f)]
    [TestCase(0.85f)]
    [TestCase(3f)]
    public void BattleImpactClock_IsMonotonicAndEndsAtAuthoredImpact(float duration)
    {
        var timing = new BattleImpactTiming(duration, 0.2f);
        Assert.That(timing.Duration - duration, Is.EqualTo(timing.SlowDuration * 0.5f).Within(0.000001f));
        Assert.That(timing.SlowDuration, Is.LessThanOrEqualTo(0.1f));
        float previous = 0f;
        for (int i = 0; i <= 100; i++)
        {
            float current = timing.AttackTimeAt(timing.Duration * i / 100f);
            Assert.That(current, Is.GreaterThanOrEqualTo(previous));
            previous = current;
        }
        Assert.That(previous, Is.EqualTo(duration).Within(0.000001f));
        Assert.That(timing.RateAt(timing.Duration), Is.EqualTo(1f));
        var legacy = new BattleImpactTiming(duration, 0.2f, false);
        Assert.That(legacy.AttackTimeAt(duration), Is.EqualTo(duration));
        Assert.That(legacy.SlowDuration, Is.Zero);
    }

    [Test]
    public void ActiveSelection_MapsDefenseCWithoutChangingLegacySequenceC()
    {
        Assert.That(DefenseInputSelectionPolicy.ResolveActive(false, false, true, out DefenseInput active),
            Is.EqualTo(DefenseInputReadStatus.Valid));
        Assert.That(active, Is.EqualTo(DefenseInput.Counter));
        Assert.That(DefenseInputSelectionPolicy.Resolve(false, false, true, out DefenseInput legacy),
            Is.EqualTo(DefenseInputReadStatus.Valid));
        Assert.That(legacy, Is.EqualTo(DefenseInput.Jump));
        Assert.That((int)DefenseInput.Jump, Is.EqualTo(3));
        Assert.That((int)DefenseInput.Counter, Is.EqualTo(4));
        Assert.That((int)DefenseRequirement.Counterable, Is.EqualTo(6));
    }

    [TestCase(DefenseRequirement.Any, DefenseInput.Parry, true)]
    [TestCase(DefenseRequirement.Any, DefenseInput.Dodge, true)]
    [TestCase(DefenseRequirement.Any, DefenseInput.Counter, false)]
    [TestCase(DefenseRequirement.ParryOnly, DefenseInput.Dodge, true)]
    [TestCase(DefenseRequirement.ParryOrDodge, DefenseInput.Parry, true)]
    [TestCase(DefenseRequirement.JumpOnly, DefenseInput.Dodge, true)]
    [TestCase(DefenseRequirement.JumpOnly, DefenseInput.Jump, false)]
    [TestCase(DefenseRequirement.JumpOnly, DefenseInput.Counter, false)]
    [TestCase(DefenseRequirement.DodgeOnly, DefenseInput.Parry, false)]
    [TestCase(DefenseRequirement.DodgeOrJump, DefenseInput.Counter, false)]
    [TestCase(DefenseRequirement.Counterable, DefenseInput.Parry, false)]
    [TestCase(DefenseRequirement.Counterable, DefenseInput.Dodge, true)]
    [TestCase(DefenseRequirement.Counterable, DefenseInput.Counter, true)]
    public void ActiveRequirement_PreservesLegacyAssetsWithoutTurningJumpIntoCounter(
        DefenseRequirement requirement, DefenseInput input, bool expected)
    {
        Assert.That(DefenseJudgementPolicy.MatchesActive(requirement, input), Is.EqualTo(expected));
    }

    [Test]
    public void ActiveGuard_PreheldAcrossImpactsReducesDamageWithoutPerfectReward()
    {
        DefenseQteRequest request = CreateActiveRequest();
        for (int impact = 0; impact < 3; impact++)
        {
            DefenseQteResult result = DefenseJudgementPolicy.EvaluateActiveDefense(request, default, true, impact);
            Assert.That(result.IsGuard, Is.True);
            Assert.That(result.IsJustGuard, Is.False);
            Assert.That(result.DamageMultiplier, Is.EqualTo(0.5f));
            Assert.That(result.PreventsDamage, Is.False);
        }
    }

    [TestCase(0.12f, false, true, 0f)]
    [TestCase(0.121f, true, false, 0.5f)]
    [TestCase(0.9f, true, false, 0.5f)]
    [TestCase(0.13f, false, false, 1f)]
    public void ActiveGuard_NewPressUsesJustWindowAndOtherwiseNeedsHeldGuard(
        float remaining, bool held, bool perfect, float multiplier)
    {
        DefenseQteResult result = DefenseJudgementPolicy.EvaluateActiveDefense(
            CreateActiveRequest(), CreateActiveAttempt(DefenseInput.Parry, remaining), held, 0f);
        Assert.That(result.IsJustGuard, Is.EqualTo(perfect));
        Assert.That(result.DamageMultiplier, Is.EqualTo(multiplier));
        Assert.That(result.IsCounterSuccess, Is.False);
    }

    [TestCase(DefenseInput.Dodge, 0.22f, true)]
    [TestCase(DefenseInput.Dodge, 0.221f, false)]
    [TestCase(DefenseInput.Counter, 0.18f, true)]
    [TestCase(DefenseInput.Counter, 0.181f, false)]
    public void ActiveSpecial_DodgeAndCounterUseOwnWindowsWithoutGuardFallback(
        DefenseInput input, float remaining, bool success)
    {
        DefenseQteResult result = DefenseJudgementPolicy.EvaluateActiveDefense(
            CreateActiveRequest(DefenseRequirement.Counterable), CreateActiveAttempt(input, remaining), true, 0f);
        Assert.That(result.PreventsDamage, Is.EqualTo(success));
        Assert.That(result.IsCounterSuccess, Is.EqualTo(success && input == DefenseInput.Counter));
        Assert.That(result.IsPerfectParry, Is.False);
        Assert.That(result.IsGuard, Is.False);
        Assert.That(result.DamageMultiplier, Is.EqualTo(success ? 0f : 1f));
    }

    [Test]
    public void ActiveNormal_FailedDodgeDoesNotFallBackToHeldGuard()
    {
        DefenseQteResult result = DefenseJudgementPolicy.EvaluateActiveDefense(
            CreateActiveRequest(), CreateActiveAttempt(DefenseInput.Dodge, 0.3f), true, 0f);
        Assert.That(result.Outcome, Is.EqualTo(DefenseOutcome.Failure));
        Assert.That(result.DamageMultiplier, Is.EqualTo(1f));
    }

    [Test]
    public void ActiveSpecial_PreheldOrPreciselyPressedGuardCannotDefend()
    {
        DefenseQteRequest request = CreateActiveRequest(DefenseRequirement.Counterable);
        Assert.That(DefenseJudgementPolicy.EvaluateActiveDefense(request, default, true, 0f).DamageMultiplier,
            Is.EqualTo(1f));
        Assert.That(DefenseJudgementPolicy.EvaluateActiveDefense(request,
            CreateActiveAttempt(DefenseInput.Parry, 0.05f), true, 0f).PreventsDamage, Is.False);
    }

    [Test]
    public void ActiveAttempt_FirstPressCannotBeReplacedBySpamOrAnotherInput()
    {
        ActiveDefenseAttempt attempt = CreateActiveAttempt(DefenseInput.Dodge, 0.7f);
        Assert.That(attempt.TryCommit(DefenseInputReadStatus.Valid, DefenseInput.Parry, -0.05f, -1f, 0f), Is.False);
        Assert.That(DefenseJudgementPolicy.EvaluateActiveDefense(CreateActiveRequest(), attempt, true, 0f)
            .DamageMultiplier, Is.EqualTo(1f));
    }

    [Test]
    public void ActiveGuard_RepressCannotUpgradeButStillRetainsOrdinaryGuard()
    {
        ActiveDefenseAttempt attempt = CreateActiveAttempt(DefenseInput.Parry, 0.8f);
        DefenseQteRequest request = CreateActiveRequest();
        Assert.That(DefenseJudgementPolicy.EvaluateActiveDefense(request, attempt, false, 0f)
            .DamageMultiplier, Is.EqualTo(1f), "Released guard provides no damage reduction.");

        Assert.That(attempt.TryCommit(DefenseInputReadStatus.Valid, DefenseInput.Parry,
            -0.05f, -1f, 0f), Is.False);
        DefenseQteResult result = DefenseJudgementPolicy.EvaluateActiveDefense(request, attempt, true, 0f);

        Assert.That(result.IsGuard, Is.True);
        Assert.That(result.IsJustGuard, Is.False);
        Assert.That(result.DamageMultiplier, Is.EqualTo(0.5f));
    }

    [Test]
    public void ActiveDefense_NoInputAndNoHeldGuardTakesNormalDamage()
    {
        DefenseQteResult result = DefenseJudgementPolicy.EvaluateActiveDefense(
            CreateActiveRequest(), default, false, 0f);
        Assert.That(result.InputStatus, Is.EqualTo(DefenseInputReadStatus.None));
        Assert.That(result.Outcome, Is.EqualTo(DefenseOutcome.Failure));
        Assert.That(result.DamageMultiplier, Is.EqualTo(1f));
    }

    [Test]
    public void ActiveAttempt_AmbiguousInputConsumesOpportunityAndPreventsHeldFallback()
    {
        var attempt = new ActiveDefenseAttempt();
        Assert.That(attempt.TryCommit(DefenseInputReadStatus.Ambiguous, DefenseInput.None, -0.1f, -1f, 0f), Is.True);
        Assert.That(attempt.TryCommit(DefenseInputReadStatus.Valid, DefenseInput.Counter, -0.05f, -1f, 0f), Is.False);
        DefenseQteResult result = DefenseJudgementPolicy.EvaluateActiveDefense(CreateActiveRequest(), attempt, true, 0f);
        Assert.That(result.Outcome, Is.EqualTo(DefenseOutcome.Invalid));
        Assert.That(result.DamageMultiplier, Is.EqualTo(1f));
    }

    [TestCase(-1.01f)]
    [TestCase(0f)]
    [TestCase(0.1f)]
    [TestCase(float.NaN)]
    public void ActiveAttempt_RejectsOutsideWindowAndInvalidTimestamps(float pressedAt)
    {
        var attempt = new ActiveDefenseAttempt();
        Assert.That(attempt.TryCommit(DefenseInputReadStatus.Valid, DefenseInput.Counter,
            pressedAt, -1f, 0f, enforceWindowOpening: true), Is.False);
        Assert.That(attempt.HasAttempt, Is.False);
    }

    [TestCase(DefenseRequirement.Any, DefenseInput.Parry, 0.12f)]
    [TestCase(DefenseRequirement.Any, DefenseInput.Dodge, 0.12f)]
    [TestCase(DefenseRequirement.DodgeOnly, DefenseInput.Dodge, 0.22f)]
    [TestCase(DefenseRequirement.Counterable, DefenseInput.Counter, 0.18f)]
    [TestCase(DefenseRequirement.Counterable, DefenseInput.Dodge, 0.18f)]
    public void ActiveCue_EarlyInputDoesNotConsumeFreshPressAtCueOpening(
        DefenseRequirement requirement, DefenseInput input, float expectedWindow)
    {
        DefenseQteRequest request = CreateActiveRequest(requirement);
        const float impactAt = 10f;
        float window = DefenseJudgementPolicy.GetActiveCueWindow(request);
        Assert.That(window, Is.EqualTo(expectedWindow).Within(0.00001f));
        float openedAt = impactAt - window;
        var attempt = new ActiveDefenseAttempt();

        Assert.That(attempt.TryCommit(DefenseInputReadStatus.Valid, input,
            openedAt - 0.01f, openedAt, impactAt, enforceWindowOpening: true), Is.False);
        Assert.That(attempt.HasAttempt, Is.False);
        Assert.That(attempt.TryCommit(DefenseInputReadStatus.Valid, input,
            openedAt, openedAt, impactAt, enforceWindowOpening: true), Is.True);
        Assert.That(DefenseJudgementPolicy.EvaluateActiveDefense(request, attempt, false, impactAt)
            .PreventsDamage, Is.True, "Every allowed response must succeed at the visible cue boundary.");
        Assert.That(attempt.TryCommit(DefenseInputReadStatus.Valid, input,
            impactAt - 0.001f, openedAt, impactAt, enforceWindowOpening: true), Is.False);
    }

    [TestCase(0.5f)]
    [TestCase(1f)]
    [TestCase(2f)]
    public void ActiveCue_CommonWindowTracksDifficultyAndShortAttacks(float difficulty)
    {
        foreach (DefenseRequirement requirement in System.Enum.GetValues(typeof(DefenseRequirement)))
        foreach (float duration in new[] { 0.08f, 0.85f, 2f })
        foreach (float impactAt in new[] { 1f, 10f, 1000f })
        {
            var request = new DefenseQteRequest(duration, difficulty, requirement,
                new DefenseTimingProfile(0.12f, 0.22f, 0.4f), useActiveDefense: true);
            float openedAt = impactAt - DefenseJudgementPolicy.GetActiveCueWindow(request);
            foreach (DefenseInput input in new[] { DefenseInput.Parry, DefenseInput.Dodge, DefenseInput.Counter })
            {
                if (!DefenseJudgementPolicy.MatchesActive(requirement, input)) continue;
                var attempt = new ActiveDefenseAttempt();
                Assert.That(attempt.TryCommit(DefenseInputReadStatus.Valid, input,
                    openedAt, openedAt, impactAt, enforceWindowOpening: true), Is.True);
                Assert.That(DefenseJudgementPolicy.EvaluateActiveDefense(request, attempt, false, impactAt)
                    .PreventsDamage, Is.True, $"{requirement}/{input}, duration={duration}, difficulty={difficulty}");
            }
        }
    }

    [Test]
    public void ActiveWindows_RespectDifficultyAndDoNotGrantDodgePerfectAp()
    {
        var request = new DefenseQteRequest(1f, 2f, DefenseRequirement.Counterable,
            new DefenseTimingProfile(0.12f, 0.22f, 0.4f), useActiveDefense: true);
        DefenseQteResult dodge = DefenseJudgementPolicy.EvaluateActiveDefense(
            request, CreateActiveAttempt(DefenseInput.Dodge, 0.1f), false, 0f);
        DefenseQteResult counter = DefenseJudgementPolicy.EvaluateActiveDefense(
            request, CreateActiveAttempt(DefenseInput.Counter, 0.1f), false, 0f);
        Assert.That(dodge.PreventsDamage, Is.True);
        Assert.That(dodge.IsPerfectParry, Is.False);
        Assert.That(counter.PreventsDamage, Is.False);
    }

    private static DefenseQteRequest CreateActiveRequest(DefenseRequirement requirement = DefenseRequirement.Any)
    {
        return new DefenseQteRequest(1f, 1f, requirement,
            new DefenseTimingProfile(0.12f, 0.22f, 0.4f), useActiveDefense: true);
    }

    private static ActiveDefenseAttempt CreateActiveAttempt(DefenseInput input, float remaining)
    {
        var attempt = new ActiveDefenseAttempt();
        Assert.That(attempt.TryCommit(DefenseInputReadStatus.Valid, input, -remaining, -1f, 0f), Is.True);
        return attempt;
    }

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
