using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UI;

/// <summary>
/// 모바일 터치만 전용 버튼 영역에서 읽어 공용 입력 액션에 전달합니다.
/// EventSystem 포인터/클릭을 활성화하지 않으므로 PC 마우스 금지를 보존합니다.
/// </summary>
[DefaultExecutionOrder(-200)]
public sealed class MobileGameInputOverlay : MonoBehaviour
{
    private struct TouchButton
    {
        public RectTransform Rect;
        public GamepadButton Button;
        public Image Fill;
        public bool Held;
    }

    private static MobileGameInputOverlay _instance;
    private readonly TouchButton[] _buttons = new TouchButton[12];
    private Gamepad _device;
    private Canvas _canvas;
    private uint _lastMask;
    private bool _hasFocus = true;
    private TMP_FontAsset _font;
    private static readonly Color IdleColor = new Color(0.06f, 0.06f, 0.09f, 0.58f);
    private static readonly Color HeldColor = new Color(0.65f, 0.51f, 0.16f, 0.8f);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void InstallOnMobile()
    {
        if (!Application.isMobilePlatform || _instance != null) return;
        new GameObject("Mobile Game Controls", typeof(MobileGameInputOverlay));
    }

    private void Awake()
    {
        if (_instance != null && _instance != this) { Destroy(gameObject); return; }
        _instance = this;
        DontDestroyOnLoad(gameObject);
        GameInput.InitializeRuntimeInput();
        _device = InputSystem.AddDevice<Gamepad>(GameInput.TouchDeviceName);
        BuildView();
    }

    private void BuildView()
    {
        // FixedViewport: 공용 640x480 영역 안의 터치 컨트롤. 카메라 검색/배율 계산을 중복하지 않습니다.
        _canvas = gameObject.AddComponent<Canvas>();
        _canvas.overrideSorting = true;
        _canvas.sortingOrder = 32760;
        gameObject.AddComponent<CanvasScaler>();
        _font = GameContentCatalog.Instance != null ? GameContentCatalog.Instance.DefaultUiFont : TMP_Settings.defaultFontAsset;
        CreateButton(0, "↑", new Vector2(58, 100), GamepadButton.DpadUp);
        CreateButton(1, "↓", new Vector2(58, 20), GamepadButton.DpadDown);
        CreateButton(2, "←", new Vector2(18, 60), GamepadButton.DpadLeft);
        CreateButton(3, "→", new Vector2(98, 60), GamepadButton.DpadRight);
        CreateButton(4, "Z", new Vector2(-112, 24), GamepadButton.South, true);
        CreateButton(5, "X", new Vector2(-66, 64), GamepadButton.East, true);
        CreateButton(6, "C", new Vector2(-20, 104), GamepadButton.North, true);
        CreateButton(7, "F", new Vector2(-158, 64), GamepadButton.West, true);
        CreateButton(8, "L", new Vector2(18, 146), GamepadButton.LeftShoulder);
        CreateButton(9, "R", new Vector2(-20, 150), GamepadButton.RightShoulder, true);
        CreateButton(10, "↺", new Vector2(-66, 150), GamepadButton.Select, true);
        CreateButton(11, "Ⅱ", new Vector2(-20, 426), GamepadButton.Start, true);
        UIRuntimeGuard.NormalizeCanvas(gameObject);
    }

    private void CreateButton(int index, string label, Vector2 position, GamepadButton button, bool right = false)
    {
        var go = new GameObject("Touch_" + button, typeof(RectTransform), typeof(Image));
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.SetParent(transform, false);
        rect.anchorMin = rect.anchorMax = new Vector2(right ? 1f : 0f, 0f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        // 화면 끝에 여백을 남겨 손가락 중심을 화면 안에 둘 수 있게 합니다.
        rect.anchoredPosition = position + new Vector2(right ? -10 : 10, 10);
        rect.sizeDelta = new Vector2(38, 38);
        Image fill = go.GetComponent<Image>();
        fill.color = IdleColor;
        fill.raycastTarget = false;
        var textGo = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        RectTransform textRect = textGo.GetComponent<RectTransform>();
        textRect.SetParent(rect, false);
        textRect.anchorMin = Vector2.zero; textRect.anchorMax = Vector2.one;
        textRect.offsetMin = textRect.offsetMax = Vector2.zero;
        var text = textGo.GetComponent<TextMeshProUGUI>();
        text.font = _font; text.fontSize = 18f; text.text = label;
        text.alignment = TextAlignmentOptions.Center; text.raycastTarget = false;
        _buttons[index] = new TouchButton { Rect = rect, Button = button, Fill = fill };
    }

    private void Update()
    {
        bool menuOwnsTouch = FieldMenuTouchInput.IsActive;
        if (_canvas != null) _canvas.enabled = !menuOwnsTouch;
        if (menuOwnsTouch) { ReleaseButtons(); return; }
        Touchscreen screen = Touchscreen.current;
        if (!_hasFocus || screen == null || _device == null) { ReleaseButtons(); return; }
        uint mask = 0;
        for (int i = 0; i < _buttons.Length; i++)
        {
            bool held = false;
            for (int t = 0; t < screen.touches.Count; t++)
            {
                var touch = screen.touches[t];
                if (!touch.press.isPressed) continue;
                if (RectTransformUtility.RectangleContainsScreenPoint(_buttons[i].Rect,
                    touch.position.ReadValue(), _canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : _canvas.worldCamera))
                { held = true; break; }
            }
            if (held) mask |= 1u << (int)_buttons[i].Button;
            if (_buttons[i].Held == held) continue;
            _buttons[i].Held = held;
            _buttons[i].Fill.color = held ? HeldColor : IdleColor;
        }
        if (mask == _lastMask) return;
        _lastMask = mask;
        InputSystem.QueueStateEvent(_device, new GamepadState { buttons = mask });
    }

    private void ReleaseButtons()
    {
        if (_lastMask == 0) return;
        _lastMask = 0;
        if (_device != null && _device.added) InputSystem.QueueStateEvent(_device, new GamepadState());
        for (int i = 0; i < _buttons.Length; i++)
        {
            _buttons[i].Held = false;
            if (_buttons[i].Fill != null) _buttons[i].Fill.color = IdleColor;
        }
    }

    private void OnApplicationFocus(bool focus) { _hasFocus = focus; if (!focus) ReleaseButtons(); }
    private void OnApplicationPause(bool paused) { if (paused) ReleaseButtons(); }
    private void OnDisable() => ReleaseButtons();
    private void OnDestroy()
    {
        ReleaseButtons();
        if (_device != null && _device.added) InputSystem.RemoveDevice(_device);
        if (_instance == this) _instance = null;
    }
}
