using System;
using System.Collections.Generic;

/// <summary>수치 증감과 구분하여 실제 상태 부여·해제·빙결 전환만 기억하는 표시 전용 비교기입니다.</summary>
public sealed class BattleStatusTransitionTracker
{
    private HashSet<Key> _previous = new HashSet<Key>();
    private HashSet<Key> _current = new HashSet<Key>();
    private bool _initialized;
    public bool HasPreviousSnapshot { get; private set; }
    public bool HasAdded { get; private set; }
    public bool HasRemoved { get; private set; }

    public void Capture(ElementalStatusState state, IReadOnlyList<StatusEffect> effects = null)
    {
        HasPreviousSnapshot = _initialized;
        HashSet<Key> spare = _previous;
        _previous = _current;
        _current = spare;
        _current.Clear();
        int count = ElementalStatusPresentation.GetEntryCount(state);
        for (int i = 0; i < count; i++)
            if (ElementalStatusPresentation.TryGetEntry(state, i, out ElementalStatusKind kind, out _, out bool frozen))
                _current.Add(Key.Element(kind, frozen));
        for (int i = 0; effects != null && i < effects.Count; i++)
            if (effects[i] != null) _current.Add(Key.Legacy(effects[i].EffectID));
        HasAdded = HasRemoved = false;
        if (HasPreviousSnapshot)
        {
            foreach (Key key in _current)
                if (!_previous.Contains(key)) { HasAdded = true; break; }
            foreach (Key key in _previous)
                if (!_current.Contains(key)) { HasRemoved = true; break; }
        }
        _initialized = true;
    }

    public bool IsNewElement(ElementalStatusKind kind, bool frozen) => IsNew(Key.Element(kind, frozen));
    public bool IsNewLegacy(string id) => IsNew(Key.Legacy(id));
    private bool IsNew(Key key) => HasPreviousSnapshot && _current.Contains(key) && !_previous.Contains(key);

    public void Reset()
    {
        _previous.Clear();
        _current.Clear();
        _initialized = HasPreviousSnapshot = HasAdded = HasRemoved = false;
    }

    private readonly struct Key : IEquatable<Key>
    {
        private readonly int _element;
        private readonly string _legacy;
        private Key(int element, string legacy) { _element = element; _legacy = legacy; }
        public static Key Element(ElementalStatusKind kind, bool frozen) => new Key(frozen ? -1 : (int)kind + 1, null);
        public static Key Legacy(string id) => new Key(0, id ?? string.Empty);
        public bool Equals(Key other) => _element == other._element
            && string.Equals(_legacy, other._legacy, StringComparison.OrdinalIgnoreCase);
        public override bool Equals(object obj) => obj is Key other && Equals(other);
        public override int GetHashCode() => (_element * 397)
            ^ (_legacy != null ? StringComparer.OrdinalIgnoreCase.GetHashCode(_legacy) : 0);
    }
}
