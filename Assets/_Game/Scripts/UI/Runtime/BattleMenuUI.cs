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
    [SerializeField] private float _menuSlideExtraOffsetY = 50f;
    [FoldoutGroup("Slide Animation"), LabelWidth(140)] 
    [SerializeField] private float _menuSlideDuration = 0.25f;

    [FoldoutGroup("Style"), LabelWidth(140)] [SerializeField] private Color _selectedColor = new Color(1f, 0.95f, 0.3f);
    [FoldoutGroup("Style"), LabelWidth(140)] [SerializeField] private Color _normalColor = Color.white;
    [FoldoutGroup("Style"), LabelWidth(140)] [SerializeField] private Color _disabledColor = new Color(0.4f, 0.4f, 0.4f, 1f); 
    [FoldoutGroup("Style"), LabelWidth(140)] [SerializeField] private float _bouncePunch = 0.22f;
    #endregion

    #region [ Example / Default Data ]
    [FoldoutGroup("Fallback Data"), LabelWidth(140)] 
    [SerializeField] private List<SkillData> _exampleSkills = new List<SkillData>();
    [FoldoutGroup("Fallback Data"), LabelWidth(140)] 
    [SerializeField] private List<ItemData> _exampleItems = new List<ItemData>();
    #endregion

    #region [ Internal State ]
    private int _selectedIndex = 0;
    private PlayerCharacter _currentActor;
    private bool _inputEnabled = false;
    private bool _isExternallySuspended = false;
    
    private Button[] _buttons;
    private PlayerMenuAction[] _mappedActions;

    private RectTransform _rectTransform;
    private float _baseMenuY;
    private Image[] _buttonImages;
    private Tween[] _buttonColorTweens;
    private Tween[] _buttonPunchTweens;
    private Tween _menuMoveTween;
    private Tween _resumeInputTween;
    #endregion

    #region [ Initialization ]
    protected override void Awake()
    {
        base.Awake();
        _rectTransform = GetComponent<RectTransform>();
        _baseMenuY = _rectTransform.anchoredPosition.y;

        _buttons = new[] { _attackBtn, _actBtn, _itemBtn, _runBtn };
        _buttonImages = new Image[_buttons.Length];
        _buttonColorTweens = new Tween[_buttons.Length];
        _buttonPunchTweens = new Tween[_buttons.Length];
        _mappedActions = new[] { 
            PlayerMenuAction.Attack, 
            PlayerMenuAction.Skill, 
            PlayerMenuAction.Item, 
            PlayerMenuAction.Run 
        };

        // 마우스/터치 클릭 이벤트 연동
        for (int i = 0; i < _buttons.Length; i++)
        {
            int index = i;
            if (_buttons[i] != null)
            {
                _buttonImages[i] = _buttons[i].GetComponent<Image>();
                _buttons[i].onClick.AddListener(() => Confirm(index));
            }
        }
    }
    #endregion

    #region [ Lifecycle & State ]
    public override void Hide()
    {
        StopOwnedAnimations();
        base.Hide();
    }

    public override void HideImmediate()
    {
        StopOwnedAnimations();
        base.HideImmediate();
    }

    protected override void OnDisable()
    {
        StopOwnedAnimations();
        base.OnDisable();
    }

    protected override void OnDestroy()
    {
        StopOwnedAnimations();
        base.OnDestroy();
    }

    private void StopOwnedAnimations()
    {
        _inputEnabled = false;
        Kill(ref _menuMoveTween);
        Kill(ref _resumeInputTween);
        if (_rectTransform != null)
            _rectTransform.anchoredPosition = new Vector2(_rectTransform.anchoredPosition.x, _baseMenuY);
        if (_buttons == null) return;
        for (int i = 0; i < _buttons.Length; i++)
        {
            Kill(ref _buttonColorTweens[i]);
            Kill(ref _buttonPunchTweens[i]);
            if (_buttons[i] != null) _buttons[i].transform.localScale = Vector3.one;
        }
    }

    private static void Kill(ref Tween tween)
    {
        Tween owned = tween;
        tween = null;
        if (owned != null && owned.IsActive()) owned.Kill(false);
    }

    protected override void OnShowComplete()
    {
        if (_isExternallySuspended) return;

        _inputEnabled = true;
        
        if (_buttons != null && _buttons.Length > 0)
        {
            if (_buttons[_selectedIndex] == null || !_buttons[_selectedIndex].interactable) Navigate(1);
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
            if (_buttonColorTweens != null) Kill(ref _buttonColorTweens[3]);
            Image image = _runBtn.GetComponent<Image>();
            if (image != null) image.color = isEnabled ? _normalColor : _disabledColor;
        }
    }

    public void SuspendForModuleSwitch()
    {
        _isExternallySuspended = true;
        _inputEnabled = false;

        _subMenu?.ForceCloseImmediate();

        HideImmediate();
    }

    public void ResumeAfterModuleSwitch()
    {
        _isExternallySuspended = false;

        if (_rectTransform != null)
        {
            Kill(ref _menuMoveTween);
            _rectTransform.anchoredPosition = new Vector2(_rectTransform.anchoredPosition.x, _baseMenuY);
        }
    }
    #endregion

    #region [ Input & Navigation ]
    private void Update()
    {
        if (_isExternallySuspended) return;
        if (!_inputEnabled || !IsVisible) return;
        if (_subMenu != null && _subMenu.IsActive) return;
        if (BattleUIController.Instance != null && BattleUIController.Instance.IsNarrationBlockingInput()) return;

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

        for (int i = 0; i < _buttons.Length; i++)
        {
            _selectedIndex = (_selectedIndex + dir + _buttons.Length) % _buttons.Length;
            if (_buttons[_selectedIndex] != null && _buttons[_selectedIndex].interactable) break;
        }

        HighlightButton(_selectedIndex);
    }

    private void Confirm(int index)
    {
        if (!_inputEnabled || _isExternallySuspended || !isActiveAndEnabled || !IsVisible
            || _buttons == null || index < 0 || index >= _buttons.Length || _buttons[index] == null)
            return;
        if (BattleUIController.Instance != null && BattleUIController.Instance.IsNarrationBlockingInput()) return;
        if (!_buttons[index].interactable) return; 

        var action = _mappedActions[index]; 

        // 🚨 즉각 반응: 도망(Run)을 제외한 모든 액션 클릭 시 즉시 BattleReady 애니메이션 재생
        if (action != PlayerMenuAction.Run)
        {
            if (_currentActor != null) _currentActor.PlayBattleAnim(PlayerCharacter.HashBattleReady);
        }

        if (action == PlayerMenuAction.Skill) 
            OpenSkillSubMenu();
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
    private void OpenSkillSubMenu()
    {
        var entries = new List<IMenuEntry>();
        var sourceSkills = (_currentActor != null && _currentActor.Skills?.Count > 0) ? _currentActor.Skills : _exampleSkills;
        
        foreach (var skill in sourceSkills) 
        {
            if (skill != null) entries.Add(new SkillMenuEntry(skill));
        }

        if (entries.Count == 0)
            entries.Add(new EmptyMenuEntry("NO SKILL", "등록된 스킬이 없습니다."));

        _inputEnabled = false; 
        SlideMenuUp();
        _subMenu?.Open("SKILL", entries, OnSkillSelected, OnSubMenuCancelled);
    }

    private void OpenItemSubMenu()
    {
        var entries = new List<IMenuEntry>();
        GlobalDataManager global = GlobalDataManager.Instance;
        if (global != null)
        {
            foreach (KeyValuePair<string, int> pair in global.GetInventory())
            {
                if (pair.Value <= 0) continue;
                ItemData item = ItemDatabase.FindById(pair.Key);
                if (item == null || item.Type != ItemType.Consumable || !item.UsableInBattle) continue;
                entries.Add(new ItemMenuEntry(item, pair.Value));
            }
        }

        if (entries.Count == 0)
            entries.Add(new EmptyMenuEntry("NO ITEM", "사용 가능한 아이템이 없습니다."));

        _inputEnabled = false;
        SlideMenuUp();
        _subMenu?.Open("ITEM", entries, OnItemSelected, OnSubMenuCancelled);
    }

    private void OnSkillSelected(IMenuEntry entry)
    {
        if (!CanReceiveSubMenuCallback()) return;
        SlideMenuDown();
        if (entry is EmptyMenuEntry)
        {
            ResumeInputAfterSlide();
            return;
        }
        BattleManager manager = BattleManager.Instance;
        if (manager != null && entry is SkillMenuEntry skillEntry)
            manager.OnSubMenuActionSelected(_currentActor, PlayerMenuAction.Skill, skillEntry.Data, null);
    }

    private void OnItemSelected(IMenuEntry entry)
    {
        if (!CanReceiveSubMenuCallback()) return;
        SlideMenuDown();
        if (entry is EmptyMenuEntry)
        {
            ResumeInputAfterSlide();
            return;
        }
        BattleManager manager = BattleManager.Instance;
        if (manager != null && entry is ItemMenuEntry itemEntry)
            manager.OnSubMenuActionSelected(_currentActor, PlayerMenuAction.Item, null, itemEntry.Data);
    }

    private void OnSubMenuCancelled()
    {
        if (!CanReceiveSubMenuCallback()) return;
        SlideMenuDown();
        BattleManager manager = BattleManager.Instance;
        if (manager != null) manager.CancelActionSelection();

        ResumeInputAfterSlide();
    }

    private bool CanReceiveSubMenuCallback() => this != null && isActiveAndEnabled && !_isExternallySuspended;

    private void ResumeInputAfterSlide()
    {
        Kill(ref _resumeInputTween);
        _resumeInputTween = DOVirtual.DelayedCall(_menuSlideDuration, () => {
            _resumeInputTween = null;
            if (this == null || !isActiveAndEnabled || _isExternallySuspended || !IsVisible) return;
            _inputEnabled = true;
            HighlightButton(_selectedIndex);
        }).SetRecyclable(false).SetLink(gameObject, LinkBehaviour.KillOnDisable);
    }

    private void ExecuteDirectAction(int index, PlayerMenuAction action)
    {
        if (_buttons[index] == null) return;
        Kill(ref _buttonPunchTweens[index]);
        _buttons[index].transform.localScale = Vector3.one;
        PlayerCharacter actor = _currentActor;
        _buttonPunchTweens[index] = _buttons[index].transform.DOPunchScale(Vector3.one * 0.35f, 0.25f, 8, 0.5f)
            .SetRecyclable(false).SetLink(_buttons[index].gameObject, LinkBehaviour.KillOnDisable)
            .OnComplete(() => {
                if (this == null || !isActiveAndEnabled || _isExternallySuspended || actor == null || _currentActor != actor) return;
                BattleManager manager = BattleManager.Instance;
                if (manager != null) manager.OnPlayerActionSelected(actor, action);
            });
    }
    #endregion

    #region [ UI Animations (Slide & Sync) ]
    private void SlideMenuUp()
    {
        float slideOffsetY = ResolveMenuSlideOffsetY();

        SlideMenu(_baseMenuY + slideOffsetY, Ease.OutCubic);
        BattleUIController.Instance?.MovePartyPanelUp(slideOffsetY, _menuSlideDuration);
    }

    private void SlideMenuDown()
    {
        SlideMenu(_baseMenuY, Ease.InCubic);
        BattleUIController.Instance?.ResetPartyPanelPosition(_menuSlideDuration);
    }

    private void HighlightButton(int index)
    {
        if (_buttons == null) return;
        for (int i = 0; i < _buttons.Length; i++)
        {
            if (_buttons[i] == null) continue;
            
            Image img = _buttonImages[i];
            Kill(ref _buttonColorTweens[i]);
            Kill(ref _buttonPunchTweens[i]);
            _buttons[i].transform.localScale = Vector3.one;
            if (img == null) continue;

            if (!_buttons[i].interactable)
            {
                img.color = _disabledColor;
            }
            else if (i == index)
            {
                _buttonColorTweens[i] = TweenButtonColor(img, _selectedColor);
                _buttonPunchTweens[i] = _buttons[i].transform.DOPunchScale(Vector3.one * _bouncePunch, 0.3f, 8, 0.5f)
                    .SetRecyclable(false).SetLink(_buttons[i].gameObject, LinkBehaviour.KillOnDisable);
            }
            else
            {
                _buttonColorTweens[i] = TweenButtonColor(img, _normalColor);
            }
        }
    }

    private static Tween TweenButtonColor(Image image, Color color)
    {
        return DOTween.To(() => image != null ? image.color : color,
                value => { if (image != null) image.color = value; }, color, 0.1f)
            .SetTarget(image).SetRecyclable(false).SetLink(image.gameObject, LinkBehaviour.KillOnDisable);
    }

    private void SlideMenu(float targetY, Ease ease)
    {
        Kill(ref _menuMoveTween);
        if (_rectTransform == null) return;
        _menuMoveTween = _rectTransform.DOAnchorPosY(targetY, _menuSlideDuration).SetEase(ease)
            .SetRecyclable(false).SetLink(gameObject, LinkBehaviour.KillOnDisable);
    }

    private float ResolveMenuSlideOffsetY()
    {
        return _menuSlideOffsetY + _menuSlideExtraOffsetY;
    }

    
    #endregion
}
