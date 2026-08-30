using System;
using System.Collections.Generic;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using DG.Tweening;
using UnityEngine.UI;

/// <summary>
/// C 메뉴의 설정 패널. SettingPanel Canvas를 FixedViewport로 정규화하며,
/// 프레임 안쪽의 카테고리/상세 콘텐츠 영역을 기준으로 레이아웃한다.
/// </summary>
public class ConfigPanelUI : UIPanel
{
    private enum Focus { Category, RowList, KeyCapture }
    private enum Category { Audio, Gameplay, Controls, System }
    private enum RowType
    {
        MasterVolume,
        BgmVolume,
        SfxVolume,
        Language,
        TextSpeed,
        AutoAdvance,
        ScreenShake,
        FlashIntensity,
        Fullscreen,
        WindowScale,
        VSync,
        TargetFps,
        ResetDefault,
        Key_Up,
        Key_Down,
        Key_Left,
        Key_Right,
        Key_Confirm,
        Key_Cancel,
        Key_Run,
        Key_Menu,
        ControlsResetDefault
    }

    [Serializable] private class CategoryLabel { public Category category; public TextMeshProUGUI text; }
    private class SpawnedRow { public RowType type; public GameObject go; public TextMeshProUGUI name; public TextMeshProUGUI value; }

    [Header("UI")]
    [SerializeField] private TextMeshProUGUI _titleText;
    [SerializeField] private List<CategoryLabel> _categories = new List<CategoryLabel>();
    [SerializeField] private Transform _detailRoot;
    [SerializeField] private GameObject _rowPrefab;
    [SerializeField] private ScrollRect _scrollRect;
    [SerializeField] private TextMeshProUGUI _gameplayPreviewText;

    [Header("Audio SFX")]
    [SerializeField] private AudioClip _moveSfx;
    [SerializeField] private AudioClip _selectSfx;

    [Header("Visual")]
    [SerializeField] private Color _normalColor = Color.white;
    [SerializeField] private Color _selectedColor = new Color(1f, 0.92f, 0.2f, 1f);
    [SerializeField] private Vector3 _normalScale = Vector3.one;
    [SerializeField] private Vector3 _selectedScale = new Vector3(1.08f, 1.08f, 1f);
    [SerializeField] private float _punch = 0.08f;
    [SerializeField] private float _punchDuration = 0.12f;

    private readonly List<SpawnedRow> _rows = new List<SpawnedRow>();
    private Focus _focus = Focus.Category;
    private Category _selectedCategory = Category.Audio;
    private int _rowIndex;
    private bool _skipOneFrame;
    private readonly Dictionary<TextMeshProUGUI, bool> _lastSelectedState = new Dictionary<TextMeshProUGUI, bool>();
    private readonly Dictionary<TextMeshProUGUI, float> _baseFontSize = new Dictionary<TextMeshProUGUI, float>();
    private Coroutine _textPreviewRoutine;
    private string _lastScrollContractError;
    private string _lastRowContractError;
    private bool _ownsModalState;
    private float _timeScaleBeforeOpen = 1f;
    private GameState _stateBeforeOpen = GameState.Exploration;

    private GameConfigManager Config { get { return GameConfigManager.EnsureInstance(); } }

    protected override void Awake()
    {
        UIRuntimeGuard.NormalizeCanvas(gameObject, GameConfigPolicy.ReferenceResolution);
        base.Awake();
        GameInput.SetConfigModalActive(false);
        if (_gameplayPreviewText != null)
            _gameplayPreviewText.maxVisibleLines = 2;
    }

    public override void Show()
    {
        AcquireModalState();
        base.Show();
        _focus = Focus.Category;
        _selectedCategory = Category.Audio;
        _rowIndex = 0;
        _skipOneFrame = true;

        RebuildRows();
        Refresh();
    }

    public override void Hide()
    {
        KillAllTweens();
        ClearRows();
        base.Hide();
        ReleaseModalState();
    }

    public override void HideImmediate()
    {
        KillAllTweens();
        ClearRows();
        ReleaseModalState();
        base.HideImmediate();
    }

    protected override void OnDisable()
    {
        KillAllTweens();
        OnDisableLanguageHook();
        ReleaseModalState();
        base.OnDisable();
    }

    protected override void OnDestroy()
    {
        KillAllTweens();
        ClearRows();
        ReleaseModalState();
        base.OnDestroy();
    }

    private void AcquireModalState()
    {
        if (!_ownsModalState)
        {
            _timeScaleBeforeOpen = Time.timeScale;
            GameStateManager stateManager = GameStateManager.Instance;
            if (stateManager != null)
                _stateBeforeOpen = stateManager.CurrentState;
            _ownsModalState = true;
        }

        GameInput.SetConfigModalActive(true);
        Time.timeScale = 0f;
        GameStateManager.Instance?.ChangeState(GameState.Paused);
    }

    private void ReleaseModalState()
    {
        GameInput.SetConfigModalActive(false);

        if (!_ownsModalState)
            return;

        _ownsModalState = false;
        Time.timeScale = _timeScaleBeforeOpen;

        GameStateManager stateManager = GameStateManager.Instance;
        if (stateManager != null && stateManager.CurrentState == GameState.Paused)
            stateManager.ChangeState(_stateBeforeOpen);
    }

    private void OnEnable()
    {
        LocalizationManager.LanguageChanged += HandleLanguageChanged;
    }

    private void OnDisableLanguageHook()
    {
        LocalizationManager.LanguageChanged -= HandleLanguageChanged;
    }

    private void HandleLanguageChanged(LanguageType _)
    {
        if (!IsVisible) return;
        Refresh();
    }

    private void Update()
    {
        if (!IsVisible) return;
        if (_skipOneFrame) { _skipOneFrame = false; return; }

        if (_focus == Focus.KeyCapture) { CaptureKey(); return; }

        if (GameInput.ConfigUpPressed) Move(-1);
        if (GameInput.ConfigDownPressed) Move(1);
        if (GameInput.ConfigLeftPressed) Adjust(-1);
        if (GameInput.ConfigRightPressed) Adjust(1);
        if (GameInput.ConfigSubmitPressed) Submit();
        if (GameInput.ConfigBackPressed) Back();
    }

    private void Move(int dir)
    {
        if (_focus == Focus.Category)
        {
            int count = Enum.GetValues(typeof(Category)).Length;
            _selectedCategory = (Category)(((int)_selectedCategory + dir + count) % count);
            _rowIndex = 0;
            RebuildRows();
        }
        else
        {
            if (_rows.Count == 0) return;
            _rowIndex = (_rowIndex + dir + _rows.Count) % _rows.Count;
        }
        AudioManager.Instance?.PlayUISFX(_moveSfx);
        Refresh();
        if (_selectedCategory == Category.Gameplay && _focus == Focus.RowList && _rows.Count > 0)
            TriggerGameplayRowPreview(_rows[_rowIndex].type);
    }

    private void Submit()
    {
        if (_focus == Focus.Category)
        {
            _focus = Focus.RowList;
            if (_selectedCategory == Category.Controls) _rowIndex = 0;
            AudioManager.Instance?.PlayUISFX(_selectSfx);
            Refresh();
            if (_selectedCategory == Category.Gameplay && _rows.Count > 0)
                TriggerGameplayRowPreview(_rows[_rowIndex].type);
            return;
        }

        if (_rows.Count == 0) return;
        RowType t = _rows[_rowIndex].type;

        if (IsKeyRow(t))
        {
            _focus = Focus.KeyCapture;
            _skipOneFrame = true;
            AudioManager.Instance?.PlayUISFX(_selectSfx);
            Refresh();
            return;
        }

        switch (t)
        {
            case RowType.Fullscreen: Config.SetFullscreen(!Config.IsFullscreen); break;
            case RowType.Language: CycleLanguage(1); break;
            case RowType.ResetDefault: Config.ResetDefaults(); break;
            case RowType.ControlsResetDefault: Config.ResetControlsDefaults(); break;
        }
        AudioManager.Instance?.PlayUISFX(_selectSfx);
        Refresh();
    }

    private void Adjust(int dir)
    {
        if (_focus != Focus.RowList || _rows.Count == 0) return;
        RowType t = _rows[_rowIndex].type;
        const float step = 0.05f;
        switch (t)
        {
            case RowType.MasterVolume: Config.SetMasterVolume(Config.MasterVolume + step * dir); break;
            case RowType.BgmVolume: Config.SetBgmVolume(Config.BgmVolume + step * dir); break;
            case RowType.SfxVolume: Config.SetSfxVolume(Config.SfxVolume + step * dir); break;
            case RowType.Language: CycleLanguage(dir); break;
            case RowType.TextSpeed: Config.SetTextSpeed(Config.TextSpeed + dir * 0.1f); break;
            case RowType.AutoAdvance: Config.SetAutoAdvance(!Config.AutoAdvance); break;
            case RowType.ScreenShake: Config.SetScreenShake(Config.ScreenShake + dir * 0.1f); break;
            case RowType.FlashIntensity: Config.SetFlashIntensity(Config.FlashIntensity + dir * 0.1f); break;
            case RowType.Fullscreen: Config.SetFullscreen(!Config.IsFullscreen); break;
            case RowType.WindowScale: Config.SetWindowScale(Config.WindowScale + dir); break;
            case RowType.VSync: Config.SetVSync(!Config.UseVSync); break;
            case RowType.TargetFps: Config.AdjustTargetFps(dir); break;
        }
        AudioManager.Instance?.PlayUISFX(_moveSfx);
        Refresh();
        TriggerGameplayRowPreview(t);
    }

    private void Back()
    {
        if (_focus == Focus.KeyCapture) { _focus = Focus.RowList; Refresh(); return; }
        if (_focus == Focus.RowList) { _focus = Focus.Category; Refresh(); return; }
        UIManager.Instance?.CloseTopPanel();
        if (UIManager.Instance == null) Hide();
    }

    private void CaptureKey()
    {
        if (!GameInput.TryReadPressedKey(out Key key)) return;
        if (key == Key.None || key == Key.Escape) return;
        ConfigurableAction action = RowToAction(_rows[_rowIndex].type);

        foreach (ConfigurableAction a in Enum.GetValues(typeof(ConfigurableAction)))
        {
            if (a == action) continue;
            if (Config.GetKey(a) == key) return;
        }

        Config.SetKey(action, key);
        _focus = Focus.RowList;
        Refresh();
    }

    private void RebuildRows()
    {
        ClearRows();
        if (!ValidateScrollContract())
            return;

        List<RowType> defs = GetRowsForCategory(_selectedCategory);
        for (int i = 0; i < defs.Count; i++)
        {
            GameObject go = Instantiate(_rowPrefab, _detailRoot);
            go.name = "Row_" + defs[i];
            if (!TryGetRowBindings(go, out TextMeshProUGUI nameText, out TextMeshProUGUI valueText, out string missing))
            {
                LogRowContractErrorOnce(missing);
                go.SetActive(false);
                DestroyRowObject(go);
                ClearRows();
                return;
            }

            _lastRowContractError = null;

            SpawnedRow row = new SpawnedRow();
            row.type = defs[i];
            row.go = go;
            row.name = nameText;
            row.value = valueText;
            _rows.Add(row);
        }

        if (_rowIndex >= _rows.Count) _rowIndex = Mathf.Max(0, _rows.Count - 1);
        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)_detailRoot);
        ResetScrollToTop();
    }

    private bool ValidateScrollContract()
    {
        string missing = null;
        RectTransform content = _detailRoot as RectTransform;
        RectTransform viewport = _scrollRect != null ? _scrollRect.viewport : null;

        if (_rowPrefab == null) missing = "rowPrefab";
        else if (_detailRoot == null) missing = "detailRoot/content";
        else if (content == null) missing = "detailRoot RectTransform";
        else if (_scrollRect == null) missing = "scrollRect";
        else if (viewport == null) missing = "viewport";
        else if (_scrollRect.content == null) missing = "scrollRect.content";
        else if (_scrollRect.content != content) missing = "content binding";
        else if (content == viewport || !content.IsChildOf(viewport)) missing = "content/viewport hierarchy";
        else if (viewport.GetComponent<RectMask2D>() == null) missing = "viewport RectMask2D";

        if (missing == null)
        {
            _lastScrollContractError = null;
            return true;
        }

        if (_lastScrollContractError != missing)
        {
            _lastScrollContractError = missing;
            Debug.LogError("[ConfigPanelUI] config_panel_scroll_contract_invalid: " + missing, this);
        }
        return false;
    }

    private void LogRowContractErrorOnce(string missing)
    {
        if (_lastRowContractError == missing)
            return;

        _lastRowContractError = missing;
        Debug.LogError("[ConfigPanelUI] config_panel_row_contract_invalid: " + missing, this);
    }

    private static bool TryGetRowBindings(
        GameObject rowObject,
        out TextMeshProUGUI nameText,
        out TextMeshProUGUI valueText,
        out string missing)
    {
        nameText = null;
        valueText = null;
        missing = null;

        if (rowObject == null)
        {
            missing = "row instance";
            return false;
        }

        if (rowObject.GetComponent<HorizontalLayoutGroup>() == null)
        {
            missing = "HorizontalLayoutGroup";
            return false;
        }

        if (rowObject.GetComponent<LayoutElement>() == null)
        {
            missing = "LayoutElement";
            return false;
        }

        TextMeshProUGUI[] texts = rowObject.GetComponentsInChildren<TextMeshProUGUI>(true);
        if (texts.Length != 2)
        {
            missing = "TMP children count=" + texts.Length + " (expected 2)";
            return false;
        }

        nameText = texts[0];
        valueText = texts[1];
        return true;
    }

    private void ResetScrollToTop()
    {
        RectTransform content = _scrollRect != null ? _scrollRect.content : null;
        if (content != null)
            content.anchoredPosition = new Vector2(content.anchoredPosition.x, 0f);

        if (_scrollRect == null) return;
        _scrollRect.StopMovement();
        _scrollRect.verticalNormalizedPosition = 1f;
    }

    private void ClearRows()
    {
        for (int i = 0; i < _rows.Count; i++)
        {
            if (_rows[i] == null || _rows[i].go == null) continue;
            _rows[i].go.SetActive(false);
            DestroyRowObject(_rows[i].go);
        }
        _rows.Clear();
        _lastSelectedState.Clear();
        _baseFontSize.Clear();
    }

    private static void DestroyRowObject(GameObject rowObject)
    {
        if (rowObject == null)
            return;

        if (Application.isPlaying)
            Destroy(rowObject);
        else
            DestroyImmediate(rowObject);
    }

    private void Refresh()
    {
        if (_titleText != null) _titleText.text = "CONFIG";
        RefreshGameplayPreview();

        for (int i = 0; i < _categories.Count; i++)
        {
            var c = _categories[i];
            if (c == null || c.text == null) continue;
            ApplyVisual(c.text, c.category == _selectedCategory);
        }

        for (int i = 0; i < _rows.Count; i++)
        {
            SpawnedRow r = _rows[i];
            SetRowText(r);
            bool selected = _focus != Focus.Category && i == _rowIndex;
            ApplyVisual(r.name, selected);
            if (r.value != null) ApplyVisual(r.value, selected);
        }

        EnsureSelectedRowVisible();
    }

    private void RefreshGameplayPreview()
    {
        var preview = _gameplayPreviewText;
        if (preview == null) return;
        bool isGameplay = _selectedCategory == Category.Gameplay;
        preview.gameObject.SetActive(isGameplay);
        if (!isGameplay) return;

        string sample = L("config.preview.sample", "예시 테스트입니다! 확인해주세요!");
        if (_rows.Count > 0 && _rowIndex >= 0 && _rowIndex < _rows.Count)
        {
            RowType current = _rows[_rowIndex].type;
            if (current == RowType.TextSpeed)
            {
                preview.text = sample;
                PlayTextSpeedPreview();
                return;
            }

            if (current == RowType.ScreenShake)
            {
                preview.text = sample;
                PlayScreenShakeTextPreview();
                return;
            }

            if (current == RowType.FlashIntensity)
            {
                preview.text = sample;
                PlayFlashTextPreview();
                return;
            }
        }

        preview.text = sample;
        preview.DOKill();
        preview.rectTransform.DOKill();
        preview.maxVisibleCharacters = int.MaxValue;
        preview.rectTransform.localPosition = Vector3.zero;
        preview.color = _normalColor;
    }

    private void TriggerGameplayRowPreview(RowType rowType)
    {
        if (_selectedCategory != Category.Gameplay) return;
        if (rowType == RowType.TextSpeed) PlayTextSpeedPreview();
        else if (rowType == RowType.ScreenShake) PlayScreenShakeTextPreview();
        else if (rowType == RowType.FlashIntensity) PlayFlashTextPreview();
    }

    private void PlayTextSpeedPreview()
    {
        var preview = _gameplayPreviewText;
        if (preview == null) return;
        preview.DOKill();
        preview.rectTransform.DOKill();
        preview.rectTransform.localPosition = Vector3.zero;
        preview.maxVisibleCharacters = int.MaxValue;
        preview.color = _normalColor;

        if (_textPreviewRoutine != null) StopCoroutine(_textPreviewRoutine);
        _textPreviewRoutine = StartCoroutine(CoTypePreview(preview));
    }

    private IEnumerator CoTypePreview(TextMeshProUGUI preview)
    {
        string full = L("config.preview.sample", "예시 테스트입니다! 확인해주세요!");
        preview.text = string.Empty;

        float cps = Mathf.Lerp(8f, 40f, (Config.TextSpeed - 0.5f) / 1.5f);
        float delay = 1f / Mathf.Max(1f, cps);

        for (int i = 1; i <= full.Length; i++)
        {
            preview.text = full.Substring(0, i);
            yield return new WaitForSecondsRealtime(delay);
        }

        _textPreviewRoutine = null;
    }

    private void PlayScreenShakeTextPreview()
    {
        var preview = _gameplayPreviewText;
        if (preview == null) return;
        preview.DOKill();
        preview.rectTransform.DOKill();
        if (_textPreviewRoutine != null) StopCoroutine(_textPreviewRoutine);

        int percent = Mathf.RoundToInt(Config.ScreenShake * 100f);
        string sample = L("config.preview.sample", "예시 테스트입니다! 확인해주세요!");
        preview.text = sample + "\nSHAKE: " + percent + "%";
        preview.maxVisibleCharacters = int.MaxValue;
        preview.color = _selectedColor;
        preview.DOColor(_normalColor, 0.2f);
        preview.rectTransform.localPosition = Vector3.zero;
        float strength = Mathf.Lerp(0f, 8f, Config.ScreenShake);
        preview.rectTransform.DOShakeAnchorPos(
                0.25f,
                new Vector2(strength, strength),
                12,
                90f,
                false,
                true)
            .SetUpdate(true);
    }

    private void PlayFlashTextPreview()
    {
        var preview = _gameplayPreviewText;
        if (preview == null) return;

        preview.DOKill();
        preview.rectTransform.DOKill();
        if (_textPreviewRoutine != null)
        {
            StopCoroutine(_textPreviewRoutine);
            _textPreviewRoutine = null;
        }

        int percent = Mathf.RoundToInt(Config.FlashIntensity * 100f);
        preview.text = L("config.preview.sample", "예시 테스트입니다! 확인해주세요!")
            + "\nFLASH: " + percent + "%";
        preview.maxVisibleCharacters = int.MaxValue;
        preview.rectTransform.localPosition = Vector3.zero;
        preview.color = _normalColor;

        float dimmedAlpha = Mathf.Lerp(1f, 0.15f, Config.FlashIntensity);
        DOTween.Sequence()
            .SetUpdate(true)
            .SetTarget(preview)
            .Append(preview.DOFade(dimmedAlpha, 0.08f))
            .Append(preview.DOFade(1f, 0.12f));
    }

    private void EnsureSelectedRowVisible()
    {
        if (_scrollRect == null || _rows.Count <= 1) return;
        if (_focus == Focus.Category) return;

        Canvas.ForceUpdateCanvases();
        RectTransform content = _scrollRect.content;
        RectTransform viewport = _scrollRect.viewport;
        if (content == null || viewport == null) return;

        LayoutRebuilder.ForceRebuildLayoutImmediate(content);

        if (_rowIndex < 0 || _rowIndex >= _rows.Count) return;
        SpawnedRow selected = _rows[_rowIndex];
        if (selected == null || selected.go == null) return;

        RectTransform row = selected.go.transform as RectTransform;
        if (row == null) return;

        float contentH = content.rect.height;
        float viewportH = viewport.rect.height;
        if (contentH <= viewportH + 0.01f) return;

        Vector3 worldCenter = row.TransformPoint(row.rect.center);
        Vector3 localInContent = content.InverseTransformPoint(worldCenter);
        _scrollRect.verticalNormalizedPosition = CalculateVerticalNormalizedPosition(
            content.rect.yMax,
            localInContent.y,
            contentH,
            viewportH);
    }

    private static float CalculateVerticalNormalizedPosition(
        float contentYMax,
        float rowCenterY,
        float contentHeight,
        float viewportHeight)
    {
        float maxTop = Mathf.Max(0f, contentHeight - viewportHeight);
        if (maxTop <= 0.001f)
            return 1f;

        float centerFromTop = contentYMax - rowCenterY;
        float targetTop = Mathf.Clamp(centerFromTop - (viewportHeight * 0.5f), 0f, maxTop);
        return Mathf.Clamp01(1f - (targetTop / maxTop));
    }

    private void SetRowText(SpawnedRow r)
    {
        switch (r.type)
        {
            case RowType.MasterVolume: SetRow(r, L("config.master", "Master Volume"), ToPercent(Config.MasterVolume)); break;
            case RowType.BgmVolume: SetRow(r, L("config.bgm", "BGM Volume"), ToPercent(Config.BgmVolume)); break;
            case RowType.SfxVolume: SetRow(r, L("config.sfx", "SFX Volume"), ToPercent(Config.SfxVolume)); break;
            case RowType.Language: SetRow(r, L("config.language", "Language"), Config.Language.ToString()); break;
            case RowType.TextSpeed: SetRow(r, L("config.text_speed", "Text Speed"), string.Format("{0:0.0}x", Config.TextSpeed)); break;
            case RowType.AutoAdvance: SetRow(r, L("config.auto_advance", "Auto Advance"), Config.AutoAdvance ? L("common.on", "ON") : L("common.off", "OFF")); break;
            case RowType.ScreenShake: SetRow(r, L("config.screen_shake", "Screen Shake"), ToPercent(Config.ScreenShake)); break;
            case RowType.FlashIntensity: SetRow(r, L("config.flash_intensity", "Flash Intensity"), ToPercent(Config.FlashIntensity)); break;
            case RowType.Fullscreen: SetRow(r, L("config.fullscreen", "Fullscreen"), Config.IsFullscreen ? L("common.on", "ON") : L("common.off", "OFF")); break;
            case RowType.WindowScale: SetRow(r, L("config.window_size", "Window Size"), Config.WindowSize.x + " x " + Config.WindowSize.y); break;
            case RowType.VSync: SetRow(r, L("config.vsync", "VSync"), Config.UseVSync ? L("common.on", "ON") : L("common.off", "OFF")); break;
            case RowType.TargetFps: SetRow(r, L("config.target_fps", "Target FPS"), Config.TargetFps.ToString()); break;
            case RowType.ResetDefault: SetRow(r, L("config.reset_default", "Reset Default"), ""); break;
            case RowType.Key_Up: SetKeyRow(r, ConfigurableAction.Up); break;
            case RowType.Key_Down: SetKeyRow(r, ConfigurableAction.Down); break;
            case RowType.Key_Left: SetKeyRow(r, ConfigurableAction.Left); break;
            case RowType.Key_Right: SetKeyRow(r, ConfigurableAction.Right); break;
            case RowType.Key_Confirm: SetKeyRow(r, ConfigurableAction.Confirm); break;
            case RowType.Key_Cancel: SetKeyRow(r, ConfigurableAction.Cancel); break;
            case RowType.Key_Run: SetKeyRow(r, ConfigurableAction.Run); break;
            case RowType.Key_Menu: SetKeyRow(r, ConfigurableAction.Menu); break;
            case RowType.ControlsResetDefault: SetRow(r, L("config.reset_controls", "Reset Controls"), ""); break;
        }
    }

    private void SetKeyRow(SpawnedRow r, ConfigurableAction action)
    {
        string wait = (_focus == Focus.KeyCapture && _rows[_rowIndex] == r) ? " ..." : "";
        SetRow(r, ActionLabel(action), Config.GetKey(action).ToString() + wait);
    }

    private static string ActionLabel(ConfigurableAction action)
    {
        switch (action)
        {
            case ConfigurableAction.Up: return LStatic("config.key.up", "Move Up");
            case ConfigurableAction.Down: return LStatic("config.key.down", "Move Down");
            case ConfigurableAction.Left: return LStatic("config.key.left", "Move Left");
            case ConfigurableAction.Right: return LStatic("config.key.right", "Move Right");
            case ConfigurableAction.Confirm: return LStatic("config.key.confirm", "Confirm");
            case ConfigurableAction.Cancel: return LStatic("config.key.cancel", "Cancel");
            case ConfigurableAction.Run: return LStatic("config.key.run", "Run");
            case ConfigurableAction.Menu: return LStatic("config.key.menu", "Menu");
            default: return action.ToString();
        }
    }

    private string L(string key, string fallback)
    {
        if (LocalizationManager.Instance != null)
        {
            string text = LocalizationManager.Instance.GetText(key, fallback);
            if (!string.IsNullOrEmpty(text) && text != fallback) return text;
        }
        return LocalFallback(key, fallback);
    }

    private static string LStatic(string key, string fallback)
    {
        if (LocalizationManager.Instance != null)
        {
            string text = LocalizationManager.Instance.GetText(key, fallback);
            if (!string.IsNullOrEmpty(text) && text != fallback) return text;
        }
        LanguageType lang = GameConfigManager.EnsureInstance().Language;
        return LocalFallbackStatic(key, fallback, lang);
    }

    private string LocalFallback(string key, string fallback)
    {
        return LocalFallbackStatic(key, fallback, Config.Language);
    }

    private static string LocalFallbackStatic(string key, string fallback, LanguageType lang)
    {
        if (lang == LanguageType.KR)
        {
            switch (key)
            {
                case "config.language": return "언어";
                case "config.text_speed": return "텍스트 속도";
                case "config.auto_advance": return "자동 진행";
                case "config.screen_shake": return "화면 흔들림";
                case "config.flash_intensity": return "점멸 강도";
                case "config.fullscreen": return "전체화면";
                case "config.window_size": return "창 크기";
                case "config.vsync": return "수직동기화";
                case "config.target_fps": return "목표 FPS";
                case "config.reset_default": return "기본값 초기화";
                case "config.reset_controls": return "조작키 초기화";
                case "common.on": return "켜짐";
                case "common.off": return "꺼짐";
            }
        }
        return fallback;
    }

    private static void SetRow(SpawnedRow r, string name, string value)
    {
        if (r.name != null) r.name.text = name;
        if (r.value != null) r.value.text = value;
    }

    private void ApplyVisual(TextMeshProUGUI text, bool selected)
    {
        if (text == null) return;

        if (!_baseFontSize.ContainsKey(text))
            _baseFontSize[text] = text.fontSize;

        text.color = selected ? _selectedColor : _normalColor;
        text.fontSize = _baseFontSize[text];
        text.fontStyle = FontStyles.Normal;

        RectTransform rt = text.rectTransform;
        if (rt == null) return;

        Vector3 targetScale = selected ? _selectedScale : _normalScale;

        bool wasSelected;
        if (!_lastSelectedState.TryGetValue(text, out wasSelected)) wasSelected = !selected;

        rt.DOKill();
        rt.localScale = targetScale;

        // 선택 상태가 바뀔 때만 펀치(매 프레임 리프레시로 애니메이션 상쇄 방지)
        if (selected && !wasSelected)
            rt.DOPunchScale(Vector3.one * _punch, _punchDuration, 4, 0.4f);

        _lastSelectedState[text] = selected;
    }

    private void KillAllTweens()
    {
        for (int i = 0; i < _categories.Count; i++) _categories[i]?.text?.rectTransform?.DOKill();

        for (int i = 0; i < _rows.Count; i++)
        {
            _rows[i]?.name?.rectTransform?.DOKill();
            _rows[i]?.value?.rectTransform?.DOKill();
        }

        if (_gameplayPreviewText != null)
        {
            _gameplayPreviewText.DOKill();
            _gameplayPreviewText.rectTransform.DOKill();
        }

        if (_textPreviewRoutine == null) return;
        StopCoroutine(_textPreviewRoutine);
        _textPreviewRoutine = null;
    }

    private void CycleLanguage(int dir)
    {
        int count = Enum.GetValues(typeof(LanguageType)).Length;
        int next = ((int)Config.Language + dir + count) % count;
        Config.SetLanguage((LanguageType)next);
    }

    private static bool IsKeyRow(RowType t)
    {
        return t == RowType.Key_Up || t == RowType.Key_Down || t == RowType.Key_Left || t == RowType.Key_Right
            || t == RowType.Key_Confirm || t == RowType.Key_Cancel || t == RowType.Key_Run || t == RowType.Key_Menu;
    }

    private static ConfigurableAction RowToAction(RowType t)
    {
        switch (t)
        {
            case RowType.Key_Up: return ConfigurableAction.Up;
            case RowType.Key_Down: return ConfigurableAction.Down;
            case RowType.Key_Left: return ConfigurableAction.Left;
            case RowType.Key_Right: return ConfigurableAction.Right;
            case RowType.Key_Confirm: return ConfigurableAction.Confirm;
            case RowType.Key_Cancel: return ConfigurableAction.Cancel;
            case RowType.Key_Run: return ConfigurableAction.Run;
            case RowType.Key_Menu: return ConfigurableAction.Menu;
            default: return ConfigurableAction.Up;
        }
    }

    private static List<RowType> GetRowsForCategory(Category c)
    {
        if (c == Category.Audio) return new List<RowType> { RowType.MasterVolume, RowType.BgmVolume, RowType.SfxVolume };
        if (c == Category.Gameplay) return new List<RowType> { RowType.Language, RowType.TextSpeed, RowType.AutoAdvance, RowType.ScreenShake, RowType.FlashIntensity };
        if (c == Category.Controls) return new List<RowType> { RowType.Key_Up, RowType.Key_Down, RowType.Key_Left, RowType.Key_Right, RowType.Key_Confirm, RowType.Key_Cancel, RowType.Key_Run, RowType.Key_Menu, RowType.ControlsResetDefault };
        return new List<RowType> { RowType.Fullscreen, RowType.WindowScale, RowType.VSync, RowType.TargetFps, RowType.ResetDefault };
    }

    private static string ToPercent(float v) { return Mathf.RoundToInt(v * 100f) + "%"; }
}
