using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using DG.Tweening;

public class BattleMenuUI : UIPanel 
{
    [Header("Buttons")]
    [SerializeField] private Button _attackBtn;
    [SerializeField] private Button _skillBtn;
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
    [SerializeField] private float _bouncePunch = 0.22f;

    [Header("Example Data")]
    [SerializeField] private List<SkillData> _exampleSkills = new List<SkillData>();
    [SerializeField] private List<ItemData> _exampleItems = new List<ItemData>();

    private int _selectedIndex = 0;
    private PlayerCharacter _currentActor;
    private bool _inputEnabled = false;
    private Button[] _buttons;

    private RectTransform _rectTransform;
    private float _baseMenuY;

    protected override void Awake()
    {
        base.Awake();
        _rectTransform = GetComponent<RectTransform>();
        _baseMenuY = _rectTransform.anchoredPosition.y;

        _buttons = new[] { _attackBtn, _skillBtn, _itemBtn, _runBtn };

        _attackBtn?.onClick.AddListener(() => Confirm(0));
        _skillBtn?.onClick.AddListener(() => Confirm(1));
        _itemBtn?.onClick.AddListener(() => Confirm(2));
        _runBtn?.onClick.AddListener(() => Confirm(3));
    }

    protected override void OnShowComplete()
    {
        _selectedIndex = 0;
        _inputEnabled = true;
        HighlightButton(_selectedIndex);
    }

    public void SetActor(PlayerCharacter actor) => _currentActor = actor;

    private void Update()
    {
        if (!_inputEnabled || !IsVisible) return;
        if (_subMenu != null && _subMenu.IsActive) return;

        if (Keyboard.current.leftArrowKey.wasPressedThisFrame || Keyboard.current.aKey.wasPressedThisFrame)
            Navigate(-1);
        else if (Keyboard.current.rightArrowKey.wasPressedThisFrame || Keyboard.current.dKey.wasPressedThisFrame)
            Navigate(1);
        else if (Keyboard.current.zKey.wasPressedThisFrame)
            Confirm(_selectedIndex);
    }

    private void Navigate(int dir)
    {
        _selectedIndex = (_selectedIndex + dir + _buttons.Length) % _buttons.Length;
        HighlightButton(_selectedIndex);
    }

    private void Confirm(int index)
    {
        var action = (PlayerMenuAction)index; 

        if (action == PlayerMenuAction.Skill) OpenSkillSubMenu();
        else if (action == PlayerMenuAction.Item) OpenItemSubMenu();
        else 
        {
            _inputEnabled = false;
            ExecuteDirectAction(index);
        }
    }

    private void OpenSkillSubMenu()
    {
        var entries = new List<IMenuEntry>();
        var sourceSkills = (_currentActor != null && _currentActor.Skills?.Count > 0) ? _currentActor.Skills : _exampleSkills;
        
        foreach (var s in sourceSkills) 
        {
            if (s != null) 
            {
                if (_currentActor.CurrentMP >= s.MPCost) entries.Add(new SkillMenuEntry(s));
                else Debug.Log($"{s.SkillName}은 MP가 부족합니다!"); 
            }
        }

        if (entries.Count == 0) return;

        _inputEnabled = false; 
        SlideMenuUp();
        _subMenu.Open("MAGIC", entries, OnSkillSelected, OnSubMenuCancelled);
    }

    private void OpenItemSubMenu()
    {
        var entries = new List<IMenuEntry>();
        foreach (var i in _exampleItems) if (i != null) entries.Add(new ItemMenuEntry(i, 1));

        if (entries.Count == 0) return;

        _inputEnabled = false; 
        SlideMenuUp();
        _subMenu.Open("ITEM", entries, OnItemSelected, OnSubMenuCancelled);
    }

    // 🚨 딜레이 호출(DOVirtual)을 없애고 즉시 명령을 하달하도록 수정
    private void OnSkillSelected(IMenuEntry entry)
    {
        SlideMenuDown();
        if (entry is SkillMenuEntry skillEntry)
        {
            BattleManager.Instance.OnSubMenuActionSelected(_currentActor, PlayerMenuAction.Skill, skillEntry.Data, null);
        }
    }

    private void OnItemSelected(IMenuEntry entry)
    {
        SlideMenuDown();
        if (entry is ItemMenuEntry itemEntry)
        {
            BattleManager.Instance.OnSubMenuActionSelected(_currentActor, PlayerMenuAction.Item, null, itemEntry.Data);
        }
    }

    private void OnSubMenuCancelled()
    {
        SlideMenuDown();
        DOVirtual.DelayedCall(_menuSlideDuration, () => {
            _inputEnabled = true;
            HighlightButton(_selectedIndex);
        });
    }

    private void ExecuteDirectAction(int index)
    {
        _buttons[index]?.transform.DOPunchScale(Vector3.one * 0.35f, 0.25f, 8, 0.5f).OnComplete(() => {
            BattleManager.Instance.OnPlayerActionSelected(_currentActor, (PlayerMenuAction)index);
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
            if (i == index)
            {
                img.DOColor(_selectedColor, 0.1f);
                _buttons[i].transform.DOKill();
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