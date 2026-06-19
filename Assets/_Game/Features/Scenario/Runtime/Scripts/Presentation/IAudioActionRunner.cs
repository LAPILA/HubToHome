using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public interface IAudioActionRunner
{
    IEnumerator CrossfadeBgm(string clipId, float duration, ActionExecutionHandle handle);
}

public interface IAudioClipResolver
{
    bool TryResolveBgmClip(string clipId, out AudioClip clip);
}

public sealed class ResourcesAudioClipResolver : IAudioClipResolver
{
    public bool TryResolveBgmClip(string clipId, out AudioClip clip)
    {
        clip = null;
        string resourcePath = NormalizeResourcePath(clipId);
        if (string.IsNullOrEmpty(resourcePath))
        {
            return false;
        }

        clip = Resources.Load<AudioClip>(resourcePath);
        return clip != null;
    }

    private static string NormalizeResourcePath(string clipId)
    {
        if (string.IsNullOrWhiteSpace(clipId))
        {
            return string.Empty;
        }

        string value = clipId.Trim().Replace('\\', '/');
        const string resourcesMarker = "/Resources/";
        int resourcesIndex = value.IndexOf(resourcesMarker, System.StringComparison.OrdinalIgnoreCase);
        if (resourcesIndex >= 0)
        {
            value = value.Substring(resourcesIndex + resourcesMarker.Length);
        }

        if (value.StartsWith("Resources/", System.StringComparison.OrdinalIgnoreCase))
        {
            value = value.Substring("Resources/".Length);
        }

        if (value.StartsWith("Assets/", System.StringComparison.OrdinalIgnoreCase))
        {
            value = value.Substring("Assets/".Length);
        }

        if (value.EndsWith(".wav", System.StringComparison.OrdinalIgnoreCase)
            || value.EndsWith(".mp3", System.StringComparison.OrdinalIgnoreCase)
            || value.EndsWith(".ogg", System.StringComparison.OrdinalIgnoreCase)
            || value.EndsWith(".aiff", System.StringComparison.OrdinalIgnoreCase))
        {
            value = value.Substring(0, value.LastIndexOf('.'));
        }

        return value;
    }
}

public sealed class AudioManagerActionRunner : IAudioActionRunner
{
    private readonly IAudioClipResolver _clipResolver;

    public AudioManagerActionRunner(IAudioClipResolver clipResolver = null)
    {
        _clipResolver = clipResolver ?? new ResourcesAudioClipResolver();
    }

    public IEnumerator CrossfadeBgm(string clipId, float duration, ActionExecutionHandle handle)
    {
        if (AudioManager.Instance == null)
        {
            handle.Fail("AudioManager.Instance is missing for bgm.crossfade.");
            yield break;
        }

        AudioClip clip;
        if (!_clipResolver.TryResolveBgmClip(clipId, out clip))
        {
            handle.Fail("BGM clip could not be resolved: " + clipId);
            yield break;
        }

        if (duration <= 0f)
        {
            AudioManager.Instance.PlayBGM(clip);
            yield break;
        }

        AudioManager.Instance.CrossFadeBGM(clip, duration);

        float elapsed = 0f;
        while (elapsed < duration)
        {
            if (handle.IsCancellationRequested)
            {
                yield break;
            }

            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }
    }
}

public sealed class ScenarioAudioClipResolver : IAudioClipResolver
{
    private readonly Dictionary<string, AudioClip> _clips =
        new Dictionary<string, AudioClip>(System.StringComparer.Ordinal);
    private readonly IAudioClipResolver _fallback;

    public ScenarioAudioClipResolver(
        IEnumerable<ScenarioAudioReferenceData> references,
        IAudioClipResolver fallback = null)
    {
        _fallback = fallback;
        if (references == null)
        {
            return;
        }

        foreach (ScenarioAudioReferenceData reference in references)
        {
            if (reference == null || reference.Clip == null || string.IsNullOrWhiteSpace(reference.AudioId))
            {
                continue;
            }

            _clips[reference.AudioId.Trim()] = reference.Clip;
        }
    }

    public bool TryResolveBgmClip(string clipId, out AudioClip clip)
    {
        clip = null;
        string normalizedId = string.IsNullOrWhiteSpace(clipId) ? string.Empty : clipId.Trim();
        if (!string.IsNullOrEmpty(normalizedId) && _clips.TryGetValue(normalizedId, out clip) && clip != null)
        {
            return true;
        }

        return _fallback != null && _fallback.TryResolveBgmClip(clipId, out clip);
    }
}
