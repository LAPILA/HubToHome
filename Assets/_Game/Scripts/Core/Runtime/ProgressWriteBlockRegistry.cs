using System;
using System.Threading;

/// <summary>진행 저장 차단의 중첩 소유권을 관리합니다. 초기화 이전의 핸들은 새 세대에 영향을 주지 않습니다.</summary>
public sealed class ProgressWriteBlockRegistry
{
    private readonly object _gate = new object();
    private object _generation = new object();
    private int _count;

    public bool IsBlocked
    {
        get { lock (_gate) return _count > 0; }
    }

    public IDisposable Acquire()
    {
        lock (_gate)
        {
            var lease = new Lease(this, _generation);
            checked { _count++; }
            return lease;
        }
    }

    public void Reset()
    {
        lock (_gate)
        {
            _generation = new object();
            _count = 0;
        }
    }

    private void Release(object generation)
    {
        lock (_gate)
        {
            if (ReferenceEquals(generation, _generation)) _count--;
        }
    }

    private sealed class Lease : IDisposable
    {
        private ProgressWriteBlockRegistry _owner;
        private readonly object _generation;

        public Lease(ProgressWriteBlockRegistry owner, object generation)
        {
            _owner = owner;
            _generation = generation;
        }

        public void Dispose()
        {
            // 같은 핸들을 중복/동시에 해제해도 해당 소유권은 한 번만 반납합니다.
            Interlocked.Exchange(ref _owner, null)?.Release(_generation);
        }
    }
}
