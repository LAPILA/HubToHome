using System;

/// <summary>명시된 true 플래그의 변경만 관찰합니다. HP/페이즈 규칙이나 표시 구현은 소유하지 않습니다.</summary>
public sealed class BattleFlagPresentationGate : IDisposable
{
    private readonly IBattleSessionStateReader _state;
    private readonly IBattleSessionFlagEvents _events;
    private readonly string _flag;
    private readonly Action<bool> _setVisible;
    private bool _disposed;
    public bool IsShown { get; private set; }

    public BattleFlagPresentationGate(IBattleSessionStateReader state, string flag, Action<bool> setVisible)
    {
        _state = state ?? throw new ArgumentNullException(nameof(state));
        _events = state as IBattleSessionFlagEvents ?? throw new ArgumentException("Flag events are required.", nameof(state));
        if (string.IsNullOrWhiteSpace(flag)) throw new ArgumentException("Activation flag is required.", nameof(flag));
        _flag = flag.Trim();
        _setVisible = setVisible ?? throw new ArgumentNullException(nameof(setVisible));
        _events.FlagChanged += HandleChanged;
        try { Refresh(); }
        catch { _events.FlagChanged -= HandleChanged; throw; }
    }

    private void HandleChanged(string flag)
    {
        if (!_disposed && string.Equals(flag, _flag, StringComparison.Ordinal)) Refresh();
    }

    private void Refresh()
    {
        bool visible = _state.TryGetFlagValue(_flag, out string value)
            && bool.TryParse(value, out bool enabled) && enabled;
        if (visible == IsShown) return;
        IsShown = visible;
        _setVisible(visible);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _events.FlagChanged -= HandleChanged;
        if (!IsShown) return;
        IsShown = false;
        _setVisible(false);
    }
}
