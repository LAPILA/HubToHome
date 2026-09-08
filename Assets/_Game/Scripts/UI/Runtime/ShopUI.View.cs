using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed partial class ShopUI
{
    private void BuildView()
    {
        Canvas canvas = gameObject.AddComponent<Canvas>();
        // FixedViewport: 공통 출력 카메라/640x480 기준 영역은 Awake의 NormalizeCanvas가 연결합니다.
        canvas.renderMode = RenderMode.ScreenSpaceCamera;
        // 기존 DialogueCanvas와 같은 UI 레이어에서 대화창(998) 아래에 표시합니다.
        canvas.sortingLayerName = "UI";
        canvas.sortingOrder = 900;
        var scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(640f, 480f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;
        _canvasGroup = gameObject.AddComponent<CanvasGroup>();

        TMP_FontAsset font = GameContentCatalog.Instance != null
            ? GameContentCatalog.Instance.DefaultUiFont
            : TMP_Settings.defaultFontAsset;

        var content = new GameObject("ShopContent", typeof(RectTransform), typeof(CanvasGroup));
        content.transform.SetParent(transform, false);
        Stretch((RectTransform)content.transform);
        _contentGroup = content.GetComponent<CanvasGroup>();
        _contentGroup.interactable = false;
        _contentGroup.blocksRaycasts = false;

        Image backdrop = CreateImage("Backdrop", content.transform, new Color32(8, 8, 12, 245));
        Stretch(backdrop.rectTransform);

        _vendorImage = CreateImage("VendorPortrait", content.transform, Color.white);
        _vendorImage.preserveAspect = true;
        _vendorImage.raycastTarget = false;
        SetRect(_vendorImage.rectTransform, new Vector2(0f, 0f), new Vector2(640f, 480f));

        var menu = new GameObject("Menu", typeof(RectTransform), typeof(CanvasGroup));
        menu.transform.SetParent(content.transform, false);
        Stretch((RectTransform)menu.transform);
        _menuGroup = menu.GetComponent<CanvasGroup>();
        _menuGroup.interactable = false;
        _menuGroup.blocksRaycasts = false;
        Transform menuRoot = menu.transform;

        // 샘플 그림의 하단 메뉴 공간에 5행 목록과 설명을 배치합니다.
        Image contentPanel = CreateImage("ContentPanel", menuRoot, PanelColor);
        SetRect(contentPanel.rectTransform, new Vector2(0f, -155f), new Vector2(640f, 170f));
        Image divider = CreateImage("Divider", menuRoot, new Color32(95, 148, 144, 255));
        SetRect(divider.rectTransform, new Vector2(0f, -70f), new Vector2(608f, 2f));
        Image columnDivider = CreateImage("ColumnDivider", menuRoot, new Color32(47, 67, 68, 255));
        SetRect(columnDivider.rectTransform, new Vector2(-14f, -158f), new Vector2(1f, 108f));

        _title = CreateText("Title", menuRoot, font, 19f, FontStyles.Bold);
        _title.alignment = TextAlignmentOptions.Left;
        SetRect(_title.rectTransform, new Vector2(-90f, 218f), new Vector2(420f, 28f));

        _tabs = CreateText("Tabs", menuRoot, font, 17f, FontStyles.Bold);
        _tabs.alignment = TextAlignmentOptions.Left;
        _tabs.color = AccentColor;
        SetRect(_tabs.rectTransform, new Vector2(-120f, -87f), new Vector2(360f, 24f));

        _money = CreateText("Money", menuRoot, font, 17f, FontStyles.Bold);
        _money.alignment = TextAlignmentOptions.Right;
        SetRect(_money.rectTransform, new Vector2(225f, -87f), new Vector2(150f, 24f));

        _list = CreateText("List", menuRoot, font, 17f, FontStyles.Normal);
        _list.alignment = TextAlignmentOptions.TopLeft;
        _list.textWrappingMode = TextWrappingModes.NoWrap;
        _list.lineSpacing = 1f;
        SetRect(_list.rectTransform, new Vector2(-162f, -159f), new Vector2(280f, 112f));

        _description = CreateText("Description", menuRoot, font, 17f, FontStyles.Normal);
        _description.alignment = TextAlignmentOptions.TopLeft;
        _description.textWrappingMode = TextWrappingModes.Normal;
        SetRect(_description.rectTransform, new Vector2(156f, -145f), new Vector2(284f, 84f));

        _status = CreateText("Status", menuRoot, font, 15f, FontStyles.Normal);
        _status.alignment = TextAlignmentOptions.TopLeft;
        _status.color = AccentColor;
        SetRect(_status.rectTransform, new Vector2(156f, -205f), new Vector2(284f, 32f));

        _help = CreateText("Help", menuRoot, font, 13f, FontStyles.Normal);
        _help.text = "↑ ↓ 선택   확인 열기   취소 닫기";
        _help.alignment = TextAlignmentOptions.Center;
        _help.color = MutedColor;
        SetRect(_help.rectTransform, new Vector2(0f, -230f), new Vector2(608f, 18f));

        // 상점 전용 FixedViewport 암전. 모니터 양옆의 검은 여백이나 다른 전환 Canvas는 건드리지 않습니다.
        _entryBlack = CreateImage("EntryFade", transform, Color.black);
        Stretch(_entryBlack.rectTransform);
        _entryBlack.gameObject.SetActive(false);
    }

    private static Image CreateImage(string name, Transform parent, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(parent, false);
        Image image = go.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
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
        text.overflowMode = TextOverflowModes.Ellipsis;
        return text;
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
