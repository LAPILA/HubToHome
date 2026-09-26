using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>고정된 상세 영역. 표시 수명과 목록 입력 수명을 분리합니다.</summary>
public class BattleSubMenu : MonoBehaviour
{
    [SerializeField] private RectTransform _rectTransform;
    [SerializeField] private OptionRowUI _rowPrefab;
    [SerializeField] private RectTransform _container;
    [SerializeField] private RectTransform _viewport;
    [SerializeField] private float _rowHeight = 23f;
    [SerializeField] private int _visibleRows = 4;
    [SerializeField] private TextMeshProUGUI _titleText;
    [SerializeField] private TextMeshProUGUI _descriptionText;
    [SerializeField] private TextMeshProUGUI _scrollHint;
    [SerializeField] private Color _selectedColor = new Color(1f, 0.87f, 0.35f);
    [SerializeField] private Color _normalColor = Color.white;

    private readonly List<IMenuEntry> _entries = new List<IMenuEntry>();
    private readonly List<OptionRowUI> _spawnedRows = new List<OptionRowUI>();
    private int _currentIndex;
    private int _topVisibleRow;
    private int _openedFrame = -1;
    private PlayerCharacter _actor;
    private Action<IMenuEntry> _onConfirmCallback;
    private Action _onCancelCallback;
    private GridLayoutGroup _grid;
    public bool IsActive { get; private set; }
    public int SelectedIndex => _currentIndex;

    // 고정 HUD에서는 BattleMenuUI 한 곳이 좌우/상하/확정을 소유합니다.
    public void MoveSelection(int offset) => ChangeIndex(offset);
    public void SelectIndex(int index)
    {
        _currentIndex = Mathf.Clamp(index, 0, Mathf.Max(0, _entries.Count - 1));
        RefreshRows();
        AutoScroll();
    }
    public bool TryGetSelectedEntry(out IMenuEntry entry)
    {
        entry = _entries.Count > 0 ? _entries[_currentIndex] : null;
        return BattleMenuEntryPresentation.CanUse(entry, _actor);
    }

    private void Awake()
    {
        if (_rectTransform == null) _rectTransform = transform as RectTransform;
        if (_viewport == null && _container != null) _viewport = _container.parent as RectTransform;
        if (_container != null) _grid = _container.GetComponent<GridLayoutGroup>();
    }

    private void Update()
    {
        if (!IsActive || Time.frameCount <= _openedFrame || GameInput.BattleUIInputConsumed) return;
        if (BattleUIController.Instance != null && BattleUIController.Instance.IsNarrationBlockingInput()) return;
        if (GameInput.BattleUpPressed) ChangeIndex(-1);
        else if (GameInput.BattleDownPressed) ChangeIndex(1);
        if (GameInput.BattleConfirmPressed) ConfirmSelection();
        else if (GameInput.BattleCancelPressed) Close();
    }

    public void Preview(string title, List<IMenuEntry> entries, PlayerCharacter actor)
    {
        IsActive = false;
        ClearCallbacks();
        _actor = actor;
        _entries.Clear();
        if (entries != null) _entries.AddRange(entries);
        _currentIndex = 0;
        _topVisibleRow = 0;
        if (_titleText != null) _titleText.text = title;
        gameObject.SetActive(true);
        RefreshRows();
        AutoScroll();
    }

    public void Open(string title, List<IMenuEntry> entries, Action<IMenuEntry> onConfirm,
        Action onCancel, PlayerCharacter actor = null)
    {
        Preview(title, entries, actor);
        _onConfirmCallback = onConfirm;
        _onCancelCallback = onCancel;
        IsActive = true;
        _openedFrame = Time.frameCount;
    }

    public void Close()
    {
        if (!IsActive) return;
        GameInput.ConsumeBattleUIInput();
        IsActive = false;
        var callback = _onCancelCallback;
        ClearCallbacks();
        callback?.Invoke();
    }

    // 상세 내용은 남기고 입력/콜백만 정리합니다. 숨김은 소유자가 명시합니다.
    public void ForceCloseImmediate()
    {
        IsActive = false;
        ClearCallbacks();
    }

    private void OnDisable() => ForceCloseImmediate();

    private void ConfirmSelection()
    {
        if (!IsActive || _entries.Count == 0) return;
        GameInput.ConsumeBattleUIInput();
        IMenuEntry selected = _entries[_currentIndex];
        if (!BattleMenuEntryPresentation.CanUse(selected, _actor)) return;
        IsActive = false;
        var callback = _onConfirmCallback;
        ClearCallbacks();
        callback?.Invoke(selected);
    }

    private void ClearCallbacks()
    {
        _onConfirmCallback = null;
        _onCancelCallback = null;
    }

    private void RefreshRows()
    {
        if (_rowPrefab == null || _container == null) return;
        while (_spawnedRows.Count < _entries.Count)
            _spawnedRows.Add(Instantiate(_rowPrefab, _container));
        for (int i = 0; i < _spawnedRows.Count; i++)
        {
            if (i >= _entries.Count) _spawnedRows[i].SetEmpty();
            else _spawnedRows[i].SetEntry(_entries[i], i == _currentIndex,
                _selectedColor, _normalColor, 1f, BattleMenuEntryPresentation.CanUse(_entries[i], _actor));
        }
        float step = RowStep;
        float height = _entries.Count * step - (_grid != null && _entries.Count > 0 ? _grid.spacing.y : 0f);
        _container.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, Mathf.Max(0f, height));
        UpdateDescription();
    }

    private float RowStep => _grid != null ? _grid.cellSize.y + _grid.spacing.y : Mathf.Max(1f, _rowHeight);

    private void ChangeIndex(int offset)
    {
        if (_entries.Count == 0) return;
        int next = Mathf.Clamp(_currentIndex + offset, 0, _entries.Count - 1);
        if (next == _currentIndex) return;
        _currentIndex = next;
        RefreshRows();
        AutoScroll();
    }

    private void AutoScroll()
    {
        if (_container == null) return;
        float step = RowStep;
        float viewportHeight = _viewport != null ? _viewport.rect.height : _visibleRows * step;
        int visible = Mathf.Max(1, Mathf.FloorToInt((viewportHeight + (_grid != null ? _grid.spacing.y : 0f)) / step));
        if (_currentIndex < _topVisibleRow) _topVisibleRow = _currentIndex;
        else if (_currentIndex >= _topVisibleRow + visible) _topVisibleRow = _currentIndex - visible + 1;
        float maxY = Mathf.Max(0f, _container.rect.height - viewportHeight);
        _container.anchoredPosition = new Vector2(_container.anchoredPosition.x,
            Mathf.Clamp(_topVisibleRow * step, 0f, maxY));
        if (_scrollHint != null)
            _scrollHint.text = _entries.Count > visible
                ? $"{_currentIndex + 1}/{_entries.Count}  ↑↓" : string.Empty;
    }

    private void UpdateDescription()
    {
        if (_descriptionText == null) return;
        _descriptionText.text = _entries.Count > 0
            ? BattleMenuEntryPresentation.Describe(_entries[_currentIndex], _actor) : string.Empty;
    }
}
