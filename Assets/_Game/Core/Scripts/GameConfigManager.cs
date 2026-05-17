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

    public const float DefaultVolume = 0.8f;
    public const float BgmOutputCompensation = 0.2f;

    private const string MasterVolumeKey = "Config.MasterVolume";
    private const string BgmVolumeKey = "Config.BGMVolume";
    private const string SfxVolumeKey = "Config.SFXVolume";
    private const string FullscreenKey = "Config.Fullscreen";
    private const string LanguageKey = "Config.Language";
    private const string KeyPrefix = "Config.Key.";
    private const string TextSpeedKey = "Config.TextSpeed";
    private const string AutoAdvanceKey = "Config.AutoAdvance";
    private const string ScreenShakeKey = "Config.ScreenShake";
    private const string VSyncKey = "Config.VSync";
    private const string TargetFpsKey = "Config.TargetFps";
    private const int DefaultWindowWidth = 640;
    private const int DefaultWindowHeight = 480;

    public float MasterVolume { get; private set; } = DefaultVolume;
    public float BgmVolume { get; private set; } = DefaultVolume;
    public float SfxVolume { get; private set; } = DefaultVolume;
    public bool IsFullscreen { get; private set; } = false;
    public LanguageType Language { get; private set; } = LanguageType.KR;
    public float TextSpeed { get; private set; } = 1f;
    public bool AutoAdvance { get; private set; } = false;
    public float ScreenShake { get; private set; } = 1f;
    public bool UseVSync { get; private set; } = false;
    public int TargetFps { get; private set; } = 60;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        Load();
        ApplyAll();
    }

    public static GameConfigManager EnsureInstance()
    {
        if (Instance != null) return Instance;

        var go = new GameObject("[GameConfigManager]");
        return go.AddComponent<GameConfigManager>();
    }

    public void Load()
    {
        MasterVolume = PlayerPrefs.GetFloat(MasterVolumeKey, DefaultVolume);
        BgmVolume = PlayerPrefs.GetFloat(BgmVolumeKey, DefaultVolume);
        SfxVolume = PlayerPrefs.GetFloat(SfxVolumeKey, DefaultVolume);
        IsFullscreen = PlayerPrefs.GetInt(FullscreenKey, 0) == 1;
        Language = (LanguageType)PlayerPrefs.GetInt(LanguageKey, (int)LanguageType.KR);
        TextSpeed = PlayerPrefs.GetFloat(TextSpeedKey, 1f);
        AutoAdvance = PlayerPrefs.GetInt(AutoAdvanceKey, 0) == 1;
        ScreenShake = PlayerPrefs.GetFloat(ScreenShakeKey, 1f);
        UseVSync = PlayerPrefs.GetInt(VSyncKey, 0) == 1;
        TargetFps = PlayerPrefs.GetInt(TargetFpsKey, 60);
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
        PlayerPrefs.SetInt(VSyncKey, UseVSync ? 1 : 0);
        PlayerPrefs.SetInt(TargetFpsKey, TargetFps);
        PlayerPrefs.Save();
    }

    public void ApplyAll()
    {
        ApplyAudio();
        ApplyDisplayMode();
        QualitySettings.vSyncCount = UseVSync ? 1 : 0;
        Application.targetFrameRate = UseVSync ? -1 : TargetFps;
        LocalizationManager.Instance?.ChangeLanguage(Language);
    }

    private void ApplyDisplayMode()
    {
        if (IsFullscreen)
        {
            Screen.fullScreenMode = FullScreenMode.FullScreenWindow;
            Screen.fullScreen = true;
        }
        else
        {
            Screen.fullScreen = false;
            Screen.fullScreenMode = FullScreenMode.Windowed;
            Screen.SetResolution(DefaultWindowWidth, DefaultWindowHeight, FullScreenMode.Windowed);
        }
    }

    public void ApplyAudio()
    {
        AudioManager.Instance?.ApplyConfiguredVolumes(MasterVolume, BgmVolume, SfxVolume);
    }

    public void SetMasterVolume(float value)
    {
        MasterVolume = Mathf.Clamp01(value);
        AudioManager.Instance?.ApplyConfiguredVolumes(MasterVolume, BgmVolume, SfxVolume);
        Save();
    }

    public void SetBgmVolume(float value)
    {
        BgmVolume = Mathf.Clamp01(value);
        AudioManager.Instance?.ApplyConfiguredVolumes(MasterVolume, BgmVolume, SfxVolume);
        Save();
    }

    public void SetSfxVolume(float value)
    {
        SfxVolume = Mathf.Clamp01(value);
        AudioManager.Instance?.ApplyConfiguredVolumes(MasterVolume, BgmVolume, SfxVolume);
        Save();
    }

    public void SetFullscreen(bool value)
    {
        IsFullscreen = value;
        ApplyDisplayMode();
        Save();
    }

    public void SetLanguage(LanguageType language)
    {
        Language = language;
        LocalizationManager.Instance?.ChangeLanguage(language);
        Save();
    }

    public void SetTextSpeed(float value) { TextSpeed = Mathf.Clamp(value, 0.5f, 2f); Save(); }
    public void SetAutoAdvance(bool value) { AutoAdvance = value; Save(); }
    public void SetScreenShake(float value) { ScreenShake = Mathf.Clamp01(value); Save(); }
    public void SetVSync(bool value) { UseVSync = value; ApplyAll(); Save(); }
    public void SetTargetFps(int value) { TargetFps = Mathf.Clamp(value, 30, 240); ApplyAll(); Save(); }

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
        Language = LanguageType.KR;
        TextSpeed = 1f;
        AutoAdvance = false;
        ScreenShake = 1f;
        UseVSync = false;
        TargetFps = 60;

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
}