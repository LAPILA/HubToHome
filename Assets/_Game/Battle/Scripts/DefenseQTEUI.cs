using UnityEngine;
using DG.Tweening;
using Sirenix.OdinInspector;

public class DefenseQTEUI : UIPanel
{
    [BoxGroup("Countdown Bar"), LabelWidth(120)]
    [SerializeField] private UnityEngine.UI.Image _barFill;

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

    [BoxGroup("Skill QTE Dynamic"), LabelWidth(120)]
    [SerializeField] private RectTransform _qteRoot;

    [BoxGroup("Skill QTE Dynamic"), LabelWidth(120)]
    [SerializeField] private TMPro.TextMeshProUGUI _targetKeyLabel;

    private Tweener _barTween;
    private Sequence _resultSequence;

    // ═══════════════════════════════════════════════════════════
    // ── 방어 QTE ──────────────────────────────────────────────
    public void ShowQTE(float attackDelay, string attackTypeName = "ATTACK")
    {
        _resultSequence?.Kill(); 
        ShowImmediate();

        if (_resultLabel != null) { _resultLabel.text = ""; _resultLabel.alpha = 0f; }
        if (_barFill != null)
        {
            _barFill.fillAmount = 1f;
            _barFill.color      = Color.white;
            _barTween?.Kill();
            _barTween = _barFill.DOFillAmount(0f, attackDelay).SetEase(Ease.Linear);

            DOTween.To(() => _barFill.fillAmount, _ => { }, 0f, attackDelay)
                .OnUpdate(() => {
                    if (_barFill != null && _barFill.fillAmount < 0.35f)
                        _barFill.DOColor(new Color(1f, 0.2f, 0.2f), 0.1f);
                });
        }
    }

    public void ShowResult(QTEManager.QTEGrade grade, DefenseInput input)
    {
        _barTween?.Kill();
        _resultSequence?.Kill();
        if (_resultLabel == null) { Hide(); return; }

        _resultLabel.text  = GetDefenseResultText(grade, input);
        _resultLabel.color = GetResultColor(grade);
        _resultLabel.alpha = 0f;
        _resultLabel.transform.localScale = Vector3.one * 0.5f;

        _resultSequence = DOTween.Sequence()
            .Append(_resultLabel.DOFade(1f, 0.08f))
            .Join(_resultLabel.transform.DOScale(Vector3.one, 0.12f).SetEase(Ease.OutBack))
            .AppendCallback(() => _resultLabel.transform.DOPunchScale(Vector3.one * 0.3f, 0.2f, 8, 0.5f))
            .AppendInterval(0.75f)
            .Append(_resultLabel.DOFade(0f, 0.15f))
            .AppendCallback(() => Hide());
    }

    // ═══════════════════════════════════════════════════════════
    // ── 스킬 QTE ──────────────────────────────────────────────
    public void ShowSkillQTE(Vector2 screenPos, string targetKey, float duration)
    {
        _resultSequence?.Kill(); 
        ShowImmediate(); 
        Canvas.ForceUpdateCanvases(); // UI 렉 보정용

        if (_qteRoot != null)
        {
            Canvas canvas = GetComponentInParent<Canvas>();
            Camera cam = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            
            if (cam != null)
            {
                RectTransformUtility.ScreenPointToWorldPointInRectangle(
                    (RectTransform)_qteRoot.parent, screenPos, cam, out Vector3 worldPos);
                _qteRoot.position = worldPos;
            }
            else
            {
                _qteRoot.position = screenPos; 
            }
        }

        if (_targetKeyLabel != null) _targetKeyLabel.text = targetKey;
        if (_resultLabel != null) { _resultLabel.text = ""; _resultLabel.alpha = 0f; }

        if (_barFill != null)
        {
            _barFill.fillAmount = 1f;
            _barFill.color      = new Color(0.2f, 0.8f, 1f); 
            _barTween?.Kill();
            
            // duration이 0이면(Pre-warm) 트윈을 아예 건너뜀
            if (duration > 0f) 
                _barTween = _barFill.DOFillAmount(0f, duration).SetEase(Ease.Linear);
        }
    }

    public void ShowSkillResult(bool isHit)
    {
        _barTween?.Kill();
        _resultSequence?.Kill();
        if (_resultLabel == null) { Hide(); return; }

        _resultLabel.text  = isHit ? "<bounce>HIT!</bounce>" : "<shake>MISS</shake>";
        _resultLabel.color = isHit ? new Color(0.2f, 1f, 0.4f) : new Color(1f, 0.3f, 0.3f);
        _resultLabel.alpha = 0f;
        _resultLabel.transform.localScale = Vector3.one * 0.5f;

        _resultSequence = DOTween.Sequence()
            .Append(_resultLabel.DOFade(1f, 0.08f))
            .Join(_resultLabel.transform.DOScale(Vector3.one, 0.12f).SetEase(Ease.OutBack))
            .AppendCallback(() => _resultLabel.transform.DOPunchScale(Vector3.one * 0.3f, 0.2f, 8, 0.5f))
            .AppendInterval(0.35f) 
            .Append(_resultLabel.DOFade(0f, 0.12f)); 
            // 🚨 화면이 중간에 꺼지는 증상을 막기 위해 Hide() 하지 않음!
    }

    protected override void OnHideComplete()
    {
        _barTween?.Kill();
        _resultSequence?.Kill();
        if (_barFill != null) { _barFill.fillAmount = 1f; _barFill.color = Color.white; }
    }

    private string GetDefenseResultText(QTEManager.QTEGrade grade, DefenseInput input)
    {
        string inputName = input switch { DefenseInput.Parry => "패링", DefenseInput.Dodge => "회피", DefenseInput.Jump => "점프", _ => "" };
        return grade switch
        {
            QTEManager.QTEGrade.Perfect => $"<shake a=0.3>PERFECT!</shake> {inputName}",
            QTEManager.QTEGrade.Great   => $"<wave a=0.2>GREAT!</wave> {inputName}",
            QTEManager.QTEGrade.Good    => $"GOOD {inputName}",
            QTEManager.QTEGrade.Bad     => $"BAD {inputName}",
            _                           => "<shake a=0.5>MISS</shake>",
        };
    }

    private Color GetResultColor(QTEManager.QTEGrade grade)
    {
        return grade switch
        {
            QTEManager.QTEGrade.Perfect => _colorPerfect, QTEManager.QTEGrade.Great => _colorGreat,
            QTEManager.QTEGrade.Good => _colorGood, QTEManager.QTEGrade.Bad => _colorBad, _ => _colorMiss,
        };
    }
}