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

    private void Awake()
    {
        GameConfigManager.EnsureInstance();

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

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

    private void Update()
    {
        if (_isLocked || EventSystem.current == null) return;
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

        if (GameInput.UISubmitPressed)
        {
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
        _isLocked = true; 
        AudioManager.Instance?.PlaySFX(_confirmSFX);

        if (textTarget != null)
        {
            textTarget.DOFade(0f, 0.15f).SetLoops(2, LoopType.Yoyo).OnComplete(() => onCompleteAction?.Invoke());
        }
        else
        {
            onCompleteAction?.Invoke();
        }
    }
}