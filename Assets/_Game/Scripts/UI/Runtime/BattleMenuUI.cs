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
    // 기존 프리팹 직렬화 호환용. 메뉴는 이제 고정 배치라 슬라이드 값은 사용하지 않습니다.
#pragma warning disable CS0414
    [SerializeField, HideInInspector] private float _menuSlideOffsetY = 150f;
    [SerializeField, HideInInspector] private float _menuSlideExtraOffsetY = 50f;
#pragma warning restore CS0414
    [FoldoutGroup("표시 설정"), LabelText("입력 복귀 대기(초)"), Min(0f)]
    [SerializeField] private float _menuSlideDuration = 0.25f;

    [BoxGroup("대화 전환"), LabelText("전환 시간(초)"), Range(0f, 0.4f)]
    [SerializeField] private float _dialogueSlideDuration = 0.3f;
    [BoxGroup("대화 전환"), LabelText("아래 이동 거리"), Min(0f)]
    [SerializeField] private float _dialogueSlideDistance = 164f;

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
    private bool _dialogueMode;
    private bool _dialogueMenuWasActive;
    private bool _dialogueDetailsWereActive;
    private bool _resumeAfterHiddenDialogue;
    private float _dialogueMenuAlpha;
    private int _presentationRevision, _dialogueRevision;
    private BattleHudEntryMotion _dialogueMotion;
    private Tween _dialogueTween;
    private float _dialogueProgress;
    private RectTransform _dialogueBoundsSpace;
    private Rect _dialogueBounds;
    private readonly Vector3[] _dialogueCorners = new Vector3[4];
    
    private Button[] _buttons;
    private PlayerMenuAction[] _mappedActions;
    private TMPro.TMP_Text[] _buttonLabels;
    private int _inputEnabledFrame = -1;
    private bool _initialized;
    public bool CommandsEnabled => _inputEnabled && !_isExternallySuspended && !_dialogueMode && _dialogueMotion == null;
    public bool IsDialoguePresentationActive => _dialogueMode;
    public float DialogueTransitionDuration => Mathf.Clamp(_dialogueSlideDuration, 0f, .4f);
    public event Action<bool> DialoguePresentationChanged;
    // 프리팹에서 상세 목록/설명은 메인 메뉴의 자식이 아닌 형제입니다.
    public RectTransform SubMenuRoot => _subMenu != null ? _subMenu.transform as RectTransform : null;
    public void SetStatusPreview(CharacterBase actor) => _subMenu?.SetStatusPreview(actor);

    private RectTransform _rectTransform;
    private float _baseMenuY;
    private Image[] _buttonImages;
    private Tween[] _buttonColorTweens;
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
                _buttons[i].onClick.AddListener(() => HandleMainPointerClick(index));
            }
        }
        _subMenu?.SetPreviewPointerInput(CanAcceptSubMenuPointer, ConfirmSubMenuPointer);
    }
    #endregion

    #region [ Lifecycle & State ]
    public override void Hide()
    {
        _resumeAfterHiddenDialogue = false;
        _presentationRevision++;
        SetDetailsVisible(false);
        StopOwnedAnimations();
        base.Hide();
    }

    public override void HideImmediate()
    {
        _resumeAfterHiddenDialogue = false;
        _presentationRevision++;
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
        _subMenu?.SetPreviewPointerInput(null, null);
        StopOwnedAnimations();
        base.OnDestroy();
    }

    private void StopOwnedAnimations()
    {
        StopDialogueMotion();
        if (_dialogueMode)
        {
            _dialogueMode = false;
            DialoguePresentationChanged?.Invoke(false);
        }
        _inputEnabled = false;
        _subMenu?.ForceCloseImmediate();
        Kill(ref _resumeInputTween);
        if (_rectTransform != null)
            _rectTransform.anchoredPosition = new Vector2(_rectTransform.anchoredPosition.x, _baseMenuY);
        if (_buttons == null) return;
        for (int i = 0; i < _buttons.Length; i++)
        {
            Kill(ref _buttonColorTweens[i]);
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
        bool resumeHiddenDialogue = !_dialogueMode && _resumeAfterHiddenDialogue;
        _resumeAfterHiddenDialogue = false;
        _presentationRevision++;
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
        if (_dialogueMode || _dialogueMotion != null)
        {
            _dialogueMenuAlpha = _canvasGroup.alpha;
            _dialogueMenuWasActive = true;
            _dialogueMotion?.SetProgress(_dialogueProgress);
        }
        if (resumeHiddenDialogue && _dialogueMotion == null)
        {
            _subMenu?.SetPresentationInputBlocked(true);
            _dialogueMotion = new BattleHudEntryMotion(Mathf.Max(0f, _dialogueSlideDistance), _rectTransform, SubMenuRoot);
            _dialogueProgress = 0f;
            AnimateDialogueMotion(1f, StopDialogueMotion);
        }
        HighlightButton(_selectedIndex);
    }

    // 대본/턴은 소유하지 않습니다. 목록과 선택은 유지한 채 표시와 명령만 잠급니다.
    public bool BeginDialoguePresentation()
    {
        if (_dialogueMode || _isExternallySuspended || SubMenuRoot == null) return false;
        if (!_initialized) Awake();
        StopDialogueMotion();
        _resumeAfterHiddenDialogue = false;
        Canvas.ForceUpdateCanvases();
        CaptureDialogueBounds();
        _dialogueMenuWasActive = gameObject.activeSelf;
        _dialogueDetailsWereActive = SubMenuRoot.gameObject.activeSelf;
        _dialogueMenuAlpha = _canvasGroup.alpha;
        _dialogueRevision = _presentationRevision;
        _dialogueMode = true;
        // 시나리오가 먼저 감췄어도 중앙 두 메뉴의 퇴장 연출은 표시합니다.
        // 카메라/시나리오 모드는 바꾸지 않고 종료 때 원래 표시 계약으로 돌아갑니다.
        base.ShowImmediate();
        SubMenuRoot.gameObject.SetActive(true);
        _canvasGroup.interactable = _canvasGroup.blocksRaycasts = false;
        _subMenu.SetPresentationInputBlocked(true);
        _dialogueMotion = new BattleHudEntryMotion(Mathf.Max(0f, _dialogueSlideDistance), _rectTransform, SubMenuRoot);
        _dialogueProgress = 1f;
        _dialogueMotion.SetProgress(_dialogueProgress);
        AnimateDialogueMotion(0f, null);
        HighlightButton(_selectedIndex);
        DialoguePresentationChanged?.Invoke(true);
        return true;
    }

    public void EndDialoguePresentation()
    {
        if (!_dialogueMode) return;
        _dialogueMode = false;
        bool restoreVisible = _dialogueMenuWasActive && isActiveAndEnabled && !_isExternallySuspended;
        // 아직 cinematic gate가 소유 중이면, gate 해제의 다음 메뉴 표시에서 복귀합니다.
        _resumeAfterHiddenDialogue = !restoreVisible && isActiveAndEnabled && !_isExternallySuspended;
        if (restoreVisible) AnimateDialogueMotion(1f, FinishDialoguePresentation);
        else FinishDialoguePresentation();
        DialoguePresentationChanged?.Invoke(false);
    }

    private void FinishDialoguePresentation()
    {
        StopDialogueMotion();
        if (_canvasGroup != null && (_presentationRevision == _dialogueRevision || isActiveAndEnabled))
        {
            _canvasGroup.alpha = _dialogueMenuAlpha;
            if (_presentationRevision == _dialogueRevision && !_dialogueDetailsWereActive && SubMenuRoot != null)
                SubMenuRoot.gameObject.SetActive(false);
            if (_presentationRevision == _dialogueRevision && !_dialogueMenuWasActive) gameObject.SetActive(false);
        }
        if (isActiveAndEnabled) HighlightButton(_selectedIndex);
    }

    private void AnimateDialogueMotion(float target, Action completed)
    {
        Kill(ref _dialogueTween);
        if (_dialogueMotion == null) { completed?.Invoke(); return; }
        if (!Application.isPlaying || DialogueTransitionDuration <= 0f)
        {
            _dialogueProgress = target;
            _dialogueMotion.SetProgress(target);
            completed?.Invoke();
            return;
        }
        _dialogueTween = DOTween.To(() => _dialogueProgress, value =>
            {
                _dialogueProgress = value;
                _dialogueMotion?.SetProgress(value);
            }, target, DialogueTransitionDuration)
            .SetEase(target > 0f ? Ease.OutCubic : Ease.InQuad).SetUpdate(true)
            .SetRecyclable(false).SetLink(gameObject, LinkBehaviour.KillOnDisable)
            .OnComplete(() => { _dialogueTween = null; completed?.Invoke(); });
    }

    private void StopDialogueMotion()
    {
        Kill(ref _dialogueTween);
        BattleHudEntryMotion owned = _dialogueMotion;
        _dialogueMotion = null;
        owned?.Dispose();
        _subMenu?.SetPresentationInputBlocked(false);
        _inputEnabledFrame = Time.frameCount;
    }

    private void CaptureDialogueBounds()
    {
        _dialogueBoundsSpace = _rectTransform.parent as RectTransform;
        if (_dialogueBoundsSpace == null) _dialogueBoundsSpace = _rectTransform;
        Vector2 min = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
        Vector2 max = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
        IncludeDialogueBounds(_rectTransform, ref min, ref max);
        IncludeDialogueBounds(SubMenuRoot, ref min, ref max);
        _dialogueBounds = Rect.MinMaxRect(min.x, min.y, max.x, max.y);
    }

    private void IncludeDialogueBounds(RectTransform rect, ref Vector2 min, ref Vector2 max)
    {
        rect.GetWorldCorners(_dialogueCorners);
        for (int i = 0; i < 4; i++)
        {
            Vector2 local = _dialogueBoundsSpace.InverseTransformPoint(_dialogueCorners[i]);
            min = Vector2.Min(min, local);
            max = Vector2.Max(max, local);
        }
    }

    // 이동 전 명령+상세 합집합입니다. 대사창은 내려가는 메뉴를 따라가지 않습니다.
    public bool TryGetDialogueWorldCorners(Vector3[] corners)
    {
        if (!_dialogueMode || _dialogueBoundsSpace == null || corners == null || corners.Length < 4) return false;
        corners[0] = _dialogueBoundsSpace.TransformPoint(new Vector3(_dialogueBounds.xMin, _dialogueBounds.yMin));
        corners[1] = _dialogueBoundsSpace.TransformPoint(new Vector3(_dialogueBounds.xMin, _dialogueBounds.yMax));
        corners[2] = _dialogueBoundsSpace.TransformPoint(new Vector3(_dialogueBounds.xMax, _dialogueBounds.yMax));
        corners[3] = _dialogueBoundsSpace.TransformPoint(new Vector3(_dialogueBounds.xMax, _dialogueBounds.yMin));
        return true;
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
            _rectTransform.anchoredPosition = new Vector2(_rectTransform.anchoredPosition.x, _baseMenuY);
        }
    }
    #endregion

    #region [ Input & Navigation ]
    private void Update()
    {
        if (!CommandsEnabled) return;
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
        if (Time.frameCount <= _inputEnabledFrame || GameInput.IsBattleInputBlocked || GameInput.BattleUIInputConsumed) return;
        if (!CommandsEnabled || !isActiveAndEnabled || !IsVisible
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
        {
            _subMenu?.PlayDeniedFeedback();
            return;
        }
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

    private void HandleMainPointerClick(int index)
    {
        if (!CanAcceptSubMenuPointer() || _buttons == null || index < 0 || index >= _buttons.Length
            || _buttons[index] == null || !_buttons[index].interactable) return;
        if (index != _selectedIndex)
        {
            // 다른 탭의 이전 상세 항목을 새 명령으로 잘못 확정하지 않습니다.
            GameInput.ConsumeBattleUIInput();
            _selectedIndex = index;
            HighlightButton(index);
            RefreshPreview();
            PlayMoveSfx();
            return;
        }
        Confirm(index);
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
        if (entry is EmptyMenuEntry)
        {
            ResumeInputAfterSlide();
            return;
        }
        BattleManager manager = BattleManager.Instance;
        if (manager != null && entry is ItemMenuEntry itemEntry)
            manager.OnSubMenuActionSelected(_currentActor, PlayerMenuAction.Item, null, itemEntry.Data);
    }

    // Confirm은 재입력을 막기 위해 먼저 _inputEnabled를 끕니다.
    // 이미 확정된 요청의 전달을 그 입력 잠금으로 다시 거부하면 대상 선택까지 도달하지 못합니다.
    private bool CanReceiveSubMenuCallback() => this != null && isActiveAndEnabled
        && !_isExternallySuspended && !_dialogueMode && _dialogueMotion == null
        && !GameInput.IsBattleInputBlocked;

    private bool CanAcceptSubMenuPointer() => CommandsEnabled && isActiveAndEnabled && IsVisible
        && Time.frameCount > _inputEnabledFrame && !GameInput.IsBattleInputBlocked
        && !GameInput.BattleUIInputConsumed
        && (BattleUIController.Instance == null || !BattleUIController.Instance.IsNarrationBlockingInput());

    private void ConfirmSubMenuPointer() => Confirm(_selectedIndex);

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
    private void HighlightButton(int index)
    {
        if (_buttons == null) return;
        for (int i = 0; i < _buttons.Length; i++)
        {
            if (_buttons[i] == null) continue;
            
            Image img = _buttonImages[i];
            Kill(ref _buttonColorTweens[i]);
            _buttons[i].transform.localScale = Vector3.one;
            if (_buttonLabels != null && _buttonLabels[i] != null)
                _buttonLabels[i].color = _dialogueMode || !_buttons[i].interactable ? _disabledColor
                    : i == index ? _selectedColor : Color.white;
            if (img == null) continue;

            if (_dialogueMode || !_buttons[i].interactable)
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
                value => { if (image != null) image.color = value; }, color, 0.1f).SetUpdate(true)
            .SetTarget(image).SetRecyclable(false).SetLink(image.gameObject, LinkBehaviour.KillOnDisable);
    }

    
    #endregion
}
