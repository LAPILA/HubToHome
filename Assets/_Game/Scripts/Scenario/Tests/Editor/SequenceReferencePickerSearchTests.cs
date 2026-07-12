using System.Collections.Generic;
using NUnit.Framework;

public class SequenceReferencePickerSearchTests
{
    [TestCase("체력", "event.hp")]
    [TestCase("battle.hp", "event.hp")]
    [TestCase("전투", "event.hp")]
    [TestCase("phase", "condition.phase")]
    [TestCase("2페이즈", "condition.phase")]
    public void SearchMatchesHumanAndStableMetadata(string query, string expectedId)
    {
        List<SequenceReferencePickerOption> result =
            SequenceReferencePickerPopup.Search(Options(), query);

        Assert.That(result.Exists(item => item.Id == expectedId), Is.True);
    }

    [Test]
    public void EmptyQuerySortsByCategoryThenDisplayName()
    {
        List<SequenceReferencePickerOption> result =
            SequenceReferencePickerPopup.Search(Options(), string.Empty);

        Assert.That(result[0].Id, Is.EqualTo("condition.phase"));
        Assert.That(result[1].Id, Is.EqualTo("event.hp"));
    }

    [Test]
    public void UnknownQueryReturnsEmptyList()
    {
        Assert.That(
            SequenceReferencePickerPopup.Search(Options(), "존재하지않음"),
            Is.Empty);
    }

    private static List<SequenceReferencePickerOption> Options()
    {
        var hp = new SequenceReferencePickerOption
        {
            Id = "event.hp",
            DisplayNameKo = "체력 변화",
            Category = "전투",
            DescriptionKo = "참가자의 체력이 바뀔 때"
        };
        hp.Keywords.Add("damage");
        hp.Keywords.Add("battle.hp");
        var phase = new SequenceReferencePickerOption
        {
            Id = "condition.phase",
            DisplayNameKo = "페이즈 확인",
            Category = "상태",
            DescriptionKo = "현재 페이즈를 비교"
        };
        phase.Keywords.Add("2페이즈");
        return new List<SequenceReferencePickerOption> { hp, phase };
    }
}
