#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>실행 버튼에서만 만드는 독립 실습 데이터. 기존 자산을 재작성하지 않습니다.</summary>
public static class BunnySlimeLabContentBuilder
{
    public const string SourceEnemyPath = "Assets/_Game/Content/Characters/EnemyDB/tests_BunnySlime/tests_Enemy_BunnySlime.asset";
    private const string PlayerPath = "Assets/_Game/Content/Characters/AllyDB/PlayerDB.asset";
    private const string CatalogPath = "Assets/_Game/Resources/HubToHome/GameContentCatalog.asset";
    private const string AllySkills = "Assets/_Game/Content/Skills/Ally/tests_Player/tests_Skill_";
    private const string SampleItems = "Assets/_Game/Content/Items/Consumables/tests_Samples/tests_";

    public static BunnySlimeBattleLabData Build(string root)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode
            || !string.Equals(root, BunnySlimeBattleLabBuilder.Root, StringComparison.Ordinal))
            throw new InvalidOperationException("Edit Mode의 지정된 실험실 폴더에서만 생성합니다.");
        EnemyData source = Require<EnemyData>(SourceEnemyPath);
        CharacterData player = Require<CharacterData>(PlayerPath);
        GameContentCatalog catalog = Require<GameContentCatalog>(CatalogPath);
        GameObject sparks = RequireGuid<GameObject>("389d534cbbbcee443bc83df66acb5c97");
        GameObject fire = RequireGuid<GameObject>("2c094461acd8e2a49b2dbc6982bfe8aa");
        GameObject ice = RequireGuid<GameObject>("25663f8f60fc0504ca6f36128d2e25fc");
        AudioClip bgm = RequireGuid<AudioClip>("2358903c2d2850440a9afe9917fe42d3");

        string[] originalNames = { "PressureSlash", "TwinPressure", "SteamThrust", "ReverseFlow", "TurbineRush",
            "SteamTraverse", "OverheatDive", "BoilerBurst", "CriticalBreakthrough", "PressureRelease" };
        var originals = new SkillData[originalNames.Length];
        for (int i = 0; i < originals.Length; i++)
            originals[i] = Require<SkillData>(AllySkills + originalNames[i] + ".asset");
        string[] itemNames = { "EmergencyBandage", "AdvancedRepairTonic", "FullRepairKit", "FieldRations",
            "SmallPressureCell", "SharedPressureSupply", "ReserveBoiler" };
        var items = new ItemData[itemNames.Length];
        for (int i = 0; i < items.Length; i++)
            items[i] = Require<ItemData>(SampleItems + itemNames[i] + ".asset");

        string skills = root + "/Data/Skills";
        SkillData guard = EnemyStrike(skills, "guard", "말랑 내려찍기", DefenseRequirement.Any, 1f, 1, false, sparks);
        SkillData dodge = EnemyStrike(skills, "dodge", "쓸어 담기 · X", DefenseRequirement.DodgeOnly, 1.1f, 1, false, sparks);
        SkillData triple = EnemyStrike(skills, "triple", "하나, 둘, 말랑!", DefenseRequirement.Any, .65f, 3, false, sparks);
        SkillData splash = EnemyStrike(skills, "splash", "공방 대청소", DefenseRequirement.Any, .9f, 1, true, ice);
        SkillData counter = EnemyStrike(skills, "counter", "최종 안전검사 · C", DefenseRequirement.Counterable, 2f, 1, true, sparks);
        SkillData wave = EnemyStrike(skills, "wave", "후열 호출 시험", DefenseRequirement.Any, 1f, 1, true, sparks);
        SkillData shot = EnemyProjectileSkill(skills, "shot", "말랑 압축탄", false, false, fire, sparks);
        SkillData doubleShot = EnemyProjectileSkill(skills, "double_shot", "엇박자 두 발", true, false, fire, sparks);
        SkillData dodgeShot = EnemyProjectileSkill(skills, "dodge_shot", "과압력탄 · X", false, true, fire, sparks);
        SkillData wet = StatusSkill(skills, "enemy_wet", "분무기 오작동", StatusEffectIds.Wet, false, true, ice, SkillUsageProfile.EnemyOnly);
        SkillData poison = StatusSkill(skills, "enemy_poison", "시약 누출 주의", StatusEffectIds.Poison, false, false, sparks, SkillUsageProfile.EnemyOnly);

        var status = new SkillData[StatusEffectFactory.KnownIds.Count];
        string[] statusNames = { "불씨 실험", "냉각 실험", "날붙이 실험", "부식 시약", "고정 장치", "충격 시험", "과출력 시동", "냉각 보호막", "분무 노즐" };
        for (int i = 0; i < status.Length; i++)
        {
            string id = StatusEffectFactory.KnownIds[i];
            bool friendly = id == StatusEffectIds.IceShield || id == StatusEffectIds.Berserk;
            status[i] = StatusSkill(skills, "status_" + id.ToLowerInvariant(), statusNames[i], id,
                friendly, false, friendly ? ice : sparks, SkillUsageProfile.PlayerOnly);
        }
        SkillData fireShot = ProjectileSkill(skills, "fire", "휴대 화염 노즐", DamageElement.Fire, fire, sparks);
        SkillData electric = ProjectileSkill(skills, "electric", "휴대 방전기", DamageElement.Electric, fire, sparks);
        SkillData corrosion = ProjectileSkill(skills, "corrosion", "부식액 발사", DamageElement.Corrosion, fire, sparks);
        SkillData iceShot = ProjectileSkill(skills, "ice", "냉각탄 발사", DamageElement.Ice, fire, ice);

        EnemyData enemy = Asset<EnemyData>(root + "/Data/Enemy/BunnySlime_Lab.asset", value =>
        {
            value.EnemyId = "lab.bunny_slime";
            value.EnemyName = "말랑 안전감독관";
            value.Portrait = source.Portrait;
            value.TurnOrderPortrait = source.TurnOrderPortrait;
            value.BaseStats = new StatBlock { MaxHP = 1800, MaxAP = 100, ATK = 26, DEF = 4, SPD = 14 };
            value.BattleBGM = bgm;
            value.SkillUseChance = 1;
            value.StrongSkillUseChance = 0;
            value.HasEnragedPattern = false;
            value.AllowInstantKillAfterDefeat = false;
            value.SkillList = new List<SkillData> { guard, shot, dodge, doubleShot, triple, wet, poison, splash, dodgeShot, counter };
            value.StrongSkillList = new List<SkillData>();
            value.EXPReward = 30;
            value.GoldReward = 20;
            value.Drops = new List<EnemyDropEntry>
            {
                new EnemyDropEntry { ItemId = items[0].ItemID, MinAmount = 1, MaxAmount = 1, DropChance = 1f }
            };
        });
        EnsureId(enemy.EnemyId, "lab.bunny_slime", enemy);
        if (enemy.Prefab == null)
        {
            BunnySlimeLabVisualBuilder.Build(root, source, enemy);
            AssetDatabase.SaveAssetIfDirty(enemy);
        }
        BunnySlimeLabVisualBuilder.ValidateCombatPrefab(enemy.Prefab, enemy);

        // 같은 원본 외형을 쓰되 식별자와 전투 역할/색은 분리합니다. 신규 동료 디자인으로 간주하지 않습니다.
        string[] ids = { "front", "support", "striker", "reserve1", "reserve2", "reserve3" };
        string[] names = { "위젤 · 압력", "위젤 · 지원", "위젤 · 돌격", "위젤 · 열기", "위젤 · 냉각", "위젤 · 방전" };
        Color[] colors = { new Color(.95f,.72f,.3f), new Color(.3f,.8f,.68f), new Color(.88f,.5f,.56f),
            new Color(.98f,.46f,.27f), new Color(.45f,.75f,1f), new Color(.75f,.58f,1f) };
        SkillData[][] loadouts =
        {
            new[] { originals[0], originals[1], originals[2], originals[3] },
            new[] { status[7], status[8], status[6], originals[5] },
            new[] { originals[4], originals[6], originals[7], originals[8] },
            new[] { fireShot, status[0], status[2], originals[9] },
            new[] { iceShot, status[1], status[4], status[5] },
            new[] { electric, corrosion, status[3], originals[0] }
        };
        var party = new CharacterData[6];
        for (int i = 0; i < party.Length; i++)
        {
            int index = i;
            string id = "lab.wizzel." + ids[i];
            party[i] = Asset<CharacterData>(root + "/Data/Party/" + ids[i] + ".asset", value =>
            {
                value.CharacterID = id;
                value.DisplayName = names[index];
                value.DisplayNameMode = CharacterDisplayNameMode.StaticData;
                value.Portrait = player.Portrait;
                value.TurnOrderPortrait = player.TurnOrderPortrait;
                value.BattlePrefab = player.BattlePrefab;
                value.BattleSymbolColor = colors[index];
                value.GrowthProfile = player.GrowthProfile;
                value.BaseStats = new StatBlock { MaxHP = 220, MaxAP = 100, ATK = index == 1 ? 20 : 28, DEF = 5, SPD = 13 - index };
                value.DefaultSkills = new List<SkillData>(loadouts[index]);
            });
            EnsureId(party[i].CharacterID, id, party[i]);
        }

        EquipmentData weapon = Equipment(root, "pressure_blade", "실습용 압력검", EquipmentSlot.Weapon, 4, 0, 0);
        EquipmentData armor = Equipment(root, "apron", "말랑 작업복", EquipmentSlot.Body, 0, 2, 0);
        EquipmentData charm = Equipment(root, "valve", "예비 압력 밸브", EquipmentSlot.Accessory1, 0, 0, 20);
        BunnySlimeBattleLabData data = Asset<BunnySlimeBattleLabData>(root + "/Data/BunnySlimeBattleLab.asset", value =>
        {
            value.Enemy = enemy;
            value.Party = party;
            value.Items = items;
            value.Equipment = new[] { weapon, armor, charm };
            value.GuardSkill = guard;
            value.DodgeSkill = dodge;
            value.CounterSkill = counter;
            value.WaveSkill = wave;
            value.ProjectileSkills = new[] { shot, doubleShot, dodgeShot };
        });

        // ID 기반 전투 로더를 우회하지 않습니다. 사용자의 생성 명령에서만 카탈로그에 추가합니다.
        // 충돌 검사가 모두 끝나기 전에는 실 카탈로그 목록을 변경하지 않습니다.
        var characters = new List<CharacterData>(catalog.Characters);
        var enemies = new List<EnemyData>(catalog.Enemies);
        var registeredItems = new List<ItemData>(catalog.Items);
        var equipment = new List<EquipmentData>(catalog.Equipment);
        var registeredSkills = new List<SkillData>(catalog.Skills);
        bool changed = false;
        foreach (CharacterData member in data.Party) changed |= Register(characters, member, x => x.CharacterID);
        changed |= Register(enemies, data.Enemy, x => x.EnemyId);
        foreach (ItemData item in data.Items) changed |= Register(registeredItems, item, x => x.ItemID);
        foreach (EquipmentData equip in data.Equipment) changed |= Register(equipment, equip, x => x.ItemID);
        foreach (CharacterData member in data.Party)
            foreach (SkillData skill in member.DefaultSkills) changed |= Register(registeredSkills, skill, x => x.SkillID);
        foreach (SkillData skill in data.Enemy.SkillList) changed |= Register(registeredSkills, skill, x => x.SkillID);
        changed |= Register(registeredSkills, data.WaveSkill, x => x.SkillID);
        if (changed)
        {
            catalog.Characters = characters;
            catalog.Enemies = enemies;
            catalog.Items = registeredItems;
            catalog.Equipment = equipment;
            catalog.Skills = registeredSkills;
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssetIfDirty(catalog);
            GameContentCatalog.InvalidateRuntimeCache();
        }
        return data;
    }

    private static SkillData EnemyStrike(string folder, string id, string title, DefenseRequirement requirement,
        float damage, int hits, bool aoe, GameObject impact)
    {
        return Skill(folder, "enemy_" + id, title, SkillUsageProfile.EnemyOnly, aoe, false, value =>
        {
            value.Description = requirement == DefenseRequirement.Counterable
                ? "C 타이밍 성공: 무피해 + 공격받은 한 명이 접근, 패링, 공격 후 양쪽 복귀. X 회피 가능. Z 방어 불가."
                : requirement == DefenseRequirement.DodgeOnly ? "X 전용 회피 공격. Z로 막을 수 없습니다."
                : "Z 유지: 피해 감소 / Z 정확한 타이밍: 무피해 + AP / X: 회피. 각 타격마다 판정합니다.";
            bool approach = !aoe || requirement == DefenseRequirement.Counterable;
            if (approach) value.ActionTimeline.Add(new Action_Move
            {
                Destination = requirement == DefenseRequirement.Counterable
                    ? Action_Move.MoveDest.Center : Action_Move.MoveDest.AttackStaging,
                Duration = .22f,
                EnemyHopHeight = requirement == DefenseRequirement.Counterable ? .45f : .25f
            });
            for (int i = 0; i < hits; i++)
            {
                float window = hits > 1 ? .65f : .85f;
                value.ActionTimeline.Add(new Action_DefenseWindow
                {
                    DesignerLabel = (i + 1) + "타 · " + title,
                    Requirement = requirement,
                    TelegraphVisualMode = TelegraphVisualMode.AnimatorTrigger,
                    TelegraphAnimatorTriggerName = "Telegraph",
                    TelegraphDuration = i == 0 ? .65f : .18f,
                    TimeWindow = window,
                    // 전용 클립의 충돌 키프레임(.65/.85초)을 QTE 마감과 일치시킵니다.
                    // AttackAnimDelay는 시작 지연이 아니라 시작 후 대기이므로 여기서는 0입니다.
                    AttackAnimTriggerName = hits > 1 ? "LabComboStrike" : aoe ? "LabBurst" : "LabStrike",
                    AttackAnimationLeadTime = window,
                    ImpactCuePrefab = Require<GameObject>("Assets/_Game/Presentation/Custom_VFX/Prefabs/Telegraph.prefab"),
                    ImpactCuePivotName = CharacterPivotId.Top,
                    AttackAnimDelay = 0,
                    DelayAfter = 0,
                    OverrideTimingProfile = true,
                    TimingProfile = new DefenseTimingProfile(.12f, .23f, .38f),
                    CounterDamageMultiplier = 1.8f,
                    FailShakeIntensity = .15f,
                    FailShakeDuration = .12f
                });
                value.ActionTimeline.Add(new Action_VFX { VfxPrefab = impact, Pivot = Action_VFX.VfxPivot.TargetCenter });
                value.ActionTimeline.Add(new Action_Damage { SkillMultiplier = damage, ShakeCamera = false });
                if (i + 1 < hits) value.ActionTimeline.Add(new Action_Wait { WaitTime = .16f });
            }
            value.ActionTimeline.Add(new Action_Wait { WaitTime = .18f });
            if (approach) value.ActionTimeline.Add(new Action_Move { Destination = Action_Move.MoveDest.OriginalPos, Duration = .25f });
        });
    }

    private static SkillData EnemyProjectileSkill(string folder, string id, string title,
        bool doubleShot, bool dodgeOnly, GameObject projectile, GameObject impact)
    {
        return Skill(folder, "enemy_" + id, title, SkillUsageProfile.EnemyOnly, false, false, value =>
        {
            value.Description = dodgeOnly ? "X 전용 투사체. Z 방어 불가."
                : doubleShot ? "느린 첫 탄 뒤 빠른 두 번째 탄. 각 전조마다 Z/X를 새로 입력하세요."
                : "제자리에서 발사하는 단발 투사체. Z 가드/저스트 가드 또는 X 회피.";
            for (int i = 0; i < (doubleShot ? 2 : 1); i++)
            {
                float flight = i == 1 ? .42f : dodgeOnly ? .65f : .55f;
                value.ActionTimeline.Add(new Action_DefenseWindow
                {
                    DesignerLabel = (i + 1) + "탄 대응",
                    Requirement = dodgeOnly ? DefenseRequirement.DodgeOnly : DefenseRequirement.Any,
                    TelegraphVisualMode = TelegraphVisualMode.AnimatorTrigger,
                    TelegraphAnimatorTriggerName = "Telegraph", TelegraphDuration = i == 0 ? .35f : .10f,
                    TimeWindow = flight, AttackAnimTriggerName = "Skill", AttackAnimationLeadTime = flight,
                    ImpactCuePrefab = Require<GameObject>("Assets/_Game/Presentation/Custom_VFX/Prefabs/Telegraph.prefab"),
                    ImpactCuePivotName = CharacterPivotId.Top, AttackAnimDelay = 0, DelayAfter = 0,
                    OverrideTimingProfile = true, TimingProfile = new DefenseTimingProfile(.24f, .28f, .38f),
                    ShakeOnFail = false
                });
                value.ActionTimeline.Add(new Action_Projectile
                {
                    ProjectilePrefab = projectile, ImpactVFXPrefab = impact, FlightDuration = flight,
                    DamageMultiplier = doubleShot ? .65f : dodgeOnly ? 1.2f : 1f
                });
                value.ActionTimeline.Add(new Action_Wait { WaitTime = doubleShot && i == 0 ? .12f : .18f });
            }
        });
    }

    private static SkillData StatusSkill(string folder, string id, string title, string status, bool friendly,
        bool aoe, GameObject vfx, SkillUsageProfile usage)
    {
        return Skill(folder, id, title, usage, aoe, friendly, value =>
        {
            value.Description = StatusLabel(status) + " 2턴 부여. 피해 공격이 아닌 독립 상태 부여이므로 방어 판정이 없습니다.";
            value.ActionTimeline.Add(new Action_PlayAnim { AnimTriggerName = usage == SkillUsageProfile.EnemyOnly ? "Skill" : "Attack", DelayAfter = .2f });
            value.ActionTimeline.Add(new Action_VFX { VfxPrefab = vfx, Pivot = Action_VFX.VfxPivot.TargetCenter });
            value.ActionTimeline.Add(new Action_ApplyStatus { StatusID = status, DurationTurns = 2 });
            value.ActionTimeline.Add(new Action_Wait { WaitTime = .3f });
        });
    }

    private static SkillData ProjectileSkill(string folder, string id, string title, DamageElement element,
        GameObject projectile, GameObject impact)
    {
        return Skill(folder, "shot_" + id, title, SkillUsageProfile.PlayerOnly, false, false, value =>
        {
            string elementName = element == DamageElement.Fire ? "화염" : element == DamageElement.Ice ? "빙결"
                : element == DamageElement.Electric ? "전기" : "부식";
            value.Description = elementName + " 속성 1.3배 투사체. 공격 QTE 성공 시 1.5배, 실패 시 0.75배.";
            value.ActionTimeline.Add(new Action_QTE { TimeLimit = 1f, SuccessMultiplier = 1.5f, FailMultiplier = .75f,
                Nodes = new List<SkillQTENode> { new SkillQTENode { PosX = .5f, PosY = .55f, TargetKey = "z" } } });
            value.ActionTimeline.Add(new Action_PlayAnim { AnimTriggerName = "Attack", DelayAfter = .1f });
            value.ActionTimeline.Add(new Action_Projectile { ProjectilePrefab = projectile, ImpactVFXPrefab = impact,
                Element = element, DamageMultiplier = 1.3f, FlightDuration = .35f });
            value.ActionTimeline.Add(new Action_Wait { WaitTime = .15f });
        });
    }

    private static SkillData Skill(string folder, string id, string title, SkillUsageProfile usage, bool aoe,
        bool friendly, Action<SkillData> configure)
    {
        SkillData skill = Asset<SkillData>(folder + "/" + id + ".asset", value =>
        {
            value.SkillID = "lab.bunny." + id;
            value.SkillName = title;
            value.UsageProfile = usage;
            value.TargetType = friendly ? TargetAreaType.AllyOnly : TargetAreaType.EnemyOnly;
            value.IsAoE = aoe;
            value.APCost = usage == SkillUsageProfile.EnemyOnly ? 0 : 8;
            value.ActionTimeline = new List<SkillActionBlock>();
            configure(value);
        });
        EnsureId(skill.SkillID, "lab.bunny." + id, skill);
        EnemyAttackAuthoringReport report = EnemyAttackAuthoringAnalyzer.Analyze(skill);
        if (report.HasErrors) throw new InvalidOperationException(skill.name + "\n" + report.BuildValidationSummary());
        return skill;
    }

    private static EquipmentData Equipment(string root, string id, string title, EquipmentSlot slot, int atk, int def, int ap)
    {
        EquipmentData equipment = Asset<EquipmentData>(root + "/Data/Equipment/" + id + ".asset", value =>
        {
            value.ItemID = "lab.equipment." + id;
            value.ItemName = title;
            value.Slot = slot;
            value.Description = "실습용 수치 장비. 공격 +" + atk + " / 방어 +" + def + " / 최대 AP +" + ap;
            value.StatBonuses = StatBlock.CreateZeroModifier();
            value.StatBonuses.ATK = atk;
            value.StatBonuses.DEF = def;
            value.StatBonuses.MaxAP = ap;
        });
        EnsureId(equipment.ItemID, "lab.equipment." + id, equipment);
        return equipment;
    }

    private static string StatusLabel(string id)
    {
        switch (id)
        {
            case StatusEffectIds.Burn: return "화상";
            case StatusEffectIds.Freeze: return "빙결";
            case StatusEffectIds.Bleed: return "출혈";
            case StatusEffectIds.Poison: return "독";
            case StatusEffectIds.Bind: return "속박";
            case StatusEffectIds.Stun: return "기절";
            case StatusEffectIds.Berserk: return "광폭화";
            case StatusEffectIds.IceShield: return "얼음 보호막";
            case StatusEffectIds.Wet: return "젖음";
            default: throw new ArgumentException("표시 이름이 없는 상태 ID입니다: " + id);
        }
    }

    private static bool Register<T>(List<T> list, T asset, Func<T, string> id) where T : UnityEngine.Object
    {
        if (asset == null) throw new InvalidOperationException("샘플 참조가 비어 있습니다: " + typeof(T).Name);
        string key = id(asset);
        if (string.IsNullOrWhiteSpace(key)) throw new InvalidOperationException("ID가 없습니다: " + asset.name);
        foreach (T existing in list)
        {
            if (existing == asset) return false;
            if (existing != null && string.Equals(id(existing), key, StringComparison.Ordinal))
                throw new InvalidOperationException("카탈로그 ID 충돌: " + key + ". 기존 자산은 교체하지 않았습니다.");
        }
        list.Add(asset);
        return true;
    }

    public static T Require<T>(string path) where T : UnityEngine.Object
    {
        T value = AssetDatabase.LoadAssetAtPath<T>(path);
        if (value == null) throw new InvalidOperationException("필수 자산을 찾을 수 없습니다: " + path);
        return value;
    }

    private static T RequireGuid<T>(string guid) where T : UnityEngine.Object => Require<T>(AssetDatabase.GUIDToAssetPath(guid));

    public static T Asset<T>(string path, Action<T> configure) where T : ScriptableObject
    {
        UnityEngine.Object existing = AssetDatabase.LoadMainAssetAtPath(path);
        if (existing != null)
        {
            if (existing is T typed) return typed;
            throw new InvalidOperationException("생성 위치에 다른 자산이 있습니다: " + path);
        }
        if (System.IO.File.Exists(path) || System.IO.File.Exists(path + ".meta"))
            throw new InvalidOperationException("아직 불러오지 못한 기존 파일은 덮어쓰지 않습니다: " + path);
        EnsureFolder(path.Substring(0, path.LastIndexOf('/')));
        T value = ScriptableObject.CreateInstance<T>();
        value.name = System.IO.Path.GetFileNameWithoutExtension(path);
        try
        {
            configure(value);
            AssetDatabase.CreateAsset(value, path);
            AssetDatabase.SaveAssetIfDirty(value);
            return value;
        }
        catch
        {
            if (!AssetDatabase.Contains(value)) UnityEngine.Object.DestroyImmediate(value);
            throw;
        }
    }

    public static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        int split = path.LastIndexOf('/');
        if (split < 0 || !path.StartsWith("Assets/", StringComparison.Ordinal))
            throw new ArgumentException("Assets 내부 폴더가 필요합니다.");
        EnsureFolder(path.Substring(0, split));
        AssetDatabase.CreateFolder(path.Substring(0, split), path.Substring(split + 1));
    }

    private static void EnsureId(string actual, string expected, UnityEngine.Object asset)
    {
        if (!string.Equals(actual, expected, StringComparison.Ordinal))
            throw new InvalidOperationException("실습 ID가 바뀐 자산은 덮어쓰지 않습니다: " + AssetDatabase.GetAssetPath(asset));
    }
}
#endif
