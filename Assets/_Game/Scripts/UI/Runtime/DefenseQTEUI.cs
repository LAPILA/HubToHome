using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;
using Sirenix.OdinInspector;

/// <summary>
/// 전투 내 모든 QTE(방어, 스킬) UI의 시각적 연출을 담당합니다.
/// DOTween 충돌 방지 및 픽셀 퍼펙트를 위한 좌표 계산에 특화되어 있습니다.
/// </summary>
public class DefenseQTEUI : UIPanel
{
    #region [ Inspector Variables ]
    [BoxGroup("Countdown Bar"), LabelWidth(120)] [SerializeField] private Image _barFill;
    [BoxGroup("Result"), LabelWidth(120)] [SerializeField] private TextMeshProUGUI _resultLabel;

    [FoldoutGroup("Result Colors")] [SerializeField] private Color _colorPerfect = new Color(1f, 0.95f, 0.1f);
    [FoldoutGroup("Result Colors")] [SerializeField] private Color _colorGreat   = new Color(0.4f, 1f, 0.4f);
    [FoldoutGroup("Result Colors")] [SerializeField] private Color _colorGood    = new Color(0.3f, 0.85f, 1f);
    [FoldoutGroup("Result Colors")] [SerializeField] private Color _colorBad     = new Color(1f, 0.45f, 0.2f);
    [FoldoutGroup("Result Colors")] [SerializeField] private Color _colorMiss    = new Color(0.55f, 0.55f, 0.55f);

    [BoxGroup("Skill QTE Dynamic"), LabelWidth(120)] [SerializeField] private RectTransform _qteRoot;
    [BoxGroup("Skill QTE Dynamic"), LabelWidth(120)] [SerializeField] private TextMeshProUGUI _targetKeyLabel;
    #endregion

    #region [ Internal State ]
    private Tweener _barTween;
    private Sequence _resultSequence;

    private Canvas _parentCanvas;
    private Camera _uiCamera;
    private RectTransform _baselineQteRoot;
    private Vector2 _qteBaselinePosition;
    private Vector3 _qteBaselineScale;
    private TextMeshProUGUI _baselineKeyLabel;
    private float _keyBaselineFontSize;
    private bool _keyBaselineAutoSizing;
    private TextMeshProUGUI _baselineResultLabel;
    private float _resultBaselineFontSize;
    private bool _resultBaselineAutoSizing;
    private bool _showingTimedGuard;
    private int _guardDisplayState = -1;
    private string _guardReadyText;
    private string _guardActiveText;
    private string _guardEndedText;
    #endregion

    protected override void Awake()
    {
        base.Awake();
        _parentCanvas = GetComponentInParent<Canvas>();
        if (_parentCanvas != null)
        {
            _uiCamera = _parentCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : _parentCanvas.worldCamera;
        }
        CapturePresentationBaseline();
    }

    #region [ Defense QTE Logic ]
    public void ShowQTE(float attackDelay, string attackTypeName = "ATTACK")
    {
        CapturePresentationBaseline();
        ResetState();
        RestoreDefenseRootBaseline();
        ShowImmediate();
        if (_targetKeyLabel != null)
            _targetKeyLabel.text = attackTypeName ?? "";

        if (_barFill != null)
        {
            _barFill.fillAmount = 1f;
            _barFill.color = Color.white;

            _barTween = _barFill.DOFillAmount(0f, attackDelay)
                .SetEase(Ease.Linear)
                .SetId(this)
                .SetUpdate(true)
                .OnUpdate(() =>
                {
                    if (_barFill.fillAmount < 0.35f)
                        _barFill.color = new Color(1f, 0.2f, 0.2f);
                });
        }
    }

    public void ShowQTE(DefenseQteRequest request)
    {
        ShowQTE(request.Duration);
        if (!request.UseTimedGuard)
        {
            if (_targetKeyLabel != null)
                _targetKeyLabel.text = GetLegacyDefenseKeys(request.Requirement);
            return;
        }

        _showingTimedGuard = true;
        string key = GetConfiguredKeyName(ConfigurableAction.Confirm, "Z");
        _guardReadyText = key + " 방어\n<size=65%>정확히 누르면 퍼펙트</size>";
        _guardActiveText = key + " 방어 중";
        _guardEndedText = key + " 방어 종료";
        if (_targetKeyLabel != null)
        {
            _targetKeyLabel.enableAutoSizing = false;
            _targetKeyLabel.fontSize = Mathf.Min(_keyBaselineFontSize, 24f);
        }
        UpdateDefenseGuard(request.GuardDuration, false);
    }

    public void UpdateDefenseGuard(float remainingSeconds, bool attempted)
    {
        if (!_showingTimedGuard) return;
        int state = !attempted ? 0 : remainingSeconds > 0f ? 1 : 2;
        if (_guardDisplayState == state) return;
        _guardDisplayState = state;
        if (_targetKeyLabel == null) return;
        _targetKeyLabel.text = state == 0 ? _guardReadyText : state == 1 ? _guardActiveText : _guardEndedText;
    }

    public void ShowResult(DefenseQteResult result)
    {
        string text = result.IsPerfectParry ? "퍼펙트 패링"
            : result.IsGuard ? "방어"
            : _showingTimedGuard ? "피격"
            : result.Outcome switch
        {
            DefenseOutcome.Invalid => "INVALID",
            DefenseOutcome.Failure => "MISS",
            _ => GetDefenseResultText(result.Grade, result.Input)
        };
        Color color = result.IsPerfectParry ? _colorPerfect
            : result.IsGuard ? _colorGood
            : result.Outcome switch
        {
            DefenseOutcome.Invalid => _colorBad,
            DefenseOutcome.Failure => _colorMiss,
            _ => GetResultColor(result.Grade)
        };
        ShowDefenseResult(text, color);
    }

    public void ShowResult(QTEManager.QTEGrade grade, DefenseInput input)
    {
        ShowDefenseResult(GetDefenseResultText(grade, input), GetResultColor(grade));
    }

    private void ShowDefenseResult(string text, Color color)
    {
        CapturePresentationBaseline();
        ResetState();
        ShowImmediate();
        if (_resultLabel == null) { Hide(); return; }

        _resultLabel.enableAutoSizing = false;
        _resultLabel.fontSize = Mathf.Min(_resultBaselineFontSize, 24f);
        _resultLabel.text = text;
        _resultLabel.color = color;
        _resultLabel.alpha = 0f;
        _resultLabel.transform.localScale = Vector3.one * 0.5f;

        _resultSequence = DOTween.Sequence()
            .SetId(this)
            .SetUpdate(true)
            .Append(_resultLabel.DOFade(1f, 0.08f))
            .Join(_resultLabel.transform.DOScale(Vector3.one, 0.12f).SetEase(Ease.OutBack))
            .AppendCallback(() => _resultLabel.transform
                .DOPunchScale(Vector3.one * 0.3f, 0.2f, 8, 0.5f)
                .SetId(this)
                .SetUpdate(true))
            .AppendInterval(0.75f)
            .Append(_resultLabel.DOFade(0f, 0.15f))
            .OnComplete(Hide);
    }
    #endregion

    #region [ Skill QTE Logic ]
    public void ShowSkillQTE(Vector2 relativePos, string targetKey, float duration)
    {
        CapturePresentationBaseline();
        ResetState();
        bool isWarmUp = string.IsNullOrEmpty(targetKey) && duration <= 0;
        ShowImmediate();
        if (isWarmUp)
        {
            if (!gameObject.activeSelf) gameObject.SetActive(true);
            _canvasGroup.alpha = 0f;
            _canvasGroup.interactable = false;
            _canvasGroup.blocksRaycasts = false;

            if (_qteRoot != null) _qteRoot.anchoredPosition = new Vector2(-9999, -9999);
        }
        else
        {
            ShowImmediate();
            _canvasGroup.alpha = 1f;
            _canvasGroup.interactable = true;
            _canvasGroup.blocksRaycasts = true;

            if (_qteRoot != null && _parentCanvas != null)
            {
                RectTransform canvasRect = _parentCanvas.GetComponent<RectTransform>();
                float targetX = (relativePos.x - 0.5f) * canvasRect.rect.width;
                float targetY = (relativePos.y - 0.5f) * canvasRect.rect.height;
                _qteRoot.anchoredPosition = new Vector2(Mathf.Round(targetX), Mathf.Round(targetY));
            }
        }

        if (_qteRoot != null) _qteRoot.localScale = Vector3.one;

        if (!isWarmUp && _targetKeyLabel != null)
            _targetKeyLabel.text = targetKey;

        if (!isWarmUp && _barFill != null)
        {
            _barFill.fillAmount = 1f;
            _barFill.color = new Color(0.2f, 0.8f, 1f);
            if (duration > 0f)
            {
                _barTween = _barFill.DOFillAmount(0f, duration)
                    .SetEase(Ease.Linear)
                    .SetId(this)
                    .SetUpdate(true);
            }
        }
    }

    public void ShowSkillResult(bool isHit)
    {
        ResetState();
        ShowImmediate();
        if (_resultLabel == null) { Hide(); return; }

        _resultLabel.text = isHit ? "HIT!" : "MISS";
        _resultLabel.color = isHit ? new Color(0.2f, 1f, 0.4f) : new Color(1f, 0.3f, 0.3f);
        _resultLabel.alpha = 0f;
        _resultLabel.transform.localScale = Vector3.one * 0.5f;

        _resultSequence = DOTween.Sequence()
            .SetId(this)
            .SetUpdate(true)
            .Append(_resultLabel.DOFade(1f, 0.08f))
            .Join(_resultLabel.transform.DOScale(Vector3.one, 0.12f).SetEase(Ease.OutBack))
            .AppendCallback(() => _resultLabel.transform
                .DOPunchScale(Vector3.one * 0.3f, 0.2f, 8, 0.5f)
                .SetId(this)
                .SetUpdate(true))
            .AppendInterval(0.35f)
            .Append(_resultLabel.DOFade(0f, 0.12f));
    }
    #endregion

    #region [ Utilities & Cleanup ]
    protected override void OnHideComplete()
    {
        ResetState();
        if (_barFill != null)
        {
            _barFill.fillAmount = 1f;
            _barFill.color = Color.white;
        }
    }

    protected override void OnDisable()
    {
        base.OnDisable();
        ResetState();
    }

    protected override void OnDestroy()
    {
        base.OnDestroy();
        ResetState();
    }

    private void ResetState()
    {
        DOTween.Kill(this, false);
        _barTween = null;
        _resultSequence = null;
        _showingTimedGuard = false;
        _guardDisplayState = -1;
        _guardReadyText = null;
        _guardActiveText = null;
        _guardEndedText = null;

        if (_targetKeyLabel != null)
        {
            _targetKeyLabel.text = "";
            if (_baselineKeyLabel == _targetKeyLabel)
            {
                _targetKeyLabel.fontSize = _keyBaselineFontSize;
                _targetKeyLabel.enableAutoSizing = _keyBaselineAutoSizing;
            }
        }

        if (_resultLabel != null)
        {
            _resultLabel.DOKill(false);
            _resultLabel.transform.DOKill(false);
            _resultLabel.text = "";
            _resultLabel.alpha = 0f;
            if (_baselineResultLabel == _resultLabel)
            {
                _resultLabel.fontSize = _resultBaselineFontSize;
                _resultLabel.enableAutoSizing = _resultBaselineAutoSizing;
            }
        }
    }

    private void CapturePresentationBaseline()
    {
        if (_qteRoot != null && _baselineQteRoot != _qteRoot)
        {
            _baselineQteRoot = _qteRoot;
            _qteBaselinePosition = _qteRoot.anchoredPosition;
            _qteBaselineScale = _qteRoot.localScale;
        }
        if (_targetKeyLabel != null && _baselineKeyLabel != _targetKeyLabel)
        {
            _baselineKeyLabel = _targetKeyLabel;
            _keyBaselineFontSize = _targetKeyLabel.fontSize;
            _keyBaselineAutoSizing = _targetKeyLabel.enableAutoSizing;
        }
        if (_resultLabel != null && _baselineResultLabel != _resultLabel)
        {
            _baselineResultLabel = _resultLabel;
            _resultBaselineFontSize = _resultLabel.fontSize;
            _resultBaselineAutoSizing = _resultLabel.enableAutoSizing;
        }
    }

    private void RestoreDefenseRootBaseline()
    {
        if (_qteRoot == null || _baselineQteRoot != _qteRoot) return;
        _qteRoot.anchoredPosition = _qteBaselinePosition;
        _qteRoot.localScale = _qteBaselineScale;
    }

    private static string GetConfiguredKeyName(ConfigurableAction action, string fallback)
    {
        return GameConfigManager.Instance != null
            ? GameConfigManager.Instance.GetKey(action).ToString()
            : fallback;
    }

    private static string GetLegacyDefenseKeys(DefenseRequirement requirement)
    {
        string parry = GetConfiguredKeyName(ConfigurableAction.Confirm, "Z");
        string dodge = GetConfiguredKeyName(ConfigurableAction.Cancel, "X");
        string jump = GetConfiguredKeyName(ConfigurableAction.Menu, "C");
        return requirement switch
        {
            DefenseRequirement.ParryOnly => parry,
            DefenseRequirement.DodgeOnly => dodge,
            DefenseRequirement.JumpOnly => jump,
            DefenseRequirement.ParryOrDodge => parry + " / " + dodge,
            DefenseRequirement.DodgeOrJump => dodge + " / " + jump,
            _ => parry + " / " + dodge + " / " + jump
        };
    }

    private string GetDefenseResultText(QTEManager.QTEGrade grade, DefenseInput input)
    {
        string inputName = input switch
        {
            DefenseInput.Parry => "패링",
            DefenseInput.Dodge => "회피",
            DefenseInput.Jump => "점프",
            _ => ""
        };
        return grade switch
        {
            QTEManager.QTEGrade.Perfect => $"PERFECT!\n<size=70%>{inputName}</size>",
            QTEManager.QTEGrade.Great => $"GREAT!\n<size=70%>{inputName}</size>",
            QTEManager.QTEGrade.Good => $"GOOD\n<size=70%>{inputName}</size>",
            QTEManager.QTEGrade.Bad => $"BAD\n<size=70%>{inputName}</size>",
            _ => "MISS",
        };
    }

    private Color GetResultColor(QTEManager.QTEGrade grade) => grade switch
    {
        QTEManager.QTEGrade.Perfect => _colorPerfect,
        QTEManager.QTEGrade.Great => _colorGreat,
        QTEManager.QTEGrade.Good => _colorGood,
        QTEManager.QTEGrade.Bad => _colorBad,
        _ => _colorMiss,
    };
    #endregion
}
