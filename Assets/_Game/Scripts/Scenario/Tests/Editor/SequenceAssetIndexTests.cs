using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

public class SequenceAssetIndexTests
{
    private readonly List<UnityEngine.Object> _created = new List<UnityEngine.Object>();

    [TearDown]
    public void TearDown()
    {
        for (int i = 0; i < _created.Count; i++)
        {
            UnityEngine.Object.DestroyImmediate(_created[i]);
        }

        _created.Clear();
    }

    [Test]
    public void BuildIndexesBattleFlowsOwnedSequencesAndStandaloneSequences()
    {
        ActionSequenceAsset owned = Sequence("battle.opening", "전투 시작");
        ActionSequenceAsset standalone = Sequence("town.arcade", "오락실 게임");
        BattleScenarioData battle = Battle("zev.encounter", "ZEV 전투", owned);

        SequenceAssetIndex index = SequenceAssetIndex.Build(
            new[] { battle },
            new[] { owned, standalone });

        Assert.That(index.BattleFlows, Has.Count.EqualTo(1));
        Assert.That(index.Sequences, Has.Count.EqualTo(2));
        Assert.That(index.FindByAsset(owned).IsStandalone, Is.False);
        Assert.That(index.FindByAsset(owned).OwningScenarioIds, Contains.Item("zev.encounter"));
        Assert.That(index.FindByAsset(standalone).IsStandalone, Is.True);
    }

    [Test]
    public void BuildDiscoversScenarioOwnedSequenceEvenWhenNotInExplicitSequenceList()
    {
        ActionSequenceAsset owned = Sequence("battle.phase", "전투 페이즈");
        BattleScenarioData battle = Battle("battle", "전투", owned);

        SequenceAssetIndex index = SequenceAssetIndex.Build(
            new[] { battle },
            Array.Empty<ActionSequenceAsset>());

        Assert.That(index.FindSequenceById("battle.phase"), Is.Not.Null);
        Assert.That(index.Sequences, Has.Count.EqualTo(1));
    }

    [Test]
    public void SearchMatchesKoreanNameIdDescriptionAndTagsWithExactIdFirst()
    {
        ActionSequenceAsset exact = Sequence("town.arcade", "다른 이름");
        ActionSequenceAsset korean = Sequence("town.game", "오락실 게임");
        korean.Contract.DescriptionKo = "마을에서 비디오 게임을 시작한다.";
        korean.Contract.Tags.Add("arcade");
        SequenceAssetIndex index = SequenceAssetIndex.Build(
            Array.Empty<BattleScenarioData>(),
            new[] { korean, exact });

        IReadOnlyList<SequenceAssetIndexEntry> exactResult = index.Search("town.arcade");
        IReadOnlyList<SequenceAssetIndexEntry> koreanResult = index.Search("오락실");
        IReadOnlyList<SequenceAssetIndexEntry> tagResult = index.Search("arcade");

        Assert.That(exactResult[0].Sequence, Is.SameAs(exact));
        Assert.That(koreanResult[0].Sequence, Is.SameAs(korean));
        Assert.That(tagResult.Exists(entry => entry.Sequence == korean), Is.True);
    }

    [Test]
    public void DuplicateSequenceIdsAreReportedWithoutDroppingEitherAsset()
    {
        ActionSequenceAsset first = Sequence("shared.call", "첫 번째");
        ActionSequenceAsset second = Sequence("shared.call", "두 번째");

        SequenceAssetIndex index = SequenceAssetIndex.Build(
            Array.Empty<BattleScenarioData>(),
            new[] { first, second });

        Assert.That(index.Sequences, Has.Count.EqualTo(2));
        Assert.That(index.DuplicateSequenceIds, Contains.Item("shared.call"));
        Assert.That(index.FindSequencesById("shared.call"), Has.Count.EqualTo(2));
    }

    [Test]
    public void RecentHistoryDeduplicatesAndKeepsNewestFirst()
    {
        ActionSequenceAsset first = Sequence("first", "첫 번째");
        ActionSequenceAsset second = Sequence("second", "두 번째");
        SequenceAssetIndex index = SequenceAssetIndex.Build(
            Array.Empty<BattleScenarioData>(),
            new[] { first, second });
        var history = new SequenceNavigatorHistory(new MemoryPreferences(), 3);

        history.RecordOpened(index.FindByAsset(first).StableKey);
        history.RecordOpened(index.FindByAsset(second).StableKey);
        history.RecordOpened(index.FindByAsset(first).StableKey);

        IReadOnlyList<SequenceAssetIndexEntry> recent = history.ResolveRecent(index);
        Assert.That(recent, Has.Count.EqualTo(2));
        Assert.That(recent[0].Sequence, Is.SameAs(first));
        Assert.That(recent[1].Sequence, Is.SameAs(second));
    }

    [Test]
    public void FavoritesUseStableAssetKeySoSequenceRenameDoesNotLoseFavorite()
    {
        ActionSequenceAsset sequence = Sequence("old.id", "이전 이름");
        var preferences = new MemoryPreferences();
        SequenceAssetIndex before = SequenceAssetIndex.Build(
            Array.Empty<BattleScenarioData>(),
            new[] { sequence });
        var history = new SequenceNavigatorHistory(preferences);
        history.SetFavorite(before.FindByAsset(sequence).StableKey, true);

        sequence.SequenceId = "new.id";
        sequence.DisplayNameKo = "새 이름";
        SequenceAssetIndex after = SequenceAssetIndex.Build(
            Array.Empty<BattleScenarioData>(),
            new[] { sequence });
        var restored = new SequenceNavigatorHistory(preferences);

        IReadOnlyList<SequenceAssetIndexEntry> favorites = restored.ResolveFavorites(after);
        Assert.That(favorites, Has.Count.EqualTo(1));
        Assert.That(favorites[0].SequenceId, Is.EqualTo("new.id"));
    }

    [Test]
    public void RemovedAssetsAreIgnoredWhenResolvingStoredHistory()
    {
        var preferences = new MemoryPreferences();
        preferences.SetString(SequenceNavigatorHistory.RecentKey, "missing-key");
        preferences.SetString(SequenceNavigatorHistory.FavoritesKey, "missing-key");
        var history = new SequenceNavigatorHistory(preferences);
        SequenceAssetIndex empty = SequenceAssetIndex.Build(
            Array.Empty<BattleScenarioData>(),
            Array.Empty<ActionSequenceAsset>());

        Assert.That(history.ResolveRecent(empty), Is.Empty);
        Assert.That(history.ResolveFavorites(empty), Is.Empty);
    }

    private ActionSequenceAsset Sequence(string id, string displayName)
    {
        ActionSequenceAsset sequence = ScriptableObject.CreateInstance<ActionSequenceAsset>();
        sequence.SequenceId = id;
        sequence.DisplayNameKo = displayName;
        sequence.name = id;
        _created.Add(sequence);
        return sequence;
    }

    private BattleScenarioData Battle(
        string id,
        string title,
        params ActionSequenceAsset[] sequences)
    {
        BattleScenarioData battle = ScriptableObject.CreateInstance<BattleScenarioData>();
        battle.ScenarioId = id;
        battle.TitleKo = title;
        battle.Sequences.AddRange(sequences);
        battle.name = id;
        _created.Add(battle);
        return battle;
    }

    private sealed class MemoryPreferences : ISequenceMakerPreferences
    {
        private readonly Dictionary<string, object> _values =
            new Dictionary<string, object>(StringComparer.Ordinal);

        public float GetFloat(string key, float defaultValue) =>
            _values.TryGetValue(key, out object value) && value is float typed ? typed : defaultValue;
        public bool GetBool(string key, bool defaultValue) =>
            _values.TryGetValue(key, out object value) && value is bool typed ? typed : defaultValue;
        public string GetString(string key, string defaultValue) =>
            _values.TryGetValue(key, out object value) && value is string typed ? typed : defaultValue;
        public void SetFloat(string key, float value) => _values[key] = value;
        public void SetBool(string key, bool value) => _values[key] = value;
        public void SetString(string key, string value) => _values[key] = value;
    }
}

internal static class SequenceAssetIndexTestExtensions
{
    public static bool Exists(
        this IReadOnlyList<SequenceAssetIndexEntry> entries,
        Predicate<SequenceAssetIndexEntry> predicate)
    {
        for (int i = 0; i < entries.Count; i++)
        {
            if (predicate(entries[i]))
            {
                return true;
            }
        }

        return false;
    }
}
