using UnityEngine;
using DG.Tweening;
using Sirenix.OdinInspector;

/// <summary>
/// 방어/스킬 QTE UI.
/// 
/// 방어 QTE 흐름:
///   카운트다운 바 수축 → Z(패링)/C(회피)/Space(점프) 입력 → 결과 팝업
/// 
/// 스킬 QTE 흐름:
///   파란 바 수축 → Z키 타이밍 입력 → 결과 팝업
/// 
/// Hierarchy:
/// DefenseQTEUI (UIPanel + CanvasGroup)
///   ├── CountdownBar
///   │     ├── BarBG    (Image, 배경)
///   │     └── BarFill  (Image, fillMethod=Horizontal, fillOrigin=Right)
///   └── ResultLabel    (TMP + TextAnimator 컴포넌트)
/// </summary>
public class DefenseQTEUI : UIPanel
{
    // ── 카운트다운 바 ─────────────────────────────────────────
    [BoxGroup("Countdown Bar"), LabelWidth(120)]
    [SerializeField] private UnityEngine.UI.Image _barFill;

    // ── 결과 표시 ─────────────────────────────────────────────
    [BoxGroup("Result"), LabelWidth(120)]
    [SerializeField] private TMPro.TextMeshProUGUI _resultLabel;

    [FoldoutGroup("Result Colors")]
    [SerializeField] private Color _colorPerfect = new Color(1f,   0.95f, 0.1f);
    [FoldoutGroup("Result Colors")]
    [SerializeField] private Color _colorGreat   = new Color(0.4f, 1f,   0.4f);
    [FoldoutGroup("Result Colors")]
    [SerializeField] private Color _colorGood    = new Color(0.3f, 0.85f, 1f);
    [FoldoutGroup("Result Colors")]
    [SerializeField] private Color _colorBad     = new Color(1f,   0.45f, 0.2f);
    [FoldoutGroup("Result Colors")]
    [SerializeField] private Color _colorMiss    = new Color(0.55f, 0.55f, 0.55f);

    // ── 내부 상태 ─────────────────────────────────────────────
    private Tweener _barTween;

    // ═══════════════════════════════════════════════════════════
    // ── 방어 QTE ──────────────────────────────────────────────
    // ═══════════════════════════════════════════════════════════

    /// <summary>방어 QTE UI 표시 (카운트다운 바 수축)</summary>
    public void ShowQTE(float attackDelay, string attackTypeName = "ATTACK")
    {
        ShowImmediate();

        if (_resultLabel != null) { _resultLabel.text = ""; _resultLabel.alpha = 0f; }

        if (_barFill != null)
        {
            _barFill.fillAmount = 1f;
            _barFill.color      = Color.white;
            _barTween?.Kill();
            _barTween = _barFill.DOFillAmount(0f, attackDelay).SetEase(Ease.Linear);

            // 35% 이하 → 빨간색 경고
            DOTween.To(() => _barFill.fillAmount, _ => { }, 0f, attackDelay)
                .OnUpdate(() =>
                {
                    if (_barFill != null && _barFill.fillAmount < 0.35f)
                        _barFill.DOColor(new Color(1f, 0.2f, 0.2f), 0.1f);
                });
        }
    }

    /// <summary>방어 QTE 결과 표시 후 닫기</summary>
    public void ShowResult(QTEManager.QTEGrade grade, DefenseInput input)
    {
        _barTween?.Kill();

        if (_resultLabel == null) { Hide(); return; }

        _resultLabel.text  = GetDefenseResultText(grade, input);
        _resultLabel.color = GetResultColor(grade);
        _resultLabel.alpha = 0f;
        _resultLabel.transform.localScale = Vector3.one * 0.5f;

        DOTween.Sequence()
            .Append(_resultLabel.DOFade(1f, 0.08f))
            .Join(_resultLabel.transform.DOScale(Vector3.one, 0.12f).SetEase(Ease.OutBack))
            .AppendCallback(() =>
                _resultLabel.transform.DOPunchScale(Vector3.one * 0.3f, 0.2f, 8, 0.5f))
            .AppendInterval(0.75f)
            .Append(_resultLabel.DOFade(0f, 0.15f))
            .AppendCallback(() => Hide());
    }

    // ═══════════════════════════════════════════════════════════
    // ── 스킬 QTE ──────────────────────────────────────────────
    // ═══════════════════════════════════════════════════════════

    /// <summary>스킬 QTE UI 표시 (파란 바 수축, Z키 타이밍)</summary>
    public void ShowSkillQTE(float duration)
    {
        ShowImmediate();

        if (_resultLabel != null) { _resultLabel.text = ""; _resultLabel.alpha = 0f; }

        if (_barFill != null)
        {
            _barFill.fillAmount = 1f;
            _barFill.color      = new Color(0.3f, 0.8f, 1f); // 파란색
            _barTween?.Kill();
            _barTween = _barFill.DOFillAmount(0f, duration).SetEase(Ease.Linear);
        }
    }

    /// <summary>스킬 QTE 결과 표시 후 닫기</summary>
    public void ShowSkillResult(QTEManager.QTEGrade grade)
    {
        _barTween?.Kill();

        if (_resultLabel == null) { Hide(); return; }

        _resultLabel.text  = GetSkillResultText(grade);
        _resultLabel.color = GetResultColor(grade);
        _resultLabel.alpha = 0f;
        _resultLabel.transform.localScale = Vector3.one * 0.5f;

        DOTween.Sequence()
            .Append(_resultLabel.DOFade(1f, 0.08f))
            .Join(_resultLabel.transform.DOScale(Vector3.one, 0.12f).SetEase(Ease.OutBack))
            .AppendCallback(() =>
                _resultLabel.transform.DOPunchScale(Vector3.one * 0.3f, 0.2f, 8, 0.5f))
            .AppendInterval(0.6f)
            .Append(_resultLabel.DOFade(0f, 0.12f))
            .AppendCallback(() => Hide());
    }

    // ── 정리 ──────────────────────────────────────────────────
    protected override void OnHideComplete()
    {
        _barTween?.Kill();
        if (_barFill != null) { _barFill.fillAmount = 1f; _barFill.color = Color.white; }
    }

    // ── 유틸리티 ──────────────────────────────────────────────
    private string GetDefenseResultText(QTEManager.QTEGrade grade, DefenseInput input)
    {
        string inputName = input switch
        {
            DefenseInput.Parry => "패링",
            DefenseInput.Dodge => "회피",
            DefenseInput.Jump  => "점프",
            _                  => "",
        };
        return grade switch
        {
            QTEManager.QTEGrade.Perfect => $"<shake a=0.3>PERFECT!</shake> {inputName}",
            QTEManager.QTEGrade.Great   => $"<wave a=0.2>GREAT!</wave> {inputName}",
            QTEManager.QTEGrade.Good    => $"GOOD {inputName}",
            QTEManager.QTEGrade.Bad     => $"BAD {inputName}",
            _                           => "<shake a=0.5>MISS</shake>",
        };
    }

    private string GetSkillResultText(QTEManager.QTEGrade grade)
    {
        return grade switch
        {
            QTEManager.QTEGrade.Perfect => "<shake a=0.3>PERFECT!</shake>",
            QTEManager.QTEGrade.Great   => "<wave a=0.2>GREAT!</wave>",
            QTEManager.QTEGrade.Good    => "GOOD",
            QTEManager.QTEGrade.Bad     => "BAD",
            _                           => "MISS",
        };
    }

    private Color GetResultColor(QTEManager.QTEGrade grade)
    {
        return grade switch
        {
            QTEManager.QTEGrade.Perfect => _colorPerfect,
            QTEManager.QTEGrade.Great   => _colorGreat,
            QTEManager.QTEGrade.Good    => _colorGood,
            QTEManager.QTEGrade.Bad     => _colorBad,
            _                           => _colorMiss,
        };
    }
}
