using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

/// <summary>
/// 플레이어 턴 행동 선택 메뉴 UI.
/// UIPanel을 상속하며 BattleManager.OnPlayerActionSelected()를 호출합니다.
/// 
/// Hierarchy 구조:
/// BattleMenu (UIPanel + CanvasGroup)
///   ├── Btn_Attack  (Button)
///   ├── Btn_Skill   (Button)
///   ├── Btn_Item    (Button)
///   └── Btn_Run     (Button)
/// 
/// 사용법:
/// - BattleManager가 PlayerTurn 진입 시 Show() 호출
/// - 버튼 클릭 → OnXxx() → BattleManager.OnPlayerActionSelected() → Hide()
/// </summary>
public class BattleMenuUI : UIPanel
{
    [Header("Buttons")]
    [SerializeField] private Button _attackBtn;
    [SerializeField] private Button _skillBtn;
    [SerializeField] private Button _itemBtn;
    [SerializeField] private Button _runBtn;

    protected override void Awake()
    {
        base.Awake();
        _attackBtn?.onClick.AddListener(OnAttack);
        _skillBtn?.onClick.AddListener(OnSkill);
        _itemBtn?.onClick.AddListener(OnItem);
        _runBtn?.onClick.AddListener(OnRun);
    }

    protected override void OnShowComplete()
    {
        // 버튼 등장 시 순차 팝인 연출
        AnimateButtonsIn();
    }

    // ── 버튼 핸들러 ───────────────────────────────────────────

    private void OnAttack()
    {
        Hide();
        BattleManager.Instance?.OnPlayerActionSelected(PlayerMenuAction.Attack, targetIndex: 0);
    }

    private void OnSkill()
    {
        Hide();
        BattleManager.Instance?.OnPlayerActionSelected(PlayerMenuAction.Skill);
    }

    private void OnItem()
    {
        Hide();
        BattleManager.Instance?.OnPlayerActionSelected(PlayerMenuAction.Item);
    }

    private void OnRun()
    {
        Hide();
        BattleManager.Instance?.OnPlayerActionSelected(PlayerMenuAction.Run);
    }

    // ── 연출 ──────────────────────────────────────────────────

    private void AnimateButtonsIn()
    {
        Button[] buttons = { _attackBtn, _skillBtn, _itemBtn, _runBtn };
        for (int i = 0; i < buttons.Length; i++)
        {
            if (buttons[i] == null) continue;
            var rt = buttons[i].GetComponent<RectTransform>();
            rt.DOKill();
            // 아래에서 위로 슬라이드 인
            rt.anchoredPosition += Vector2.down * 20f;
            rt.DOAnchorPosY(rt.anchoredPosition.y + 20f, 0.2f)
              .SetDelay(i * 0.05f)
              .SetEase(Ease.OutBack);
        }
    }
}
