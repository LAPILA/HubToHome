using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using DG.Tweening;

public class BattleMenuUI : UIPanel 
{
    [Header("Buttons (ATTACK / ACT / ITEM / RUN)")]
    [SerializeField] private Button _attackBtn;
    [SerializeField] private Button _actBtn; // 기획에 맞춰 이름 변경
    [SerializeField] private Button _itemBtn;
    [SerializeField] private Button _runBtn;

    [Header("Sub Menu")]
    [SerializeField] private BattleSubMenu _subMenu;

    [Header("Menu Slide Animation")]
    [SerializeField] private float _menuSlideOffsetY = 150f; 
    [SerializeField] private float _menuSlideDuration = 0.25f;

    [Header("Style")]
    [SerializeField] private Color _selectedColor = new Color(1f, 0.95f, 0.3f);
    [SerializeField] private Color _normalColor = Color.white;
    [SerializeField] private Color _disabledColor = new Color(0.4f, 0.4f, 0.4f, 1f); // 🚨 회색조 비활성화 색상
    [SerializeField] private float _bouncePunch = 0.22f;

    [Header("Example Data")]
    [SerializeField] private List<SkillData> _exampleActs = new List<SkillData>();
    [SerializeField] private List<ItemData> _exampleItems = new List<ItemData>();

    private int _selectedIndex = 0;
    private PlayerCharacter _currentActor;
    private bool _inputEnabled = false;
    
    private Button[] _buttons;
    private PlayerMenuAction[] _mappedActions;

    private RectTransform _rectTransform;
    private float _baseMenuY;

    protected override void Awake()
    {
        base.Awake();
        _rectTransform = GetComponent<RectTransform>();
        _baseMenuY = _rectTransform.anchoredPosition.y;

        // 버튼과 실제 액션 열거형을 1:1 매핑
        _buttons = new[] { _attackBtn, _actBtn, _itemBtn, _runBtn };
        _mappedActions = new[] { 
            PlayerMenuAction.Attack, 
            PlayerMenuAction.Act, 
            PlayerMenuAction.Item, 
            PlayerMenuAction.Run 
        };

        // 클릭 이벤트 연동 (키보드 조작과 동일한 효과)
        for (int i = 0; i < _buttons.Length; i++)
        {
            int index = i;
            if (_buttons[i] != null)
                _buttons[i].onClick.AddListener(() => Confirm(index));
        }
    }

    protected override void OnShowComplete()
    {
        _inputEnabled = true;
        
        // 메뉴가 열릴 때 무조건 첫 번째 활성화된 버튼으로 커서를 맞춤
        _selectedIndex = 0;
        if (!_buttons[_selectedIndex].interactable) Navigate(1); 
        else HighlightButton(_selectedIndex);
    }

    public void SetActor(PlayerCharacter actor) => _currentActor = actor;

    // ── 🚨 보스전 도망 불가 처리 (BattleManager에서 호출) ──
    public void SetRunEnabled(bool isEnabled)
    {
        if (_runBtn != null)
        {
            _runBtn.interactable = isEnabled;
            _runBtn.GetComponent<Image>().color = isEnabled ? _normalColor : _disabledColor;
        }
    }

    private void Update()
    {
        if (!_inputEnabled || !IsVisible) return;
        if (_subMenu != null && _subMenu.IsActive) return;
        if (Keyboard.current == null) return;

        var kb = Keyboard.current;

        if (kb.leftArrowKey.wasPressedThisFrame || kb.aKey.wasPressedThisFrame)
            Navigate(-1);
        else if (kb.rightArrowKey.wasPressedThisFrame || kb.dKey.wasPressedThisFrame)
            Navigate(1);
        else if (kb.zKey.wasPressedThisFrame)
            Confirm(_selectedIndex);
    }

    private void Navigate(int dir)
    {
    if (_buttons == null || _buttons.Length == 0) return;

    int max = _buttons.Length;
    int loopCount = 0;

    do
    {
        _selectedIndex = (_selectedIndex + dir + max) % max;
        loopCount++;
        
        if (_buttons[_selectedIndex] == null) continue;

    } while (!_buttons[_selectedIndex].interactable && loopCount < max);

    HighlightButton(_selectedIndex);
    }

    private void Confirm(int index)
    {
        if (!_buttons[index].interactable) return; // 클릭 방어

        var action = _mappedActions[index]; 

        // ACT(스킬)와 ITEM은 서브 메뉴 오픈, 나머지는 즉시 실행
        if (action == PlayerMenuAction.Act) OpenActSubMenu();
        else if (action == PlayerMenuAction.Item) OpenItemSubMenu();
        else 
        {
            _inputEnabled = false;
            ExecuteDirectAction(index, action);
        }
    }

    private void OpenActSubMenu()
    {
        var entries = new List<IMenuEntry>();
        var sourceActs = (_currentActor != null && _currentActor.Skills?.Count > 0) ? _currentActor.Skills : _exampleActs;
        
        foreach (var act in sourceActs) 
        {
            if (act != null) entries.Add(new SkillMenuEntry(act));
        }

        if (entries.Count == 0) return;

        _inputEnabled = false; 
        SlideMenuUp();
        _subMenu.Open("ACT", entries, OnActSelected, OnSubMenuCancelled);
    }

    private void OpenItemSubMenu()
    {
        var entries = new List<IMenuEntry>();
        // 실제 인벤토리와 연동할 땐 GlobalDataManager.Instance.GetInventory() 기반으로 루프
        foreach (var i in _exampleItems) if (i != null) entries.Add(new ItemMenuEntry(i, 1));

        if (entries.Count == 0) return;

        _inputEnabled = false; 
        SlideMenuUp();
        _subMenu.Open("ITEM", entries, OnItemSelected, OnSubMenuCancelled);
    }

    private void OnActSelected(IMenuEntry entry)
    {
        SlideMenuDown();
        if (entry is SkillMenuEntry skillEntry)
            BattleManager.Instance.OnSubMenuActionSelected(_currentActor, PlayerMenuAction.Act, skillEntry.Data, null);
    }

    private void OnItemSelected(IMenuEntry entry)
    {
        SlideMenuDown();
        if (entry is ItemMenuEntry itemEntry)
            BattleManager.Instance.OnSubMenuActionSelected(_currentActor, PlayerMenuAction.Item, null, itemEntry.Data);
    }

    private void OnSubMenuCancelled()
    {
        SlideMenuDown();
        // DOTween 딜레이 없이 시퀀스로 정확하게 입력 활성화 타이밍 통제
        DOVirtual.DelayedCall(_menuSlideDuration, () => {
            _inputEnabled = true;
            HighlightButton(_selectedIndex);
        });
    }

    private void ExecuteDirectAction(int index, PlayerMenuAction action)
    {
        _buttons[index]?.transform.DOPunchScale(Vector3.one * 0.35f, 0.25f, 8, 0.5f).OnComplete(() => {
            BattleManager.Instance.OnPlayerActionSelected(_currentActor, action);
        });
    }

    private void SlideMenuUp()
    {
        _rectTransform.DOKill();
        _rectTransform.DOAnchorPosY(_baseMenuY + _menuSlideOffsetY, _menuSlideDuration).SetEase(Ease.OutCubic);
    }

    private void SlideMenuDown()
    {
        _rectTransform.DOKill();
        _rectTransform.DOAnchorPosY(_baseMenuY, _menuSlideDuration).SetEase(Ease.InCubic);
    }

    private void HighlightButton(int index)
    {
        for (int i = 0; i < _buttons.Length; i++)
        {
            if (_buttons[i] == null) continue;
            
            var img = _buttons[i].GetComponent<Image>();
            img.DOKill();
            _buttons[i].transform.DOKill();

            if (!_buttons[i].interactable)
            {
                // 비활성화된 버튼은 항상 회색 유지
                img.color = _disabledColor;
                _buttons[i].transform.localScale = Vector3.one;
            }
            else if (i == index)
            {
                img.DOColor(_selectedColor, 0.1f);
                _buttons[i].transform.DOPunchScale(Vector3.one * _bouncePunch, 0.3f, 8, 0.5f);
            }
            else
            {
                img.DOColor(_normalColor, 0.1f);
                _buttons[i].transform.DOScale(Vector3.one, 0.1f);
            }
        }
    }
}