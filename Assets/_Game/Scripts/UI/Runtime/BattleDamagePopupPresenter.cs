using System.Collections.Generic;
using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;

[DisallowMultipleComponent]
public sealed partial class BattleDamagePopupPresenter : MonoBehaviour
{
    private const string PopupRootName = "BattleDamagePopupRoot";
    private const string PopupViewName = "BattleDamagePopup";

    [BoxGroup("Typography"), AssetsOnly, LabelText("TMP Font")]
    [SerializeField] private TMP_FontAsset _fontAsset;
    [BoxGroup("Typography"), MinValue(1f), LabelText("기본 글자 크기")]
    [SerializeField] private float _fontSize = 30f;
    [BoxGroup("Typography"), Range(0f, 0.25f), LabelText("검정 외곽선")]
    [SerializeField] private float _outlineWidth = 0.08f;

    [BoxGroup("Pool"), MinValue(1), LabelText("미리 생성 수")]
    [SerializeField] private int _prewarmCount = 8;

    [BoxGroup("Motion"), LabelText("시작 이동 시간")]
    [SerializeField] private float _launchDuration = 0.16f;
    [BoxGroup("Motion"), LabelText("착지 시간")]
    [SerializeField] private float _settleDuration = 0.12f;
    [BoxGroup("Motion"), MinValue(0.30f), LabelText("유지 시간")]
    [SerializeField] private float _holdDuration = 0.30f;
    [BoxGroup("Motion"), LabelText("소멸 시간")]
    [SerializeField] private float _fadeDuration = 0.24f;
    [BoxGroup("Motion"), LabelText("기준 위치 보정")]
    [SerializeField] private Vector2 _originOffset = new Vector2(0f, 12f);
    [BoxGroup("Motion"), MinValue(0f), LabelText("캐릭터 바깥쪽 간격")]
    [SerializeField] private float _characterSideOffset = 48f;
    [BoxGroup("Motion"), LabelText("튀어오름 위치")]
    [SerializeField] private Vector2 _launchOffset = new Vector2(8f, 14f);
    [BoxGroup("Motion"), LabelText("착지 위치")]
    [SerializeField] private Vector2 _settleOffset = new Vector2(12f, 10f);
    [BoxGroup("Motion"), LabelText("소멸 위치")]
    [SerializeField] private Vector2 _fadeOffset = new Vector2(16f, 24f);

    [BoxGroup("Critical"), LabelText("확대 시간")]
    [SerializeField] private float _criticalGrowDuration = 0.09f;
    [BoxGroup("Critical"), LabelText("시작 크기")]
    [SerializeField] private float _criticalStartScale = 0.45f;
    [BoxGroup("Critical"), LabelText("최종 크기")]
    [SerializeField] private float _criticalEndScale = 1.65f;

    private readonly Stack<BattleDamagePopupView> _available = new Stack<BattleDamagePopupView>();
    private readonly HashSet<BattleDamagePopupView> _active = new HashSet<BattleDamagePopupView>();
    private readonly List<BattleDamagePopupView> _releaseBuffer = new List<BattleDamagePopupView>();
    private readonly List<BattleDamagePopupView> _displayOrder = new List<BattleDamagePopupView>(16);
    private readonly Dictionary<BattleDamagePopupView, Rect> _footprints = new Dictionary<BattleDamagePopupView, Rect>(16);
    private readonly List<Rect> _occupied = new List<Rect>(16);

    private RectTransform _popupRoot;
    private Camera _worldCamera;
    private bool _initialized;
    private bool _isDisposing;

    public int ActiveCount => _active.Count;
    public int AvailableCount => _available.Count;
    public RectTransform PopupRoot => _popupRoot;

    public void Initialize(RectTransform host, Camera worldCamera, TMP_FontAsset fallbackFont)
    {
        if (host == null)
            throw new System.ArgumentNullException(nameof(host));

        _worldCamera = worldCamera;
        if (_fontAsset == null)
            _fontAsset = fallbackFont != null ? fallbackFont : TMP_Settings.defaultFontAsset;

        EnsurePopupRoot(host);
        if (!_initialized)
        {
            _initialized = true;
            int count = Mathf.Max(1, _prewarmCount);
            for (int i = 0; i < count; i++)
                _available.Push(CreateView());
        }
    }

    public void SetFontSize(float fontSize)
    {
        _fontSize = Mathf.Max(1f, fontSize);
    }

    public void SetOriginOffset(Vector2 originOffset)
    {
        _originOffset = originOffset;
    }

    public void BindWorldCamera(Camera worldCamera)
    {
        if (worldCamera != null)
            _worldCamera = worldCamera;
    }

    public bool TryShow(BattleDamageFeedback feedback, out BattleDamagePopupView view)
    {
        view = null;
        if (!_initialized || _popupRoot == null || feedback.Target == null)
            return false;

        Camera camera = ResolveWorldCamera();
        if (camera == null)
            return false;

        Vector3 worldPosition = feedback.Target.GetPivot(CharacterPivotId.Center).position;
        if (!TryWorldToPopupPosition(worldPosition, camera, out Vector2 localPosition))
            return false;

        // 아군/적 모두 화면 기준 오른쪽. 카메라 회전/캐릭터 방향과 무관합니다.
        float direction = 1f;
        localPosition.x += direction * Mathf.Max(0f, _characterSideOffset);
        if (feedback.Kind == BattleDamageFeedbackKind.Damage)
            return TryShowTotal(feedback.Target, feedback.Amount,
                BattleDamageTypeUtility.FromElement(feedback.Element), feedback.BatchId,
                feedback.IsCritical, localPosition, out view);
        return TryShowDirected(
            "MISS",
            feedback.ResolveColor(),
            feedback.IsCritical,
            localPosition,
            direction,
            out view);
    }

    public bool TryShowElemental(CharacterBase target, int amount, ElementalStatusKind kind,
        out BattleDamagePopupView view)
    {
        view = null;
        if (!_initialized || _popupRoot == null || target == null || amount <= 0) return false;
        Camera camera = ResolveWorldCamera();
        if (camera == null || !TryWorldToPopupPosition(
            target.GetPivot(CharacterPivotId.Center).position, camera, out Vector2 localPosition)) return false;
        float direction = 1f;
        localPosition.x += direction * Mathf.Max(0f, _characterSideOffset);
        return TryShowTotal(target, amount, BattleDamageTypeUtility.FromStatus(kind),
            _batches != null ? _batches.CurrentId : 0, false, localPosition, out view);
    }

    public bool TryShowAtLocalPosition(
        string content,
        Color color,
        bool isCritical,
        Vector2 localPosition,
        out BattleDamagePopupView view)
    {
        float direction = 1f;
        return TryShowDirected(content, color, isCritical, localPosition, direction, out view);
    }

    private bool TryShowDirected(
        string content,
        Color color,
        bool isCritical,
        Vector2 localPosition,
        float direction,
        out BattleDamagePopupView view)
    {
        view = null;
        if (!_initialized || _popupRoot == null)
            return false;

        view = Acquire();
        view.SetFontSize(_fontSize);
        Vector2 textSize = view.Label.GetPreferredValues(content);
        float scale = isCritical ? Mathf.Max(1f, Mathf.Max(_criticalStartScale, _criticalEndScale)) : 1f;
        textSize = new Vector2(Mathf.Max(_fontSize, textSize.x), Mathf.Max(_fontSize, textSize.y)) * scale;
        float minX = Mathf.Min(0f, Mathf.Min(_launchOffset.x, Mathf.Min(_settleOffset.x, _fadeOffset.x)));
        float maxX = Mathf.Max(0f, Mathf.Max(_launchOffset.x, Mathf.Max(_settleOffset.x, _fadeOffset.x)));
        float minY = Mathf.Min(0f, Mathf.Min(_launchOffset.y, Mathf.Min(_settleOffset.y, _fadeOffset.y)));
        float maxY = Mathf.Max(0f, Mathf.Max(_launchOffset.y, Mathf.Max(_settleOffset.y, _fadeOffset.y)));
        // 현재 프레임뿐 아니라 이동/치명타 확대가 끝날 때까지의 영역을 예약합니다.
        Rect footprint = Rect.MinMaxRect(-textSize.x * 0.5f + minX - 3f, -textSize.y * 0.5f + minY - 3f,
            textSize.x * 0.5f + maxX + 3f, textSize.y * 0.5f + maxY + 3f);
        Vector2 preferred = localPosition + _originOffset + Vector2.right * textSize.x * 0.5f;
        Rect area = _popupRoot.rect;
        if (area.width < 1f || area.height < 1f) area = new Rect(-320f, -240f, 640f, 480f);
        area = Rect.MinMaxRect(area.xMin + 8f, area.yMin + area.height * 0.34f,
            area.xMax - 8f, area.yMax - area.height * 0.12f);
        Vector2 position;
        while (true)
        {
            _occupied.Clear();
            foreach (Rect occupied in _footprints.Values) _occupied.Add(occupied);
            if (BattleOverlayLayout.TryPlacePopup(preferred, footprint, area, _occupied, out position)) break;
            // 공간이 찬 연타는 오래된 표시부터 반환합니다. 실제 피해/상태 계산은 건드리지 않습니다.
            BattleDamagePopupView oldestTransient = null;
            for (int i = 0; i < _displayOrder.Count; i++)
                if (!_viewGroups.ContainsKey(_displayOrder[i])) { oldestTransient = _displayOrder[i]; break; }
            if (oldestTransient == null)
            {
                Release(view);
                view = null;
                return false;
            }
            Release(oldestTransient);
        }
        _footprints[view] = new Rect(position + footprint.position, footprint.size);
        _displayOrder.Add(view);
        view.Play(
            content,
            color,
            isCritical,
            new Vector2(
                Mathf.Round(position.x),
                Mathf.Round(position.y)),
            direction,
            BuildAnimationSettings(),
            Release);
        return true;
    }

    public void ReleaseAll()
    {
        _releaseBuffer.Clear();
        foreach (BattleDamagePopupView view in _active)
            _releaseBuffer.Add(view);

        for (int i = 0; i < _releaseBuffer.Count; i++)
            Release(_releaseBuffer[i]);

        _releaseBuffer.Clear();
    }

    private void EnsurePopupRoot(RectTransform host)
    {
        Transform existing = host.Find(PopupRootName);
        if (existing != null)
        {
            _popupRoot = existing as RectTransform;
            if (_popupRoot != null)
                _popupRoot.gameObject.layer = host.gameObject.layer;
        }
        else
        {
            var rootObject = new GameObject(PopupRootName, typeof(RectTransform));
            _popupRoot = rootObject.GetComponent<RectTransform>();
            _popupRoot.SetParent(host, false);
            _popupRoot.gameObject.layer = host.gameObject.layer;
            _popupRoot.anchorMin = Vector2.zero;
            _popupRoot.anchorMax = Vector2.one;
            _popupRoot.offsetMin = Vector2.zero;
            _popupRoot.offsetMax = Vector2.zero;
            _popupRoot.pivot = new Vector2(0.5f, 0.5f);
        }

        _popupRoot.SetAsLastSibling();
    }

    private BattleDamagePopupView Acquire()
    {
        BattleDamagePopupView view = null;
        while (_available.Count > 0 && view == null)
        {
            view = _available.Pop();
            if (view != null && !view.IsReady)
            {
                view.StopAndReset();
                Destroy(view.gameObject);
                view = null;
            }
        }

        if (view == null)
            view = CreateView();

        view.transform.SetParent(_popupRoot, false);
        view.gameObject.SetActive(true);
        _active.Add(view);
        return view;
    }

    private void Release(BattleDamagePopupView view)
    {
        if (!_active.Remove(view))
            return;

        _footprints.Remove(view);
        _displayOrder.Remove(view);
        RemoveTotalView(view);
        if (view == null) return;
        view.StopAndReset();
        if (_isDisposing || _popupRoot == null || !view.IsReady)
        {
            Destroy(view.gameObject);
            return;
        }

        view.transform.SetParent(_popupRoot, false);
        _available.Push(view);
    }

    private BattleDamagePopupView CreateView()
    {
        var viewObject = new GameObject(
            PopupViewName,
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(CanvasGroup),
            typeof(TextMeshProUGUI),
            typeof(BattleDamagePopupView));
        RectTransform rect = viewObject.GetComponent<RectTransform>();
        rect.SetParent(_popupRoot, false);
        viewObject.layer = _popupRoot.gameObject.layer;
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(160f, 64f);

        TextMeshProUGUI label = viewObject.GetComponent<TextMeshProUGUI>();
        CanvasGroup canvasGroup = viewObject.GetComponent<CanvasGroup>();
        BattleDamagePopupView view = viewObject.GetComponent<BattleDamagePopupView>();
        view.Initialize(rect, label, canvasGroup, _fontAsset, _fontSize, _outlineWidth);
        viewObject.SetActive(false);
        return view;
    }

    private bool TryWorldToPopupPosition(Vector3 worldPosition, Camera worldCamera, out Vector2 localPosition)
    {
        localPosition = default;
        if (_popupRoot == null || worldCamera == null)
            return false;

        Vector3 screenPoint = worldCamera.WorldToScreenPoint(worldPosition);
        if (screenPoint.z < 0f)
            return false;

        Canvas canvas = _popupRoot.GetComponentInParent<Canvas>();
        if (canvas != null) canvas = canvas.rootCanvas;
        Camera uiCamera = null;
        if (canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay)
            uiCamera = canvas.worldCamera != null ? canvas.worldCamera : worldCamera;

        return RectTransformUtility.ScreenPointToLocalPointInRectangle(
            _popupRoot,
            screenPoint,
            uiCamera,
            out localPosition);
    }

    private Camera ResolveWorldCamera()
    {
        if (_worldCamera != null)
            return _worldCamera;

        _worldCamera = Camera.main;
        return _worldCamera;
    }

    private BattleDamagePopupAnimationSettings BuildAnimationSettings()
    {
        return new BattleDamagePopupAnimationSettings
        {
            LaunchDuration = _launchDuration,
            SettleDuration = _settleDuration,
            HoldDuration = _holdDuration,
            FadeDuration = _fadeDuration,
            LaunchOffset = _launchOffset,
            SettleOffset = _settleOffset,
            FadeOffset = _fadeOffset,
            CriticalGrowDuration = _criticalGrowDuration,
            CriticalStartScale = _criticalStartScale,
            CriticalEndScale = _criticalEndScale
        };
    }

    private void OnDisable()
    {
        if (_initialized)
            ReleaseAll();
    }

    private void OnDestroy()
    {
        if (_batches != null) _batches.Ended -= FinishBatch;
        _batches = null;
        _isDisposing = true;
        ReleaseAll();
        while (_available.Count > 0)
        {
            BattleDamagePopupView view = _available.Pop();
            if (view != null)
                Destroy(view.gameObject);
        }
    }
}
