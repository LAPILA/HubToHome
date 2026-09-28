using System;
using System.Collections.Generic;

/// <summary>Discrete battle time. Menus, animations and preview generation never advance it.</summary>
public sealed class BattleSpeedTurnScheduler<T> where T : class
{
    private const double TurnWork = 1000d;
    private const double TieTolerance = 0.000000001d;

    private struct Entry
    {
        public T Actor;
        public int Speed;
        public double Remaining;
        public int ForecastTurns;
    }

    private readonly List<Entry> _entries = new List<Entry>();
    private readonly List<Entry> _preview = new List<Entry>();
    private bool _hasTakenTurn;
    private bool _openingTurnGranted;

    public int Count => _entries.Count;

    // Safe at turn boundaries and stat changes; this never advances readiness.
    // The adapter filters Unity's destroyed/dead objects.
    public void Synchronize(IReadOnlyList<T> actors, Func<T, int> readSpeed)
    {
        if (readSpeed == null) throw new ArgumentNullException(nameof(readSpeed));
        for (int i = _entries.Count - 1; i >= 0; i--)
        {
            if (!ContainsReference(actors, _entries[i].Actor))
                _entries.RemoveAt(i);
        }

        if (actors == null) return;
        for (int i = 0; i < actors.Count; i++)
        {
            T actor = actors[i];
            if (ReferenceEquals(actor, null)) continue;
            int index = FindEntry(actor);
            int speed = Math.Max(1, readSpeed(actor));
            if (index < 0)
                _entries.Add(new Entry { Actor = actor, Speed = speed, Remaining = TurnWork });
            else
            {
                Entry entry = _entries[index];
                entry.Speed = speed;
                _entries[index] = entry;
            }
        }
    }

    public bool GrantOpeningTurn(T actor)
    {
        if (_hasTakenTurn || _openingTurnGranted) return false;
        int index = FindEntry(actor);
        if (index < 0) return false;
        Entry entry = _entries[index];
        entry.Remaining = 0d;
        _entries[index] = entry;
        _openingTurnGranted = true;
        return true;
    }

    public bool TryTakeNext(out T actor)
    {
        bool taken = TryTakeNext(_entries, out actor);
        if (taken) _hasTakenTurn = true;
        return taken;
    }

    /// <summary>
    /// Append future actors without spending real readiness or ticking live effects.
    /// The optional reader returns speed after this actor's simulated completed turns.
    /// At count zero it must account for an already-active turn's pending end, if any.
    /// </summary>
    public void AppendPreview(IList<T> destination, int count, Func<T, int, int> readSpeedAfterTurns = null)
    {
        if (destination == null) throw new ArgumentNullException(nameof(destination));
        _preview.Clear();
        _preview.AddRange(_entries);
        if (readSpeedAfterTurns != null)
        {
            for (int i = 0; i < _preview.Count; i++)
            {
                Entry entry = _preview[i];
                entry.ForecastTurns = 0;
                entry.Speed = Math.Max(1, readSpeedAfterTurns(entry.Actor, 0));
                _preview[i] = entry;
            }
        }
        for (int i = 0; i < count && TryTakeNext(_preview, out T actor, readSpeedAfterTurns); i++)
            destination.Add(actor);
    }

    private static bool TryTakeNext(List<Entry> entries, out T actor, Func<T, int, int> readSpeedAfterTurns = null)
    {
        actor = null;
        if (entries.Count == 0) return false;
        int selected = 0;
        double elapsed = entries[0].Remaining / entries[0].Speed;
        for (int i = 1; i < entries.Count; i++)
        {
            double time = entries[i].Remaining / entries[i].Speed;
            // Keep original registration order for equal arrival times.
            if (time < elapsed - TieTolerance)
            {
                selected = i;
                elapsed = time;
            }
        }

        for (int i = 0; i < entries.Count; i++)
        {
            Entry entry = entries[i];
            entry.Remaining = Math.Max(0d, entry.Remaining - elapsed * entry.Speed);
            if (i == selected)
            {
                actor = entry.Actor;
                entry.Remaining = TurnWork;
                if (readSpeedAfterTurns != null)
                {
                    entry.ForecastTurns++;
                    entry.Speed = Math.Max(1, readSpeedAfterTurns(actor, entry.ForecastTurns));
                }
            }
            entries[i] = entry;
        }
        return true;
    }

    private int FindEntry(T actor)
    {
        for (int i = 0; i < _entries.Count; i++)
            if (ReferenceEquals(_entries[i].Actor, actor)) return i;
        return -1;
    }

    private static bool ContainsReference(IReadOnlyList<T> actors, T actor)
    {
        if (actors == null) return false;
        for (int i = 0; i < actors.Count; i++)
            if (ReferenceEquals(actors[i], actor)) return true;
        return false;
    }
}
