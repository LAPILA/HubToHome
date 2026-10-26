using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Sirenix.OdinInspector;

[RequireComponent(typeof(CanvasGroup))]
public class BattleNarrationUI : MonoBehaviour
{
    [SerializeField, LabelText("기록 본문")] private TextMeshProUGUI _messageText;
    [SerializeField, LabelText("고정 창 배경")] private RectTransform _bubbleBackground;
    [SerializeField, LabelText("고정 창 크기")] private Vector2 _fixedBubbleSize = new Vector2(480f, 72f);
    [SerializeField, LabelText("본문 전체 여백")] private Vector2 _bubblePadding = new Vector2(24f, 12f);
    [SerializeField, LabelText("화면 위쪽 여백"), Min(0f)] private float _topInset = 56f;
    [SerializeField, LabelText("글자 크기"), Range(8f, 24f)] private float _fontSize = 14f;
    // 이전 프리팹의 직렬화 키만 보존합니다. 글 분량에 따른 크기 변경은 하지 않습니다.
    [SerializeField, HideInInspector] private Vector2 _minBubbleSize = new Vector2(240f, 96f);
    [SerializeField, HideInInspector] private float _maxBubbleWidthRatio = 0.5f;
    [SerializeField, HideInInspector] private float _maxBubbleHeightRatio = 0.35f;
    [SerializeField, LabelText("글자 간격 (초)"), Min(0f)] private float _typeInterval = 0.015f;
    [SerializeField, LabelText("줄 자동 진행 대기 (초)"), Min(0f)] private float _defaultHoldDuration = 0.5f;
    [SerializeField, LabelText("줄 올라가는 시간 (초)"), Min(0f)] private float _scrollDuration = 0.1f;
    [SerializeField, LabelText("대기 기록 수"), Min(1)] private int _maxQueueCount = 20;
    [SerializeField, LabelText("최대 표시 줄 수"), Range(1, 8)] private int _maxRollingLines = 3;

    private readonly Queue<BattleNarrationMessage> _queue = new Queue<BattleNarrationMessage>();
    private CanvasGroup _canvasGroup;
    private Coroutine _routine;
    private bool _isShowing;
    private bool _awaitingConfirm;
    private readonly List<NarrationLine> _lines = new List<NarrationLine>(16);
    private RectMask2D _textMask;
    private Vector2 _textHomePosition;
    private float _textWidth, _textHeight;
    private float _scrollFrom, _scrollTarget, _scrollElapsed, _scrollOffset;
    private int _visibleLineCount = 3;
    private bool _initialized;
    private int _lastConfirmFrame = -1;
    private int _messageStartedFrame = -1;

    public bool IsBusy => _routine != null || _isShowing;
    public bool IsAwaitingConfirm => _awaitingConfirm;

    private void Awake() => EnsureInitialized();

    private void EnsureInitialized()
    {
        if (_initialized) return;
        _initialized = true;
        _canvasGroup = GetComponent<CanvasGroup>();
        if (_messageText == null) _messageText = GetComponentInChildren<TextMeshProUGUI>(true);
        if (_bubbleBackground == null) _bubbleBackground = FindChildRect("Backg");
        if (_bubbleBackground != null)
        {
            _textMask = _bubbleBackground.GetComponent<RectMask2D>();
            if (_textMask == null) _textMask = _bubbleBackground.gameObject.AddComponent<RectMask2D>();
        }
        HideImmediate();
    }

    public void Enqueue(BattleNarrationMessage message)
    {
        // 비어 있는 템플릿/효과 안내는 창 자체를 열지 않습니다.
        if (string.IsNullOrWhiteSpace(message.Text)) return;
        EnsureInitialized();
        if (_messageText == null || _bubbleBackground == null) return;

        while (_queue.Count >= Mathf.Max(1, _maxQueueCount))
            _queue.Dequeue();

        _queue.Enqueue(message);
        if (!gameObject.activeSelf) gameObject.SetActive(true);
        if (_routine == null && isActiveAndEnabled)
            _routine = StartCoroutine(ProcessQueue());
    }

    private void OnEnable()
    {
        EnsureInitialized();
        if (_routine == null && _queue.Count > 0 && isActiveAndEnabled)
            _routine = StartCoroutine(ProcessQueue());
    }

    public void Clear()
    {
        StopProcessing();
        HideImmediate();
    }

    private void OnDisable()
    {
        // GameObject 비활성화로 멈춘 코루틴 핸들을 다음 표시까지 남기지 않습니다.
        StopProcessing();
        ResetHiddenState();
    }

    private void StopProcessing()
    {
        _queue.Clear();
        Coroutine routine = _routine;
        _routine = null;
        if (routine != null) StopCoroutine(routine);
    }

    private IEnumerator ProcessQueue()
    {
        try
        {
            while (_queue.Count > 0)
            {
                BattleNarrationMessage msg = _queue.Dequeue();
                yield return ShowMessage(msg);
            }
        }
        finally
        {
            _routine = null;
            if (this != null) HideImmediate();
        }
    }

    private IEnumerator ShowMessage(BattleNarrationMessage msg)
    {
        if (_messageText == null || _canvasGroup == null) yield break;
        _isShowing = true;
        _messageStartedFrame = Time.frameCount;
        gameObject.SetActive(true);

        _canvasGroup.alpha = 1f;
        _messageText.color = Color.white;
        ConfigureFixedBubble();
        ResetTextPosition();
        _messageText.text = WrapTextForBubble(msg.Text);
        _messageText.firstVisibleCharacter = 0;
        _messageText.maxVisibleCharacters = int.MaxValue;
        _messageText.ForceMeshUpdate();
        CaptureLineRanges();
        _messageText.maxVisibleCharacters = 0;

        float interval = Mathf.Max(0f, _typeInterval);
        float hold = Mathf.Max(0f, msg.HoldOverride > 0f ? msg.HoldOverride : _defaultHoldDuration);
        try
        {
            for (int index = 0; index < _lines.Count; index++)
            {
                if (_messageText == null) yield break;
                NarrationLine line = _lines[index];
                BeginLineScroll(index);
                // 즉시 표시도 현재 줄에만 적용합니다. 다음 줄은 새 확인 입력을 받습니다.
                int start = Mathf.Max(_messageText.maxVisibleCharacters, line.Start);
                _messageText.maxVisibleCharacters = msg.RevealImmediately ? line.End : start;
                float elapsed = 0f;
                while (_messageText.maxVisibleCharacters < line.End)
                {
                    if (!IsNarrationPaused && (ConsumeConfirmPress() || interval <= 0f)) break;
                    yield return null;
                    if (_messageText == null) yield break;
                    if (IsNarrationPaused) continue;
                    AdvanceLineScroll(Time.unscaledDeltaTime);
                    elapsed += Time.unscaledDeltaTime;
                    _messageText.maxVisibleCharacters = interval <= 0f ? line.End
                        : Mathf.Min(line.End, start + Mathf.FloorToInt(elapsed / interval));
                }

                _messageText.maxVisibleCharacters = line.End;
                _awaitingConfirm = msg.RequiresConfirm;
                int revealedFrame = Time.frameCount;
                elapsed = 0f;
                while (msg.RequiresConfirm || elapsed < hold || Time.frameCount == revealedFrame)
                {
                    if (!IsNarrationPaused && ConsumeConfirmPress()) break;
                    yield return null;
                    if (_messageText == null) yield break;
                    if (IsNarrationPaused) continue;
                    AdvanceLineScroll(Time.unscaledDeltaTime);
                    elapsed += Time.unscaledDeltaTime;
                }
                _awaitingConfirm = false;
            }
            // 폰트/본문이 비어도 StartCoroutine 핸들이 동기 완료로 남지 않게 합니다.
            if (_lines.Count == 0) yield return null;
        }
        finally
        {
            _awaitingConfirm = false;
            _isShowing = false;
            if (_messageText != null)
            {
                _messageText.text = string.Empty;
                ResetTextPosition();
            }
        }
    }

    private static bool IsNarrationPaused => BattleDelay.IsPaused || GameInput.IsBattleInputBlocked;

    private bool ConsumeConfirmPress()
    {
        // 적 대응 중의 Z는 가드 소유입니다. 비모달 전투 기록이 같은 press를
        // 메뉴 입력으로 소비하면 이후 타격까지 유지한 가드가 풀리므로 양보합니다.
        if (BattleManager.Instance != null && BattleManager.Instance.CanAcceptRealtimeDefenseInput)
            return false;
        if (Time.frameCount <= _messageStartedFrame || _lastConfirmFrame == Time.frameCount
            || GameInput.IsBattleInputBlocked || GameInput.BattleUIInputConsumed) return false;
        // WasPressedThisFrame은 이미 새 입력입니다. 코루틴 사이에서 release를 놓쳐도 다음 press를 받습니다.
        if (!GameInput.BattleConfirmPressed && !GameInput.DialogueAdvancePressed && !GameInput.ConfirmPressed)
            return false;
        _lastConfirmFrame = Time.frameCount;
        GameInput.ConsumeBattleUIInput();
        GameInput.SuppressPlayerConfirmForCurrentFrame();
        return true;
    }

    private void HideImmediate()
    {
        ResetHiddenState();
        gameObject.SetActive(false);
    }

    private void ResetHiddenState()
    {
        if (_canvasGroup == null) _canvasGroup = GetComponent<CanvasGroup>();
        if (_canvasGroup != null)
        {
            _canvasGroup.alpha = 0f;
            _canvasGroup.interactable = false;
            _canvasGroup.blocksRaycasts = false;
        }
        if (_messageText != null)
        {
            _lines.Clear();
            _messageText.text = string.Empty;
            _messageText.firstVisibleCharacter = 0;
            _messageText.maxVisibleCharacters = 0;
            ResetTextPosition();
        }
        _isShowing = false;
        _awaitingConfirm = false;
    }

    private void ConfigureFixedBubble()
    {
        RectTransform canvasRect = GetComponentInParent<Canvas>()?.transform as RectTransform;
        float canvasWidth = canvasRect != null && canvasRect.rect.width > 1f ? canvasRect.rect.width : 640f;
        float canvasHeight = canvasRect != null && canvasRect.rect.height > 1f ? canvasRect.rect.height : 480f;
        float inset = Mathf.Clamp(_topInset, 0f, Mathf.Max(0f, canvasHeight - 1f));
        Vector2 size = new Vector2(Mathf.Clamp(_fixedBubbleSize.x, 1f, canvasWidth),
            Mathf.Clamp(_fixedBubbleSize.y, 1f, canvasHeight - inset));
        Vector2 padding = new Vector2(Mathf.Clamp(_bubblePadding.x, 0f, size.x - 1f),
            Mathf.Clamp(_bubblePadding.y, 0f, size.y - 1f));
        if (_bubbleBackground != null)
        {
            _bubbleBackground.anchorMin = _bubbleBackground.anchorMax = new Vector2(.5f, 1f);
            _bubbleBackground.pivot = new Vector2(.5f, .5f);
            _bubbleBackground.sizeDelta = size;
            _bubbleBackground.anchoredPosition = new Vector2(0f, -inset - size.y * .5f);
        }
        _textWidth = size.x - padding.x;
        _textHeight = size.y - padding.y;
        _textHomePosition = new Vector2(0f, -padding.y * .5f);
        RectTransform textRect = _messageText.rectTransform;
        textRect.anchorMin = textRect.anchorMax = new Vector2(.5f, 1f);
        textRect.pivot = new Vector2(.5f, 1f);
        textRect.sizeDelta = new Vector2(_textWidth, _textHeight);
        _messageText.enableAutoSizing = false;
        _messageText.fontSize = Mathf.Clamp(_fontSize, 8f, 24f);
        // 가로 정렬은 TMP 프리팹 데이터(기본 중앙)를 따르고, 스크롤의 세로 기준만 고정합니다.
        _messageText.verticalAlignment = VerticalAlignmentOptions.Top;
        _messageText.margin = Vector4.zero;
        _messageText.lineSpacing = 0f;
        _messageText.useMaxVisibleDescender = false;
        _messageText.overflowMode = TextOverflowModes.Overflow;
        _messageText.textWrappingMode = TextWrappingModes.NoWrap;
        if (_textMask != null)
        {
            _textMask.padding = new Vector4(padding.x * .5f, padding.y * .5f, padding.x * .5f, padding.y * .5f);
            _textMask.softness = Vector2Int.zero;
        }
    }

    private void CaptureLineRanges()
    {
        _lines.Clear();
        TMP_TextInfo info = _messageText.textInfo;
        if (info.characterCount == 0 || info.lineCount == 0) return;
        float firstBaseline = info.lineInfo[0].baseline;
        float stride = info.lineCount > 1 ? firstBaseline - info.lineInfo[1].baseline : info.lineInfo[0].lineHeight;
        stride = Mathf.Max(1f, stride);
        _visibleLineCount = Mathf.Clamp(_maxRollingLines, 1, Mathf.Max(1, Mathf.FloorToInt(_textHeight / stride)));
        for (int i = 0; i < info.lineCount; i++)
        {
            TMP_LineInfo line = info.lineInfo[i];
            if (line.firstCharacterIndex >= info.characterCount) break;
            _lines.Add(new NarrationLine(line.firstCharacterIndex,
                Mathf.Min(info.characterCount, line.lastCharacterIndex + 1), firstBaseline - line.baseline));
        }
        if (_textMask != null)
        {
            Vector4 padding = _textMask.padding;
            // 창 크기와 별개로 정확히 N줄만 보이게 남는 아래쪽 공간을 마스킹합니다.
            padding.y += Mathf.Max(0f, _textHeight - stride * _visibleLineCount);
            _textMask.padding = padding;
        }
    }

    private string WrapTextForBubble(string text)
    {
        return SmartTextWrapper.Wrap(_messageText, text, _textWidth);
    }

    private void BeginLineScroll(int lineIndex)
    {
        int first = Mathf.Max(0, lineIndex - _visibleLineCount + 1);
        _scrollFrom = _scrollOffset;
        _scrollTarget = _lines[first].Offset;
        _scrollElapsed = 0f;
        if (_scrollDuration <= 0f) AdvanceLineScroll(0f);
    }

    private void AdvanceLineScroll(float delta)
    {
        _scrollElapsed += Mathf.Max(0f, delta);
        float t = _scrollDuration > 0f ? Mathf.Clamp01(_scrollElapsed / _scrollDuration) : 1f;
        _scrollOffset = Mathf.Lerp(_scrollFrom, _scrollTarget, 1f - (1f - t) * (1f - t));
        _messageText.rectTransform.anchoredPosition = _textHomePosition + new Vector2(0f, Mathf.Round(_scrollOffset));
    }

    private void ResetTextPosition()
    {
        _scrollFrom = _scrollTarget = _scrollElapsed = _scrollOffset = 0f;
        if (_messageText != null) _messageText.rectTransform.anchoredPosition = _textHomePosition;
    }

    private readonly struct NarrationLine
    {
        public readonly int Start, End;
        public readonly float Offset;
        public NarrationLine(int start, int end, float offset) { Start = start; End = end; Offset = offset; }
    }

    private RectTransform FindChildRect(string childName)
    {
        RectTransform[] rects = GetComponentsInChildren<RectTransform>(true);
        for (int i = 0; i < rects.Length; i++)
        {
            RectTransform rect = rects[i];
            if (rect != null && rect != transform && rect.name == childName)
                return rect;
        }

        return null;
    }

    public static BattleNarrationUI FindInActiveScene()
    {
        BattleNarrationUI[] all = Resources.FindObjectsOfTypeAll<BattleNarrationUI>();
        Scene activeScene = SceneManager.GetActiveScene();
        foreach (var ui in all)
        {
            if (ui == null) continue;
            if (ui.gameObject.scene == activeScene)
                return ui;
        }

        return all != null && all.Length > 0 ? all[0] : null;
    }
}
