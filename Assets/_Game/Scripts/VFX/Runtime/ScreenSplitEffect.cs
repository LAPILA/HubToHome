using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>표시 중인 카메라별 화면 분할 스냅샷. 시간/피해/카메라 이동은 호출자가 소유합니다.</summary>
public static class ScreenSplitEffect
{
    public readonly struct Frame
    {
        public Frame(Vector4 line, Vector4 warp, Color color) { Line = line; Warp = warp; Color = color; }
        public Vector4 Line { get; }
        public Vector4 Warp { get; }
        public Color Color { get; }
    }

    private struct Entry
    {
        public long Token;
        public Component Owner;
        public Func<bool> IsActive;
        public Vector2 Tangent;
        public float Separation, Twist, LineWidth;
        public Color Color;
        public Frame Frame;
    }

    private static readonly Dictionary<int, Entry> Entries = new Dictionary<int, Entry>(2);
    private static long _nextToken;
    private static bool _listening;
    public static int ActiveCount => Entries.Count;

    public readonly struct Lease : IDisposable
    {
        private readonly int _cameraId;
        private readonly long _token;
        internal Lease(int cameraId, long token) { _cameraId = cameraId; _token = token; }
        public bool IsCurrent => _token != 0 && Entries.TryGetValue(_cameraId, out Entry value) && value.Token == _token;
        public void SetProgress(float normalized, float attackFraction = .12f)
        {
            if (!IsCurrent) return;
            if (!Finite(normalized) || normalized >= 1f) { Dispose(); return; }
            SetStrength(EvaluateEnvelope(normalized, attackFraction));
        }
        public void SetStrength(float strength, float outlineEmphasis = 0f)
        {
            if (!IsCurrent) return;
            if (!Finite(strength) || !Finite(outlineEmphasis)) { Dispose(); return; }
            strength = Mathf.Clamp01(strength);
            Entry value = Entries[_cameraId];
            float rotation = value.Twist * strength * Mathf.Deg2Rad;
            value.Frame = new Frame(new Vector4(value.Tangent.x, value.Tangent.y, .5f, .5f),
                new Vector4(value.Separation * strength, Mathf.Cos(rotation), Mathf.Sin(rotation),
                    value.LineWidth * strength * (1f + Mathf.Clamp01(outlineEmphasis))), value.Color);
            Entries[_cameraId] = value;
        }
        public void Dispose()
        {
            if (IsCurrent) Remove(_cameraId);
        }
    }

    public static Lease Begin(Camera camera, Component owner, float angleDegrees, float separationPixels,
        float twistDegrees, float lineWidthPixels, Color lineColor, Func<bool> isActive = null, float initialStrength = 1f)
    {
        if (camera == null || owner == null || camera.cameraType != CameraType.Game || !OwnerIsActive(owner)
            || !Finite(angleDegrees) || !Finite(separationPixels) || !Finite(twistDegrees) || !Finite(lineWidthPixels)
            || !Finite(lineColor.r) || !Finite(lineColor.g) || !Finite(lineColor.b) || !Finite(lineColor.a)
            || !Finite(initialStrength)) return default;
        GameConfigManager config = GameConfigManager.Instance;
        float movement = config != null ? Mathf.Clamp01(config.ScreenShake) : 1f;
        float flash = config != null ? Mathf.Clamp01(config.FlashIntensity) : 1f;
        float separation = Mathf.Clamp(separationPixels, 0, 32) * movement;
        float twist = Mathf.Clamp(twistDegrees, -5, 5) * movement;
        float width = Mathf.Clamp(lineWidthPixels, 0, 4);
        Color color = lineColor; color.a = Mathf.Clamp01(color.a) * flash;
        if (separation <= 0 && Mathf.Abs(twist) <= .0001f && (width <= 0 || color.a <= 0)) return default;
        float angle = Mathf.Repeat(angleDegrees, 360) * Mathf.Deg2Rad;
        int id = camera.GetInstanceID();
        long token = ++_nextToken;
        if (token == 0) token = ++_nextToken;
        Entries[id] = new Entry { Token = token, Owner = owner, IsActive = isActive,
            Tangent = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)), Separation = separation,
            Twist = twist, LineWidth = width, Color = color };
        if (!_listening)
        {
            SceneManager.activeSceneChanged += SceneChanged;
            SceneManager.sceneUnloaded += SceneUnloaded;
            _listening = true;
        }
        var lease = new Lease(id, token);
        lease.SetStrength(initialStrength);
        return lease;
    }

    /// <summary>타격 프레임은 최대 강도, 초기 짧은 유지 이후 부드럽게0으로 복원합니다.</summary>
    public static float EvaluateEnvelope(float normalized, float attackFraction = .12f)
    {
        if (!Finite(normalized) || !Finite(attackFraction)) return 0;
        float hold = Mathf.Clamp(attackFraction, 0, .5f);
        float recovery = Mathf.Clamp01((Mathf.Clamp01(normalized) - hold) / (1f - hold));
        float remaining = 1f - recovery;
        return remaining * remaining * remaining;
    }

    /// <summary>확대/유지/복원을 양끝 기울기0인 cubic smoothstep으로 연결합니다.</summary>
    public static float EvaluateSwell(float elapsed, float expand, float hold, float restore)
    {
        if (!Finite(elapsed) || !Finite(expand) || !Finite(hold) || !Finite(restore)
            || expand < 0 || hold < 0 || restore <= 0 || !Finite(expand + hold + restore)) return 0;
        if (expand > 0 && elapsed < expand)
        {
            float progress = Mathf.Clamp01(elapsed / expand);
            return progress * progress * (3f - 2f * progress);
        }
        float recovery = Mathf.Clamp01((elapsed - expand - hold) / restore);
        return 1f - recovery * recovery * (3f - 2f * recovery);
    }

    /// <summary>최대로 벌어지는 순간 전후에만 외곽선을 강조합니다. 추가 텍스처는 쓰지 않습니다.</summary>
    public static float EvaluateOutlinePulse(float elapsed, float peakTime)
    {
        if (!Finite(elapsed) || !Finite(peakTime) || peakTime < 0) return 0;
        float pulse = Mathf.Clamp01(1f - Mathf.Abs(elapsed - peakTime) / .16f);
        return pulse * pulse;
    }

    public static bool TryGetFrame(Camera camera, out Frame frame)
    {
        frame = default;
        if (Entries.Count == 0 || camera == null) return false;
        int id = camera.GetInstanceID();
        if (!Entries.TryGetValue(id, out Entry value)) return false;
        bool active = OwnerIsActive(value.Owner);
        if (active && value.IsActive != null)
        {
            try { active = value.IsActive(); }
            catch (Exception) { active = false; }
        }
        if (!active) { Remove(id); return false; }
        frame = value.Frame; return true;
    }

    private static bool OwnerIsActive(Component owner)
        => owner != null && owner.gameObject.activeInHierarchy && (!(owner is Behaviour behaviour) || behaviour.isActiveAndEnabled);
    private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

    private static void Remove(int id)
    {
        Entries.Remove(id);
        if (Entries.Count == 0) StopListening();
    }
    private static void SceneChanged(Scene oldScene, Scene newScene) => Clear();
    private static void SceneUnloaded(Scene scene) => Clear();
    private static void StopListening()
    {
        if (!_listening) return;
        SceneManager.activeSceneChanged -= SceneChanged;
        SceneManager.sceneUnloaded -= SceneUnloaded;
        _listening = false;
    }
    private static void Clear() { Entries.Clear(); StopListening(); }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    // Keep tokens monotonic when Domain Reload is disabled: an old lease must never match a new session.
    private static void ResetSession() => Clear();
}
