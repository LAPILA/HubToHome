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

    private void Awake()
    {
        // 에디터에서 드래그 안 했을 경우 스스로 찾아오기 (편의성)
        if (_rectTarget == null) _rectTarget = GetComponent<RectTransform>();
        if (_textTarget == null) _textTarget = GetComponentInChildren<TextMeshProUGUI>();
    }

    // 유니티 EventSystem이 이 버튼을 선택(키보드 방향키 도달)했을 때 자동 실행
    public void OnSelect(BaseEventData eventData)
    {
        _rectTarget.DOKill();
        _textTarget.DOKill();

        // 🚨 쫀득하게 1.4배 커지고 노란색으로 변함
        _rectTarget.DOScale(_scaleSize, 0.2f).SetEase(Ease.OutBack);
        _textTarget.DOColor(_selectedColor, 0.2f);
    }

    // 유니티 EventSystem이 이 버튼에서 떠났을 때(다른 버튼으로 이동) 자동 실행
    public void OnDeselect(BaseEventData eventData)
    {
        _rectTarget.DOKill();
        _textTarget.DOKill();

        // 부드럽게 원래 크기(1.0)와 흰색으로 복귀
        _rectTarget.DOScale(1.0f, 0.2f).SetEase(Ease.OutQuad);
        _textTarget.DOColor(_normalColor, 0.2f);
    }
}