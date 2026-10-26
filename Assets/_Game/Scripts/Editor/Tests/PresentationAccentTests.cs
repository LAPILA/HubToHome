#if UNITY_EDITOR
using System;
using System.IO;
using HubToHome.EditorTools.SkillMaker;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

public sealed class PresentationAccentTests
{
    [TestCase(Action_PresentationAccent.AccentMode.Silhouette)]
    [TestCase(Action_PresentationAccent.AccentMode.Flash)]
    public void DefaultModesHaveStableAuthoringContract(Action_PresentationAccent.AccentMode mode)
    {
        var block = new Action_PresentationAccent { Mode = mode };
        Assert.That(block.HasValidSettings, Is.True);
        Assert.That(block.CanRunWithoutLivingTargets, Is.True);
        Assert.That(block.BlockHeader, Does.Contain("화면 연출").And.Contain("명중 점멸"));
        Assert.That(block.GetAuthoringTiming().Duration,
            Is.EqualTo(mode == Action_PresentationAccent.AccentMode.Silhouette ? block.Duration : 0f));
        Assert.That(SkillMakerBlockEditor.BlockTypes, Does.Contain(typeof(Action_PresentationAccent)));
        Assert.That(SkillMakerBlockEditor.GetBlockMenuPath(typeof(Action_PresentationAccent)), Does.StartWith("공통 · 흐름과 연출/"));
        Assert.That(SkillMakerBlockEditor.GetBlockHelp(typeof(Action_PresentationAccent)), Does.Contain("명중"));
    }

    [Test]
    public void EnemyCombatSilhouetteDoesNotDelayButStoryDisplayStillWaits()
    {
        var actorObject = new GameObject("Accent flow actor", typeof(EnemyCharacter));
        var actor = actorObject.GetComponent<EnemyCharacter>();
        typeof(CharacterBase).GetField("_currentHP", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
            .SetValue(actor, 1);
        var settings = new Action_PresentationAccent { Strength = 0f };
        var context = new SkillContext { Actor = actor, IsContinuousEnemyAction = true };
        System.Collections.IEnumerator story = null;
        try
        {
            Assert.That(settings.Execute(context).MoveNext(), Is.False,
                "적 공격은 접근성 설정으로 표시가 꺼져 있어도 실루엣 때문에 기다리지 않습니다.");
            context.IsContinuousEnemyAction = false;
            story = settings.Execute(context);
            Assert.That(story.MoveNext(), Is.True, "일반 스토리 표시의 기존 시간 계약은 보존합니다.");
        }
        finally
        {
            (story as IDisposable)?.Dispose();
            context.CancelPresentationAccent();
            UnityEngine.Object.DestroyImmediate(actorObject);
        }
    }

    [Test]
    public void MakerHelpDescribesMovingDefenseAndRetiresStandingWindup()
    {
        Assert.That(SkillMakerBlockEditor.GetBlockName(typeof(Action_EnemyWindup)), Does.Contain("미사용"));
        Assert.That(SkillMakerBlockEditor.GetBlockHelp(typeof(Action_EnemyWindup)), Does.Contain("사용 중단"));
        Assert.That(SkillMakerBlockEditor.GetBlockHelp(typeof(Action_DefenseWindow)), Does.Not.Contain("접촉 정지"));
        Assert.That(SkillMakerBlockEditor.GetBlockHelp(typeof(Action_DefenseWindow)), Does.Contain("판정 시간은 보존"));
        Assert.That(SkillMakerBlockEditor.GetBlockHelp(typeof(Action_EnemyAssaultCombo)), Does.Contain("난무"));
        Assert.That(SkillMakerBlockEditor.GetBlockHelp(typeof(Action_PresentationAccent)), Does.Contain("병행"));
    }

    [Test]
    public void RetiredCutInNeverCreatesDisplaySoundOrWait()
    {
        var block = new Action_PresentationAccent { Mode = Action_PresentationAccent.AccentMode.CutIn };
        Assert.That((int)Action_PresentationAccent.AccentMode.Silhouette, Is.EqualTo(1));
        Assert.That((int)Action_PresentationAccent.AccentMode.Flash, Is.EqualTo(2));
        Assert.That(new Action_PresentationAccent().Mode, Is.EqualTo(Action_PresentationAccent.AccentMode.Silhouette));
        Assert.That(block.GetAuthoringTiming().Duration, Is.Zero);
        Assert.That(block.Execute(new SkillContext()).MoveNext(), Is.False);
        Assert.That(BattleSkillAccentPresentation.EvaluateEnvelope(block.Mode, 0.4f), Is.Zero);
    }

    [Test]
    public void InvalidSettingsFailWithoutStartingPresentation()
    {
        Assert.That(new Action_PresentationAccent { Duration = 0f }.HasValidSettings, Is.False);
        Assert.That(new Action_PresentationAccent { Duration = float.NaN }.HasValidSettings, Is.False);
        Assert.That(new Action_PresentationAccent { Strength = float.PositiveInfinity }.HasValidSettings, Is.False);
        Assert.That(new Action_PresentationAccent { SlidePixels = float.NaN }.HasValidSettings, Is.True,
            "Retired cut-in fields no longer influence silhouette or impact flash.");
        Assert.That(new Action_PresentationAccent { SfxVolume = 2f }.HasValidSettings, Is.False);
        Assert.That(new Action_PresentationAccent { Mode = (Action_PresentationAccent.AccentMode)3 }.HasValidSettings, Is.False);
        Assert.That(new Action_PresentationAccent().Execute(null).MoveNext(), Is.False);
        Assert.That(new Action_PresentationAccent().Execute(new SkillContext()).MoveNext(), Is.False);
    }

    [TestCase(Action_PresentationAccent.AccentMode.CutIn)]
    [TestCase(Action_PresentationAccent.AccentMode.Silhouette)]
    [TestCase(Action_PresentationAccent.AccentMode.Flash)]
    public void EnvelopeIsBoundedAndEndsWithoutResidualAlpha(Action_PresentationAccent.AccentMode mode)
    {
        Assert.That(BattleSkillAccentPresentation.EvaluateEnvelope(mode, 1f), Is.Zero.Within(0.00001f));
        Assert.That(BattleSkillAccentPresentation.EvaluateEnvelope(mode, float.NaN), Is.Zero);
        Assert.That(BattleSkillAccentPresentation.EvaluateEnvelope(mode, float.PositiveInfinity), Is.Zero);
        for (int i = -10; i <= 110; i++)
            Assert.That(BattleSkillAccentPresentation.EvaluateEnvelope(mode, i / 100f), Is.InRange(0f, 1f));
    }

    [Test]
    public void WorldSilhouetteCopiesExactLocalPoseAndUnbindsWithoutChangingItsParent()
    {
        var sourceObject = new GameObject("Accent source", typeof(SpriteRenderer));
        var parking = new GameObject("Accent parking");
        var viewObject = new GameObject("Accent clone", typeof(SpriteRenderer), typeof(BattleSkillAccentSpriteView));
        var texture = new Texture2D(64, 64);
        var frameA = Sprite.Create(texture, new Rect(0, 0, 32, 64), new Vector2(0.2f, 0.1f), 32);
        var frameB = Sprite.Create(texture, new Rect(32, 0, 32, 64), new Vector2(0.8f, 0.5f), 64);
        try
        {
            SpriteRenderer source = sourceObject.GetComponent<SpriteRenderer>();
            source.sprite = frameA;
            source.transform.SetPositionAndRotation(new Vector3(2, -3, 1), Quaternion.Euler(0, 0, 20));
            source.transform.localScale = new Vector3(-2, 3, 1);
            source.flipX = true;
            source.flipY = true;
            source.sortingOrder = 55;
            source.color = new Color(0.2f, 0.5f, 0.7f, 0.5f);
            var view = viewObject.GetComponent<BattleSkillAccentSpriteView>();
            view.Bind(source, null);
            view.SetTint(new Color(1, 0, 0, 0.8f));
            view.SyncNow();
            Assert.That(view.transform.parent, Is.SameAs(source.transform));
            Assert.That(view.transform.localPosition, Is.EqualTo(Vector3.zero));
            Assert.That(view.transform.localRotation, Is.EqualTo(Quaternion.identity));
            Assert.That(view.transform.localScale, Is.EqualTo(Vector3.one));
            Assert.That(view.Renderer.flipX && view.Renderer.flipY, Is.True);
            Assert.That(view.Renderer.sprite, Is.SameAs(frameA));
            Assert.That(view.Renderer.sortingOrder, Is.EqualTo(56));
            Assert.That(view.Renderer.color.a, Is.EqualTo(0.4f).Within(0.00001f));
            Vector3 probe = new Vector3(0.23f, 0.87f, 0);
            Assert.That(view.transform.TransformPoint(probe), Is.EqualTo(source.transform.TransformPoint(probe)));
            source.sprite = frameB;
            view.SyncNow();
            Assert.That(view.Renderer.sprite, Is.SameAs(frameB));
            Assert.That(view.Renderer.sprite.pixelsPerUnit, Is.EqualTo(64));
            Assert.That(view.Renderer.sprite.pivot, Is.EqualTo(frameB.pivot));
            source.enabled = false;
            view.SyncNow();
            Assert.That(view.Renderer.enabled, Is.False);
            view.Unbind(parking.transform);
            Assert.That(view.transform.parent, Is.SameAs(source.transform));
            Assert.That(view.gameObject.activeSelf, Is.False);
            Assert.That(view.Renderer.sprite, Is.Null);
            Assert.That(view.Renderer.sharedMaterial, Is.Null);
            view.Bind(source, null);
            Assert.That(view.gameObject.activeSelf, Is.True);
            Assert.That(view.Renderer.sprite, Is.SameAs(frameB));
            view.Unbind(parking.transform);
            Assert.That(source.transform.localScale, Is.EqualTo(new Vector3(-2, 3, 1)));
            Assert.That(source.color, Is.EqualTo(new Color(0.2f, 0.5f, 0.7f, 0.5f)));
            Assert.That(BattleSkillAccentPresentation.CanvasOrder, Is.LessThan(-100));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(viewObject);
            UnityEngine.Object.DestroyImmediate(sourceObject);
            UnityEngine.Object.DestroyImmediate(parking);
            UnityEngine.Object.DestroyImmediate(frameA);
            UnityEngine.Object.DestroyImmediate(frameB);
            UnityEngine.Object.DestroyImmediate(texture);
        }
    }

    [Test]
    public void ActorDisableMayUnbindItsSilhouetteWithoutReparentingDuringActivation()
    {
        var sourceObject = new GameObject("Accent disabling actor", typeof(SpriteRenderer));
        var parking = new GameObject("Accent owner");
        var viewObject = new GameObject("Accent child", typeof(SpriteRenderer), typeof(BattleSkillAccentSpriteView));
        try
        {
            var view = viewObject.GetComponent<BattleSkillAccentSpriteView>();
            view.Bind(sourceObject.GetComponent<SpriteRenderer>(), null);
            var probe = sourceObject.AddComponent<AccentSilhouetteDisableProbe>();
            probe.View = view;
            probe.Parking = parking.transform;
            sourceObject.SetActive(false);
            Assert.That(probe.DidUnbind, Is.True);
            Assert.That(view.transform.parent, Is.SameAs(sourceObject.transform));
            Assert.That(view.gameObject.activeSelf, Is.False);
            Assert.That(view.Renderer.enabled, Is.False);
            LogAssert.NoUnexpectedReceived();
            sourceObject.SetActive(true);
            view.Bind(sourceObject.GetComponent<SpriteRenderer>(), null);
            Assert.That(view.gameObject.activeSelf, Is.True);
            var replacement = parking.AddComponent<SpriteRenderer>();
            view.Bind(replacement, null);
            Assert.That(view.transform.parent, Is.SameAs(replacement.transform));
            Assert.That(sourceObject.GetComponentsInChildren<BattleSkillAccentSpriteView>(true), Is.Empty);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(viewObject);
            UnityEngine.Object.DestroyImmediate(sourceObject);
            UnityEngine.Object.DestroyImmediate(parking);
        }
    }

    [Test]
    public void ImpactReservationIsOneShotAndKillingHitsAreValid()
    {
        var request = new SkillAccentImpactRequest();
        var flash = new Action_PresentationAccent { Mode = Action_PresentationAccent.AccentMode.Flash };
        Assert.That(request.Queue(flash), Is.True);
        Assert.That(request.TryTake(default, out _), Is.False);
        Assert.That(request.TryTake(new DamageResult(10, DamageElement.Physical, 0, true, false, false), out _), Is.False);
        var killingHit = new DamageResult(10, DamageElement.Physical, 10, true, false, true);
        Assert.That(request.TryTake(killingHit, out Action_PresentationAccent taken), Is.True);
        Assert.That(taken, Is.SameAs(flash));
        for (int i = 0; i < 50; i++) Assert.That(request.TryTake(killingHit, out _), Is.False);
        Assert.That(request.Queue(flash), Is.True);
        request.Clear();
        Assert.That(request.TryTake(killingHit, out _), Is.False);
        Assert.That(request.Queue(new Action_PresentationAccent()), Is.False);
        Assert.That(request.Queue(new Action_PresentationAccent { Mode = Action_PresentationAccent.AccentMode.CutIn }), Is.False);
    }

    [Test]
    public void DestroyingPresentationOwnerRemovesDormantActorChildWithoutDestroyingActor()
    {
        var sourceObject = new GameObject("Accent surviving actor", typeof(SpriteRenderer));
        var ownerObject = new GameObject("Accent owner", typeof(BattleSkillAccentPresentation));
        var viewObject = new GameObject("Accent owned view", typeof(SpriteRenderer), typeof(BattleSkillAccentSpriteView));
        try
        {
            var owner = ownerObject.GetComponent<BattleSkillAccentPresentation>();
            var view = viewObject.GetComponent<BattleSkillAccentSpriteView>();
            view.Bind(sourceObject.GetComponent<SpriteRenderer>(), null);
            typeof(BattleSkillAccentPresentation)
                .GetField("_spriteView", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                .SetValue(owner, view);
            UnityEngine.Object.DestroyImmediate(ownerObject);
            Assert.That(viewObject == null, Is.True);
            Assert.That(sourceObject != null, Is.True);
            LogAssert.NoUnexpectedReceived();
        }
        finally
        {
            if (viewObject != null) UnityEngine.Object.DestroyImmediate(viewObject);
            if (ownerObject != null) UnityEngine.Object.DestroyImmediate(ownerObject);
            UnityEngine.Object.DestroyImmediate(sourceObject);
        }
    }

    [TestCase(Action_PresentationAccent.AccentMode.Silhouette)]
    [TestCase(Action_PresentationAccent.AccentMode.Flash)]
    public void EveryModeHonorsStrengthAndColorAlpha(Action_PresentationAccent.AccentMode mode)
    {
        float full = BattleSkillAccentPresentation.EvaluateOpacity(mode, 0.4f, 1f, 1f);
        Assert.That(full, Is.GreaterThan(0f));
        Assert.That(BattleSkillAccentPresentation.EvaluateOpacity(mode, 0.4f, 0f, 1f), Is.Zero);
        Assert.That(BattleSkillAccentPresentation.EvaluateOpacity(mode, 0.4f, 1f, 0f), Is.Zero);
        Assert.That(BattleSkillAccentPresentation.EvaluateOpacity(mode, 0.4f, 0.5f, 0.5f), Is.EqualTo(full * 0.25f).Within(0.00001f));
        Assert.That(BattleSkillAccentPresentation.EvaluateOpacity(mode, 0.4f, float.NaN, 1f), Is.Zero);
    }

    [Test]
    public void AuthoringValidationRejectsAccentInsideDefenseImpactGap()
    {
        var skill = ScriptableObject.CreateInstance<SkillData>();
        try
        {
            skill.UsageProfile = SkillUsageProfile.EnemyOnly;
            skill.ActionTimeline.Add(new Action_DefenseWindow());
            skill.ActionTimeline.Add(new Action_PresentationAccent { Duration = float.NaN });
            skill.ActionTimeline.Add(new Action_Damage());
            EnemyAttackAuthoringReport report = EnemyAttackAuthoringAnalyzer.Analyze(skill);
            bool invalidSettings = false, invalidGap = false;
            for (int i = 0; i < report.Issues.Count; i++)
            {
                invalidSettings |= report.Issues[i].Code == "skill.accent.settings.invalid";
                invalidGap |= report.Issues[i].Code == "skill.accent.defense_impact_gap";
            }
            Assert.That(invalidSettings, Is.True);
            Assert.That(invalidGap, Is.True);
        }
        finally { UnityEngine.Object.DestroyImmediate(skill); }
    }

    [Test]
    public void ShaderUsesWorldSpriteInstanceFlipAndSingleSampleAlphaMask()
    {
        string source = File.ReadAllText(Path.Combine(Application.dataPath, "_Game/Resources/HubToHome/SkillAccentSilhouette.shader"));
        Assert.That(source, Does.Contain("UniversalPipeline").And.Contain("SetUpSpriteInstanceProperties").And.Contain("UnityFlipSprite"));
        Assert.That(source, Does.Not.Contain("UnityUI.cginc"));
        Assert.That(source.Split(new[] { "SAMPLE_TEXTURE2D(" }, StringSplitOptions.None).Length - 1, Is.EqualTo(1));
    }
}

public sealed class AccentSilhouetteDisableProbe : MonoBehaviour
{
    public BattleSkillAccentSpriteView View;
    public Transform Parking;
    public bool DidUnbind;

    private void OnDisable()
    {
        if (View == null) return;
        View.Unbind(Parking);
        DidUnbind = true;
    }
}
#endif
