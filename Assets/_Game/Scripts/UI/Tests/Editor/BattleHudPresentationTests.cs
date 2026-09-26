using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public sealed class BattleHudPresentationTests
{
    private const string HostPath = "Assets/_Game/Content/Battle/Prefabs/System/SeamlessBattleHost.prefab";
    private readonly List<Object> _owned = new List<Object>();

    [Test]
    public void SharedBattleMenu_UsesCustomNavigationAudio()
    {
        GameObject host = AssetDatabase.LoadAssetAtPath<GameObject>(HostPath);
        var menu = new SerializedObject(host.GetComponentInChildren<BattleMenuUI>(true));
        Assert.That(AssetDatabase.GetAssetPath(menu.FindProperty("_moveSfx").objectReferenceValue),
            Is.EqualTo("Assets/_Game/Content/Audio/CUSTOM/SFX/ButtonMove.wav"));
        Assert.That(AssetDatabase.GetAssetPath(menu.FindProperty("_confirmSfx").objectReferenceValue),
            Is.EqualTo("Assets/_Game/Content/Audio/CUSTOM/SFX/ButtonSelect.wav"));
        Assert.That(AssetDatabase.GetAssetPath(menu.FindProperty("_cancelSfx").objectReferenceValue),
            Is.EqualTo("Assets/_Game/Content/Audio/CUSTOM/SFX/Cancle.wav"));
    }

    [TearDown]
    public void TearDown()
    {
        for (int i = _owned.Count - 1; i >= 0; i--)
            if (_owned[i] != null) Object.DestroyImmediate(_owned[i]);
        _owned.Clear();
    }

    [Test]
    public void DisplayRoster_PutsWizelFirst_WithoutReorderingTargetSource()
    {
        GameObject root = Own(new GameObject("HUD", typeof(RectTransform)));
        root.SetActive(false);
        BattleUIController ui = root.AddComponent<BattleUIController>();
        PlayerCharacter crow = Player("crow");
        PlayerCharacter wizel = Player("player_001");
        PlayerCharacter wolf = Player("wolf");
        var party = new List<PlayerCharacter> { crow, wizel, wolf };
        Invoke(ui, "BindPartySlots", party);
        var display = Get<List<PlayerCharacter>>(ui, "_displayParty");
        CollectionAssert.AreEqual(new[] { wizel, crow, wolf }, display);
        CollectionAssert.AreEqual(new[] { crow, wizel, wolf }, party);
        Invoke(ui, "HandlePlayerTurnStarted", wolf);
        CollectionAssert.AreEqual(new[] { wizel, crow, wolf }, display);
    }

    [Test]
    public void ActorHighlight_AndTargetHighlight_AreIndependent()
    {
        GameObject root = Own(new GameObject("Row", typeof(RectTransform)));
        var slot = new PartySlotUI
        {
            Root = root, RowBackground = Image(root, "Background"), TargetBorder = Image(root, "Target"),
            Portrait = Image(root, "Portrait")
        };
        slot.SetHighlight(true);
        Color acting = slot.RowBackground.color;
        slot.SetTargeted(true);
        Assert.That(slot.RowBackground.color, Is.EqualTo(acting));
        Assert.That(slot.Portrait.color, Is.EqualTo(Color.white));
        slot.SetTargeted(false);
        Assert.That(slot.RowBackground.color, Is.EqualTo(acting));
        Assert.That(slot.TargetBorder.enabled, Is.False);
        Assert.That(root.transform.localScale, Is.EqualTo(Vector3.one));
    }

    [Test]
    public void PersistentMenu_RemainsVisible_ButCannotReceiveEnemyCommands()
    {
        GameObject root = Own(new GameObject("Menu", typeof(RectTransform), typeof(CanvasGroup)));
        root.SetActive(false);
        BattleMenuUI menu = root.AddComponent<BattleMenuUI>();
        menu.SetCommandInputEnabled(false);
        Assert.That(menu.IsVisible, Is.True);
        Assert.That(menu.CommandsEnabled, Is.False);
        Assert.That(root.GetComponent<CanvasGroup>().blocksRaycasts, Is.False);
        menu.SetCommandInputEnabled(true);
        Assert.That(menu.CommandsEnabled, Is.True);
        menu.SetCommandInputEnabled(false);
        Assert.That(menu.CommandsEnabled, Is.False);
    }

    [Test]
    public void SolidBars_UseExactColorAndResourceRatio()
    {
        GameObject root = Own(new GameObject("Row", typeof(RectTransform)));
        var slot = new PartySlotUI { Root = root, HPFill = Image(root, "HP"), APFill = Image(root, "AP"), SolidColorBars = true };
        slot.RefreshHP(25, 100, 0f, DG.Tweening.Ease.Linear);
        slot.RefreshAP(0, 50, 0f, DG.Tweening.Ease.Linear);
        Assert.That(slot.HPFill.rectTransform.localScale.x, Is.EqualTo(0.25f));
        Assert.That(slot.APFill.rectTransform.localScale.x, Is.Zero);
    }

    [Test]
    public void TurnIcon_HighlightsBorder_NotPortrait()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/_Game/Presentation/UI/Prefabs/Battle/TurnPfp_Prefab.prefab");
        GameObject root = Own(Object.Instantiate(prefab));
        BattleTurnQueueIcon icon = root.GetComponent<BattleTurnQueueIcon>();
        icon.Bind(null, "WIZEL", true);
        Image border = Get<Image>(icon, "_border");
        Image portrait = Get<Image>(icon, "_portrait");
        Color first = border.color;
        Assert.That(portrait.color, Is.EqualTo(Color.white));
        icon.Bind(null, "CROW", false);
        Assert.That(border.color, Is.Not.EqualTo(first));
        Assert.That(portrait.color, Is.EqualTo(Color.white));
    }

    [Test]
    public void SharedHost_HasConnectedPortraitStatusAndHintReferences()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(HostPath);
        BattleUIController ui = prefab.GetComponentInChildren<BattleUIController>(true);
        Assert.That(Get<Image>(ui, "_largePortrait"), Is.Not.Null);
        Assert.That(Get<BattleInputHintView>(ui, "_inputHints"), Is.Not.Null);
        PartySlotUI[] slots = Get<PartySlotUI[]>(ui, "_partySlots");
        Assert.That(slots.Length, Is.EqualTo(3));
        foreach (PartySlotUI slot in slots)
        {
            Assert.That(slot.RowBackground, Is.Not.Null);
            Assert.That(slot.TargetBorder, Is.Not.Null);
            Assert.That(slot.IdentityStrip, Is.Not.Null);
            Assert.That(slot.StatusIcons, Is.Not.Null);
            Assert.That(slot.SolidColorBars, Is.True);
            Assert.That(slot.HPFill.sprite, Is.Null, "원본 텍스처 색이 대표색을 틴트하지 않아야 합니다.");
            Assert.That(slot.APFill.sprite, Is.Null);
        }
        BattleSubMenu sub = prefab.GetComponentInChildren<BattleSubMenu>(true);
        RectTransform viewport = Get<RectTransform>(sub, "_viewport");
        RectTransform content = Get<RectTransform>(sub, "_container");
        Assert.That(viewport, Is.Not.Null);
        Assert.That(content.GetComponent<GridLayoutGroup>().constraintCount, Is.EqualTo(1));
        foreach (LayoutGroup layout in sub.GetComponents<LayoutGroup>())
            Assert.That(layout.enabled, Is.False, "고정 분할 영역을 자동 레이아웃이 덮으면 안 됩니다.");
    }

    [Test]
    public void LastListRow_IsFullyInsideViewport_AfterScrolling()
    {
        GameObject root = Own(new GameObject("Details", typeof(RectTransform)));
        root.SetActive(false);
        BattleSubMenu menu = root.AddComponent<BattleSubMenu>();
        var viewportObject = new GameObject("Viewport", typeof(RectTransform));
        viewportObject.transform.SetParent(root.transform);
        RectTransform viewport = viewportObject.GetComponent<RectTransform>();
        viewport.sizeDelta = new Vector2(177f, 92f);
        var contentObject = new GameObject("Content", typeof(RectTransform), typeof(GridLayoutGroup));
        contentObject.transform.SetParent(viewport, false);
        RectTransform content = contentObject.GetComponent<RectTransform>();
        GridLayoutGroup grid = contentObject.GetComponent<GridLayoutGroup>();
        grid.cellSize = new Vector2(177f, 22f);
        grid.spacing = new Vector2(0f, 1f);
        Set(menu, "_viewport", viewport);
        Set(menu, "_container", content);
        Set(menu, "_grid", grid);
        Set(menu, "_rowPrefab", AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/_Game/Presentation/UI/Prefabs/Battle/OptionRow.prefab").GetComponent<OptionRowUI>());
        var entries = new List<IMenuEntry>();
        for (int i = 0; i < 11; i++) entries.Add(new BattleCommandPreviewEntry("Item " + i, ""));
        menu.Preview("ITEM", entries, null);
        for (int i = 0; i < 10; i++) Invoke(menu, "ChangeIndex", 1);
        Assert.That(content.anchoredPosition.y, Is.EqualTo(content.rect.height - viewport.rect.height).Within(0.01f));
    }

    [Test]
    public void LeftRightAndUpDown_SelectSkillWithoutEnteringSubMenu()
    {
        GameObject root = Own(new GameObject("Menu", typeof(RectTransform), typeof(CanvasGroup)));
        root.SetActive(false);
        BattleMenuUI menu = root.AddComponent<BattleMenuUI>();
        foreach (string field in new[] { "_attackBtn", "_actBtn", "_itemBtn", "_runBtn" })
        {
            GameObject button = new GameObject(field, typeof(RectTransform), typeof(Image), typeof(Button));
            button.transform.SetParent(root.transform, false);
            Set(menu, field, button.GetComponent<Button>());
        }
        GameObject details = Own(new GameObject("Details", typeof(RectTransform)));
        details.SetActive(false);
        BattleSubMenu sub = details.AddComponent<BattleSubMenu>();
        Set(menu, "_subMenu", sub);
        PlayerCharacter actor = Player("Wizzel_ally");
        SkillData first = Own(ScriptableObject.CreateInstance<SkillData>());
        SkillData second = Own(ScriptableObject.CreateInstance<SkillData>());
        first.APCost = second.APCost = 0;
        actor.Skills = new List<SkillData> { first, second };
        menu.SetActor(actor);
        menu.SetCommandInputEnabled(true);

        Invoke(menu, "Navigate", 1);
        Invoke(menu, "MoveItemSelection", 1);
        Assert.That(sub.TryGetSelectedEntry(out IMenuEntry selected), Is.True);
        Assert.That(((SkillMenuEntry)selected).Data, Is.SameAs(second));
        Assert.That(menu.CommandsEnabled, Is.True);
        Assert.That(sub.IsActive, Is.False, "별도 하위 입력 단계로 진입하지 않습니다.");

        // 메뉴 전환 후에도 각 목록의 선택 위치를 기억합니다.
        Invoke(menu, "Navigate", -1);
        Invoke(menu, "Navigate", 1);
        Assert.That(sub.SelectedIndex, Is.EqualTo(1));
        menu.SetCommandInputEnabled(false);
        Assert.That(menu.CommandsEnabled, Is.False);
    }

    [Test]
    public void LeadDbReference_SurvivesCharacterIdChange()
    {
        GameObject root = Own(new GameObject("HUD", typeof(RectTransform)));
        root.SetActive(false);
        BattleUIController ui = root.AddComponent<BattleUIController>();
        PlayerCharacter other = Player("other");
        PlayerCharacter wizel = Player("Wizzel_ally");
        Set(ui, "_leadCharacterData", wizel.CharacterData);
        wizel.CharacterData.CharacterID = "renamed.id";
        var party = new List<PlayerCharacter> { other, wizel };
        Invoke(ui, "BindPartySlots", party);
        Assert.That(Get<List<PlayerCharacter>>(ui, "_displayParty")[0], Is.SameAs(wizel));
        Assert.That(party[0], Is.SameAs(other));
    }

    [Test]
    public void RunAndOtherTabs_UseTheSameFixedCell()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(HostPath);
        BattleMenuUI menu = prefab.GetComponentInChildren<BattleMenuUI>(true);
        Button run = Get<Button>(menu, "_runBtn");
        GridLayoutGroup grid = run.transform.parent.GetComponent<GridLayoutGroup>();
        Assert.That(grid.enabled, Is.True);
        Assert.That(grid.cellSize, Is.EqualTo(new Vector2(86f, 22f)));
        Assert.That(grid.constraintCount, Is.EqualTo(4));
        foreach (Button button in menu.GetComponentsInChildren<Button>(true))
        {
            Assert.That(((RectTransform)button.transform).sizeDelta, Is.EqualTo(grid.cellSize));
            TMP_Text label = button.GetComponentInChildren<TMP_Text>(true);
            Assert.That(label.enableAutoSizing, Is.False);
            Assert.That(label.fontSize, Is.EqualTo(14f));
        }
    }

    [Test]
    public void WizelPortraits_AreReadFromTheBoundCharacterDb()
    {
        CharacterData data = AssetDatabase.LoadAssetAtPath<CharacterData>(
            "Assets/_Game/Content/Characters/AllyDB/WizzelDB.asset");
        Assert.That(data, Is.Not.Null);
        PlayerCharacter player = Player("portrait-test");
        Set(player, "_characterData", data);
        Assert.That(player.BattlePortrait, Is.SameAs(data.Portrait));
        Assert.That(player.TurnOrderPortrait, Is.SameAs(data.TurnOrderPortrait));
        GameObject root = Own(new GameObject("HUD", typeof(RectTransform)));
        root.SetActive(false);
        BattleUIController ui = root.AddComponent<BattleUIController>();
        Image portrait = Image(root, "LargePortrait");
        Set(ui, "_largePortrait", portrait);
        Invoke(ui, "SetPortraitActor", player);
        Assert.That(portrait.sprite, Is.SameAs(data.BattleLargePortrait));
    }

    private PlayerCharacter Player(string id)
    {
        GameObject go = Own(new GameObject(id));
        go.SetActive(false);
        PlayerCharacter player = go.AddComponent<PlayerCharacter>();
        CharacterData data = Own(ScriptableObject.CreateInstance<CharacterData>());
        data.CharacterID = id;
        data.DisplayName = id;
        Set(player, "_characterData", data);
        return player;
    }

    private T Own<T>(T obj) where T : Object { _owned.Add(obj); return obj; }

    private static Image Image(GameObject parent, string name)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent.transform, false);
        return go.GetComponent<Image>();
    }

    private static FieldInfo Field(object obj, string name) => obj.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
    private static T Get<T>(object obj, string name) => (T)Field(obj, name).GetValue(obj);
    private static void Set(object obj, string name, object value) => Field(obj, name).SetValue(obj, value);
    private static void Invoke(object obj, string name, params object[] args) =>
        obj.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(obj, args);
}
