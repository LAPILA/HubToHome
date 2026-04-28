using UnityEngine;
using DG.Tweening;

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

    // ── 표시 ──────────────────────────────────────────────────
    public virtual void Show()
    {
        gameObject.SetActive(true);
        _currentTween?.Kill();
        _canvasGroup.interactable   = true;
        _canvasGroup.blocksRaycasts = true;
        _currentTween = _canvasGroup
            .DOFade(1f, _showDuration)
            .SetEase(_showEase)
            .OnComplete(OnShowComplete);
    }

    // ── 숨김 ──────────────────────────────────────────────────
    public virtual void Hide()
    {
        _currentTween?.Kill();
        _canvasGroup.interactable   = false;
        _canvasGroup.blocksRaycasts = false;
        _currentTween = _canvasGroup
            .DOFade(0f, _hideDuration)
            .SetEase(_hideEase)
            .OnComplete(() =>
            {
                gameObject.SetActive(false);
                OnHideComplete();
            });
    }

    // ── 즉시 표시/숨김 ────────────────────────────────────────
    public virtual void ShowImmediate()
    {
        _currentTween?.Kill();
        gameObject.SetActive(true);
        _canvasGroup.alpha          = 1f;
        _canvasGroup.interactable   = true;
        _canvasGroup.blocksRaycasts = true;
        OnShowComplete();
    }

    public virtual void HideImmediate()
    {
        _currentTween?.Kill();
        _canvasGroup.alpha          = 0f;
        _canvasGroup.interactable   = false;
        _canvasGroup.blocksRaycasts = false;
        gameObject.SetActive(false);
        OnHideComplete();
    }

    // ── 가상 콜백 ─────────────────────────────────────────────
    protected virtual void OnShowComplete() { }
    protected virtual void OnHideComplete() { }

    public bool IsVisible => gameObject.activeSelf && _canvasGroup.alpha > 0f;
}
