using UnityEngine;
using DG.Tweening;
using Sirenix.OdinInspector;

public class DefenseQTEUI : UIPanel
{
    [BoxGroup("Countdown Bar"), LabelWidth(120)] [SerializeField] private UnityEngine.UI.Image _barFill;
    [BoxGroup("Result"), LabelWidth(120)] [SerializeField] private TMPro.TextMeshProUGUI _resultLabel;

    [FoldoutGroup("Result Colors")] [SerializeField] private Color _colorPerfect = new Color(1f,   0.95f, 0.1f);
    [FoldoutGroup("Result Colors")] [SerializeField] private Color _colorGreat   = new Color(0.4f, 1f,   0.4f);
    [FoldoutGroup("Result Colors")] [SerializeField] private Color _colorGood    = new Color(0.3f, 0.85f, 1f);
    [FoldoutGroup("Result Colors")] [SerializeField] private Color _colorBad     = new Color(1f,   0.45f, 0.2f);
    [FoldoutGroup("Result Colors")] [SerializeField] private Color _colorMiss    = new Color(0.55f, 0.55f, 0.55f);

    [BoxGroup("Skill QTE Dynamic"), LabelWidth(120)] [SerializeField] private RectTransform _qteRoot;
    [BoxGroup("Skill QTE Dynamic"), LabelWidth(120)] [SerializeField] private TMPro.TextMeshProUGUI _targetKeyLabel;

    private Tweener _barTween;
    private Sequence _resultSequence;

    // 🚨 렉 방지를 위한 캐싱 (Caching)
    private Canvas _parentCanvas;
    private Camera _uiCamera;

    protected override void Awake()
    {
        base.Awake();
        // Awake 시점에 한 번만 찾아서 저장해둡니다. (프레임 드랍 방지)
        _parentCanvas = GetComponentInParent<Canvas>();
        if (_parentCanvas != null)
            _uiCamera = _parentCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : _parentCanvas.worldCamera;
    }

    // ── 방어 QTE ──────────────────────────────────────────────
    public void ShowQTE(float attackDelay, string attackTypeName = "ATTACK")
    {
        ResetState();
        ShowImmediate();

        if (_barFill != null)
        {
            _barFill.fillAmount = 1f;
            _barFill.color      = Color.white;
            
            // 🚨 안전한 Tween 호출
            _barTween = _barFill.DOFillAmount(0f, attackDelay)
                .SetEase(Ease.Linear)
                .OnUpdate(() => {
                    if (_barFill.fillAmount < 0.35f) _barFill.color = new Color(1f, 0.2f, 0.2f);
                });
        }
    }

    public void ShowResult(QTEManager.QTEGrade grade, DefenseInput input)
    {
        ResetState();
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
            .OnComplete(Hide); // 콜백을 OnComplete로 깔끔하게 정리
    }

    // ── 스킬 QTE ──────────────────────────────────────────────
    public void ShowSkillQTE(Vector2 screenPos, string targetKey, float duration)
    {
        ResetState();
        ShowImmediate(); 

        if (_qteRoot != null && _parentCanvas != null)
        {
            if (_uiCamera != null)
            {
                RectTransformUtility.ScreenPointToWorldPointInRectangle(
                    (RectTransform)_qteRoot.parent, screenPos, _uiCamera, out Vector3 worldPos);
                _qteRoot.position = worldPos;
            }
            else _qteRoot.position = screenPos; 
        }

        if (_targetKeyLabel != null) _targetKeyLabel.text = targetKey;

        if (_barFill != null)
        {
            _barFill.fillAmount = 1f;
            _barFill.color      = new Color(0.2f, 0.8f, 1f); 
            
            if (duration > 0f) 
                _barTween = _barFill.DOFillAmount(0f, duration).SetEase(Ease.Linear);
        }
    }

    public void ShowSkillResult(bool isHit)
    {
        ResetState();
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
    }

    protected override void OnHideComplete()
    {
        ResetState();
        if (_barFill != null) { _barFill.fillAmount = 1f; _barFill.color = Color.white; }
    }

    // 🚨 트윈 찌꺼기를 없애는 단일 유틸리티
    private void ResetState()
    {
        _barTween?.Kill();
        _resultSequence?.Kill();
        if (_resultLabel != null) { _resultLabel.text = ""; _resultLabel.alpha = 0f; }
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

    private Color GetResultColor(QTEManager.QTEGrade grade) => grade switch
    {
        QTEManager.QTEGrade.Perfect => _colorPerfect, QTEManager.QTEGrade.Great => _colorGreat,
        QTEManager.QTEGrade.Good => _colorGood, QTEManager.QTEGrade.Bad => _colorBad, _ => _colorMiss,
    };
}