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
    private bool _confirmWasDown;

    public bool IsBusy => _routine != null || _isShowing;
    public bool IsAwaitingConfirm => _awaitingConfirm;

    private void Awake()
    {
        _canvasGroup = GetComponent<CanvasGroup>();
        if (_messageText == null) _messageText = GetComponentInChildren<TextMeshProUGUI>(true);
        if (_bubbleBackground == null) _bubbleBackground = FindChildRect("Backg");
        HideImmediate();
    }

    public void Enqueue(BattleNarrationMessage message)
    {
        if (!gameObject.activeInHierarchy) gameObject.SetActive(true);
        if (string.IsNullOrWhiteSpace(message.Text) || _messageText == null) return;

        while (_queue.Count >= _maxQueueCount)
            _queue.Dequeue();

        _queue.Enqueue(message);
        if (_routine == null && isActiveAndEnabled)
            _routine = StartCoroutine(ProcessQueue());
    }

    private void OnEnable()
    {
        if (_routine == null && _queue.Count > 0)
            _routine = StartCoroutine(ProcessQueue());
    }

    public void Clear()
    {
        _queue.Clear();
        if (_routine != null) StopCoroutine(_routine);
        _routine = null;
        HideImmediate();
    }

    private IEnumerator ProcessQueue()
    {
        while (_queue.Count > 0)
        {
            BattleNarrationMessage msg = _queue.Dequeue();
            yield return ShowMessage(msg);
        }

        _routine = null;
        _isShowing = false;
        if (_queue.Count == 0)
            HideImmediate();
    }

    private IEnumerator ShowMessage(BattleNarrationMessage msg)
    {
        _isShowing = true;
        gameObject.SetActive(true);

        _canvasGroup.DOKill();
        _canvasGroup.alpha = 1f;
        _messageText.color = Color.white;
        _messageText.text = msg.Text;
        _messageText.maxVisibleCharacters = 0;
        ResizeBubbleToText(msg.Text);
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
        bool isDown = GameInput.BattleConfirmPressed || GameInput.DialogueAdvancePressed || GameInput.ConfirmPressed;
        bool pressedThisFrame = isDown && !_confirmWasDown;
        _confirmWasDown = isDown;
        return pressedThisFrame;
    }

    private void HideImmediate()
    {
        if (_canvasGroup == null) _canvasGroup = GetComponent<CanvasGroup>();
        _canvasGroup.alpha = 1f;
        if (_messageText != null)
        {
            _rollingLines.Clear();
            _sb.Clear();
            _messageText.text = string.Empty;
        }
        _isShowing = false;
        _awaitingConfirm = false;
        _confirmWasDown = false;
        gameObject.SetActive(false);
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
