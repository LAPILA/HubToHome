using UnityEngine;
using UnityEngine.EventSystems;
using TMPro;
using DG.Tweening;

[RequireComponent(typeof(UnityEngine.UI.Button))]
public class MenuButtonAnimator : MonoBehaviour, ISelectHandler, IDeselectHandler
{
    [Header("연출 대상")]
    [SerializeField] private RectTransform _rectTarget;
    [SerializeField] private TextMeshProUGUI _textTarget;

    [Header("연출 세팅")]
    [SerializeField] private Color _selectedColor = Color.yellow;
    [SerializeField] private Color _normalColor = Color.white;
    [SerializeField] private float _scaleSize = 1.4f;
    [SerializeField] private Color _disabledColor = new Color(0.42f, 0.48f, 0.50f, 1f);

    private UnityEngine.UI.Button _button;
    private Vector3 _baseScale;
    private Tween _scaleTween;
    private Tween _colorTween;
    private bool _selected;
    private bool _ready;

    private void Awake()
    {
        EnsureReferences();
    }

    private void OnEnable() => RefreshVisual(true);

    private void EnsureReferences()
    {
        if (_ready) return;
        if (_rectTarget == null) _rectTarget = GetComponent<RectTransform>();
        if (_textTarget == null) _textTarget = GetComponentInChildren<TextMeshProUGUI>();
        _button = GetComponent<UnityEngine.UI.Button>();
        _baseScale = _rectTarget != null ? _rectTarget.localScale : Vector3.one;
        _ready = true;
    }

    // 유니티 EventSystem이 이 버튼을 선택(키보드 방향키 도달)했을 때 자동 실행
    public void OnSelect(BaseEventData eventData)
    {
        _selected = true;
        RefreshVisual();
    }

    // 유니티 EventSystem이 이 버튼에서 떠났을 때(다른 버튼으로 이동) 자동 실행
    public void OnDeselect(BaseEventData eventData)
    {
        _selected = false;
        RefreshVisual();
    }

    public void RefreshVisual(bool immediate = false)
    {
        EnsureReferences();
        KillOwnedTweens();
        bool enabled = _button != null && _button.interactable;
        bool selected = enabled && _selected;
        Vector3 scale = _baseScale * (selected ? _scaleSize : 1f);
        Color color = !enabled ? _disabledColor : selected ? _selectedColor : _normalColor;
        if (immediate || !isActiveAndEnabled)
        {
            if (_rectTarget != null) _rectTarget.localScale = scale;
            if (_textTarget != null) _textTarget.color = color;
            return;
        }
        if (_rectTarget != null)
            _scaleTween = _rectTarget.DOScale(scale, 0.12f).SetEase(Ease.OutQuad).SetUpdate(true);
        if (_textTarget != null)
            _colorTween = _textTarget.DOColor(color, 0.12f).SetUpdate(true);
    }

    private void OnDisable()
    {
        _selected = false;
        RefreshVisual(true);
    }

    private void OnDestroy() => KillOwnedTweens();

    private void KillOwnedTweens()
    {
        // 같은 텍스트를 사용하는 타이틀 확인 연출 등 다른 소유자의 tween은 취소하지 않습니다.
        _scaleTween?.Kill(false);
        _colorTween?.Kill(false);
        _scaleTween = _colorTween = null;
    }
}
