using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>전열 전멸 후 후열 투입 여부를 선택하는 전투 전용 창입니다.</summary>
[DisallowMultipleComponent]
public sealed class BattleReserveChoiceUI : MonoBehaviour
{
    public enum Choice { Pending, Continue, GiveUp, Cancelled }

    private Canvas _canvas;
    private TMP_Text _continueLabel;
    private TMP_Text _giveUpLabel;
    private int _selectedIndex;
    private int _openedFrame;
    private int _version;
    public bool IsVisible { get; private set; }
    public Choice Result { get; private set; } = Choice.Cancelled;

    public static BattleReserveChoiceUI Create(Transform owner)
    {
        var root = new GameObject("BattleReserveChoiceUI", typeof(RectTransform));
        root.transform.SetParent(owner, false);
        return root.AddComponent<BattleReserveChoiceUI>();
    }

    public IEnumerator Show()
    {
        int version = ++_version;
        if (_canvas == null) BuildView();
        gameObject.SetActive(true);
        Result = Choice.Pending;
        _selectedIndex = 0;
        _openedFrame = Time.frameCount;
        IsVisible = true;
        _canvas.enabled = true;
        RefreshSelection();
        GameInput.ConsumeBattleUIInput();
        try
        {
            // 전멸 타격에 사용한 Z/X/C로 즉시 결정을 내리지 않습니다.
            yield return null;
            while (IsVisible && version == _version)
            {
                if (CanReadInput())
                {
                    if (GameInput.UIUpPressed || GameInput.UIDownPressed)
                    {
                        _selectedIndex = 1 - _selectedIndex;
                        RefreshSelection();
                    }
                    if (GameInput.UICancelPressed)
                    {
                        _selectedIndex = 0;
                        RefreshSelection();
                        GameInput.ConsumeBattleUIInput();
                    }
                    else if (GameInput.UISubmitPressed && !GameInput.BattleUIInputConsumed)
                        TryChoose(_selectedIndex == 0);
                }
                yield return null;
            }
        }
        finally { if (version == _version) Cancel(); }
    }

    // 이후 별도 디자인 프리팹을 연결해도 게임 규칙을 복제하지 않도록 남긴 선택 진입점입니다.
    public bool TryChoose(bool continueBattle)
    {
        if (!IsVisible || Result != Choice.Pending || Time.frameCount <= _openedFrame || !CanReadInput())
            return false;
        Result = continueBattle ? Choice.Continue : Choice.GiveUp;
        Close();
        return true;
    }

    public void Cancel()
    {
        _version++;
        if (Result == Choice.Pending) Result = Choice.Cancelled;
        Close();
    }

    private bool CanReadInput()
    {
        GameStateManager state = GameStateManager.Instance;
        return !GameInput.IsConfigModalActive
            && (state == null || (state.CurrentState != GameState.Paused && state.CurrentState != GameState.Dialogue))
            && (DialogueManager.Instance == null || !DialogueManager.Instance.IsPresentationVisible);
    }

    private void Close()
    {
        if (IsVisible) GameInput.ConsumeBattleUIInput();
        IsVisible = false;
        if (_canvas != null) _canvas.enabled = false;
    }

    private void OnDisable() => Cancel();

    private void RefreshSelection()
    {
        Color selected = new Color32(255, 224, 92, 255);
        _continueLabel.text = (_selectedIndex == 0 ? "▶ " : "   ") + "계속 싸우기";
        _giveUpLabel.text = (_selectedIndex == 1 ? "▶ " : "   ") + "포기하기";
        _continueLabel.color = _selectedIndex == 0 ? selected : Color.white;
        _giveUpLabel.color = _selectedIndex == 1 ? selected : Color.white;
    }

    private void BuildView()
    {
        _canvas = gameObject.AddComponent<Canvas>();
        _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _canvas.sortingOrder = 31000;
        var scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(640f, 480f);
        scaler.matchWidthOrHeight = 0.5f;
        // 마우스/EventSystem 선택을 등록하지 않습니다. 공용 UI 입력만 사용합니다.
        Image backdrop = new GameObject("Backdrop", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
        backdrop.transform.SetParent(transform, false);
        backdrop.color = new Color32(0, 0, 0, 218);
        backdrop.raycastTarget = false;
        backdrop.rectTransform.anchorMin = Vector2.zero;
        backdrop.rectTransform.anchorMax = Vector2.one;
        backdrop.rectTransform.offsetMin = backdrop.rectTransform.offsetMax = Vector2.zero;
        TMP_FontAsset font = GameContentCatalog.Instance != null
            ? GameContentCatalog.Instance.DefaultUiFont : TMP_Settings.defaultFontAsset;
        CreateText("Title", "전열이 쓰러졌습니다", 25f, 78f, font);
        CreateText("Message", "후열의 동료들과 전투를 이어갈까요?", 17f, 32f, font);
        _continueLabel = CreateText("Continue", "", 23f, -26f, font);
        _giveUpLabel = CreateText("GiveUp", "", 23f, -74f, font);
        CreateText("Hint", "방향키: 선택   결정: 확인   취소: 계속 싸우기로 이동", 13f, -128f, font);
        _canvas.enabled = false;
    }

    private TMP_Text CreateText(string objectName, string value, float size, float y, TMP_FontAsset font)
    {
        var label = new GameObject(objectName, typeof(RectTransform), typeof(TextMeshProUGUI))
            .GetComponent<TextMeshProUGUI>();
        label.transform.SetParent(transform, false);
        label.rectTransform.anchorMin = label.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        label.rectTransform.sizeDelta = new Vector2(560f, 42f);
        label.rectTransform.anchoredPosition = new Vector2(0f, y);
        label.font = font;
        label.fontSize = size;
        label.text = value;
        label.alignment = TextAlignmentOptions.Center;
        label.color = Color.white;
        label.raycastTarget = false;
        return label;
    }
}
