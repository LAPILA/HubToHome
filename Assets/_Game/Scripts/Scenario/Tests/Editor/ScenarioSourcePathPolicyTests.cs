using NUnit.Framework;

public class ScenarioSourcePathPolicyTests
{
    [TestCase("C:/outside/test.yaml")]
    [TestCase("../outside/test.yaml")]
    [TestCase("Assets/_Game/Other/test.yaml")]
    [TestCase("Assets/_Game/Content/Scenarios/Source/test.txt")]
    public void TryNormalize_RejectsUnsafePaths(string path)
    {
        Assert.That(
            ScenarioSourcePathPolicy.TryNormalize(path, out _, out _),
            Is.False);
    }

    [Test]
    public void TryNormalize_AcceptsScenarioSourceYaml()
    {
        const string path =
            "Assets/_Game/Content/Scenarios/Source/Overworld/test.sequence.yaml";

        Assert.That(
            ScenarioSourcePathPolicy.TryNormalize(path, out string normalized, out _),
            Is.True);
        Assert.That(normalized, Is.EqualTo(path));
    }
}
