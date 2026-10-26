using System;
using System.Collections;
using System.Collections.Generic;

/// <summary>프롤로그 한 세대의 중첩 iterator만 진행·해제합니다. Unity StopCoroutine의 암묵적 Dispose에 의존하지 않습니다.</summary>
public sealed class PrologueTutorialRoutine : IEnumerator, IDisposable
{
    private readonly Stack<IEnumerator> _stack = new Stack<IEnumerator>();
    private readonly Func<bool> _canContinue;
    private bool _moving;
    private bool _cancelled;
    public object Current { get; private set; }

    public PrologueTutorialRoutine(IEnumerator root, Func<bool> canContinue)
    {
        if (root == null) throw new ArgumentNullException(nameof(root));
        _canContinue = canContinue ?? throw new ArgumentNullException(nameof(canContinue));
        _stack.Push(root);
    }

    public bool MoveNext()
    {
        if (_cancelled) return false;
        _moving = true;
        try
        {
            while (_stack.Count > 0)
            {
                if (_cancelled || !_canContinue()) { _cancelled = true; return false; }
                IEnumerator current = _stack.Peek();
                bool moved = current.MoveNext();
                if (_cancelled || !_canContinue()) { _cancelled = true; return false; }
                if (!moved) { _stack.Pop(); (current as IDisposable)?.Dispose(); continue; }
                if (current.Current is IEnumerator nested) _stack.Push(nested);
                else { Current = current.Current; return true; }
            }
            _cancelled = true;
            return false;
        }
        catch { _cancelled = true; throw; }
        finally { _moving = false; if (_cancelled) DisposeStack(); }
    }

    public void Dispose()
    {
        _cancelled = true;
        if (!_moving) DisposeStack();
    }

    private void DisposeStack()
    {
        Exception first = null;
        while (_stack.Count > 0)
        {
            try { (_stack.Pop() as IDisposable)?.Dispose(); }
            catch (Exception exception) { if (first == null) first = exception; }
        }
        Current = null;
        if (first != null) throw first;
    }

    public void Reset() => throw new NotSupportedException();
}
