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

    public Image RowBackground;
    public Image IdentityStrip;
    public Image TargetBorder;
    public BattleStatusIconStrip StatusIcons;
    [Tooltip("텍스처 틴트 없이 대표색을 그대로 사용합니다. 막대 RectTransform의 왼쪽 피벗을 유지하세요.")]
    public bool SolidColorBars;

    private PlayerCharacter _player;
    private BattleStatusIconDefinition[] _statusDefinitions;
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
    private Tween _apColorTween;
    private Tween _apTextTween;
    private Tween _feedbackTween;
    private Tween _targetTween;
    private bool _targeted;

    public void Init(PlayerCharacter player)
    {
        Unbind();
        ReleaseTweens();
        if (player == null) { Hide(); return; }
        _player = player;
        _player.OnHPChanged += HandleHPChanged;
        _player.OnAPChanged += HandleAPChanged;
        _player.OnStatusEffectsChanged += HandleStatusChanged;
        if (Root != null) Root.SetActive(true);
        if (NameText != null) NameText.text = player.DisplayName;
        if (Portrait != null)
        {
            Portrait.sprite = player.BattlePortrait;
            Portrait.enabled = Portrait.sprite != null;
            Portrait.preserveAspect = true;
            Portrait.color = Color.white;
        }
        if (HPFill != null) HPFill.color = player.BattleSymbolColor;
        if (IdentityStrip != null) IdentityStrip.color = player.BattleSymbolColor;
        if (APFill != null) APFill.color = new Color(1f, 0.87f, 0.35f);
        SetHighlight(false);
        SetTargeted(false);
        RefreshStatus();
        RefreshHP(player.CurrentHP, player.MaxHP, 0f, Ease.Linear);
        RefreshAP(player.CurrentAP, player.MaxAP, 0f, Ease.Linear);
    }

    public void Hide()
    {
        Unbind();
        ReleaseTweens();
        if (Root != null) Root.SetActive(false);
    }

    public void SetStatusDefinitions(BattleStatusIconDefinition[] definitions)
    {
        _statusDefinitions = definitions;
        RefreshStatus();
    }

    public void Unbind()
    {
        // ReferenceEquals: 이미 파괴된 Unity 객체에도 C# 이벤트 구독을 해제합니다.
        if (ReferenceEquals(_player, null)) return;
        _player.OnHPChanged -= HandleHPChanged;
        _player.OnAPChanged -= HandleAPChanged;
        _player.OnStatusEffectsChanged -= HandleStatusChanged;
        _player = null;
    }

    private void HandleHPChanged(CharacterBase actor, int value, int max) => RefreshHP(value, max, 0.2f, Ease.OutQuad);
    private void HandleAPChanged(CharacterBase actor, int value, int max) => RefreshAP(value, max, 0.2f, Ease.OutQuad);
    private void HandleStatusChanged(CharacterBase actor) => RefreshStatus();
    private void RefreshStatus()
    {
        if (StatusIcons != null) StatusIcons.Refresh(_player != null ? _player.ActiveStatusEffects : null, _statusDefinitions);
    }

    public void SetTargeted(bool active)
    {
        bool changed = _targeted != active;
        _targeted = active;
        if (TargetBorder == null) return;
        TargetBorder.enabled = active;
        if (!changed) return;
        Kill(ref _targetTween);
        if (active && Application.isPlaying && CanAnimate(TargetBorder, 0.2f))
            _targetTween = PulseColor(TargetBorder, Color.white, 0.2f);
    }

    public void ReleaseTweens()
    {
        Kill(ref _portraitTween);
        Kill(ref _hpFillTween);
        Kill(ref _hpColorTween);
        Kill(ref _hpTextTween);
        Kill(ref _apFillTween);
        Kill(ref _apColorTween);
        Kill(ref _apTextTween);
        Kill(ref _feedbackTween);
        Kill(ref _targetTween);
        _targeted = false;
        if (TargetBorder != null) TargetBorder.enabled = false;
        _hasHighlight = false;
        if (_hasHP) RefreshHP(_targetHP, _maxHP, 0f, Ease.Linear);
        if (_hasAP) RefreshAP(_targetAP, _maxAP, 0f, Ease.Linear);
    }

    public void SetHighlight(bool active)
    {
        if (_hasHighlight && _highlighted == active) return;
        if (RowBackground != null)
        {
            _hasHighlight = true;
            _highlighted = active;
            RowBackground.color = active ? new Color(1f, 0.87f, 0.35f) : Color.black;
            Color foreground = active ? Color.black : Color.white;
            if (NameText != null) NameText.color = foreground;
            if (HPText != null) HPText.color = foreground;
            if (APText != null) APText.color = foreground;
            return;
        }
        if (Portrait == null) return;
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
        // 캐릭터 알림과 전투 이벤트에서 같은 값이 와도 표시 전환을 재시작하지 않습니다.
        if (duration > 0f && _hasHP && current == _targetHP && max == _maxHP) return;
        bool changed = _hasHP && current != _targetHP;
        bool lostHealth = _hasHP && current < _targetHP;
        _targetHP = current; _maxHP = max; _hasHP = true;
        Kill(ref _hpFillTween);
        Kill(ref _hpColorTween);
        Kill(ref _hpTextTween);
        bool decreased = current < _displayHP;
        bool increased = current > _displayHP;
        _hpFillTween = RefreshFill(HPFill, current, max, duration, ease);
        if (RowBackground != null && changed && Application.isPlaying && CanAnimate(HPFill, duration))
        {
            _hpColorTween = PulseColor(HPFill,
                lostHealth ? new Color(1f, 0.48f, 0.48f) : new Color(0.65f, 1f, 0.75f), 0.22f);
            if (lostHealth) TapPortrait();
        }
        if (RowBackground == null && CanAnimate(HPFill, duration) && (decreased || increased))
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
            if (text != null) text.SetText("HP {0}/{1}", current, max);
            return;
        }
        _hpTextTween = DOTween.To(() => _displayHP, value =>
        {
            _displayHP = value;
            if (text != null) text.SetText("HP {0}/{1}", value, max);
        }, current, duration).SetEase(ease).SetTarget(text).SetRecyclable(false)
            .SetLink(text.gameObject, LinkBehaviour.KillOnDisable);
    }

    public void RefreshAP(int current, int max, float duration, Ease ease)
    {
        if (duration > 0f && _hasAP && current == _targetAP && max == _maxAP) return;
        bool changed = _hasAP && current != _targetAP;
        _targetAP = current; _maxAP = max; _hasAP = true;
        Kill(ref _apFillTween);
        Kill(ref _apColorTween);
        Kill(ref _apTextTween);
        _apFillTween = RefreshFill(APFill, current, max, duration, ease);
        if (RowBackground != null && changed && Application.isPlaying && CanAnimate(APFill, duration))
            _apColorTween = PulseColor(APFill, Color.white, 0.18f);
        if (RowBackground == null && duration > 0f && current != _displayAP)
            Punch(current < _displayAP ? new Vector3(0.03f, 0.03f, 0f) : new Vector3(0f, 5f, 0f),
                current < _displayAP, 0.2f, 5, 1f);
        TextMeshProUGUI text = APText;
        if (!CanAnimate(text, duration))
        {
            _displayAP = current;
            if (text != null) text.SetText("AP {0}/{1}", current, max);
            return;
        }
        _apTextTween = DOTween.To(() => _displayAP, value =>
        {
            _displayAP = value;
            if (text != null) text.SetText("AP {0}/{1}", value, max);
        }, current, duration).SetEase(ease).SetTarget(text).SetRecyclable(false)
            .SetLink(text.gameObject, LinkBehaviour.KillOnDisable);
    }

    private Tween RefreshFill(Image image, int current, int max, float duration, Ease ease)
    {
        if (image == null) return null;
        float ratio = max > 0 ? Mathf.Clamp01((float)current / max) : 0f;
        if (!CanAnimate(image, duration)) { SetFill(image, ratio); return null; }
        return DOTween.To(() => image != null ? image.fillAmount : ratio,
            value => { if (image != null) SetFill(image, value); }, ratio, duration)
            .SetEase(ease).SetTarget(image).SetRecyclable(false)
            .SetLink(image.gameObject, LinkBehaviour.KillOnDisable);
    }

    private void SetFill(Image image, float ratio)
    {
        image.fillAmount = ratio;
        // Sprite 없는 Image는 Filled 메쉬가 만들어지지 않습니다. 순색 막대만 폭 배율로 표시합니다.
        if (SolidColorBars)
            image.rectTransform.localScale = new Vector3(ratio, 1f, 1f);
    }

    private static Tween TweenColor(Image image, Color color, float duration)
    {
        // GameObject뿐 아니라 Image 컴포넌트만 먼저 파괴되는 경우도 안전합니다.
        return DOTween.To(() => image != null ? image.color : color,
            value => { if (image != null) image.color = value; }, color, duration)
            .SetTarget(image).SetRecyclable(false)
            .SetLink(image.gameObject, LinkBehaviour.KillOnDisable);
    }

    private static Tween PulseColor(Image image, Color accent, float duration)
    {
        if (BattleUIController.JuiceIntensity <= 0f) return null;
        Color baseline = image.color;
        image.color = Color.Lerp(baseline, accent, 0.7f * BattleUIController.JuiceIntensity);
        return TweenColor(image, baseline, duration * BattleUIController.JuiceDurationScale).SetEase(Ease.OutQuad).SetUpdate(true)
            .OnKill(() => { if (image != null) image.color = baseline; });
    }

    private void TapPortrait()
    {
        Kill(ref _feedbackTween);
        if (Portrait == null || !Portrait.gameObject.activeInHierarchy || BattleUIController.JuiceIntensity <= 0f) return;
        RectTransform portrait = Portrait.rectTransform;
        Vector2 home = portrait.anchoredPosition;
        float intensity = BattleUIController.JuiceIntensity;
        _feedbackTween = DOTween.To(() => -2f * intensity, offset =>
            {
                if (portrait != null) portrait.anchoredPosition = home + Vector2.right * Mathf.Round(offset);
            }, 0f, 0.18f * BattleUIController.JuiceDurationScale).SetEase(Ease.OutQuad).SetUpdate(true).SetRecyclable(false)
            .SetLink(portrait.gameObject, LinkBehaviour.KillOnDisable)
            .OnKill(() => { if (portrait != null) portrait.anchoredPosition = home; });
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
