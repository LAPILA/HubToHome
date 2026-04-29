using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;
using Sirenix.OdinInspector;

/// <summary>
/// 전투 HUD — 플레이어/적 HP 바 + 턴 표시.
/// DOTween Pro TMP 확장 + Odin Inspector 사용.
/// 
/// Hierarchy 구조:
/// BattleHUD (UIPanel + CanvasGroup)
///   ├── PlayerHPBar
///   │     ├── Fill (Image, fillMethod = Horizontal)
///   │     └── HPText (TextMeshProUGUI)
///   ├── EnemyHPBar
///   │     ├── Fill (Image, fillMethod = Horizontal)
///   │     └── HPText (TextMeshProUGUI)
///   └── TurnLabel (TextMeshProUGUI)
/// </summary>
public class BattleHUD : UIPanel
{
    [BoxGroup("Player HP"), LabelWidth(80)]
    [SerializeField] private Image _playerHPFill;

    [BoxGroup("Player HP"), LabelWidth(80)]
    [SerializeField] private TMPro.TextMeshProUGUI _playerHPText;

    [BoxGroup("Enemy HP"), LabelWidth(80)]
    [SerializeField] private Image _enemyHPFill;

    [BoxGroup("Enemy HP"), LabelWidth(80)]
    [SerializeField] private TMPro.TextMeshProUGUI _enemyHPText;

    [BoxGroup("Turn Label")]
    [SerializeField] private TMPro.TextMeshProUGUI _turnLabel;

    [FoldoutGroup("HP Bar Tween"), LabelWidth(120)]
    [SerializeField] private float _hpTweenDuration = 0.4f;

    [FoldoutGroup("HP Bar Tween"), LabelWidth(120)]
    [SerializeField] private Ease _hpTweenEase = Ease.OutQuad;

    // ── 외부 API ──────────────────────────────────────────────

    /// <summary>플레이어 HP 바를 업데이트합니다.</summary>
    public void SetPlayerHP(int current, int max)
    {
        float ratio = max > 0 ? (float)current / max : 0f;
        _playerHPFill?.DOFillAmount(ratio, _hpTweenDuration).SetEase(_hpTweenEase);

        if (_playerHPText != null)
        {
            // DOTween Pro TMP: 숫자 카운트업 연출
            int prev = ParseHPFromText(_playerHPText.text);
            DOTween.To(() => prev, x => _playerHPText.text = $"{x} / {max}", current, _hpTweenDuration)
                   .SetEase(_hpTweenEase);
        }
    }

    /// <summary>적 HP 바를 업데이트합니다.</summary>
    public void SetEnemyHP(int current, int max)
    {
        float ratio = max > 0 ? (float)current / max : 0f;
        _enemyHPFill?.DOFillAmount(ratio, _hpTweenDuration).SetEase(_hpTweenEase);

        if (_enemyHPText != null)
        {
            int prev = ParseHPFromText(_enemyHPText.text);
            DOTween.To(() => prev, x => _enemyHPText.text = $"{x} / {max}", current, _hpTweenDuration)
                   .SetEase(_hpTweenEase);
        }
    }

    /// <summary>턴 레이블을 업데이트합니다.</summary>
    public void SetTurnLabel(string text)
    {
        if (_turnLabel == null) return;

        // DOTween Pro TMP: 텍스트 페이드 교체 + 펀치 스케일
        _turnLabel.DOKill();
        _turnLabel.DOFade(0f, 0.1f).OnComplete(() =>
        {
            _turnLabel.text = text;
            _turnLabel.DOFade(1f, 0.15f);
            _turnLabel.transform.DOKill();
            _turnLabel.transform.DOPunchScale(Vector3.one * 0.18f, 0.25f, 5, 0.5f);
        });
    }

    // ── 유틸리티 ──────────────────────────────────────────────

    private int ParseHPFromText(string text)
    {
        if (string.IsNullOrEmpty(text)) return 0;
        var parts = text.Split('/');
        return int.TryParse(parts[0].Trim(), out int val) ? val : 0;
    }
}
