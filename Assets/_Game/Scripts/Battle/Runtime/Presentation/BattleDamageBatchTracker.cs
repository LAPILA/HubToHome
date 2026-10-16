using System;
using System.Collections.Generic;

/// <summary>표시 묶음의 수명만 소유합니다. 턴/피해/저장 데이터는 변경하지 않습니다.</summary>
public sealed class BattleDamageBatchTracker
{
    private readonly List<long> _open = new List<long>(4);
    private long _nextId;
    private long _overrideId;
    public event Action<long> Ended;
    public long CurrentId => _overrideId != 0 ? _overrideId : (_open.Count > 0 ? _open[_open.Count - 1] : 0);
    public bool IsOpen(long id) => id > 0 && _open.Contains(id);

    public IDisposable Begin()
    {
        long id = ++_nextId;
        _open.Add(id);
        return new Scope(this, id);
    }

    // 병렬 시나리오의 타격도 자신의 ID를 사용합니다. 이 범위 안에서는 yield하지 않습니다.
    public Activation Activate(long id)
    {
        return new Activation(this, id);
    }

    public void Clear()
    {
        while (_open.Count > 0) End(_open[_open.Count - 1]);
        _overrideId = 0;
    }

    private void End(long id)
    {
        if (!_open.Remove(id)) return;
        // 종료 알림 안에서 추가 피해가 발생해도 닫힌 묶음에 다시 합산하지 않습니다.
        if (_overrideId == id) _overrideId = 0;
        Ended?.Invoke(id);
    }

    private sealed class Scope : IDisposable
    {
        private BattleDamageBatchTracker _owner;
        private readonly long _id;
        public Scope(BattleDamageBatchTracker owner, long id) { _owner = owner; _id = id; }
        public void Dispose() { var owner = _owner; _owner = null; owner?.End(_id); }
    }

    public struct Activation : IDisposable
    {
        private BattleDamageBatchTracker _owner;
        private readonly long _previous;
        public Activation(BattleDamageBatchTracker owner, long id)
        {
            _owner = owner;
            _previous = owner._overrideId;
            owner._overrideId = owner.IsOpen(id) ? id : 0;
        }
        public void Dispose()
        {
            if (_owner == null) return;
            _owner._overrideId = _owner.IsOpen(_previous) ? _previous : 0;
            _owner = null;
        }
    }
}

public interface IBattleDamagePresentationHost
{
    BattleDamageBatchTracker DamageBatches { get; }
}
