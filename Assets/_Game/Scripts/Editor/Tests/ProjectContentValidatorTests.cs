#if UNITY_EDITOR
using NUnit.Framework;

public sealed class ProjectContentValidatorTests
{
    [TestCase("player_001")]
    [TestCase("zev.basic")]
    [TestCase("consumable.small-potion")]
    public void ContentIdPolicyAcceptsExistingProjectFormats(string value)
    {
        Assert.That(ContentIdPolicy.IsValid(value), Is.True);
    }

    [TestCase(" Player")]
    [TestCase("Player")]
    [TestCase("player/001")]
    [TestCase("player 001")]
    [TestCase("")]
    public void ContentIdPolicyRejectsAmbiguousFormats(string value)
    {
        Assert.That(ContentIdPolicy.IsValid(value), Is.False);
    }
}
#endif
