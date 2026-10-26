using System.Collections.Generic;
using NUnit.Framework;

public sealed class PrologueTutorialProgressTests
{
    private readonly Dictionary<string, int> _flags = new Dictionary<string, int>();
    private int Read(string key) => _flags.TryGetValue(key, out int value) ? value : 0;

    [SetUp] public void Reset() => _flags.Clear();

    [Test]
    public void IntroductionDoesNotReplayAfterRepairOrAnExistingCompletedIntroduction()
    {
        Assert.That(PrologueTutorialProgress.ShouldPlayIntroduction(Read));
        _flags[PrologueTutorialProgress.IntroductionCompleted] = 1;
        Assert.That(PrologueTutorialProgress.ShouldPlayIntroduction(Read), Is.False);
        Assert.That(PrologueTutorialProgress.ResolveStage(Read), Is.EqualTo(PrologueTutorialStage.InspectMachine));
        _flags.Clear();
        _flags[PrologueTutorialProgress.RepairCompleted] = 1;
        Assert.That(PrologueTutorialProgress.ShouldPlayIntroduction(Read), Is.False);
    }

    [Test]
    public void RepairRequiresThreeOrderedInteractions()
    {
        Assert.That(PrologueTutorialProgress.CanInteract(PrologueTutorialInteraction.InspectMachine, Read));
        Assert.That(PrologueTutorialProgress.CanInteract(PrologueTutorialInteraction.TestMachine, Read), Is.False);
        _flags[PrologueTutorialProgress.InspectCompleted] = 1;
        Assert.That(PrologueTutorialProgress.ResolveStage(Read), Is.EqualTo(PrologueTutorialStage.AdjustValve));
        _flags[PrologueTutorialProgress.ValveCompleted] = 1;
        Assert.That(PrologueTutorialProgress.ResolveStage(Read), Is.EqualTo(PrologueTutorialStage.TestMachine));
        _flags[PrologueTutorialProgress.RepairCompleted] = 1;
        Assert.That(PrologueTutorialProgress.ResolveStage(Read), Is.EqualTo(PrologueTutorialStage.MeetDragon));
    }

    [Test]
    public void LegacyRevealAndSuppliesDoNotCompleteNewTutorial()
    {
        _flags["prologue.train.reveal.completed"] = 1;
        _flags["prologue.opening.completed"] = 1;
        _flags[PrologueTutorialProgress.SuppliesReady] = 1;
        Assert.That(PrologueTutorialProgress.ResolveStage(Read), Is.EqualTo(PrologueTutorialStage.InspectMachine));
        Assert.That(PrologueTutorialProgress.LastCompletedFlag(Read), Is.Empty);
    }

    [Test]
    public void WithdrawalReentersAtRescueNotBattleAndSealIsIdempotent()
    {
        _flags[PrologueTutorialProgress.WithdrawalCompleted] = 1;
        Assert.That(PrologueTutorialProgress.SafeRoom(Read), Is.EqualTo(PrologueTutorialRoom.Rear));
        Assert.That(PrologueTutorialProgress.ResolveStage(Read), Is.EqualTo(PrologueTutorialStage.Rescue));
        Assert.That(PrologueTutorialProgress.CanInteract(PrologueTutorialInteraction.EnterRear, Read), Is.False);
        _flags[PrologueTutorialProgress.SealCompleted] = 1;
        _flags[PrologueTutorialProgress.SealCompleted] = 1;
        Assert.That(PrologueTutorialProgress.ResolveStage(Read), Is.EqualTo(PrologueTutorialStage.Forward));
        Assert.That(PrologueTutorialProgress.SafeRoom(Read), Is.EqualTo(PrologueTutorialRoom.Rear));
        Assert.That(PrologueTutorialProgress.CanInteract(PrologueTutorialInteraction.SealDoor, Read), Is.False);
    }

    [Test]
    public void ForwardRouteRequiresOrderedMilestonesAndNeverSkipsAnAdjacentCarriage()
    {
        Assert.That(PrologueTutorialProgress.CanTravelBetween(PrologueTutorialRoom.Repair, PrologueTutorialRoom.Common, Read), Is.False);
        _flags[PrologueTutorialProgress.RepairCompleted] = 1;
        Assert.That(PrologueTutorialProgress.CanTravelBetween(PrologueTutorialRoom.Repair, PrologueTutorialRoom.Common, Read));
        Assert.That(PrologueTutorialProgress.CanTravelBetween(PrologueTutorialRoom.Repair, PrologueTutorialRoom.Rear, Read), Is.False);
        Assert.That(PrologueTutorialProgress.CanTravelBetween(PrologueTutorialRoom.Common, PrologueTutorialRoom.Rear, Read), Is.False);
        _flags[PrologueTutorialProgress.IncidentCompleted] = 1;
        Assert.That(PrologueTutorialProgress.CanTravelBetween(PrologueTutorialRoom.Common, PrologueTutorialRoom.Rear, Read));
        Assert.That(PrologueTutorialProgress.CanTravelBetween(PrologueTutorialRoom.Rear, PrologueTutorialRoom.Control, Read), Is.False);
        Assert.That(PrologueTutorialProgress.CanTravelBetween(PrologueTutorialRoom.Common, PrologueTutorialRoom.Control, Read), Is.False);
        _flags[PrologueTutorialProgress.SealCompleted] = 1;
        Assert.That(PrologueTutorialProgress.CanTravelBetween(PrologueTutorialRoom.Rear, PrologueTutorialRoom.Control, Read));
        Assert.That(PrologueTutorialProgress.CanTravelBetween(PrologueTutorialRoom.Rear, PrologueTutorialRoom.Common, Read), Is.False);
        Assert.That(PrologueTutorialProgress.CanTravelBetween(PrologueTutorialRoom.Control, PrologueTutorialRoom.Rear, Read), Is.False);
    }

    [Test]
    public void CompletedOrUnknownCarriageCannotStartAnotherRoomTransition()
    {
        _flags[PrologueTutorialProgress.RepairCompleted] = 1;
        _flags[PrologueTutorialProgress.IncidentCompleted] = 1;
        _flags[PrologueTutorialProgress.SealCompleted] = 1;
        _flags[PrologueTutorialProgress.Completed] = 1;
        foreach (PrologueTutorialRoom from in System.Enum.GetValues(typeof(PrologueTutorialRoom)))
            foreach (PrologueTutorialRoom to in System.Enum.GetValues(typeof(PrologueTutorialRoom)))
                Assert.That(PrologueTutorialProgress.CanTravelBetween(from, to, Read), Is.False);
        Assert.That(PrologueTutorialProgress.CanTravelBetween((PrologueTutorialRoom)99, PrologueTutorialRoom.Common, Read), Is.False);
        Assert.That(PrologueTutorialProgress.CanTravelBetween(PrologueTutorialRoom.Repair, PrologueTutorialRoom.Common, null), Is.False);
    }

    [Test]
    public void CompletionAlwaysResolvesToSafeEndingWithoutReplayingControl()
    {
        _flags[PrologueTutorialProgress.ControlCompleted] = 1;
        Assert.That(PrologueTutorialProgress.ResolveStage(Read), Is.EqualTo(PrologueTutorialStage.Derail));
        _flags[PrologueTutorialProgress.Completed] = 1;
        Assert.That(PrologueTutorialProgress.ResolveStage(Read), Is.EqualTo(PrologueTutorialStage.Completed));
        Assert.That(PrologueTutorialProgress.SafeRoom(Read), Is.EqualTo(PrologueTutorialRoom.Completed));
        foreach (PrologueTutorialInteraction kind in System.Enum.GetValues(typeof(PrologueTutorialInteraction)))
            Assert.That(PrologueTutorialProgress.CanInteract(kind, Read), Is.False, kind.ToString());
    }
}
