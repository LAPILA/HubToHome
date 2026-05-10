using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;
using Sirenix.OdinInspector;

/// <summary>
/// 플레이어 턴에 표시되는 주 메뉴(Attack, Act, Item, Run)를 제어합니다.
/// 서브메뉴(스킬/아이템 목록) 등장 시 체력창(Party Status)과 함께 부드럽게 슬라이드됩니다.
/// </summary>
public class BattleMenuUI : UIPanel 
{
    #region [ UI Components ]
    [BoxGroup("Buttons"), LabelWidth(100)] [SerializeField] private Button _attackBtn;
    [BoxGroup("Buttons"), LabelWidth(100)] [SerializeField] private Button _actBtn; 
    [BoxGroup("Buttons"), LabelWidth(100)] [SerializeField] private Button _itemBtn;
    [BoxGroup("Buttons"), LabelWidth(100)] [SerializeField] private Button _runBtn;

    [BoxGroup("Sub Menu"), LabelWidth(100)] [SerializeField] private BattleSubMenu _subMenu;
    #endregion

    #region [ Animation & Style Settings ]
    [FoldoutGroup("Slide Animation"), LabelWidth(140)] 
    [SerializeField] private float _menuSlideOffsetY = 150f; 
    [FoldoutGroup("Slide Animation"), LabelWidth(140)] 
    [SerializeField] private float _menuSlideDuration = 0.25f;

    [FoldoutGroup("Style"), LabelWidth(140)] [SerializeField] private Color _selectedColor = new Color(1f, 0.95f, 0.3f);
    [FoldoutGroup("Style"), LabelWidth(140)] [SerializeField] private Color _normalColor = Color.white;
    [FoldoutGroup("Style"), LabelWidth(140)] [SerializeField] private Color _disabledColor = new Color(0.4f, 0.4f, 0.4f, 1f); 
    [FoldoutGroup("Style"), LabelWidth(140)] [SerializeField] private float _bouncePunch = 0.22f;
    #endregion

    #region [ Example / Default Data ]
    [FoldoutGroup("Fallback Data"), LabelWidth(140)] 
    [SerializeField] private List<SkillData> _exampleActs = new List<SkillData>();
    [FoldoutGroup("Fallback Data"), LabelWidth(140)] 
    [SerializeField] private List<ItemData> _exampleItems = new List<ItemData>();
    #endregion

    #region [ Internal State ]
    private int _selectedIndex = 0;
    private PlayerCharacter _currentActor;
    private bool _inputEnabled = false;
    
    private Button[] _buttons;
    private PlayerMenuAction[] _mappedActions;

    private RectTransform _rectTransform;
    private float _baseMenuY;
    #endregion

    #region [ Initialization ]
    protected override void Awake()
    {
        base.Awake();
        _rectTransform = GetComponent<RectTransform>();
        _baseMenuY = _rectTransform.anchoredPosition.y;

        _buttons = new[] { _attackBtn, _actBtn, _itemBtn, _runBtn };
        _mappedActions = new[] { 
            PlayerMenuAction.Attack, 
            PlayerMenuAction.Act, 
            PlayerMenuAction.Item, 
            PlayerMenuAction.Run 
        };

        // 마우스/터치 클릭 이벤트 연동
        for (int i = 0; i < _buttons.Length; i++)
        {
            int index = i;
            if (_buttons[i] != null)
                _buttons[i].onClick.AddListener(() => Confirm(index));
        }
    }
    #endregion

    #region [ Lifecycle & State ]
    protected override void OnShowComplete()
    {
        _inputEnabled = true;
        
        if (_buttons != null && _buttons.Length > 0)
        {
            if (!_buttons[_selectedIndex].interactable) Navigate(1); 
            else HighlightButton(_selectedIndex);
        }
    }

    public void SetActor(PlayerCharacter actor)
    {
        if (_currentActor != actor)
        {
            _selectedIndex = 0;
        }
        _currentActor = actor;
    }

    public void SetRunEnabled(bool isEnabled)
    {
        if (_runBtn != null)
        {
            _runBtn.interactable = isEnabled;
            _runBtn.GetComponent<Image>().color = isEnabled ? _normalColor : _disabledColor;
        }
    }
    #endregion

    #region [ Input & Navigation ]
    private void Update()
    {
        if (!_inputEnabled || !IsVisible) return;
        if (_subMenu != null && _subMenu.IsActive) return;

        if (GameInput.BattleLeftPressed)
            Navigate(-1);
        else if (GameInput.BattleRightPressed)
            Navigate(1);
        else if (GameInput.BattleConfirmPressed)
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
        if (!_buttons[index].interactable) return; 

        var action = _mappedActions[index]; 

        // 🚨 즉각 반응: 도망(Run)을 제외한 모든 액션 클릭 시 즉시 BattleReady 애니메이션 재생
        if (action != PlayerMenuAction.Run)
        {
            _currentActor?.PlayBattleAnim(PlayerCharacter.HashBattleReady);
        }

        if (action == PlayerMenuAction.Act) 
            OpenActSubMenu();
        else if (action == PlayerMenuAction.Item) 
            OpenItemSubMenu();
        else 
        {
            _inputEnabled = false;
            ExecuteDirectAction(index, action);
        }
    }
    #endregion

    #region [ Sub Menu Controls ]
    private void OpenActSubMenu()
    {
        var entries = new List<IMenuEntry>();
        var sourceActs = (_currentActor != null && _currentActor.Skills?.Count > 0) ? _currentActor.Skills : _exampleActs;
        
        foreach (var act in sourceActs) 
        {
            if (act != null) entries.Add(new SkillMenuEntry(act));
        }

        if (entries.Count == 0)
            entries.Add(new EmptyMenuEntry("NO ACT", "등록된 ACT/스킬이 없습니다."));

        _inputEnabled = false; 
        SlideMenuUp();
        _subMenu?.Open("ACT", entries, OnActSelected, OnSubMenuCancelled);
    }

    private void OpenItemSubMenu()
    {
        var entries = new List<IMenuEntry>();
        // 추후 GlobalDataManager.Instance.GetInventory() 연동
        foreach (var i in _exampleItems) if (i != null) entries.Add(new ItemMenuEntry(i, 1));

        if (entries.Count == 0)
            entries.Add(new EmptyMenuEntry("NO ITEM", "사용 가능한 아이템이 없습니다."));

        _inputEnabled = false; 
        SlideMenuUp();
        _subMenu?.Open("ITEM", entries, OnItemSelected, OnSubMenuCancelled);
    }

    private void OnActSelected(IMenuEntry entry)
    {
        SlideMenuDown();
        if (entry is EmptyMenuEntry)
        {
            DOVirtual.DelayedCall(_menuSlideDuration, () => {
                _inputEnabled = true;
                HighlightButton(_selectedIndex);
            });
            return;
        }
        if (entry is SkillMenuEntry skillEntry)
            BattleManager.Instance.OnSubMenuActionSelected(_currentActor, PlayerMenuAction.Act, skillEntry.Data, null);
    }

    private void OnItemSelected(IMenuEntry entry)
    {
        SlideMenuDown();
        if (entry is EmptyMenuEntry)
        {
            DOVirtual.DelayedCall(_menuSlideDuration, () => {
                _inputEnabled = true;
                HighlightButton(_selectedIndex);
            });
            return;
        }
        if (entry is ItemMenuEntry itemEntry)
            BattleManager.Instance.OnSubMenuActionSelected(_currentActor, PlayerMenuAction.Item, null, itemEntry.Data);
    }

    private void OnSubMenuCancelled()
    {
        SlideMenuDown();
        
        BattleManager.Instance.CancelActionSelection();

        DOVirtual.DelayedCall(_menuSlideDuration, () => {
            _inputEnabled = true;
            HighlightButton(_selectedIndex);
        });
    }

    private void ExecuteDirectAction(int index, PlayerMenuAction action)
    {
        if (_buttons[index] == null) return;
        _buttons[index].transform.DOKill(true);
        _buttons[index].transform.localScale = Vector3.one;

        _buttons[index].transform.DOPunchScale(Vector3.one * 0.35f, 0.25f, 8, 0.5f).OnComplete(() => {
            BattleManager.Instance.OnPlayerActionSelected(_currentActor, action);
        });
    }
    #endregion

    #region [ UI Animations (Slide & Sync) ]
    private void SlideMenuUp()
    {
        _rectTransform.DOKill();
        _rectTransform.DOAnchorPosY(_baseMenuY + _menuSlideOffsetY, _menuSlideDuration).SetEase(Ease.OutCubic);
        BattleUIController.Instance?.MovePartyPanelUp(_menuSlideOffsetY, _menuSlideDuration);
    }

    private void SlideMenuDown()
    {
        _rectTransform.DOKill();
        _rectTransform.DOAnchorPosY(_baseMenuY, _menuSlideDuration).SetEase(Ease.InCubic);
        BattleUIController.Instance?.ResetPartyPanelPosition(_menuSlideDuration);
    }

    private void HighlightButton(int index)
    {
        for (int i = 0; i < _buttons.Length; i++)
        {
            if (_buttons[i] == null) continue;
            
            var img = _buttons[i].GetComponent<Image>();
            img.DOKill();
            
            // 🚨 핵심 방어코드: 트윈 강제 종료 및 스케일 초기화
            _buttons[i].transform.DOKill(true); 
            _buttons[i].transform.localScale = Vector3.one; 

            if (!_buttons[i].interactable)
            {
                img.color = _disabledColor;
            }
            else if (i == index)
            {
                img.DOColor(_selectedColor, 0.1f);
                _buttons[i].transform.DOPunchScale(Vector3.one * _bouncePunch, 0.3f, 8, 0.5f);
            }
            else
            {
                img.DOColor(_normalColor, 0.1f);
            }
        }
    }

    
    #endregion
}