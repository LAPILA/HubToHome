using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 전투 HUD만 출력 카메라에서 분리합니다. 월드 팝업/말풍선은 기존 카메라를 사용합니다.
/// 런타임 자식 루트로 640x480 앵커를 보존하며 카메라 회전/줌/충격을 상속하지 않습니다.
/// </summary>
[DisallowMultipleComponent, RequireComponent(typeof(Canvas))]
public sealed class BattleHudViewport : MonoBehaviour
{
    private const float DialogueBottomBand = 20f;
    private static BattleHudViewport _activeViewport;
    private Canvas _canvas;
    private CanvasScaler _scaler;
    private RectTransform _viewport;
    private Image _bottomMatte;
    private BattleUIController _owner;
    private Camera _output;
    private Rect _lastRect;
    private Vector2Int _lastScreen;
    private bool _hiddenForModal;
    public RectTransform ContentRect => _viewport;

    public static void Ensure(Canvas canvas, Camera output)
    {
        if (canvas == null) return;
        // 프리팹의 중첩 Canvas에는 오래된 WorldSpace 직렬화 값이 남을 수 있습니다.
        // 실제 출력 소유자는 최상위 Canvas이며 독립 월드 Canvas만 제외합니다.
        canvas = canvas.rootCanvas;
        if (canvas.renderMode == RenderMode.WorldSpace) return;
        var viewport = canvas.GetComponent<BattleHudViewport>();
        if (viewport == null) viewport = canvas.gameObject.AddComponent<BattleHudViewport>();
        viewport.Configure(output);
    }

    public void Configure(Camera output)
    {
        _output = output;
        if (_canvas == null) _canvas = GetComponent<Canvas>();
        if (_scaler == null) _scaler = GetComponent<CanvasScaler>();
        if (_scaler == null) _scaler = gameObject.AddComponent<CanvasScaler>();
        if (_owner == null) _owner = GetComponentInChildren<BattleUIController>(true);
        if (_owner != null && isActiveAndEnabled) _activeViewport = this;
        _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _canvas.worldCamera = null;
        // 결과(500), 로딩 페이드(999), 저장 UI(0)보다 앞에 덮어 그리지 않습니다.
        _canvas.sortingLayerID = 0;
        _canvas.sortingOrder = -100;
        _scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
        if (_viewport == null)
        {
            _viewport = new GameObject("Battle HUD Viewport (640x480)", typeof(RectTransform))
                .GetComponent<RectTransform>();
            _viewport.gameObject.layer = gameObject.layer;
            _viewport.SetParent(transform, false);
            // 좌표를 다시 계산하지 않고 기존 앵커와 참조를 보존합니다.
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                Transform child = transform.GetChild(i);
                if (child == _viewport) continue;
                if (child.TryGetComponent(out Canvas childCanvas) && childCanvas.isRootCanvas
                    && childCanvas.renderMode == RenderMode.WorldSpace)
                    continue;
                RectTransform rect = child as RectTransform;
                Vector3 position = rect != null ? rect.anchoredPosition3D : Vector3.zero;
                Vector2 size = rect != null ? rect.sizeDelta : Vector2.zero;
                child.SetParent(_viewport, false);
                if (rect != null)
                {
                    rect.anchoredPosition3D = position;
                    rect.sizeDelta = size;
                }
            }
            // 역순으로 옮겼으므로 원래 형제 순서로 돌려 그리기 순서를 보존합니다.
            for (int i = 1; i < _viewport.childCount; i++) _viewport.GetChild(i).SetAsFirstSibling();
        }
        if (_bottomMatte == null)
        {
            var matte = new GameObject("Battle HUD Bottom Matte", typeof(RectTransform), typeof(Image));
            matte.layer = gameObject.layer;
            matte.transform.SetParent(transform, false);
            matte.transform.SetAsFirstSibling();
            _bottomMatte = matte.GetComponent<Image>();
            _bottomMatte.color = Color.black;
            _bottomMatte.raycastTarget = false;
        }
        ApplyLayout();
    }

    private void OnEnable()
    {
        if (_owner != null) _activeViewport = this;
    }

    private void OnDisable()
    {
        if (_activeViewport == this) _activeViewport = null;
    }

    private void LateUpdate()
    {
        // 설정창은 기존 ScreenSpaceCamera입니다. 모달 중 HUD만 잠시 숨겨 가리지 않게 합니다.
        // 게임 오브젝트를 비활성화하지 않으므로 QTE/행동 코루틴 수명은 그대로입니다.
        if (_canvas != null)
        {
            bool dockedDialogue = BattleUIController.Instance != null && BattleUIController.Instance.IsMainDialogueDocked;
            bool otherModal = GameInput.IsConfigModalActive
                || (GameStateManager.Instance != null && GameStateManager.Instance.CurrentState == GameState.Paused)
                || (BattleManager.Instance != null && BattleManager.Instance.IsAwaitingReserveChoice);
            bool modalVisible = ShouldHideForModal(GameInput.IsDefenseInputBlocked, dockedDialogue, otherModal);
            if (modalVisible && _canvas.enabled)
            { _canvas.enabled = false; _hiddenForModal = true; }
            else if (!modalVisible && _hiddenForModal)
            { _canvas.enabled = true; _hiddenForModal = false; }
        }
        Rect rect = OutputRect;
        if (_lastRect != rect || _lastScreen.x != Screen.width || _lastScreen.y != Screen.height)
            ApplyLayout();
        RefreshMatteVisibility();
    }

    public static bool ShouldHideForModal(bool inputBlocked, bool dockedDialogue, bool otherModal)
        => otherModal || (inputBlocked && !dockedDialogue);

    private Rect OutputRect => _output != null ? _output.pixelRect : new Rect(0, 0, Screen.width, Screen.height);

    public static float ReferenceScale(Rect viewport) => Mathf.Max(0.01f, Mathf.Min(viewport.width / 640f, viewport.height / 480f));

    public static Rect CalculateContentScreenRect(Rect output)
    {
        Vector2 size = new Vector2(640f, 480f) * ReferenceScale(output);
        return new Rect(output.center - size * 0.5f, size);
    }

    public static Rect CalculateBottomMatteRect(Rect output)
    {
        float height = Mathf.Max(0f, CalculateContentScreenRect(output).yMin - output.yMin);
        return new Rect(output.xMin, output.yMin, output.width, height);
    }

    public static float CalculateDockedDialogueBottomLimit(Rect output)
        => CalculateContentScreenRect(output).yMin + DialogueBottomBand * ReferenceScale(output);

    public static bool TryGetDockedDialogueBottomLimit(out float screenHeight)
    {
        screenHeight = 0f;
        BattleHudViewport viewport = _activeViewport;
        if (viewport == null || !viewport.isActiveAndEnabled || !viewport.IsHudChromeVisible
            || !viewport._owner.IsMainDialogueDocked) return false;
        screenHeight = CalculateDockedDialogueBottomLimit(viewport.OutputRect);
        return true;
    }

    private bool IsHudChromeVisible => _canvas != null && _canvas.enabled
        && _owner != null && _owner.IsHudChromeVisible;

    private void RefreshMatteVisibility()
    {
        if (_bottomMatte == null) return;
        bool visible = IsHudChromeVisible && _bottomMatte.rectTransform.sizeDelta.y > 0f;
        if (_bottomMatte.enabled != visible) _bottomMatte.enabled = visible;
    }

    private void ApplyLayout()
    {
        if (_viewport == null || _canvas == null || _scaler == null) return;
        Rect rect = OutputRect;
        float scale = ReferenceScale(rect);
        _scaler.scaleFactor = scale;
        _canvas.scaleFactor = scale;
        _viewport.anchorMin = _viewport.anchorMax = _viewport.pivot = new Vector2(0.5f, 0.5f);
        _viewport.sizeDelta = new Vector2(640f, 480f);
        _viewport.anchoredPosition = (rect.center - new Vector2(Screen.width, Screen.height) * 0.5f) / scale;
        _viewport.localScale = Vector3.one;
        _viewport.localRotation = Quaternion.identity;
        if (_bottomMatte != null)
        {
            Rect matte = CalculateBottomMatteRect(rect);
            RectTransform matteRect = _bottomMatte.rectTransform;
            matteRect.anchorMin = matteRect.anchorMax = matteRect.pivot = Vector2.zero;
            matteRect.anchoredPosition = matte.position / scale;
            matteRect.sizeDelta = matte.size / scale;
            RefreshMatteVisibility();
        }
        _lastRect = rect;
        _lastScreen = new Vector2Int(Screen.width, Screen.height);
    }
}
