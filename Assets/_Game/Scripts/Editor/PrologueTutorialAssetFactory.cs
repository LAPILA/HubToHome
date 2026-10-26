#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using Object = UnityEngine.Object;

internal static class PrologueTutorialAssetFactory
{
    internal const string EnemyPath = "Assets/_Game/Content/Characters/EnemyDB/Prologue/Enemy_WhiteEntityPart.asset";
    internal const string SkillPath = "Assets/_Game/Content/Skills/Enemy/Prologue/Skill_WhiteReach.asset";
    internal const string ItemPath = "Assets/_Game/Content/Items/Consumables/Prologue/Item_EmergencyDressing.asset";
    internal const string TelegraphPath = "Assets/_Game/Content/Art/Icons/Custom_VFX/Prefabs/Telegraph.prefab";
    internal const string BattleSource = "Assets/_Game/Content/Scenarios/Source/Battle/Prologue/prologue_white_entity.scenario.yaml";
    internal const string SequenceSourceRoot = "Assets/_Game/Content/Scenarios/Source/Overworld/Prologue/";
    internal const string OriginalWindowDialoguePath = PrologueTutorialContentBuilder.RegionRoot + "/Data/Prologue/Dialogue/Dialogue_Prologue_Window.asset";
    private const string Root = PrologueTutorialContentBuilder.Root;
    private static Dictionary<string, string[]> _translations;

    /// <summary>모든 입력을 쓰기 전에 확인합니다. 생성·새로고침·씬 열기를 수행하지 않습니다.</summary>
    internal static void ValidateDependencies()
    {
        PrologueTutorialContentBuilder.Require<CharacterData>("Assets/_Game/Content/Characters/AllyDB/WizzelDB.asset");
        PrologueTutorialContentBuilder.Require<SpeakerData>("Assets/_Game/Content/Dialogue/Data/Speakers/Speaker_Wizzel.asset");
        ValidateWindowSource();
        PrologueTutorialContentBuilder.Require<RuntimeAnimatorController>("Assets/_Game/Content/Art/Characters/Player/Wizzel/Player_Animator.controller");
        PrologueTutorialContentBuilder.Require<PrologueTrainAudioProfile>(PrologueTutorialContentBuilder.RegionRoot + "/Data/Prologue/Prologue_TrainAudio.asset");
        PrologueTutorialContentBuilder.Require<ItemData>("Assets/_Game/Content/Items/Consumables/tests_Samples/tests_EmergencyBandage.asset");
        PrologueTutorialContentBuilder.Require<GameObject>(TelegraphPath);
        PrologueTutorialContentBuilder.Require<JournalCatalog>("Assets/_Game/Resources/HubToHome/JournalCatalog.asset");
        PrologueTutorialContentBuilder.Require<SceneAsset>(PrologueTutorialContentBuilder.RegionScene);
        PrologueTutorialContentBuilder.Require<SceneAsset>(PrologueTutorialContentBuilder.IntroScene);
        PrologueTutorialContentBuilder.Require<RoomDefinition>(PrologueTutorialContentBuilder.RegionRoot + "/Data/Rooms/Room_ALL_TrainQ0.asset");
        GameObject room = PrologueTutorialContentBuilder.Require<GameObject>(PrologueTutorialContentBuilder.OriginalRoom);
        if (room.GetComponent<RoomInstance>() == null || room.GetComponent<RoomInstance>().CameraBounds == null)
            throw new InvalidOperationException("Q0 원본에 RoomInstance/카메라 경계가 없습니다.");
        GameObject enemy = PrologueTutorialContentBuilder.Require<GameObject>("Assets/_Game/Content/Characters/Prefabs/Enemy/Enemy_Base.prefab");
        if (enemy.GetComponent<EnemyCharacter>() == null || enemy.GetComponent<Animator>() == null || enemy.GetComponent<SpriteRenderer>() == null)
            throw new InvalidOperationException("공용 적 원본에 EnemyCharacter/Animator/SpriteRenderer가 없습니다.");
        GameObject host = PrologueTutorialContentBuilder.Require<GameObject>("Assets/_Game/Content/Battle/Prefabs/System/SeamlessBattleHost.prefab");
        if (host.GetComponent<SeamlessBattleHost>() == null || !host.GetComponent<SeamlessBattleHost>().IsConfigured(out _))
            throw new InvalidOperationException("공용 전투 Host의 연결이 유효하지 않습니다.");
        foreach (string path in new[] {
            "Assets/_Game/Content/Art/Characters/Player/Wizzel/Wizzel_overworld.aseprite",
            "Assets/_Game/Content/Art/Animation/Prologue/Monster.png",
            "Assets/_Game/Content/Art/Environment/Train/In/SubwayIn.png" }) FirstSprite(path);
        foreach (string path in new[] { "/Data/Prologue/Prologue_Opening.asset", "/Data/Prologue/Prologue_RearReveal.asset" })
        {
            IllustratedStoryAsset story = PrologueTutorialContentBuilder.Require<IllustratedStoryAsset>(PrologueTutorialContentBuilder.RegionRoot + path);
            if (!story.TryValidate(out string error)) throw new InvalidOperationException("삽화 원본 오류: " + path + "/" + error);
        }
        PrologueTutorialContentBuilder.Require<ActionCatalogAsset>(ProductionActionLibraryBuildCommand.GeneratedAssetPath);
        var sourcePaths = new List<string> { BattleSource };
        foreach (string id in new[] { "incident", "rescue", "seal", "forward", "control", "derail" })
            sourcePaths.Add(SequenceSourceRoot + id + ".sequence.yaml");
        foreach (string path in sourcePaths)
        {
            string text = File.ReadAllText(path, PrologueTutorialContentBuilder.StrictUtf8);
            ScenarioSourceParseResult parsed = new ScenarioSourceYamlParser().Parse(text, path);
            RequireValid(parsed.Validation, path);
            if (!parsed.Success) throw new InvalidOperationException("시나리오 원본 읽기 실패: " + path);
        }
        GameContentCatalog catalog = PrologueTutorialContentBuilder.Require<GameContentCatalog>(PrologueTutorialContentBuilder.CatalogPath);
        ValidateCatalogId(catalog.Items, "prologue.tutorial.field_dressing", ItemPath, value => value.ItemID);
        ValidateCatalogId(catalog.Skills, "prologue.tutorial.white_reach", SkillPath, value => value.SkillID);
        ValidateCatalogId(catalog.Enemies, "prologue.white_entity.part", EnemyPath, value => value.EnemyId);
    }

    internal static void ValidateLocalization()
    {
        string text = File.ReadAllText("Assets/Resources/LocalizationTable.csv", PrologueTutorialContentBuilder.StrictUtf8);
        _translations = new Dictionary<string, string[]>(StringComparer.Ordinal);
        foreach (string[] row in LocalizationCsv.Parse(text))
        {
            if (row.Length == 0 || !row[0].StartsWith("HTH_PROLOGUE_", StringComparison.Ordinal)) continue;
            if (row.Length != 5 || !_translations.TryAdd(row[0], row))
                throw new InvalidOperationException("튜토리얼 번역 중복/열 개수 오류: " + row[0]);
            for (int i = 1; i < 5; i++)
                if (string.IsNullOrWhiteSpace(row[i])) throw new InvalidOperationException("빈 튜토리얼 번역: " + row[0] + "/" + i);
        }
        if (_translations.Count < 60) throw new InvalidOperationException("튜토리얼 번역이 누락되었습니다.");
    }

    internal static string Korean(string key)
    {
        if (_translations == null) ValidateLocalization();
        return _translations.TryGetValue(key, out string[] row) ? row[1].Replace("\\n", "\n")
            : throw new InvalidOperationException("누락된 튜토리얼 번역: " + key);
    }

    internal static PrologueTutorialDefinition Build()
    {
        PrologueTutorialDefinition definition = PrologueTutorialContentBuilder.Asset<PrologueTutorialDefinition>(PrologueTutorialContentBuilder.DefinitionPath);
        definition.Protagonist = PrologueTutorialContentBuilder.Require<CharacterData>("Assets/_Game/Content/Characters/AllyDB/WizzelDB.asset");
        definition.Protagonist.DisplayNameLocalizationKey = "HTH_PROLOGUE_NAME_WIZZEL";
        PrologueTutorialContentBuilder.Save(definition.Protagonist);
        definition.TrainAudio = PrologueTutorialContentBuilder.Require<PrologueTrainAudioProfile>(PrologueTutorialContentBuilder.RegionRoot + "/Data/Prologue/Prologue_TrainAudio.asset");
        BuildRoles(definition);
        BuildDialogues(definition);
        BuildStories(definition);
        definition.Introduction = new PrologueTutorialIntroduction();
        BuildBattle(definition);
        BuildSequences(definition);
        definition.RoomFadeDuration = .25f;
        definition.IncidentQuietDuration = .4f;
        definition.IncidentDuration = .85f;
        definition.MaxDefenseAttempts = 3;
        definition.MaxItemPracticeActions = 3;
        PrologueTutorialContentBuilder.Save(definition);
        return definition;
    }

    private static void BuildRoles(PrologueTutorialDefinition definition)
    {
        SpeakerData source = PrologueTutorialContentBuilder.Require<SpeakerData>("Assets/_Game/Content/Dialogue/Data/Speakers/Speaker_Wizzel.asset");
        Sprite protagonistSprite = FirstSprite("Assets/_Game/Content/Art/Characters/Player/Wizzel/Wizzel_overworld.aseprite");
        Sprite monster = FirstSprite("Assets/_Game/Content/Art/Animation/Prologue/Monster.png");
        string[] ids = { "protagonist", "companion_dragon", "companion_deer", "companion_medic", "crew", "white_entity", "conductor" };
        string[] keys = { "WIZZEL", "DRAGON", "DEER", "MEDIC", "CREW", "WHITE_ENTITY", "CONDUCTOR" };
        Color[] colors = { Color.white, new Color(.85f,.57f,.33f), new Color(.64f,.79f,.62f), new Color(.8f,.77f,.94f), new Color(.61f,.72f,.88f), Color.white, new Color(.61f,.72f,.88f) };
        definition.Roles.Clear();
        for (int i = 0; i < ids.Length; i++)
        {
            SpeakerData speaker = PrologueTutorialContentBuilder.Asset<SpeakerData>(Root + "/Speakers/Speaker_" + ids[i] + ".asset");
            speaker.SpeakerID = "prologue.tutorial." + ids[i];
            speaker.DisplayNameLocalizationKey = "HTH_PROLOGUE_NAME_" + keys[i];
            speaker.DisplayName = Korean(speaker.DisplayNameLocalizationKey);
            speaker.VoiceBlipSound = source.VoiceBlipSound;
            speaker.VoicePitch = i == 1 ? .8f : .9f;
            speaker.VoiceVolume = .25f;
            speaker.MinPitch = speaker.VoicePitch;
            speaker.MaxPitch = speaker.VoicePitch;
            speaker.Portraits.Clear();
            foreach (var pair in source.Portraits) if (pair.Value != null) speaker.Portraits[pair.Key] = pair.Value;
            if (i == 5) speaker.Portraits[EmotionType.Normal] = monster;
            PrologueTutorialContentBuilder.Save(speaker);
            var role = new PrologueTutorialRole { RoleId = ids[i], Speaker = speaker, Sprite = i == 5 ? monster : protagonistSprite,
                Animator = i == 5 ? null : PrologueTutorialContentBuilder.Require<RuntimeAnimatorController>("Assets/_Game/Content/Art/Characters/Player/Wizzel/Player_Animator.controller"), Tint = colors[i] };
            role.VisualPrefab = BuildRolePrefab(role, i == 1 ? 1.5f : 1f);
            definition.Roles.Add(role);
        }
    }

    private static GameObject BuildRolePrefab(PrologueTutorialRole role, float scale)
    {
        string path = PrologueTutorialContentBuilder.RegionRoot + "/Prefabs/NPCs/Prologue_" + role.RoleId + ".prefab";
        PrologueTutorialContentBuilder.EnsureFolder(Path.GetDirectoryName(path).Replace('\\','/'));
        var root = new GameObject("Prologue_" + role.RoleId);
        try
        {
            root.AddComponent<PrologueTutorialRoleAnchor>().Configure(role.RoleId);
            var renderer = root.AddComponent<SpriteRenderer>();
            renderer.sprite = role.Sprite;
            renderer.color = role.Tint;
            renderer.sortingLayerName = "Characters";
            renderer.sortingOrder = 3;
            renderer.spriteSortPoint = SpriteSortPoint.Pivot;
            if (role.Animator != null) root.AddComponent<Animator>().runtimeAnimatorController = role.Animator;
            root.transform.localScale = Vector3.one * scale;
            PrologueTutorialContentBuilder.Track(path);
            GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, path);
            return saved;
        }
        finally { Object.DestroyImmediate(root); }
    }

    private static void BuildDialogues(PrologueTutorialDefinition definition)
    {
        definition.Dialogues.Clear();
        Add(definition,"inspect_machine",new[]{"protagonist","protagonist"});
        Add(definition,"adjust_valve",new[]{"protagonist"});
        Add(definition,"test_machine",new[]{"protagonist","crew"});
        Add(definition,"dragon",new[]{"companion_dragon","protagonist","companion_dragon"});
        Add(definition,"incident",new[]{"protagonist","companion_dragon"});
        Add(definition,"rear",new[]{"protagonist","protagonist"});
        Add(definition,"pressure",new[]{"protagonist","protagonist"});
        Add(definition,"rescue",new[]{"companion_dragon","protagonist","companion_dragon"});
        Add(definition,"seal",new[]{"protagonist","companion_dragon"});
        Add(definition,"forward",new[]{"protagonist","crew"});
        Add(definition,"control",new[]{"protagonist","protagonist"});
        Add(definition,"derail",new[]{"protagonist"});
        Add(definition,"optional_deer",new[]{"companion_deer"});
        Add(definition,"optional_medic",new[]{"companion_medic","protagonist","companion_medic"});
        Add(definition,"optional_crew",new[]{"crew"});
        AddSingle(definition,"tutorial_attack","HTH_PROLOGUE_TUTORIAL_ATTACK");
        AddSingle(definition,"tutorial_guard","HTH_PROLOGUE_TUTORIAL_GUARD","HTH_PROLOGUE_TUTORIAL_JUST_GUARD");
        AddSingle(definition,"tutorial_item","HTH_PROLOGUE_TUTORIAL_ITEM");
        AddSingle(definition,"retry","HTH_PROLOGUE_RETRY_PROMPT");
        AddSingle(definition,"ending","HTH_PROLOGUE_ENDING");
        AddSingle(definition,"save_failed","HTH_PROLOGUE_SAVE_FAILED");
        AddSingle(definition,"blocked_repair","HTH_PROLOGUE_BLOCKED_REPAIR");
        AddSingle(definition,"blocked_rear","HTH_PROLOGUE_BLOCKED_REAR");
        AddSingle(definition,"blocked_control","HTH_PROLOGUE_BLOCKED_CONTROL");
        BuildOpeningDialogues(definition);
    }

    internal static void ValidateWindowSource()
    {
        DialogueData source = PrologueTutorialContentBuilder.Require<DialogueData>(OriginalWindowDialoguePath);
        if (source.Nodes == null || source.Nodes.Count != 3)
            throw new InvalidOperationException("기존 창가 대사는 원문 세 줄이어야 합니다.");
        string[] expected = { "밖이 하나도 안 보이네.", "첫 원정대도\n여길 지나갔겠지.", "다들 무사해야 할 텐데." };
        for (int i = 0; i < expected.Length; i++)
            if (source.Nodes[i] == null || !string.Equals(source.Nodes[i].DefaultText, expected[i], StringComparison.Ordinal))
                throw new InvalidOperationException("기존 창가 대사가 달라졌습니다. 원문을 임의로 덮어쓰지 않습니다: " + i);
    }

    /// <summary>기존 승무원 프록시를 바탕으로 기관사 역할만 추가합니다. 다른 역할은 변경하지 않습니다.</summary>
    internal static void BuildConductorRole(PrologueTutorialDefinition definition)
    {
        PrologueTutorialRole crew = definition.GetRole("crew");
        if (crew == null || crew.Speaker == null || crew.VisualPrefab == null)
            throw new InvalidOperationException("기관사 샘플의 원본 승무원 역할이 없습니다.");
        SpeakerData speaker = PrologueTutorialContentBuilder.Asset<SpeakerData>(Root + "/Speakers/Speaker_conductor.asset");
        speaker.name = "Speaker_conductor";
        speaker.SpeakerID = "prologue.tutorial.conductor";
        speaker.DisplayNameLocalizationKey = "HTH_PROLOGUE_NAME_CONDUCTOR";
        speaker.DisplayName = Korean(speaker.DisplayNameLocalizationKey);
        speaker.VoiceSounds = crew.Speaker.VoiceSounds == null ? null : (AudioClip[])crew.Speaker.VoiceSounds.Clone();
        speaker.VoiceBlipSound = crew.Speaker.VoiceBlipSound;
        speaker.VoicePitch = crew.Speaker.VoicePitch;
        speaker.VoiceVolume = crew.Speaker.VoiceVolume;
        speaker.MinPitch = crew.Speaker.MinPitch;
        speaker.MaxPitch = crew.Speaker.MaxPitch;
        // Odin dictionaries are copied in memory as well, not only their serialized backing data.
        speaker.Portraits = new Dictionary<EmotionType, Sprite>(crew.Speaker.Portraits);
        PrologueTutorialContentBuilder.Save(speaker);
        var role = new PrologueTutorialRole { RoleId = "conductor", Speaker = speaker, Sprite = crew.Sprite,
            Animator = crew.Animator, Tint = crew.Tint };
        role.VisualPrefab = BuildRolePrefab(role, crew.VisualPrefab.transform.localScale.x);
        for (int i = definition.Roles.Count - 1; i >= 0; i--)
            if (definition.Roles[i]?.RoleId == role.RoleId) definition.Roles.RemoveAt(i);
        definition.Roles.Add(role);
    }

    internal static void BuildOpeningDialogues(PrologueTutorialDefinition definition)
    {
        foreach (string id in new[] { "window", "introduction", "optional_conductor" })
            for (int i = definition.Dialogues.Count - 1; i >= 0; i--)
                if (definition.Dialogues[i]?.Id == id) definition.Dialogues.RemoveAt(i);
        DialogueData window = PrologueTutorialContentBuilder.Asset<DialogueData>(Root + "/Dialogue/Dialogue_window.asset");
        EditorUtility.CopySerialized(PrologueTutorialContentBuilder.Require<DialogueData>(OriginalWindowDialoguePath), window);
        window.name = "Dialogue_window";
        window.RevealTextImmediately = true;
        for (int i = 0; i < window.Nodes.Count; i++)
        {
            window.Nodes[i].LocalizationKey = "HTH_PROLOGUE_WINDOW_" + (i + 1).ToString("00");
            window.Nodes[i].DefaultText = Korean(window.Nodes[i].LocalizationKey);
            window.Nodes[i].Speaker = definition.GetRole("protagonist").Speaker;
        }
        PrologueTutorialContentBuilder.Save(window);
        definition.Dialogues.Add(new PrologueTutorialDialogueBinding { Id = "window", Dialogue = window });
        Add(definition, "introduction", new[] { "conductor", "protagonist", "conductor", "protagonist", "conductor" });
        Add(definition, "optional_conductor", new[] { "conductor" });
    }

    private static void Add(PrologueTutorialDefinition definition,string id,string[] roles)
    {
        string[] keys = new string[roles.Length];
        for (int i=0;i<keys.Length;i++) keys[i]="HTH_PROLOGUE_DLG_"+id.ToUpperInvariant()+"_"+(i+1).ToString("00");
        WriteDialogue(definition,id,keys,roles);
    }
    private static void AddSingle(PrologueTutorialDefinition definition,string id,params string[] keys)
        => WriteDialogue(definition,id,keys,null);
    private static void WriteDialogue(PrologueTutorialDefinition definition,string id,string[] keys,string[] roles)
    {
        DialogueData dialogue=PrologueTutorialContentBuilder.Asset<DialogueData>(Root+"/Dialogue/Dialogue_"+id+".asset");
        dialogue.Style=DialogueStyle.Overworld;
        dialogue.RevealTextImmediately=true;
        dialogue.Nodes.Clear();
        for(int i=0;i<keys.Length;i++) dialogue.Nodes.Add(new DialogueNode { LocalizationKey=keys[i],DefaultText=Korean(keys[i]),
            Speaker=roles!=null?definition.GetRole(roles[i]).Speaker:null,Emotion=EmotionType.Normal });
        PrologueTutorialContentBuilder.Save(dialogue);
        definition.Dialogues.Add(new PrologueTutorialDialogueBinding { Id=id,Dialogue=dialogue });
    }

    private static void BuildStories(PrologueTutorialDefinition definition)
    {
        IllustratedStoryAsset original=PrologueTutorialContentBuilder.Require<IllustratedStoryAsset>(PrologueTutorialContentBuilder.RegionRoot+"/Data/Prologue/Prologue_Opening.asset");
        IllustratedStoryAsset opening=PrologueTutorialContentBuilder.Asset<IllustratedStoryAsset>(Root+"/Story_TrainOpening.asset");
        EditorUtility.CopySerialized(original,opening);
        opening.name="Story_TrainOpening";
        opening.Pages.RemoveRange(1,Mathf.Max(0,opening.Pages.Count-1));
        IllustratedStoryPage page=opening.Pages[0];
        RestoreWindowPage(page, definition.GetDialogue("window"));
        PrologueTutorialContentBuilder.Save(opening);
        definition.OpeningStory=opening;
        IllustratedStoryAsset rear=PrologueTutorialContentBuilder.Asset<IllustratedStoryAsset>(Root+"/Story_RearReveal.asset");
        EditorUtility.CopySerialized(PrologueTutorialContentBuilder.Require<IllustratedStoryAsset>(PrologueTutorialContentBuilder.RegionRoot+"/Data/Prologue/Prologue_RearReveal.asset"),rear);
        rear.name="Story_RearReveal";
        foreach(IllustratedStoryPage rearPage in rear.Pages) { rearPage.WaitForConfirmWithoutDialogue=false;rearPage.Dialogue=null; }
        PrologueTutorialContentBuilder.Save(rear);definition.RearRevealStory=rear;
    }

    internal static void RestoreWindowPage(IllustratedStoryPage page, DialogueData window)
    {
        if (page == null || window == null) throw new InvalidOperationException("창가 그림 또는 대사 연결이 없습니다.");
        page.Dialogue = window;
        page.MinimumDuration = .5f;
        page.WaitForConfirmWithoutDialogue = false;
        page.FadeInDuration = .45f;
        page.FadeOutDuration = .6f;
        page.MuteTypingSound = false;
    }

    private static void BuildBattle(PrologueTutorialDefinition definition)
    {
        ItemData item=PrologueTutorialContentBuilder.Asset<ItemData>(ItemPath);
        item.ItemID="prologue.tutorial.field_dressing";item.NameLocalizationKey="HTH_PROLOGUE_ITEM_NAME";
        item.DescriptionLocalizationKey="HTH_PROLOGUE_ITEM_DESCRIPTION";item.ItemName=Korean(item.NameLocalizationKey);
        item.Description=Korean(item.DescriptionLocalizationKey);item.Type=ItemType.Consumable;
        item.TargetType=TargetAreaType.AllyOnly;item.IsAoE=false;item.UsableInOverworld=false;item.UsableInBattle=true;
        item.IsSellable=false;item.Price=0;item.ActionType=EffectActionType.Heal;item.TargetStat=TargetStatType.HP;
        item.CalcType=ValueCalcType.Flat;item.EffectValue=90;item.IsStackable=true;item.MaxStackSize=99;
        ItemData bandage=PrologueTutorialContentBuilder.Require<ItemData>("Assets/_Game/Content/Items/Consumables/tests_Samples/tests_EmergencyBandage.asset");
        item.Icon=bandage.Icon;PrologueTutorialContentBuilder.Save(item);definition.HealingItem=item;

        SkillData skill=PrologueTutorialContentBuilder.Asset<SkillData>(SkillPath);
        skill.SkillID="prologue.tutorial.white_reach";skill.NameLocalizationKey="HTH_PROLOGUE_SKILL_NAME";
        skill.DescriptionLocalizationKey="HTH_PROLOGUE_SKILL_DESCRIPTION";skill.SkillName=Korean(skill.NameLocalizationKey);
        skill.Description=Korean(skill.DescriptionLocalizationKey);skill.UsageProfile=SkillUsageProfile.EnemyOnly;
        skill.APCost=0;skill.TargetType=TargetAreaType.EnemyOnly;skill.IsAoE=false;
        skill.ActionTimeline=new List<SkillActionBlock> {
            new Action_Move { DesignerLabel="입구에서 대상 앞으로",Destination=Action_Move.MoveDest.AttackStaging,Duration=.35f },
            new Action_DefenseWindow { DesignerLabel="첫 학습 · 단일 근접 전조",Requirement=DefenseRequirement.Any,
                TelegraphVisualMode=TelegraphVisualMode.AnimatorTrigger,TelegraphAnimatorTriggerName="BattleReady",TelegraphDuration=.6f,
                TimeWindow=.7f,AttackAnimTriggerName="Attack",AttackAnimationLeadTime=.7f,AttackAnimDelay=0,DelayAfter=0,
                ImpactCuePrefab=PrologueTutorialContentBuilder.Require<GameObject>(TelegraphPath),
                ImpactCuePivotName=CharacterPivotId.Center,OverrideTimingProfile=true,TimingProfile=new DefenseTimingProfile(.16f,.25f,.38f),
                ShakeOnFail=false },
            new Action_Damage { DesignerLabel="실제 단일 피해",SkillMultiplier=1f,ShakeCamera=false },
            new Action_Wait { WaitTime=.2f },
            new Action_Move { DesignerLabel="기본 위치로 복귀",Destination=Action_Move.MoveDest.OriginalPos,Duration=.3f }
        };
        EnemyAttackAuthoringReport report=EnemyAttackAuthoringAnalyzer.Analyze(skill);
        if(report.HasErrors)throw new InvalidOperationException(report.BuildValidationSummary());
        PrologueTutorialContentBuilder.Save(skill);

        EnemyData enemy=PrologueTutorialContentBuilder.Asset<EnemyData>(EnemyPath);
        enemy.EnemyId="prologue.white_entity.part";enemy.NameLocalizationKey="HTH_PROLOGUE_NAME_WHITE_ENTITY";
        enemy.EnemyName=Korean(enemy.NameLocalizationKey);enemy.Kind=EnemyKind.WhiteEntity;enemy.Rank=EnemyRank.Elite;
        enemy.BaseStats=new StatBlock { MaxHP=1100,MaxAP=0,ATK=22,DEF=4,SPD=9 };
        enemy.SkillUseChance=1;enemy.UseOrderedSkills=true;enemy.StrongSkillUseChance=0;enemy.TelegraphStrongSkill=false;
        enemy.SkillList=new List<SkillData>{skill};enemy.StrongSkillList=new List<SkillData>();
        enemy.EXPReward=0;enemy.GoldReward=0;enemy.Drops.Clear();enemy.DropItemIDs.Clear();
        enemy.Portrait=definition.GetRole("white_entity").Sprite;enemy.TurnOrderPortrait=enemy.Portrait;
        enemy.Prefab=BuildEnemyPrefab(enemy,definition.GetRole("white_entity").Sprite);
        PrologueTutorialContentBuilder.Save(enemy);definition.Enemy=enemy;
        definition.BattleScenario=BuildBattleScenario();
        GameContentCatalog catalog=PrologueTutorialContentBuilder.Require<GameContentCatalog>(PrologueTutorialContentBuilder.CatalogPath);
        Register(catalog.Items,item,value=>value.ItemID);Register(catalog.Skills,skill,value=>value.SkillID);Register(catalog.Enemies,enemy,value=>value.EnemyId);
        PrologueTutorialContentBuilder.Save(catalog);
    }

    private static GameObject BuildEnemyPrefab(EnemyData enemy,Sprite sprite)
    {
        const string path="Assets/_Game/Content/Characters/Prefabs/Enemy/Prologue_WhiteEntityPart.prefab";
        GameObject root=PrefabUtility.LoadPrefabContents("Assets/_Game/Content/Characters/Prefabs/Enemy/Enemy_Base.prefab");
        try {
            root.name="Prologue_WhiteEntityPart";
            foreach(OverworldEnemy component in root.GetComponentsInChildren<OverworldEnemy>(true)) Object.DestroyImmediate(component);
            foreach(DialogueBattleNPC component in root.GetComponentsInChildren<DialogueBattleNPC>(true)) Object.DestroyImmediate(component);
            root.GetComponent<EnemyCharacter>().Data=enemy;
            SpriteRenderer renderer=root.GetComponent<SpriteRenderer>();renderer.sprite=sprite;
            root.transform.localScale=Vector3.one*.18f;
            root.GetComponent<Animator>().runtimeAnimatorController=BuildEnemyAnimator(sprite);
            PrologueTutorialContentBuilder.Track(path);
            return PrefabUtility.SaveAsPrefabAsset(root,path);
        } finally {PrefabUtility.UnloadPrefabContents(root);}
    }

    private static AnimatorController BuildEnemyAnimator(Sprite sprite)
    {
        string path=Root+"/Visuals/WhiteEntity_Animator.controller";
        PrologueTutorialContentBuilder.EnsureFolder(Root+"/Visuals");
        PrologueTutorialContentBuilder.Track(path);
        AnimatorController controller=AssetDatabase.LoadAssetAtPath<AnimatorController>(path)
            ?? AnimatorController.CreateAnimatorControllerAtPath(path);
        string[] names={"BattleIdle","BattleReady","Attack","Hurt","Die","BattleMove","BattleMoveBack","Skill","Telegraph"};
        AnimatorStateMachine machine=controller.layers[0].stateMachine;
        foreach(string name in names) {
            bool hasParameter=false;
            foreach(AnimatorControllerParameter parameter in controller.parameters)
                if(parameter.name==name) {
                    if(hasParameter || parameter.type!=AnimatorControllerParameterType.Trigger)
                        throw new InvalidOperationException("백색체 Animator 파라미터 충돌: "+name);
                    hasParameter=true;
                }
            if(!hasParameter)controller.AddParameter(name,AnimatorControllerParameterType.Trigger);
            string clipPath=Root+"/Visuals/WhiteEntity_"+name+".anim";
            PrologueTutorialContentBuilder.Track(clipPath);
            AnimationClip clip=AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
            if(clip==null) {
                clip=new AnimationClip {name="WhiteEntity_"+name};
                AssetDatabase.CreateAsset(clip,clipPath);
            }
            AnimationUtility.SetObjectReferenceCurve(clip,EditorCurveBinding.PPtrCurve("",typeof(SpriteRenderer),"m_Sprite"),
                new[]{new ObjectReferenceKeyframe {time=0,value=sprite},new ObjectReferenceKeyframe {time=.75f,value=sprite}});
            PrologueTutorialContentBuilder.Save(clip);
            AnimatorState state=null;
            foreach(ChildAnimatorState candidate in machine.states)
                if(candidate.state.name==name) {
                    if(state!=null)throw new InvalidOperationException("백색체 Animator 상태 중복: "+name);
                    state=candidate.state;
                }
            if(state==null)state=machine.AddState(name);
            state.motion=clip;
            bool hasTransition=false;
            foreach(AnimatorStateTransition candidate in machine.anyStateTransitions)
                if(candidate.destinationState==state) {hasTransition=true;break;}
            if(!hasTransition) {
                AnimatorStateTransition transition=machine.AddAnyStateTransition(state);
                transition.hasExitTime=false;transition.duration=0;transition.canTransitionToSelf=true;
                transition.AddCondition(AnimatorConditionMode.If,0,name);
            }
        }
        machine.defaultState=machine.states[0].state;
        PrologueTutorialContentBuilder.Save(controller);return controller;
    }

    private static BattleScenarioData BuildBattleScenario()
    {
        ActionCatalogAsset catalog=PrologueTutorialContentBuilder.Require<ActionCatalogAsset>(ProductionActionLibraryBuildCommand.GeneratedAssetPath);
        string target=Root+"/Scenario/BattleScenario_WhiteEntity.asset";
        BattleScenarioData existing=AssetDatabase.LoadAssetAtPath<BattleScenarioData>(target);
        if(existing!=null) {
            string currentSource=File.ReadAllText(BattleSource,PrologueTutorialContentBuilder.StrictUtf8);
            if(existing.Source==null || existing.Source.SourceHash!=ScenarioSourceHash.Compute(currentSource))
                throw new InvalidOperationException("전투 원본이 달라졌습니다. 시퀀스 메이커에서 안전하게 다시 가져오세요: "+BattleSource);
            RequireValid(ScenarioCatalogValidator.ValidateBattleScenario(existing,catalog),BattleSource);
            PrologueTutorialContentBuilder.Track(target);return existing;
        }
        var resolver=new AssetDatabaseScenarioDialogueReferenceResolver(new[]{Root+"/Dialogue"});
        string source=File.ReadAllText(BattleSource,PrologueTutorialContentBuilder.StrictUtf8);
        ScenarioSourceSyncResult result=new ScenarioSourceImporter(new ScenarioSourceYamlParser(),resolver,resolver).Import(source,BattleSource);
        if(!result.Success || result.Scenario==null || result.Validation.HasErrors)throw new InvalidOperationException("백색체 시나리오 YAML 가져오기 실패");
        BattleScenarioData scenario=result.Scenario;
        RequireValid(ScenarioCatalogValidator.ValidateBattleScenario(scenario,catalog),BattleSource);
        PrologueTutorialContentBuilder.EnsureFolder(Root+"/Scenario");AssetDatabase.CreateAsset(scenario,target);
        foreach(ActionSequenceAsset sequence in scenario.Sequences) AssetDatabase.AddObjectToAsset(sequence,scenario);
        PrologueTutorialContentBuilder.Save(scenario);return scenario;
    }

    private static void BuildSequences(PrologueTutorialDefinition definition)
    {
        ActionCatalogAsset catalog=PrologueTutorialContentBuilder.Require<ActionCatalogAsset>(ProductionActionLibraryBuildCommand.GeneratedAssetPath);
        definition.Sequences.Clear();
        foreach(string id in new[]{"incident","rescue","seal","forward","control","derail"}) {
            string sourcePath=SequenceSourceRoot+id+".sequence.yaml";
            string target=Root+"/Scenario/Sequence_"+id+".asset";
            ActionSequenceAsset sequence=AssetDatabase.LoadAssetAtPath<ActionSequenceAsset>(target);
            string source=File.ReadAllText(sourcePath,PrologueTutorialContentBuilder.StrictUtf8);
            if(sequence==null) {
                ActionSequenceSourceImportResult result=ActionSequenceSourceSync.Import(source,sourcePath);
                if(!result.Success)throw new InvalidOperationException("시퀀스 YAML 가져오기 실패: "+sourcePath);
                sequence=result.Sequence;PrologueTutorialContentBuilder.EnsureFolder(Root+"/Scenario");AssetDatabase.CreateAsset(sequence,target);
            } else if(sequence.Source==null || sequence.Source.SourceHash!=ScenarioSourceHash.Compute(source))
                throw new InvalidOperationException("시퀀스 원본이 달라졌습니다. 시퀀스 메이커의 안전한 다시 가져오기를 사용하세요: "+sourcePath);
            RequireValid(ScenarioCatalogValidator.ValidateSequence(sequence,catalog),sourcePath);
            PrologueTutorialContentBuilder.Save(sequence);
            definition.Sequences.Add(new PrologueTutorialSequenceBinding {Id=id,Sequence=sequence});
        }
    }

    private static void Register<T>(List<T> list,T item,Func<T,string> id) where T:Object
    {foreach(T entry in list)if(entry!=null&&id(entry)==id(item)){if(entry!=item)throw new InvalidOperationException("콘텐츠 ID 충돌: "+id(item));return;}list.Add(item);}
    internal static Sprite FirstSprite(string path)
    {foreach(Object asset in AssetDatabase.LoadAllAssetsAtPath(path))if(asset is Sprite sprite)return sprite;throw new InvalidOperationException("Sprite 없음: "+path);}
    private static void RequireValid(ScenarioValidationResult validation,string source)
    {
        if(!validation.HasErrors)return;
        var text=new System.Text.StringBuilder(source);
        foreach(ScenarioValidationMessage message in validation.Messages)
            if(message.Severity==ScenarioValidationSeverity.Error)text.Append('\n').Append(message.Code).Append(": ").Append(message.Message);
        throw new InvalidOperationException(text.ToString());
    }
    private static void ValidateCatalogId<T>(IReadOnlyList<T> entries,string id,string expectedPath,Func<T,string> readId) where T:Object
    {
        if(entries==null)throw new InvalidOperationException("콘텐츠 카탈로그 목록이 없습니다: "+id);
        foreach(T entry in entries)
            if(entry!=null && readId(entry)==id && AssetDatabase.GetAssetPath(entry)!=expectedPath)
                throw new InvalidOperationException("다른 작성 데이터와 콘텐츠 ID가 충돌합니다: "+id);
    }
}
#endif
