#if UNITY_EDITOR
using System.Linq;
using NUnit.Framework;

public sealed class AssetDatabaseContentSourceTests
{
    [Test]
    public void CaptureLoadsSupportedProjectAssetsInStablePathOrder()
    {
        ProjectContentSnapshot snapshot = AssetDatabaseContentSource.Capture();

        Assert.That(snapshot.Catalog, Is.Not.Null);
        Assert.That(snapshot.Characters, Is.Not.Empty);
        Assert.That(snapshot.Enemies, Is.Not.Empty);
        Assert.That(snapshot.Skills, Is.Not.Empty);
        Assert.That(snapshot.Items, Is.Not.Empty);
        Assert.That(snapshot.ActionCatalogs, Is.Not.Empty);

        string[] skillPaths = snapshot.Skills
            .Select(snapshot.GetAssetPath)
            .ToArray();
        Assert.That(skillPaths, Is.EqualTo(skillPaths.OrderBy(path => path, System.StringComparer.Ordinal)));
    }
}
#endif
