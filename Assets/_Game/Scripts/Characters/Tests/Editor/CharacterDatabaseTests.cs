using System.Linq;
using NUnit.Framework;

public sealed class CharacterDatabaseTests
{
    [SetUp]
    public void SetUp()
    {
        CharacterDatabase.InvalidateCache();
    }

    [TearDown]
    public void TearDown()
    {
        CharacterDatabase.InvalidateCache();
    }

    [Test]
    public void FindByIdTrimsCallerWhitespace()
    {
        CharacterData expected = CharacterDatabase.GetAll().FirstOrDefault();
        Assert.That(expected, Is.Not.Null, "Runtime catalog must contain at least one character.");

        CharacterData actual = CharacterDatabase.FindById("  " + expected.CharacterID + "\t");

        Assert.That(actual, Is.SameAs(expected));
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("   ")]
    public void FindByIdRejectsMissingIds(string characterId)
    {
        Assert.That(CharacterDatabase.FindById(characterId), Is.Null);
    }
}
