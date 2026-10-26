using System.Collections.Generic;
using System.Reflection;
using DG.Tweening;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public sealed class ElementalStatusPresentationTests
{
    private readonly List<Object> _owned = new List<Object>();

    [TearDown]
    public void TearDown()
    {
        for (int i = _owned.Count - 1; i >= 0; i--)
            if (_owned[i] != null) Object.DestroyImmediate(_owned[i]);
        _owned.Clear();
    }

    [TestCase(ElementalStatusKind.Burn, "화상", "화", "FF9856")]
    [TestCase(ElementalStatusKind.Chill, "냉기", "냉", "72D5FF")]
    [TestCase(ElementalStatusKind.Electric, "감전", "전", "FFE566")]
    [TestCase(ElementalStatusKind.Bleed, "출혈", "혈", "FF6A85")]
    [TestCase(ElementalStatusKind.Poison, "중독", "독", "A6DE65")]
    public void KeywordsHaveKoreanLabelsAndSharedColors(ElementalStatusKind kind, string full, string shortLabel, string hex)
    {
        Assert.That(ElementalStatusPresentation.GetLabel(kind), Is.EqualTo(full));
        Assert.That(ElementalStatusPresentation.GetShortLabel(kind), Is.EqualTo(shortLabel));
        Assert.That(ElementalStatusPresentation.FormatKeyword(kind), Is.EqualTo($"<color=#{hex}>{full}</color>"));
        Assert.That(ColorUtility.ToHtmlStringRGB(ElementalStatusPresentation.GetColor(kind)), Is.EqualTo(hex));
    }

    [Test]
    public void FrozenHasFirstPriorityAndCompactReadingNeverTicksState()
    {
        EnemyCharacter actor = Enemy();
        actor.Elements.Add(ElementalStatusKind.Burn, 5);
        actor.Elements.Add(ElementalStatusKind.Poison, 9);
        actor.Elements.Add(ElementalStatusKind.Chill, 10);
        int hp = actor.CurrentHP;
        Assert.That(ElementalStatusPresentation.TryGetEntry(actor.Elements, 0, out _, out int value, out bool frozen), Is.True);
        Assert.That(frozen, Is.True);
        Assert.That(value, Is.EqualTo(1));
        Assert.That(ElementalStatusPresentation.GetEntryCount(actor.Elements), Is.EqualTo(3));
        for (int i = 0; i < 10; i++)
        {
            string text = ElementalStatusPresentation.FormatCompact(actor.Elements);
            Assert.That(text, Does.Contain("화5").And.Contain("독9").And.Contain("빙1"));
        }
        Assert.That(actor.Elements.IsFrozen, Is.True);
        Assert.That(actor.Elements.GetValue(ElementalStatusKind.Poison), Is.EqualTo(9));
        Assert.That(actor.CurrentHP, Is.EqualTo(hp));
    }

    [Test]
    public void PotencyDetailsDoNotExpandCompactHudOrChangeState()
    {
        EnemyCharacter actor = Enemy();
        actor.Elements.Add(ElementalStatusKind.Burn, 25);
        actor.Elements.Add(ElementalStatusKind.Chill, 9);
        int hp = actor.CurrentHP;
        string details = ElementalStatusPresentation.FormatDetails(actor);
        Assert.That(details, Does.Contain("화상 25").And.Contain("1.5"));
        string compact = ElementalStatusPresentation.FormatCompact(actor.Elements);
        Assert.That(compact, Does.Contain("화25").And.Contain("냉9"));
        Assert.That(compact, Does.Not.Contain("1.5").And.Not.Contain("강도"));
        Assert.That(actor.CurrentHP, Is.EqualTo(hp));
        Assert.That(actor.Elements.GetValue(ElementalStatusKind.Chill), Is.EqualTo(9));
    }

    [Test]
    public void PotencyTransitionEmphasizesOnlyThresholdRisesAndNeverInitialBindingOrDecay()
    {
        EnemyCharacter actor = Enemy();
        var tracker = new BattleStatusTransitionTracker();
        actor.Elements.Add(ElementalStatusKind.Burn, 9);
        tracker.Capture(actor.Elements);
        Assert.That(tracker.HasPotencyIncreased, Is.False);
        actor.Elements.Add(ElementalStatusKind.Burn, 1);
        tracker.Capture(actor.Elements);
        Assert.That(tracker.IsPotencyIncreased(ElementalStatusKind.Burn), Is.True);
        Assert.That(tracker.HasPotencyIncreased, Is.True);
        Assert.That(tracker.HasAdded || tracker.HasRemoved, Is.False);
        tracker.Capture(actor.Elements);
        Assert.That(tracker.HasPotencyIncreased, Is.False);
        actor.Elements.Add(ElementalStatusKind.Burn, 1);
        tracker.Capture(actor.Elements);
        Assert.That(tracker.HasPotencyIncreased, Is.False);
        actor.Elements.Consume(ElementalStatusKind.Burn, 2);
        tracker.Capture(actor.Elements);
        Assert.That(tracker.HasPotencyIncreased, Is.False);
        tracker.Reset();
        actor.Elements.Add(ElementalStatusKind.Burn, 50);
        tracker.Capture(actor.Elements);
        Assert.That(tracker.HasPotencyIncreased, Is.False);
    }

    [Test]
    public void CompactStripReusesExistingObjectsAndRetainsLegacySlot()
    {
        EnemyCharacter actor = Enemy();
        actor.Elements.Add(ElementalStatusKind.Burn, 5);
        actor.Elements.Add(ElementalStatusKind.Electric, 30);
        actor.TryApplyStatusEffect(new BurnEffect(3));
        GameObject root = Own(new GameObject("Status strip", typeof(RectTransform), typeof(BattleStatusIconStrip)));
        BattleStatusIconStrip strip = root.GetComponent<BattleStatusIconStrip>();
        var images = new Image[3];
        var labels = new TMP_Text[3];
        for (int i = 0; i < 3; i++)
        {
            var imageObject = new GameObject($"Icon{i}", typeof(RectTransform), typeof(Image));
            imageObject.transform.SetParent(root.transform, false);
            images[i] = imageObject.GetComponent<Image>();
            images[i].rectTransform.sizeDelta = new Vector2(10, 10);
            var labelObject = new GameObject($"Label{i}", typeof(RectTransform), typeof(TextMeshProUGUI));
            labelObject.transform.SetParent(imageObject.transform, false);
            labels[i] = labelObject.GetComponent<TMP_Text>();
        }
        var overflowObject = new GameObject("Overflow", typeof(RectTransform), typeof(TextMeshProUGUI));
        overflowObject.transform.SetParent(root.transform, false);
        TMP_Text overflow = overflowObject.GetComponent<TMP_Text>();
        Set(strip, "_icons", images);
        Set(strip, "_fallbackLabels", labels);
        Set(strip, "_overflow", overflow);
        var definitions = new[] { new BattleStatusIconDefinition { EffectId = StatusEffectIds.Burn, ShortLabel = "B", Color = Color.yellow } };
        int childCount = root.GetComponentsInChildren<Transform>(true).Length;
        for (int i = 0; i < 10; i++) strip.RefreshActor(actor, definitions);
        Assert.That(labels[0].text, Is.EqualTo("화5"));
        Assert.That(labels[1].text, Is.EqualTo("B"));
        Assert.That(images[2].gameObject.activeSelf, Is.False);
        Assert.That(overflow.text, Is.EqualTo("+1"));
        Assert.That(root.GetComponentsInChildren<Transform>(true).Length, Is.EqualTo(childCount));
        actor.Elements.Cleanse();
        strip.RefreshActor(actor, definitions);
        Assert.That(labels[0].text, Is.EqualTo("B"));
        Assert.That(images[0].rectTransform.sizeDelta.x, Is.EqualTo(10f));
    }

    [Test]
    public void StatusStrip_EmptyBindingCanBeReplacedBeforePartySlotRefresh()
    {
        GameObject root = Own(new GameObject("Late-bound strip", typeof(RectTransform), typeof(BattleStatusIconStrip)));
        BattleStatusIconStrip strip = root.GetComponent<BattleStatusIconStrip>();
        var slot = new PartySlotUI { StatusIcons = strip };
        Set(strip, "_icons", new Image[0]);
        slot.SetStatusDefinitions(null);

        var images = new Image[3];
        for (int i = 0; i < images.Length; i++)
            images[i] = LayoutIcon(root.transform, i * 12f, 10f + i);
        Set(strip, "_icons", images);
        Assert.DoesNotThrow(() => slot.SetStatusDefinitions(null));
        Assert.That(images[2].rectTransform.anchoredPosition.x, Is.EqualTo(24f));
        Assert.That(images[2].rectTransform.sizeDelta.x, Is.EqualTo(12f));
    }

    [Test]
    public void StatusStrip_GrowReorderAndReplacePreserveEachGraphicsBaseline()
    {
        EnemyCharacter actor = Enemy();
        actor.Elements.Add(ElementalStatusKind.Burn, 4);
        GameObject root = Own(new GameObject("Rebound strip", typeof(RectTransform), typeof(BattleStatusIconStrip)));
        BattleStatusIconStrip strip = root.GetComponent<BattleStatusIconStrip>();
        Image first = LayoutIcon(root.transform, 2f, 10f);
        Image second = LayoutIcon(root.transform, 14f, 12f);
        Set(strip, "_icons", new[] { first, second });
        strip.RefreshActor(actor, null);
        Image third = LayoutIcon(root.transform, 28f, 14f);
        var rebound = new[] { second, first, third };
        Set(strip, "_icons", rebound);
        strip.RefreshActor(null, null);
        Assert.That(first.rectTransform.anchoredPosition.x, Is.EqualTo(2f));
        Assert.That(second.rectTransform.sizeDelta.x, Is.EqualTo(12f));
        Assert.That(third.rectTransform.sizeDelta.x, Is.EqualTo(14f));

        rebound[1] = LayoutIcon(root.transform, 42f, 16f);
        for (int i = 0; i < 5; i++)
        {
            strip.RefreshActor(actor, null);
            strip.RefreshActor(null, null);
        }
        Assert.That(rebound[1].rectTransform.anchoredPosition.x, Is.EqualTo(42f));
        Assert.That(rebound[1].rectTransform.sizeDelta.x, Is.EqualTo(16f));
    }

    [Test]
    public void StatusStrip_LabelOnlyAndLateOverflowRestoreIndependentSizes()
    {
        EnemyCharacter actor = Enemy();
        actor.Elements.Add(ElementalStatusKind.Burn, 4);
        GameObject root = Own(new GameObject("Partial strip", typeof(RectTransform), typeof(BattleStatusIconStrip)));
        BattleStatusIconStrip strip = root.GetComponent<BattleStatusIconStrip>();
        Set(strip, "_icons", new Image[] { null });
        strip.RefreshActor(null, null);
        var labelRoot = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        labelRoot.transform.SetParent(root.transform, false);
        TMP_Text label = labelRoot.GetComponent<TMP_Text>();
        label.rectTransform.sizeDelta = new Vector2(7f, 9f);
        Set(strip, "_fallbackLabels", new[] { label });
        var overflowRoot = new GameObject("Overflow", typeof(RectTransform), typeof(TextMeshProUGUI));
        overflowRoot.transform.SetParent(root.transform, false);
        TMP_Text overflow = overflowRoot.GetComponent<TMP_Text>();
        overflow.rectTransform.anchoredPosition = new Vector2(52f, 2f);
        overflow.rectTransform.sizeDelta = new Vector2(12f, 9f);
        Set(strip, "_overflow", overflow);
        strip.RefreshActor(actor, null);
        strip.RefreshActor(null, null);
        Assert.That(label.rectTransform.sizeDelta, Is.EqualTo(new Vector2(7f, 9f)));
        Assert.That(overflow.rectTransform.anchoredPosition, Is.EqualTo(new Vector2(52f, 2f)));
        Assert.That(overflow.rectTransform.sizeDelta, Is.EqualTo(new Vector2(12f, 9f)));
        Object.DestroyImmediate(labelRoot);
        Set(strip, "_icons", null);
        Assert.DoesNotThrow(() => strip.RefreshActor(null, null));
    }

    private Image LayoutIcon(Transform parent, float x, float width)
    {
        var root = new GameObject("Icon", typeof(RectTransform), typeof(Image));
        root.transform.SetParent(parent, false);
        Image image = root.GetComponent<Image>();
        image.rectTransform.anchoredPosition = new Vector2(x, 3f);
        image.rectTransform.sizeDelta = new Vector2(width, 11f);
        return image;
    }

    [Test]
    public void TargetPreviewUsesExistingDescriptionAndRestoresSkillText()
    {
        EnemyCharacter actor = Enemy();
        actor.Elements.Add(ElementalStatusKind.Bleed, 7);
        GameObject root = Own(new GameObject("SubMenu", typeof(RectTransform)));
        root.SetActive(false);
        BattleSubMenu menu = root.AddComponent<BattleSubMenu>();
        var textObject = new GameObject("Description", typeof(RectTransform), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(root.transform, false);
        TMP_Text label = textObject.GetComponent<TMP_Text>();
        Set(menu, "_descriptionText", label);
        menu.Preview("ATTACK", new List<IMenuEntry> { new BattleCommandPreviewEntry("공격", "원래 설명") }, null);
        menu.SetStatusPreview(actor);
        Assert.That(label.text, Does.Contain("출혈 7"));
        menu.SetStatusPreview(null);
        Assert.That(label.text, Is.EqualTo("원래 설명"));
    }

    [Test]
    public void DataProfilesAreBoundWithoutSharingActorValues()
    {
        ElementalStatusProfile profile = Own(ScriptableObject.CreateInstance<ElementalStatusProfile>());
        CharacterData playerData = Own(ScriptableObject.CreateInstance<CharacterData>());
        playerData.StatusProfile = profile;
        GameObject playerObject = Own(new GameObject("Profile player"));
        playerObject.SetActive(false);
        PlayerCharacter player = playerObject.AddComponent<PlayerCharacter>();
        player.SetCharacterData(playerData);
        EnemyCharacter enemy = Enemy();
        enemy.Data.StatusProfile = profile;
        enemy.Setup(enemy.Data);
        player.Elements.Add(ElementalStatusKind.Burn, 4);
        Assert.That(player.Elements.Profile, Is.SameAs(profile));
        Assert.That(enemy.Elements.Profile, Is.SameAs(profile));
        Assert.That(enemy.Elements.GetValue(ElementalStatusKind.Burn), Is.Zero);
        player.SetCharacterData(playerData);
        Assert.That(player.Elements.GetValue(ElementalStatusKind.Burn), Is.EqualTo(4));
    }

    [Test]
    public void SpriteTintIsIndependentFromUiColorAndRestoredWhenHurtFlashIsCancelled()
    {
        CharacterData data = Own(ScriptableObject.CreateInstance<CharacterData>());
        data.BattleSymbolColor = Color.red;
        Assert.That(data.BattleSpriteTint, Is.EqualTo(Color.white));
        data.BattleSpriteTint = new Color(0.3f, 0.8f, 0.7f, 1f);
        GameObject root = Own(new GameObject("Tint actor", typeof(SpriteRenderer)));
        root.SetActive(false);
        PlayerCharacter player = root.AddComponent<PlayerCharacter>();
        SpriteRenderer sprite = root.GetComponent<SpriteRenderer>();
        Set(player, "_spriteRenderer", sprite);
        player.SetCharacterData(data);
        Assert.That(sprite.color, Is.EqualTo(data.BattleSpriteTint));
        player.TakeDamage(2, DamageElement.Physical);
        sprite.DOKill(false);
        root.transform.DOKill(false);
        Assert.That(sprite.color, Is.EqualTo(data.BattleSpriteTint));
    }

    [Test]
    public void EnemyStatusIconsMatchAuthoredSpriteSheetAndSharedHost()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/_Game/Content/Battle/Prefabs/System/SeamlessBattleHost.prefab");
        BattleUIController ui = prefab.GetComponentInChildren<BattleUIController>(true);
        var definitions = (ElementalStatusIconDefinition[])typeof(BattleUIController)
            .GetField("_enemyStatusIcons", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(ui);
        Assert.That(definitions.Length, Is.EqualTo(5));
        string[] names = { "Icon_16", "Icon_2", "Icon_3", "Icon_1", "Icon_19" };
        var kinds = new HashSet<ElementalStatusKind>();
        foreach (ElementalStatusIconDefinition definition in definitions)
        {
            Assert.That(kinds.Add(definition.Kind), Is.True);
            Assert.That(definition.Icon, Is.Not.Null);
            Assert.That(definition.Icon.name, Is.EqualTo(names[(int)definition.Kind]));
            Assert.That(AssetDatabase.GetAssetPath(definition.Icon),
                Is.EqualTo("Assets/_Game/Content/Art/Icons/Icon.png"));
        }
    }

    [Test]
    public void EnemyReadoutUpdatesFromEventsAndReusesCellsThroughCleanseAndRebind()
    {
        EnemyCharacter first = Enemy();
        EnemyCharacter second = Enemy();
        GameObject root = Own(new GameObject("Enemy readout", typeof(RectTransform), typeof(Canvas)));
        var presenter = root.AddComponent<BattleEnemyStatusPresenter>();
        var host = root.GetComponent<RectTransform>();
        presenter.Bind(host, null, null, new[] { first }, null);
        Transform row = host.Find("Enemy elemental status");
        int objectCount = root.GetComponentsInChildren<Transform>(true).Length;
        first.Elements.Add(ElementalStatusKind.Burn, 5);
        first.Elements.Add(ElementalStatusKind.Chill, 2);
        first.Elements.Add(ElementalStatusKind.Electric, 999);
        first.Elements.Add(ElementalStatusKind.Bleed, 4);
        first.Elements.Add(ElementalStatusKind.Poison, 9);
        string[] expected = { "5", "2", "999", "4", "9" };
        for (int i = 0; i < expected.Length; i++)
        {
            Assert.That(row.GetChild(i).gameObject.activeSelf, Is.True);
            Assert.That(row.GetChild(i).Find("Value").GetComponent<TMP_Text>().text, Is.EqualTo(expected[i]));
        }
        Assert.That(((RectTransform)row.GetChild(3)).anchoredPosition.y, Is.LessThan(0f));
        first.Elements.Add(ElementalStatusKind.Chill, 8);
        Assert.That(row.GetChild(0).Find("Value").GetComponent<TMP_Text>().text, Is.EqualTo("빙"));
        first.Elements.Cleanse();
        Assert.That(row.gameObject.activeSelf, Is.False);
        foreach (Transform cell in row) Assert.That(cell.gameObject.activeSelf, Is.False);

        presenter.Bind(host, null, null, new[] { second }, null);
        second.Elements.Add(ElementalStatusKind.Poison, 7);
        first.Elements.Add(ElementalStatusKind.Burn, 20); // 옛 대상의 이벤트가 새 표시를 바꾸면 안 됩니다.
        Assert.That(row.GetChild(0).Find("Value").GetComponent<TMP_Text>().text, Is.EqualTo("7"));
        Assert.That(root.GetComponentsInChildren<Transform>(true).Length, Is.EqualTo(objectCount));
        presenter.Clear();
        second.Elements.Add(ElementalStatusKind.Poison, 1);
        Assert.That(row.gameObject.activeSelf, Is.False);
        Assert.That(row.GetChild(0).Find("Value").GetComponent<TMP_Text>().text, Is.EqualTo("7"));
    }

    [Test]
    public void EnemyReadoutsFollowSeparateBottomPivotsAndKeepPixelSizeAcrossZoom()
    {
        EnemyCharacter first = Enemy();
        EnemyCharacter second = Enemy();
        first.gameObject.SetActive(true);
        second.gameObject.SetActive(true);
        first.transform.position = new Vector3(-2f, 0f, 0f);
        second.transform.position = new Vector3(2f, 0f, 0f);
        first.Elements.Add(ElementalStatusKind.Burn, 3);
        second.Elements.Add(ElementalStatusKind.Poison, 9);
        GameObject root = Own(new GameObject("Enemy tracking", typeof(RectTransform), typeof(Canvas)));
        root.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
        var presenter = root.AddComponent<BattleEnemyStatusPresenter>();
        var cameraObject = Own(new GameObject("Tracking camera", typeof(Camera)));
        Camera camera = cameraObject.GetComponent<Camera>();
        camera.orthographic = true;
        camera.orthographicSize = 8f;
        camera.transform.position = new Vector3(0f, 0f, -10f);
        var host = root.GetComponent<RectTransform>();
        presenter.Bind(host, camera, null, new[] { first, second }, null);
        RectTransform rightRow = (RectTransform)host.GetChild(0);
        RectTransform leftRow = (RectTransform)host.GetChild(1);
        Assert.That(leftRow.gameObject.activeSelf, Is.True);
        Assert.That(rightRow.anchoredPosition.x, Is.GreaterThan(leftRow.anchoredPosition.x));
        Vector2 cellSize = ((RectTransform)leftRow.GetChild(0)).sizeDelta;
        float previousGap = rightRow.anchoredPosition.x - leftRow.anchoredPosition.x;
        camera.orthographicSize = 4f;
        typeof(BattleEnemyStatusPresenter).GetMethod("LateUpdate", BindingFlags.Instance | BindingFlags.NonPublic)
            .Invoke(presenter, null);
        Assert.That(rightRow.anchoredPosition.x - leftRow.anchoredPosition.x, Is.GreaterThan(previousGap));
        Assert.That(((RectTransform)leftRow.GetChild(0)).sizeDelta, Is.EqualTo(cellSize));
        Object.DestroyImmediate(first.gameObject);
        Assert.DoesNotThrow(() => typeof(BattleEnemyStatusPresenter)
            .GetMethod("LateUpdate", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(presenter, null));
        Assert.That(leftRow.gameObject.activeSelf, Is.False);
    }

    private EnemyCharacter Enemy()
    {
        GameObject root = Own(new GameObject("Element display actor"));
        root.SetActive(false);
        EnemyCharacter actor = root.AddComponent<EnemyCharacter>();
        EnemyData data = Own(ScriptableObject.CreateInstance<EnemyData>());
        data.EnemyName = "검증 대상";
        actor.Setup(data);
        return actor;
    }

    private T Own<T>(T value) where T : Object { _owned.Add(value); return value; }
    private static void Set(object target, string name, object value) =>
        target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
}
