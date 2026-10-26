using System;
using System.Collections.Generic;

/// <summary>상태 부여·해제·빙결 전환과 강도 단계 상승을 기억하는 표시 전용 비교기입니다.</summary>
public sealed class BattleStatusTransitionTracker
{
    private HashSet<Key> _previous = new HashSet<Key>();
    private HashSet<Key> _current = new HashSet<Key>();
    private int[] _previousTiers = new int[5];
    private int[] _currentTiers = new int[5];
    private bool _initialized;
    public bool HasPreviousSnapshot { get; private set; }
    public bool HasAdded { get; private set; }
    public bool HasRemoved { get; private set; }
    public bool HasPotencyIncreased { get; private set; }

    public void Capture(ElementalStatusState state, IReadOnlyList<StatusEffect> effects = null)
    {
        HasPreviousSnapshot = _initialized;
        HashSet<Key> spare = _previous;
        _previous = _current;
        _current = spare;
        _current.Clear();
        int[] spareTiers = _previousTiers;
        _previousTiers = _currentTiers;
        _currentTiers = spareTiers;
        Array.Clear(_currentTiers, 0, _currentTiers.Length);
        int count = ElementalStatusPresentation.GetEntryCount(state);
        for (int i = 0; i < count; i++)
            if (ElementalStatusPresentation.TryGetEntry(state, i, out ElementalStatusKind kind, out _, out bool frozen))
            {
                _current.Add(Key.Element(kind, frozen));
                if (!frozen) _currentTiers[(int)kind] = state.GetPotencyTier(kind);
            }
        for (int i = 0; effects != null && i < effects.Count; i++)
            if (effects[i] != null) _current.Add(Key.Legacy(effects[i].EffectID));
        HasAdded = HasRemoved = HasPotencyIncreased = false;
        if (HasPreviousSnapshot)
        {
            foreach (Key key in _current)
                if (!_previous.Contains(key)) { HasAdded = true; break; }
            foreach (Key key in _previous)
                if (!_current.Contains(key)) { HasRemoved = true; break; }
            for (int i = 0; i < _currentTiers.Length; i++)
                if (IsPotencyIncreased((ElementalStatusKind)i)) { HasPotencyIncreased = true; break; }
        }
        _initialized = true;
    }

    public bool IsNewElement(ElementalStatusKind kind, bool frozen) => IsNew(Key.Element(kind, frozen));
    public bool IsNewLegacy(string id) => IsNew(Key.Legacy(id));
    public bool IsRemovedElement(ElementalStatusKind kind, bool frozen) => IsRemoved(Key.Element(kind, frozen));
    public bool IsRemovedLegacy(string id) => IsRemoved(Key.Legacy(id));
    public bool IsPotencyIncreased(ElementalStatusKind kind)
    {
        int index = (int)kind;
        return index >= 0 && index < _currentTiers.Length && HasPreviousSnapshot
            && _previous.Contains(Key.Element(kind, false)) && _current.Contains(Key.Element(kind, false))
            && _currentTiers[index] > _previousTiers[index];
    }
    private bool IsNew(Key key) => HasPreviousSnapshot && _current.Contains(key) && !_previous.Contains(key);
    private bool IsRemoved(Key key) => HasPreviousSnapshot && _previous.Contains(key) && !_current.Contains(key);

    public void Reset()
    {
        _previous.Clear();
        _current.Clear();
        Array.Clear(_previousTiers, 0, _previousTiers.Length);
        Array.Clear(_currentTiers, 0, _currentTiers.Length);
        _initialized = HasPreviousSnapshot = HasAdded = HasRemoved = HasPotencyIncreased = false;
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
