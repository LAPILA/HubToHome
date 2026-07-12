using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

public class ActionPickerSearchTests
{
    private ActionCatalogAsset _catalog;

    [SetUp]
    public void SetUp()
    {
        _catalog = ScriptableObject.CreateInstance<ActionCatalogAsset>();
    }

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(_catalog);
    }

    [Test]
    public void ExactKoreanNameAndExactIdRankAheadOfPartialMatches()
    {
        _catalog.Entries.Add(Entry("actor.move", "캐릭터 이동", "actor"));
        _catalog.Entries.Add(Entry("actor.move_relative", "캐릭터 상대 이동", "actor"));

        IReadOnlyList<ActionPickerSearchResult> korean = ActionPickerSearch.Search(
            _catalog,
            "캐릭터 이동",
            new ActionPickerContext("battle"));
        IReadOnlyList<ActionPickerSearchResult> id = ActionPickerSearch.Search(
            _catalog,
            "actor.move",
            new ActionPickerContext("battle"));

        Assert.That(korean[0].Entry.ActionId, Is.EqualTo("actor.move"));
        Assert.That(id[0].Entry.ActionId, Is.EqualTo("actor.move"));
    }

    [Test]
    public void DescriptionTagsAliasesAndParameterNamesAreSearchable()
    {
        ActionCatalogEntry entry = Entry("screen.fade", "화면 전환", "screen");
        entry.DescriptionKo = "화면을 흰색으로 서서히 덮습니다.";
        entry.Tags.Add("transition");
        entry.Aliases.Add("암전");
        entry.Parameters.Add(new ActionCatalogParameter
        {
            Name = "duration",
            DisplayNameKo = "전환 시간",
            DescriptionKo = "페이드에 걸리는 초"
        });
        _catalog.Entries.Add(entry);

        AssertMatch("흰색", "screen.fade");
        AssertMatch("transition", "screen.fade");
        AssertMatch("암전", "screen.fade");
        AssertMatch("duration", "screen.fade");
        AssertMatch("전환 시간", "screen.fade");
    }

    [Test]
    public void CompatibleActionsRankBeforeHigherTextScoreFromWrongMode()
    {
        ActionCatalogEntry wrongMode = Entry("battle.camera.focus", "카메라 초점", "camera");
        wrongMode.AllowedPrimaryModes.Add("battle");
        ActionCatalogEntry compatible = Entry("overworld.camera.focus", "필드 카메라 초점", "camera");
        compatible.AllowedPrimaryModes.Add("overworld");
        _catalog.Entries.Add(wrongMode);
        _catalog.Entries.Add(compatible);

        IReadOnlyList<ActionPickerSearchResult> results = ActionPickerSearch.Search(
            _catalog,
            "카메라 초점",
            new ActionPickerContext("overworld"));

        Assert.That(results[0].Entry, Is.SameAs(compatible));
        Assert.That(results[0].Compatibility, Is.EqualTo(ActionPickerCompatibility.Compatible));
        Assert.That(results[1].Compatibility, Is.EqualTo(ActionPickerCompatibility.Unavailable));
        Assert.That(results[1].CompatibilityReason, Does.Contain("battle"));
    }

    [Test]
    public void MissingKnownRuntimeContextIsVisibleWithReason()
    {
        ActionCatalogEntry entry = Entry("dialogue.wait", "대화하고 기다리기", "dialogue");
        entry.RequiredContexts.Add("dialogue_runner");
        _catalog.Entries.Add(entry);
        var context = new ActionPickerContext("battle", new[] { "clock" });

        IReadOnlyList<ActionPickerSearchResult> results = ActionPickerSearch.Search(
            _catalog,
            "대화",
            context);

        Assert.That(results, Has.Count.EqualTo(1));
        Assert.That(results[0].Compatibility, Is.EqualTo(ActionPickerCompatibility.Unavailable));
        Assert.That(results[0].CompatibilityReason, Does.Contain("dialogue_runner"));
    }

    [Test]
    public void DeprecatedAndDisabledActionsRemainDiscoverable()
    {
        ActionCatalogEntry deprecated = Entry("old.fade", "이전 페이드", "screen");
        deprecated.Deprecated = true;
        deprecated.ReplacementActionId = "screen.fade";
        ActionCatalogEntry disabled = Entry("debug.only", "디버그 액션", "debug");
        disabled.Disabled = true;
        _catalog.Entries.Add(deprecated);
        _catalog.Entries.Add(disabled);

        IReadOnlyList<ActionPickerSearchResult> oldResults = ActionPickerSearch.Search(
            _catalog,
            "이전",
            new ActionPickerContext("battle"));
        IReadOnlyList<ActionPickerSearchResult> debugResults = ActionPickerSearch.Search(
            _catalog,
            "디버그",
            new ActionPickerContext("battle"));

        Assert.That(oldResults[0].Compatibility, Is.EqualTo(ActionPickerCompatibility.Deprecated));
        Assert.That(oldResults[0].CompatibilityReason, Does.Contain("screen.fade"));
        Assert.That(debugResults[0].Compatibility, Is.EqualTo(ActionPickerCompatibility.Unavailable));
        Assert.That(debugResults[0].CompatibilityReason, Does.Contain("중지"));
    }

    [Test]
    public void EmptyQueryReturnsAllEntriesInStableCategoryOrder()
    {
        _catalog.Entries.Add(Entry("flow.wait", "기다리기", "flow"));
        _catalog.Entries.Add(Entry("actor.move", "이동", "actor"));

        IReadOnlyList<ActionPickerSearchResult> results = ActionPickerSearch.Search(
            _catalog,
            string.Empty,
            new ActionPickerContext("battle"));

        Assert.That(results, Has.Count.EqualTo(2));
        Assert.That(results[0].Entry.ActionId, Is.EqualTo("actor.move"));
        Assert.That(results[1].Entry.ActionId, Is.EqualTo("flow.wait"));
    }

    [Test]
    public void EverySearchTokenMustMatchSomeMetadataField()
    {
        ActionCatalogEntry entry = Entry("actor.move", "캐릭터 이동", "actor");
        entry.Tags.Add("battle");
        _catalog.Entries.Add(entry);

        Assert.That(ActionPickerSearch.Search(
            _catalog,
            "캐릭터 battle",
            new ActionPickerContext("battle")), Has.Count.EqualTo(1));
        Assert.That(ActionPickerSearch.Search(
            _catalog,
            "캐릭터 audio",
            new ActionPickerContext("battle")), Is.Empty);
    }

    private void AssertMatch(string query, string expectedId)
    {
        IReadOnlyList<ActionPickerSearchResult> results = ActionPickerSearch.Search(
            _catalog,
            query,
            new ActionPickerContext("battle"));
        Assert.That(results, Is.Not.Empty);
        Assert.That(results[0].Entry.ActionId, Is.EqualTo(expectedId));
    }

    private static ActionCatalogEntry Entry(string id, string name, string category)
    {
        return new ActionCatalogEntry
        {
            ActionId = id,
            DisplayNameKo = name,
            Category = category,
            DescriptionKo = name + " 설명",
            UsageKo = name + " 사용 시점"
        };
    }
}
