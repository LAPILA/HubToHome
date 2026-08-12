using System;
using UnityEngine;
using UnityEngine.InputSystem;

public enum ConfigurableAction
{
    Up,
    Down,
    Left,
    Right,
    Confirm,
    Cancel,
    Run,
    Menu
}

/// <summary>
/// 타이틀/인게임에서 공통으로 사용하는 환경 설정 저장소.
/// 볼륨, 전체화면, 언어, 키 설정을 PlayerPrefs에 저장하고 런타임에 적용합니다.
/// </summary>
public class GameConfigManager : MonoBehaviour
{
    public static GameConfigManager Instance { get; private set; }
    public static event Action DisplaySettingsChanged;

    public const float DefaultVolume = 0.8f;
    public const float BgmOutputCompensation = 0.2f;
    public const float DefaultTextSpeed = 1f;
    public const float DefaultScreenShake = 1f;
    public const float DefaultFlashIntensity = 1f;
    public const int DefaultTargetFps = 60;
    public const int DefaultWindowScale = GameConfigPolicy.MinWindowScale;
    public const LanguageType DefaultLanguage = LanguageType.KR;

    private const string MasterVolumeKey = "Config.MasterVolume";
    private const string BgmVolumeKey = "Config.BGMVolume";
    private const string SfxVolumeKey = "Config.SFXVolume";
    private const string FullscreenKey = "Config.Fullscreen";
    private const string LanguageKey = "Config.Language";
    private const string KeyPrefix = "Config.Key.";
    private const string TextSpeedKey = "Config.TextSpeed";
    private const string AutoAdvanceKey = "Config.AutoAdvance";
    private const string ScreenShakeKey = "Config.ScreenShake";
    private const string FlashIntensityKey = "Config.FlashIntensity";
    private const string VSyncKey = "Config.VSync";
    private const string TargetFpsKey = "Config.TargetFps";
    private const string WindowScaleKey = "Config.WindowScale";

    public float MasterVolume { get; private set; } = DefaultVolume;
    public float BgmVolume { get; private set; } = DefaultVolume;
    public float SfxVolume { get; private set; } = DefaultVolume;
    public bool IsFullscreen { get; private set; } = false;
    public LanguageType Language { get; private set; } = DefaultLanguage;
    public float TextSpeed { get; private set; } = DefaultTextSpeed;
    public bool AutoAdvance { get; private set; } = false;
    public float ScreenShake { get; private set; } = DefaultScreenShake;
    public float FlashIntensity { get; private set; } = DefaultFlashIntensity;
    public bool UseVSync { get; private set; } = false;
    public int TargetFps { get; private set; } = DefaultTargetFps;
    public int WindowScale { get; private set; } = DefaultWindowScale;
    public Vector2Int WindowSize => GameConfigPolicy.ResolveWindowSize(WindowScale);
    public bool IsHandheldPlatform => GameConfigPolicy.IsHandheldPlatform(
        Application.platform,
        SystemInfo.deviceType);

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        Load();
        ApplyAll();
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    public static GameConfigManager EnsureInstance()
    {
        if (Instance != null) return Instance;

        var go = new GameObject("[GameConfigManager]");
        return go.AddComponent<GameConfigManager>();
    }

    public void Load()
    {
        MasterVolume = GameConfigPolicy.NormalizeUnit(
            PlayerPrefs.GetFloat(MasterVolumeKey, DefaultVolume), DefaultVolume);
        BgmVolume = GameConfigPolicy.NormalizeUnit(
            PlayerPrefs.GetFloat(BgmVolumeKey, DefaultVolume), DefaultVolume);
        SfxVolume = GameConfigPolicy.NormalizeUnit(
            PlayerPrefs.GetFloat(SfxVolumeKey, DefaultVolume), DefaultVolume);
        IsFullscreen = PlayerPrefs.GetInt(FullscreenKey, 0) == 1;
        Language = GameConfigPolicy.NormalizeLanguage(
            PlayerPrefs.GetInt(LanguageKey, (int)DefaultLanguage), DefaultLanguage);
        TextSpeed = GameConfigPolicy.NormalizeFinite(
            PlayerPrefs.GetFloat(TextSpeedKey, DefaultTextSpeed), 0.5f, 2f, DefaultTextSpeed);
        AutoAdvance = PlayerPrefs.GetInt(AutoAdvanceKey, 0) == 1;
        ScreenShake = GameConfigPolicy.NormalizeUnit(
            PlayerPrefs.GetFloat(ScreenShakeKey, DefaultScreenShake), DefaultScreenShake);
        FlashIntensity = GameConfigPolicy.NormalizeUnit(
            PlayerPrefs.GetFloat(FlashIntensityKey, DefaultFlashIntensity), DefaultFlashIntensity);
        UseVSync = PlayerPrefs.GetInt(VSyncKey, 0) == 1;
        TargetFps = NormalizeTargetFps(
            PlayerPrefs.GetInt(TargetFpsKey, DefaultTargetFps));
        WindowScale = GameConfigPolicy.NormalizeWindowScale(
            PlayerPrefs.GetInt(WindowScaleKey, DefaultWindowScale));
    }

    public void Save()
    {
        PlayerPrefs.SetFloat(MasterVolumeKey, MasterVolume);
        PlayerPrefs.SetFloat(BgmVolumeKey, BgmVolume);
        PlayerPrefs.SetFloat(SfxVolumeKey, SfxVolume);
        PlayerPrefs.SetInt(FullscreenKey, IsFullscreen ? 1 : 0);
        PlayerPrefs.SetInt(LanguageKey, (int)Language);
        PlayerPrefs.SetFloat(TextSpeedKey, TextSpeed);
        PlayerPrefs.SetInt(AutoAdvanceKey, AutoAdvance ? 1 : 0);
        PlayerPrefs.SetFloat(ScreenShakeKey, ScreenShake);
        PlayerPrefs.SetFloat(FlashIntensityKey, FlashIntensity);
        PlayerPrefs.SetInt(VSyncKey, UseVSync ? 1 : 0);
        PlayerPrefs.SetInt(TargetFpsKey, TargetFps);
        PlayerPrefs.SetInt(WindowScaleKey, WindowScale);
        PlayerPrefs.Save();
    }

    public void ApplyAll()
    {
        ApplyAudio();
        ApplyDisplayMode();
        ApplyFrameTiming();
        LocalizationManager.Instance?.ChangeLanguage(Language);
    }

    private void ApplyDisplayMode()
    {
        if (IsFullscreen)
        {
            Resolution resolution = Screen.currentResolution;
            int width = Mathf.Max(GameConfigPolicy.ReferenceWidth, resolution.width);
            int height = Mathf.Max(GameConfigPolicy.ReferenceHeight, resolution.height);
            Screen.SetResolution(width, height, FullScreenMode.FullScreenWindow);
        }
        else
        {
            Vector2Int size = WindowSize;
            Screen.SetResolution(size.x, size.y, FullScreenMode.Windowed);
        }

        DisplaySettingsChanged?.Invoke();
    }

    private void ApplyFrameTiming()
    {
        QualitySettings.vSyncCount = UseVSync ? 1 : 0;
        Application.targetFrameRate = UseVSync ? -1 : TargetFps;
    }

    public void ApplyAudio()
    {
        AudioManager.Instance?.ApplyConfiguredVolumes(MasterVolume, BgmVolume, SfxVolume);
    }

    public void SetMasterVolume(float value)
    {
        float normalized = GameConfigPolicy.NormalizeUnit(value, DefaultVolume);
        if (Mathf.Approximately(MasterVolume, normalized)) return;

        MasterVolume = normalized;
        AudioManager.Instance?.ApplyConfiguredVolumes(MasterVolume, BgmVolume, SfxVolume);
        Save();
    }

    public void SetBgmVolume(float value)
    {
        float normalized = GameConfigPolicy.NormalizeUnit(value, DefaultVolume);
        if (Mathf.Approximately(BgmVolume, normalized)) return;

        BgmVolume = normalized;
        AudioManager.Instance?.ApplyConfiguredVolumes(MasterVolume, BgmVolume, SfxVolume);
        Save();
    }

    public void SetSfxVolume(float value)
    {
        float normalized = GameConfigPolicy.NormalizeUnit(value, DefaultVolume);
        if (Mathf.Approximately(SfxVolume, normalized)) return;

        SfxVolume = normalized;
        AudioManager.Instance?.ApplyConfiguredVolumes(MasterVolume, BgmVolume, SfxVolume);
        Save();
    }

    public void SetFullscreen(bool value)
    {
        if (IsFullscreen == value) return;

        IsFullscreen = value;
        ApplyDisplayMode();
        Save();
    }

    public void SetWindowScale(int value)
    {
        int normalized = GameConfigPolicy.NormalizeWindowScale(value);
        if (WindowScale == normalized) return;

        WindowScale = normalized;
        if (!IsFullscreen)
            ApplyDisplayMode();
        Save();
    }

    public void SetLanguage(LanguageType language)
    {
        LanguageType normalized = GameConfigPolicy.NormalizeLanguage(
            (int)language, DefaultLanguage);
        if (Language == normalized) return;

        Language = normalized;
        LocalizationManager.Instance?.ChangeLanguage(normalized);
        Save();
    }

    public void SetTextSpeed(float value)
    {
        float normalized = GameConfigPolicy.NormalizeFinite(
            value, 0.5f, 2f, DefaultTextSpeed);
        if (Mathf.Approximately(TextSpeed, normalized)) return;

        TextSpeed = normalized;
        Save();
    }

    public void SetAutoAdvance(bool value)
    {
        if (AutoAdvance == value) return;

        AutoAdvance = value;
        Save();
    }

    public void SetScreenShake(float value)
    {
        float normalized = GameConfigPolicy.NormalizeUnit(value, DefaultScreenShake);
        if (Mathf.Approximately(ScreenShake, normalized)) return;

        ScreenShake = normalized;
        Save();
    }

    public void SetFlashIntensity(float value)
    {
        float normalized = GameConfigPolicy.NormalizeUnit(value, DefaultFlashIntensity);
        if (Mathf.Approximately(FlashIntensity, normalized)) return;

        FlashIntensity = normalized;
        Save();
    }

    public void SetVSync(bool value)
    {
        if (UseVSync == value) return;

        UseVSync = value;
        ApplyFrameTiming();
        Save();
    }

    public void SetTargetFps(int value)
    {
        int normalized = NormalizeTargetFps(value);
        if (TargetFps == normalized) return;

        TargetFps = normalized;
        ApplyFrameTiming();
        Save();
    }

    public void AdjustTargetFps(int direction)
    {
        SetTargetFps(GameConfigPolicy.StepTargetFps(
            TargetFps,
            direction,
            IsHandheldPlatform));
    }

    public Key GetKey(ConfigurableAction action)
    {
        string defaultKey = GetDefaultKey(action).ToString();
        string savedKey = PlayerPrefs.GetString(KeyPrefix + action, defaultKey);
        return Enum.TryParse(savedKey, out Key key) ? key : GetDefaultKey(action);
    }

    public void SetKey(ConfigurableAction action, Key key)
    {
        PlayerPrefs.SetString(KeyPrefix + action, key.ToString());
        PlayerPrefs.Save();
        GameInput.RefreshKeyBindings();
    }

    public void ResetControlsDefaults()
    {
        foreach (ConfigurableAction action in Enum.GetValues(typeof(ConfigurableAction)))
        {
            PlayerPrefs.DeleteKey(KeyPrefix + action);
        }
        PlayerPrefs.Save();
        GameInput.RefreshKeyBindings();
    }

    public void ResetDefaults()
    {
        MasterVolume = DefaultVolume;
        BgmVolume = DefaultVolume;
        SfxVolume = DefaultVolume;
        IsFullscreen = false;
        Language = DefaultLanguage;
        TextSpeed = DefaultTextSpeed;
        AutoAdvance = false;
        ScreenShake = DefaultScreenShake;
        FlashIntensity = DefaultFlashIntensity;
        UseVSync = false;
        TargetFps = NormalizeTargetFps(DefaultTargetFps);
        WindowScale = DefaultWindowScale;

        foreach (ConfigurableAction action in Enum.GetValues(typeof(ConfigurableAction)))
        {
            PlayerPrefs.DeleteKey(KeyPrefix + action);
        }

        ApplyAll();
        Save();
        GameInput.RefreshKeyBindings();
    }

    public static Key GetDefaultKey(ConfigurableAction action)
    {
        return action switch
        {
            ConfigurableAction.Up => Key.UpArrow,
            ConfigurableAction.Down => Key.DownArrow,
            ConfigurableAction.Left => Key.LeftArrow,
            ConfigurableAction.Right => Key.RightArrow,
            ConfigurableAction.Confirm => Key.Z,
            ConfigurableAction.Cancel => Key.X,
            ConfigurableAction.Run => Key.LeftShift,
            ConfigurableAction.Menu => Key.C,
            _ => Key.None
        };
    }

    public bool WasPressed(ConfigurableAction action)
    {
        return action switch
        {
            ConfigurableAction.Up => GameInput.MoveUpHeld,
            ConfigurableAction.Down => GameInput.MoveDownHeld,
            ConfigurableAction.Left => GameInput.MoveLeftHeld,
            ConfigurableAction.Right => GameInput.MoveRightHeld,
            ConfigurableAction.Confirm => GameInput.ConfirmPressed,
            ConfigurableAction.Cancel => GameInput.CancelPressed,
            ConfigurableAction.Run => GameInput.RunHeld,
            ConfigurableAction.Menu => GameInput.MenuPressed,
            _ => false
        };
    }

    public bool IsPressed(ConfigurableAction action)
    {
        return action switch
        {
            ConfigurableAction.Up => GameInput.MoveUpHeld,
            ConfigurableAction.Down => GameInput.MoveDownHeld,
            ConfigurableAction.Left => GameInput.MoveLeftHeld,
            ConfigurableAction.Right => GameInput.MoveRightHeld,
            ConfigurableAction.Confirm => GameInput.ConfirmPressed,
            ConfigurableAction.Cancel => GameInput.CancelPressed,
            ConfigurableAction.Run => GameInput.RunHeld,
            ConfigurableAction.Menu => GameInput.MenuPressed,
            _ => false
        };
    }

    private int NormalizeTargetFps(int value)
    {
        return GameConfigPolicy.NormalizeTargetFps(value, IsHandheldPlatform);
    }
}
