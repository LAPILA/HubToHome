using System;
using System.Collections.Generic;
using UnityEngine;

[Flags]
public enum PreviewSideEffect
{
    None = 0,
    Presentation = 1 << 0,
    SceneState = 1 << 1,
    GameplayState = 1 << 2,
    Save = 1 << 3,
    Reward = 1 << 4,
    SceneTransition = 1 << 5,
    External = 1 << 6
}

public interface IPreviewStateParticipant
{
    object CapturePreviewState();

    void RestorePreviewState(object state);
}

public interface IPreviewUndoObjectProvider
{
    IEnumerable<UnityEngine.Object> GetPreviewUndoObjects();
}

public interface IPreviewStateScope
{
    bool IsSafePreview { get; }

    bool IsRestored { get; }

    bool TryAuthorize(
        PreviewSideEffect sideEffects,
        string blockId,
        string actionId,
        out string error);

    bool TryCapture(
        string key,
        IPreviewStateParticipant participant,
        out string error);

    void Restore();
}
