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
        _data.Party = new CharacterData[6];
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
        _data.Party = new CharacterData[5];
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
    public void Validation_RejectsDuplicateOrUnregisteredPartyIds()
    {
        Assert.That(Validate(), Is.True);
        _catalog.Characters.RemoveAt(5);
        CharacterDatabase.InvalidateCache();
        Assert.That(Validate(), Is.False);
        _catalog.Characters.Add(_data.Party[5]);
        _data.Party[5].CharacterID = _data.Party[0].CharacterID;
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
    public void WavePreparation_UsesStableRuntimeIdsAndOnlyWeakensFrontLine()
    {
        Invoke("PrepareParty", true);
        Assert.That(_global.Party, Has.Count.EqualTo(6));
        for (int i = 0; i < 6; i++)
        {
            Assert.That(_global.Party[i].CharacterDataID, Is.EqualTo(_data.Party[i].CharacterID));
            Assert.That(_global.Party[i].HP, Is.EqualTo(i < 3 ? 1 : 120));
        }
        Assert.That(_player.CharacterID, Is.EqualTo("lab.test.member.0"));
        Assert.That(_player.DisplayName, Is.EqualTo("Sample 0"));
        Invoke("PrepareParty", false);
        Assert.That(_global.Party[0].HP, Is.EqualTo(120));
        Assert.That(_global.Party, Has.Count.EqualTo(3));
        Assert.That(_global.Party[0].AP, Is.EqualTo(60));
    }

    [Test]
    public void NormalPreparation_UsesOnlyTheThreeFrontlineMembers()
    {
        Invoke("PrepareParty", false);
        Assert.That(_global.Party, Has.Count.EqualTo(3));
        for (int i = 0; i < 3; i++)
            Assert.That(_global.Party[i].CharacterDataID, Is.EqualTo(_data.Party[i].CharacterID));
    }

    [TestCase(false)]
    [TestCase(true)]
    public void SpawnedParty_AwakeRegistrationAndDelayedStartKeepThreeDistinctFrontliners(bool waveExercise)
    {
        _data.Party[1].BattleSpriteTint = Color.cyan;
        _data.Party[2].BattleSpriteTint = Color.magenta;
        Invoke("PrepareParty", waveExercise);
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

        Assert.That(battle._playerParty, Has.Count.EqualTo(3));
        Assert.That(InteractionSystem.Instance, Is.SameAs(interactions[0]));
        for (int i = 0; i < players.Count; i++)
        {
            PlayerCharacter player = players[i];
            Assert.That(player != null, Is.True, "중복 InteractionSystem이 동료 GameObject를 삭제하면 안 됩니다.");
            Assert.That(interactions[i].enabled, Is.EqualTo(i == 0));
            if (i >= 3)
            {
                Assert.That(player.gameObject.activeSelf, Is.False);
                continue;
            }
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
        Object.DestroyImmediate(players[1].gameObject);
        Assert.That(InteractionSystem.Instance, Is.SameAs(interactions[0]), "동료 정리가 필드 상호작용 소유권을 해제하면 안 됩니다.");
    }

    [TestCase(false)]
    [TestCase(true)]
    public void Destroy_RestoresOwnedSessionForBothRosterSizes(bool waveExercise)
    {
        SetField("_ownsSession", true);
        SetField("_emptySession", new SaveData());
        Invoke("PrepareParty", waveExercise);
        Invoke("OnDestroy");
        Assert.That(_global.Party, Is.Empty);
    }

    [Test]
    public void Destroy_DoesNotClaimAnUnexpectedRosterSize()
    {
        SetField("_ownsSession", true);
        SetField("_emptySession", new SaveData());
        Invoke("PrepareParty", false);
        _global.Party.Add(new CharacterSaveData { CharacterDataID = _data.Party[3].CharacterID });
        Invoke("OnDestroy");
        Assert.That(_global.Party, Has.Count.EqualTo(4));
    }

    [Test]
    public void ReturnToMenu_WaitsForHostCleanupBeforeResettingParty()
    {
        Invoke("PrepareParty", false);
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
        Invoke("PrepareParty", false);
        var replacement = new CharacterSaveData { CharacterDataID = "loaded.hero", HP = 79 };
        _global.Party.Clear();
        _global.Party.Add(replacement);
        Invoke("OnDestroy");
        Assert.That(_global.Party[0], Is.SameAs(replacement));
    }

    [Test]
    public void MenuPreview_ShowsTwoTintedCompanionsWithoutCreatingBattleActors()
    {
        _player.gameObject.AddComponent<SpriteRenderer>();
        _data.Party[1].BattleSpriteTint = Color.cyan;
        _data.Party[2].BattleSpriteTint = Color.magenta;
        Invoke("PrepareParty", false);
        SetField("_ownsSession", true);
        Invoke("BuildView");
        Invoke("SetMenuVisible", true);

        GameObject preview = GetField<GameObject>("_partyPreviewRoot");
        Assert.That(preview.activeSelf, Is.True);
        SpriteRenderer[] companions = preview.GetComponentsInChildren<SpriteRenderer>(true);
        Assert.That(companions, Has.Length.EqualTo(2));
        Assert.That(companions[0].color, Is.EqualTo(Color.cyan));
        Assert.That(companions[1].color, Is.EqualTo(Color.magenta));
        Assert.That(companions[0].transform.position, Is.Not.EqualTo(_player.transform.position));
        Assert.That(companions[1].transform.position, Is.Not.EqualTo(companions[0].transform.position));
        Assert.That(preview.GetComponentsInChildren<PlayerCharacter>(true), Is.Empty);
        Assert.That(preview.GetComponentsInChildren<PlayerController>(true), Is.Empty);
        Assert.That(preview.GetComponentsInChildren<Collider2D>(true), Is.Empty);
        Assert.That(_global.Party, Has.Count.EqualTo(3));

        Invoke("SetMenuVisible", false);
        Assert.That(preview.activeSelf, Is.False);
        Invoke("SetMenuVisible", true);
        Assert.That(GetField<GameObject>("_partyPreviewRoot"), Is.SameAs(preview));
        Assert.That(preview.GetComponentsInChildren<SpriteRenderer>(true), Has.Length.EqualTo(2));
        Assert.That(_global.Party, Has.Count.EqualTo(3));
    }

    [Test]
    public void MenuPreview_DoesNotCreateCompanionsForAnUnownedSession()
    {
        _player.gameObject.AddComponent<SpriteRenderer>();
        Invoke("BuildView");
        Invoke("SetMenuVisible", true);
        Assert.That(GetField<GameObject>("_partyPreviewRoot"), Is.Null);
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
            "시작 메뉴가 월드의 파티 미리보기를 전체 화면으로 가리면 안 됩니다.");
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
