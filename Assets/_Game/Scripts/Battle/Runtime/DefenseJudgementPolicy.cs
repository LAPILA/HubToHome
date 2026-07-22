using System;
using UnityEngine;

public enum DefenseInputReadStatus
{
    None,
    Valid,
    Ambiguous
}

public enum DefenseOutcome
{
    Success,
    NearSuccess,
    Failure,
    Invalid
}

[Serializable]
public struct DefenseTimingProfile
{
    [Min(0f), InspectorName("Perfect 판정 (초)")]
    public float PerfectWindow;
    [Min(0f), InspectorName("Great 판정 (초)")]
    public float GreatWindow;
    [Min(0f), InspectorName("Good 판정 (초)")]
    public float GoodWindow;

    public DefenseTimingProfile(float perfectWindow, float greatWindow, float goodWindow)
    {
        PerfectWindow = perfectWindow;
        GreatWindow = greatWindow;
        GoodWindow = goodWindow;
    }

    public DefenseTimingProfile Normalize(float duration, float difficultyMultiplier)
    {
        float safeDuration = Mathf.Max(0.01f, duration);
        float safeDifficulty = Mathf.Max(0.01f, difficultyMultiplier);
        float perfect = Mathf.Clamp(Mathf.Max(0f, PerfectWindow) / safeDifficulty, 0f, safeDuration);
        float great = Mathf.Clamp(
            Mathf.Max(perfect, Mathf.Max(0f, GreatWindow) / safeDifficulty),
            perfect,
            safeDuration);
        float good = Mathf.Clamp(
            Mathf.Max(great, Mathf.Max(0f, GoodWindow) / safeDifficulty),
            great,
            safeDuration);
        return new DefenseTimingProfile(perfect, great, good);
    }
}

public readonly struct DefenseQteRequest
{
    public float Duration { get; }
    public float DifficultyMultiplier { get; }
    public DefenseRequirement Requirement { get; }
    public DefenseTimingProfile TimingProfile { get; }
    public bool AllowNearSuccess { get; }

    public DefenseQteRequest(
        float duration,
        float difficultyMultiplier,
        DefenseRequirement requirement,
        DefenseTimingProfile timingProfile,
        bool allowNearSuccess = true)
    {
        Duration = Mathf.Max(0.01f, duration);
        DifficultyMultiplier = Mathf.Max(0.01f, difficultyMultiplier);
        Requirement = requirement;
        TimingProfile = timingProfile;
        AllowNearSuccess = allowNearSuccess;
    }
}

public readonly struct DefenseQteResult
{
    public DefenseInputReadStatus InputStatus { get; }
    public DefenseInput Input { get; }
    public QTEManager.QTEGrade Grade { get; }
    public DefenseOutcome Outcome { get; }
    public DefenseRequirement Requirement { get; }
    public float SecondsBeforeImpact { get; }
    public bool InputMatched { get; }
    public bool PreventsDamage { get; }

    public DefenseQteResult(
        DefenseInputReadStatus inputStatus,
        DefenseInput input,
        QTEManager.QTEGrade grade,
        DefenseOutcome outcome,
        DefenseRequirement requirement,
        float secondsBeforeImpact,
        bool inputMatched,
        bool preventsDamage)
    {
        InputStatus = inputStatus;
        Input = input;
        Grade = grade;
        Outcome = outcome;
        Requirement = requirement;
        SecondsBeforeImpact = Mathf.Max(0f, secondsBeforeImpact);
        InputMatched = inputMatched;
        PreventsDamage = preventsDamage;
    }
}

public static class DefenseInputSelectionPolicy
{
    public static DefenseInputReadStatus Resolve(
        bool parryPressed,
        bool dodgePressed,
        bool jumpPressed,
        out DefenseInput input)
    {
        input = DefenseInput.None;
        int pressedCount = (parryPressed ? 1 : 0)
            + (dodgePressed ? 1 : 0)
            + (jumpPressed ? 1 : 0);

        if (pressedCount == 0)
            return DefenseInputReadStatus.None;
        if (pressedCount > 1)
            return DefenseInputReadStatus.Ambiguous;

        input = parryPressed
            ? DefenseInput.Parry
            : dodgePressed
                ? DefenseInput.Dodge
                : DefenseInput.Jump;
        return DefenseInputReadStatus.Valid;
    }
}

public static class DefenseJudgementPolicy
{
    public static DefenseQteResult Evaluate(
        DefenseQteRequest request,
        DefenseInputReadStatus inputStatus,
        DefenseInput input,
        float secondsBeforeImpact)
    {
        if (inputStatus == DefenseInputReadStatus.None)
        {
            return CreateTerminalResult(
                request,
                inputStatus,
                DefenseInput.None,
                QTEManager.QTEGrade.Miss,
                DefenseOutcome.Failure,
                secondsBeforeImpact);
        }

        if (inputStatus == DefenseInputReadStatus.Ambiguous || input == DefenseInput.None)
        {
            return CreateTerminalResult(
                request,
                inputStatus,
                DefenseInput.None,
                QTEManager.QTEGrade.Miss,
                DefenseOutcome.Invalid,
                secondsBeforeImpact);
        }

        QTEManager.QTEGrade grade = EvaluateGrade(request, secondsBeforeImpact);
        bool inputMatched = Matches(request.Requirement, input);
        if (!inputMatched)
        {
            return new DefenseQteResult(
                inputStatus,
                input,
                grade,
                DefenseOutcome.Invalid,
                request.Requirement,
                secondsBeforeImpact,
                false,
                false);
        }

        DefenseOutcome outcome = grade == QTEManager.QTEGrade.Bad
            ? DefenseOutcome.NearSuccess
            : DefenseOutcome.Success;
        bool preventsDamage = outcome == DefenseOutcome.Success
            || (outcome == DefenseOutcome.NearSuccess && request.AllowNearSuccess);

        return new DefenseQteResult(
            inputStatus,
            input,
            grade,
            outcome,
            request.Requirement,
            secondsBeforeImpact,
            true,
            preventsDamage);
    }

    public static bool Matches(DefenseRequirement requirement, DefenseInput input)
    {
        return requirement switch
        {
            DefenseRequirement.Any => input != DefenseInput.None,
            DefenseRequirement.ParryOrDodge => input == DefenseInput.Parry || input == DefenseInput.Dodge,
            DefenseRequirement.JumpOnly => input == DefenseInput.Jump,
            DefenseRequirement.ParryOnly => input == DefenseInput.Parry,
            DefenseRequirement.DodgeOnly => input == DefenseInput.Dodge,
            DefenseRequirement.DodgeOrJump => input == DefenseInput.Dodge || input == DefenseInput.Jump,
            _ => false
        };
    }

    private static QTEManager.QTEGrade EvaluateGrade(
        DefenseQteRequest request,
        float secondsBeforeImpact)
    {
        DefenseTimingProfile timing = request.TimingProfile.Normalize(
            request.Duration,
            request.DifficultyMultiplier);
        float remaining = Mathf.Max(0f, secondsBeforeImpact);

        if (remaining <= timing.PerfectWindow) return QTEManager.QTEGrade.Perfect;
        if (remaining <= timing.GreatWindow) return QTEManager.QTEGrade.Great;
        if (remaining <= timing.GoodWindow) return QTEManager.QTEGrade.Good;
        return QTEManager.QTEGrade.Bad;
    }

    private static DefenseQteResult CreateTerminalResult(
        DefenseQteRequest request,
        DefenseInputReadStatus inputStatus,
        DefenseInput input,
        QTEManager.QTEGrade grade,
        DefenseOutcome outcome,
        float secondsBeforeImpact)
    {
        return new DefenseQteResult(
            inputStatus,
            input,
            grade,
            outcome,
            request.Requirement,
            secondsBeforeImpact,
            false,
            false);
    }
}
