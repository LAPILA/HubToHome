using UnityEngine;
using DG.Tweening;
using UnityEngine.EventSystems;

/// <summary>
/// 모든 UI 패널의 베이스 클래스.
/// DOTween을 사용한 등장/퇴장 애니메이션을 제공합니다.
/// </summary>
[RequireComponent(typeof(CanvasGroup))]
public class UIPanel : MonoBehaviour
{
    [Header("Animation")]
    [SerializeField] protected float _showDuration = 0.2f;
    [SerializeField] protected float _hideDuration = 0.15f;
    [SerializeField] protected Ease  _showEase     = Ease.OutQuad;
    [SerializeField] protected Ease  _hideEase     = Ease.InQuad;
    [SerializeField] private GameObject _defaultSelection;

    protected CanvasGroup _canvasGroup;
    private Tweener _currentTween;

    protected virtual void Awake()
    {
        _canvasGroup = GetComponent<CanvasGroup>();
        _canvasGroup.alpha          = 0f;
        _canvasGroup.interactable   = false;
        _canvasGroup.blocksRaycasts = false;
        gameObject.SetActive(false);
    }

    protected virtual void OnDisable()
    {
        KillCurrentTween();
    }

    protected virtual void OnDestroy()
    {
        KillCurrentTween();
    }

    // ── 표시 ──────────────────────────────────────────────────
    public virtual void Show()
    {
        if (_canvasGroup == null) _canvasGroup = GetComponent<CanvasGroup>();
        
        KillCurrentTween();
        
        gameObject.SetActive(true);
        _canvasGroup.interactable = true;
        _canvasGroup.blocksRaycasts = true;
        
        _currentTween = _canvasGroup.DOFade(1f, _showDuration)
            .SetEase(_showEase)
            .SetUpdate(true) // 타임스케일이 0일 때도 UI 애니메이션은 작동하도록 보장
            .OnComplete(() =>
            {
                _currentTween = null;
                OnShowComplete();
            });
    }

    // ── 숨김 ──────────────────────────────────────────────────
    public virtual void Hide()
    {
        KillCurrentTween();
        _canvasGroup.interactable   = false;
        _canvasGroup.blocksRaycasts = false;
        
        _currentTween = _canvasGroup.DOFade(0f, _hideDuration)
            .SetEase(_hideEase)
            .SetUpdate(true)
            .OnComplete(() =>
            {
                _currentTween = null;
                gameObject.SetActive(false);
                OnHideComplete();
            });
    }

    public virtual bool TryHandleCancelInput()
    {
        return false;
    }

    // ── 즉시 표시/숨김 ────────────────────────────────────────
    public virtual void HideImmediate()
    {
        KillCurrentTween();
        if (_canvasGroup == null) _canvasGroup = GetComponent<CanvasGroup>();

        _canvasGroup.alpha = 0f;
        _canvasGroup.interactable = false;
        _canvasGroup.blocksRaycasts = false;
        gameObject.SetActive(false);
    }

    public virtual void ShowImmediate()
    {
        KillCurrentTween();
        if (_canvasGroup == null) _canvasGroup = GetComponent<CanvasGroup>();

        gameObject.SetActive(true);
        _canvasGroup.alpha = 1f;
        _canvasGroup.interactable = true;
        _canvasGroup.blocksRaycasts = true;
    }

    public virtual void FocusDefaultSelection()
    {
        if (_defaultSelection == null
            || !_defaultSelection.activeInHierarchy
            || EventSystem.current == null)
            return;

        EventSystem.current.SetSelectedGameObject(_defaultSelection);
    }

    protected virtual void OnShowComplete() { }
    protected virtual void OnHideComplete() { }

    public bool IsVisible => gameObject.activeSelf && _canvasGroup.alpha > 0f;

    private void KillCurrentTween()
    {
        if (_currentTween == null)
            return;

        _currentTween.Kill(false);
        if (_canvasGroup != null)
            _canvasGroup.DOKill(false);
        _currentTween = null;
    }
}
