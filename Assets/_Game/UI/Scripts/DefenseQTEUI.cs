using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;
using Sirenix.OdinInspector;

public class DefenseQTEUI : UIPanel
{
    [BoxGroup("Countdown Bar"), LabelWidth(120)] [SerializeField] private UnityEngine.UI.Image _barFill;
    [BoxGroup("Result"), LabelWidth(120)] [SerializeField] private TextMeshProUGUI _resultLabel;

    [FoldoutGroup("Result Colors")] [SerializeField] private Color _colorPerfect = new Color(1f,   0.95f, 0.1f);
    [FoldoutGroup("Result Colors")] [SerializeField] private Color _colorGreat   = new Color(0.4f, 1f,   0.4f);
    [FoldoutGroup("Result Colors")] [SerializeField] private Color _colorGood    = new Color(0.3f, 0.85f, 1f);
    [FoldoutGroup("Result Colors")] [SerializeField] private Color _colorBad     = new Color(1f,   0.45f, 0.2f);
    [FoldoutGroup("Result Colors")] [SerializeField] private Color _colorMiss    = new Color(0.55f, 0.55f, 0.55f);

    [BoxGroup("Skill QTE Dynamic"), LabelWidth(120)] [SerializeField] private RectTransform _qteRoot;
    [BoxGroup("Skill QTE Dynamic"), LabelWidth(120)] [SerializeField] private TextMeshProUGUI _targetKeyLabel;

    private Tweener _barTween;
    private Sequence _resultSequence;

    private Canvas _parentCanvas;
    private Camera _uiCamera;

    protected override void Awake()
    {
        base.Awake();
        _parentCanvas = GetComponentInParent<Canvas>();
        if (_parentCanvas != null)
            _uiCamera = _parentCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : _parentCanvas.worldCamera;
    }

    public void ShowQTE(float attackDelay, string attackTypeName = "ATTACK")
    {
        ResetState();
        ShowImmediate();

        if (_barFill != null)
        {
            _barFill.fillAmount = 1f;
            _barFill.color      = Color.white;
            
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

        // DOTween을 사용한 화려한 텍스트 팝업 연출
        _resultSequence = DOTween.Sequence()
            .Append(_resultLabel.DOFade(1f, 0.08f))
            .Join(_resultLabel.transform.DOScale(Vector3.one, 0.12f).SetEase(Ease.OutBack))
            .AppendCallback(() => _resultLabel.transform.DOPunchScale(Vector3.one * 0.3f, 0.2f, 8, 0.5f))
            .AppendInterval(0.75f)
            .Append(_resultLabel.DOFade(0f, 0.15f))
            .OnComplete(Hide); 
    }

    public void ShowSkillQTE(Vector2 relativePos, string targetKey, float duration)
{
    // 1. 기존 트윈/시퀀스 즉시 정리
    ResetState();

    // 🚨 [추가] 예열 모드 판별: 키가 없거나 시간이 0이면 예열임
    bool isWarmUp = string.IsNullOrEmpty(targetKey) && duration <= 0;

    if (isWarmUp)
    {
        // 예열 시: 오브젝트만 켜고 투명도는 0으로 유지 (화면 밖 노출 방지)
        if (!gameObject.activeSelf) gameObject.SetActive(true);
        _canvasGroup.alpha = 0f; 
        _canvasGroup.interactable = false;
        _canvasGroup.blocksRaycasts = false;
        
        // 예열 위치는 아예 화면 밖 먼 곳으로 처리
        _qteRoot.anchoredPosition = new Vector2(-9999, -9999);
    }
    else
    {
        // 실제 작동 시: 즉시 투명도를 1로 만들고 상호작용 활성화
        ShowImmediate(); 
        
        // 🚨 픽셀 퍼펙트 대응 좌표 계산
        if (_qteRoot != null && _parentCanvas != null)
        {
            RectTransform canvasRect = _parentCanvas.GetComponent<RectTransform>();
            float targetX = (relativePos.x - 0.5f) * canvasRect.rect.width;
            float targetY = (relativePos.y - 0.5f) * canvasRect.rect.height;

            // 정수 좌표 반올림 (Pixel Snapping)
            _qteRoot.anchoredPosition = new Vector2(Mathf.Round(targetX), Mathf.Round(targetY));
        }
    }

    // 🚨 스케일이 커지는 문제 방지를 위해 항상 1로 고정
    _qteRoot.localScale = Vector3.one;

    if (!isWarmUp && _targetKeyLabel != null) 
        _targetKeyLabel.text = targetKey;

    if (!isWarmUp && _barFill != null)
    {
        _barFill.fillAmount = 1f;
        _barFill.color = new Color(0.2f, 0.8f, 1f); 
        if (duration > 0f) 
            _barTween = _barFill.DOFillAmount(0f, duration).SetEase(Ease.Linear);
    }
}

    public void ShowSkillResult(bool isHit)
    {
        ResetState();
        if (_resultLabel == null) { Hide(); return; }

        _resultLabel.text  = isHit ? "HIT!" : "MISS";
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
            QTEManager.QTEGrade.Perfect => $"PERFECT!\n<size=70%>{inputName}</size>",
            QTEManager.QTEGrade.Great   => $"GREAT!\n<size=70%>{inputName}</size>",
            QTEManager.QTEGrade.Good    => $"GOOD\n<size=70%>{inputName}</size>",
            QTEManager.QTEGrade.Bad     => $"BAD\n<size=70%>{inputName}</size>",
            _                           => "MISS",
        };
    }

    private Color GetResultColor(QTEManager.QTEGrade grade) => grade switch
    {
        QTEManager.QTEGrade.Perfect => _colorPerfect, QTEManager.QTEGrade.Great => _colorGreat,
        QTEManager.QTEGrade.Good => _colorGood, QTEManager.QTEGrade.Bad => _colorBad, _ => _colorMiss,
    };
}