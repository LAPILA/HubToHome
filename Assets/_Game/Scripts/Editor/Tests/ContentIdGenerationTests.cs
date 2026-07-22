#if UNITY_EDITOR
using System.Collections.Generic;
using NUnit.Framework;

public sealed class ContentIdGenerationTests
{
    [Test]
    public void GeneratedIdUsesAsciiSlugAndStableSuffix()
    {
        string id = ContentIdPolicy.CreateGeneratedId(
            "enemy",
            "DB Slime!",
            "ABCDEF12",
            new HashSet<string>());

        Assert.That(id, Is.EqualTo("enemy_db_slime_abcdef12"));
    }

    [Test]
    public void GeneratedIdFallsBackForNonAsciiNameAndAvoidsReservedIds()
    {
        var reserved = new HashSet<string>
        {
            "item_content_12345678",
            "item_content_12345678_2"
        };

        string id = ContentIdPolicy.CreateGeneratedId(
            "item",
            "회복약",
            "12345678",
            reserved);

        Assert.That(id, Is.EqualTo("item_content_12345678_3"));
    }
}
#endif
