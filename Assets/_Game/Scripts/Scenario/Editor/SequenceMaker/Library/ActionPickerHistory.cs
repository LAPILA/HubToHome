using System;
using System.Collections.Generic;

public sealed class ActionPickerHistory
{
    public const string FavoritesKey = "HubToHome.SequenceMaker.ActionFavorites";
    public const string RecentKey = "HubToHome.SequenceMaker.ActionRecent";
    public const int MaximumRecentCount = 12;

    private readonly ISequenceMakerPreferences _preferences;
    private readonly HashSet<string> _favorites = new HashSet<string>(StringComparer.Ordinal);
    private readonly List<string> _recent = new List<string>();

    public ActionPickerHistory(ISequenceMakerPreferences preferences)
    {
        _preferences = preferences ?? throw new ArgumentNullException(nameof(preferences));
        Load(_preferences.GetString(FavoritesKey, string.Empty), _favorites);
        Load(_preferences.GetString(RecentKey, string.Empty), _recent);
    }

    public IReadOnlyCollection<string> Favorites => _favorites;
    public IReadOnlyList<string> Recent => _recent;

    public bool IsFavorite(string actionId)
    {
        return _favorites.Contains(Normalize(actionId));
    }

    public bool ToggleFavorite(string actionId)
    {
        string normalized = Normalize(actionId);
        if (string.IsNullOrEmpty(normalized))
        {
            return false;
        }

        bool added;
        if (_favorites.Contains(normalized))
        {
            _favorites.Remove(normalized);
            added = false;
        }
        else
        {
            _favorites.Add(normalized);
            added = true;
        }

        _preferences.SetString(FavoritesKey, string.Join("\n", _favorites));
        return added;
    }

    public void RecordRecent(string actionId)
    {
        string normalized = Normalize(actionId);
        if (string.IsNullOrEmpty(normalized))
        {
            return;
        }

        _recent.Remove(normalized);
        _recent.Insert(0, normalized);
        if (_recent.Count > MaximumRecentCount)
        {
            _recent.RemoveRange(MaximumRecentCount, _recent.Count - MaximumRecentCount);
        }

        _preferences.SetString(RecentKey, string.Join("\n", _recent));
    }

    private static void Load(string serialized, ICollection<string> destination)
    {
        string[] values = (serialized ?? string.Empty).Split(
            new[] { '\n' },
            StringSplitOptions.RemoveEmptyEntries);
        for (int i = 0; i < values.Length; i++)
        {
            string normalized = Normalize(values[i]);
            if (!string.IsNullOrEmpty(normalized) && !destination.Contains(normalized))
            {
                destination.Add(normalized);
            }
        }
    }

    private static string Normalize(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
    }
}
