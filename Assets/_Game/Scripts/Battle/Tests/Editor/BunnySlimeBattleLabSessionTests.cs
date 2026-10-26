using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public sealed class BunnySlimeBattleLabSessionTests
{
    private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
    private readonly List<Object> _owned = new List<Object>();
    private readonly Dictionary<Type, object> _previousSingletons = new Dictionary<Type, object>();
    private GameContentCatalog _previousCatalog;
    private GameContentCatalog _catalog;
    private GlobalDataManager _global;
    private PlayerCharacter _player;
    private BunnySlimeBattleLabData _data;
    private BunnySlimeBattleLabSession _session;

    [SetUp]
    public void SetUp()
    {
        foreach (Type type in new[] { typeof(GlobalDataManager), typeof(BattleManager), typeof(QTEManager), typeof(CameraController), typeof(InteractionSystem) })
        {
            FieldInfo singleton = type.GetField("<Instance>k__BackingField", BindingFlags.Static | BindingFlags.NonPublic);
            _previousSingletons[type] = singleton.GetValue(null);
            singleton.SetValue(null, null);
        }
        FieldInfo catalogField = typeof(GameContentCatalog).GetField("_instance", BindingFlags.Static | BindingFlags.NonPublic);
        _previousCatalog = (GameContentCatalog)catalogField.GetValue(null);
        _catalog = MakeAsset<GameContentCatalog>();
        catalogField.SetValue(null, _catalog);
        CharacterDatabase.InvalidateCache();
        SkillDatabase.InvalidateCache();
        EquipmentDatabase.InvalidateCache();
        ItemDatabase.InvalidateCache();

        _global = MakeComponent<GlobalDataManager>("Lab Tests Global");
        SetSingleton(typeof(GlobalDataManager), _global);
        var battle = MakeComponent<BattleManager>("Lab Tests Battle");
        var qte = MakeComponent<QTEManager>("Lab Tests QTE");
        _player = MakeComponent<PlayerCharacter>("Lab Tests Player");
        PlayerController playerController = _player.gameObject.AddComponent<PlayerController>();
        _data = MakeAsset<BunnySlimeBattleLabData>();
        _data.Enemy = MakeAsset<EnemyData>();
        _data.Enemy.Prefab = MakeObject("Lab Tests Enemy Prefab");
        _data.Enemy.Prefab.AddComponent<BunnySlimeShowcaseEnemy>().Data = _data.Enemy;
        _data.Party = new CharacterData[1];
        for (int i = 0; i < _data.Party.Length; i++)
        {
            CharacterData data = MakeAsset<CharacterData>();
            data.CharacterID = "lab.test.member." + i;
            data.DisplayName = "Sample " + i;
            data.BaseStats = new StatBlock { MaxHP = 120, MaxAP = 60, ATK = 12, DEF = 4, SPD = 10 };
            _data.Party[i] = data;
            _catalog.Characters.Add(data);
        }
        _data.GuardSkill = _data.DodgeSkill = _data.CounterSkill = _data.WaveSkill = MakeAsset<SkillData>();
        _data.Encounters = new[]
        {
            new BattleLabEncounterEntry { Id = "lab.test", Title = "전투", Enemy = _data.Enemy }
        };
        _session = MakeComponent<BunnySlimeBattleLabSession>("Lab Tests Session");
        _session.Configure(_data, playerController);
        SetField("_global", _global);
        SetField("_battle", battle);
        SetField("_qte", qte);
    }

    [TearDown]
    public void TearDown()
    {
        for (int i = _owned.Count - 1; i >= 0; i--)
            if (_owned[i] != null) Object.DestroyImmediate(_owned[i]);
        _owned.Clear();
        foreach (KeyValuePair<Type, object> entry in _previousSingletons)
            SetSingleton(entry.Key, entry.Value);
        _previousSingletons.Clear();
        typeof(GameContentCatalog).GetField("_instance", BindingFlags.Static | BindingFlags.NonPublic).SetValue(null, _previousCatalog);
        CharacterDatabase.InvalidateCache();
        SkillDatabase.InvalidateCache();
        EquipmentDatabase.InvalidateCache();
        ItemDatabase.InvalidateCache();
    }

    [Test]
    public void Awake_WithExistingGlobal_RefusesAndPreservesItsParty()
    {
        var original = new CharacterSaveData { CharacterDataID = "production.hero", HP = 43 };
        _global.Party.Add(original);
        _global.AddMoney(123);
        Invoke("Awake");
        Assert.That(GetField<bool>("_freshSession"), Is.False);
        Assert.That(GetField<bool>("_ownsSession"), Is.False);
        Invoke("OnDestroy");
        Assert.That(_global.Party[0], Is.SameAs(original));
        Assert.That(_global.Party[0].HP, Is.EqualTo(43));
        Assert.That(_global.Money, Is.EqualTo(123));
    }

    [Test]
    public void Validation_RejectsMissingEnemyAndWrongPartySize()
    {
        EnemyData enemy = _data.Enemy;
        _data.Encounters[0].Enemy = null;
        Assert.That(Validate(), Is.False);
        _data.Encounters[0].Enemy = enemy;
        _data.Party = new CharacterData[2];
        Assert.That(Validate(), Is.False);
        Assert.That(_global.Party, Is.Empty);
    }

    [Test]
    public void Validation_RejectsAnAttackingPrefabWithLeftoverPassiveAi()
    {
        Assert.That(Validate(), Is.True);
        _data.Enemy.Prefab.AddComponent<BunnySlimeCharacter>();
        Assert.That(Validate(), Is.False);
        Assert.That(_global.Party, Is.Empty);
    }

    [Test]
    public void Validation_RejectsMissingOrUnregisteredSoloIdentity()
    {
        Assert.That(Validate(), Is.True);
        _catalog.Characters.RemoveAt(0);
        CharacterDatabase.InvalidateCache();
        Assert.That(Validate(), Is.False);
        _catalog.Characters.Add(_data.Party[0]);
        _data.Party[0].CharacterID = string.Empty;
        CharacterDatabase.InvalidateCache();
        Assert.That(Validate(), Is.False);
    }

    [Test]
    public void Validation_AcceptsStandardEnemyWithoutBunnySpecificAi()
    {
        Object.DestroyImmediate(_data.Enemy.Prefab.GetComponent<BunnySlimeShowcaseEnemy>());
        _data.Enemy.Prefab.AddComponent<EnemyCharacter>();
        Assert.That(Validate(), Is.True);
    }

    [Test]
    public void Validation_RejectsDuplicateEncounterIds()
    {
        _data.Encounters = new[] { _data.Encounters[0], _data.Encounters[0] };
        Assert.That(Validate(), Is.False);
    }

    [Test]
    public void Validation_RejectsTheRetiredReserveExercise()
    {
        _data.Encounters[0].WeakenFrontLine = true;
        Assert.That(Validate(), Is.False);
        Assert.That(_global.Party, Is.Empty);
    }

    [Test]
    public void VisibleEntries_HideWithoutDeletingOrReorderingTheSource()
    {
        BattleLabEncounterEntry bunny = _data.Encounters[0];
        bunny.HiddenInMenu = true;
        var zev = new BattleLabEncounterEntry { Id = "lab.zev.test", Title = "ZEV", Enemy = bunny.Enemy };
        _data.Encounters = new[] { bunny, zev };
        CollectionAssert.AreEqual(new[] { 1 }, _data.GetVisibleEncounterIndices());
        Assert.That(Validate(), Is.True);
        Assert.That(_data.Encounters[0], Is.SameAs(bunny));
        bunny.HiddenInMenu = false;
        CollectionAssert.AreEqual(new[] { 0, 1 }, _data.GetVisibleEncounterIndices());
        _data.Encounters = new[] { bunny, null, zev };
        CollectionAssert.AreEqual(new[] { 0, 2 }, _data.GetVisibleEncounterIndices());
        _data.Encounters = null;
        Assert.That(_data.GetVisibleEncounterIndices(), Is.Empty);
    }

    [Test]
    public void Validation_RejectsAllHiddenEntriesWithoutTouchingParty()
    {
        _data.Encounters[0].HiddenInMenu = true;
        Assert.That(Validate(), Is.False);
        Assert.That(_global.Party, Is.Empty);
    }

    [Test]
    public void Menu_SelectsVisibleZevRatherThanHiddenFirstEntry()
    {
        BattleLabEncounterEntry bunny = _data.Encounters[0];
        bunny.HiddenInMenu = true;
        var zev = new BattleLabEncounterEntry { Id = "lab.zev.test", Title = "ZEV", Enemy = bunny.Enemy, Description = "ZEV sample" };
        _data.Encounters = new[] { bunny, zev };
        Invoke("BuildView");
        Invoke("RefreshSelection");
        Assert.That(GetField<int[]>("_menuEncounterIndices"), Is.EqualTo(new[] { 1 }));
        object[] args = { null };
        Assert.That((bool)typeof(BunnySlimeBattleLabSession).GetMethod("TryGetSelectedEncounter", PrivateInstance).Invoke(_session, args), Is.True);
        Assert.That(args[0], Is.SameAs(zev));
        Assert.That(GetField<TMPro.TMP_Text>("_description").text, Is.EqualTo("ZEV sample"));
        Assert.That(GetField<TMPro.TMP_Text[]>("_choiceTitles"), Has.Length.EqualTo(1));
        Assert.That(GetField<TMPro.TMP_Text[]>("_choiceTitles")[0].text, Does.Contain("ZEV"));
        zev.HiddenInMenu = true;
        Assert.That((bool)typeof(BunnySlimeBattleLabSession).GetMethod("TryGetSelectedEncounter", PrivateInstance).Invoke(_session, args), Is.False);
    }

    [Test]
    public void Preparation_PreservesTheExplicitSoloSkillLoadout()
    {
        SkillData first = MakeAsset<SkillData>();
        SkillData second = MakeAsset<SkillData>();
        first.SkillID = "lab.test.first";
        second.SkillID = "lab.test.second";
        _catalog.Skills.Add(first);
        _catalog.Skills.Add(second);
        SkillDatabase.InvalidateCache();
        _data.Party[0].DefaultSkills = new List<SkillData> { first, second };
        Invoke("PrepareParty");
        Assert.That(_global.Party, Has.Count.EqualTo(1));
        Assert.That(_global.Party[0].EquippedSkillIDs, Is.EqualTo(new[] { first.SkillID, second.SkillID }));
        Assert.That(_player.Skills, Is.EqualTo(new[] { first, second }));
    }

    [Test]
    public void LegacyWaveFlagCannotCreateExtraMembersOrWeakening()
    {
        Assert.That(_data.GetPartyCount(true), Is.EqualTo(1));
        Assert.That(_data.GetPartyCount(false), Is.EqualTo(1));
        Invoke("PrepareParty");
        Assert.That(_global.Party, Has.Count.EqualTo(1));
        Assert.That(_global.Party[0].CharacterDataID, Is.EqualTo(_data.Party[0].CharacterID));
        Assert.That(_player.CharacterID, Is.EqualTo("lab.test.member.0"));
        Assert.That(_player.DisplayName, Is.EqualTo("Sample 0"));
        Assert.That(_global.Party[0].HP, Is.EqualTo(120));
        Assert.That(_global.Party[0].AP, Is.EqualTo(60));
    }

    [Test]
    public void NormalPreparation_UsesOnlyTheConfiguredSoloMember()
    {
        Invoke("PrepareParty");
        Assert.That(_global.Party, Has.Count.EqualTo(1));
        Assert.That(_global.Party[0].CharacterDataID, Is.EqualTo(_data.Party[0].CharacterID));
    }

    [Test]
    public void SpawnedParty_AwakeRegistrationAndDelayedStartKeepTheSoloFrontliner()
    {
        _data.Party[0].BattleSpriteTint = Color.white;
        Invoke("PrepareParty");
        BattleManager battle = GetField<BattleManager>("_battle");
        MethodInfo register = typeof(BattleManager).GetMethod("RegisterPreparedPartyMember", PrivateInstance);
        var interactions = new List<InteractionSystem>();
        var players = new List<PlayerCharacter>();

        for (int i = 0; i < _global.Party.Count; i++)
        {
            PlayerCharacter player = i == 0 ? _player : MakeComponent<PlayerCharacter>("Spawned lab member " + i);
            if (player.GetComponent<Rigidbody2D>() == null)
                player.gameObject.AddComponent<Rigidbody2D>();
            player.gameObject.AddComponent<SpriteRenderer>();
            PlayerController controller = player.GetComponent<PlayerController>();
            if (controller == null) controller = player.gameObject.AddComponent<PlayerController>();
            InteractionSystem interaction = player.gameObject.AddComponent<InteractionSystem>();
            // Player_Base의 실제 생성 순서: Awake → 데이터 주입 → 전열 등록 → 다음 프레임 Start.
            typeof(PlayerCharacter).GetMethod("Awake", PrivateInstance).Invoke(player, null);
            typeof(PlayerController).GetMethod("Awake", PrivateInstance).Invoke(controller, null);
            typeof(InteractionSystem).GetMethod("Awake", PrivateInstance).Invoke(interaction, null);
            player.LoadDataFromGlobal(_global.Party[i]);
            Assert.That(register.Invoke(battle, new object[] { player, null, true }), Is.True);
            players.Add(player);
            interactions.Add(interaction);
        }

        Assert.That(battle._playerParty, Has.Count.EqualTo(1));
        Assert.That(InteractionSystem.Instance, Is.SameAs(interactions[0]));
        for (int i = 0; i < players.Count; i++)
        {
            PlayerCharacter player = players[i];
            Assert.That(player != null, Is.True, "중복 InteractionSystem이 동료 GameObject를 삭제하면 안 됩니다.");
            Assert.That(interactions[i].enabled, Is.EqualTo(i == 0));
            Vector3 slot = player.transform.position;
            PlayerController controller = player.GetComponent<PlayerController>();
            typeof(PlayerController).GetMethod("Start", PrivateInstance).Invoke(controller, null);
            typeof(PlayerCharacter).GetMethod("Start", PrivateInstance).Invoke(player, null);
            Assert.That(player.transform.position, Is.EqualTo(slot));
            Assert.That(controller.State, Is.EqualTo(PlayerController.PlayerState.InBattle));
            Assert.That(player.CharacterID, Is.EqualTo(_data.Party[i].CharacterID));
            Assert.That(player.GetComponent<SpriteRenderer>().color, Is.EqualTo(_data.Party[i].BattleSpriteTint));
            Assert.That(player.gameObject.activeSelf, Is.True);
            if (i > 0) Assert.That(slot, Is.Not.EqualTo(players[i - 1].transform.position));
        }
    }

    [Test]
    public void Destroy_RestoresTheOwnedSoloSession()
    {
        SetField("_ownsSession", true);
        SetField("_emptySession", new SaveData());
        Invoke("PrepareParty");
        Invoke("OnDestroy");
        Assert.That(_global.Party, Is.Empty);
    }

    [Test]
    public void Destroy_DoesNotClaimAnUnexpectedRosterSize()
    {
        SetField("_ownsSession", true);
        SetField("_emptySession", new SaveData());
        Invoke("PrepareParty");
        _global.Party.Add(new CharacterSaveData { CharacterDataID = "unexpected.member" });
        Invoke("OnDestroy");
        Assert.That(_global.Party, Has.Count.EqualTo(2));
    }

    [Test]
    public void ReturnToMenu_WaitsForHostCleanupBeforeResettingParty()
    {
        Invoke("PrepareParty");
        _global.Party[0].HP = 7;
        IEnumerator routine = (IEnumerator)Invoke("ReturnToMenu", "Finished");
        Assert.That(routine.MoveNext(), Is.True);
        Assert.That(_global.Party[0].HP, Is.EqualTo(7));
        Assert.That(GetField<bool>("_returning"), Is.True);
        Assert.That(_session.ReturnToExplorationOnDefeat, Is.True);
        (routine as IDisposable)?.Dispose();
    }

    [Test]
    public void Destroy_DoesNotRestoreOverAReplacementParty()
    {
        SetField("_ownsSession", true);
        SetField("_emptySession", new SaveData());
        Invoke("PrepareParty");
        var replacement = new CharacterSaveData { CharacterDataID = "loaded.hero", HP = 79 };
        _global.Party.Clear();
        _global.Party.Add(replacement);
        Invoke("OnDestroy");
        Assert.That(_global.Party[0], Is.SameAs(replacement));
    }

    [Test]
    public void Destroy_DoesNotRestoreOverANewPartyWithTheSameCharacterId()
    {
        SetField("_ownsSession", true);
        SetField("_emptySession", new SaveData());
        Invoke("PrepareParty");
        var replacement = new CharacterSaveData { CharacterDataID = _data.Party[0].CharacterID, HP = 79 };
        _global.Party.Clear();
        _global.Party.Add(replacement);
        Invoke("OnDestroy");
        Assert.That(_global.Party[0], Is.SameAs(replacement));
        Assert.That(_global.Party[0].HP, Is.EqualTo(79));
    }

    [Test]
    public void Menu_ShowsTheExistingSoloActorWithoutCreatingCompanions()
    {
        _player.gameObject.AddComponent<SpriteRenderer>();
        Invoke("PrepareParty");
        SetField("_ownsSession", true);
        Invoke("BuildView");
        Invoke("SetMenuVisible", true);

        Assert.That(_session.GetComponentsInChildren<SpriteRenderer>(true), Is.Empty);
        Assert.That(_session.GetComponentsInChildren<PlayerCharacter>(true), Is.Empty);
        Assert.That(_session.GetComponentsInChildren<PlayerController>(true), Is.Empty);
        Assert.That(_session.GetComponentsInChildren<Collider2D>(true), Is.Empty);
        Assert.That(_global.Party, Has.Count.EqualTo(1));

        Invoke("SetMenuVisible", false);
        Invoke("SetMenuVisible", true);
        Assert.That(_session.GetComponentsInChildren<SpriteRenderer>(true), Is.Empty);
        Assert.That(_global.Party, Has.Count.EqualTo(1));
    }

    [Test]
    public void Menu_DoesNotCreateActorsForAnUnownedSession()
    {
        _player.gameObject.AddComponent<SpriteRenderer>();
        Invoke("BuildView");
        Invoke("SetMenuVisible", true);
        Assert.That(_session.GetComponentsInChildren<SpriteRenderer>(true), Is.Empty);
        Assert.That(_global.Party, Is.Empty);
    }

    [Test]
    public void View_Uses640By480SafeLayoutWithoutMouseControls()
    {
        Invoke("BuildView");
        CanvasScaler scaler = _session.GetComponentInChildren<CanvasScaler>(true);
        Assert.That(scaler.referenceResolution, Is.EqualTo(new Vector2(640, 480)));
        Assert.That(scaler.screenMatchMode, Is.EqualTo(CanvasScaler.ScreenMatchMode.Expand));
        Assert.That(_session.GetComponentsInChildren<GraphicRaycaster>(true), Is.Empty);
        Assert.That(_session.GetComponentsInChildren<Button>(true), Is.Empty);
        foreach (Graphic graphic in _session.GetComponentsInChildren<Graphic>(true))
            Assert.That(graphic.raycastTarget, Is.False);
        Assert.That(scaler.transform.Find("Backdrop"), Is.Null,
            "시작 메뉴가 월드의 주인공을 전체 화면으로 가리면 안 됩니다.");
        Transform content = scaler.transform.Find("640x480 Safe Content");
        var header = content.Find("Header Background").GetComponent<RectTransform>();
        var controls = content.Find("Controls Background").GetComponent<RectTransform>();
        Assert.That(-header.anchoredPosition.y + header.sizeDelta.y, Is.LessThanOrEqualTo(112f));
        Assert.That(-controls.anchoredPosition.y, Is.GreaterThanOrEqualTo(272f));
    }

    [Test]
    public void SceneBuilder_RejectsProductionPathsWithoutChangingLoadedScenes()
    {
        int count = SceneManager.sceneCount;
        Scene active = SceneManager.GetActiveScene();
        Assert.Throws<ArgumentException>(() => BunnySlimeLabSceneBuilder.Build("Assets/_Game/Content/Maps/Regions/Chapter01", _data));
        Assert.That(SceneManager.sceneCount, Is.EqualTo(count));
        Assert.That(SceneManager.GetActiveScene(), Is.EqualTo(active));
    }

    private bool Validate()
    {
        object[] args = { null };
        return (bool)typeof(BunnySlimeBattleLabSession).GetMethod("ValidateConfiguration", PrivateInstance).Invoke(_session, args);
    }
    private object Invoke(string method, params object[] args) => typeof(BunnySlimeBattleLabSession).GetMethod(method, PrivateInstance).Invoke(_session, args);
    private void SetField(string name, object value) => typeof(BunnySlimeBattleLabSession).GetField(name, PrivateInstance).SetValue(_session, value);
    private T GetField<T>(string name) => (T)typeof(BunnySlimeBattleLabSession).GetField(name, PrivateInstance).GetValue(_session);
    private static void SetSingleton(Type type, object value) => type.GetField("<Instance>k__BackingField", BindingFlags.Static | BindingFlags.NonPublic).SetValue(null, value);
    private GameObject MakeObject(string name) { var root = new GameObject(name); root.SetActive(false); _owned.Add(root); return root; }
    private T MakeComponent<T>(string name) where T : Component => MakeObject(name).AddComponent<T>();
    private T MakeAsset<T>() where T : ScriptableObject { T data = ScriptableObject.CreateInstance<T>(); _owned.Add(data); return data; }
}
