using System.Collections;
using UnityEngine;

public sealed class PreviewScreenTransitionRunner : IScreenTransitionRunner, IPreviewStateParticipant
{
    public string Mode { get; private set; } = "in";
    public string Color { get; private set; } = "black";

    public IEnumerator Fade(string mode, string color, float duration, ActionExecutionHandle handle)
    {
        if (!ScreenTransitionRunner.TryResolveTargetAlpha(mode, out float _))
        {
            handle.Fail("Unsupported screen.fade mode: " + mode);
            yield break;
        }

        if (!ScreenTransitionRunner.TryResolveColor(color, out Color _))
        {
            handle.Fail("Unsupported screen.fade color: " + color);
            yield break;
        }

        Mode = string.IsNullOrWhiteSpace(mode) ? "in" : mode.Trim();
        Color = string.IsNullOrWhiteSpace(color) ? "black" : color.Trim();
        yield break;
    }

    public object CapturePreviewState()
    {
        return new StringPair(Mode, Color);
    }

    public void RestorePreviewState(object state)
    {
        if (state is StringPair pair)
        {
            Mode = pair.First;
            Color = pair.Second;
        }
    }
}

public sealed class PreviewAudioActionRunner : IAudioActionRunner, IPreviewStateParticipant
{
    public string CurrentBgmId { get; private set; } = string.Empty;

    public IEnumerator CrossfadeBgm(string clipId, float duration, ActionExecutionHandle handle)
    {
        CurrentBgmId = string.IsNullOrWhiteSpace(clipId) ? string.Empty : clipId.Trim();
        yield break;
    }

    public object CapturePreviewState()
    {
        return CurrentBgmId;
    }

    public void RestorePreviewState(object state)
    {
        CurrentBgmId = state as string ?? string.Empty;
    }
}

public sealed class PreviewGameModuleActionRunner : IGameModuleActionRunner, IPreviewStateParticipant
{
    private readonly ActionExecutionContext _boundContext;

    public PreviewGameModuleActionRunner(string currentModuleId = "")
    {
        CurrentModuleId = Normalize(currentModuleId);
    }

    public PreviewGameModuleActionRunner(ActionExecutionContext context)
    {
        _boundContext = context;
        CurrentModuleId = Normalize(context?.ModuleId);
    }

    public string CurrentModuleId { get; private set; }
    public string EnteredModuleId { get; private set; } = string.Empty;
    public string StartedModuleId { get; private set; } = string.Empty;

    public IEnumerator SwitchTo(string moduleId, ActionExecutionContext context)
    {
        CurrentModuleId = Normalize(moduleId);
        EnteredModuleId = CurrentModuleId;
        if (context != null)
        {
            context.ModuleId = CurrentModuleId;
        }

        yield break;
    }

    public IEnumerator Start(string moduleId, ActionExecutionContext context)
    {
        CurrentModuleId = Normalize(moduleId);
        StartedModuleId = CurrentModuleId;
        if (context != null)
        {
            context.ModuleId = CurrentModuleId;
        }

        yield break;
    }

    public object CapturePreviewState()
    {
        return new ModulePreviewState(CurrentModuleId, EnteredModuleId, StartedModuleId);
    }

    public void RestorePreviewState(object state)
    {
        if (state is ModulePreviewState previewState)
        {
            CurrentModuleId = previewState.Current;
            EnteredModuleId = previewState.Entered;
            StartedModuleId = previewState.Started;
            if (_boundContext != null)
            {
                _boundContext.ModuleId = CurrentModuleId;
            }
        }
    }

    private static string Normalize(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
    }

    private sealed class ModulePreviewState
    {
        public ModulePreviewState(string current, string entered, string started)
        {
            Current = current ?? string.Empty;
            Entered = entered ?? string.Empty;
            Started = started ?? string.Empty;
        }

        public string Current { get; }
        public string Entered { get; }
        public string Started { get; }
    }
}

internal sealed class StringPair
{
    public StringPair(string first, string second)
    {
        First = first ?? string.Empty;
        Second = second ?? string.Empty;
    }

    public string First { get; }
    public string Second { get; }
}
