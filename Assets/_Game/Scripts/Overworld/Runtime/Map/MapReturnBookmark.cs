using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Runtime-only address used to return from a scene-based sublocation.
/// </summary>
public sealed class MapReturnBookmark
{
    public MapReturnBookmark(
        string sceneName,
        string roomId,
        string spawnPointId,
        Vector2 fallbackPosition,
        FacingDirection facing)
    {
        SceneName = Normalize(sceneName);
        RoomId = Normalize(roomId);
        SpawnPointId = Normalize(spawnPointId);
        FallbackPosition = fallbackPosition;
        Facing = facing;
    }

    public string SceneName { get; }
    public string RoomId { get; }
    public string SpawnPointId { get; }
    public Vector2 FallbackPosition { get; }
    public FacingDirection Facing { get; }

    public bool IsValid => !string.IsNullOrEmpty(SceneName)
        && (!string.IsNullOrEmpty(SpawnPointId) || IsFinite(FallbackPosition));

    private static string Normalize(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
    }

    private static bool IsFinite(Vector2 value)
    {
        return !float.IsNaN(value.x)
            && !float.IsInfinity(value.x)
            && !float.IsNaN(value.y)
            && !float.IsInfinity(value.y);
    }
}

public readonly struct MapReturnBookmarkToken : IEquatable<MapReturnBookmarkToken>
{
    internal MapReturnBookmarkToken(long value)
    {
        Value = value;
    }

    internal long Value { get; }
    public bool IsValid => Value > 0;

    public bool Equals(MapReturnBookmarkToken other) => Value == other.Value;
    public override bool Equals(object obj) => obj is MapReturnBookmarkToken other && Equals(other);
    public override int GetHashCode() => Value.GetHashCode();
    public static bool operator ==(MapReturnBookmarkToken left, MapReturnBookmarkToken right) => left.Equals(right);
    public static bool operator !=(MapReturnBookmarkToken left, MapReturnBookmarkToken right) => !left.Equals(right);
}

/// <summary>
/// Owns transactional push/commit/rollback semantics for sublocation returns.
/// </summary>
public sealed class MapReturnBookmarkStack
{
    private readonly List<Entry> _entries = new List<Entry>();
    private long _nextToken = 1;

    public int Count => _entries.Count;

    public MapReturnBookmarkToken PushPending(MapReturnBookmark bookmark)
    {
        ValidateBookmark(bookmark);
        MapReturnBookmarkToken token = NextToken();
        _entries.Add(new Entry(token, bookmark, false));
        return token;
    }

    public MapReturnBookmarkToken PushCommitted(MapReturnBookmark bookmark)
    {
        ValidateBookmark(bookmark);
        MapReturnBookmarkToken token = NextToken();
        _entries.Add(new Entry(token, bookmark, true));
        return token;
    }

    public bool Commit(MapReturnBookmarkToken token)
    {
        if (!TryGetTop(token, out Entry entry) || entry.IsCommitted)
            return false;

        entry.IsCommitted = true;
        return true;
    }

    public bool Rollback(MapReturnBookmarkToken token)
    {
        if (!TryGetTop(token, out Entry entry) || entry.IsCommitted)
            return false;

        _entries.RemoveAt(_entries.Count - 1);
        return true;
    }

    public bool TryPeek(out MapReturnBookmark bookmark)
    {
        return TryPeek(out bookmark, out _);
    }

    public bool TryPeek(
        out MapReturnBookmark bookmark,
        out MapReturnBookmarkToken token)
    {
        if (_entries.Count > 0 && _entries[_entries.Count - 1].IsCommitted)
        {
            Entry entry = _entries[_entries.Count - 1];
            bookmark = entry.Bookmark;
            token = entry.Token;
            return true;
        }

        bookmark = null;
        token = default;
        return false;
    }

    public bool TryPop(
        MapReturnBookmarkToken expectedToken,
        out MapReturnBookmark bookmark)
    {
        if (!TryPeek(out bookmark, out MapReturnBookmarkToken token)
            || token != expectedToken)
        {
            bookmark = null;
            return false;
        }

        _entries.RemoveAt(_entries.Count - 1);
        return true;
    }

    public void Clear()
    {
        _entries.Clear();
    }

    private bool TryGetTop(MapReturnBookmarkToken token, out Entry entry)
    {
        if (token.IsValid && _entries.Count > 0)
        {
            entry = _entries[_entries.Count - 1];
            return entry.Token == token;
        }

        entry = null;
        return false;
    }

    private MapReturnBookmarkToken NextToken()
    {
        if (_nextToken <= 0)
            _nextToken = 1;

        return new MapReturnBookmarkToken(_nextToken++);
    }

    private static void ValidateBookmark(MapReturnBookmark bookmark)
    {
        if (bookmark == null)
            throw new ArgumentNullException(nameof(bookmark));
        if (!bookmark.IsValid)
            throw new ArgumentException("Map return bookmark is invalid.", nameof(bookmark));
    }

    private sealed class Entry
    {
        public Entry(
            MapReturnBookmarkToken token,
            MapReturnBookmark bookmark,
            bool isCommitted)
        {
            Token = token;
            Bookmark = bookmark;
            IsCommitted = isCommitted;
        }

        public MapReturnBookmarkToken Token { get; }
        public MapReturnBookmark Bookmark { get; }
        public bool IsCommitted { get; set; }
    }
}