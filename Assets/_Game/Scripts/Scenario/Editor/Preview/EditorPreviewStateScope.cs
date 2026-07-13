using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEditor;

public sealed class EditorPreviewStateScope : IPreviewStateScope, IDisposable
{
    private static readonly PreviewSideEffect ForbiddenEffects =
        PreviewSideEffect.Save
        | PreviewSideEffect.Reward
        | PreviewSideEffect.SceneTransition
        | PreviewSideEffect.External;

    private readonly Dictionary<IPreviewStateParticipant, CapturedState> _captured =
        new Dictionary<IPreviewStateParticipant, CapturedState>(ReferenceComparer.Instance);
    private readonly List<CapturedState> _restoreOrder = new List<CapturedState>();
    private readonly List<string> _restoreErrors = new List<string>();
    private bool _disposed;

    public EditorPreviewStateScope(bool safePreview = true)
    {
        IsSafePreview = safePreview;
        AssemblyReloadEvents.beforeAssemblyReload += Restore;
        EditorApplication.quitting += Restore;
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
    }

    public bool IsSafePreview { get; }
    public bool IsRestored { get; private set; }
    public IReadOnlyList<string> RestoreErrors => _restoreErrors;

    public bool TryAuthorize(
        PreviewSideEffect sideEffects,
        string blockId,
        string actionId,
        out string error)
    {
        if (!IsSafePreview || (sideEffects & ForbiddenEffects) == 0)
        {
            error = string.Empty;
            return true;
        }

        error = "Safe Preview blocked irreversible side effects at block '"
            + Normalize(blockId)
            + "' ("
            + Normalize(actionId)
            + "): "
            + (sideEffects & ForbiddenEffects);
        return false;
    }

    public bool TryCapture(
        string key,
        IPreviewStateParticipant participant,
        out string error)
    {
        if (!IsSafePreview)
        {
            error = string.Empty;
            return true;
        }

        if (_disposed || IsRestored)
        {
            error = "Preview state scope is already closed.";
            return false;
        }

        if (participant == null)
        {
            error = "Preview state participant is missing for '" + Normalize(key) + "'.";
            return false;
        }

        if (_captured.ContainsKey(participant))
        {
            error = string.Empty;
            return true;
        }

        object state;
        try
        {
            state = participant.CapturePreviewState();
        }
        catch (Exception exception)
        {
            error = "Could not capture preview state for '"
                + Normalize(key)
                + "': "
                + exception.Message;
            return false;
        }

        var captured = new CapturedState(Normalize(key), participant, state);
        _captured.Add(participant, captured);
        _restoreOrder.Add(captured);

        error = string.Empty;
        return true;
    }

    public void Restore()
    {
        if (IsRestored)
        {
            return;
        }

        IsRestored = true;
        for (int i = _restoreOrder.Count - 1; i >= 0; i--)
        {
            CapturedState captured = _restoreOrder[i];
            try
            {
                captured.Participant.RestorePreviewState(captured.State);
            }
            catch (Exception exception)
            {
                _restoreErrors.Add("Failed to restore preview state for '"
                    + captured.Key
                    + "': "
                    + exception.Message);
            }
        }

        _restoreOrder.Clear();
        _captured.Clear();
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        Restore();
        AssemblyReloadEvents.beforeAssemblyReload -= Restore;
        EditorApplication.quitting -= Restore;
        EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
        _disposed = true;
    }

    private void OnPlayModeStateChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.ExitingEditMode
            || state == PlayModeStateChange.ExitingPlayMode)
        {
            Restore();
        }
    }

    private static string Normalize(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
    }

    private sealed class CapturedState
    {
        public CapturedState(string key, IPreviewStateParticipant participant, object state)
        {
            Key = key;
            Participant = participant;
            State = state;
        }

        public string Key { get; }
        public IPreviewStateParticipant Participant { get; }
        public object State { get; }
    }

    private sealed class ReferenceComparer : IEqualityComparer<IPreviewStateParticipant>
    {
        public static readonly ReferenceComparer Instance = new ReferenceComparer();

        public bool Equals(IPreviewStateParticipant x, IPreviewStateParticipant y)
        {
            return ReferenceEquals(x, y);
        }

        public int GetHashCode(IPreviewStateParticipant obj)
        {
            return obj == null ? 0 : RuntimeHelpers.GetHashCode(obj);
        }
    }
}
