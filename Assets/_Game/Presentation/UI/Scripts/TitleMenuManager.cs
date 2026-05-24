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

    [Header("오디오 세팅")]
    [SerializeField] private AudioClip _titleBGM;
    [SerializeField] private AudioClip _moveSFX;
    [SerializeField] private AudioClip _confirmSFX;

    [Header("Config")]
    [SerializeField] private ConfigPanelUI _configPanel;

    private GameObject _lastSelected;
    private bool _isLocked = false;
    private int _manualSubmitFrame = -1;
    private EventSystem _lockedEventSystem;
    private GameObject _lockedSelectedObject;
    private bool _restoreNavigationEvents;

    private void Awake()
    {
        GameConfigManager.EnsureInstance();
        UIRuntimeGuard.NormalizeCanvas(gameObject);

        #if UNITY_EDITOR
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        #else
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        #endif

        bool hasSaveData = PlayerPrefs.HasKey("SaveFileExists");

        if (!hasSaveData && _btnContinue != null)
        {
            _btnContinue.gameObject.SetActive(false);
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
        if (_isLocked)
            UnlockTitleInput();
    }

    private void Update()
    {
        if (_isLocked)
        {
            KeepLockedSelection();
            return;
        }

        if (EventSystem.current == null) return;
        if (_configPanel != null && _configPanel.IsVisible) return;

        if (EventSystem.current.currentSelectedGameObject == null && _lastSelected != null)
        {
            EventSystem.current.SetSelectedGameObject(_lastSelected);
        }
        else if (EventSystem.current.currentSelectedGameObject != null)
        {
            if (_lastSelected != EventSystem.current.currentSelectedGameObject)
            {
                AudioManager.Instance?.PlaySFX(_moveSFX);
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
                if (selectedBtn != null)
                {
                    selectedBtn.onClick.Invoke(); 
                }
            }
        }
    }

    public void OnClickNewGame(TextMeshProUGUI buttonText)
    {
        ExecuteWithBlink(buttonText, () => {
            SceneLoader.Instance?.LoadScene(_newGameSceneName);
        });
    }

    public void OnClickContinue(TextMeshProUGUI buttonText)
    {
        ExecuteWithBlink(buttonText, () => {
            _isLocked = false; 
        });
    }

    public void OnClickSettings(TextMeshProUGUI buttonText)
    {
        ExecuteWithBlink(buttonText, () => {
            OpenConfig();
        });
    }

    private void OpenConfig()
    {
        _isLocked = false;
        OptionsPanelService.Open();
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
        if (_isLocked) return;
        LockTitleInput();
        AudioManager.Instance?.PlaySFX(_confirmSFX);

        System.Action complete = () =>
        {
            UnlockTitleInput();
            onCompleteAction?.Invoke();
        };

        if (textTarget != null)
        {
            textTarget.DOFade(0f, 0.15f).SetLoops(2, LoopType.Yoyo).OnComplete(() => complete());
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
}
