using System.Collections;
using System.Collections.Generic;
using System.Text;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

public interface IBattleResultAdvanceInputSource
{
    bool AdvancePressedThisFrame { get; }
}

internal sealed class GameInputBattleResultAdvanceInputSource : IBattleResultAdvanceInputSource
{
    public static readonly GameInputBattleResultAdvanceInputSource Instance =
        new GameInputBattleResultAdvanceInputSource();

    private GameInputBattleResultAdvanceInputSource()
    {
    }

    public bool AdvancePressedThisFrame =>
        GameInput.ConfirmPressed || GameInput.BattleConfirmPressed || GameInput.DialogueAdvancePressed;
}

[DisallowMultipleComponent]
public sealed class BattleResultUI : MonoBehaviour
{
    private static BattleResultUI _globalInstance;

    [SerializeField] private CanvasGroup _canvasGroup;
    [SerializeField] private TMP_Text _title;
    [SerializeField] private TMP_Text _rewardText;
    [SerializeField] private TMP_Text _levelText;
    [SerializeField] private float _fadeDuration = 0.18f;
    [FormerlySerializedAs("_holdDuration")]
    [SerializeField, Min(0f)] private float _minimumInputDelay = 0.2f;
    private Tween _fadeTween;
    private IBattleResultAdvanceInputSource _advanceInputSource =
        GameInputBattleResultAdvanceInputSource.Instance;
    private int _presentationVersion;

    public static BattleResultUI Ensure(Transform parent)
    {
        if (parent == null) return null;
        BattleResultUI existing = parent.GetComponentInChildren<BattleResultUI>(true);
        if (existing != null) return existing;

        TMP_FontAsset font = GameContentCatalog.Instance != null ? GameContentCatalog.Instance.DefaultUiFont : null;
        TMP_Text existingText = parent.GetComponentInChildren<TMP_Text>(true);
        if (existingText != null) font = existingText.font;
        if (font == null)
        {
            TMP_Text[] loadedTexts = Resources.FindObjectsOfTypeAll<TMP_Text>();
            for (int i = 0; i < loadedTexts.Length; i++)
            {
                if (loadedTexts[i] != null && loadedTexts[i].font != null)
                {
                    font = loadedTexts[i].font;
                    break;
                }
            }
        }
        if (font == null) font = TMP_Settings.defaultFontAsset;

        GameObject panel = new GameObject("BattleResultUI", typeof(RectTransform), typeof(CanvasGroup), typeof(Image), typeof(BattleResultUI));
        panel.transform.SetParent(parent, false);

        RectTransform panelRect = panel.GetComponent<RectTransform>();
        panelRect.anchorMin = panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelRect.sizeDelta = new Vector2(576f, 208f);

        Image background = panel.GetComponent<Image>();
        background.color = new Color(0.035f, 0.04f, 0.055f, 0.96f);

        BattleResultUI view = panel.GetComponent<BattleResultUI>();
        view._canvasGroup = panel.GetComponent<CanvasGroup>();
        view._title = CreateText(panel.transform, "Title", font, 28f, FontStyles.Bold, new Vector2(0f, 58f));
        view._rewardText = CreateText(panel.transform, "Rewards", font, 22f, FontStyles.Normal, new Vector2(0f, 8f));
        view._levelText = CreateText(panel.transform, "LevelUps", font, 20f, FontStyles.Normal, new Vector2(0f, -46f));
        panel.SetActive(false);
        return view;
    }

    public static BattleResultUI EnsureGlobal()
    {
        if (_globalInstance != null)
            return _globalInstance;

        GameObject root = new GameObject(
            "GlobalBattleResultCanvas",
            typeof(RectTransform),
            typeof(Canvas),
            typeof(CanvasScaler),
            typeof(GraphicRaycaster));
        Canvas canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 500;

        CanvasScaler scaler = root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = GameConfigPolicy.ReferenceResolution;
        scaler.matchWidthOrHeight = 0.5f;

        _globalInstance = Ensure(root.transform);
        return _globalInstance;
    }

    public void SetAdvanceInputSource(IBattleResultAdvanceInputSource inputSource)
    {
        _advanceInputSource = inputSource ?? GameInputBattleResultAdvanceInputSource.Instance;
    }

    public IEnumerator Show(BattleRewardResult result, bool instantVictory = false)
    {
        if (result == null) yield break;

        gameObject.SetActive(true);
        int presentationVersion = ++_presentationVersion;
        try
        {
            transform.SetAsLastSibling();
            ResetPresentation(false);
            _canvasGroup.blocksRaycasts = true;

            List<BattleResultPage> pages = BuildPages(result, instantVictory);
            ApplyPage(pages[0]);
            yield return FadeTo(1f);
            if (!IsPresentationCurrent(presentationVersion))
                yield break;

            for (int i = 0; i < pages.Count; i++)
            {
                if (i > 0)
                    ApplyPage(pages[i]);

                yield return WaitForAdvanceInput(presentationVersion);
                if (!IsPresentationCurrent(presentationVersion))
                    yield break;
            }

            yield return FadeTo(0f);
        }
        finally
        {
            if (_presentationVersion == presentationVersion)
                ResetPresentation(true);
        }
    }

    private void ApplyPage(BattleResultPage page)
    {
        _title.text = page.Title;
        _rewardText.text = page.RewardText;
        _levelText.text = page.DetailText;
    }

    private void OnDisable()
    {
        _presentationVersion++;
        ResetPresentation(false);
    }

    private void OnDestroy()
    {
        _presentationVersion++;
        ResetPresentation(false);
        if (_globalInstance == this)
            _globalInstance = null;
    }

    private IEnumerator FadeTo(float alpha)
    {
        float duration = Mathf.Max(0f, _fadeDuration);
        if (duration <= 0f)
        {
            _canvasGroup.alpha = alpha;
            yield break;
        }

        Tween fadeTween = _canvasGroup
            .DOFade(alpha, duration)
            .SetUpdate(true);
        _fadeTween = fadeTween;
        yield return fadeTween.WaitForCompletion();
        if (ReferenceEquals(_fadeTween, fadeTween))
            _fadeTween = null;
    }

    private IEnumerator WaitForAdvanceInput(int presentationVersion)
    {
        // Even a zero delay must cross a frame boundary so one press cannot consume two pages.
        yield return null;
        if (!IsPresentationCurrent(presentationVersion))
            yield break;

        float delay = Mathf.Max(0f, _minimumInputDelay);
        float delayEndsAt = Time.realtimeSinceStartup + delay;
        while (IsPresentationCurrent(presentationVersion)
            && Time.realtimeSinceStartup < delayEndsAt)
        {
            yield return null;
        }

        while (IsPresentationCurrent(presentationVersion))
        {
            IBattleResultAdvanceInputSource inputSource =
                _advanceInputSource ?? GameInputBattleResultAdvanceInputSource.Instance;
            if (inputSource.AdvancePressedThisFrame)
                yield break;
            yield return null;
        }
    }

    private bool IsPresentationCurrent(int presentationVersion)
    {
        return _presentationVersion == presentationVersion && isActiveAndEnabled;
    }

    private void ResetPresentation(bool deactivate)
    {
        if (_fadeTween != null)
        {
            _fadeTween.Kill(false);
            _fadeTween = null;
        }

        if (_canvasGroup != null)
        {
            _canvasGroup.DOKill(false);
            _canvasGroup.alpha = 0f;
            _canvasGroup.interactable = false;
            _canvasGroup.blocksRaycasts = false;
        }

        if (deactivate && gameObject.activeSelf)
            gameObject.SetActive(false);
    }

    private static TMP_Text CreateText(
        Transform parent,
        string name,
        TMP_FontAsset font,
        float size,
        FontStyles style,
        Vector2 position)
    {
        GameObject textObject = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(parent, false);
        RectTransform rect = textObject.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(536f, 48f);
        rect.anchoredPosition = position;

        TMP_Text text = textObject.GetComponent<TMP_Text>();
        text.font = font;
        text.fontSize = size;
        text.fontStyle = style;
        text.alignment = TextAlignmentOptions.Center;
        text.color = Color.white;
        text.textWrappingMode = TextWrappingModes.Normal;
        text.enableAutoSizing = true;
        text.fontSizeMin = 16f;
        text.fontSizeMax = size;
        return text;
    }

    private static string BuildRewardText(BattleRewardResult result)
    {
        var builder = new StringBuilder();
        builder.Append("EXP +").Append(result.Experience).Append("    GOLD +").Append(result.Gold);
        for (int i = 0; i < result.Items.Count; i++)
        {
            ItemRewardResult item = result.Items[i];
            ItemData data = ItemDatabase.FindById(item.ItemId);
            builder.Append("    ").Append(data != null ? data.ItemName : item.ItemId).Append(" x").Append(item.Amount);
        }
        return builder.ToString();
    }

    private static List<BattleResultPage> BuildPages(BattleRewardResult result, bool instantVictory)
    {
        var pages = new List<BattleResultPage>(1 + result.LevelUps.Count)
        {
            new BattleResultPage(
                instantVictory ? "INSTANT VICTORY" : "VICTORY",
                BuildRewardText(result),
                " ")
        };

        for (int i = 0; i < result.LevelUps.Count; i++)
        {
            CharacterLevelUpResult level = result.LevelUps[i];
            if (level == null || !level.DidLevelUp)
                continue;

            CharacterData data = CharacterDatabase.FindById(level.CharacterDataId);
            string characterName = data != null ? data.DisplayName : level.CharacterDataId;
            pages.Add(new BattleResultPage(
                "LEVEL UP",
                $"{characterName}  LV {level.PreviousLevel} > {level.NewLevel}",
                BuildStatGainText(level)));
        }

        return pages;
    }

    private static string BuildStatGainText(CharacterLevelUpResult level)
    {
        var builder = new StringBuilder();
        AppendStatGain(builder, "HP", level.MaxHpGained);
        AppendStatGain(builder, "MP", level.MaxMpGained);
        AppendStatGain(builder, "ATK", level.AttackGained);
        AppendStatGain(builder, "DEF", level.DefenseGained);
        AppendStatGain(builder, "SPD", level.SpeedGained);
        return builder.Length > 0 ? builder.ToString() : " ";
    }

    private static void AppendStatGain(StringBuilder builder, string label, int amount)
    {
        if (amount <= 0)
            return;

        if (builder.Length > 0)
            builder.Append("    ");
        builder.Append(label).Append(" +").Append(amount);
    }

    private readonly struct BattleResultPage
    {
        public BattleResultPage(string title, string rewardText, string detailText)
        {
            Title = title;
            RewardText = rewardText;
            DetailText = detailText;
        }

        public string Title { get; }
        public string RewardText { get; }
        public string DetailText { get; }
    }
}
