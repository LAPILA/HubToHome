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
        // UI에서는 사용하지 않지만 기존 행 식별자의 숫자값을 보존한다.
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
    private class SpawnedRow { public RowType type; public GameObject go; public TextMeshProUGUI name; public TextMeshProUGUI value; public Image background; }

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
    [SerializeField] private Color _normalColor = new Color(0.8f, 0.86f, 0.9f, 1f);
    [SerializeField] private Color _selectedColor = new Color(0.5f, 0.92f, 0.84f, 1f);
    [SerializeField] private Color _rowNormalColor = new Color(0.12f, 0.18f, 0.22f, 1f);
    [SerializeField] private Color _rowSelectedColor = new Color(0.1f, 0.3f, 0.31f, 1f);

    private readonly List<SpawnedRow> _rows = new List<SpawnedRow>();
    private Focus _focus = Focus.Category;
    private Category _selectedCategory = Category.Audio;
    private int _rowIndex;
    private bool _skipOneFrame;
    private bool _keyCaptureConflict;
    private static readonly Color UnavailableColor = new Color(0.42f, 0.48f, 0.5f, 1f);
    private Vector2 _previewAnchoredPosition;
    private bool _hasPreviewBaseline;
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
        {
            _gameplayPreviewText.maxVisibleLines = 2;
            RestorePreviewPosition();
        }
    }

    public override void Show()
    {
        AcquireModalState();
        base.Show();
        _focus = Focus.Category;
        _selectedCategory = Category.Audio;
        _rowIndex = 0;
        _skipOneFrame = true;
        _keyCaptureConflict = false;

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
        if (!IsRowAvailable(t)) return;

        if (IsKeyRow(t))
        {
            _focus = Focus.KeyCapture;
            _keyCaptureConflict = false;
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
        if (!IsRowAvailable(t)) return;
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
        if (!CanRebindKeyboard)
        {
            _focus = Focus.RowList;
            Refresh();
            return;
        }
        if (!GameInput.TryReadPressedKey(out Key key)) return;
        if (key == Key.Escape)
        {
            _focus = Focus.RowList;
            _keyCaptureConflict = false;
            Refresh();
            return;
        }
        if (key == Key.None) return;
        ConfigurableAction action = RowToAction(_rows[_rowIndex].type);

        foreach (ConfigurableAction a in Enum.GetValues(typeof(ConfigurableAction)))
        {
            if (a == action) continue;
            if (Config.GetKey(a) == key)
            {
                _keyCaptureConflict = true;
                Refresh();
                return;
            }
        }

        Config.SetKey(action, key);
        _keyCaptureConflict = false;
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
            row.background = go.GetComponent<Image>();
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
        if (_titleText != null) _titleText.text = L("config.title", "SETTINGS");
        RefreshGameplayPreview();

        for (int i = 0; i < _categories.Count; i++)
        {
            var c = _categories[i];
            if (c == null || c.text == null) continue;
            switch (c.category)
            {
                case Category.Audio: c.text.text = L("config.category.audio", "Audio"); break;
                case Category.Gameplay: c.text.text = L("config.category.gameplay", "Gameplay"); break;
                case Category.Controls: c.text.text = L("config.category.controls", "Controls"); break;
                case Category.System: c.text.text = L("config.category.system", "System"); break;
            }
            ApplyVisual(c.text, c.category == _selectedCategory);
        }

        for (int i = 0; i < _rows.Count; i++)
        {
            SpawnedRow r = _rows[i];
            SetRowText(r);
            bool selected = _focus != Focus.Category && i == _rowIndex;
            bool available = IsRowAvailable(r.type);
            if (!available && r.value != null)
                r.value.text = L("config.value.unavailable", "Unavailable");
            ApplyVisual(r.name, selected, available);
            if (r.value != null) ApplyVisual(r.value, selected, available);
            if (r.background != null)
                r.background.color = selected ? _rowSelectedColor : _rowNormalColor;
        }

        EnsureSelectedRowVisible();
    }

    private void RefreshGameplayPreview()
    {
        var preview = _gameplayPreviewText;
        if (preview == null) return;
        KillAllTweens();
        preview.maxVisibleCharacters = int.MaxValue;
        preview.color = _normalColor;
        bool isGameplay = _selectedCategory == Category.Gameplay;
        bool controlsHint = _selectedCategory == Category.Controls;
        bool displayHint = _selectedCategory == Category.System && !SupportsWindowSettings(Application.platform);
        preview.gameObject.SetActive(isGameplay || controlsHint || displayHint);
        if (controlsHint)
        {
            preview.text = _focus == Focus.KeyCapture
                ? L(_keyCaptureConflict ? "config.key.duplicate" : "config.key.capture_hint",
                    _keyCaptureConflict ? "Already used" : "Press a new key. Esc to cancel.")
                : CanRebindKeyboard
                    ? L("config.controls.keyboard_hint", "Keyboard bindings.")
                    : L("config.controls.fixed_hint", "Controls use the default layout.");
            return;
        }
        if (displayHint)
        {
            preview.text = L("config.display.fixed_hint", "Display size is managed by this device.");
            return;
        }
        if (!isGameplay) return;

        string sample = L("config.preview.sample", "Dialogue appears at this speed.");
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
        RestorePreviewPosition();
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
        RestorePreviewPosition();
        preview.maxVisibleCharacters = int.MaxValue;
        preview.color = _normalColor;

        if (_textPreviewRoutine != null) StopCoroutine(_textPreviewRoutine);
        _textPreviewRoutine = StartCoroutine(CoTypePreview(preview));
    }

    private IEnumerator CoTypePreview(TextMeshProUGUI preview)
    {
        string full = L("config.preview.sample", "Dialogue appears at this speed.");
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
        if (_textPreviewRoutine != null)
        {
            StopCoroutine(_textPreviewRoutine);
            _textPreviewRoutine = null;
        }

        int percent = Mathf.RoundToInt(Config.ScreenShake * 100f);
        string sample = L("config.preview.sample", "Dialogue appears at this speed.");
        preview.text = sample + "\n" + string.Format(L("config.preview.shake", "Shake: {0}%"), percent);
        preview.maxVisibleCharacters = int.MaxValue;
        preview.color = _selectedColor;
        preview.DOColor(_normalColor, 0.2f).SetUpdate(true);
        RestorePreviewPosition();
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
        preview.text = L("config.preview.sample", "Dialogue appears at this speed.")
            + "\n" + string.Format(L("config.preview.flash", "Flash: {0}%"), percent);
        preview.maxVisibleCharacters = int.MaxValue;
        RestorePreviewPosition();
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
            case RowType.Language: SetRow(r, L("config.language", "Language"), LanguageDisplayName(Config.Language)); break;
            case RowType.TextSpeed: SetRow(r, L("config.text_speed", "Text Speed"), string.Format("{0:0.0}x", Config.TextSpeed)); break;
            case RowType.AutoAdvance: SetRow(r, L("config.auto_advance", "Auto Advance"), Config.AutoAdvance ? L("common.on", "ON") : L("common.off", "OFF")); break;
            case RowType.ScreenShake: SetRow(r, L("config.screen_shake", "Screen Shake"), ToPercent(Config.ScreenShake)); break;
            case RowType.FlashIntensity: SetRow(r, L("config.flash_intensity", "Flash Intensity"), ToPercent(Config.FlashIntensity)); break;
            case RowType.Fullscreen: SetRow(r, L("config.fullscreen", "Fullscreen"), Config.IsFullscreen ? L("common.on", "ON") : L("common.off", "OFF")); break;
            case RowType.WindowScale: SetRow(r, L("config.window_size", "Window Size"), Config.WindowSize.x + " x " + Config.WindowSize.y); break;
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
        bool waiting = _focus == Focus.KeyCapture && _rowIndex < _rows.Count && _rows[_rowIndex] == r;
        string value = waiting
            ? L("config.key.capture", "Press a key")
            : KeyDisplayName(Config.GetKey(action));
        SetRow(r, ActionLabel(action), value);
    }

    private static string LanguageDisplayName(LanguageType language)
    {
        switch (language)
        {
            case LanguageType.KR: return LStatic("config.language.kr", "한국어");
            case LanguageType.EN: return LStatic("config.language.en", "English");
            case LanguageType.JP: return LStatic("config.language.jp", "日本語");
            case LanguageType.CN: return LStatic("config.language.cn", "简体中文");
            default: return language.ToString();
        }
    }

    private static string KeyDisplayName(Key key)
    {
        // 오래된 저장값은 숫자 문자열도 enum 파싱을 통과할 수 있으므로 먼저 검증합니다.
        if (!Enum.IsDefined(typeof(Key), key)) return LStatic("config.key.none", "Unbound");
        switch (key)
        {
            case Key.None: return LStatic("config.key.none", "Unbound");
            case Key.UpArrow: return "↑";
            case Key.DownArrow: return "↓";
            case Key.LeftArrow: return "←";
            case Key.RightArrow: return "→";
            case Key.Escape: return LStatic("config.key.escape", "Esc");
            case Key.Enter: return LStatic("config.key.enter", "Enter");
            case Key.Space: return LStatic("config.key.space", "Space");
            case Key.Tab: return LStatic("config.key.tab", "Tab");
            case Key.Backspace: return LStatic("config.key.backspace", "Bksp");
            case Key.Delete: return LStatic("config.key.delete", "Del");
            case Key.Insert: return LStatic("config.key.insert", "Ins");
            case Key.Home: return LStatic("config.key.home", "Home");
            case Key.End: return LStatic("config.key.end", "End");
            case Key.PageUp: return LStatic("config.key.page_up", "PgUp");
            case Key.PageDown: return LStatic("config.key.page_down", "PgDn");
            case Key.LeftShift: return LStatic("config.key.left_shift", "L Shift");
            case Key.RightShift: return LStatic("config.key.right_shift", "R Shift");
            case Key.LeftCtrl: return LStatic("config.key.left_ctrl", "L Ctrl");
            case Key.RightCtrl: return LStatic("config.key.right_ctrl", "R Ctrl");
            case Key.LeftAlt: return LStatic("config.key.left_alt", "L Alt");
            case Key.RightAlt: return LStatic("config.key.right_alt", "R Alt");
        }
        // 문자 키는 현재 키보드 배열의 키캡 표기를 사용하고 긴 enum 이름을 노출하지 않습니다.
        if (Keyboard.current != null && (int)key > 0 && (int)key <= Keyboard.current.allKeys.Count)
        {
            string displayName = Keyboard.current[key].displayName;
            if (!string.IsNullOrEmpty(displayName)) return displayName;
        }
        string name = key.ToString();
        return name.StartsWith("Digit", StringComparison.Ordinal) ? name.Substring(5) : name;
    }

    private static bool SupportsWindowSettings(RuntimePlatform platform)
    {
        return platform == RuntimePlatform.WindowsPlayer || platform == RuntimePlatform.WindowsEditor
            || platform == RuntimePlatform.OSXPlayer || platform == RuntimePlatform.OSXEditor
            || platform == RuntimePlatform.LinuxPlayer || platform == RuntimePlatform.LinuxEditor;
    }

    private static bool CanRebindKeyboard => SupportsWindowSettings(Application.platform) && Keyboard.current != null;

    private static bool IsRowAvailable(RowType row)
    {
        if (row == RowType.Fullscreen || row == RowType.WindowScale)
            return SupportsWindowSettings(Application.platform);
        if (IsKeyRow(row) || row == RowType.ControlsResetDefault)
            return CanRebindKeyboard;
        return true;
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
            string text = LocalizationManager.Instance.GetText(key);
            if (!string.IsNullOrEmpty(text)) return text;
        }
        return LocalFallback(key, fallback);
    }

    private static string LStatic(string key, string fallback)
    {
        if (LocalizationManager.Instance != null)
        {
            string text = LocalizationManager.Instance.GetText(key);
            if (!string.IsNullOrEmpty(text)) return text;
        }
        LanguageType lang = GameConfigManager.Instance != null ? GameConfigManager.Instance.Language : LanguageType.KR;
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
                case "config.title": return "설정";
                case "config.category.audio": return "오디오";
                case "config.category.gameplay": return "게임플레이";
                case "config.category.controls": return "조작";
                case "config.category.system": return "화면";
                case "config.language": return "언어";
                case "config.text_speed": return "텍스트 속도";
                case "config.auto_advance": return "자동 진행";
                case "config.screen_shake": return "화면 흔들림";
                case "config.flash_intensity": return "점멸 강도";
                case "config.fullscreen": return "전체화면";
                case "config.window_size": return "창 크기";
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

    private void ApplyVisual(TextMeshProUGUI text, bool selected, bool available = true)
    {
        if (text == null) return;
        text.color = !available ? UnavailableColor : selected ? _selectedColor : _normalColor;
        // 선택은 색과 행 배경으로만 표시한다. TMP 자동 크기와 열 너비를 유지한다.
        text.rectTransform.localScale = Vector3.one;
    }

    private void RestorePreviewPosition()
    {
        if (_gameplayPreviewText == null) return;
        RectTransform rect = _gameplayPreviewText.rectTransform;
        if (!_hasPreviewBaseline)
        {
            _previewAnchoredPosition = rect.anchoredPosition;
            _hasPreviewBaseline = true;
        }
        rect.anchoredPosition = _previewAnchoredPosition;
    }

    private void KillAllTweens()
    {
        if (_gameplayPreviewText != null)
        {
            _gameplayPreviewText.DOKill();
            _gameplayPreviewText.rectTransform.DOKill();
            RestorePreviewPosition();
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
        return new List<RowType> { RowType.Fullscreen, RowType.WindowScale, RowType.ResetDefault };
    }

    private static string ToPercent(float v) { return Mathf.RoundToInt(v * 100f) + "%"; }
}
