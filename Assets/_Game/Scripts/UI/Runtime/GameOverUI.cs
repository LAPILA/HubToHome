using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class GameOverUI : MonoBehaviour
{
    private static GameOverUI s_instance;

    private CanvasGroup _canvasGroup;
    private TMP_Text _message;
    private Button _retryButton;
    private Button _titleButton;
    private TMP_Text _retryLabel;
    private TMP_Text _titleLabel;
    private int _selectedIndex;
    private int _submitFrame = -1;
    private bool _visible;
    private bool _transitioning;

    public static GameOverUI Instance => s_instance;
    public bool IsVisible => _visible;

    public static GameOverUI EnsureGlobal()
    {
        if (s_instance != null)
            return s_instance;

        GameOverUI existing = FindFirstObjectByType<GameOverUI>(FindObjectsInactive.Include);
        if (existing != null)
            return existing;

        var root = new GameObject("GameOverUI");
        return root.AddComponent<GameOverUI>();
    }

    public static void Request()
    {
        GameOverUI view = EnsureGlobal();
        if (view != null && !view._visible)
            view.StartCoroutine(view.Show());
    }

    public IEnumerator Show()
    {
        if (_visible)
        {
            yield return new WaitUntil(() => !_visible);
            yield break;
        }

        Time.timeScale = 1f;
        GameStateManager.Instance?.ChangeState(GameState.Cutscene);
        _visible = true;
        _transitioning = false;
        _selectedIndex = SaveManager.HasAnySave() ? 0 : 1;
        _retryButton.interactable = SaveManager.HasAnySave();
        _message.text = _retryButton.interactable
            ? "마지막 저장 지점에서 다시 시작할 수 있습니다."
            : "불러올 수 있는 저장 데이터가 없습니다.";
        _canvasGroup.alpha = 1f;
        _canvasGroup.blocksRaycasts = true;
        _canvasGroup.interactable = true;
        UpdateSelection();

        while (_visible)
            yield return null;
    }

    private void Awake()
    {
        if (s_instance != null && s_instance != this)
        {
            Destroy(gameObject);
            return;
        }

        s_instance = this;
        DontDestroyOnLoad(gameObject);
        BuildView();
        HideImmediate();
    }

    private void OnDestroy()
    {
        if (s_instance == this)
            s_instance = null;
    }

    private void Update()
    {
        if (!_visible || _transitioning)
            return;

        if (GameInput.UIUpPressed || GameInput.UIDownPressed)
        {
            _selectedIndex = _selectedIndex == 0 ? 1 : 0;
            if (_selectedIndex == 0 && !_retryButton.interactable)
                _selectedIndex = 1;
            UpdateSelection();
        }

        if (GameInput.UISubmitPressed && _submitFrame != Time.frameCount)
        {
            _submitFrame = Time.frameCount;
            if (_selectedIndex == 0 && _retryButton.interactable)
                RetryLatestSave();
            else
                ReturnToTitle();
        }
    }

    private void RetryLatestSave()
    {
        if (_transitioning)
            return;

        _transitioning = true;
        _message.text = "저장 데이터를 불러오는 중...";
        GameLoadStartResult result = GameLoadCoordinator.LoadMostRecent(HandleRetryCompleted);
        if (result.Accepted)
        {
            HideImmediate();
            return;
        }

        _transitioning = false;
        _message.text = "재시도에 실패했습니다. " + result.Message;
        _retryButton.interactable = SaveManager.HasAnySave();
        if (!_retryButton.interactable)
            _selectedIndex = 1;
        UpdateSelection();
    }

    private void HandleRetryCompleted(SceneLoadResult result)
    {
        if (SceneLoadResultUtility.WasDestinationActivated(result))
            return;

        RestoreAfterTransitionFailure(
            "Scene 복구에 실패했습니다. 타이틀로 이동해 주세요.");
    }

    private void ReturnToTitle()
    {
        if (_transitioning)
            return;

        _transitioning = true;
        GlobalDataManager.Instance?.CancelPendingBattleEncounter();
        Time.timeScale = 1f;
        SceneLoadOperation operation = SceneLoader.Instance?.LoadSceneWithResult(
            SceneName.Title,
            0.35f,
            result =>
            {
                if (SceneLoadResultUtility.WasDestinationActivated(result))
                    return;

                RestoreAfterTransitionFailure(
                    "타이틀 Scene으로 이동하지 못했습니다: " + result);
            });

        if (operation == null)
        {
            RestoreAfterTransitionFailure("타이틀 Scene을 불러올 수 없습니다.");
            return;
        }
        if (operation.IsDone)
        {
            if (!SceneLoadResultUtility.WasDestinationActivated(operation.Result)
                && !_visible)
            {
                RestoreAfterTransitionFailure("타이틀 Scene을 불러올 수 없습니다.");
            }
            return;
        }

        HideImmediate();
    }

    private void RestoreAfterTransitionFailure(string message)
    {
        _visible = true;
        _transitioning = false;
        _canvasGroup.alpha = 1f;
        _canvasGroup.blocksRaycasts = true;
        _canvasGroup.interactable = true;
        _message.text = message ?? string.Empty;
        _selectedIndex = 1;
        UpdateSelection();
    }

    private void HideImmediate()
    {
        _visible = false;
        if (_canvasGroup == null)
            return;

        _canvasGroup.alpha = 0f;
        _canvasGroup.blocksRaycasts = false;
        _canvasGroup.interactable = false;
    }

    private void UpdateSelection()
    {
        _retryLabel.color = _selectedIndex == 0 && _retryButton.interactable
            ? new Color32(255, 224, 92, 255)
            : new Color32(220, 220, 220, 255);
        _titleLabel.color = _selectedIndex == 1
            ? new Color32(255, 224, 92, 255)
            : new Color32(220, 220, 220, 255);

        Button selected = _selectedIndex == 0 ? _retryButton : _titleButton;
        if (EventSystem.current != null && selected != null && selected.interactable)
            EventSystem.current.SetSelectedGameObject(selected.gameObject);
    }

    private void BuildView()
    {
        Canvas canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = short.MaxValue;
        var scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(640f, 480f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;
        gameObject.AddComponent<GraphicRaycaster>();
        _canvasGroup = gameObject.AddComponent<CanvasGroup>();

        TMP_FontAsset font = GameContentCatalog.Instance != null
            ? GameContentCatalog.Instance.DefaultUiFont
            : TMP_Settings.defaultFontAsset;

        Image backdrop = CreateImage("Backdrop", transform, new Color32(0, 0, 0, 238));
        Stretch(backdrop.rectTransform);

        TMP_Text title = CreateText("Title", transform, font, 40f, FontStyles.Bold);
        title.text = "GAME OVER";
        title.alignment = TextAlignmentOptions.Center;
        SetRect(title.rectTransform, new Vector2(0f, 88f), new Vector2(520f, 64f));

        _message = CreateText("Message", transform, font, 18f, FontStyles.Normal);
        _message.alignment = TextAlignmentOptions.Center;
        _message.textWrappingMode = TextWrappingModes.Normal;
        SetRect(_message.rectTransform, new Vector2(0f, 28f), new Vector2(500f, 54f));

        _retryButton = CreateButton("Retry", transform, font, "최근 저장에서 재시도", out _retryLabel);
        SetRect(_retryButton.GetComponent<RectTransform>(), new Vector2(0f, -48f), new Vector2(300f, 46f));
        _retryButton.onClick.AddListener(RetryLatestSave);

        _titleButton = CreateButton("Title", transform, font, "타이틀로 이동", out _titleLabel);
        SetRect(_titleButton.GetComponent<RectTransform>(), new Vector2(0f, -106f), new Vector2(300f, 46f));
        _titleButton.onClick.AddListener(ReturnToTitle);
    }

    private static Image CreateImage(string name, Transform parent, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(parent, false);
        Image image = go.GetComponent<Image>();
        image.color = color;
        return image;
    }

    private static TMP_Text CreateText(
        string name,
        Transform parent,
        TMP_FontAsset font,
        float size,
        FontStyles style)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        TMP_Text text = go.GetComponent<TMP_Text>();
        text.font = font;
        text.fontSize = size;
        text.fontStyle = style;
        text.color = Color.white;
        text.raycastTarget = false;
        return text;
    }

    private static Button CreateButton(
        string name,
        Transform parent,
        TMP_FontAsset font,
        string label,
        out TMP_Text labelText)
    {
        Image image = CreateImage(name, parent, new Color32(24, 24, 30, 255));
        Button button = image.gameObject.AddComponent<Button>();
        ColorBlock colors = button.colors;
        colors.normalColor = new Color32(24, 24, 30, 255);
        colors.highlightedColor = new Color32(50, 50, 62, 255);
        colors.selectedColor = colors.highlightedColor;
        colors.pressedColor = new Color32(70, 70, 82, 255);
        colors.disabledColor = new Color32(18, 18, 22, 210);
        button.colors = colors;

        labelText = CreateText("Label", image.transform, font, 20f, FontStyles.Normal);
        labelText.text = label;
        labelText.alignment = TextAlignmentOptions.Center;
        Stretch(labelText.rectTransform);
        return button;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private static void SetRect(RectTransform rect, Vector2 position, Vector2 size)
    {
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }
}