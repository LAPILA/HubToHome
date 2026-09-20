using DG.Tweening;
using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

/// <summary>파티 한 슬롯의 표시와 트윈 수명. 직렬화 필드 이름은 기존 프리팹과 동일합니다.</summary>
[System.Serializable]
public class PartySlotUI
{
    [HorizontalGroup("Row"), LabelWidth(60)] public Image Portrait;
    [HorizontalGroup("Row"), LabelWidth(60)] public TextMeshProUGUI NameText;
    [HorizontalGroup("Row2"), LabelWidth(60)] public Image HPFill;
    [HorizontalGroup("Row2"), LabelWidth(60)] public TextMeshProUGUI HPText;
    [FormerlySerializedAs("MPFill")]
    [HorizontalGroup("Row3"), LabelWidth(60)] public Image APFill;
    [FormerlySerializedAs("MPText")]
    [HorizontalGroup("Row3"), LabelWidth(60)] public TextMeshProUGUI APText;
    [HorizontalGroup("Row4"), LabelWidth(60)] public GameObject Root;

    private int _displayHP;
    private int _displayAP;
    private int _targetHP, _maxHP, _targetAP, _maxAP;
    private bool _hasHP, _hasAP;
    private bool _hasHighlight;
    private bool _highlighted;
    private Tween _portraitTween;
    private Tween _hpFillTween;
    private Tween _hpColorTween;
    private Tween _hpTextTween;
    private Tween _apFillTween;
    private Tween _apTextTween;
    private Tween _feedbackTween;

    public void Init(PlayerCharacter player)
    {
        ReleaseTweens();
        if (player == null) { Hide(); return; }
        if (Root != null) Root.SetActive(true);
        if (NameText != null) NameText.text = player.DisplayName;
        if (Portrait != null)
        {
            Portrait.sprite = player.BattlePortrait;
            Portrait.enabled = Portrait.sprite != null;
            Portrait.preserveAspect = true;
            Portrait.color = Color.white;
        }
        RefreshHP(player.CurrentHP, player.MaxHP, 0f, Ease.Linear);
        RefreshAP(player.CurrentAP, player.MaxAP, 0f, Ease.Linear);
    }

    public void Hide()
    {
        ReleaseTweens();
        if (Root != null) Root.SetActive(false);
    }

    public void ReleaseTweens()
    {
        Kill(ref _portraitTween);
        Kill(ref _hpFillTween);
        Kill(ref _hpColorTween);
        Kill(ref _hpTextTween);
        Kill(ref _apFillTween);
        Kill(ref _apTextTween);
        Kill(ref _feedbackTween);
        _hasHighlight = false;
        if (_hasHP) RefreshHP(_targetHP, _maxHP, 0f, Ease.Linear);
        if (_hasAP) RefreshAP(_targetAP, _maxAP, 0f, Ease.Linear);
    }

    public void SetHighlight(bool active)
    {
        if (Portrait == null || (_hasHighlight && _highlighted == active)) return;
        _hasHighlight = true;
        _highlighted = active;
        Kill(ref _portraitTween);
        Color color = active ? Color.yellow : Color.white;
        if (CanAnimate(Portrait, 0.15f))
            _portraitTween = TweenColor(Portrait, color, 0.15f);
        else Portrait.color = color;
        if (active) Punch(new Vector3(0f, 5f, 0f), false, 0.2f, 5, 1f);
    }

    public void RefreshHP(int current, int max, float duration, Ease ease)
    {
        _targetHP = current; _maxHP = max; _hasHP = true;
        Kill(ref _hpFillTween);
        Kill(ref _hpColorTween);
        Kill(ref _hpTextTween);
        bool decreased = current < _displayHP;
        bool increased = current > _displayHP;
        _hpFillTween = RefreshFill(HPFill, current, max, duration, ease);
        if (CanAnimate(HPFill, duration) && (decreased || increased))
        {
            Image image = HPFill;
            Color original = image.color;
            _hpColorTween = TweenColor(image, decreased ? Color.red : Color.green, 0.1f)
                .SetLoops(2, LoopType.Yoyo)
                .OnKill(() => { if (image != null) image.color = original; });
            Punch(decreased ? new Vector3(10f, 0f, 0f) : new Vector3(0.05f, 0.05f, 0f),
                !decreased, 0.3f, decreased ? 15 : 5, 1f);
        }
        TextMeshProUGUI text = HPText;
        if (!CanAnimate(text, duration))
        {
            _displayHP = current;
            if (text != null) text.SetText("{0}/{1}", current, max);
            return;
        }
        _hpTextTween = DOTween.To(() => _displayHP, value =>
        {
            _displayHP = value;
            if (text != null) text.SetText("{0}/{1}", value, max);
        }, current, duration).SetEase(ease).SetTarget(text).SetRecyclable(false)
            .SetLink(text.gameObject, LinkBehaviour.KillOnDisable);
    }

    public void RefreshAP(int current, int max, float duration, Ease ease)
    {
        _targetAP = current; _maxAP = max; _hasAP = true;
        Kill(ref _apFillTween);
        Kill(ref _apTextTween);
        _apFillTween = RefreshFill(APFill, current, max, duration, ease);
        if (duration > 0f && current != _displayAP)
            Punch(current < _displayAP ? new Vector3(0.03f, 0.03f, 0f) : new Vector3(0f, 5f, 0f),
                current < _displayAP, 0.2f, 5, 1f);
        TextMeshProUGUI text = APText;
        if (!CanAnimate(text, duration))
        {
            _displayAP = current;
            if (text != null) text.SetText("{0}/{1}", current, max);
            return;
        }
        _apTextTween = DOTween.To(() => _displayAP, value =>
        {
            _displayAP = value;
            if (text != null) text.SetText("{0}/{1}", value, max);
        }, current, duration).SetEase(ease).SetTarget(text).SetRecyclable(false)
            .SetLink(text.gameObject, LinkBehaviour.KillOnDisable);
    }

    private Tween RefreshFill(Image image, int current, int max, float duration, Ease ease)
    {
        if (image == null) return null;
        float ratio = max > 0 ? Mathf.Clamp01((float)current / max) : 0f;
        if (!CanAnimate(image, duration)) { image.fillAmount = ratio; return null; }
        return DOTween.To(() => image != null ? image.fillAmount : ratio,
            value => { if (image != null) image.fillAmount = value; }, ratio, duration)
            .SetEase(ease).SetTarget(image).SetRecyclable(false)
            .SetLink(image.gameObject, LinkBehaviour.KillOnDisable);
    }

    private static Tween TweenColor(Image image, Color color, float duration)
    {
        // GameObject뿐 아니라 Image 컴포넌트만 먼저 파괴되는 경우도 안전합니다.
        return DOTween.To(() => image != null ? image.color : color,
            value => { if (image != null) image.color = value; }, color, duration)
            .SetTarget(image).SetRecyclable(false)
            .SetLink(image.gameObject, LinkBehaviour.KillOnDisable);
    }

    private void Punch(Vector3 strength, bool scale, float duration, int vibrato, float elasticity)
    {
        Kill(ref _feedbackTween);
        if (Root == null || !Root.activeInHierarchy) return;
        Transform target = Root.transform;
        Vector3 position = target.localPosition;
        Vector3 originalScale = target.localScale;
        _feedbackTween = (scale
            ? target.DOPunchScale(strength, duration, vibrato, elasticity)
            : target.DOPunchPosition(strength, duration, vibrato, elasticity))
            .SetRecyclable(false).SetLink(Root, LinkBehaviour.KillOnDisable)
            .OnKill(() =>
            {
                if (target == null) return;
                target.localPosition = position;
                target.localScale = originalScale;
            });
    }

    private bool CanAnimate(Component target, float duration) => duration > 0f
        && target != null && target.gameObject.activeInHierarchy && (Root == null || Root.activeInHierarchy);

    private static void Kill(ref Tween tween)
    {
        Tween owned = tween;
        tween = null;
        if (owned != null && owned.IsActive()) owned.Kill(false);
    }
}
