using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed partial class BunnySlimeBattleLabSession
{
    private static readonly string[] ModeNames =
    {
        "종합 전투", "가드 · 회피", "연계 반격", "3 + 3  후열 확인"
    };
    private static readonly string[] ModeSummaries =
    {
        "대사 / 페이즈 / 스킬 / 상태이상",
        "Z 지속 가드 · 저스트 / X 회피",
        "C 성공 → 피격 대상 1명 반격",
        "전열 전멸 → 대기 후열 등장"
    };
    private static readonly string[] ModeDescriptions =
    {
        "훈련 상대는 토끼 슬라임입니다.\n\n대사와 페이즈 변화가 포함된\n전체 전투를 진행합니다.\n\n아군마다 다른 스킬을 선택하고\nHP/AP 회복 아이템도 사용하세요.\n장비는 미리 장착되어 있습니다.",
        "Z 유지: 피해를 줄이는 가드\nZ 정확한 입력: 저스트 가드\nX 정확한 입력: 무피해 회피\n\n근접과 투사체 3종이 섞여 나옵니다.\n단발 / 엇박자 2연발 / X 전용탄\n\n연타는 모션을 재시작하지 않으며\n전조 전 입력 뒤 다시 시도 가능합니다.",
        "적이 앞으로 나오면 전조를 보고\n타격 직전에 C를 누르세요.\n\n공격받은 한 명이 접근해서\n패링 → 공격 후 양쪽 복귀합니다.\n다른 아군은 자리를 유지합니다.\n\nZ로 막을 수 없는 특수 공격입니다.\n위험하면 X로 피할 수 있습니다.",
        "전열 3명의 HP를 1로 시작합니다.\n후열 3명은 미리 생성되어\n비활성 상태로 기다립니다.\n\n첫 광역 공격을 막지 말고\n전열 전멸과 후열 등장을 보세요.\n\n수동 교대 버튼은 없습니다.\n전투가 끝나면 모두 복구됩니다."
    };

    private static readonly Color Ink = new Color(0.045f, 0.075f, 0.11f);
    private static readonly Color Panel = new Color(0.09f, 0.14f, 0.19f);
    private static readonly Color Muted = new Color(0.58f, 0.69f, 0.74f);
    private static readonly Color Mint = new Color(0.44f, 0.94f, 0.78f);
    private GameObject _menuRoot;
    private bool _menuVisible;
    private TMP_Text _description;
    private TMP_Text _status;
    private TMP_Text _footer;
    private readonly Image[] _choiceBackgrounds = new Image[4];
    private readonly TMP_Text[] _choiceTitles = new TMP_Text[4];
    private readonly TMP_Text[] _choiceDetails = new TMP_Text[4];
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

        Image veil = CreateImage(_menuRoot.transform, "Backdrop", Ink);
        RectTransform veilRect = veil.rectTransform;
        veilRect.anchorMin = Vector2.zero;
        veilRect.anchorMax = Vector2.one;
        veilRect.offsetMin = veilRect.offsetMax = Vector2.zero;

        var content = new GameObject("640x480 Safe Content", typeof(RectTransform));
        content.transform.SetParent(_menuRoot.transform, false);
        RectTransform contentRect = content.GetComponent<RectTransform>();
        contentRect.anchorMin = contentRect.anchorMax = new Vector2(0.5f, 0.5f);
        contentRect.sizeDelta = new Vector2(640f, 480f);

        Rect(CreateImage(content.transform, "Top Rule", Mint).rectTransform, 28f, 24f, 44f, 3f);
        Text(content.transform, "Eyebrow", "HUB TO HOME  /  COMBAT FIELD NOTES", 28f, 32f, 560f, 20f, 13f, Muted);
        Text(content.transform, "Title", "토끼 슬라임 전투 실험실", 26f, 54f, 540f, 40f, 30f, Color.white);
        Text(content.transform, "Subtitle", "하나의 전투, 네 가지 확인 경로", 28f, 97f, 560f, 24f, 16f, Mint);

        for (int i = 0; i < ModeNames.Length; i++)
        {
            float y = 138f + i * 54f;
            _choiceBackgrounds[i] = CreateImage(content.transform, "Mode " + i, Panel);
            Rect(_choiceBackgrounds[i].rectTransform, 28f, y, 268f, 46f);
            _choiceTitles[i] = Text(content.transform, "Mode Title " + i, "", 41f, y + 4f, 247f, 23f, 21f, Color.white);
            _choiceDetails[i] = Text(content.transform, "Mode Detail " + i, ModeSummaries[i], 43f, y + 26f, 246f, 16f, 12f, Muted);
        }
        Rect(CreateImage(content.transform, "Description Background", Panel).rectTransform, 309f, 138f, 303f, 208f);
        _description = Text(content.transform, "Description", "", 323f, 149f, 275f, 188f, 16f, new Color(0.84f, 0.89f, 0.91f));
        _description.textWrappingMode = TextWrappingModes.Normal;
        Rect(CreateImage(content.transform, "Status Rule", new Color(0.19f, 0.27f, 0.31f)).rectTransform, 28f, 363f, 584f, 1f);
        _status = Text(content.transform, "Status", "", 28f, 376f, 584f, 42f, 15f, Mint);
        _status.textWrappingMode = TextWrappingModes.Normal;
        _footer = Text(content.transform, "Controls", "↑ ↓ 선택  Z 시작  X 처음으로  ·  전투 중 F8: 메뉴로", 28f, 431f, 584f, 22f, 15f, Color.white);
        Text(content.transform, "Safety", "전용 샘플 데이터 / 원본 불변 / 저장 파일 I/O 없음", 28f, 455f, 584f, 16f, 12f, Muted);
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
        for (int i = 0; i < ModeNames.Length; i++)
        {
            bool selected = i == _selected;
            _choiceBackgrounds[i].color = selected ? Mint : Panel;
            _choiceTitles[i].color = selected ? Ink : Color.white;
            _choiceDetails[i].color = selected ? new Color(0.12f, 0.29f, 0.29f) : Muted;
            _choiceTitles[i].text = (selected ? "> " : "  ") + (i + 1).ToString("00") + "  " + ModeNames[i];
        }
        _description.text = ModeDescriptions[_selected];
    }

    private void SetMenuVisible(bool visible)
    {
        _menuVisible = visible;
        _menuRoot.SetActive(visible);
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
