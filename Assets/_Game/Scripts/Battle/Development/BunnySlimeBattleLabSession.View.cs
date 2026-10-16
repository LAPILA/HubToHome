using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed partial class BunnySlimeBattleLabSession
{
    private static readonly Color Ink = new Color(0.045f, 0.075f, 0.11f);
    private static readonly Color Panel = new Color(0.09f, 0.14f, 0.19f);
    private static readonly Color Muted = new Color(0.58f, 0.69f, 0.74f);
    private static readonly Color Mint = new Color(0.44f, 0.94f, 0.78f);
    private GameObject _menuRoot;
    private bool _menuVisible;
    private TMP_Text _description;
    private TMP_Text _status;
    private TMP_Text _footer;
    private Image[] _choiceBackgrounds;
    private TMP_Text[] _choiceTitles;
    private TMP_Text[] _choiceDetails;
    private TMP_FontAsset _font;

    private void BuildView()
    {
        GameContentCatalog catalog = GameContentCatalog.Instance;
        _font = catalog != null && catalog.DefaultUiFont != null ? catalog.DefaultUiFont : TMP_Settings.defaultFontAsset;
        _menuRoot = new GameObject("Lab Menu - Keyboard and Controller", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        _menuRoot.transform.SetParent(transform, false);
        Canvas canvas = _menuRoot.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 30000;
        CanvasScaler scaler = _menuRoot.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(640f, 480f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;

        var content = new GameObject("640x480 Safe Content", typeof(RectTransform));
        content.transform.SetParent(_menuRoot.transform, false);
        RectTransform contentRect = content.GetComponent<RectTransform>();
        contentRect.anchorMin = contentRect.anchorMax = new Vector2(0.5f, 0.5f);
        contentRect.sizeDelta = new Vector2(640f, 480f);

        // 중앙 112~272px는 월드에 있는 주인공과 두 동료가 보이는 영역입니다.
        Rect(CreateImage(content.transform, "Header Background", Ink).rectTransform, 0f, 0f, 640f, 112f);
        Rect(CreateImage(content.transform, "Controls Background", Ink).rectTransform, 0f, 272f, 640f, 208f);
        Rect(CreateImage(content.transform, "Top Rule", Mint).rectTransform, 28f, 14f, 44f, 3f);
        Text(content.transform, "Eyebrow", "HUB TO HOME  /  COMBAT FIELD NOTES", 28f, 22f, 560f, 20f, 13f, Muted);
        Text(content.transform, "Title", "전투 실험실", 26f, 43f, 540f, 38f, 28f, Color.white);
        Text(content.transform, "Subtitle", "전열 3명 · 각 6스킬 · 누적 상태 5종", 28f, 83f, 584f, 24f, 15f, Mint);

        // 월드 미리보기 아래의 이름표입니다. 실제 전투 참가자는 기존 진입 경로가 생성합니다.
        for (int i = 0; i < (_data != null ? _data.GetPartyCount(false) : 0); i++)
        {
            CharacterData member = _data.Party[i];
            Color color = member != null ? member.BattleSymbolColor : Color.white;
            float x = 28f + i * 198f;
            Rect(CreateImage(content.transform, "Roster Card " + i, Panel).rectTransform, x, 277f, 188f, 25f);
            Rect(CreateImage(content.transform, "Roster Color " + i, color).rectTransform, x + 5f, 283f, 5f, 13f);
            string name = member != null ? member.ResolveDisplayName("위젤") : "참조 없음";
            Text(content.transform, "Roster Member " + i, name + " · 6스킬", x + 16f, 280f, 168f, 21f, 14f, color);
        }

        int count = _data != null && _data.Encounters != null ? _data.Encounters.Length : 0;
        _choiceBackgrounds = new Image[count];
        _choiceTitles = new TMP_Text[count];
        _choiceDetails = new TMP_Text[count];
        for (int i = 0; i < count; i++)
        {
            float y = 310f + i * 35f;
            _choiceBackgrounds[i] = CreateImage(content.transform, "Mode " + i, Panel);
            Rect(_choiceBackgrounds[i].rectTransform, 28f, y, 268f, 32f);
            _choiceTitles[i] = Text(content.transform, "Mode Title " + i, "", 39f, y + 1f, 249f, 18f, 16f, Color.white);
            _choiceDetails[i] = Text(content.transform, "Mode Detail " + i, _data.Encounters[i]?.Summary ?? "", 41f, y + 19f, 247f, 13f, 10f, Muted);
        }
        Rect(CreateImage(content.transform, "Description Background", Panel).rectTransform, 309f, 310f, 303f, 102f);
        _description = Text(content.transform, "Description", "", 320f, 315f, 281f, 92f, 13f, new Color(0.84f, 0.89f, 0.91f));
        _description.textWrappingMode = TextWrappingModes.Normal;
        _description.enableAutoSizing = true;
        _description.fontSizeMin = 11f;
        _description.fontSizeMax = 13f;
        Rect(CreateImage(content.transform, "Status Rule", new Color(0.19f, 0.27f, 0.31f)).rectTransform, 28f, 419f, 584f, 1f);
        _status = Text(content.transform, "Status", "", 28f, 423f, 584f, 31f, 12f, Mint);
        _status.textWrappingMode = TextWrappingModes.Normal;
        _footer = Text(content.transform, "Controls", "↑ ↓ 선택  Z 시작  X 처음으로  ·  전투 중 F8: 메뉴로", 28f, 456f, 584f, 20f, 13f, Color.white);
        // GraphicRaycaster나 Button을 만들지 않아 마우스로 조작할 수 없습니다.
    }

    private void ShowMenu(string status)
    {
        GameStateManager.Instance?.ChangeState(GameState.Cutscene);
        _status.text = status;
        RefreshSelection();
        SetMenuVisible(true);
        _inputAfterFrame = Time.frameCount + 1;
    }

    private void ShowStartupError(string message)
    {
        _description.text = "시작 조건을 확인해 주세요.\n\n독립 실험 씬을 Edit Mode에서\n열고 Play를 눌러야 합니다.";
        _status.text = message;
        _status.color = new Color(1f, 0.59f, 0.47f);
        _footer.text = "입력 비활성 · 기존 플레이 데이터는 변경하지 않았습니다.";
        SetMenuVisible(true);
        Debug.LogError("[BunnySlimeBattleLab] " + message, this);
    }

    private void RefreshSelection()
    {
        for (int i = 0; i < _choiceTitles.Length; i++)
        {
            bool selected = i == _selected;
            _choiceBackgrounds[i].color = selected ? Mint : Panel;
            _choiceTitles[i].color = selected ? Ink : Color.white;
            _choiceDetails[i].color = selected ? new Color(0.12f, 0.29f, 0.29f) : Muted;
            _choiceTitles[i].text = (selected ? "> " : "  ") + (i + 1).ToString("00") + "  " + _data.Encounters[i].Title;
        }
        _description.text = _data.Encounters[_selected].Description;
    }

    private void SetMenuVisible(bool visible)
    {
        _menuVisible = visible;
        _menuRoot.SetActive(visible);
        SetPartyPreviewVisible(visible);
    }

    private static Image CreateImage(Transform parent, string name, Color color)
    {
        var root = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        root.transform.SetParent(parent, false);
        Image image = root.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    private TMP_Text Text(Transform parent, string name, string value, float x, float y, float width, float height, float size, Color color)
    {
        var root = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        root.transform.SetParent(parent, false);
        var label = root.GetComponent<TextMeshProUGUI>();
        label.font = _font;
        label.text = value;
        label.fontSize = size;
        label.color = color;
        label.raycastTarget = false;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.overflowMode = TextOverflowModes.Ellipsis;
        label.alignment = TextAlignmentOptions.TopLeft;
        Rect(label.rectTransform, x, y, width, height);
        return label;
    }

    private static void Rect(RectTransform rect, float x, float y, float width, float height)
    {
        rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = new Vector2(x, -y);
        rect.sizeDelta = new Vector2(width, height);
    }
}
