using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using DG.Tweening;
using TMPro;

public class TitleMenuManager : MonoBehaviour
{
    [Header("버튼 참조")]
    [Tooltip("시작하자마자 기본으로 선택되어 있을 버튼 (새 게임)")]
    [SerializeField] private Button _firstSelectButton; 
    [Tooltip("세이브 데이터가 없을 때 비활성화할 계속하기 버튼")]
    [SerializeField] private Button _btnContinue;
    
    [Header("씬 이동 세팅")]
    [SerializeField] private string _newGameSceneName = "01_IntroScene";
    [Tooltip("-1이면 가장 최근 슬롯, 0 이상이면 해당 슬롯을 불러옵니다.")]
    [SerializeField] private int _continueSlot = -1;

    [Header("오디오 세팅")]
    [SerializeField] private AudioClip _titleBGM;
    [SerializeField] private AudioClip _moveSFX;
    [SerializeField] private AudioClip _confirmSFX;

    [Header("Config")]
    [SerializeField] private ConfigPanelUI _configPanel;

    [Header("메뉴 표시")]
    [Tooltip("타이틀 이미지가 아닌 버튼 목록만 연결합니다. 옵션을 여는 동안 숨깁니다.")]
    [SerializeField] private CanvasGroup _menuGroup;

    private GameObject _lastSelected;
    private bool _isLocked = false;
    private int _manualSubmitFrame = -1;
    private EventSystem _lockedEventSystem;
    private GameObject _lockedSelectedObject;
    private bool _restoreNavigationEvents;
    private bool _optionsCoveringMenu;
    private int _resumeInputAfterFrame = -1;
    private Tween _confirmTween;
    private TextMeshProUGUI _confirmLabel;
    private float _confirmAlpha;
    private float _menuAlpha = 1f;
    private bool _menuInteractable = true;
    private bool _menuBlocksRaycasts = true;

    private void Awake()
    {
        GameConfigManager.EnsureInstance();
        UIRuntimeGuard.NormalizeCanvas(gameObject);
        if (_menuGroup == null && _firstSelectButton != null && _firstSelectButton.transform.parent != null)
        {
            GameObject menu = _firstSelectButton.transform.parent.gameObject;
            _menuGroup = menu.GetComponent<CanvasGroup>();
            if (_menuGroup == null) _menuGroup = menu.AddComponent<CanvasGroup>();
        }
        if (_menuGroup != null)
        {
            _menuAlpha = _menuGroup.alpha;
            _menuInteractable = _menuGroup.interactable;
            _menuBlocksRaycasts = _menuGroup.blocksRaycasts;
        }

        #if UNITY_EDITOR
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        #else
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        #endif

        bool hasSaveData = SaveManager.HasAnySave();

        if (_btnContinue != null)
        {
            // 메뉴 항목 수를 유지하여 세이브 유무에 따라 다른 버튼이 재배치되지 않게 합니다.
            _btnContinue.gameObject.SetActive(true);
            SetContinueInteractable(hasSaveData);
        }
    }

    private void Start()
    {
        EnsureConfigPanel();

        if (_titleBGM != null)
        {
            AudioManager.Instance?.CrossFadeBGM(_titleBGM, 1.0f);
        }

        if (_firstSelectButton != null)
        {
            _firstSelectButton.Select();
            _lastSelected = _firstSelectButton.gameObject;
        }
    }

    private void OnDisable()
    {
        CancelConfirmation();
        if (_isLocked)
            UnlockTitleInput();
        SetOptionsCover(false, false);
    }

    private void Update()
    {
        SyncOptionsCover();
        if (_optionsCoveringMenu || Time.frameCount <= _resumeInputAfterFrame) return;

        if (_isLocked)
        {
            KeepLockedSelection();
            return;
        }

        if (EventSystem.current == null) return;

        if (EventSystem.current.currentSelectedGameObject == null && _lastSelected != null)
        {
            EventSystem.current.SetSelectedGameObject(_lastSelected);
        }
        else if (IsTitleButton(EventSystem.current.currentSelectedGameObject))
        {
            if (_lastSelected != EventSystem.current.currentSelectedGameObject)
            {
                AudioManager.Instance?.PlayUISFX(_moveSFX);
                _lastSelected = EventSystem.current.currentSelectedGameObject;
            }
        }

        if (GameInput.UISubmitPressed && _manualSubmitFrame != Time.frameCount)
        {
            _manualSubmitFrame = Time.frameCount;
            GameObject currentSelected = EventSystem.current.currentSelectedGameObject;
            if (currentSelected != null)
            {
                Button selectedBtn = currentSelected.GetComponent<Button>();
                if (selectedBtn != null && IsTitleButton(currentSelected) && selectedBtn.IsInteractable())
                {
                    selectedBtn.onClick.Invoke(); 
                }
            }
        }
    }

    public void OnClickNewGame(TextMeshProUGUI buttonText)
    {
        ExecuteWithBlink(buttonText, () =>
        {
            GlobalDataManager.Instance?.ResetForNewGame();
            Time.timeScale = 1f;
            SceneLoader.Instance?.LoadScene(_newGameSceneName);
        });
    }

    public void OnClickContinue(TextMeshProUGUI buttonText)
    {
        ExecuteWithBlink(buttonText, BeginContinue);
    }

    public void SetContinueSlot(int slotIndex)
    {
        _continueSlot = slotIndex;
    }

    private void BeginContinue()
    {
        LockTitleInput();
        SetContinueInteractable(false);

        GameLoadStartResult result = _continueSlot >= 0
            ? GameLoadCoordinator.LoadSlot(_continueSlot, HandleContinueCompleted)
            : GameLoadCoordinator.LoadMostRecent(HandleContinueCompleted);
        if (result.Accepted)
            return;

        Debug.LogError("[TitleMenuManager] Continue 실패: " + result.Message, this);
        RestoreContinueInput();
    }

    private void HandleContinueCompleted(SceneLoadResult result)
    {
        if (SceneLoadResultUtility.WasDestinationActivated(result))
            return;

        Debug.LogError("[TitleMenuManager] Continue Scene 이동 실패: " + result, this);
        RestoreContinueInput();
    }

    private void RestoreContinueInput()
    {
        if (_btnContinue != null)
        {
            SetContinueInteractable(true);
            _btnContinue.Select();
            _lastSelected = _btnContinue.gameObject;
        }

        UnlockTitleInput();
    }

    public void OnClickSettings(TextMeshProUGUI buttonText)
    {
        ExecuteWithBlink(buttonText, () => {
            OpenConfig();
        });
    }

    private void OpenConfig()
    {
        EnsureConfigPanel();
        if (_configPanel == null) return;
        OptionsPanelService.Open();
        // Open이 실패해도 반환되므로 실제로 열린 경우에만 메뉴를 숨깁니다.
        SyncOptionsCover();
    }

    private void EnsureConfigPanel()
    {
        _configPanel = OptionsPanelService.EnsurePanel();
    }

    public void OnClickQuit(TextMeshProUGUI buttonText)
    {
        ExecuteWithBlink(buttonText, () => {
            Application.Quit();
            #if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
            #endif
        });
    }

    private void ExecuteWithBlink(TextMeshProUGUI textTarget, System.Action onCompleteAction)
    {
        if (_isLocked || _optionsCoveringMenu || Time.frameCount <= _resumeInputAfterFrame
            || (_configPanel != null && _configPanel.gameObject.activeInHierarchy)) return;
        CancelConfirmation();
        LockTitleInput();
        AudioManager.Instance?.PlayUISFX(_confirmSFX);

        System.Action complete = () =>
        {
            _confirmTween = null;
            RestoreConfirmationLabel();
            UnlockTitleInput();
            if (isActiveAndEnabled) onCompleteAction?.Invoke();
        };

        if (textTarget != null)
        {
            _confirmLabel = textTarget;
            _confirmAlpha = textTarget.alpha;
            _confirmTween = textTarget.DOFade(_confirmAlpha * 0.45f, 0.07f)
                .SetLoops(2, LoopType.Yoyo).SetUpdate(true).OnComplete(() => complete());
        }
        else
        {
            complete();
        }
    }

    private void LockTitleInput()
    {
        _isLocked = true;
        _lockedEventSystem = EventSystem.current;
        _lockedSelectedObject = _lockedEventSystem != null ? _lockedEventSystem.currentSelectedGameObject : null;

        if (_lockedEventSystem != null)
        {
            _restoreNavigationEvents = _lockedEventSystem.sendNavigationEvents;
            _lockedEventSystem.sendNavigationEvents = false;
        }
    }

    private void UnlockTitleInput()
    {
        if (_lockedEventSystem != null)
        {
            _lockedEventSystem.sendNavigationEvents = _restoreNavigationEvents;
        }

        _lockedEventSystem = null;
        _lockedSelectedObject = null;
        _isLocked = false;
    }

    private void KeepLockedSelection()
    {
        EventSystem eventSystem = EventSystem.current;
        if (eventSystem == null || _lockedSelectedObject == null) return;
        if (eventSystem.currentSelectedGameObject != _lockedSelectedObject)
            eventSystem.SetSelectedGameObject(_lockedSelectedObject);
    }

    private void SyncOptionsCover()
    {
        // alpha는 표시 tween 첫 프레임에 0입니다. 닫기 tween까지 포함한 활성 수명을 사용합니다.
        bool covered = _configPanel != null && _configPanel.gameObject.activeInHierarchy;
        if (covered == _optionsCoveringMenu) return;
        if (covered)
        {
            CancelConfirmation();
            if (_isLocked) UnlockTitleInput();
        }
        SetOptionsCover(covered, true);
    }

    private void SetOptionsCover(bool covered, bool restoreSelection)
    {
        _optionsCoveringMenu = covered;
        if (_menuGroup != null)
        {
            _menuGroup.alpha = covered ? 0f : _menuAlpha;
            _menuGroup.interactable = !covered && _menuInteractable;
            _menuGroup.blocksRaycasts = !covered && _menuBlocksRaycasts;
        }

        EventSystem eventSystem = EventSystem.current;
        if (covered)
        {
            if (eventSystem != null && IsTitleButton(eventSystem.currentSelectedGameObject))
            {
                _lastSelected = eventSystem.currentSelectedGameObject;
                eventSystem.SetSelectedGameObject(null);
            }
            return;
        }
        if (!restoreSelection) return;
        _resumeInputAfterFrame = Time.frameCount + 1;
        if (eventSystem == null) return;
        Button previous = _lastSelected != null ? _lastSelected.GetComponent<Button>() : null;
        Button target = previous != null && IsTitleButton(previous.gameObject)
            && previous.IsActive() && previous.IsInteractable() ? previous : _firstSelectButton;
        if (target != null && target.IsActive() && target.IsInteractable())
        {
            _lastSelected = target.gameObject;
            eventSystem.SetSelectedGameObject(_lastSelected);
        }
    }

    private bool IsTitleButton(GameObject candidate) => candidate != null
        && _menuGroup != null && candidate.transform.IsChildOf(_menuGroup.transform);

    private void SetContinueInteractable(bool interactable)
    {
        if (_btnContinue == null) return;
        _btnContinue.interactable = interactable;
        _btnContinue.GetComponent<MenuButtonAnimator>()?.RefreshVisual(true);
    }

    private void CancelConfirmation()
    {
        _confirmTween?.Kill(false);
        _confirmTween = null;
        RestoreConfirmationLabel();
    }

    private void RestoreConfirmationLabel()
    {
        if (_confirmLabel != null) _confirmLabel.alpha = _confirmAlpha;
        _confirmLabel = null;
    }
}
