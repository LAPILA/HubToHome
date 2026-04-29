using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;
using Sirenix.OdinInspector;

/// <summary>
/// 적 공격 시 방어 QTE 인디케이터 UI.
/// 인디케이터가 오른쪽 → 왼쪽으로 이동하며 중앙 판정 구간(Perfect/Good)을 통과합니다.
/// DOTween Pro TMP 확장 + Odin Inspector 사용.
/// 
/// Hierarchy 구조:
/// DefenseQTEUI (UIPanel + CanvasGroup)
///   ├── Track           — 바 배경 (Image, Width = _trackWidth)
///   ├── Indicator       — 이동 마커 (Image, RectTransform)
///   ├── PerfectZone     — 중앙 좁은 노란 Image
///   ├── GoodZone        — 중앙 넓은 초록 Image
///   ├── InputHintGroup  — 입력 힌트 (Z=패링 / C=회피 / Space=점프)
///   └── ResultLabel     — 판정 결과 TextMeshProUGUI
/// </summary>
public class DefenseQTEUI : UIPanel
{
    [BoxGroup("Indicator"), LabelWidth(100)]
    [SerializeField] private RectTransform _indicator;

    [BoxGroup("Indicator"), LabelWidth(100)]
    [Tooltip("Track Image의 가로 크기와 일치시키세요.")]
    [SerializeField] private float _trackWidth = 600f;

    [BoxGroup("Zones"), LabelWidth(100)]
    [SerializeField] private RectTransform _perfectZone;

    [BoxGroup("Zones"), LabelWidth(100)]
    [SerializeField] private RectTransform _goodZone;

    [BoxGroup("Result"), LabelWidth(100)]
    [SerializeField] private TMPro.TextMeshProUGUI _resultLabel;

    [FoldoutGroup("Result Colors")]
    [SerializeField] private Color _colorPerfect = new Color(1f,  0.9f, 0.1f);
    [FoldoutGroup("Result Colors")]
    [SerializeField] private Color _colorGreat   = new Color(0.4f, 1f,  0.4f);
    [FoldoutGroup("Result Colors")]
    [SerializeField] private Color _colorGood    = new Color(0.3f, 0.8f, 1f);
    [FoldoutGroup("Result Colors")]
    [SerializeField] private Color _colorBad     = new Color(1f,  0.5f, 0.2f);
    [FoldoutGroup("Result Colors")]
    [SerializeField] private Color _colorMiss    = new Color(0.6f, 0.6f, 0.6f);

    private Tweener _indicatorTween;

    // ── QTE 시작 ──────────────────────────────────────────────

    /// <summary>
    /// 방어 QTE UI를 표시하고 인디케이터를 이동시킵니다.
    /// </summary>
    /// <param name="attackDelay">QTEManager에 전달한 것과 동일한 attackDelay</param>
    public void ShowQTE(float attackDelay)
    {
        ShowImmediate();

        if (_resultLabel != null)
        {
            _resultLabel.text  = "";
            _resultLabel.alpha = 0f;
        }

        if (_indicator == null) return;

        // 오른쪽 끝에서 시작
        _indicator.anchoredPosition = new Vector2(_trackWidth * 0.5f, _indicator.anchoredPosition.y);

        // 왼쪽 끝까지 선형 이동
        _indicatorTween?.Kill();
        _indicatorTween = _indicator
            .DOAnchorPosX(-_trackWidth * 0.5f, attackDelay)
            .SetEase(Ease.Linear);

        // 판정 구간 펄스 (주의 환기)
        _perfectZone?.DOKill();
        _perfectZone?.DOScale(Vector3.one * 1.05f, 0.4f)
            .SetLoops(-1, LoopType.Yoyo)
            .SetEase(Ease.InOutSine);
    }

    /// <summary>
    /// QTE 결과를 표시하고 UI를 닫습니다.
    /// </summary>
    public void ShowResult(QTEManager.QTEGrade grade, DefenseInput input)
    {
        _indicatorTween?.Kill();
        _perfectZone?.DOKill();
        _perfectZone?.DOScale(Vector3.one, 0.1f);

        if (_resultLabel == null) { Hide(); return; }

        _resultLabel.text  = GetResultText(grade, input);
        _resultLabel.color = GetResultColor(grade);
        _resultLabel.alpha = 0f;

        // DOTween Pro TMP: 페이드인 + 펀치 스케일 → 1초 후 닫기
        DOTween.Sequence()
            .Append(_resultLabel.DOFade(1f, 0.12f))
            .AppendCallback(() => _resultLabel.transform.DOPunchScale(Vector3.one * 0.25f, 0.2f, 6, 0.5f))
            .AppendInterval(0.9f)
            .AppendCallback(() => Hide());
    }

    // ── 유틸리티 ──────────────────────────────────────────────

    private string GetResultText(QTEManager.QTEGrade grade, DefenseInput input)
    {
        string inputName = input switch
        {
            DefenseInput.Parry => "패링",
            DefenseInput.Dodge => "회피",
            DefenseInput.Jump  => "점프",
            _                  => "미스",
        };

        return grade switch
        {
            QTEManager.QTEGrade.Perfect => $"PERFECT! {inputName}",
            QTEManager.QTEGrade.Great   => $"GREAT! {inputName}",
            QTEManager.QTEGrade.Good    => $"GOOD {inputName}",
            QTEManager.QTEGrade.Bad     => $"BAD {inputName}",
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

    protected override void OnHideComplete()
    {
        _indicatorTween?.Kill();
        _perfectZone?.DOKill();
    }
}
