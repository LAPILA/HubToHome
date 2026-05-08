using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
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

    private GameObject _lastSelected;
    
    // 🚨 전역 EventSystem을 끄는 대신, 타이틀 자체 입력을 막는 락(Lock) 변수
    private bool _isLocked = false;

    private void Awake()
    {
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
        if (_firstSelectButton != null)
        {
            _firstSelectButton.Select();
            _lastSelected = _firstSelectButton.gameObject;
        }
    }

    private void Update()
    {
        // 🚨 선택 연출이 재생 중이거나 씬이 넘어가는 중이면 모든 입력 무시
        if (_isLocked || EventSystem.current == null) return;

        if (EventSystem.current.currentSelectedGameObject == null && _lastSelected != null)
        {
            EventSystem.current.SetSelectedGameObject(_lastSelected);
        }
        else if (EventSystem.current.currentSelectedGameObject != null)
        {
            _lastSelected = EventSystem.current.currentSelectedGameObject;
        }

        if (Keyboard.current != null && Keyboard.current.zKey.wasPressedThisFrame)
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
            Debug.Log("새 게임 시작!");
            SceneLoader.Instance?.LoadScene(_newGameSceneName);
        });
    }

    public void OnClickContinue(TextMeshProUGUI buttonText)
    {
        ExecuteWithBlink(buttonText, () => {
            Debug.Log("게임 불러오기!");
            _isLocked = false; // 씬 전환이 안 일어나는 임시 버튼은 다시 락 해제
        });
    }

    public void OnClickSettings(TextMeshProUGUI buttonText)
    {
        ExecuteWithBlink(buttonText, () => {
            Debug.Log("설정 패널 열기!");
            _isLocked = false; 
        });
    }

    public void OnClickQuit(TextMeshProUGUI buttonText)
    {
        ExecuteWithBlink(buttonText, () => {
            Debug.Log("게임 종료!");
            Application.Quit();
            #if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
            #endif
        });
    }

    private void ExecuteWithBlink(TextMeshProUGUI textTarget, System.Action onCompleteAction)
    {
        // 🚨 EventSystem.current.enabled = false; <- 삭제!!
        _isLocked = true; // 타이틀 매니저의 Update 입력을 차단

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