using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

/// <summary>등록된 카메라의 저스트가드 배경 파장. 전투 규칙·코루틴·렌더 리소스는 소유하지 않습니다.</summary>
public static class ParryBackgroundRipple
{
    public readonly struct Frame
    {
        public Frame(Vector3 originWorld, float radiusPixels, float widthPixels, float amplitudePixels, Color ringColor)
        {
            OriginWorld = originWorld;
            RadiusPixels = radiusPixels;
            WidthPixels = widthPixels;
            AmplitudePixels = amplitudePixels;
            RingColor = ringColor;
        }

        public Vector3 OriginWorld { get; }
        public float RadiusPixels { get; }
        public float WidthPixels { get; }
        public float AmplitudePixels { get; }
        public Color RingColor { get; }
    }

    private sealed class CameraState
    {
        public Camera Camera;
        public ParryBackgroundRippleProfile Profile;
        public ScriptableRendererFeature Feature;
        public PlayerController Owner;
        public PlayerCharacter Character;
        public Vector3 OriginWorld;
        public float Duration, StartRadius, EndRadius, Width, Amplitude, Elapsed;
        public Color RingColor;
        public int LastClockFrame, LastRequestFrame = int.MinValue;
        public readonly HashSet<int> RequestOwners = new HashSet<int>();
        public bool Active;

        public void Stop()
        {
            Active = false;
            Owner = null;
            Character = null;
        }
    }

    private static readonly Dictionary<int, CameraState> Cameras = new Dictionary<int, CameraState>(2);
    private static readonly List<int> RemovalKeys = new List<int>(2);
    private static bool _listening;

    public static void Register(Camera camera, ParryBackgroundRippleProfile profile, ScriptableRendererFeature owner)
    {
        if (camera == null || profile == null || owner == null || camera.cameraType != CameraType.Game) return;
        int id = camera.GetInstanceID();
        if (Cameras.TryGetValue(id, out CameraState state))
        {
            if (state.Camera == camera && state.Profile == profile && state.Feature == owner) return;
            state.Stop();
            state.LastRequestFrame = int.MinValue;
            state.RequestOwners.Clear();
        }
        else
        {
            state = new CameraState();
            Cameras.Add(id, state);
        }
        state.Camera = camera;
        state.Profile = profile;
        state.Feature = owner;
        if (_listening) return;
        SceneManager.activeSceneChanged += SceneChanged;
        SceneManager.sceneUnloaded += SceneUnloaded;
        _listening = true;
    }

    public static void Unregister(ScriptableRendererFeature owner)
    {
        // Unity가 먼저 파괴한 Feature도 C# 참조가 같으면 자신이 등록한 카메라를 해제합니다.
        if (ReferenceEquals(owner, null)) return;
        RemovalKeys.Clear();
        foreach (KeyValuePair<int, CameraState> pair in Cameras)
            if (ReferenceEquals(pair.Value.Feature, owner)) RemovalKeys.Add(pair.Key);
        for (int i = 0; i < RemovalKeys.Count; i++) Cameras.Remove(RemovalKeys[i]);
        RemovalKeys.Clear();
        if (Cameras.Count == 0) StopListening();
    }

    public static bool TryPlay(PlayerController owner, Vector3 originWorld)
    {
        if (Cameras.Count == 0 || !OwnerIsActive(owner) || !Finite(originWorld)) return false;
        GameConfigManager config = GameConfigManager.Instance;
        return TryPlayAt(Camera.main, owner, originWorld, Time.frameCount,
            config != null ? config.ScreenShake : GameConfigManager.DefaultScreenShake,
            config != null ? config.FlashIntensity : GameConfigManager.DefaultFlashIntensity);
    }

    private static bool TryPlayAt(Camera camera, PlayerController owner, Vector3 originWorld,
        int frame, float movement, float flash)
    {
        if (camera == null || !camera.isActiveAndEnabled
            || !OwnerIsActive(owner) || !Finite(originWorld)
            || !Cameras.TryGetValue(camera.GetInstanceID(), out CameraState state)
            || state.Feature == null || !state.Feature.isActive || state.Profile == null
            || !state.Profile.HasValidSettings) return false;
        PlayerCharacter character = owner.GetComponent<PlayerCharacter>();
        if (character == null || !character.IsAlive) return false;

        int ownerId = owner.GetInstanceID();
        if (state.LastRequestFrame == frame && state.RequestOwners.Contains(ownerId)) return false;
        ParryBackgroundRippleProfile profile = state.Profile;
        Color color = profile.RingColor;
        color.a *= VisualAccessibilityPolicy.NormalizeScale(flash);
        float amplitude = profile.DisplacementPixels * VisualAccessibilityPolicy.NormalizeScale(movement);
        if (amplitude <= 0f && color.a <= 0f) return false;

        state.Owner = owner;
        state.Character = character;
        state.OriginWorld = originWorld;
        state.Duration = profile.Duration;
        state.StartRadius = profile.StartRadiusPixels;
        state.EndRadius = profile.EndRadiusPixels;
        state.Width = profile.BandWidthPixels;
        state.Amplitude = amplitude;
        state.RingColor = color;
        state.Elapsed = 0f;
        state.LastClockFrame = frame;
        if (state.LastRequestFrame != frame) state.RequestOwners.Clear();
        state.LastRequestFrame = frame;
        state.RequestOwners.Add(ownerId);
        state.Active = true;
        return true;
    }

    public static bool TryGetFrame(Camera camera, out Frame frame)
        => TryGetFrameAt(camera, Time.frameCount, Time.unscaledDeltaTime, BattleDelay.IsPaused, out frame);

    // 명시적 시계 입력은 중복 조회·메뉴 정지 정책을 Unity 재생 없이 검증하기 위한 내부 경계입니다.
    private static bool TryGetFrameAt(Camera camera, int frameIndex, float delta, bool paused, out Frame frame)
    {
        frame = default;
        if (camera == null || !Cameras.TryGetValue(camera.GetInstanceID(), out CameraState state)
            || !state.Active) return false;
        if (!camera.isActiveAndEnabled || state.Feature == null || !state.Feature.isActive
            || !OwnerIsActive(state.Owner) || state.Character == null || !state.Character.IsAlive)
        {
            state.Stop();
            return false;
        }
        if (state.LastClockFrame != frameIndex)
        {
            state.LastClockFrame = frameIndex;
            if (!paused)
            {
                if (!Finite(delta)) { state.Stop(); return false; }
                state.Elapsed += Mathf.Max(0f, delta);
            }
        }
        if (state.Elapsed >= state.Duration)
        {
            state.Stop();
            return false;
        }
        float progress = Mathf.Clamp01(state.Elapsed / state.Duration);
        float remaining = 1f - progress;
        float fade = remaining * remaining;
        Color color = state.RingColor;
        color.a *= fade;
        frame = new Frame(state.OriginWorld, Mathf.Lerp(state.StartRadius, state.EndRadius, progress),
            state.Width, state.Amplitude * fade, color);
        return true;
    }

    private static bool OwnerIsActive(PlayerController owner)
        => owner != null && owner.isActiveAndEnabled && owner.State == PlayerController.PlayerState.InBattle;
    private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    private static bool Finite(Vector3 value) => Finite(value.x) && Finite(value.y) && Finite(value.z);
    private static void SceneChanged(Scene oldScene, Scene newScene) => Clear();
    private static void SceneUnloaded(Scene scene) => Clear();
    private static void StopListening()
    {
        if (!_listening) return;
        SceneManager.activeSceneChanged -= SceneChanged;
        SceneManager.sceneUnloaded -= SceneUnloaded;
        _listening = false;
    }
    private static void Clear() { Cameras.Clear(); RemovalKeys.Clear(); StopListening(); }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetSession() => Clear();
}
