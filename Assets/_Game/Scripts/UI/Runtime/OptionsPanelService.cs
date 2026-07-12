using UnityEngine;

/// <summary>
/// 타이틀/오버월드 공용 Options(Config) 패널 서비스.
/// - 런타임 생성/재사용
/// - UIManager 등록 보장
/// - 코드 한 줄로 오픈 가능
/// </summary>
public static class OptionsPanelService
{
    public const string PanelId = UIPanelId.Config;

    private static ConfigPanelUI _cachedPanel;

    private static bool IsAlive(Object obj) => obj != null;

    public static ConfigPanelUI EnsurePanel()
    {
        if (IsAlive(_cachedPanel))
        {
            RegisterToUIManager();
            return _cachedPanel;
        }

        _cachedPanel = Object.FindFirstObjectByType<ConfigPanelUI>(FindObjectsInactive.Include);
        if (!IsAlive(_cachedPanel))
        {
            Debug.LogWarning("[OptionsPanelService] ConfigPanelUI를 찾지 못했습니다. 씬/프리팹에 ConfigPanelUI를 배치하고 연결해주세요.");
            return null;
        }

        RegisterToUIManager();
        return _cachedPanel;
    }

    public static void Open()
    {
        if (GameStateManager.Instance != null && GameStateManager.Instance.CurrentState == GameState.Battle)
            return;

        var panel = EnsurePanel();
        if (panel == null) return;

        if (UIManager.Instance != null) UIManager.Instance.OpenPanel(panel);
        else panel.Show();
    }

    public static void RegisterToUIManager()
    {
        if (UIManager.Instance == null) return;

        var panel = IsAlive(_cachedPanel) ? _cachedPanel : Object.FindFirstObjectByType<ConfigPanelUI>(FindObjectsInactive.Include);
        if (!IsAlive(panel)) return;

        _cachedPanel = panel;
        UIManager.Instance.RegisterPanel(PanelId, panel);
    }
}
