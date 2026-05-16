using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;
using TMPro;

/// <summary>
/// 서브 메뉴 UI 컨트롤러.
/// JRPG 스타일의 상하 스크롤 그리드 및 콜백 메모리 릭 방지를 적용했습니다.
/// </summary>
public class BattleSubMenu : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private RectTransform _rectTransform;
    
    [Header("Dynamic Grid Settings")]
    [SerializeField] private OptionRowUI _rowPrefab;       
    [SerializeField] private RectTransform _container;     
    [SerializeField] private float _rowHeight = 50f;       
    [SerializeField] private int _visibleRows = 3;         

    [Header("Text References")]
    [SerializeField] private TextMeshProUGUI _titleText;
    [SerializeField] private TextMeshProUGUI _descriptionText;

    [Header("Animation Settings")]
    [SerializeField] private float _showY = 150f;
    [SerializeField] private float _hideY = -200f;
    [SerializeField] private float _slideDuration = 0.25f;

    [Header("Style Settings")]
    [SerializeField] private Color _selectedColor = new Color(1f, 0.95f, 0.3f);
    [SerializeField] private Color _normalColor = Color.white;
    [SerializeField] private float _selectedScale = 1.1f;

    private readonly List<IMenuEntry> _entries = new List<IMenuEntry>();
    private readonly List<OptionRowUI> _spawnedRows = new List<OptionRowUI>(); 
    
    private int _currentIndex = 0;
    private int _topVisibleRow = 0; 
    
    private System.Action<IMenuEntry> _onConfirmCallback;
    private System.Action _onCancelCallback;

    public bool IsActive { get; private set; }
    private bool _isAnimating = false;

    private void Awake()
    {
        if (_rectTransform == null) _rectTransform = GetComponent<RectTransform>();
        _rectTransform.anchoredPosition = new Vector2(_rectTransform.anchoredPosition.x, _hideY);
    }

    private void Update()
    {
        if (!IsActive || _isAnimating) return;
        if (BattleUIController.Instance != null && BattleUIController.Instance.IsNarrationBlockingInput()) return;

        // 상하좌우 그리드 이동
        if (GameInput.BattleUpPressed) ChangeIndex(-2);
        else if (GameInput.BattleDownPressed) ChangeIndex(2);
        else if (GameInput.BattleLeftPressed) ChangeIndex(-1);
        else if (GameInput.BattleRightPressed) ChangeIndex(1);

        if (GameInput.BattleConfirmPressed) ConfirmSelection();
        else if (GameInput.BattleCancelPressed) Close();
    }

    public void Open(string title, List<IMenuEntry> entries, System.Action<IMenuEntry> onConfirm, System.Action onCancel)
    {
        if (_isAnimating) return;

        _entries.Clear();
        if (entries != null) _entries.AddRange(entries);

        _currentIndex = 0;
        _topVisibleRow = 0; 
        
        _onConfirmCallback = onConfirm;
        _onCancelCallback = onCancel;
        
        IsActive = true;

        if (_titleText != null) _titleText.text = title;
        
        SpawnAndRefreshRows();
        
        _container.anchoredPosition = new Vector2(_container.anchoredPosition.x, 0);
        PlaySlideIn();
    }

    public void Close()
    {
        if (!IsActive || _isAnimating) return;
        IsActive = false;
        
        var tempCancel = _onCancelCallback;
        ClearCallbacks();
        tempCancel?.Invoke();
        
        PlaySlideOut(null); 
    }

    private void ConfirmSelection()
    {
        if (_entries.Count == 0 || _isAnimating) return;
        
        var selected = _entries[_currentIndex];
        IsActive = false;
        _isAnimating = true; 
        
        _spawnedRows[_currentIndex].transform.DOPunchScale(Vector3.one * 0.2f, 0.15f).OnComplete(() => {
            var tempConfirm = _onConfirmCallback;
            ClearCallbacks();
            tempConfirm?.Invoke(selected);
            PlaySlideOut(null);
        });
    }

    private void ClearCallbacks()
    {
        _onConfirmCallback = null;
        _onCancelCallback = null;
    }

    private void SpawnAndRefreshRows()
    {
        int needed = _entries.Count;
        while (_spawnedRows.Count < needed)
        {
            var newRow = Instantiate(_rowPrefab, _container);
            _spawnedRows.Add(newRow);
        }

        for (int i = 0; i < _spawnedRows.Count; i++)
        {
            if (i < needed)
            {
                bool isSelected = (i == _currentIndex);
                _spawnedRows[i].SetEntry(_entries[i], isSelected, _selectedColor, _normalColor, _selectedScale);
            }
            else _spawnedRows[i].SetEmpty();
        }
        UpdateDescription();
    }

    private void ChangeIndex(int offset)
    {
        if (_entries.Count == 0) return;

        int prevIndex = _currentIndex;
        int targetIndex = _currentIndex + offset;

        // 🚨 클램핑 방식: 배열 범위를 넘어가면 무시 (좌우/상하 리스트 꼬임 방지)
        if (targetIndex < 0 || targetIndex >= _entries.Count) return;

        _currentIndex = targetIndex;

        _spawnedRows[prevIndex].SetEntry(_entries[prevIndex], false, _selectedColor, _normalColor, _selectedScale);
        _spawnedRows[_currentIndex].SetEntry(_entries[_currentIndex], true, _selectedColor, _normalColor, _selectedScale);

        UpdateDescription();
        AutoScroll();
    }

    private void AutoScroll()
    {
        // 2열 그리드 기준 스크롤 수학 계산
        int currentRow = _currentIndex / 2;
        if (currentRow < _topVisibleRow) _topVisibleRow = currentRow;
        else if (currentRow >= _topVisibleRow + _visibleRows) _topVisibleRow = currentRow - _visibleRows + 1;

        float targetY = _topVisibleRow * _rowHeight;
        _container.DOKill();
        _container.DOAnchorPosY(targetY, 0.15f).SetEase(Ease.OutQuad);
    }

    private void UpdateDescription()
    {
        if (_descriptionText != null && _entries.Count > 0)
            _descriptionText.text = _entries[_currentIndex].Description;
    }

    private void PlaySlideIn()
    {
        _isAnimating = true;
        _rectTransform.DOKill();
        _rectTransform.DOAnchorPosY(_showY, _slideDuration).SetEase(Ease.OutCubic).OnComplete(() => _isAnimating = false);
    }

    private void PlaySlideOut(System.Action onComplete)
    {
        _isAnimating = true;
        _rectTransform.DOKill();
        _rectTransform.DOAnchorPosY(_hideY, _slideDuration).SetEase(Ease.InCubic).OnComplete(() => {
            _isAnimating = false;
            onComplete?.Invoke();
        });
    }
}