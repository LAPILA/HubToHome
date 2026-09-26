using System.Collections;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using DG.Tweening;
using UnityEngine.SceneManagement;

[RequireComponent(typeof(CanvasGroup))]
public class BattleNarrationUI : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI _messageText;
    [SerializeField] private RectTransform _bubbleBackground;
    [SerializeField] private Vector2 _bubblePadding = new Vector2(72f, 48f);
    [SerializeField] private Vector2 _minBubbleSize = new Vector2(240f, 96f);
    [SerializeField] private float _maxBubbleWidthRatio = 0.5f;
    [SerializeField] private float _maxBubbleHeightRatio = 0.35f;
    [SerializeField] private float _typeInterval = 0.008f;
    [SerializeField] private float _defaultHoldDuration = 0.12f;
    [SerializeField] private int _maxQueueCount = 20;
    [SerializeField] private int _maxRollingLines = 3;

    private readonly Queue<BattleNarrationMessage> _queue = new Queue<BattleNarrationMessage>();
    private CanvasGroup _canvasGroup;
    private Coroutine _routine;
    private bool _isShowing;
    private bool _awaitingConfirm;
    private readonly List<string> _rollingLines = new List<string>();
    private readonly StringBuilder _sb = new StringBuilder(512);
    private bool _initialized;
    private int _lastConfirmFrame = -1;

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
        DisableTmpAutomaticWrapping();
        HideImmediate();
    }

    public void Enqueue(BattleNarrationMessage message)
    {
        // 비어 있는 템플릿/효과 안내는 창 자체를 열지 않습니다.
        if (string.IsNullOrWhiteSpace(message.Text)) return;
        EnsureInitialized();
        if (_messageText == null) return;

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
        _isShowing = true;
        gameObject.SetActive(true);

        _canvasGroup.DOKill();
        _canvasGroup.alpha = 1f;
        _messageText.color = Color.white;
        DisableTmpAutomaticWrapping();
        string wrappedText = WrapTextForBubble(msg.Text);
        _messageText.text = wrappedText;
        _messageText.maxVisibleCharacters = 0;
        ResizeBubbleToText(wrappedText);
        _messageText.ForceMeshUpdate();

        int total = _messageText.textInfo.characterCount;
        bool skipTyping = false;
        for (int i = 0; i <= total; i++)
        {
            _messageText.maxVisibleCharacters = i;
            if (ConsumeConfirmPress())
            {
                skipTyping = true;
                break;
            }
            yield return new WaitForSecondsRealtime(_typeInterval);
        }

        _messageText.maxVisibleCharacters = total;
        if (!skipTyping)
            yield return new WaitForSecondsRealtime(msg.HoldOverride > 0f ? msg.HoldOverride : _defaultHoldDuration);
        
        float hold = msg.HoldOverride > 0f ? msg.HoldOverride : _defaultHoldDuration;

        if (msg.RequiresConfirm)
        {
            _awaitingConfirm = true;
            yield return new WaitUntil(ConsumeConfirmPress);
            _awaitingConfirm = false;
            yield return new WaitForSecondsRealtime(hold * 0.25f);
        }
        else
        {
            yield return new WaitForSecondsRealtime(hold);
        }
        _messageText.text = string.Empty; 
        _isShowing = false;
    }

    private bool ConsumeConfirmPress()
    {
        if (_lastConfirmFrame == Time.frameCount || GameInput.BattleUIInputConsumed) return false;
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
            _rollingLines.Clear();
            _sb.Clear();
            _messageText.text = string.Empty;
        }
        _isShowing = false;
        _awaitingConfirm = false;
    }

    private void AppendRollingLine(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return;

        _rollingLines.Add(text);
        while (_rollingLines.Count > _maxRollingLines)
            _rollingLines.RemoveAt(0);

        _sb.Clear();
        for (int i = 0; i < _rollingLines.Count; i++)
        {
            if (i > 0) _sb.Append('\n');
            _sb.Append(_rollingLines[i]);
        }
        _messageText.text = _sb.ToString();
    }

    private void ResizeBubbleToText(string text)
    {
        if (_messageText == null || _bubbleBackground == null || string.IsNullOrWhiteSpace(text))
            return;

        RectTransform canvasRect = GetComponentInParent<Canvas>()?.transform as RectTransform;
        float canvasWidth = canvasRect != null && canvasRect.rect.width > 1f ? canvasRect.rect.width : Screen.width;
        float canvasHeight = canvasRect != null && canvasRect.rect.height > 1f ? canvasRect.rect.height : Screen.height;

        float maxWidth = Mathf.Max(_minBubbleSize.x, canvasWidth * Mathf.Clamp01(_maxBubbleWidthRatio));
        float maxHeight = Mathf.Max(_minBubbleSize.y, canvasHeight * Mathf.Clamp01(_maxBubbleHeightRatio));
        float textMaxWidth = Mathf.Max(1f, maxWidth - _bubblePadding.x);
        float textMaxHeight = Mathf.Max(1f, maxHeight - _bubblePadding.y);

        Vector2 preferred = _messageText.GetPreferredValues(text, textMaxWidth, textMaxHeight);
        Vector2 bubbleSize = new Vector2(
            Mathf.Clamp(preferred.x + _bubblePadding.x, _minBubbleSize.x, maxWidth),
            Mathf.Clamp(preferred.y + _bubblePadding.y, _minBubbleSize.y, maxHeight));

        _bubbleBackground.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, bubbleSize.x);
        _bubbleBackground.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, bubbleSize.y);

        RectTransform textRect = _messageText.rectTransform;
        textRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, Mathf.Max(1f, bubbleSize.x - _bubblePadding.x));
        textRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, Mathf.Max(1f, bubbleSize.y - _bubblePadding.y));
        textRect.anchoredPosition = Vector2.zero;

        Canvas.ForceUpdateCanvases();
    }

    private void DisableTmpAutomaticWrapping()
    {
        if (_messageText == null) return;

        // SmartTextWrapper owns line breaks here; TMP auto wrapping can split Korean tokens like "있어.".
        _messageText.textWrappingMode = TextWrappingModes.NoWrap;
    }

    private string WrapTextForBubble(string text)
    {
        if (_messageText == null || string.IsNullOrWhiteSpace(text))
            return text;

        RectTransform canvasRect = GetComponentInParent<Canvas>()?.transform as RectTransform;
        float canvasWidth = canvasRect != null && canvasRect.rect.width > 1f ? canvasRect.rect.width : Screen.width;
        float maxWidth = Mathf.Max(_minBubbleSize.x, canvasWidth * Mathf.Clamp01(_maxBubbleWidthRatio));
        float textMaxWidth = Mathf.Max(1f, maxWidth - _bubblePadding.x);

        // Keep battle narration from ending lines with tiny word fragments before measuring the bubble.
        return SmartTextWrapper.Wrap(_messageText, text, textMaxWidth);
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
