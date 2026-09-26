using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;
using Sirenix.OdinInspector;

/// <summary>
/// 플레이어 턴에 표시되는 주 메뉴(Attack, Act, Item, Run)를 제어합니다.
/// 메뉴/상세 영역은 고정 표시하고, 입력 가능 여부만 전투 상태에 따라 바꿉니다.
/// </summary>
public class BattleMenuUI : UIPanel 
{
    #region [ UI Components ]
    [BoxGroup("Buttons"), LabelWidth(100)] [SerializeField] private Button _attackBtn;
    [BoxGroup("Buttons"), LabelWidth(100)] [SerializeField] private Button _actBtn; 
    [BoxGroup("Buttons"), LabelWidth(100)] [SerializeField] private Button _itemBtn;
    [BoxGroup("Buttons"), LabelWidth(100)] [SerializeField] private Button _runBtn;

    [BoxGroup("Sub Menu"), LabelWidth(100)] [SerializeField] private BattleSubMenu _subMenu;
    [BoxGroup("효과음")] [SerializeField] private AudioClip _moveSfx;
    [BoxGroup("효과음")] [SerializeField] private AudioClip _confirmSfx;
    [BoxGroup("효과음")] [SerializeField] private AudioClip _cancelSfx;
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
    #endregion

    #region [ Example / Default Data ]
    [FoldoutGroup("Fallback Data"), LabelWidth(140)] 
    [SerializeField] private List<SkillData> _exampleSkills = new List<SkillData>();
    [FoldoutGroup("Fallback Data"), LabelWidth(140)] 
    [SerializeField] private List<ItemData> _exampleItems = new List<ItemData>();
    #endregion

    #region [ Internal State ]
    private int _selectedIndex = 0;
    private readonly int[] _selectionByMenu = new int[4];
    private PlayerCharacter _currentActor;
    private bool _inputEnabled = false;
    private bool _isExternallySuspended = false;
    
    private Button[] _buttons;
    private PlayerMenuAction[] _mappedActions;
    private TMPro.TMP_Text[] _buttonLabels;
    private int _inputEnabledFrame = -1;
    private bool _initialized;
    public bool CommandsEnabled => _inputEnabled && !_isExternallySuspended;

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
        if (_initialized) return;
        _initialized = true;
        base.Awake();
        _rectTransform = GetComponent<RectTransform>();
        _baseMenuY = _rectTransform.anchoredPosition.y;

        _buttons = new[] { _attackBtn, _actBtn, _itemBtn, _runBtn };
        _buttonImages = new Image[_buttons.Length];
        _buttonLabels = new TMPro.TMP_Text[_buttons.Length];
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
                _buttonLabels[i] = _buttons[i].GetComponentInChildren<TMPro.TMP_Text>(true);
                // 이동/확정은 GameInput이 단독 소유합니다. EventSystem 중복 확정을 차단합니다.
                _buttons[i].navigation = new Navigation { mode = Navigation.Mode.None };
                _buttons[i].onClick.AddListener(() => Confirm(index));
            }
        }
    }
    #endregion

    #region [ Lifecycle & State ]
    public override void Hide()
    {
        SetDetailsVisible(false);
        StopOwnedAnimations();
        base.Hide();
    }

    public override void HideImmediate()
    {
        SetDetailsVisible(false);
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
        _subMenu?.ForceCloseImmediate();
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
            if (_buttons[_selectedIndex] == null || !_buttons[_selectedIndex].interactable) NavigateToAvailableButton(1, false);
            else HighlightButton(_selectedIndex);
        }
    }

    public void SetActor(PlayerCharacter actor)
    {
        if (_currentActor != actor)
        {
            _selectedIndex = 0;
            Array.Clear(_selectionByMenu, 0, _selectionByMenu.Length);
        }
        _currentActor = actor;
        RefreshPreview();
    }

    public override void Show() => SetCommandInputEnabled(true);

    public void SetCommandInputEnabled(bool enabled)
    {
        if (_isExternallySuspended) return;
        // 비활성 프리팹에 대한 첫 호출도 UIPanel.Awake의 자동 숨김 이후에 표시합니다.
        if (!_initialized) Awake();
        Kill(ref _resumeInputTween);
        ShowImmediate();
        _canvasGroup.interactable = false;
        _canvasGroup.blocksRaycasts = false;
        _inputEnabled = enabled;
        _inputEnabledFrame = Time.frameCount;
        if (!enabled) _subMenu?.ForceCloseImmediate();
        else RefreshPreview();
        HighlightButton(_selectedIndex);
    }

    public void SetDetailsVisible(bool visible)
    {
        if (!visible) _subMenu?.ForceCloseImmediate();
        if (_subMenu != null) _subMenu.gameObject.SetActive(visible);
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
        if (!_inputEnabled || !IsVisible || Time.frameCount <= _inputEnabledFrame || GameInput.BattleUIInputConsumed) return;
        if (BattleUIController.Instance != null && BattleUIController.Instance.IsNarrationBlockingInput()) return;

        if (GameInput.BattleLeftPressed)
            Navigate(-1);
        else if (GameInput.BattleRightPressed)
            Navigate(1);
        else if (GameInput.BattleUpPressed)
            MoveItemSelection(-1);
        else if (GameInput.BattleDownPressed)
            MoveItemSelection(1);
        else if (GameInput.BattleConfirmPressed)
            Confirm(_selectedIndex);
        else if (GameInput.BattleCancelPressed)
        {
            GameInput.ConsumeBattleUIInput();
            if (_selectedIndex != 0) PlayCancelSfx();
            _selectedIndex = 0;
            HighlightButton(_selectedIndex);
            RefreshPreview();
        }
    }

    private void MoveItemSelection(int direction)
    {
        if (_subMenu == null) return;
        int previous = _subMenu.SelectedIndex;
        _subMenu.MoveSelection(direction);
        if (_subMenu.SelectedIndex != previous) PlayMoveSfx();
        _selectionByMenu[_selectedIndex] = _subMenu.SelectedIndex;
    }

    private void Navigate(int dir) => NavigateToAvailableButton(dir, true);

    private void NavigateToAvailableButton(int dir, bool playSound)
    {
        if (_buttons == null || _buttons.Length == 0) return;
        int previous = _selectedIndex;

        for (int i = 0; i < _buttons.Length; i++)
        {
            _selectedIndex = (_selectedIndex + dir + _buttons.Length) % _buttons.Length;
            if (_buttons[_selectedIndex] != null && _buttons[_selectedIndex].interactable) break;
        }

        HighlightButton(_selectedIndex);
        RefreshPreview();
        if (playSound && _selectedIndex != previous) PlayMoveSfx();
    }

    private void Confirm(int index)
    {
        // 버튼 onClick도 Update와 동일한 입력 경계를 통과해야 합니다.
        // 대상 확정 후 같은 프레임에 다시 열린 메뉴로 Submit이 재진입하는 것을 막습니다.
        if (Time.frameCount <= _inputEnabledFrame || GameInput.BattleUIInputConsumed) return;
        if (!_inputEnabled || _isExternallySuspended || !isActiveAndEnabled || !IsVisible
            || _buttons == null || index < 0 || index >= _buttons.Length || _buttons[index] == null)
            return;
        if (BattleUIController.Instance != null && BattleUIController.Instance.IsNarrationBlockingInput()) return;
        if (!_buttons[index].interactable) return; 

        GameInput.ConsumeBattleUIInput();
        _selectedIndex = index;
        HighlightButton(index);
        var action = _mappedActions[index]; 

        IMenuEntry entry = null;
        if ((action == PlayerMenuAction.Skill || action == PlayerMenuAction.Item)
            && (_subMenu == null || !_subMenu.TryGetSelectedEntry(out entry)))
            return;
        if (_subMenu != null) _selectionByMenu[index] = _subMenu.SelectedIndex;
        _inputEnabled = false;
        PlayConfirmSfx();

        // 목록에서 Z를 누르면 기존 대상 선택으로 바로 전달합니다.
        if (action != PlayerMenuAction.Run)
        {
            if (_currentActor != null) _currentActor.PlayBattleAnim(PlayerCharacter.HashBattleReady);
        }

        if (action == PlayerMenuAction.Skill)
            OnSkillSelected(entry);
        else if (action == PlayerMenuAction.Item)
            OnItemSelected(entry);
        else
            ExecuteDirectAction(index, action);
    }
    #endregion

    // 대상 선택도 같은 음원을 사용합니다. 입력 수신 경로에서만 호출해 자동 갱신 소리를 막습니다.
    public void PlayMoveSfx() => AudioManager.Instance?.PlayUISFX(_moveSfx);
    public void PlayConfirmSfx() => AudioManager.Instance?.PlayUISFX(_confirmSfx);
    public void PlayCancelSfx() => AudioManager.Instance?.PlayUISFX(_cancelSfx);

    #region [ Sub Menu Controls ]
    private List<IMenuEntry> BuildSkillEntries()
    {
        var entries = new List<IMenuEntry>();
        var source = (_currentActor != null && _currentActor.Skills?.Count > 0)
            ? _currentActor.Skills : _exampleSkills;
        foreach (SkillData skill in source)
            if (skill != null) entries.Add(new SkillMenuEntry(skill));
        if (entries.Count == 0) entries.Add(new EmptyMenuEntry("NO SKILL", "등록된 스킬이 없습니다."));
        return entries;
    }

    private List<IMenuEntry> BuildItemEntries()
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
        if (entries.Count == 0) entries.Add(new EmptyMenuEntry("NO ITEM", "사용 가능한 아이템이 없습니다."));
        return entries;
    }

    private void RefreshPreview()
    {
        if (_subMenu == null || _isExternallySuspended) return;
        switch (_selectedIndex)
        {
            case 1: _subMenu.Preview("SKILL", BuildSkillEntries(), _currentActor); break;
            case 2: _subMenu.Preview("ITEM", BuildItemEntries(), _currentActor); break;
            case 3:
                _subMenu.Preview("RUN", new List<IMenuEntry> {
                    new BattleCommandPreviewEntry("RUN", _runBtn != null && !_runBtn.interactable
                        ? "이 전투에서는 도망칠 수 없습니다." : "전투에서 도망치기를 시도합니다.") }, _currentActor);
                break;
            default:
                _subMenu.Preview("ATTACK", new List<IMenuEntry> {
                    new BattleCommandPreviewEntry("ATTACK", "적에게 접근해 일반 공격을 합니다.\n\n적 1명\nAP 소모 없음") }, _currentActor);
                break;
        }
        _subMenu.SelectIndex(_selectionByMenu[_selectedIndex]);
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
        if (_currentActor == null) return;
        BattleManager manager = BattleManager.Instance;
        if (manager != null) manager.OnPlayerActionSelected(_currentActor, action);
    }
    #endregion

    #region [ UI Animations (Slide & Sync) ]
    // 기존 외부 호출/직렬화 호환. 고정 HUD는 하위 목록을 열어도 이동하지 않습니다.
    private void SlideMenuUp() { }
    private void SlideMenuDown() { }

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
            if (_buttonLabels != null && _buttonLabels[i] != null)
                _buttonLabels[i].color = !_buttons[i].interactable ? _disabledColor
                    : i == index ? _selectedColor : Color.white;
            if (img == null) continue;

            if (!_buttons[i].interactable)
            {
                img.color = _disabledColor;
            }
            else if (i == index)
            {
                _buttonColorTweens[i] = TweenButtonColor(img, _selectedColor);
                // 고정 크기: 선택/취소 때 버튼이 커졌다 작아지지 않습니다.
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
