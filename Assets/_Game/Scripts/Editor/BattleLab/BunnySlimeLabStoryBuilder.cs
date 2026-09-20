#if UNITY_EDITOR
using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>사용자가 Lab 생성 메뉴를 실행할 때만 원본 YAML과 대화를 새 시나리오 자산으로 연결합니다.</summary>
public static class BunnySlimeLabStoryBuilder
{
    public const string ContentRoot = "Assets/_Game/Content/Maps/Development/BunnySlimeBattleLab";
    public const string SourcePath = "Assets/_Game/Content/Scenarios/Source/Battle/BunnySlimeBattleLab/bunny_slime_lab.scenario.yaml";
    public const string ScenarioPath = ContentRoot + "/Data/Scenario/BattleScenario_BunnySlimeLab.asset";
    private const string ExpectedEnemyId = "lab.bunny_slime";
    private static readonly UTF8Encoding StrictUtf8 = new UTF8Encoding(false, true);

    public static BattleScenarioData Build(string root, EnemyData enemy)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("게임 재생을 종료한 뒤 시험장을 생성하세요.");
        if (!string.Equals((root ?? string.Empty).Replace('\\', '/').TrimEnd('/'), ContentRoot, StringComparison.Ordinal))
            throw new ArgumentException("버니슬라임 시험장의 지정 콘텐츠 폴더만 사용합니다.", nameof(root));
        if (enemy == null || enemy.EnemyId != ExpectedEnemyId)
            throw new ArgumentException("EnemyData의 EnemyId는 lab.bunny_slime이어야 합니다.", nameof(enemy));

        ActionCatalogAsset catalog = AssetDatabase.LoadAssetAtPath<ActionCatalogAsset>(
            ProductionActionLibraryBuildCommand.GeneratedAssetPath);
        if (catalog == null)
            throw new InvalidOperationException("공식 ActionLibrary.asset이 없습니다. 시나리오 라이브러리 생성 상태를 확인하세요.");

        string sourceText = File.ReadAllText(ScenarioSourcePathPolicy.RequireAbsolute(SourcePath), StrictUtf8);
        ScenarioSourceParseResult parsed = new ScenarioSourceYamlParser().Parse(sourceText, SourcePath);
        RequireValid(parsed.Validation, "시험장 YAML");
        if (!parsed.Success || parsed.Document == null || !parsed.Document.EnemyIds.Contains(ExpectedEnemyId))
            throw new InvalidOperationException("시험장 YAML의 버니슬라임 참가자 정의를 확인하세요.");

        BattleScenarioData existing = AssetDatabase.LoadAssetAtPath<BattleScenarioData>(ScenarioPath);
        if (existing != null)
        {
            RequireValid(ScenarioCatalogValidator.ValidateBattleScenario(existing, catalog), "기존 시험장 시나리오");
            if (existing.Source == null || existing.Source.SourceHash != ScenarioSourceHash.Compute(sourceText))
                Debug.LogWarning("[BunnySlimeLab] 수정된 기존 시나리오는 덮어쓰지 않았습니다. 시퀀스 메이커에서 YAML 동기화를 확인하세요.", existing);
            return existing;
        }
        RequireUnusedPath(ScenarioPath);

        string folder = ContentRoot + "/Data/Scenario";
        EnsureFolder(folder);
        SpeakerData wizzel = GetOrCreate<SpeakerData>(folder + "/Speaker_BunnyLab_Wizzel.asset", speaker =>
        {
            speaker.SpeakerID = "lab.bunny_slime.speaker.wizzel";
            speaker.DisplayName = "위젤";
            Sprite normal = AssetDatabase.LoadAssetAtPath<Sprite>(
                "Assets/_Game/Content/Art/Characters/Player/Wizzel/대화얼굴/wizzel_normal.png");
            Sprite happy = AssetDatabase.LoadAssetAtPath<Sprite>(
                "Assets/_Game/Content/Art/Characters/Player/Wizzel/대화얼굴/wizzel_happy.png");
            if (normal != null) speaker.Portraits[EmotionType.Normal] = normal;
            if (happy != null) speaker.Portraits[EmotionType.Happy] = happy;
            ConfigureVoice(speaker);
        });
        SpeakerData bunny = GetOrCreate<SpeakerData>(folder + "/Speaker_BunnyLab_Bunny.asset", speaker =>
        {
            speaker.SpeakerID = "lab.bunny_slime.speaker.examiner";
            speaker.DisplayName = "버니슬라임 시험관";
            if (enemy.Portrait != null) speaker.Portraits[EmotionType.Normal] = enemy.Portrait;
            ConfigureVoice(speaker);
            speaker.VoicePitch = 1.2f;
        });

        CreateDialogue(folder, "OpeningA", "opening_a",
            Line(bunny, "말랑한 공방 안전시험에 오신 걸 환영합니다. 안전모는요?"),
            Line(wizzel, "모자는 있는데, 안전한지는 모르겠네요."),
            Line(bunny, "그럼 몸으로 확인하겠습니다."),
            Line(wizzel, "안전시험 맞죠?"));
        CreateDialogue(folder, "OpeningB", "opening_b",
            Line(wizzel, "잠깐, 방금은 하나도 안 아팠는데요?"),
            Line(bunny, "시범용 고무 도장이거든요. 이제 진짜로 갑니다."),
            Line(bunny, "첫 시험은 가드입니다. 누르고 버티거나, 닿기 직전에 정확히 눌러 보세요."),
            Line(wizzel, "설명 끝나기 전에 치시면 실격입니다. 시험관이요."));
        CreateDialogue(folder, "Phase2", "phase2",
            Line(bunny, "가드 항목 합격! 다음은 미끄럼 주의 구간입니다."),
            Line(wizzel, "바닥에 뭘 뿌리신 거예요?"),
            Line(bunny, "제 일부요. 가드 불가 표시가 보이면 회피해 주세요."),
            Line(wizzel, "청소도 시험에 들어가나요?"));
        CreateDialogue(folder, "Phase3", "phase3",
            Line(bunny, "최종 항목, 대응 안전수칙! 아주 크게 부풀어 보겠습니다."),
            Line(wizzel, "그건 그냥 위험수칙 아닌가요?"),
            Line(bunny, "반격 표시가 뜨면 C를 누르세요. 공격받는 분이 앞으로 나와 받아치는 겁니다."),
            Line(bunny, "불안하면 회피해도 됩니다. 가드로 받는 건 추천이 아니라 금지입니다."),
            Line(wizzel, "다들 자리 지켜요. 제가 앞에서 받아칠게요."));
        CreateDialogue(folder, "Ending", "ending",
            Line(bunny, "합격입니다! 마지막 도장은 조금 납작하게 찍혔네요."),
            Line(wizzel, "시험관님이 도장이 된 것 같은데요."),
            Line(bunny, "복원력도 인증받았습니다. 잠깐만 말리면 됩니다."),
            Line(wizzel, "좋아요. 다음 시험은 서류로 합시다.", EmotionType.Happy));

        var resolver = new AssetDatabaseScenarioDialogueReferenceResolver(new[] { folder, "Assets/_Game/Content/Audio" });
        ScenarioSourceSyncResult imported = new ScenarioSourceImporter(
            new ScenarioSourceYamlParser(), resolver, resolver).Import(sourceText, SourcePath);
        BattleScenarioData scenario = imported.Scenario;
        bool created = false;
        try
        {
            RequireValid(imported.Validation, "시험장 YAML 가져오기");
            if (scenario == null)
                throw new InvalidOperationException("시험장 YAML에서 시나리오를 생성하지 못했습니다.");
            RequireValid(ScenarioCatalogValidator.ValidateBattleScenario(scenario, catalog), "시험장 Action Library 검사");
            RequireUnusedPath(ScenarioPath);
            scenario.name = "BattleScenario_BunnySlimeLab";
            AssetDatabase.CreateAsset(scenario, ScenarioPath);
            created = AssetDatabase.LoadAssetAtPath<BattleScenarioData>(ScenarioPath) == scenario;
            if (!created)
                throw new IOException("시험장 시나리오를 저장하지 못했습니다: " + ScenarioPath);

            for (int i = 0; i < scenario.Sequences.Count; i++)
            {
                ActionSequenceAsset sequence = scenario.Sequences[i];
                sequence.name = sequence.SequenceId;
                AssetDatabase.AddObjectToAsset(sequence, scenario);
                EditorUtility.SetDirty(sequence);
            }
            EditorUtility.SetDirty(scenario);
            AssetDatabase.SaveAssetIfDirty(scenario);
            for (int i = 0; i < scenario.Sequences.Count; i++)
            {
                if (!EditorUtility.IsPersistent(scenario.Sequences[i])
                    || AssetDatabase.GetAssetPath(scenario.Sequences[i]) != ScenarioPath)
                    throw new IOException("시험장 시퀀스의 하위 자산 저장을 확인하지 못했습니다.");
            }
            return scenario;
        }
        catch
        {
            // 이 호출에서 새로 생성한 정확한 시나리오만 회수합니다. 기존 대화/화자 자산은 유지합니다.
            if (created && AssetDatabase.LoadAssetAtPath<BattleScenarioData>(ScenarioPath) == scenario)
                AssetDatabase.DeleteAsset(ScenarioPath);
            throw;
        }
        finally
        {
            DestroyTransientScenario(scenario);
        }
    }

    private static DialogueNode Line(SpeakerData speaker, string text, EmotionType emotion = EmotionType.Normal)
    {
        return new DialogueNode { Speaker = speaker, DefaultText = text, Emotion = emotion };
    }

    private static void CreateDialogue(string folder, string suffix, string id, params DialogueNode[] lines)
    {
        GetOrCreate<DialogueData>(folder + "/Dialogue_BunnyLab_" + suffix + ".asset", dialogue =>
        {
            dialogue.Style = DialogueStyle.Cinematic;
            for (int i = 0; i < lines.Length; i++)
            {
                lines[i].LocalizationKey = "lab.bunny_slime." + id + "." + i.ToString("00");
                dialogue.Nodes.Add(lines[i]);
            }
        });
    }

    private static void ConfigureVoice(SpeakerData speaker)
    {
        speaker.VoiceBlipSound = AssetDatabase.LoadAssetAtPath<AudioClip>(
            "Assets/_Game/Content/Audio/CUSTOM/SFX/DefaultVoice.wav");
    }

    private static T GetOrCreate<T>(string path, Action<T> initialize) where T : ScriptableObject
    {
        T existing = AssetDatabase.LoadAssetAtPath<T>(path);
        if (existing != null) return existing;
        RequireUnusedPath(path);
        T asset = ScriptableObject.CreateInstance<T>();
        try
        {
            asset.name = Path.GetFileNameWithoutExtension(path);
            initialize(asset);
            AssetDatabase.CreateAsset(asset, path);
            if (!EditorUtility.IsPersistent(asset))
                throw new IOException("시험장 자산 생성에 실패했습니다: " + path);
            AssetDatabase.SaveAssetIfDirty(asset);
            return asset;
        }
        catch
        {
            if (asset != null && !EditorUtility.IsPersistent(asset)) Object.DestroyImmediate(asset);
            throw;
        }
    }

    private static void RequireUnusedPath(string path)
    {
        string absolute = Path.GetFullPath(path);
        if (File.Exists(absolute) || File.Exists(absolute + ".meta")
            || !string.IsNullOrEmpty(AssetDatabase.AssetPathToGUID(path)))
            throw new IOException("기존 파일은 덮어쓰지 않습니다. 해당 경로를 확인하세요: " + path);
    }

    private static void EnsureFolder(string path)
    {
        string[] parts = path.Split('/');
        string current = parts[0];
        for (int i = 1; i < parts.Length; i++)
        {
            string next = current + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(next))
            {
                string guid = AssetDatabase.CreateFolder(current, parts[i]);
                if (string.IsNullOrEmpty(guid))
                    throw new IOException("시험장 폴더 생성에 실패했습니다: " + next);
            }
            current = next;
        }
    }

    private static void RequireValid(ScenarioValidationResult validation, string stage)
    {
        if (validation == null || !validation.HasErrors) return;
        var message = new StringBuilder(stage + "에 오류가 있습니다.");
        for (int i = 0; i < validation.Messages.Count; i++)
        {
            ScenarioValidationMessage issue = validation.Messages[i];
            if (issue.Severity == ScenarioValidationSeverity.Error)
                message.Append("\n").Append(issue.Code).Append(": ").Append(issue.Message);
        }
        throw new InvalidOperationException(message.ToString());
    }

    private static void DestroyTransientScenario(BattleScenarioData scenario)
    {
        if (scenario == null || EditorUtility.IsPersistent(scenario)) return;
        for (int i = 0; i < scenario.Sequences.Count; i++)
        {
            ActionSequenceAsset sequence = scenario.Sequences[i];
            if (sequence != null && !EditorUtility.IsPersistent(sequence))
                Object.DestroyImmediate(sequence);
        }
        Object.DestroyImmediate(scenario);
    }
}
#endif
