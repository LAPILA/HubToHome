using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// UI 패널 열기/닫기를 총괄하는 싱글톤 매니저.
/// [개선] 씬 전환 시 에러 방지를 위해 런타임 패널 등록(Dictionary) 방식과 자동 뒤로가기(Pop)를 지원합니다.
/// </summary>
public class UIManager : MonoBehaviour
{
    public static UIManager Instance { get; private set; }

    // ── 패널 스택 및 저장소 ──────────────────────────────────
    private readonly Stack<UIPanel> _panelStack = new Stack<UIPanel>();
    
    // 식별자(String)로 패널을 관리하여 씬이 바뀌어도 유연하게 대응
    private readonly Dictionary<string, UIPanel> _registeredPanels = new Dictionary<string, UIPanel>();

    [Header("Global Panels (씬 무관하게 항상 존재하는 UI)")]
    [SerializeField] private UIPanel _pausePanel;
    [SerializeField] private UIPanel _saveLoadPanel;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        // 글로벌 패널 기본 등록
        if (_pausePanel != null) RegisterPanel("Pause", _pausePanel);
        if (_saveLoadPanel != null) RegisterPanel("SaveLoad", _saveLoadPanel);
    }

    private void Update()
    {
        // 최상단 패널 자동 닫기 (ESC 또는 X키)
        if (IsAnyPanelOpen)
        {
            if (GameInput.UICancelPressed)
            {
                CloseTopPanel();
            }
        }
    }

    // ── 패널 등록 (씬 전용 UI들이 Start에서 호출) ──────────────
    public void RegisterPanel(string panelID, UIPanel panel)
    {
        if (!_registeredPanels.ContainsKey(panelID))
            _registeredPanels.Add(panelID, panel);
        else
            _registeredPanels[panelID] = panel;
    }

    public void UnregisterPanel(string panelID)
    {
        if (_registeredPanels.ContainsKey(panelID))
            _registeredPanels.Remove(panelID);
    }

    // ── 패널 열기 / 닫기 ────────────────────────────────────
    public void OpenPanel(string panelID)
    {
        if (_registeredPanels.TryGetValue(panelID, out var panel))
            OpenPanel(panel);
        else
            Debug.LogWarning($"[UIManager] '{panelID}' 패널을 찾을 수 없습니다! RegisterPanel이 호출되었는지 확인하세요.");
    }

    public void OpenPanel(UIPanel panel)
    {
        if (panel == null) return;
        if (_panelStack.Count > 0 && _panelStack.Peek() == panel) return; // 이미 최상단이면 무시

        _panelStack.Push(panel);
        panel.Show();
        
        // UI가 열리면 게임 일시정지 (선택 사항)
        // Time.timeScale = 0f; 
    }

    public void CloseTopPanel()
    {
        if (_panelStack.Count == 0) return;
        var panel = _panelStack.Pop();
        panel.Hide();

        // 스택이 비면 일시정지 해제
        // if (_panelStack.Count == 0) Time.timeScale = 1f;
    }

    public void CloseAllPanels()
    {
        while (_panelStack.Count > 0)
        {
            var panel = _panelStack.Pop();
            panel.Hide();
        }
    }

    public bool IsAnyPanelOpen => _panelStack.Count > 0;
}