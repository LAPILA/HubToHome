using UnityEngine;
using TMPro;
using UnityEngine.EventSystems; // 🚨 EventSystem 처리를 위해 필수
using DG.Tweening;
using System;
using System.Collections;

public class NameInputUI : MonoBehaviour
{
    private const int MinNameLength = 2;
    private const int MaxNameLength = 10;

    [Header("UI 참조")]
    [SerializeField] private TMP_InputField _inputField;
    [SerializeField] private TextMeshProUGUI _guideText;
    [SerializeField] private CanvasGroup _canvasGroup;

    private string[] _guides = {
        "ENTER YOUR NAME.", "이름을 입력해 주세요.", "名前を入力してください。", "请输入你的名字。"
    };

    private int _currentLangIndex = 0;
    private Action<string> _onNamingComplete;
    private bool _isOpen = false;

    private void Awake()
    {
        if (_canvasGroup != null) _canvasGroup.alpha = 0;
        if (_inputField != null) _inputField.characterLimit = MaxNameLength;
    }

    public void Open(Action<string> onComplete)
    {
        _onNamingComplete = onComplete;
        _isOpen = true;
        gameObject.SetActive(true);

        if (_inputField != null)
        {
            _inputField.characterLimit = MaxNameLength;
            _inputField.text = ""; // 이전 입력 초기화
        }
        if (_canvasGroup != null) _canvasGroup.DOFade(1f, 0.5f);
        
        UpdateLanguage(0);

        // 🚨 코루틴으로 한 프레임 쉬고 포커스를 강제로 잡습니다. (활성화 타이밍 꼬임 방지)
        StartCoroutine(ForceFocusRoutine());
    }

    private IEnumerator ForceFocusRoutine()
    {
        // UI가 완전히 켜질 때까지 딱 1프레임만 대기
        yield return null;

        // 🚨 NRE 원천 차단: EventSystem이 씬에 있는지 검사
        if (EventSystem.current == null)
        {
            Debug.LogError("🚨 [치명적 오류] 씬에 EventSystem이 없습니다! DialogueManager 프리팹 안에 EventSystem을 꼭 추가해주세요.");
            yield break; // 에러 내면서 터지는 걸 막음
        }

        if (_inputField != null)
        {
            // 1. 이벤트 시스템에서 이 인풋필드를 "현재 선택된 놈"으로 강제 지정
            EventSystem.current.SetSelectedGameObject(_inputField.gameObject);
            
            // 2. 타이핑 활성화 (커서 깜빡임 시작)
            _inputField.ActivateInputField();
            _inputField.Select();
        }
    }

    private void Update()
    {
        if (!_isOpen) return;

        // 🚨 지속적인 포커스 유지: 만약 포커스가 날아가면 강제로 다시 잡음
        if (EventSystem.current != null && EventSystem.current.currentSelectedGameObject != _inputField.gameObject)
        {
            EventSystem.current.SetSelectedGameObject(_inputField.gameObject);
        }

        HandleLanguageShortcut();

        // 이름 결정 (Enter 키)
        if (GameInput.DialogueAdvancePressed)
        {
            if (IsValidNameLength())
            {
                ConfirmName();
            }
        }
    }

    private bool IsValidNameLength()
    {
        if (_inputField == null) return false;

        string trimmedName = _inputField.text.Trim();
        return trimmedName.Length >= MinNameLength && trimmedName.Length <= MaxNameLength;
    }

    private void HandleLanguageShortcut()
    {
        if (GameInput.LanguageKRPressed) SetLanguage(LanguageType.KR, 0);
        else if (GameInput.LanguageENPressed) SetLanguage(LanguageType.EN, 1);
        else if (GameInput.LanguageJPPressed) SetLanguage(LanguageType.JP, 2);
        else if (GameInput.LanguageCNPressed) SetLanguage(LanguageType.CN, 3);
    }

    private void SetLanguage(LanguageType language, int guideIndex)
    {
        _currentLangIndex = Mathf.Clamp(guideIndex, 0, _guides.Length - 1);
        GameConfigManager.EnsureInstance().SetLanguage(language);
        UpdateLanguage(_currentLangIndex);
    }

    private void UpdateLanguage(int index)
    {
        if (_guideText != null)
        {
            _guideText.text = _guides[index];
            _guideText.transform.DOPunchScale(Vector3.one * 0.1f, 0.2f);
        }
    }

    private void ConfirmName()
    {
        if (!IsValidNameLength()) return;

        _isOpen = false;
        string confirmedName = _inputField.text.Trim();
        
        if (_inputField != null) _inputField.DeactivateInputField();
        
        if (_canvasGroup != null)
        {
            _canvasGroup.DOFade(0f, 1f).OnComplete(() => {
                _onNamingComplete?.Invoke(confirmedName);
                gameObject.SetActive(false);
            });
        }
    }
}