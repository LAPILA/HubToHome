using System;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// 키보드 전용 공용 설정 창.
/// ↑/↓: 항목 이동, Z: 선택/토글/키 변경 시작, ←/→: 값 조절, X: 뒤로가기.
/// </summary>
public class ConfigPanelUI : UIPanel
{
    private enum ConfigRow
    {
        MasterVolume,
        BgmVolume,
        SfxVolume,
        Controls,
        Fullscreen,
        Language,
        ResetDefault,
        BackToTitle,
        Back
    }

    [Header("Config UI")]
    [SerializeField] private TextMeshProUGUI _titleText;
    [SerializeField] private TextMeshProUGUI _bodyText;
    [SerializeField] private TextMeshProUGUI _helpText;

    [Header("Scene")]
    [SerializeField] private string _titleSceneName = "00_TitleScene";

    private ConfigRow _selectedRow = ConfigRow.MasterVolume;
    private ConfigurableAction _selectedControl = ConfigurableAction.Up;
    private bool _isEditingControls;
    private bool _isWaitingForKey;
    private bool _skipOneFrame;

    private GameConfigManager Config => GameConfigManager.EnsureInstance();

    protected override void Awake()
    {
        base.Awake();
        EnsureRuntimeLayout();
    }

    public override void Show()
    {
        base.Show();
        _selectedRow = ConfigRow.MasterVolume;
        _isEditingControls = false;
        _isWaitingForKey = false;
        Refresh();
    }

    private void Update()
    {
        if (!IsVisible) return;

        if (_skipOneFrame)
        {
            _skipOneFrame = false;
            return;
        }

        if (_isWaitingForKey)
        {
            TryCaptureKey();
            return;
        }

        if (GameInput.ConfigUpPressed) MoveSelection(-1);
        if (GameInput.ConfigDownPressed) MoveSelection(1);
        if (GameInput.ConfigLeftPressed) AdjustSelected(-1);
        if (GameInput.ConfigRightPressed) AdjustSelected(1);
        if (GameInput.ConfigSubmitPressed) ConfirmSelected();
        if (GameInput.ConfigBackPressed) Back();
    }

    private void MoveSelection(int direction)
    {
        if (_isEditingControls)
        {
            int nextAction = Mathf.Clamp((int)_selectedControl + direction, 0, Enum.GetValues(typeof(ConfigurableAction)).Length - 1);
            _selectedControl = (ConfigurableAction)nextAction;
        }
        else
        {
            int rowCount = Enum.GetValues(typeof(ConfigRow)).Length;
            _selectedRow = (ConfigRow)(((int)_selectedRow + direction + rowCount) % rowCount);
        }

        Refresh();
    }

    private void AdjustSelected(int direction)
    {
        const float step = 0.05f;

        switch (_selectedRow)
        {
            case ConfigRow.MasterVolume:
                Config.SetMasterVolume(Config.MasterVolume + step * direction);
                break;
            case ConfigRow.BgmVolume:
                Config.SetBgmVolume(Config.BgmVolume + step * direction);
                break;
            case ConfigRow.SfxVolume:
                Config.SetSfxVolume(Config.SfxVolume + step * direction);
                break;
            case ConfigRow.Fullscreen:
                Config.SetFullscreen(!Config.IsFullscreen);
                break;
            case ConfigRow.Language:
                CycleLanguage(direction);
                break;
        }

        Refresh();
    }

    private void ConfirmSelected()
    {
        if (_isEditingControls)
        {
            _isWaitingForKey = true;
            _skipOneFrame = true;
            Refresh();
            return;
        }

        switch (_selectedRow)
        {
            case ConfigRow.Controls:
                _isEditingControls = true;
                _selectedControl = ConfigurableAction.Up;
                break;
            case ConfigRow.Fullscreen:
                Config.SetFullscreen(!Config.IsFullscreen);
                break;
            case ConfigRow.Language:
                CycleLanguage(1);
                break;
            case ConfigRow.ResetDefault:
                Config.ResetDefaults();
                break;
            case ConfigRow.BackToTitle:
                SceneLoader.Instance?.LoadScene(_titleSceneName);
                break;
            case ConfigRow.Back:
                Back();
                break;
        }

        Refresh();
    }

    private void Back()
    {
        if (_isWaitingForKey)
        {
            _isWaitingForKey = false;
            Refresh();
            return;
        }

        if (_isEditingControls)
        {
            _isEditingControls = false;
            Refresh();
            return;
        }

        UIManager.Instance?.CloseTopPanel();
        if (UIManager.Instance == null) Hide();
    }

    private void TryCaptureKey()
    {
        if (!GameInput.TryReadPressedKey(out Key key)) return;
        if (key == Key.Escape || key == Key.None) return;
        if (IsForbiddenDefaultKey(key)) return;

        Config.SetKey(_selectedControl, key);
        _isWaitingForKey = false;
        Refresh();
    }

    private static bool IsForbiddenDefaultKey(Key key)
    {
        return key == Key.W || key == Key.A || key == Key.S || key == Key.D;
    }

    private void CycleLanguage(int direction)
    {
        int count = Enum.GetValues(typeof(LanguageType)).Length;
        int next = ((int)Config.Language + direction + count) % count;
        Config.SetLanguage((LanguageType)next);
    }

    private void Refresh()
    {
        if (_titleText != null) _titleText.text = "CONFIG";
        if (_helpText != null)
        {
            _helpText.text = _isWaitingForKey
                ? "Press any key. X: cancel"
                : "↑↓ Move   ←→ Change   Z Select   X Back";
        }

        if (_bodyText == null) return;

        var builder = new StringBuilder();

        if (_isEditingControls)
        {
            builder.AppendLine("<b>CONTROLS</b>");
            foreach (ConfigurableAction action in Enum.GetValues(typeof(ConfigurableAction)))
            {
                string cursor = action == _selectedControl ? ">" : " ";
                string wait = _isWaitingForKey && action == _selectedControl ? "  ..." : string.Empty;
                builder.AppendLine($"{cursor} {action,-8} : {Config.GetKey(action)}{wait}");
            }
            builder.AppendLine();
            builder.AppendLine("Z: change key / X: back");
        }
        else
        {
            AppendRow(builder, ConfigRow.MasterVolume, $"Master Volume  {ToPercent(Config.MasterVolume)}");
            AppendRow(builder, ConfigRow.BgmVolume, $"BGM Volume     {ToPercent(Config.BgmVolume)}");
            AppendRow(builder, ConfigRow.SfxVolume, $"SFX Volume     {ToPercent(Config.SfxVolume)}");
            AppendRow(builder, ConfigRow.Controls, "Controls       >");
            AppendRow(builder, ConfigRow.Fullscreen, $"Fullscreen     {(Config.IsFullscreen ? "ON" : "OFF")}");
            AppendRow(builder, ConfigRow.Language, $"Language       {Config.Language}");
            AppendRow(builder, ConfigRow.ResetDefault, "Reset Default");
            AppendRow(builder, ConfigRow.BackToTitle, "Back To Title");
            AppendRow(builder, ConfigRow.Back, "Back");
        }

        _bodyText.text = builder.ToString();
    }

    private void AppendRow(StringBuilder builder, ConfigRow row, string label)
    {
        builder.AppendLine($"{(row == _selectedRow ? ">" : " ")} {label}");
    }

    private static string ToPercent(float value) => $"{Mathf.RoundToInt(value * 100f)}%";

    private void EnsureRuntimeLayout()
    {
        if (_bodyText != null) return;

        var rect = GetComponent<RectTransform>();
        if (rect == null) rect = gameObject.AddComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        Image bg = GetComponent<Image>();
        if (bg == null) bg = gameObject.AddComponent<Image>();
        bg.color = new Color(0f, 0f, 0f, 0.85f);

        _titleText = CreateText("Title", "CONFIG", 44, TextAlignmentOptions.Center, new Vector2(0.5f, 0.85f), new Vector2(760f, 80f));
        _bodyText = CreateText("Body", string.Empty, 28, TextAlignmentOptions.TopLeft, new Vector2(0.5f, 0.48f), new Vector2(760f, 420f));
        _helpText = CreateText("Help", string.Empty, 20, TextAlignmentOptions.Center, new Vector2(0.5f, 0.12f), new Vector2(900f, 60f));
    }

    private TextMeshProUGUI CreateText(string name, string text, int fontSize, TextAlignmentOptions alignment, Vector2 anchor, Vector2 size)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(transform, false);

        var rect = go.GetComponent<RectTransform>();
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.sizeDelta = size;
        rect.anchoredPosition = Vector2.zero;

        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = fontSize;
        tmp.color = Color.white;
        tmp.alignment = alignment;
        return tmp;
    }

    public static ConfigPanelUI CreateRuntime(Canvas parentCanvas = null)
    {
        if (parentCanvas == null) parentCanvas = FindObjectOfType<Canvas>();

        if (parentCanvas == null)
        {
            var canvasObject = new GameObject("ConfigCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            parentCanvas = canvasObject.GetComponent<Canvas>();
            parentCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            DontDestroyOnLoad(canvasObject);
        }

        var panelObject = new GameObject("ConfigPanel", typeof(RectTransform), typeof(CanvasGroup), typeof(Image));
        panelObject.transform.SetParent(parentCanvas.transform, false);
        var panel = panelObject.AddComponent<ConfigPanelUI>();
        return panel;
    }
}