using System;
using UnityEngine;
using UnityEngine.InputSystem;

public enum GameInputDeviceKind { Keyboard, Gamepad, Touch }

/// <summary>기존 액션의 기기별 연결과 안내만 담당합니다. 모든 기기를 동시에 받습니다.</summary>
public static partial class GameInput
{
    public static event Action<GameInputDeviceKind> DeviceChanged;
    public static GameInputDeviceKind ActiveDevice { get; private set; }
    public static string ActiveGamepadLayout { get; private set; } = string.Empty;
    internal const string TouchDeviceName = "HubToHomeTouch";
    private static int _activeHardwareDeviceId = -1;
    private static InputAction _battlePause;

    // C는 전투의 특수 반격/QTE로 유지합니다. 일시정지는 충돌하지 않는 별도 입력입니다.
    public static bool BattlePausePressed
    {
        get
        {
            if (_configModalActive || BattleUIInputConsumed || Time.frameCount <= _suppressSharedUIUntilFrame)
                return false;
            EnsureInitialized();
            return _battlePause != null && _battlePause.WasPressedThisFrame();
        }
    }

    internal static void InitializeRuntimeInput() => EnsureInitialized();

    private static void BindDeviceInputs()
    {
        _battlePause = _ui.AddAction("BattlePause", InputActionType.Button);
        _battlePause.AddBinding("<Keyboard>/escape");
        _battlePause.AddBinding("<Gamepad>/start");
        BindAxis(_playerMove); BindAxis(_uiNavigate); BindAxis(_battleNavigate);
        BindAxis(_configNavigate); BindAxis(_configAdjust);
        BindButton(_playerConfirm, "buttonSouth"); BindButton(_uiSubmit, "buttonSouth");
        BindButton(_battleConfirm, "buttonSouth"); BindButton(_dialogueAdvance, "buttonSouth");
        BindButton(_choice1, "buttonSouth"); BindButton(_configSubmit, "buttonSouth");
        BindButton(_playerCancel, "buttonEast"); BindButton(_uiCancel, "buttonEast");
        BindButton(_battleCancel, "buttonEast"); BindButton(_choice2, "buttonEast");
        BindButton(_configBack, "buttonEast");
        BindButton(_playerMenu, "buttonNorth"); BindButton(_uiMenu, "buttonNorth");
        BindButton(_choice3, "buttonNorth"); BindButton(_playerRun, "leftShoulder");
        BindButton(_configReset, "select");
        _player.actionTriggered += OnDeviceAction;
        _ui.actionTriggered += OnDeviceAction;
        _battle.actionTriggered += OnDeviceAction;
        _dialogue.actionTriggered += OnDeviceAction;
        _config.actionTriggered += OnDeviceAction;
        InputSystem.onDeviceChange -= OnInputDeviceChange;
        InputSystem.onDeviceChange += OnInputDeviceChange;
    }

    private static void BindAxis(InputAction action)
    {
        action.AddBinding("<Gamepad>/dpad");
        action.AddBinding("<Gamepad>/leftStick");
    }

    private static void BindButton(InputAction action, string control)
        => action.AddBinding("<Gamepad>/" + control);

    private static void OnDeviceAction(InputAction.CallbackContext context)
    {
        if (context.phase != InputActionPhase.Performed || context.control == null) return;
        InputDevice device = context.control.device;
        if (device is Gamepad && context.valueType == typeof(Vector2)
            && context.ReadValue<Vector2>().sqrMagnitude < 0.25f) return;
        if (device.name == TouchDeviceName) SetActiveDevice(GameInputDeviceKind.Touch, string.Empty, device.deviceId);
        else if (device is Gamepad) SetActiveDevice(GameInputDeviceKind.Gamepad, device.layout, device.deviceId);
        else if (device is Keyboard) SetActiveDevice(GameInputDeviceKind.Keyboard, string.Empty, device.deviceId);
    }

    private static void OnInputDeviceChange(InputDevice device, InputDeviceChange change)
    {
        if (!(device is Gamepad) || device.name == TouchDeviceName) return;
        if (change != InputDeviceChange.Disconnected && change != InputDeviceChange.Removed
            && change != InputDeviceChange.Disabled) return;
        if (ActiveDevice == GameInputDeviceKind.Gamepad && device.deviceId == _activeHardwareDeviceId)
        {
            SetActiveDevice(Application.isMobilePlatform ? GameInputDeviceKind.Touch
                : GameInputDeviceKind.Keyboard, string.Empty);
            ResetCachedState();
            ConsumeBattleUIInput();
        }
    }

    private static void SetActiveDevice(GameInputDeviceKind kind, string layout, int deviceId = -1)
    {
        _activeHardwareDeviceId = deviceId;
        if (ActiveDevice == kind && ActiveGamepadLayout == layout) return;
        ActiveDevice = kind;
        ActiveGamepadLayout = layout;
        DeviceChanged?.Invoke(kind);
    }

    public static string GetActionLabel(ConfigurableAction action)
    {
        if (ActiveDevice == GameInputDeviceKind.Keyboard)
            return GameConfigManager.EnsureInstance().GetKey(action).ToString().ToUpperInvariant();
        if (ActiveDevice == GameInputDeviceKind.Touch)
            return action == ConfigurableAction.Confirm ? "Z"
                : action == ConfigurableAction.Cancel ? "X" : action == ConfigurableAction.Menu ? "C" : "↑↓←→";
        bool playStation = ActiveGamepadLayout.IndexOf("Dual", StringComparison.OrdinalIgnoreCase) >= 0;
        // 확인되지 않은 Nintendo A/B 배치를 추측하지 않고 위치 표기를 기본으로 사용합니다.
        return action == ConfigurableAction.Confirm ? (playStation ? "×" : "↓")
            : action == ConfigurableAction.Cancel ? (playStation ? "○" : "→")
            : action == ConfigurableAction.Menu ? (playStation ? "△" : "↑")
            : action == ConfigurableAction.Run ? "L1 / LB" : "✚";
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetDeviceSession()
    {
        InputSystem.onDeviceChange -= OnInputDeviceChange;
        _generatedActions?.Dispose();
        _generatedActions = null;
        _asset = null;
        _battlePause = null;
        DeviceChanged = null;
        ActiveDevice = Application.isMobilePlatform ? GameInputDeviceKind.Touch : GameInputDeviceKind.Keyboard;
        ActiveGamepadLayout = string.Empty;
        _activeHardwareDeviceId = -1;
        _configModalActive = false;
        _qteZPressedAt = _qteXPressedAt = _qteCPressedAt = -1d;
        _consumedZAt = _consumedXAt = _consumedCAt = -2d;
        _consumedBattleUIFrame = _suppressPlayerConfirmUntilFrame = -1;
        _suppressSharedUIUntilFrame = -1;
        ResetCachedState();
    }
}
