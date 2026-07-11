using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public enum SequenceAssetIndexEntryKind
{
    BattleFlow,
    Sequence
}

public sealed class SequenceAssetIndexEntry
{
    internal SequenceAssetIndexEntry(
        SequenceAssetIndexEntryKind kind,
        string stableKey,
        string assetPath,
        BattleScenarioData battleScenario,
        ActionSequenceAsset sequence,
        string id,
        string displayNameKo,
        string descriptionKo,
        IList<string> tags,
        IList<string> owningScenarioIds)
    {
        Kind = kind;
        StableKey = stableKey ?? string.Empty;
        AssetPath = assetPath ?? string.Empty;
        BattleScenario = battleScenario;
        Sequence = sequence;
        Id = id ?? string.Empty;
        DisplayNameKo = displayNameKo ?? string.Empty;
        DescriptionKo = descriptionKo ?? string.Empty;
        Tags = new List<string>(tags ?? Array.Empty<string>());
        OwningScenarioIds = new List<string>(owningScenarioIds ?? Array.Empty<string>());
    }

    public SequenceAssetIndexEntryKind Kind { get; }
    public string StableKey { get; }
    public string AssetPath { get; }
    public BattleScenarioData BattleScenario { get; }
    public ActionSequenceAsset Sequence { get; }
    public string Id { get; }
    public string SequenceId => Kind == SequenceAssetIndexEntryKind.Sequence ? Id : string.Empty;
    public string ScenarioId => Kind == SequenceAssetIndexEntryKind.BattleFlow ? Id : string.Empty;
    public string DisplayNameKo { get; }
    public string DescriptionKo { get; }
    public IReadOnlyList<string> Tags { get; }
    public IReadOnlyList<string> OwningScenarioIds { get; }
    public bool IsStandalone => Kind == SequenceAssetIndexEntryKind.Sequence
        && OwningScenarioIds.Count == 0;
    public UnityEngine.Object Asset => Kind == SequenceAssetIndexEntryKind.BattleFlow
        ? BattleScenario
        : (UnityEngine.Object)Sequence;
}

public sealed class SequenceAssetIndex
{
    private readonly List<SequenceAssetIndexEntry> _entries =
        new List<SequenceAssetIndexEntry>();
    private readonly List<SequenceAssetIndexEntry> _battleFlows =
        new List<SequenceAssetIndexEntry>();
    private readonly List<SequenceAssetIndexEntry> _sequences =
        new List<SequenceAssetIndexEntry>();
    private readonly Dictionary<int, SequenceAssetIndexEntry> _byInstanceId =
        new Dictionary<int, SequenceAssetIndexEntry>();
    private readonly Dictionary<string, SequenceAssetIndexEntry> _byStableKey =
        new Dictionary<string, SequenceAssetIndexEntry>(StringComparer.Ordinal);
    private readonly Dictionary<string, List<SequenceAssetIndexEntry>> _sequencesById =
        new Dictionary<string, List<SequenceAssetIndexEntry>>(StringComparer.Ordinal);
    private readonly List<string> _duplicateSequenceIds = new List<string>();

    private SequenceAssetIndex()
    {
    }

    public IReadOnlyList<SequenceAssetIndexEntry> Entries => _entries;
    public IReadOnlyList<SequenceAssetIndexEntry> BattleFlows => _battleFlows;
    public IReadOnlyList<SequenceAssetIndexEntry> Sequences => _sequences;
    public IReadOnlyList<string> DuplicateSequenceIds => _duplicateSequenceIds;

    public static SequenceAssetIndex Build(
        IEnumerable<BattleScenarioData> battleScenarios,
        IEnumerable<ActionSequenceAsset> sequences,
        Func<UnityEngine.Object, string> stableKeyResolver = null,
        Func<UnityEngine.Object, string> assetPathResolver = null)
    {
        stableKeyResolver = stableKeyResolver ?? DefaultStableKey;
        assetPathResolver = assetPathResolver ?? (_ => string.Empty);
        var result = new SequenceAssetIndex();
        var battles = DistinctObjects(battleScenarios);
        var sequenceAssets = DistinctObjects(sequences);
        var ownerIds = new Dictionary<int, List<string>>();

        for (int i = 0; i < battles.Count; i++)
        {
            BattleScenarioData battle = battles[i];
            if (battle == null)
            {
                continue;
            }

            if (battle.Sequences != null)
            {
                for (int j = 0; j < battle.Sequences.Count; j++)
                {
                    ActionSequenceAsset sequence = battle.Sequences[j];
                    if (sequence == null)
                    {
                        continue;
                    }

                    AddDistinct(sequenceAssets, sequence);
                    if (!ownerIds.TryGetValue(sequence.GetInstanceID(), out List<string> owners))
                    {
                        owners = new List<string>();
                        ownerIds.Add(sequence.GetInstanceID(), owners);
                    }

                    string scenarioId = Normalize(battle.ScenarioId);
                    if (!string.IsNullOrEmpty(scenarioId) && !owners.Contains(scenarioId))
                    {
                        owners.Add(scenarioId);
                    }
                }
            }

            var entry = new SequenceAssetIndexEntry(
                SequenceAssetIndexEntryKind.BattleFlow,
                stableKeyResolver(battle),
                assetPathResolver(battle),
                battle,
                null,
                Normalize(battle.ScenarioId),
                battle.TitleKo,
                BuildBattleDescription(battle),
                Array.Empty<string>(),
                Array.Empty<string>());
            result.Add(entry);
        }

        for (int i = 0; i < sequenceAssets.Count; i++)
        {
            ActionSequenceAsset sequence = sequenceAssets[i];
            if (sequence == null)
            {
                continue;
            }

            ownerIds.TryGetValue(sequence.GetInstanceID(), out List<string> owners);
            ActionSequenceContractData contract = sequence.Contract ?? new ActionSequenceContractData();
            var entry = new SequenceAssetIndexEntry(
                SequenceAssetIndexEntryKind.Sequence,
                stableKeyResolver(sequence),
                assetPathResolver(sequence),
                null,
                sequence,
                Normalize(sequence.SequenceId),
                sequence.DisplayNameKo,
                contract.DescriptionKo,
                contract.Tags,
                owners ?? new List<string>());
            result.Add(entry);
        }

        result.SortAndFinalizeDuplicates();
        return result;
    }

    public static SequenceAssetIndex BuildFromAssetDatabase()
    {
        var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        AddPaths(paths, AssetDatabase.FindAssets("t:BattleScenarioData"));
        AddPaths(paths, AssetDatabase.FindAssets("t:ActionSequenceAsset"));

        var battles = new List<BattleScenarioData>();
        var sequences = new List<ActionSequenceAsset>();
        foreach (string path in paths)
        {
            UnityEngine.Object[] assets = AssetDatabase.LoadAllAssetsAtPath(path);
            for (int i = 0; i < assets.Length; i++)
            {
                if (assets[i] is BattleScenarioData battle)
                {
                    AddDistinct(battles, battle);
                }
                else if (assets[i] is ActionSequenceAsset sequence)
                {
                    AddDistinct(sequences, sequence);
                }
            }
        }

        return Build(battles, sequences, AssetStableKey, AssetDatabase.GetAssetPath);
    }

    public SequenceAssetIndexEntry FindByAsset(UnityEngine.Object asset)
    {
        if (asset == null)
        {
            return null;
        }

        _byInstanceId.TryGetValue(asset.GetInstanceID(), out SequenceAssetIndexEntry entry);
        return entry;
    }

    public SequenceAssetIndexEntry FindByStableKey(string stableKey)
    {
        _byStableKey.TryGetValue(Normalize(stableKey), out SequenceAssetIndexEntry entry);
        return entry;
    }

    public SequenceAssetIndexEntry FindSequenceById(string sequenceId)
    {
        IReadOnlyList<SequenceAssetIndexEntry> matches = FindSequencesById(sequenceId);
        return matches.Count > 0 ? matches[0] : null;
    }

    public IReadOnlyList<SequenceAssetIndexEntry> FindSequencesById(string sequenceId)
    {
        return _sequencesById.TryGetValue(
            Normalize(sequenceId),
            out List<SequenceAssetIndexEntry> matches)
            ? matches
            : (IReadOnlyList<SequenceAssetIndexEntry>)Array.Empty<SequenceAssetIndexEntry>();
    }

    public IReadOnlyList<SequenceAssetIndexEntry> Search(string query)
    {
        string normalized = Normalize(query);
        if (string.IsNullOrEmpty(normalized))
        {
            return new List<SequenceAssetIndexEntry>(_entries);
        }

        string[] tokens = normalized.Split(
            new[] { ' ' },
            StringSplitOptions.RemoveEmptyEntries);
        var hits = new List<SearchHit>();
        for (int i = 0; i < _entries.Count; i++)
        {
            int score = Score(_entries[i], tokens, normalized);
            if (score >= 0)
            {
                hits.Add(new SearchHit(_entries[i], score));
            }
        }

        hits.Sort((left, right) =>
        {
            int scoreCompare = right.Score.CompareTo(left.Score);
            return scoreCompare != 0
                ? scoreCompare
                : CompareEntries(left.Entry, right.Entry);
        });
        var result = new List<SequenceAssetIndexEntry>(hits.Count);
        for (int i = 0; i < hits.Count; i++)
        {
            result.Add(hits[i].Entry);
        }

        return result;
    }

    private void Add(SequenceAssetIndexEntry entry)
    {
        if (entry == null || entry.Asset == null)
        {
            return;
        }

        _entries.Add(entry);
        _byInstanceId[entry.Asset.GetInstanceID()] = entry;
        if (!string.IsNullOrEmpty(entry.StableKey))
        {
            _byStableKey[entry.StableKey] = entry;
        }

        if (entry.Kind == SequenceAssetIndexEntryKind.BattleFlow)
        {
            _battleFlows.Add(entry);
            return;
        }

        _sequences.Add(entry);
        if (!_sequencesById.TryGetValue(entry.SequenceId, out List<SequenceAssetIndexEntry> matches))
        {
            matches = new List<SequenceAssetIndexEntry>();
            _sequencesById.Add(entry.SequenceId, matches);
        }

        matches.Add(entry);
    }

    private void SortAndFinalizeDuplicates()
    {
        _entries.Sort(CompareEntries);
        _battleFlows.Sort(CompareEntries);
        _sequences.Sort(CompareEntries);
        foreach (KeyValuePair<string, List<SequenceAssetIndexEntry>> pair in _sequencesById)
        {
            pair.Value.Sort(CompareEntries);
            if (!string.IsNullOrEmpty(pair.Key) && pair.Value.Count > 1)
            {
                _duplicateSequenceIds.Add(pair.Key);
            }
        }

        _duplicateSequenceIds.Sort(StringComparer.Ordinal);
    }

    private static int Score(
        SequenceAssetIndexEntry entry,
        IList<string> tokens,
        string fullQuery)
    {
        int score = 0;
        string id = entry.Id ?? string.Empty;
        if (string.Equals(id, fullQuery, StringComparison.OrdinalIgnoreCase))
        {
            score += 1000;
        }
        else if (id.StartsWith(fullQuery, StringComparison.OrdinalIgnoreCase))
        {
            score += 500;
        }

        for (int i = 0; i < tokens.Count; i++)
        {
            string token = tokens[i];
            int tokenScore = Math.Max(
                MatchScore(entry.Id, token, 120),
                Math.Max(
                    MatchScore(entry.DisplayNameKo, token, 100),
                    Math.Max(
                        MatchScore(entry.DescriptionKo, token, 45),
                        Math.Max(
                            MatchTags(entry.Tags, token),
                            MatchScore(entry.AssetPath, token, 15)))));
            if (tokenScore < 0)
            {
                return -1;
            }

            score += tokenScore;
        }

        return score;
    }

    private static int MatchScore(string value, string token, int baseScore)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return -1;
        }

        if (string.Equals(value.Trim(), token, StringComparison.OrdinalIgnoreCase))
        {
            return baseScore + 30;
        }

        if (value.Trim().StartsWith(token, StringComparison.OrdinalIgnoreCase))
        {
            return baseScore + 15;
        }

        return value.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0
            ? baseScore
            : -1;
    }

    private static int MatchTags(IReadOnlyList<string> tags, string token)
    {
        if (tags == null)
        {
            return -1;
        }

        int best = -1;
        for (int i = 0; i < tags.Count; i++)
        {
            best = Math.Max(best, MatchScore(tags[i], token, 70));
        }

        return best;
    }

    private static int CompareEntries(
        SequenceAssetIndexEntry left,
        SequenceAssetIndexEntry right)
    {
        int kind = left.Kind.CompareTo(right.Kind);
        if (kind != 0)
        {
            return kind;
        }

        int display = StringComparer.OrdinalIgnoreCase.Compare(
            DisplayName(left),
            DisplayName(right));
        return display != 0
            ? display
            : StringComparer.Ordinal.Compare(left.Id, right.Id);
    }

    private static string DisplayName(SequenceAssetIndexEntry entry)
    {
        return string.IsNullOrWhiteSpace(entry.DisplayNameKo)
            ? entry.Id
            : entry.DisplayNameKo;
    }

    private static string BuildBattleDescription(BattleScenarioData battle)
    {
        if (battle == null)
        {
            return string.Empty;
        }

        return string.Join(" ", new[]
        {
            battle.PrimaryMode,
            battle.OpeningModule,
            battle.MemoryKey
        });
    }

    private static void AddPaths(HashSet<string> paths, string[] guids)
    {
        if (guids == null)
        {
            return;
        }

        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            if (!string.IsNullOrWhiteSpace(path))
            {
                paths.Add(path);
            }
        }
    }

    private static string AssetStableKey(UnityEngine.Object asset)
    {
        if (asset == null)
        {
            return string.Empty;
        }

        GlobalObjectId id = GlobalObjectId.GetGlobalObjectIdSlow(asset);
        string text = id.ToString();
        return string.IsNullOrWhiteSpace(text) || text.EndsWith("-0-0", StringComparison.Ordinal)
            ? DefaultStableKey(asset)
            : text;
    }

    private static string DefaultStableKey(UnityEngine.Object asset)
    {
        return asset == null
            ? string.Empty
            : "instance:" + asset.GetInstanceID();
    }

    private static List<T> DistinctObjects<T>(IEnumerable<T> source)
        where T : UnityEngine.Object
    {
        var result = new List<T>();
        if (source == null)
        {
            return result;
        }

        foreach (T item in source)
        {
            AddDistinct(result, item);
        }

        return result;
    }

    private static void AddDistinct<T>(List<T> target, T item)
        where T : UnityEngine.Object
    {
        if (item == null)
        {
            return;
        }

        for (int i = 0; i < target.Count; i++)
        {
            if (target[i] == item)
            {
                return;
            }
        }

        target.Add(item);
    }

    private static string Normalize(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
    }

    private sealed class SearchHit
    {
        public SearchHit(SequenceAssetIndexEntry entry, int score)
        {
            Entry = entry;
            Score = score;
        }

        public SequenceAssetIndexEntry Entry { get; }
        public int Score { get; }
    }
}

[InitializeOnLoad]
public static class SequenceAssetIndexCache
{
    private static SequenceAssetIndex _current;
    private static bool _dirty = true;

    static SequenceAssetIndexCache()
    {
        EditorApplication.projectChanged += MarkDirty;
    }

    public static SequenceAssetIndex Current
    {
        get
        {
            if (_dirty || _current == null)
            {
                Refresh();
            }

            return _current;
        }
    }

    public static void MarkDirty()
    {
        _dirty = true;
    }

    public static SequenceAssetIndex Refresh()
    {
        _current = SequenceAssetIndex.BuildFromAssetDatabase();
        _dirty = false;
        return _current;
    }
}

public sealed class SequenceNavigatorHistory
{
    public const string RecentKey = "HubToHome.SequenceMaker.RecentTargets";
    public const string FavoritesKey = "HubToHome.SequenceMaker.FavoriteTargets";

    private readonly ISequenceMakerPreferences _preferences;
    private readonly int _maxRecent;
    private readonly List<string> _recent = new List<string>();
    private readonly HashSet<string> _favorites = new HashSet<string>(StringComparer.Ordinal);

    public SequenceNavigatorHistory(
        ISequenceMakerPreferences preferences,
        int maxRecent = 12)
    {
        _preferences = preferences ?? throw new ArgumentNullException(nameof(preferences));
        _maxRecent = Math.Max(1, maxRecent);
        LoadList(_preferences.GetString(RecentKey, string.Empty), _recent, _maxRecent);
        var favorites = new List<string>();
        LoadList(_preferences.GetString(FavoritesKey, string.Empty), favorites, int.MaxValue);
        for (int i = 0; i < favorites.Count; i++)
        {
            _favorites.Add(favorites[i]);
        }
    }

    public bool IsFavorite(string stableKey)
    {
        return _favorites.Contains(Normalize(stableKey));
    }

    public void RecordOpened(string stableKey)
    {
        string normalized = Normalize(stableKey);
        if (string.IsNullOrEmpty(normalized))
        {
            return;
        }

        _recent.Remove(normalized);
        _recent.Insert(0, normalized);
        if (_recent.Count > _maxRecent)
        {
            _recent.RemoveRange(_maxRecent, _recent.Count - _maxRecent);
        }

        Save();
    }

    public void SetFavorite(string stableKey, bool favorite)
    {
        string normalized = Normalize(stableKey);
        if (string.IsNullOrEmpty(normalized))
        {
            return;
        }

        if (favorite)
        {
            _favorites.Add(normalized);
        }
        else
        {
            _favorites.Remove(normalized);
        }

        Save();
    }

    public IReadOnlyList<SequenceAssetIndexEntry> ResolveRecent(SequenceAssetIndex index)
    {
        return Resolve(index, _recent, false);
    }

    public IReadOnlyList<SequenceAssetIndexEntry> ResolveFavorites(SequenceAssetIndex index)
    {
        var keys = new List<string>(_favorites);
        return Resolve(index, keys, true);
    }

    private static IReadOnlyList<SequenceAssetIndexEntry> Resolve(
        SequenceAssetIndex index,
        IList<string> keys,
        bool sort)
    {
        var result = new List<SequenceAssetIndexEntry>();
        if (index == null)
        {
            return result;
        }

        for (int i = 0; i < keys.Count; i++)
        {
            SequenceAssetIndexEntry entry = index.FindByStableKey(keys[i]);
            if (entry != null)
            {
                result.Add(entry);
            }
        }

        if (sort)
        {
            result.Sort((left, right) => StringComparer.OrdinalIgnoreCase.Compare(
                string.IsNullOrWhiteSpace(left.DisplayNameKo) ? left.Id : left.DisplayNameKo,
                string.IsNullOrWhiteSpace(right.DisplayNameKo) ? right.Id : right.DisplayNameKo));
        }

        return result;
    }

    private void Save()
    {
        _preferences.SetString(RecentKey, string.Join("\n", _recent));
        var favorites = new List<string>(_favorites);
        favorites.Sort(StringComparer.Ordinal);
        _preferences.SetString(FavoritesKey, string.Join("\n", favorites));
    }

    private static void LoadList(
        string serialized,
        List<string> target,
        int maxCount)
    {
        target.Clear();
        string[] parts = (serialized ?? string.Empty).Split(
            new[] { '\n' },
            StringSplitOptions.RemoveEmptyEntries);
        for (int i = 0; i < parts.Length && target.Count < maxCount; i++)
        {
            string value = Normalize(parts[i]);
            if (!string.IsNullOrEmpty(value) && !target.Contains(value))
            {
                target.Add(value);
            }
        }
    }

    private static string Normalize(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
    }
}
