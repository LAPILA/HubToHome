using System;

public enum PrologueTutorialStage
{
    InspectMachine, AdjustValve, TestMachine, MeetDragon, InvestigateRear,
    Battle, Rescue, Forward, EmergencyControl, Derail, Completed
}

/// <summary>저장된 완료 사실을 읽기만 합니다. 실행 중 시간·대사·전투 상태는 저장하지 않습니다.</summary>
public static class PrologueTutorialProgress
{
    public const string IntroductionCompleted = "prologue.tutorial.introduction.completed";
    public const string InspectCompleted = "prologue.tutorial.repair.inspected";
    public const string ValveCompleted = "prologue.tutorial.repair.valve";
    public const string RepairCompleted = "prologue.tutorial.repair.completed";
    public const string IncidentCompleted = "prologue.tutorial.incident.completed";
    public const string RearCompleted = "prologue.tutorial.rear.completed";
    public const string WithdrawalCompleted = "prologue.tutorial.withdrawal.completed";
    public const string RescueCompleted = "prologue.tutorial.rescue.completed";
    public const string SealCompleted = "prologue.tutorial.seal.completed";
    public const string ForwardCompleted = "prologue.tutorial.forward.completed";
    public const string ControlCompleted = "prologue.tutorial.control.completed";
    public const string Completed = "prologue.tutorial.completed";
    public const string SuppliesReady = "prologue.tutorial.supplies.ready";

    private static readonly string[] Checkpoints =
    {
        InspectCompleted, ValveCompleted, RepairCompleted, IncidentCompleted, RearCompleted,
        WithdrawalCompleted, RescueCompleted, SealCompleted, ForwardCompleted, ControlCompleted, Completed
    };

    public static string LastCompletedFlag(Func<string, int> readFlag)
    {
        if (readFlag == null) return string.Empty;
        for (int i = Checkpoints.Length - 1; i >= 0; i--)
            if (readFlag(Checkpoints[i]) != 0) return Checkpoints[i];
        return string.Empty;
    }

    public static PrologueTutorialStage ResolveStage(Func<string, int> readFlag)
    {
        string flag = LastCompletedFlag(readFlag);
        if (flag == Completed) return PrologueTutorialStage.Completed;
        if (flag == ControlCompleted) return PrologueTutorialStage.Derail;
        if (flag == ForwardCompleted) return PrologueTutorialStage.EmergencyControl;
        if (flag == SealCompleted) return PrologueTutorialStage.Forward;
        if (flag == WithdrawalCompleted || flag == RescueCompleted) return PrologueTutorialStage.Rescue;
        if (flag == RearCompleted) return PrologueTutorialStage.Battle;
        if (flag == IncidentCompleted) return PrologueTutorialStage.InvestigateRear;
        if (flag == RepairCompleted) return PrologueTutorialStage.MeetDragon;
        if (flag == ValveCompleted) return PrologueTutorialStage.TestMachine;
        if (flag == InspectCompleted) return PrologueTutorialStage.AdjustValve;
        return PrologueTutorialStage.InspectMachine;
    }

    public static bool CanInteract(PrologueTutorialInteraction kind, Func<string, int> readFlag)
    {
        PrologueTutorialStage stage = ResolveStage(readFlag);
        switch (kind)
        {
            case PrologueTutorialInteraction.InspectMachine: return stage == PrologueTutorialStage.InspectMachine;
            case PrologueTutorialInteraction.AdjustValve: return stage == PrologueTutorialStage.AdjustValve;
            case PrologueTutorialInteraction.TestMachine: return stage == PrologueTutorialStage.TestMachine;
            case PrologueTutorialInteraction.TalkToDragon: return stage == PrologueTutorialStage.MeetDragon;
            case PrologueTutorialInteraction.EnterRear: return stage == PrologueTutorialStage.InvestigateRear || stage == PrologueTutorialStage.Battle;
            case PrologueTutorialInteraction.SealDoor: return stage == PrologueTutorialStage.Rescue;
            case PrologueTutorialInteraction.UseEmergencyControl: return stage == PrologueTutorialStage.EmergencyControl || stage == PrologueTutorialStage.Derail;
            case PrologueTutorialInteraction.OptionalDialogue: return stage != PrologueTutorialStage.Completed;
            default: return false;
        }
    }

    public static bool ShouldPlayIntroduction(Func<string, int> readFlag)
    {
        if (readFlag == null || readFlag(IntroductionCompleted) != 0) return false;
        PrologueTutorialStage stage = ResolveStage(readFlag);
        return stage == PrologueTutorialStage.InspectMachine || stage == PrologueTutorialStage.AdjustValve
            || stage == PrologueTutorialStage.TestMachine;
    }

    /// <summary>객차의 실제 인접 관계와 진행 잠금을 함께 검사합니다.</summary>
    public static bool CanTravelBetween(PrologueTutorialRoom from, PrologueTutorialRoom to, Func<string, int> readFlag)
    {
        if (readFlag == null || ResolveStage(readFlag) == PrologueTutorialStage.Completed) return false;
        switch (from)
        {
            case PrologueTutorialRoom.Repair:
                return to == PrologueTutorialRoom.Common && readFlag(RepairCompleted) != 0;
            case PrologueTutorialRoom.Common:
                return to == PrologueTutorialRoom.Repair || (to == PrologueTutorialRoom.Rear
                    && readFlag(IncidentCompleted) != 0 && readFlag(SealCompleted) == 0);
            case PrologueTutorialRoom.Rear:
                return (to == PrologueTutorialRoom.Common && readFlag(SealCompleted) == 0)
                    || (to == PrologueTutorialRoom.Control && readFlag(SealCompleted) != 0);
            case PrologueTutorialRoom.Control:
                return to == PrologueTutorialRoom.Rear && readFlag(SealCompleted) == 0;
            default:
                return false;
        }
    }

    public static PrologueTutorialRoom SafeRoom(Func<string, int> readFlag)
    {
        switch (ResolveStage(readFlag))
        {
            case PrologueTutorialStage.Completed: return PrologueTutorialRoom.Completed;
            case PrologueTutorialStage.Derail: return PrologueTutorialRoom.Control;
            case PrologueTutorialStage.EmergencyControl: return PrologueTutorialRoom.Control;
            case PrologueTutorialStage.Battle:
            case PrologueTutorialStage.Rescue:
            case PrologueTutorialStage.Forward: return PrologueTutorialRoom.Rear;
            case PrologueTutorialStage.InvestigateRear: return PrologueTutorialRoom.Common;
            default: return PrologueTutorialRoom.Repair;
        }
    }
}
