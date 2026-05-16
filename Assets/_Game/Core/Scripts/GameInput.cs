using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

/// <summary>
/// 프로젝트 전체 입력 Facade.
/// 다른 시스템은 Keyboard.current/InputAction을 직접 사용하지 않고 이 클래스만 바라봅니다.
/// 기본 입력은 델타룬/언더테일 스타일: 방향키 + Z/X/C 입니다.
/// </summary>
public static class GameInput
{
    private const float AxisThreshold = 0.5f;
    private static bool _configModalActive;

    private static InputSystem_Actions _generatedActions;
    private static InputActionAsset _asset;
    private static InputActionMap _player;
    private static InputActionMap _ui;
    private static InputActionMap _battle;
    private static InputActionMap _dialogue;
    private static InputActionMap _config;

    private static InputAction _playerMove;
    private static InputAction _playerConfirm;
    private static InputAction _playerCancel;
    private static InputAction _playerMenu;
    private static InputAction _playerRun;

    private static InputAction _uiNavigate;
    private static InputAction _uiSubmit;
    private static InputAction _uiCancel;
    private static InputAction _uiMenu;

    private static InputAction _battleNavigate;
    private static InputAction _battleConfirm;
    private static InputAction _battleCancel;
    private static InputAction _qteZ;
    private static InputAction _qteX;
    private static InputAction _qteC;

    private static InputAction _dialogueAdvance;
    private static InputAction _choice1;
    private static InputAction _choice2;
    private static InputAction _choice3;
    private static InputAction _langKR;
    private static InputAction _langEN;
    private static InputAction _langJP;
    private static InputAction _langCN;

    private static InputAction _configNavigate;
    private static InputAction _configAdjust;
    private static InputAction _configSubmit;
    private static InputAction _configBack;
    private static InputAction _configReset;
    private static int _cachedFrame = -1;

    private static Vector2 _prevPlayerMove;
    private static Vector2 _currPlayerMove;
    private static Vector2 _prevUINavigate;
    private static Vector2 _currUINavigate;
    private static Vector2 _prevBattleNavigate;
    private static Vector2 _currBattleNavigate;
    private static Vector2 _prevConfigNavigate;
    private static Vector2 _currConfigNavigate;
    private static Vector2 _prevConfigAdjust;
    private static Vector2 _currConfigAdjust;

    private static void EnsureInitialized()
    {
        if (_asset != null) return;

        _generatedActions = new InputSystem_Actions();
        _asset = _generatedActions.asset;

        if (_asset == null)
        {
            Debug.LogError("[GameInput] Failed to create InputSystem_Actions asset.");
            return;
        }

        _player = _asset.FindActionMap("Player", true);
        _ui = _asset.FindActionMap("UI", true);
        _battle = _asset.FindActionMap("Battle", true);
        _dialogue = _asset.FindActionMap("Dialogue", true);
        _config = _asset.FindActionMap("Config", true);

        _playerMove = _player.FindAction("Move", true);
        _playerConfirm = _player.FindAction("Confirm", true);
        _playerCancel = _player.FindAction("Cancel", true);
        _playerMenu = _player.FindAction("Menu", true);
        _playerRun = _player.FindAction("Run", true);

        _uiNavigate = _ui.FindAction("Navigate", true);
        _uiSubmit = _ui.FindAction("Submit", true);
        _uiCancel = _ui.FindAction("Cancel", true);
        _uiMenu = _ui.FindAction("Menu", true);

        _battleNavigate = _battle.FindAction("Navigate", true);
        _battleConfirm = _battle.FindAction("Confirm", true);
        _battleCancel = _battle.FindAction("Cancel", true);
        _qteZ = _battle.FindAction("QTE_Z", true);
        _qteX = _battle.FindAction("QTE_X", true);
        _qteC = _battle.FindAction("QTE_C", true);

        _dialogueAdvance = _dialogue.FindAction("Advance", true);
        _choice1 = _dialogue.FindAction("Choice1", true);
        _choice2 = _dialogue.FindAction("Choice2", true);
        _choice3 = _dialogue.FindAction("Choice3", true);
        _langKR = _dialogue.FindAction("LanguageKR", true);
        _langEN = _dialogue.FindAction("LanguageEN", true);
        _langJP = _dialogue.FindAction("LanguageJP", true);
        _langCN = _dialogue.FindAction("LanguageCN", true);

        _configNavigate = _config.FindAction("Navigate", true);
        _configAdjust = _config.FindAction("Adjust", true);
        _configSubmit = _config.FindAction("Submit", true);
        _configBack = _config.FindAction("Back", true);
        _configReset = _config.FindAction("ResetDefaults", true);

        ApplySavedKeyBindings();

        _player.Enable();
        _ui.Enable();
        _battle.Enable();
        _dialogue.Enable();
        _config.Enable();
    }

    public static Vector2 MoveVector
    {
        get
        {
            UpdateCache();
            return _currPlayerMove;
        }
    }

    public static bool MoveLeftHeld  { get { UpdateCache(); return IsLeft(_currPlayerMove); } }
    public static bool MoveRightHeld { get { UpdateCache(); return IsRight(_currPlayerMove); } }
    public static bool MoveUpHeld    { get { UpdateCache(); return IsUp(_currPlayerMove); } }
    public static bool MoveDownHeld  { get { UpdateCache(); return IsDown(_currPlayerMove); } }

    public static bool ConfirmPressed { get { if (_configModalActive) return false; EnsureInitialized(); return _playerConfirm.WasPressedThisFrame() || _uiSubmit.WasPressedThisFrame(); } }
    public static bool CancelPressed  { get { if (_configModalActive) return false; EnsureInitialized(); return _playerCancel.WasPressedThisFrame() || _uiCancel.WasPressedThisFrame(); } }
    public static bool MenuPressed    { get { if (_configModalActive) return false; EnsureInitialized(); return _playerMenu.WasPressedThisFrame() || _uiMenu.WasPressedThisFrame(); } }
    public static bool RunHeld        { get { if (_configModalActive) return false; EnsureInitialized(); return _playerRun.IsPressed(); } }

    public static bool UIUpPressed    { get { UpdateCache(); return PressedUp(_prevUINavigate, _currUINavigate); } }
    public static bool UIDownPressed  { get { UpdateCache(); return PressedDown(_prevUINavigate, _currUINavigate); } }
    public static bool UILeftPressed  { get { UpdateCache(); return PressedLeft(_prevUINavigate, _currUINavigate); } }
    public static bool UIRightPressed { get { UpdateCache(); return PressedRight(_prevUINavigate, _currUINavigate); } }
    public static bool UISubmitPressed { get { if (_configModalActive) return false; EnsureInitialized(); return _uiSubmit.WasPressedThisFrame(); } }
    public static bool UICancelPressed { get { if (_configModalActive) return false; EnsureInitialized(); return _uiCancel.WasPressedThisFrame(); } }
    public static bool UIMenuPressed   { get { if (_configModalActive) return false; EnsureInitialized(); return _uiMenu.WasPressedThisFrame(); } }

    public static bool BattleUpPressed    { get { UpdateCache(); return PressedUp(_prevBattleNavigate, _currBattleNavigate); } }
    public static bool BattleDownPressed  { get { UpdateCache(); return PressedDown(_prevBattleNavigate, _currBattleNavigate); } }
    public static bool BattleLeftPressed  { get { UpdateCache(); return PressedLeft(_prevBattleNavigate, _currBattleNavigate); } }
    public static bool BattleRightPressed { get { UpdateCache(); return PressedRight(_prevBattleNavigate, _currBattleNavigate); } }
    public static bool BattleConfirmPressed { get { if (_configModalActive) return false; EnsureInitialized(); return _battleConfirm.WasPressedThisFrame(); } }
    public static bool BattleCancelPressed  { get { if (_configModalActive) return false; EnsureInitialized(); return _battleCancel.WasPressedThisFrame(); } }
    public static bool QTEZPressed { get { if (_configModalActive) return false; EnsureInitialized(); return _qteZ.WasPressedThisFrame() || KeyboardPressed(Key.Z); } }
    public static bool QTEXPressed { get { if (_configModalActive) return false; EnsureInitialized(); return _qteX.WasPressedThisFrame() || KeyboardPressed(Key.X); } }
    public static bool QTECPressed { get { if (_configModalActive) return false; EnsureInitialized(); return _qteC.WasPressedThisFrame() || KeyboardPressed(Key.C); } }

    public static bool DialogueAdvancePressed { get { if (_configModalActive) return false; EnsureInitialized(); return _dialogueAdvance.WasPressedThisFrame(); } }
    public static bool Choice1Pressed { get { if (_configModalActive) return false; EnsureInitialized(); return _choice1.WasPressedThisFrame(); } }
    public static bool Choice2Pressed { get { if (_configModalActive) return false; EnsureInitialized(); return _choice2.WasPressedThisFrame(); } }
    public static bool Choice3Pressed { get { if (_configModalActive) return false; EnsureInitialized(); return _choice3.WasPressedThisFrame(); } }
    public static bool LanguageKRPressed { get { if (_configModalActive) return false; EnsureInitialized(); return _langKR.WasPressedThisFrame(); } }
    public static bool LanguageENPressed { get { if (_configModalActive) return false; EnsureInitialized(); return _langEN.WasPressedThisFrame(); } }
    public static bool LanguageJPPressed { get { if (_configModalActive) return false; EnsureInitialized(); return _langJP.WasPressedThisFrame(); } }
    public static bool LanguageCNPressed { get { if (_configModalActive) return false; EnsureInitialized(); return _langCN.WasPressedThisFrame(); } }

    public static bool ConfigUpPressed    { get { UpdateCache(); return PressedUp(_prevConfigNavigate, _currConfigNavigate); } }
    public static bool ConfigDownPressed  { get { UpdateCache(); return PressedDown(_prevConfigNavigate, _currConfigNavigate); } }
    public static bool ConfigLeftPressed
    {
        get
        {
            UpdateCache();
            // Config/Adjust 바인딩이 비어있거나 누락된 프리팹에서도
            // 방향키(=Navigate x축)로 좌/우 조절이 항상 동작하도록 fallback 처리
            return PressedLeft(_prevConfigAdjust, _currConfigAdjust)
                   || PressedLeft(_prevConfigNavigate, _currConfigNavigate);
        }
    }

    public static bool ConfigRightPressed
    {
        get
        {
            UpdateCache();
            return PressedRight(_prevConfigAdjust, _currConfigAdjust)
                   || PressedRight(_prevConfigNavigate, _currConfigNavigate);
        }
    }

    public static bool ConfigSubmitPressed
    {
        get
        {
            EnsureInitialized();
            bool actionPressed = _configSubmit.WasPressedThisFrame();

            // 액션맵 바인딩 이상/포커스 이슈 시에도 Z/Enter를 보조 입력으로 허용
            var keyboard = Keyboard.current;
            bool fallbackPressed = keyboard != null
                                   && ((keyboard.zKey != null && keyboard.zKey.wasPressedThisFrame)
                                       || (keyboard.enterKey != null && keyboard.enterKey.wasPressedThisFrame)
                                       || (keyboard.numpadEnterKey != null && keyboard.numpadEnterKey.wasPressedThisFrame));

            return actionPressed || fallbackPressed;
        }
    }
    public static bool ConfigBackPressed { get { EnsureInitialized(); return _configBack.WasPressedThisFrame(); } }
    public static bool ConfigResetDefaultsPressed { get { EnsureInitialized(); return _configReset.WasPressedThisFrame(); } }

    public static void SetConfigModalActive(bool active)
    {
        _configModalActive = active;
    }

    public static bool TryReadPressedKey(out Key key)
    {
        key = Key.None;
        if (!_configModalActive) return false;
        var keyboard = Keyboard.current;
        if (keyboard == null) return false;

        foreach (KeyControl keyControl in keyboard.allKeys)
        {
            if (!keyControl.wasPressedThisFrame) continue;
            key = keyControl.keyCode;
            return key != Key.None;
        }

        return false;
    }

    public static void ResetCachedState()
    {
        _prevPlayerMove = _currPlayerMove = Vector2.zero;
        _prevUINavigate = _currUINavigate = Vector2.zero;
        _prevBattleNavigate = _currBattleNavigate = Vector2.zero;
        _prevConfigNavigate = _currConfigNavigate = Vector2.zero;
        _prevConfigAdjust = _currConfigAdjust = Vector2.zero;
        _cachedFrame = -1;
    }

    public static void RefreshKeyBindings()
    {
        EnsureInitialized();
        ApplySavedKeyBindings();
        ResetCachedState();
    }

    private static void ApplySavedKeyBindings()
    {
        if (_asset == null) return;
        var config = GameConfigManager.EnsureInstance();

        ApplyMoveBindings(_playerMove, config.GetKey(ConfigurableAction.Up), config.GetKey(ConfigurableAction.Down), config.GetKey(ConfigurableAction.Left), config.GetKey(ConfigurableAction.Right));
        ApplyMoveBindings(_uiNavigate, config.GetKey(ConfigurableAction.Up), config.GetKey(ConfigurableAction.Down), config.GetKey(ConfigurableAction.Left), config.GetKey(ConfigurableAction.Right));
        ApplyMoveBindings(_battleNavigate, config.GetKey(ConfigurableAction.Up), config.GetKey(ConfigurableAction.Down), config.GetKey(ConfigurableAction.Left), config.GetKey(ConfigurableAction.Right));
        ApplyMoveBindings(_configNavigate, config.GetKey(ConfigurableAction.Up), config.GetKey(ConfigurableAction.Down), config.GetKey(ConfigurableAction.Left), config.GetKey(ConfigurableAction.Right));

        ApplyButtonBinding(_playerConfirm, config.GetKey(ConfigurableAction.Confirm));
        ApplyButtonBinding(_uiSubmit, config.GetKey(ConfigurableAction.Confirm));
        ApplyButtonBinding(_battleConfirm, config.GetKey(ConfigurableAction.Confirm));
        ApplyButtonBinding(_dialogueAdvance, config.GetKey(ConfigurableAction.Confirm));
        ApplyButtonBinding(_choice1, config.GetKey(ConfigurableAction.Confirm));
        ApplyButtonBinding(_qteZ, config.GetKey(ConfigurableAction.Confirm));
        ApplyButtonBinding(_configSubmit, config.GetKey(ConfigurableAction.Confirm));

        ApplyButtonBinding(_playerCancel, config.GetKey(ConfigurableAction.Cancel));
        ApplyButtonBinding(_uiCancel, config.GetKey(ConfigurableAction.Cancel));
        ApplyButtonBinding(_battleCancel, config.GetKey(ConfigurableAction.Cancel));
        ApplyButtonBinding(_choice2, config.GetKey(ConfigurableAction.Cancel));
        ApplyButtonBinding(_qteX, config.GetKey(ConfigurableAction.Cancel));
        ApplyButtonBinding(_configBack, config.GetKey(ConfigurableAction.Cancel));

        ApplyButtonBinding(_playerMenu, config.GetKey(ConfigurableAction.Menu));
        ApplyButtonBinding(_uiMenu, config.GetKey(ConfigurableAction.Menu));
        ApplyButtonBinding(_choice3, config.GetKey(ConfigurableAction.Menu));
        ApplyButtonBinding(_qteC, config.GetKey(ConfigurableAction.Menu));

        ApplyButtonBinding(_playerRun, config.GetKey(ConfigurableAction.Run));
    }

    private static void ApplyMoveBindings(InputAction action, Key up, Key down, Key left, Key right)
    {
        if (action == null) return;
        ApplyCompositePartBinding(action, "up", up);
        ApplyCompositePartBinding(action, "down", down);
        ApplyCompositePartBinding(action, "left", left);
        ApplyCompositePartBinding(action, "right", right);
    }

    private static void ApplyCompositePartBinding(InputAction action, string partName, Key key)
    {
        string path = ToKeyboardPath(key);
        for (int i = 0; i < action.bindings.Count; i++)
        {
            InputBinding binding = action.bindings[i];
            if (!binding.isPartOfComposite) continue;
            if (!string.Equals(binding.name, partName, System.StringComparison.OrdinalIgnoreCase)) continue;
            action.ApplyBindingOverride(i, path);
        }
    }

    private static void ApplyButtonBinding(InputAction action, Key key)
    {
        if (action == null) return;
        string path = ToKeyboardPath(key);
        bool appliedPrimary = false;
        for (int i = 0; i < action.bindings.Count; i++)
        {
            InputBinding binding = action.bindings[i];
            if (binding.isComposite || binding.isPartOfComposite) continue;
            if (!binding.path.StartsWith("<Keyboard>/") && !binding.effectivePath.StartsWith("<Keyboard>/")) continue;

            if (!appliedPrimary)
            {
                action.ApplyBindingOverride(i, path);
                appliedPrimary = true;
            }
            else
            {
                action.ApplyBindingOverride(i, new InputBinding { overridePath = "" });
            }
        }
    }

    private static string ToKeyboardPath(Key key)
    {
        var keyboard = Keyboard.current;
        if (keyboard != null)
        {
            KeyControl control = keyboard[key];
            if (control != null && !string.IsNullOrEmpty(control.path)) return control.path;
        }
        return "<Keyboard>/" + key.ToString().ToLowerInvariant();
    }

    private static void UpdateCache()
    {
        if (_cachedFrame == Time.frameCount) return;
        EnsureInitialized();
        _cachedFrame = Time.frameCount;

        _prevPlayerMove = _currPlayerMove;
        _prevUINavigate = _currUINavigate;
        _prevBattleNavigate = _currBattleNavigate;
        _prevConfigNavigate = _currConfigNavigate;
        _prevConfigAdjust = _currConfigAdjust;

        _currPlayerMove = _playerMove.ReadValue<Vector2>();
        _currUINavigate = _uiNavigate.ReadValue<Vector2>();
        _currBattleNavigate = _battleNavigate.ReadValue<Vector2>();
        _currConfigNavigate = _configNavigate.ReadValue<Vector2>();
        _currConfigAdjust = _configAdjust.ReadValue<Vector2>();

        if (_configModalActive)
        {
            _currPlayerMove = Vector2.zero;
            _currUINavigate = Vector2.zero;
            _currBattleNavigate = Vector2.zero;
        }
    }

    private static bool IsLeft(Vector2 value) => value.x < -AxisThreshold;
    private static bool IsRight(Vector2 value) => value.x > AxisThreshold;
    private static bool IsUp(Vector2 value) => value.y > AxisThreshold;
    private static bool IsDown(Vector2 value) => value.y < -AxisThreshold;

    private static bool KeyboardPressed(Key key)
    {
        var keyboard = Keyboard.current;
        if (keyboard == null) return false;

        KeyControl control = keyboard[key];
        return control != null && control.wasPressedThisFrame;
    }

    private static bool PressedLeft(Vector2 prev, Vector2 curr) => !IsLeft(prev) && IsLeft(curr);
    private static bool PressedRight(Vector2 prev, Vector2 curr) => !IsRight(prev) && IsRight(curr);
    private static bool PressedUp(Vector2 prev, Vector2 curr) => !IsUp(prev) && IsUp(curr);
    private static bool PressedDown(Vector2 prev, Vector2 curr) => !IsDown(prev) && IsDown(curr);
}