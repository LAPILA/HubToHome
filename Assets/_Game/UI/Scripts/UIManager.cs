using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;

/// <summary>
/// UI 패널 열기/닫기를 총괄하는 싱글톤 매니저.
/// DOTween을 사용한 패널 등장/퇴장 애니메이션을 지원합니다.
/// </summary>
public class UIManager : MonoBehaviour
{
    public static UIManager Instance { get; private set; }

    // ── 패널 스택 ─────────────────────────────────────────────
    private readonly Stack<UIPanel> _panelStack = new Stack<UIPanel>();

    // ── 등록된 패널 ───────────────────────────────────────────
    [Header("Panels")]
    [SerializeField] private UIPanel _dialoguePanel;
    [SerializeField] private UIPanel _inventoryPanel;
    [SerializeField] private UIPanel _battleHUDPanel;
    [SerializeField] private UIPanel _pausePanel;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    // ── 패널 열기 ─────────────────────────────────────────────
    public void OpenPanel(UIPanel panel)
    {
        if (panel == null) return;
        if (_panelStack.Count > 0 && _panelStack.Peek() == panel) return;

        _panelStack.Push(panel);
        panel.Show();
    }

    // ── 패널 닫기 (최상단) ────────────────────────────────────
    public void CloseTopPanel()
    {
        if (_panelStack.Count == 0) return;
        var panel = _panelStack.Pop();
        panel.Hide();
    }

    // ── 특정 패널 닫기 ────────────────────────────────────────
    public void ClosePanel(UIPanel panel)
    {
        if (panel == null) return;
        panel.Hide();
        // 스택에서 제거 (재구성)
        var temp = new Stack<UIPanel>();
        while (_panelStack.Count > 0)
        {
            var p = _panelStack.Pop();
            if (p != panel) temp.Push(p);
        }
        while (temp.Count > 0)
            _panelStack.Push(temp.Pop());
    }

    // ── 모든 패널 닫기 ────────────────────────────────────────
    public void CloseAllPanels()
    {
        while (_panelStack.Count > 0)
        {
            var panel = _panelStack.Pop();
            panel.Hide();
        }
    }

    // ── 편의 메서드 ───────────────────────────────────────────
    public void OpenDialogue()   => OpenPanel(_dialoguePanel);
    public void CloseDialogue()  => ClosePanel(_dialoguePanel);
    public void OpenInventory()  => OpenPanel(_inventoryPanel);
    public void CloseInventory() => ClosePanel(_inventoryPanel);
    public void OpenBattleHUD()  => OpenPanel(_battleHUDPanel);
    public void CloseBattleHUD() => ClosePanel(_battleHUDPanel);
    public void OpenPause()      => OpenPanel(_pausePanel);
    public void ClosePause()     => ClosePanel(_pausePanel);

    public bool IsAnyPanelOpen => _panelStack.Count > 0;
}
