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
