using UnityEngine;
using UnityEngine.InputSystem;
using DG.Tweening;
using Sirenix.OdinInspector;
using Febucci.TextAnimatorCore;
using Febucci.TextAnimatorForUnity;
using Febucci.TextAnimatorCore.Typing;

/// <summary>
/// 대화 UI 컨트롤러.
/// TextAnimator(Febucci)로 타이핑 효과를 처리하고,
/// DialogueManager의 이벤트를 받아 UI를 갱신합니다.
/// 
/// Hierarchy 구조:
/// DialoguePanel (UIPanel + CanvasGroup)
///   ├── SpeakerName   (TextMeshProUGUI)
///   ├── Portrait      (Image)
///   ├── DialogueBox   (Image)
///   │     └── DialogueText (TextAnimatorPlayer + TypewriterByCharacter)
///   ├── AdvanceIcon   (GameObject — 깜빡이는 ▼ 아이콘)
///   └── ChoiceGroup   (GameObject)
///         └── ChoiceButton (Prefab — 동적 생성)
/// 
/// 사용법:
/// - DialogueManager.OnLineStarted 이벤트에 자동 구독
/// - Z키 입력 → 타이핑 중이면 스킵, 완료면 AdvanceLine
/// </summary>
public class DialogueController : UIPanel
{
    // ── UI 참조 ───────────────────────────────────────────────
    [BoxGroup("UI References"), LabelWidth(120)]
    [SerializeField] private TMPro.TextMeshProUGUI _speakerNameText;

    [BoxGroup("UI References"), LabelWidth(120)]
    [SerializeField] private UnityEngine.UI.Image _portraitImage;

    [BoxGroup("UI References"), LabelWidth(120)]
    [SerializeField] private TypewriterCore _textAnimatorPlayer;

    [BoxGroup("UI References"), LabelWidth(120)]
    [SerializeField] private GameObject _advanceIcon;

    [BoxGroup("UI References"), LabelWidth(120)]
    [SerializeField] private Transform _choiceContainer;

    [BoxGroup("UI References"), LabelWidth(120)]
    [SerializeField] private GameObject _choiceButtonPrefab;

    // ── 타이핑 설정 ───────────────────────────────────────────
    [FoldoutGroup("Typing Settings"), LabelWidth(140)]
    [Tooltip("기본 타이핑 속도 (초/글자)")]
    [SerializeField] private float _defaultTypeSpeed = 0.04f;

    [FoldoutGroup("Typing Settings"), LabelWidth(140)]
    [Tooltip("스킵 시 즉시 완성 여부")]
    [SerializeField] private bool _skipToComplete = true;

    // ── 상태 ──────────────────────────────────────────────────
    private bool _isTyping = false;

    // ── 초기화 ────────────────────────────────────────────────
    protected override void Awake()
    {
        base.Awake();

        // TextAnimatorPlayer 이벤트 구독
        if (_textAnimatorPlayer != null)
        {
            _textAnimatorPlayer.OnTextShowed += OnTypingComplete;
        }
    }

    private void OnEnable()
    {
        DialogueManager.Instance?.OnLineStarted.AddListener(OnLineStarted);
        DialogueManager.Instance?.OnChoicesShown.AddListener(OnChoicesShown);
        DialogueManager.Instance?.OnDialogueEnded.AddListener(OnDialogueEnded);
    }

    private void OnDisable()
    {
        if (DialogueManager.Instance == null) return;
        DialogueManager.Instance.OnLineStarted.RemoveListener(OnLineStarted);
        DialogueManager.Instance.OnChoicesShown.RemoveListener(OnChoicesShown);
        DialogueManager.Instance.OnDialogueEnded.RemoveListener(OnDialogueEnded);
    }

    // ── 입력 처리 ─────────────────────────────────────────────
    private void Update()
    {
        if (!IsVisible) return;
        if (Keyboard.current == null) return;
        if (!Keyboard.current.zKey.wasPressedThisFrame) return;

        if (_isTyping && _skipToComplete)
        {
            // 타이핑 중 → 즉시 완성
            _textAnimatorPlayer?.SkipTypewriter();
        }
        else if (!_isTyping)
        {
            // 타이핑 완료 → 다음 줄
            DialogueManager.Instance?.AdvanceLine();
        }
    }

    // ── DialogueManager 이벤트 핸들러 ────────────────────────

    private void OnLineStarted(DialogueLine line)
    {
        // 화자 이름
        if (_speakerNameText != null)
            _speakerNameText.text = line.speaker ?? "";

        // 초상화 (TODO: 초상화 딕셔너리 연동)
        // if (_portraitImage != null) ...

        // 선택지 초기화
        ClearChoices();
        SetAdvanceIconVisible(false);

        // TextAnimator로 타이핑 시작
        _isTyping = true;
        if (_textAnimatorPlayer != null)
        {
            _textAnimatorPlayer.ShowText(line.text ?? "");
        }
        else
        {
            // TextAnimatorPlayer 없을 경우 폴백
            Debug.LogWarning("[DialogueController] TextAnimatorPlayer is not assigned!");
            OnTypingComplete();
        }
    }

    private void OnTypingComplete()
    {
        _isTyping = false;
        SetAdvanceIconVisible(true);
        DialogueManager.Instance?.CompleteTyping();
    }

    private void OnChoicesShown(System.Collections.Generic.List<DialogueChoice> choices)
    {
        SetAdvanceIconVisible(false);
        ClearChoices();

        if (_choiceButtonPrefab == null || _choiceContainer == null) return;

        foreach (var choice in choices)
        {
            var go  = Instantiate(_choiceButtonPrefab, _choiceContainer);
            var btn = go.GetComponent<UnityEngine.UI.Button>();
            var txt = go.GetComponentInChildren<TMPro.TextMeshProUGUI>();

            if (txt != null) txt.text = choice.text;

            // 클로저 캡처 방지
            var captured = choice;
            btn?.onClick.AddListener(() =>
            {
                ClearChoices();
                DialogueManager.Instance?.OnChoiceSelected(captured);
            });

            // 버튼 팝인 연출
            go.transform.localScale = Vector3.zero;
            go.transform.DOScale(Vector3.one, 0.15f).SetEase(Ease.OutBack);
        }
    }

    private void OnDialogueEnded()
    {
        ClearChoices();
        Hide();
    }

    // ── 유틸리티 ──────────────────────────────────────────────

    private void SetAdvanceIconVisible(bool visible)
    {
        if (_advanceIcon == null) return;
        _advanceIcon.SetActive(visible);

        if (visible)
        {
            // 깜빡이는 펄스
            _advanceIcon.transform.DOKill();
            _advanceIcon.transform.DOLocalMoveY(
                _advanceIcon.transform.localPosition.y - 4f, 0.5f)
                .SetLoops(-1, LoopType.Yoyo)
                .SetEase(Ease.InOutSine);
        }
        else
        {
            _advanceIcon.transform.DOKill();
        }
    }

    private void ClearChoices()
    {
        if (_choiceContainer == null) return;
        foreach (Transform child in _choiceContainer)
            Destroy(child.gameObject);
    }
}
