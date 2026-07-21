using System.Collections;
using System.Text;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class BattleResultUI : MonoBehaviour
{
    private static BattleResultUI _globalInstance;

    [SerializeField] private CanvasGroup _canvasGroup;
    [SerializeField] private TMP_Text _title;
    [SerializeField] private TMP_Text _rewardText;
    [SerializeField] private TMP_Text _levelText;
    [SerializeField] private float _fadeDuration = 0.18f;
    [SerializeField] private float _holdDuration = 1.25f;

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
        panelRect.sizeDelta = new Vector2(680f, 250f);

        Image background = panel.GetComponent<Image>();
        background.color = new Color(0.035f, 0.04f, 0.055f, 0.96f);

        BattleResultUI view = panel.GetComponent<BattleResultUI>();
        view._canvasGroup = panel.GetComponent<CanvasGroup>();
        view._title = CreateText(panel.transform, "Title", font, 38f, FontStyles.Bold, new Vector2(0f, 72f));
        view._rewardText = CreateText(panel.transform, "Rewards", font, 27f, FontStyles.Normal, new Vector2(0f, 16f));
        view._levelText = CreateText(panel.transform, "LevelUps", font, 23f, FontStyles.Normal, new Vector2(0f, -54f));
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
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        _globalInstance = Ensure(root.transform);
        return _globalInstance;
    }

    public IEnumerator Show(BattleRewardResult result, bool instantVictory = false)
    {
        if (result == null) yield break;
        gameObject.SetActive(true);
        transform.SetAsLastSibling();

        _title.text = instantVictory ? "INSTANT VICTORY" : "VICTORY";
        _rewardText.text = BuildRewardText(result);
        _levelText.text = BuildLevelText(result);

        _canvasGroup.DOKill();
        _canvasGroup.alpha = 0f;
        _canvasGroup.interactable = false;
        _canvasGroup.blocksRaycasts = true;
        yield return _canvasGroup.DOFade(1f, _fadeDuration).SetUpdate(true).WaitForCompletion();
        yield return new WaitForSecondsRealtime(_holdDuration);
        yield return _canvasGroup.DOFade(0f, _fadeDuration).SetUpdate(true).WaitForCompletion();
        _canvasGroup.blocksRaycasts = false;
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
        rect.sizeDelta = new Vector2(620f, 52f);
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

    private static string BuildLevelText(BattleRewardResult result)
    {
        var builder = new StringBuilder();
        for (int i = 0; i < result.LevelUps.Count; i++)
        {
            CharacterLevelUpResult level = result.LevelUps[i];
            if (level == null || !level.DidLevelUp) continue;
            if (builder.Length > 0) builder.Append("    ");
            CharacterData data = CharacterDatabase.FindById(level.CharacterDataId);
            builder.Append(data != null ? data.DisplayName : level.CharacterDataId)
                .Append("  LV ").Append(level.PreviousLevel).Append(" > ").Append(level.NewLevel);
        }
        return builder.Length > 0 ? builder.ToString() : " ";
    }
}
