#if UNITY_EDITOR
using System.IO;
using NUnit.Framework;
using UnityEngine;

public sealed class BattlePhaseOverlayTests
{
    [Test]
    public void CornersRemainOutsideCommandAndQueueBands()
    {
        Rect area = BattlePhaseOverlayPolicy.Playfield;
        Assert.That(area.yMin, Is.EqualTo(170));
        Assert.That(area.yMax, Is.EqualTo(424));
        for (int i = 0; i < 4; i++)
        {
            Vector2 center = BattlePhaseOverlayPolicy.CornerCenter(i, 18);
            Assert.That(Mathf.Abs(center.x), Is.EqualTo(302));
            Assert.That(Mathf.Abs(center.y), Is.EqualTo(109));
            Assert.That(Mathf.Abs(BattlePhaseOverlayPolicy.CornerAngle(i)), Is.EqualTo(45));
        }
        Assert.That(BattlePhaseOverlayPresentation.CanvasOrder, Is.LessThan(-100));
    }

    [Test]
    public void ScreenCornersCrossAdjacentScreenEdgesInsteadOfPointingIntoTheArena()
    {
        Rect area = BattlePhaseOverlayPolicy.AreaFor(BattlePhaseOverlayPlacement.ScreenCorners);
        Assert.That(area, Is.EqualTo(new Rect(0, 0, 640, 480)));
        float[] angles = { 45, -45, 45, -45 };
        for (int i = 0; i < 4; i++)
        {
            Vector2 center = BattlePhaseOverlayPolicy.CornerCenter(i, 64, area.size);
            Assert.That(Mathf.Abs(center.x), Is.EqualTo(256));
            Assert.That(Mathf.Abs(center.y), Is.EqualTo(176));
            float angle = BattlePhaseOverlayPolicy.CornerAngle(i, BattlePhaseOverlayPlacement.ScreenCorners);
            Assert.That(angle, Is.EqualTo(angles[i]));
            Vector2 axis = Quaternion.Euler(0, 0, angle) * Vector3.up;
            Vector2 first = center - axis * 128, last = center + axis * 128;
            Assert.That(Mathf.Max(Mathf.Abs(first.x), Mathf.Abs(last.x)), Is.GreaterThan(320));
            Assert.That(Mathf.Max(Mathf.Abs(first.y), Mathf.Abs(last.y)), Is.GreaterThan(240));
        }
        Assert.That(BattlePhaseOverlayPolicy.AreaFor(BattlePhaseOverlayPlacement.PlayfieldCorners),
            Is.EqualTo(BattlePhaseOverlayPolicy.Playfield), "기존 프로필의 전투 영역 배치는 보존합니다.");
    }

    [Test]
    public void PlaybackPausesAndAudioStartsOnlyOnceWhenReady()
    {
        var playback = new BattlePhaseOverlayPlayback();
        uint token = playback.Begin(24, 96, .18f, 1.2f);
        Assert.That(playback.Owns(token), Is.True);
        Assert.That(playback.Tick(.5f, true, true), Is.EqualTo(BattlePhaseOverlayAudioCommand.None));
        Assert.That(playback.ScrollPixels, Is.Zero);
        Assert.That(playback.Tick(.5f, false, false), Is.EqualTo(BattlePhaseOverlayAudioCommand.None));
        Assert.That(playback.ScrollPixels, Is.EqualTo(12));
        Assert.That(playback.Tick(0, false, true), Is.EqualTo(BattlePhaseOverlayAudioCommand.Start));
        Assert.That(playback.Volume, Is.EqualTo(.18f).Within(.00001));
        Assert.That(playback.Tick(.6f, false, true), Is.EqualTo(BattlePhaseOverlayAudioCommand.None));
        Assert.That(playback.Volume, Is.LessThan(.18f).And.GreaterThan(0));
        float pausedVolume = playback.Volume;
        playback.Tick(10, true, true);
        Assert.That(playback.Volume, Is.EqualTo(pausedVolume));
        Assert.That(playback.Tick(.6f, false, true), Is.EqualTo(BattlePhaseOverlayAudioCommand.Stop));
        Assert.That(playback.Tick(10, false, true), Is.EqualTo(BattlePhaseOverlayAudioCommand.None));
        Assert.That(playback.Volume, Is.Zero);
        playback.Hide();
        Assert.That(playback.Owns(token), Is.False);
    }

    [Test]
    public void NewShowInvalidatesOldGenerationAndResetsState()
    {
        var playback = new BattlePhaseOverlayPlayback();
        uint previous = playback.Begin(24, 96, .18f, 1.2f);
        playback.Tick(2, false, true);
        uint current = playback.Begin(24, 96, .18f, 1.2f);
        Assert.That(playback.Owns(previous), Is.False);
        Assert.That(playback.Owns(current), Is.True);
        Assert.That(playback.ScrollPixels, Is.Zero);
        Assert.That(playback.Tick(0, false, true), Is.EqualTo(BattlePhaseOverlayAudioCommand.Start));
        playback.Hide();
        Assert.That(playback.IsActive, Is.False);
        Assert.That(playback.Tick(1, false, true), Is.EqualTo(BattlePhaseOverlayAudioCommand.None));
        Assert.That(playback.Begin(float.NaN, 96, .18f, 1.2f), Is.Zero);
        Assert.That(playback.IsActive, Is.False);
    }

    [Test]
    public void ScrollIsBoundedForBothDirectionsAndInvalidDelta()
    {
        for (int i = -1000; i <= 1000; i++)
        {
            float value = BattlePhaseOverlayPolicy.AdvanceScroll(20, i / 10f, 24, 96, false);
            Assert.That(value, Is.InRange(0f, 95.99999f));
        }
        Assert.That(BattlePhaseOverlayPolicy.AdvanceScroll(20, 1, -24, 96, false), Is.EqualTo(92));
        Assert.That(BattlePhaseOverlayPolicy.AdvanceScroll(20, float.NaN, 24, 96, false), Is.EqualTo(20));
        Assert.That(BattlePhaseOverlayPolicy.AdvanceScroll(20, 100, 24, 96, true), Is.EqualTo(20));
    }

    [Test]
    public void DoubledArtworkUsesDoubledRepeatPeriodAndIndependentMirrors()
    {
        Assert.That(BattlePhaseOverlayPolicy.RepeatPeriod(96, 1), Is.EqualTo(96));
        Assert.That(BattlePhaseOverlayPolicy.RepeatPeriod(96, 2), Is.EqualTo(192));
        Assert.That(256 / BattlePhaseOverlayPolicy.RepeatPeriod(96, 2), Is.EqualTo(128 / 96f));
        Assert.That(BattlePhaseOverlayPolicy.RepeatPeriod(float.NaN, 2), Is.Zero);
        Assert.That(BattlePhaseOverlayPolicy.RepeatPeriod(96, 0), Is.Zero);
        var profile = ScriptableObject.CreateInstance<BattlePhaseOverlayProfile>();
        try
        {
            Assert.That(profile.SpriteScale, Is.EqualTo(1));
            for (int i = 0; i < 4; i++) Assert.That(profile.GetCornerMirror(i), Is.EqualTo(BattlePhaseOverlayMirror.None));
            profile.TopLeftMirror = BattlePhaseOverlayMirror.Vertical;
            profile.TopRightMirror = BattlePhaseOverlayMirror.Both;
            profile.BottomRightMirror = BattlePhaseOverlayMirror.Horizontal;
            Vector3[] expected = { Vector3.one, new Vector3(1, -1, 1), new Vector3(-1, -1, 1), new Vector3(-1, 1, 1) };
            for (int i = 0; i < 4; i++)
                Assert.That(BattlePhaseOverlayPolicy.CornerScale(profile.GetCornerMirror(i)), Is.EqualTo(expected[i]));
        }
        finally { Object.DestroyImmediate(profile); }
    }

    [TestCase(0f)]
    [TestCase(.45f)]
    [TestCase(1f)]
    public void StartVolumeAcceptsTheFullSourceRangeAndFadesWithoutClampingToPointThree(float start)
    {
        var playback = new BattlePhaseOverlayPlayback();
        Assert.That(playback.Begin(24, 192, start, 1.2f), Is.Not.Zero);
        Assert.That(playback.Tick(0, false, true), Is.EqualTo(BattlePhaseOverlayAudioCommand.Start));
        Assert.That(playback.Volume, Is.EqualTo(start));
        playback.Tick(.6f, false, true);
        Assert.That(playback.Volume, Is.EqualTo(start * .25f).Within(.00001));
        Assert.That(playback.Tick(.6f, false, true), Is.EqualTo(BattlePhaseOverlayAudioCommand.Stop));
        Assert.That(playback.Volume, Is.Zero);
    }

    [Test]
    public void ProfileDefaultsNeverUseALabFlagAndLabAssetOwnsItsReferences()
    {
        var profile = ScriptableObject.CreateInstance<BattlePhaseOverlayProfile>();
        try
        {
            Assert.That(profile.ActivationFlag, Is.Empty);
            Assert.That(profile.HasValidSettings, Is.False);
            Assert.That(profile.StartVolume, Is.EqualTo(.18f));
            Assert.That(profile.SoundFadeSeconds, Is.EqualTo(1.2f));
            Assert.That(profile.Placement, Is.EqualTo(BattlePhaseOverlayPlacement.PlayfieldCorners));
            Assert.That(profile.CanvasSortingOrder, Is.EqualTo(-200));
        }
        finally { Object.DestroyImmediate(profile); }
        string path = Path.Combine(Application.dataPath, "_Game/Content/Maps/Development/BunnySlimeBattleLab/Presentation/ZEV_ChainPhase.asset");
        string source = File.ReadAllText(path);
        Assert.That(source, Does.Contain("lab.zev.chain_phase").And.Contain("2f2c17a813ab2254d88a8d7de8817800"));
        Assert.That(source, Does.Contain("fc412acdfe175f5498ddc99d984e011b"));
        Assert.That(source, Does.Contain("SpriteScale: 2").And.Contain("WidthPixels: 32").And.Contain("LengthPixels: 256"));
        Assert.That(source, Does.Contain("StartVolume: 0.45").And.Contain("SoundFadeSeconds: 1.2"));
        Assert.That(source, Does.Contain("Placement: 1").And.Contain("CanvasSortingOrder: -50").And.Contain("InsetPixels: 64"));
        Assert.That(source, Does.Contain("BottomLeftMirror: 0").And.Contain("TopLeftMirror: 2")
            .And.Contain("TopRightMirror: 3").And.Contain("BottomRightMirror: 1"));
    }

    [Test]
    public void ShaderIsSingleSampleAndPresenterDoesNotDuckBgmOrPollWhenIdle()
    {
        string shader = File.ReadAllText(Path.Combine(Application.dataPath, "_Game/Resources/HubToHome/BattlePhaseOverlay.shader"));
        Assert.That(shader, Does.Contain("frac(").And.Contain("UNITY_UI_CLIP_RECT"));
        Assert.That(shader.Split(new[] { "tex2D(" }, System.StringSplitOptions.None).Length - 1, Is.EqualTo(1));
        string presenter = File.ReadAllText(Path.Combine(Application.dataPath, "_Game/Scripts/UI/Runtime/BattlePhaseOverlayPresentation.cs"));
        Assert.That(presenter, Does.Not.Contain("DuckBGM").And.Not.Contain("PrimaryBgmSource"));
        Assert.That(presenter, Does.Not.Contain("void Update(").And.Not.Contain("void LateUpdate("));
    }
}
#endif
