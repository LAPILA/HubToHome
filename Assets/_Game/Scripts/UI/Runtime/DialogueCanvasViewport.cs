using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 모달 대사창은 월드 카메라의 위치/회전/줌이 아니라 출력 영역만 따릅니다.
/// 원래 패널 앵커와 640x480 배치를 보존하며 말풍선에는 적용하지 않습니다.
/// </summary>
[DisallowMultipleComponent, RequireComponent(typeof(Canvas))]
public sealed class DialogueCanvasViewport : MonoBehaviour
{
    private static readonly Vector2 ReferenceSize = new Vector2(640f, 480f);
    private Canvas _canvas;
    private CanvasScaler _scaler;
    private RectTransform _content;
    private Camera _output;
    private Rect _lastOutputRect;
    private Vector2Int _lastScreen;

    public RectTransform ContentRect => _content;

    public static void Ensure(Canvas canvas, Camera output)
    {
        if (canvas == null || canvas.renderMode == RenderMode.WorldSpace) return;
        if (!canvas.TryGetComponent(out DialogueCanvasViewport viewport))
            viewport = canvas.gameObject.AddComponent<DialogueCanvasViewport>();
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
        // 전체 전환 페이드(32767) > 모달 대사(32766) > 레터박스(32765).
        // 같은 정렬 레이어에서 비교하여 프리팹의 구형 UI 순서에 영향받지 않습니다.
        _canvas.overrideSorting = true;
        _canvas.sortingLayerID = 0;
        _canvas.sortingOrder = short.MaxValue - 1;
        _scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;

        if (_content == null)
        {
            _content = new GameObject("Dialogue Viewport (640x480)", typeof(RectTransform))
                .GetComponent<RectTransform>();
            _content.gameObject.layer = gameObject.layer;
            _content.SetParent(transform, false);
            _content.anchorMin = _content.anchorMax = _content.pivot = new Vector2(0.5f, 0.5f);
            _content.sizeDelta = ReferenceSize;
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                Transform child = transform.GetChild(i);
                if (child == _content) continue;
                if (child.TryGetComponent(out Canvas childCanvas) && childCanvas.renderMode == RenderMode.WorldSpace)
                    continue;
                // SetParent(false)는 localPosition 기준입니다. 부모 pivot/크기가
                // 바뀌어도 하단 고정 창이 밀리지 않게 UI 좌표를 명시적으로 보존합니다.
                RectTransform rect = child as RectTransform;
                Vector3 position = rect != null ? rect.anchoredPosition3D : Vector3.zero;
                Vector2 size = rect != null ? rect.sizeDelta : Vector2.zero;
                child.SetParent(_content, false);
                if (rect != null)
                {
                    rect.anchoredPosition3D = position;
                    rect.sizeDelta = size;
                }
            }
            for (int i = 1; i < _content.childCount; i++) _content.GetChild(i).SetAsFirstSibling();
        }
        ApplyLayout();
    }

    private void LateUpdate()
    {
        if (_lastOutputRect != OutputRect || _lastScreen.x != Screen.width || _lastScreen.y != Screen.height)
            ApplyLayout();
    }

    private Rect OutputRect => _output != null ? _output.pixelRect : new Rect(0f, 0f, Screen.width, Screen.height);

    private void ApplyLayout()
    {
        if (_content == null || _canvas == null || _scaler == null) return;
        Rect output = OutputRect;
        float scale = Mathf.Max(0.01f, Mathf.Min(output.width / ReferenceSize.x, output.height / ReferenceSize.y));
        _scaler.scaleFactor = scale;
        _canvas.scaleFactor = scale;
        _content.anchoredPosition = (output.center - new Vector2(Screen.width, Screen.height) * 0.5f) / scale;
        _content.localScale = Vector3.one;
        _content.localRotation = Quaternion.identity;
        _lastOutputRect = output;
        _lastScreen = new Vector2Int(Screen.width, Screen.height);
    }
}
