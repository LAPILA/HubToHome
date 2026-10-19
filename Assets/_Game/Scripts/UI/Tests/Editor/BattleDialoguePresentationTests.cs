using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public sealed class BattleDialoguePresentationTests
{
    private readonly List<Object> _owned = new List<Object>();
    private BattleMenuUI _menu;
    private BattleSubMenu _details;
    private RectTransform _root;
    private BattleDialogueLayout _layout;

    [SetUp]
    public void SetUp()
    {
        _root = Rect("Battle", null);
        _owned.Add(_root.gameObject);
        _root.sizeDelta = new Vector2(640f, 480f);
        RectTransform menuRect = Rect("Menu", _root);
        menuRect.anchorMin = menuRect.anchorMax = Vector2.zero;
        menuRect.pivot = new Vector2(0f, 1f);
        menuRect.anchoredPosition = new Vector2(177f, 154f);
        menuRect.sizeDelta = new Vector2(362f, 22f);
        menuRect.gameObject.SetActive(false);
        _menu = menuRect.gameObject.AddComponent<BattleMenuUI>();
        RectTransform detailsRect = Rect("Details", _root);
        detailsRect.anchorMin = detailsRect.anchorMax = Vector2.zero;
        detailsRect.pivot = new Vector2(0f, 1f);
        detailsRect.anchoredPosition = new Vector2(177f, 128f);
        detailsRect.sizeDelta = new Vector2(362f, 104f);
        _details = detailsRect.gameObject.AddComponent<BattleSubMenu>();
        Set(_menu, "_subMenu", _details);
        _menu.SetCommandInputEnabled(true);
    }

    [TearDown]
    public void TearDown()
    {
        _layout?.Dispose();
        _layout = null;
        if (_menu != null) _menu.EndDialoguePresentation();
        for (int i = _owned.Count - 1; i >= 0; i--)
            if (_owned[i] != null) Object.DestroyImmediate(_owned[i]);
        _owned.Clear();
    }

    [Test]
    public void DialogueSlide_DefaultDurationIsPointThreeSeconds()
        => Assert.That(_menu.DialogueTransitionDuration, Is.EqualTo(.3f));

    [Test]
    public void Dialogue_BorrowsVisibilityWithoutLosingSelectionOrCallbacks()
    {
        var entries = new List<IMenuEntry> { new EmptyMenuEntry("A", "a"), new EmptyMenuEntry("B", "b") };
        System.Action<IMenuEntry> callback = _ => { };
        _details.Open("SKILL", entries, callback, null);
        _details.SelectIndex(1);
        Set(_menu, "_selectedIndex", 1);
        CanvasGroup group = _details.gameObject.AddComponent<CanvasGroup>();
        group.alpha = .7f;

        Assert.That(_menu.BeginDialoguePresentation(), Is.True);
        Assert.That(_menu.CommandsEnabled, Is.False);
        Assert.That(_menu.gameObject.activeSelf, Is.True, "대화의 표시 소유자는 비활성화하지 않습니다.");
        Assert.That(_menu.GetComponent<CanvasGroup>().alpha, Is.Zero, "명령 버튼도 상세와 함께 감춥니다.");
        Assert.That(_details.gameObject.activeSelf, Is.True);
        Assert.That(_details.IsActive, Is.True);
        Assert.That(_details.SelectedIndex, Is.EqualTo(1));
        Assert.That(Get<System.Action<IMenuEntry>>(_details, "_onConfirmCallback"), Is.SameAs(callback));
        Assert.That(group.alpha, Is.Zero);
        Assert.That(group.interactable || group.blocksRaycasts, Is.False);

        _menu.EndDialoguePresentation();
        Assert.That(_menu.CommandsEnabled, Is.True);
        Assert.That(_details.SelectedIndex, Is.EqualTo(1));
        Assert.That(Get<int>(_menu, "_selectedIndex"), Is.EqualTo(1));
        Assert.That(Get<System.Action<IMenuEntry>>(_details, "_onConfirmCallback"), Is.SameAs(callback));
        Assert.That(group.alpha, Is.EqualTo(.7f));
        Assert.That(group.interactable && group.blocksRaycasts, Is.True);
        Assert.That(Get<int>(_menu, "_inputEnabledFrame"), Is.EqualTo(Time.frameCount));
    }

    [Test]
    public void NewEnemyTurn_StaysLockedAfterDialogueCloses()
    {
        _menu.BeginDialoguePresentation();
        _menu.SetCommandInputEnabled(false);
        _menu.EndDialoguePresentation();
        Assert.That(_menu.CommandsEnabled, Is.False);
        Assert.That(_menu.IsVisible, Is.True);
    }

    [Test]
    public void ExternalHide_IsNotUndoneByDialogueCleanup()
    {
        _menu.BeginDialoguePresentation();
        _menu.HideImmediate();
        _menu.EndDialoguePresentation();
        Assert.That(_menu.gameObject.activeSelf, Is.False);
        Assert.That(_details.gameObject.activeSelf, Is.False);
    }

    [Test]
    public void PreviouslyHiddenCinematicMenu_IsRestoredToHidden()
    {
        _menu.HideImmediate();
        Assert.That(_menu.BeginDialoguePresentation(), Is.True);
        Assert.That(_menu.gameObject.activeSelf, Is.True);
        Assert.That(_menu.SubMenuRoot.gameObject.activeSelf, Is.True);
        // 시나리오가 미리 숨긴 메뉴도 전환 출발점에서는 실제로 보이는 상태여야 합니다.
        Get<BattleHudEntryMotion>(_menu, "_dialogueMotion").SetProgress(1f);
        Assert.That(_menu.GetComponent<CanvasGroup>().alpha, Is.EqualTo(1f));
        Assert.That(_menu.SubMenuRoot.GetComponent<CanvasGroup>().alpha, Is.EqualTo(1f));
        _menu.EndDialoguePresentation();
        Assert.That(_menu.gameObject.activeSelf, Is.False);
        Assert.That(_menu.SubMenuRoot.gameObject.activeSelf, Is.False);
        Assert.That(Get<bool>(_menu, "_resumeAfterHiddenDialogue"), Is.True);
        _menu.SetDetailsVisible(true);
        _menu.SetCommandInputEnabled(false); // 시나리오 gate 종료 시 기존 전투 상태로 복귀
        Assert.That(Get<bool>(_menu, "_resumeAfterHiddenDialogue"), Is.False);
        Assert.That(_menu.IsVisible, Is.True);
        Assert.That(_menu.CommandsEnabled, Is.False);
        Assert.That(((RectTransform)_menu.transform).anchoredPosition.y, Is.EqualTo(154f));
    }

    [Test]
    public void RepeatedBeginAndEnd_DoNotReplaceTheOriginalSnapshot()
    {
        Assert.That(_menu.BeginDialoguePresentation(), Is.True);
        Assert.That(_menu.BeginDialoguePresentation(), Is.False);
        _menu.EndDialoguePresentation();
        _menu.EndDialoguePresentation();
        Assert.That(_menu.CommandsEnabled, Is.True);
        Assert.That(_details.GetComponent<CanvasGroup>().alpha, Is.EqualTo(1f));
    }

    [Test]
    public void Layout_MatchesRestingCommandAndDetailsUnion_AndRestoresTheOverworldPanel()
    {
        RectTransform panel = Rect("Dialogue", _root);
        panel.anchorMin = new Vector2(0f, 0f);
        panel.anchorMax = new Vector2(1f, 0f);
        panel.anchoredPosition = new Vector2(0f, 72f);
        panel.sizeDelta = new Vector2(-64f, 144f);
        Vector2 position = panel.anchoredPosition, size = panel.sizeDelta;
        RectTransform background = Rect("Blue background", panel);
        TextMeshProUGUI body = Text("Body", background);
        TextMeshProUGUI speaker = Text("Speaker", background);
        body.fontSize = 22f;
        body.overflowMode = TextOverflowModes.Overflow;
        Image portrait = Rect("Portrait", background).gameObject.AddComponent<Image>();
        portrait.preserveAspect = false;
        RectTransform choices = Rect("Choices", background);
        var group = choices.gameObject.AddComponent<VerticalLayoutGroup>();
        choices.gameObject.SetActive(false);
        _menu.BeginDialoguePresentation();
        _layout = new BattleDialogueLayout(_menu, panel, portrait, speaker, body, choices, null);
        _layout.Refresh(null, 0);

        var expected = new Vector3[4];
        var actual = new Vector3[4];
        Assert.That(_menu.TryGetDialogueWorldCorners(expected), Is.True);
        panel.GetWorldCorners(actual);
        for (int i = 0; i < 4; i++) Assert.That(Vector3.Distance(expected[i], actual[i]), Is.LessThan(.01f));
        Assert.That(panel.rect.width, Is.EqualTo(362f).Within(.01f));
        Assert.That(panel.rect.height, Is.EqualTo(130f).Within(.01f));
        Assert.That(body.fontSize, Is.EqualTo(14f));
        Assert.That(body.overflowMode, Is.EqualTo(TextOverflowModes.Page));
        Assert.That(group.enabled, Is.False);
        Assert.That(portrait.preserveAspect, Is.True);

        _layout.Dispose();
        _layout.Dispose();
        Assert.That(panel.anchoredPosition, Is.EqualTo(position));
        Assert.That(panel.sizeDelta, Is.EqualTo(size));
        Assert.That(panel.anchorMin, Is.EqualTo(Vector2.zero));
        Assert.That(panel.anchorMax, Is.EqualTo(new Vector2(1f, 0f)));
        Assert.That(body.fontSize, Is.EqualTo(22f));
        Assert.That(body.overflowMode, Is.EqualTo(TextOverflowModes.Overflow));
        Assert.That(group.enabled, Is.True);
        Assert.That(portrait.preserveAspect, Is.False);
        Assert.That(_menu.IsDialoguePresentationActive, Is.False);
    }

    [Test]
    public void CentralMenus_MoveTogether_ButTheDialogueDockDoesNotMove()
    {
        RectTransform commands = (RectTransform)_menu.transform;
        RectTransform details = _menu.SubMenuRoot;
        Vector2 commandHome = commands.anchoredPosition, detailHome = details.anchoredPosition;
        var expected = new Vector3[4];
        _menu.BeginDialoguePresentation();
        Assert.That(commands.anchoredPosition.y, Is.EqualTo(commandHome.y - 164f));
        Assert.That(details.anchoredPosition.y, Is.EqualTo(detailHome.y - 164f));
        Assert.That(_menu.TryGetDialogueWorldCorners(expected), Is.True);
        Assert.That(_root.InverseTransformPoint(expected[0]).y, Is.EqualTo(24f - 240f).Within(.01f));
        Assert.That(_root.InverseTransformPoint(expected[2]).y, Is.EqualTo(154f - 240f).Within(.01f));

        // 같은 대화 중 메뉴 이동/화면 배율 변경에 도킹 사각형은 영향받지 않습니다.
        commands.anchoredPosition += Vector2.down * 20f;
        details.anchoredPosition += Vector2.down * 20f;
        _root.localScale = Vector3.one * 1.5f;
        var resized = new Vector3[4];
        Assert.That(_menu.TryGetDialogueWorldCorners(resized), Is.True);
        Assert.That(_root.InverseTransformPoint(resized[2]).y, Is.EqualTo(154f - 240f).Within(.01f));
        _menu.EndDialoguePresentation();
        Assert.That(commands.anchoredPosition, Is.EqualTo(commandHome));
        Assert.That(details.anchoredPosition, Is.EqualTo(detailHome));
    }

    [Test]
    public void ReopenAndExternalDisable_RestoreBothMenuCoordinates()
    {
        Vector2 commandHome = ((RectTransform)_menu.transform).anchoredPosition;
        Vector2 detailHome = _menu.SubMenuRoot.anchoredPosition;
        for (int i = 0; i < 3; i++)
        {
            Assert.That(_menu.BeginDialoguePresentation(), Is.True);
            _menu.EndDialoguePresentation();
        }
        _menu.BeginDialoguePresentation();
        _menu.HideImmediate();
        _menu.EndDialoguePresentation();
        Assert.That(((RectTransform)_menu.transform).anchoredPosition, Is.EqualTo(commandHome));
        Assert.That(_menu.SubMenuRoot.anchoredPosition, Is.EqualTo(detailHome));
        Assert.That(_menu.IsDialoguePresentationActive, Is.False);
        Assert.That(_menu.gameObject.activeSelf, Is.False);
    }

    [Test]
    public void TurnUpdateDuringDialogue_DoesNotExposeMenuOrMoveTheDock()
    {
        var before = new Vector3[4];
        var after = new Vector3[4];
        _menu.BeginDialoguePresentation();
        _menu.TryGetDialogueWorldCorners(before);
        _menu.SetCommandInputEnabled(false);
        _menu.TryGetDialogueWorldCorners(after);
        Assert.That(_menu.GetComponent<CanvasGroup>().alpha, Is.Zero);
        Assert.That(_menu.IsDialoguePresentationActive, Is.True);
        Assert.That(after, Is.EqualTo(before));
        _menu.EndDialoguePresentation();
        Assert.That(_menu.IsVisible, Is.True);
        Assert.That(_menu.CommandsEnabled, Is.False);
        Assert.That(((RectTransform)_menu.transform).anchoredPosition.y, Is.EqualTo(154f));
    }

    [Test]
    public void CinematicDialogue_RevealsSideHudWithoutChangingCinematicMode()
    {
        GameObject owner = new GameObject("Controller", typeof(RectTransform));
        owner.SetActive(false);
        _owned.Add(owner);
        BattleUIController controller = owner.AddComponent<BattleUIController>();
        RectTransform party = Rect("Party", _root);
        RectTransform decoration = Rect("Decoration", _root);
        RectTransform portrait = Rect("Resource", decoration);
        RectTransform queue = Rect("Queue", _root);
        Set(controller, "_battleMenuUI", _menu);
        Set(controller, "_partyStatusPanel", party);
        Set(controller, "_hudDecoration", decoration.gameObject);
        Set(controller, "_turnQueueContainer", queue);
        owner.SetActive(true);
        controller.SetScenarioCinematicMode(true);
        Assert.That(party.gameObject.activeSelf, Is.False);
        Assert.That(decoration.gameObject.activeSelf, Is.False);
        _menu.BeginDialoguePresentation();
        Assert.That(party.gameObject.activeSelf, Is.True);
        Assert.That(portrait.gameObject.activeInHierarchy, Is.True);
        Assert.That(queue.gameObject.activeSelf, Is.True);
        Assert.That(Get<bool>(controller, "_isScenarioCinematicMode"), Is.True);
        _menu.EndDialoguePresentation();
        Assert.That(party.gameObject.activeSelf, Is.False);
        Assert.That(decoration.gameObject.activeSelf, Is.False);
        Assert.That(_menu.gameObject.activeSelf, Is.False);
    }

    [Test]
    public void NestedHudCanvas_UsesRootViewportAndPreservesAnchoredGeometry()
    {
        RectTransform root = Rect("Canvas", null);
        _owned.Add(root.gameObject);
        Canvas canvas = root.gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        root.sizeDelta = new Vector2(1280f, 960f);
        RectTransform child = Rect("Nested HUD", root);
        child.anchorMin = child.anchorMax = Vector2.zero;
        child.anchoredPosition = new Vector2(14f, 20f);
        child.sizeDelta = new Vector2(640f, 480f);
        Canvas nested = child.gameObject.AddComponent<Canvas>();
        BattleHudViewport.Ensure(nested, null);
        BattleHudViewport viewport = root.GetComponent<BattleHudViewport>();
        Assert.That(viewport, Is.Not.Null);
        Assert.That(child.parent, Is.SameAs(viewport.ContentRect));
        Assert.That(child.GetComponent<BattleHudViewport>(), Is.Null);
        Assert.That(child.anchoredPosition, Is.EqualTo(new Vector2(14f, 20f)));
        Assert.That(child.sizeDelta, Is.EqualTo(new Vector2(640f, 480f)));
    }

    [TestCase(640f, 500f, 10f, 30f)]
    [TestCase(960f, 768f, 144f, 164f)]
    [TestCase(1920f, 1080f, 60f, 100f)]
    public void BottomHudGeometry_CoversOnlyOutputGapAndPreservesPartyRows(
        float width, float height, float gap, float dialogueLimit)
    {
        var output = new UnityEngine.Rect(0f, 0f, width, height);
        float scale = BattleHudViewport.ReferenceScale(output);
        UnityEngine.Rect content = BattleHudViewport.CalculateContentScreenRect(output);
        UnityEngine.Rect matte = BattleHudViewport.CalculateBottomMatteRect(output);
        Assert.That(content.size, Is.EqualTo(new Vector2(640f, 480f) * scale));
        Assert.That(content.yMin, Is.EqualTo(gap).Within(.001f));
        Assert.That(matte, Is.EqualTo(new UnityEngine.Rect(0f, 0f, width, gap)));
        float limit = BattleHudViewport.CalculateDockedDialogueBottomLimit(output);
        Assert.That(limit, Is.EqualTo(dialogueLimit).Within(.001f));
        Assert.That(limit, Is.GreaterThan(content.yMin + 18f * scale), "하단 조작 안내까지 가립니다.");
        Assert.That(limit, Is.LessThan(content.yMin + 24f * scale), "파티 세 번째 행과 대사는 보존합니다.");
    }

    [TestCase(640f, 500f, 0f, 0f, 0f)]
    [TestCase(640f, 500f, .1f, 50f, 30f)]
    [TestCase(960f, 768f, 0f, 0f, 0f)]
    [TestCase(960f, 768f, .1f, 76.8f, 76.8f)]
    [TestCase(1920f, 1080f, 0f, 0f, 0f)]
    [TestCase(1920f, 1080f, .1f, 108f, 100f)]
    public void Letterbox_OnlyDockedBottomIsLimited_AndZeroStaysHidden(
        float width, float height, float thickness, float top, float bottom)
    {
        float limit = BattleHudViewport.CalculateDockedDialogueBottomLimit(
            new UnityEngine.Rect(0f, 0f, width, height));
        Vector2 docked = CinematicLetterboxOverlay.CalculateBarHeights(height, thickness, limit);
        Assert.That(docked.x, Is.EqualTo(top).Within(.001f), "상단 연출은 그대로 유지합니다.");
        Assert.That(docked.y, Is.EqualTo(bottom).Within(.001f));
        Vector2 full = CinematicLetterboxOverlay.CalculateBarHeights(height, thickness, null);
        Assert.That(full.x, Is.EqualTo(top).Within(.001f));
        Assert.That(full.y, Is.EqualTo(top).Within(.001f), "일반 필드/전체 연출은 대칭 두께입니다.");
    }

    [Test]
    public void BottomMatte_UsesOutputOriginRatherThanScreenOrigin()
    {
        var output = new UnityEngine.Rect(40f, 30f, 640f, 500f);
        Assert.That(BattleHudViewport.CalculateBottomMatteRect(output),
            Is.EqualTo(new UnityEngine.Rect(40f, 30f, 640f, 10f)));
        Assert.That(BattleHudViewport.CalculateDockedDialogueBottomLimit(output), Is.EqualTo(60f));
    }

    [Test]
    public void BottomMatte_TracksCanvasAndChromeVisibility_WithoutMovingContent()
    {
        BattleHudViewport viewport = CreateHudViewport(out Canvas canvas, out Camera camera, out GameObject decoration);
        Vector2 menuPosition = ((RectTransform)_menu.transform).anchoredPosition;
        Image matte = Get<Image>(viewport, "_bottomMatte");
        Assert.That(matte.rectTransform.parent, Is.SameAs(canvas.transform));
        Assert.That(matte.raycastTarget, Is.False);
        Assert.That(matte.enabled, Is.True);
        Assert.That(matte.rectTransform.sizeDelta.x, Is.EqualTo(640f).Within(.001f));
        Assert.That(matte.rectTransform.sizeDelta.y, Is.EqualTo(10f).Within(.001f));
        viewport.Configure(camera);
        Assert.That(((RectTransform)_menu.transform).anchoredPosition, Is.EqualTo(menuPosition));
        Assert.That(viewport.ContentRect.sizeDelta, Is.EqualTo(new Vector2(640f, 480f)));

        _menu.BeginDialoguePresentation();
        Assert.That(BattleHudViewport.TryGetDockedDialogueBottomLimit(out float limit), Is.True);
        Assert.That(limit, Is.EqualTo(BattleHudViewport.CalculateDockedDialogueBottomLimit(
            UIViewportService.GetOutputRect(camera))).Within(.001f));
        canvas.enabled = false;
        Invoke(viewport, "RefreshMatteVisibility");
        Assert.That(matte.enabled, Is.False);
        Assert.That(BattleHudViewport.TryGetDockedDialogueBottomLimit(out _), Is.False);
        canvas.enabled = true;
        decoration.SetActive(false);
        Invoke(viewport, "RefreshMatteVisibility");
        Assert.That(matte.enabled, Is.False);
        Assert.That(BattleHudViewport.TryGetDockedDialogueBottomLimit(out _), Is.False);
        decoration.SetActive(true);
        Invoke(viewport, "RefreshMatteVisibility");
        Assert.That(matte.enabled, Is.True);
        _menu.EndDialoguePresentation();
        Assert.That(BattleHudViewport.TryGetDockedDialogueBottomLimit(out _), Is.False);
        _root.gameObject.SetActive(false);
        Assert.That(BattleHudViewport.TryGetDockedDialogueBottomLimit(out _), Is.False);
    }

    [Test]
    public void Letterbox_ReappliesDockAndOutputChanges_WithoutChangingAuthoredThickness()
    {
        CreateHudViewport(out _, out Camera camera, out _);
        GameObject overlayObject = new GameObject("Letterbox", typeof(RectTransform), typeof(Canvas));
        _owned.Add(overlayObject);
        CinematicLetterboxOverlay overlay = overlayObject.AddComponent<CinematicLetterboxOverlay>();
        overlay.SetThicknessImmediate(.1f);
        RectTransform top = Get<RectTransform>(overlay, "_top");
        RectTransform bottom = Get<RectTransform>(overlay, "_bottom");
        float requestedHeight = Screen.height * .1f;
        Assert.That(top.sizeDelta.y, Is.EqualTo(requestedHeight).Within(.001f));
        Assert.That(bottom.sizeDelta.y, Is.EqualTo(requestedHeight).Within(.001f));

        _menu.BeginDialoguePresentation();
        Invoke(overlay, "LateUpdate");
        float initialLimit = BattleHudViewport.CalculateDockedDialogueBottomLimit(UIViewportService.GetOutputRect(camera));
        Assert.That(bottom.sizeDelta.y, Is.EqualTo(Mathf.Min(requestedHeight, initialLimit)).Within(.001f));
        Assert.That(top.sizeDelta.y, Is.EqualTo(requestedHeight).Within(.001f));
        camera.pixelRect = new UnityEngine.Rect(0f, 0f, 960f, 768f);
        Invoke(overlay, "LateUpdate");
        float resizedLimit = BattleHudViewport.CalculateDockedDialogueBottomLimit(UIViewportService.GetOutputRect(camera));
        Assert.That(bottom.sizeDelta.y, Is.EqualTo(Mathf.Min(requestedHeight, resizedLimit)).Within(.001f));
        Assert.That(overlay.CurrentThickness, Is.EqualTo(.1f));
        _menu.EndDialoguePresentation();
        Invoke(overlay, "LateUpdate");
        Assert.That(bottom.sizeDelta.y, Is.EqualTo(requestedHeight).Within(.001f));
        Assert.That(overlay.CurrentThickness, Is.EqualTo(.1f));
        overlay.SetThicknessImmediate(0f);
        Assert.That(top.sizeDelta.y, Is.Zero);
        Assert.That(bottom.sizeDelta.y, Is.Zero);
        Assert.That(overlay.gameObject.activeSelf, Is.False);
    }

    [Test]
    public void Layout_ExpandsForNoPortrait_AndScrollsToTheLastChoice()
    {
        RectTransform panel = Rect("Dialogue", _root);
        TextMeshProUGUI body = Text("Body", panel);
        Image portrait = Rect("Portrait", panel).gameObject.AddComponent<Image>();
        RectTransform choices = Rect("Choices", panel);
        var labels = new List<TextMeshProUGUI>();
        for (int i = 0; i < 5; i++) labels.Add(Text("Choice " + i, choices));
        var settings = new BattleDialogueLayoutSettings();
        _menu.BeginDialoguePresentation();
        _layout = new BattleDialogueLayout(_menu, panel, portrait, null, body, choices, settings);
        _layout.Refresh(labels, 0);
        float withPortrait = body.rectTransform.rect.width;
        portrait.gameObject.SetActive(false);
        settings.FontSize = 17f;
        _layout.Refresh(labels, 4);

        Assert.That(body.rectTransform.rect.width, Is.GreaterThan(withPortrait));
        Assert.That(body.fontSize, Is.EqualTo(17f));
        Assert.That(labels[0].gameObject.activeSelf, Is.False);
        Assert.That(labels[4].gameObject.activeSelf, Is.True);
        Assert.That(labels.FindAll(label => label.gameObject.activeSelf).Count, Is.InRange(1, 3));
        Assert.That(body.rectTransform.rect.height, Is.GreaterThanOrEqualTo(24f));
    }

    [Test]
    public void LostHost_InvalidatesLayout_AndCleanupIsSafe()
    {
        RectTransform panel = Rect("Dialogue", _root);
        TextMeshProUGUI body = Text("Body", panel);
        _menu.BeginDialoguePresentation();
        _layout = new BattleDialogueLayout(_menu, panel, null, null, body, null, null);
        Object.DestroyImmediate(_menu.gameObject);
        Assert.That(_layout.IsHostAvailable, Is.False);
        Assert.DoesNotThrow(() => _layout.Refresh(null, 0));
        Assert.DoesNotThrow(() => _layout.Dispose());
    }

    [TestCase(false, false, false, false)]
    [TestCase(true, false, false, true)]
    [TestCase(true, true, false, false)]
    [TestCase(true, true, true, true)]
    [TestCase(false, true, true, true)]
    public void ModalPolicy_OnlyDockedDialogueKeepsTheHud(bool blocked, bool docked, bool other, bool hidden)
        => Assert.That(BattleHudViewport.ShouldHideForModal(blocked, docked, other), Is.EqualTo(hidden));

    [Test]
    public void InputHints_OnlyOfferDialogueActions_ThenRestoreDefenseHints()
    {
        BattleInputHintView view = Rect("Hints", _root).gameObject.AddComponent<BattleInputHintView>();
        var slots = new BattleHintSlot[3];
        for (int i = 0; i < slots.Length; i++)
            slots[i] = new BattleHintSlot { Key = Text("Key", view.transform), Label = Text("Action", view.transform) };
        Set(view, "_slots", slots);
        view.SetContext(BattleHintContext.Defense);
        view.SetDialoguePresentation(true);
        Assert.That(slots[0].Label.text, Is.Empty);
        Assert.That(slots[1].Label.text, Is.EqualTo("대사 진행"));
        Assert.That(slots[2].Label.text, Is.EqualTo("대사 넘기기"));
        view.SetDialoguePresentation(true, true);
        Assert.That(slots[0].Label.text, Is.EqualTo("선택지 이동"));
        view.SetDialoguePresentation(false);
        Assert.That(slots[0].Label.text, Is.EqualTo("방어"));
        Assert.That(slots[1].Label.text, Is.EqualTo("회피"));
        Assert.That(slots[2].Label.text, Is.EqualTo("특수 반격"));
    }

    [Test]
    public void SharedDialoguePrefab_HasExistingSpeakerBodyAndChoiceReferences()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Game/Content/Dialogue/Prefabs/DialogueCanvas.prefab");
        DialogueUI panel = System.Array.Find(prefab.GetComponentsInChildren<DialogueUI>(true),
            ui => ui.name == "OverworldPanel");
        Assert.That(panel, Is.Not.Null);
        Assert.That(Get<Image>(panel, "_portraitImage"), Is.Not.Null);
        Assert.That(Get<TextMeshProUGUI>(panel, "_speakerNameText"), Is.Not.Null);
        Assert.That(Get<TextMeshProUGUI>(panel, "_dialogueText"), Is.Not.Null);
        Assert.That(Get<RectTransform>(panel, "_choiceRoot"), Is.Not.Null);
    }

    private static RectTransform Rect(string name, Transform parent)
    {
        var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        return rect;
    }

    private BattleHudViewport CreateHudViewport(out Canvas canvas, out Camera camera, out GameObject decoration)
    {
        _root.gameObject.SetActive(false);
        canvas = _root.gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        BattleUIController controller = _root.gameObject.AddComponent<BattleUIController>();
        decoration = Rect("HudDecoration", _root).gameObject;
        Set(controller, "_hudDecoration", decoration);
        Set(controller, "_battleMenuUI", _menu);
        _root.gameObject.SetActive(true);
        GameObject cameraObject = new GameObject("HUD Output", typeof(Camera));
        _owned.Add(cameraObject);
        camera = cameraObject.GetComponent<Camera>();
        // 테스트 Game View가 작아도 하단 여백이 있는 실제 출력 영역을 만든다.
        float outputScale = Mathf.Min(1f, Mathf.Min(Screen.width / 640f, Screen.height / 500f));
        camera.pixelRect = new UnityEngine.Rect(0f, 0f, 640f * outputScale, 500f * outputScale);
        BattleHudViewport.Ensure(canvas, camera);
        return canvas.GetComponent<BattleHudViewport>();
    }
    private static TextMeshProUGUI Text(string name, Transform parent)
        => Rect(name, parent).gameObject.AddComponent<TextMeshProUGUI>();
    private static void Set(object owner, string field, object value)
        => owner.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(owner, value);
    private static T Get<T>(object owner, string field)
        => (T)owner.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(owner);
    private static void Invoke(object owner, string method)
        => owner.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(owner, null);
}
