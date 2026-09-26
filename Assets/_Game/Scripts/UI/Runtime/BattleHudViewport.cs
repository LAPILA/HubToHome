using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 전투 HUD만 출력 카메라에서 분리합니다. 월드 팝업/말풍선은 기존 카메라를 사용합니다.
/// 런타임 자식 루트로 640x480 앵커를 보존하며 카메라 회전/줌/충격을 상속하지 않습니다.
/// </summary>
[DisallowMultipleComponent, RequireComponent(typeof(Canvas))]
public sealed class BattleHudViewport : MonoBehaviour
{
    private Canvas _canvas;
    private CanvasScaler _scaler;
    private RectTransform _viewport;
    private Camera _output;
    private Rect _lastRect;
    private Vector2Int _lastScreen;
    private bool _hiddenForModal;
    public RectTransform ContentRect => _viewport;

    public static void Ensure(Canvas canvas, Camera output)
    {
        if (canvas == null || canvas.renderMode == RenderMode.WorldSpace) return;
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
                if (child.TryGetComponent(out Canvas childCanvas) && childCanvas.renderMode == RenderMode.WorldSpace)
                    continue;
                child.SetParent(_viewport, false);
            }
            // 역순으로 옮겼으므로 원래 형제 순서로 돌려 그리기 순서를 보존합니다.
            for (int i = 1; i < _viewport.childCount; i++) _viewport.GetChild(i).SetAsFirstSibling();
        }
        ApplyLayout();
    }

    private void LateUpdate()
    {
        // 설정창은 기존 ScreenSpaceCamera입니다. 모달 중 HUD만 잠시 숨겨 가리지 않게 합니다.
        // 게임 오브젝트를 비활성화하지 않으므로 QTE/행동 코루틴 수명은 그대로입니다.
        if (_canvas != null)
        {
            if (GameInput.IsDefenseInputBlocked && _canvas.enabled)
            { _canvas.enabled = false; _hiddenForModal = true; }
            else if (!GameInput.IsDefenseInputBlocked && _hiddenForModal)
            { _canvas.enabled = true; _hiddenForModal = false; }
        }
        Rect rect = OutputRect;
        if (_lastRect != rect || _lastScreen.x != Screen.width || _lastScreen.y != Screen.height)
            ApplyLayout();
    }

    private Rect OutputRect => _output != null ? _output.pixelRect : new Rect(0, 0, Screen.width, Screen.height);

    public static float ReferenceScale(Rect viewport) => Mathf.Max(0.01f, Mathf.Min(viewport.width / 640f, viewport.height / 480f));

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
        _lastRect = rect;
        _lastScreen = new Vector2Int(Screen.width, Screen.height);
    }
}
