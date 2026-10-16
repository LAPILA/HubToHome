using NUnit.Framework;

public sealed class LocalizationCsvTests
{
    [Test]
    public void ReadsQuotedCommaQuoteAndPhysicalNewline()
    {
        var rows = LocalizationCsv.Parse("key,KR,EN,JP,CN\r\nname,\"한,글\",\"Line 1\r\nLine \"\"2\"\"\",日本語,中文\r\n");
        Assert.That(rows.Count, Is.EqualTo(2));
        Assert.That(rows[1].Length, Is.EqualTo(5));
        Assert.That(rows[1][1], Is.EqualTo("한,글"));
        Assert.That(rows[1][2], Is.EqualTo("Line 1\r\nLine \"2\""));
    }

    [Test]
    public void PreservesEmptyTranslationsAndFinalRowWithoutNewline()
    {
        var rows = LocalizationCsv.Parse("\uFEFFkey,KR,EN,JP,CN\nname,이름,,,");
        Assert.That(rows[0][0], Is.EqualTo("key"));
        Assert.That(rows[1].Length, Is.EqualTo(5));
        Assert.That(rows[1][4], Is.Empty);
    }

    [Test]
    public void UnterminatedQuoteDoesNotPublishBrokenRow()
    {
        var rows = LocalizationCsv.Parse("key,KR,EN,JP,CN\nvalid,정상,OK,正常,正常\nbad,\"닫히지 않음");
        Assert.That(rows.Count, Is.EqualTo(2));
        Assert.That(rows[1][0], Is.EqualTo("valid"));
    }
}
