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
    Invalid,
    Guarded
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
    public bool UseTimedGuard { get; }
    public float GuardDuration { get; }
    public float GuardDamageMultiplier { get; }
    public bool UseActiveDefense { get; }
    public float DodgeWindow { get; }
    public float CounterWindow { get; }

    public DefenseQteRequest(
        float duration,
        float difficultyMultiplier,
        DefenseRequirement requirement,
        DefenseTimingProfile timingProfile,
        bool allowNearSuccess = true,
        bool useTimedGuard = false,
        float guardDuration = 0.4f,
        float guardDamageMultiplier = 0.5f,
        bool useActiveDefense = false,
        float dodgeWindow = 0.22f,
        float counterWindow = 0.18f)
    {
        Duration = Mathf.Max(0.01f, duration);
        DifficultyMultiplier = Mathf.Max(0.01f, difficultyMultiplier);
        Requirement = requirement;
        TimingProfile = timingProfile;
        AllowNearSuccess = allowNearSuccess;
        UseTimedGuard = useTimedGuard;
        GuardDuration = Mathf.Max(0.01f, guardDuration);
        GuardDamageMultiplier = Mathf.Clamp(guardDamageMultiplier, 0.01f, 1f);
        UseActiveDefense = useActiveDefense;
        DodgeWindow = Mathf.Max(0f, dodgeWindow);
        CounterWindow = Mathf.Max(0f, counterWindow);
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
    public float DamageMultiplier { get; }
    public bool IsGuard => Outcome == DefenseOutcome.Guarded;
    public bool IsPerfectParry => PreventsDamage
        && Input == DefenseInput.Parry && Grade == QTEManager.QTEGrade.Perfect;
    public bool IsJustGuard => IsPerfectParry;
    public bool IsCounterSuccess => PreventsDamage && Outcome == DefenseOutcome.Success
        && Input == DefenseInput.Counter && Requirement == DefenseRequirement.Counterable;

    public DefenseQteResult(
        DefenseInputReadStatus inputStatus,
        DefenseInput input,
        QTEManager.QTEGrade grade,
        DefenseOutcome outcome,
        DefenseRequirement requirement,
        float secondsBeforeImpact,
        bool inputMatched,
        bool preventsDamage,
        float damageMultiplier = 1f)
    {
        InputStatus = inputStatus;
        Input = input;
        Grade = grade;
        Outcome = outcome;
        Requirement = requirement;
        SecondsBeforeImpact = Mathf.Max(0f, secondsBeforeImpact);
        InputMatched = inputMatched;
        PreventsDamage = preventsDamage;
        DamageMultiplier = preventsDamage ? 0f : Mathf.Clamp01(damageMultiplier);
    }
}

/// <summary>타격 하나의 첫 입력만 소유합니다. 재입력이나 계속 누르기로 시간이 갱신되지 않습니다.</summary>
public struct TimedGuardAttempt
{
    public bool HasAttempt { get; private set; }
    public float PressedAt { get; private set; }
    public bool WasReleased { get; private set; }

    public bool TryPress(float pressedAt, float openedAt, float impactAt)
    {
        if (HasAttempt || pressedAt < openedAt || pressedAt >= impactAt)
            return false;

        HasAttempt = true;
        PressedAt = pressedAt;
        return true;
    }

    public void ObserveHeld(bool held)
    {
        if (HasAttempt && !held)
            WasReleased = true;
    }

    public float RemainingAt(float now, float duration)
    {
        return HasAttempt && !WasReleased
            ? Mathf.Max(0f, PressedAt + duration - now) : 0f;
    }
}

/// <summary>
/// 타격 하나의 능동 입력을 소유합니다. 미리 유지한 Z는 시도를 소비하지 않지만,
/// 새 Z/X/C 또는 복수 입력을 확정한 뒤에는 재입력으로 판정을 바꾸지 못합니다.
/// </summary>
public struct ActiveDefenseAttempt
{
    public bool HasAttempt { get; private set; }
    public DefenseInputReadStatus InputStatus { get; private set; }
    public DefenseInput Input { get; private set; }
    public float PressedAt { get; private set; }

    public bool TryCommit(DefenseInputReadStatus status, DefenseInput input,
        float pressedAt, float openedAt, float impactAt, bool enforceWindowOpening = false)
    {
        if (HasAttempt || status == DefenseInputReadStatus.None
            || float.IsNaN(pressedAt) || float.IsInfinity(pressedAt)
            || (enforceWindowOpening && pressedAt < openedAt)
            || pressedAt >= impactAt)
            return false;

        // 전조 연동 공격은 전조 이전 입력으로 시도를 소모하지 않습니다.
        // 레거시 호출은 기존처럼 접근 단계의 첫 입력을 보존합니다.
        // 적 행동의 접근/전조부터 PlayerController가 입력을 버퍼링할 수 있습니다.
        // 이 입력은 방어창이 열린 시각보다 앞서도 기록하되, 판정 정책의
        // `fresh` 조건에서 제외되어 X/C의 조기 성공으로 소급되지 않습니다.
        // Z는 충돌 시점에 계속 누르고 있을 때만 일반 가드로 인정됩니다.

        HasAttempt = true;
        InputStatus = status;
        Input = status == DefenseInputReadStatus.Valid ? input : DefenseInput.None;
        PressedAt = pressedAt;
        return true;
    }
}

public static class DefenseInputSelectionPolicy
{
    public static DefenseInputReadStatus ResolveActive(
        bool guardPressed, bool dodgePressed, bool counterPressed, out DefenseInput input)
    {
        DefenseInputReadStatus status = Resolve(guardPressed, dodgePressed, counterPressed, out input);
        if (input == DefenseInput.Jump)
            input = DefenseInput.Counter;
        return status;
    }

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

/// <summary>입력 버퍼와 분리된 모션 소유권. 선택된 대상만 미리보며 확정 후에는 재시작하지 않습니다.</summary>
public struct DefensePresentationGate
{
    private bool _open;
    private bool _committed;
    public void Open() { _open = true; _committed = false; }
    public void Close() { _open = false; _committed = false; }
    public void Commit() { _committed = true; }
    public bool CanPreview(bool visualBusy) => _open && !_committed && !visualBusy;
}

public static class DefenseJudgementPolicy
{
    public const float BattleLateGrace = 0.06f;
    // 실시간 적 대응에는 난이도 보정 후에도 확보되는 최소 여유를 둡니다.
    public static DefenseQteRequest WithBattleAssistance(DefenseQteRequest request, float duration)
    {
        float difficulty = request.DifficultyMultiplier;
        DefenseTimingProfile profile = request.TimingProfile;
        profile.PerfectWindow = Mathf.Max(profile.PerfectWindow, 0.24f * difficulty);
        profile.GreatWindow = Mathf.Max(profile.GreatWindow, profile.PerfectWindow);
        profile.GoodWindow = Mathf.Max(profile.GoodWindow, profile.GreatWindow);
        return new DefenseQteRequest(duration, difficulty, request.Requirement, profile,
            request.AllowNearSuccess, false, request.GuardDuration, request.GuardDamageMultiplier,
            true, Mathf.Max(request.DodgeWindow, 0.34f * difficulty),
            Mathf.Max(request.CounterWindow, 0.28f * difficulty));
    }

    public static float GetPreparationWindow(DefenseQteRequest request)
    {
        return Mathf.Max(GetActiveSuccessWindow(request, DefenseInput.Dodge), GetActiveCueWindow(request));
    }

    public static bool TryCommitBattleInput(ref ActiveDefenseAttempt attempt, DefenseQteRequest request,
        DefenseInputReadStatus status, DefenseInput input, float pressedAt, float impactAt)
    {
        // 각 키의 성공 구간 밖에서는 실패 시도를 잠그지 않습니다. 유지 Z는 별도로 처리합니다.
        float window = GetActiveSuccessWindow(request, input);
        if (status != DefenseInputReadStatus.Valid || window <= 0f) return false;
        return attempt.TryCommit(status, input, pressedAt, impactAt - window, impactAt + BattleLateGrace, true);
    }

    // 이미 성공 입력이 있으면 원래 타격 시각에 즉시 처리합니다. 없는 타격만 짧게 유예합니다.
    public static bool ShouldResolveBattleImpact(float now, float impactAt, ActiveDefenseAttempt attempt)
        => now >= impactAt && (attempt.HasAttempt || now >= impactAt + BattleLateGrace);

    /// <summary>표시와 판정이 함께 사용하는 실제 성공 구간(난이도/공격 길이 보정 후).</summary>
    public static float GetActiveSuccessWindow(DefenseQteRequest request, DefenseInput input)
    {
        if (!MatchesActive(request.Requirement, input)) return 0f;
        float perfect = request.TimingProfile.Normalize(request.Duration, request.DifficultyMultiplier).PerfectWindow;
        float window = input == DefenseInput.Parry ? perfect
            : input == DefenseInput.Dodge ? Mathf.Max(perfect, request.DodgeWindow / request.DifficultyMultiplier)
            : request.CounterWindow / request.DifficultyMultiplier;
        return Mathf.Clamp(window, 0f, request.Duration);
    }

    /// <summary>보이는 동안에는 그 공격에 허용된 모든 대응이 성공하는 공통 구간.</summary>
    public static float GetActiveCueWindow(DefenseQteRequest request)
    {
        if (MatchesActive(request.Requirement, DefenseInput.Parry))
            return GetActiveSuccessWindow(request, DefenseInput.Parry);
        if (MatchesActive(request.Requirement, DefenseInput.Counter))
            return Mathf.Min(GetActiveSuccessWindow(request, DefenseInput.Counter),
                GetActiveSuccessWindow(request, DefenseInput.Dodge));
        return GetActiveSuccessWindow(request, DefenseInput.Dodge);
    }

    public static bool MatchesActive(DefenseRequirement requirement, DefenseInput input)
    {
        switch (requirement)
        {
            case DefenseRequirement.Any:
            case DefenseRequirement.ParryOrDodge:
            case DefenseRequirement.ParryOnly:
                return input == DefenseInput.Parry || input == DefenseInput.Dodge;
            case DefenseRequirement.JumpOnly:
            case DefenseRequirement.DodgeOnly:
            case DefenseRequirement.DodgeOrJump:
                return input == DefenseInput.Dodge;
            case DefenseRequirement.Counterable:
                return input == DefenseInput.Dodge || input == DefenseInput.Counter;
            default:
                return false;
        }
    }

    public static DefenseQteResult EvaluateActiveDefense(
        DefenseQteRequest request, ActiveDefenseAttempt attempt, bool guardHeld, float impactAt,
        float lateGrace = 0f)
    {
        if (attempt.HasAttempt && (attempt.InputStatus != DefenseInputReadStatus.Valid
            || attempt.Input == DefenseInput.None))
        {
            return CreateTerminalResult(request, attempt.InputStatus, DefenseInput.None,
                QTEManager.QTEGrade.Miss, DefenseOutcome.Invalid, 0f);
        }

        float remaining = attempt.HasAttempt ? impactAt - attempt.PressedAt : 0f;
        bool fresh = attempt.HasAttempt && (attempt.PressedAt <= impactAt
                || (lateGrace > 0f && attempt.PressedAt < impactAt + lateGrace))
            && attempt.PressedAt >= impactAt - request.Duration;
        DefenseInput input = attempt.HasAttempt ? attempt.Input
            : guardHeld ? DefenseInput.Parry : DefenseInput.None;
        bool matched = MatchesActive(request.Requirement, input);
        DefenseInputReadStatus status = input == DefenseInput.None
            ? DefenseInputReadStatus.None : DefenseInputReadStatus.Valid;
        if (!matched)
        {
            return CreateTerminalResult(request, status, input, QTEManager.QTEGrade.Miss,
                input == DefenseInput.None ? DefenseOutcome.Failure : DefenseOutcome.Invalid, remaining);
        }

        float successWindow = GetActiveSuccessWindow(request, input);
        // 같은 시작 시각끼리 비교해야 경계에서 float 뺄셈 오차로 실패하지 않습니다.
        bool inSuccessWindow = fresh && attempt.PressedAt >= impactAt - successWindow;
        bool perfect = input == DefenseInput.Parry && inSuccessWindow;
        // X/C를 선택한 타격은 실패해도 유지 중인 Z로 덮지 않습니다.
        bool guarded = input == DefenseInput.Parry && !perfect && guardHeld;
        bool avoided = (input == DefenseInput.Dodge || input == DefenseInput.Counter)
            && inSuccessWindow;
        bool success = perfect || avoided;
        return new DefenseQteResult(status, input,
            perfect ? QTEManager.QTEGrade.Perfect : avoided ? QTEManager.QTEGrade.Great
                : guarded ? QTEManager.QTEGrade.Good : QTEManager.QTEGrade.Miss,
            success ? DefenseOutcome.Success : guarded ? DefenseOutcome.Guarded : DefenseOutcome.Failure,
            request.Requirement, remaining, true, success,
            guarded ? request.GuardDamageMultiplier : 1f);
    }

    public static DefenseQteResult EvaluateTimedGuard(
        DefenseQteRequest request,
        TimedGuardAttempt attempt,
        float impactAt)
    {
        float secondsBeforeImpact = impactAt - attempt.PressedAt;
        if (!attempt.HasAttempt || secondsBeforeImpact < 0f
            || secondsBeforeImpact > request.Duration)
        {
            return CreateTerminalResult(request, DefenseInputReadStatus.None,
                DefenseInput.None, QTEManager.QTEGrade.Miss, DefenseOutcome.Failure, 0f);
        }

        float perfectWindow = request.TimingProfile.Normalize(
            request.Duration, request.DifficultyMultiplier).PerfectWindow;
        // 정확한 새 입력은 짧게 눌렀다 떼어도 패링입니다. 일반 방어만 유지가 필요합니다.
        bool perfect = secondsBeforeImpact <= perfectWindow;
        bool guarded = !perfect && !attempt.WasReleased
            && secondsBeforeImpact <= request.GuardDuration;
        return new DefenseQteResult(
            DefenseInputReadStatus.Valid, DefenseInput.Parry,
            perfect ? QTEManager.QTEGrade.Perfect
                : guarded ? QTEManager.QTEGrade.Good : QTEManager.QTEGrade.Miss,
            perfect ? DefenseOutcome.Success
                : guarded ? DefenseOutcome.Guarded : DefenseOutcome.Failure,
            request.Requirement, secondsBeforeImpact, true, perfect,
            guarded ? request.GuardDamageMultiplier : 1f);
    }

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
            DefenseRequirement.Any => input == DefenseInput.Parry || input == DefenseInput.Dodge || input == DefenseInput.Jump,
            DefenseRequirement.ParryOrDodge => input == DefenseInput.Parry || input == DefenseInput.Dodge,
            DefenseRequirement.JumpOnly => input == DefenseInput.Jump,
            DefenseRequirement.ParryOnly => input == DefenseInput.Parry,
            DefenseRequirement.DodgeOnly => input == DefenseInput.Dodge,
            DefenseRequirement.DodgeOrJump => input == DefenseInput.Dodge || input == DefenseInput.Jump,
            DefenseRequirement.Counterable => input == DefenseInput.Dodge || input == DefenseInput.Counter,
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
