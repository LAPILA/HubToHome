using NUnit.Framework;

public sealed class PrologueTutorialIntroductionOwnershipTests
{
    [Test]
    public void RestoreRequiresSameRoomAndLeaseEvenForExplicitCancellation()
    {
        for (int bits = 0; bits < 32; bits++)
        {
            bool sameRoom = (bits & 1) != 0;
            bool sameGeneration = (bits & 2) != 0;
            bool ownsLease = (bits & 4) != 0;
            bool ownedCancellation = (bits & 8) != 0;
            bool battleActive = (bits & 16) != 0;
            bool expected = sameRoom && ownsLease && !battleActive && (sameGeneration || ownedCancellation);
            Assert.That(PrologueTutorialIntroductionOwnership.CanRestore(sameRoom, sameGeneration, ownsLease,
                ownedCancellation, battleActive), Is.EqualTo(expected), bits.ToString());
        }
        Assert.That(PrologueTutorialIntroductionOwnership.CanRestore(true, false, true, true, false));
        Assert.That(PrologueTutorialIntroductionOwnership.CanRestore(false, true, true, true, false), Is.False);
        Assert.That(PrologueTutorialIntroductionOwnership.CanRestore(true, false, true, false, false), Is.False);
        Assert.That(PrologueTutorialIntroductionOwnership.CanRestore(true, true, false, true, false), Is.False);
    }
}
