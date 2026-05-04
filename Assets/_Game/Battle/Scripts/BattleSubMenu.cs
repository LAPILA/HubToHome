using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using DG.Tweening;
using TMPro;

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
    private int _topVisibleRow = 0; // 스크롤 상단 위치 추적용
    
    private System.Action<IMenuEntry> _onConfirmCallback;
    private System.Action _onCancelCallback;

    public bool IsActive { get; private set; }

    private void Awake()
    {
        if (_rectTransform == null) _rectTransform = GetComponent<RectTransform>();
        
        // 초기에는 아래에 숨겨둠 (오브젝트를 끄지 않음)
        _rectTransform.anchoredPosition = new Vector2(_rectTransform.anchoredPosition.x, _hideY);
    }

    private void Update()
    {
        if (!IsActive) return;

        // 2D 그리드 이동
        if (Keyboard.current.upArrowKey.wasPressedThisFrame) ChangeIndex(-2);        
        else if (Keyboard.current.downArrowKey.wasPressedThisFrame) ChangeIndex(2);  
        else if (Keyboard.current.leftArrowKey.wasPressedThisFrame) ChangeIndex(-1); 
        else if (Keyboard.current.rightArrowKey.wasPressedThisFrame) ChangeIndex(1); 

        if (Keyboard.current.zKey.wasPressedThisFrame) ConfirmSelection();
        else if (Keyboard.current.xKey.wasPressedThisFrame) Close();
    }

    public void Open(string title, List<IMenuEntry> entries, System.Action<IMenuEntry> onConfirm, System.Action onCancel)
    {
        _entries.Clear();
        if (entries != null) _entries.AddRange(entries);

        _currentIndex = 0;
        _topVisibleRow = 0; // 스크롤 초기화
        _onConfirmCallback = onConfirm;
        _onCancelCallback = onCancel;
        IsActive = true;

        if (_titleText != null) _titleText.text = title;
        
        SpawnAndRefreshRows();
        
        // 스크롤 맨 위로 즉시 리셋
        _container.anchoredPosition = new Vector2(_container.anchoredPosition.x, 0);
        
        PlaySlideIn();
    }

    public void Close()
    {
        if (!IsActive) return;
        IsActive = false;
        _onCancelCallback?.Invoke();
        PlaySlideOut(null); 
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
            else
            {
                _spawnedRows[i].SetEmpty();
            }
        }

        UpdateDescription();
    }

    private void ChangeIndex(int offset)
    {
        if (_entries.Count == 0) return;

        int prevIndex = _currentIndex;
        int targetIndex = _currentIndex + offset;

        if (targetIndex < 0 || targetIndex >= _entries.Count) return;

        _currentIndex = targetIndex;

        _spawnedRows[prevIndex].SetEntry(_entries[prevIndex], false, _selectedColor, _normalColor, _selectedScale);
        _spawnedRows[_currentIndex].SetEntry(_entries[_currentIndex], true, _selectedColor, _normalColor, _selectedScale);

        UpdateDescription();
        AutoScroll();
    }

    private void AutoScroll()
    {
        int currentRow = _currentIndex / 2;

        if (currentRow < _topVisibleRow)
        {
            _topVisibleRow = currentRow;
        }
        else if (currentRow >= _topVisibleRow + _visibleRows)
        {
            _topVisibleRow = currentRow - _visibleRows + 1;
        }

        float targetY = _topVisibleRow * _rowHeight;
        _container.DOKill();
        _container.DOAnchorPosY(targetY, 0.15f).SetEase(Ease.OutQuad);
    }

    private void ConfirmSelection()
    {
        if (_entries.Count == 0) return;
        var selected = _entries[_currentIndex];
        
        _spawnedRows[_currentIndex].transform.DOPunchScale(Vector3.one * 0.2f, 0.15f).OnComplete(() => {
            IsActive = false;
            _onConfirmCallback?.Invoke(selected);
            PlaySlideOut(null);
        });
    }

    private void UpdateDescription()
    {
        if (_descriptionText != null && _entries.Count > 0)
            _descriptionText.text = _entries[_currentIndex].Description;
    }

    // ── 애니메이션 (SetActive 완전 제거) ──
    private void PlaySlideIn()
    {
        _rectTransform.DOKill();
        _rectTransform.DOAnchorPosY(_showY, _slideDuration).SetEase(Ease.OutCubic);
    }

    private void PlaySlideOut(System.Action onComplete)
    {
        _rectTransform.DOKill();
        _rectTransform.DOAnchorPosY(_hideY, _slideDuration).SetEase(Ease.InCubic).OnComplete(() => {
            onComplete?.Invoke();
        });
    }
}