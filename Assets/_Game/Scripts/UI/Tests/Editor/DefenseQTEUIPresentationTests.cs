using DG.Tweening;
using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEngine;

public class DefenseQTEUIPresentationTests
{
    private GameObject _root;
    private TextMeshProUGUI _resultLabel;
    private TextMeshProUGUI _keyLabel;
    private DefenseQTEUITestDouble _ui;

    [SetUp]
    public void SetUp()
    {
        _root = new GameObject(
            "DefenseQTEUIPresentationTests",
            typeof(RectTransform),
            typeof(CanvasGroup));
        _ui = _root.AddComponent<DefenseQTEUITestDouble>();
        RectTransform panel = (RectTransform)_root.transform;
        panel.sizeDelta = new Vector2(640f, 480f);
        SetPrivateField(_ui, "_qteRoot", panel);

        var ringObject = new GameObject("ScaledCountdownRing", typeof(RectTransform));
        ringObject.transform.SetParent(_root.transform, false);
        ringObject.transform.localScale = Vector3.one * 0.5f;
        var keyObject = new GameObject("KeyLabel", typeof(RectTransform));
        keyObject.transform.SetParent(ringObject.transform, false);
        _keyLabel = keyObject.AddComponent<TextMeshProUGUI>();
        _keyLabel.rectTransform.sizeDelta = new Vector2(200f, 50f);
        _keyLabel.fontSize = 60f;
        SetPrivateField(_ui, "_targetKeyLabel", _keyLabel);

        var labelObject = new GameObject("ResultLabel", typeof(RectTransform));
        labelObject.transform.SetParent(_root.transform, false);
        _resultLabel = labelObject.AddComponent<TextMeshProUGUI>();
        SetPrivateField(_ui, "_resultLabel", _resultLabel);
    }

    [TearDown]
    public void TearDown()
    {
        _ui?.InvokeDisableLifecycleForTest();
        Object.DestroyImmediate(_root);
    }

    [Test]
    public void ShowResult_DistinguishesInvalidInputFromTimeout()
    {
        _ui.ShowResult(CreateResult(DefenseInputReadStatus.Ambiguous, DefenseOutcome.Invalid));
        Assert.That(_resultLabel.text, Is.EqualTo("INVALID"));

        _ui.ShowResult(CreateResult(DefenseInputReadStatus.None, DefenseOutcome.Failure));
        Assert.That(_resultLabel.text, Is.EqualTo("MISS"));
    }

    [Test]
    public void Disable_CancelsOwnedResultSequence()
    {
        _ui.ShowResult(CreateResult(DefenseInputReadStatus.Valid, DefenseOutcome.Success));
        Sequence sequence = GetPrivateField<Sequence>(_ui, "_resultSequence");

        Assert.That(sequence, Is.Not.Null);
        Assert.That(sequence.IsActive(), Is.True);

        _ui.InvokeDisableLifecycleForTest();

        Assert.That(GetPrivateField<Sequence>(_ui, "_resultSequence"), Is.Null,
            "Disabling DefenseQTEUI must release its owned sequence reference.");
        Assert.That(sequence.IsActive(), Is.False,
            "Disabling DefenseQTEUI must cancel its owned result sequence.");
    }

    [Test]
    public void ActiveDefense_OrdinaryAttackShowsGuardDodgeAndDisabledCounter()
    {
        _ui.ShowQTE(CreateActiveRequest(DefenseRequirement.Any));

        Assert.That(_keyLabel.text, Does.Contain("Z 가드"));
        Assert.That(_keyLabel.text, Does.Contain("X 회피"));
        Assert.That(_keyLabel.text, Does.Contain("<color=#929292>C 반격 불가</color>"));
        Assert.That(_keyLabel.text, Does.Contain("저스트 가드"));
        Assert.That(_keyLabel.fontSize * _keyLabel.transform.parent.localScale.y, Is.EqualTo(18f).Within(0.01f));
        Assert.That(_keyLabel.rectTransform.rect.width * _keyLabel.transform.parent.localScale.x, Is.EqualTo(480f).Within(0.01f));
    }

    [Test]
    public void ActiveDefense_CounterableAttackExplicitlyDisablesGuardAndHighlightsCounter()
    {
        _ui.ShowQTE(CreateActiveRequest(DefenseRequirement.Counterable));

        Assert.That(_keyLabel.text, Does.Contain("<color=#929292>Z 가드</color>"));
        Assert.That(_keyLabel.text, Does.Contain("<color=#FFE879>C 반격</color>"));
        Assert.That(_keyLabel.text, Does.Contain("가드 불가 · 연계 반격 기회"));
        _ui.UpdateActiveDefense(true, DefenseInput.None);
        Assert.That(_keyLabel.text, Does.Not.Contain("가드 중"));
    }

    [TestCase(DefenseRequirement.DodgeOnly)]
    [TestCase(DefenseRequirement.JumpOnly)]
    [TestCase(DefenseRequirement.DodgeOrJump)]
    public void ActiveDefense_LegacyEvadeRequirementsDoNotOfferJumpOrCounter(DefenseRequirement requirement)
    {
        _ui.ShowQTE(CreateActiveRequest(requirement));

        Assert.That(_keyLabel.text, Does.Contain("가드 불가 · 회피"));
        Assert.That(_keyLabel.text, Does.Contain("C 반격 불가"));
        Assert.That(_keyLabel.text, Does.Not.Contain("점프"));
    }

    [Test]
    public void ActiveDefense_HeldGuardHasNoExpiryAndDodgeAttemptTakesPresentationPriority()
    {
        _ui.ShowQTE(CreateActiveRequest(DefenseRequirement.Any));
        _ui.UpdateActiveDefense(true, DefenseInput.Parry);
        Assert.That(_keyLabel.text, Does.Contain("가드 중"));
        _ui.UpdateDefenseGuard(0f, true);
        Assert.That(_keyLabel.text, Does.Not.Contain("종료"));

        _ui.UpdateActiveDefense(true, DefenseInput.Dodge);
        Assert.That(_keyLabel.text, Does.Contain("회피 시도"));
        Assert.That(_keyLabel.text, Does.Not.Contain("가드 중"));
    }

    [Test]
    public void ActiveDefense_AmbiguousInputDoesNotAdvertiseHeldGuard()
    {
        _ui.ShowQTE(CreateActiveRequest(DefenseRequirement.Any));
        _ui.UpdateActiveDefense(true, DefenseInput.None, DefenseInputReadStatus.Ambiguous);

        Assert.That(_keyLabel.text, Does.Contain("동시 입력"));
        Assert.That(_keyLabel.text, Does.Not.Contain("가드 중"));
    }

    [Test]
    public void ActiveDefense_OrdinaryAttackDoesNotAdvertiseCounterAfterInvalidC()
    {
        _ui.ShowQTE(CreateActiveRequest(DefenseRequirement.Any));
        _ui.UpdateActiveDefense(true, DefenseInput.Counter, DefenseInputReadStatus.Valid);

        Assert.That(_keyLabel.text, Does.Contain("반격 불가 · 이 타격"));
        Assert.That(_keyLabel.text, Does.Not.Contain("연계 반격 시도"));
        Assert.That(_keyLabel.text, Does.Not.Contain("가드 중"));
    }

    [Test]
    public void ActiveDefense_UsesCenteredLabelAndRestoresAuthoredTransformAndMargins()
    {
        _keyLabel.alignment = TextAlignmentOptions.TopLeft;
        _keyLabel.margin = new Vector4(8f, 9f, 10f, 11f);
        _keyLabel.rectTransform.localScale = new Vector3(0.75f, 1.25f, 1f);
        _keyLabel.rectTransform.anchoredPosition3D = new Vector3(7f, 8f, 9f);

        _ui.ShowQTE(CreateActiveRequest(DefenseRequirement.Any));
        Assert.That(_keyLabel.alignment, Is.EqualTo(TextAlignmentOptions.Center));
        Assert.That(_keyLabel.margin, Is.EqualTo(Vector4.zero));
        Assert.That(_keyLabel.rectTransform.localScale, Is.EqualTo(Vector3.one));

        _ui.ShowSkillQTE(Vector2.one * 0.5f, "C", 0.5f);
        Assert.That(_keyLabel.alignment, Is.EqualTo(TextAlignmentOptions.TopLeft));
        Assert.That(_keyLabel.margin, Is.EqualTo(new Vector4(8f, 9f, 10f, 11f)));
        Assert.That(_keyLabel.rectTransform.localScale, Is.EqualTo(new Vector3(0.75f, 1.25f, 1f)));
        Assert.That(_keyLabel.rectTransform.anchoredPosition3D, Is.EqualTo(new Vector3(7f, 8f, 9f)));
    }

    [Test]
    public void AttackQteAfterActiveDefenseRestoresOriginalKeyLayout()
    {
        _ui.ShowQTE(CreateActiveRequest(DefenseRequirement.Any));
        _ui.ShowSkillQTE(Vector2.one * 0.5f, "C", 0.5f);

        Assert.That(_keyLabel.text, Is.EqualTo("C"));
        Assert.That(_keyLabel.fontSize, Is.EqualTo(60f));
        Assert.That(_keyLabel.rectTransform.sizeDelta, Is.EqualTo(new Vector2(200f, 50f)));
        Assert.That(_keyLabel.rectTransform.anchoredPosition, Is.EqualTo(Vector2.zero));
    }

    [TestCase(DefenseInput.Parry, DefenseOutcome.Success, QTEManager.QTEGrade.Perfect, true, "저스트 가드")]
    [TestCase(DefenseInput.Parry, DefenseOutcome.Guarded, QTEManager.QTEGrade.Good, false, "가드 · 피해 감소")]
    [TestCase(DefenseInput.Dodge, DefenseOutcome.Success, QTEManager.QTEGrade.Great, true, "회피")]
    [TestCase(DefenseInput.Counter, DefenseOutcome.Success, QTEManager.QTEGrade.Perfect, true, "연계 반격")]
    public void ActiveDefense_ResultNamesDescribeGameplay(
        DefenseInput input, DefenseOutcome outcome, QTEManager.QTEGrade grade, bool preventsDamage, string expected)
    {
        _ui.ShowQTE(CreateActiveRequest(DefenseRequirement.Counterable));
        _ui.ShowResult(new DefenseQteResult(DefenseInputReadStatus.Valid, input, grade, outcome,
            DefenseRequirement.Counterable, 0.05f, true, preventsDamage, 0.5f));

        Assert.That(_resultLabel.text, Is.EqualTo(expected));
        Assert.That(_resultLabel.rectTransform.anchoredPosition, Is.EqualTo(new Vector2(0f, 60f)));
    }

    [Test]
    public void DefensePause_PausesAndResumesOnlyTheCountdownTween()
    {
        var barObject = new GameObject("Bar", typeof(RectTransform));
        barObject.transform.SetParent(_root.transform, false);
        var bar = barObject.AddComponent<UnityEngine.UI.Image>();
        SetPrivateField(_ui, "_barFill", bar);
        _ui.ShowQTE(CreateActiveRequest(DefenseRequirement.Any));
        Tweener tween = GetPrivateField<Tweener>(_ui, "_barTween");

        _ui.SetDefenseQTEPaused(true);
        Assert.That(tween.IsPlaying(), Is.False);
        _ui.SetDefenseQTEPaused(false);
        Assert.That(tween.IsPlaying(), Is.True);
    }

    private static DefenseQteRequest CreateActiveRequest(DefenseRequirement requirement)
    {
        return new DefenseQteRequest(0.8f, 1f, requirement,
            new DefenseTimingProfile(0.12f, 0.22f, 0.4f), useActiveDefense: true);
    }

    private static DefenseQteResult CreateResult(
        DefenseInputReadStatus inputStatus,
        DefenseOutcome outcome)
    {
        return new DefenseQteResult(
            inputStatus,
            DefenseInput.None,
            QTEManager.QTEGrade.Miss,
            outcome,
            DefenseRequirement.Any,
            0f,
            false,
            false);
    }

    private static void SetPrivateField(object target, string fieldName, object value)
    {
        FieldInfo field = typeof(DefenseQTEUI).GetField(
            fieldName,
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null);
        field.SetValue(target, value);
    }

    private static T GetPrivateField<T>(object target, string fieldName)
    {
        FieldInfo field = typeof(DefenseQTEUI).GetField(
            fieldName,
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null);
        return (T)field.GetValue(target);
    }
}

public sealed class DefenseQTEUITestDouble : DefenseQTEUI
{
    public void InvokeDisableLifecycleForTest()
    {
        base.OnDisable();
    }
}
