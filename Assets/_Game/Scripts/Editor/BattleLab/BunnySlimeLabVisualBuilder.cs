#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>원본 토끼 슬라임을 수정하지 않고 실습용 시각 자산을 생성합니다.</summary>
public static class BunnySlimeLabVisualBuilder
{
    public static GameObject Build(string root, EnemyData source, EnemyData sample)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("실습용 시각 자산은 Play Mode 밖에서 생성해야 합니다.");
        if (source == null || sample == null || source == sample || source.Prefab == null)
            throw new ArgumentException("별도의 원본/실습용 적 데이터와 원본 프리팹이 필요합니다.");

        root = NormalizeRoot(root);
        EnsureFolder(root + "/Prefabs");
        EnsureFolder(root + "/Animations");

        string prefabPath = root + "/Prefabs/BunnySlime_Showcase.prefab";
        GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        if (existing != null)
        {
            ValidateCombatPrefab(existing, sample);

            sample.Prefab = existing;
            EditorUtility.SetDirty(sample);
            return existing;
        }

        if (AssetDatabase.LoadMainAssetAtPath(prefabPath) != null)
            throw new InvalidOperationException("생성 위치에 다른 자산이 있습니다: " + prefabPath);

        string sourcePath = AssetDatabase.GetAssetPath(source.Prefab);
        if (string.IsNullOrEmpty(sourcePath) || !sourcePath.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("원본 적은 저장된 프리팹이어야 합니다.");

        GameObject clone = PrefabUtility.LoadPrefabContents(sourcePath);
        try
        {
            clone.name = "BunnySlime_Showcase";
            clone.tag = "Untagged";

            Animator animator = clone.GetComponent<Animator>();
            SpriteRenderer sprite = clone.GetComponent<SpriteRenderer>();
            EnemyCharacter passiveEnemy = clone.GetComponent<EnemyCharacter>();
            if (animator == null || sprite == null || passiveEnemy == null)
                throw new InvalidOperationException("원본 적 루트에 Animator, SpriteRenderer, EnemyCharacter가 필요합니다.");

            RuntimeAnimatorController sourceController = animator.runtimeAnimatorController;
            Vector3 baselineScale = clone.transform.localScale;
            ConfigureCombatComponents(clone, sample);

            // OverworldEnemy를 제거하면 DisableForBattleInstance도 호출되지 않으므로 여기서 물리를 해제합니다.
            Collider2D[] colliders = clone.GetComponentsInChildren<Collider2D>(true);
            for (int i = 0; i < colliders.Length; i++)
                colliders[i].enabled = false;
            Rigidbody2D body = clone.GetComponent<Rigidbody2D>();
            if (body != null)
            {
                body.linearVelocity = Vector2.zero;
                body.angularVelocity = 0f;
                body.simulated = false;
            }

            animator.runtimeAnimatorController = BuildController(
                root + "/Animations", sourceController, sprite.sprite, baselineScale);
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(clone, prefabPath);
            if (prefab == null)
                throw new InvalidOperationException("실습용 슬라임 프리팹 저장에 실패했습니다.");

            sample.Prefab = prefab;
            sample.ReturnMoveTrigger = "BattleMoveBack";
            EditorUtility.SetDirty(sample);
            return prefab;
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(clone);
        }
    }

    /// <summary>프리팹 편집용 복제본에서만 호출합니다. 의존 컴포넌트를 먼저 제거한 뒤 AI를 교체합니다.</summary>
    public static BunnySlimeShowcaseEnemy ConfigureCombatComponents(GameObject clone, EnemyData sample)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || clone == null || sample == null
            || EditorUtility.IsPersistent(clone))
            throw new ArgumentException("Edit Mode의 프리팹 편집 복제본과 샘플 데이터가 필요합니다.");

        EnemyCharacter[] existing = clone.GetComponents<EnemyCharacter>();
        int showcaseCount = 0;
        for (int i = 0; i < existing.Length; i++)
        {
            if (existing[i] is BunnySlimeShowcaseEnemy) showcaseCount++;
            else if (!(existing[i] is BunnySlimeCharacter) && existing[i].GetType() != typeof(EnemyCharacter))
                throw new InvalidOperationException("알 수 없는 적 AI는 교체하지 않습니다: " + existing[i].GetType().Name);
        }
        if (existing.Length == 0 || showcaseCount > 1)
            throw new InvalidOperationException("교체할 적 AI가 없거나 실습 AI가 중복되어 있습니다.");

        // OverworldEnemy에는 RequireComponent(EnemyCharacter)가 있습니다.
        // 역순으로 지우면 Unity가 삭제를 거절하고 수동 더미와 실습 AI가 함께 남습니다.
        OverworldEnemy[] overworld = clone.GetComponents<OverworldEnemy>();
        for (int i = 0; i < overworld.Length; i++)
            Object.DestroyImmediate(overworld[i]);
        for (int i = 0; i < existing.Length; i++)
            if (!(existing[i] is BunnySlimeShowcaseEnemy))
                Object.DestroyImmediate(existing[i]);

        // DestroyImmediate는 의존성 오류를 예외 대신 로그로 알리므로 실제 제거 여부를 검사합니다.
        EnemyCharacter[] remaining = clone.GetComponents<EnemyCharacter>();
        for (int i = 0; i < remaining.Length; i++)
            if (!(remaining[i] is BunnySlimeShowcaseEnemy))
                throw new InvalidOperationException("기존 AI를 제거하지 못했습니다. 중복 적 프리팹은 저장하지 않습니다.");

        BunnySlimeShowcaseEnemy enemy = clone.GetComponent<BunnySlimeShowcaseEnemy>();
        if (enemy == null)
        {
            enemy = clone.AddComponent<BunnySlimeShowcaseEnemy>();
            enemy.UseHealthPhases = true;
            enemy.Phase2StartIndex = 5;
            enemy.Phase3StartIndex = 9;
        }
        enemy.Data = sample;
        ValidateCombatPrefab(clone, sample);
        return enemy;
    }

    public static void ValidateCombatPrefab(GameObject prefab, EnemyData sample)
    {
        EnemyCharacter[] enemies = prefab != null ? prefab.GetComponents<EnemyCharacter>() : null;
        if (enemies == null || enemies.Length != 1 || !(enemies[0] is BunnySlimeShowcaseEnemy)
            || enemies[0].Data != sample || prefab.GetComponent<OverworldEnemy>() != null)
            throw new InvalidOperationException("토끼 실습 프리팹에는 샘플 데이터와 연결된 BunnySlimeShowcaseEnemy 하나만 있어야 합니다.");
    }

    private static AnimatorController BuildController(
        string folder, RuntimeAnimatorController source, Sprite fallback, Vector3 scale)
    {
        string path = folder + "/BunnySlime_Showcase.controller";
        AnimatorController existing = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
        if (existing != null)
            return existing;
        if (AssetDatabase.LoadMainAssetAtPath(path) != null)
            throw new InvalidOperationException("컨트롤러 생성 위치에 다른 자산이 있습니다: " + path);

        AnimationClip sourceIdle = ResolveIdle(source);
        AnimationClip idle = BuildPose(folder, "BattleIdle", sourceIdle, fallback, scale,
            Mathf.Max(0.5f, sourceIdle != null ? sourceIdle.length : 0.5f), 1f, 1f, true);
        AnimationClip attack = BuildPose(folder, "Attack", sourceIdle, fallback, scale, 0.3f, 1.22f, 0.78f, false);
        AnimationClip skill = BuildPose(folder, "Skill", sourceIdle, fallback, scale, 0.45f, 0.8f, 1.24f, false);
        AnimationClip move = BuildPose(folder, "BattleMove", sourceIdle, fallback, scale, 0.32f, 1.12f, 0.88f, false);
        AnimationClip back = BuildPose(folder, "BattleMoveBack", sourceIdle, fallback, scale, 0.32f, 0.9f, 1.1f, false);
        AnimationClip hurt = BuildPose(folder, "Hurt", sourceIdle, fallback, scale, 0.22f, 1.16f, 0.84f, false);
        AnimationClip telegraph = BuildPose(folder, "Telegraph", sourceIdle, fallback, scale, 0.6f, 1.3f, 0.7f, false);
        AnimationClip die = BuildPose(folder, "Die", sourceIdle, fallback, scale, 0.7f, 1.35f, 0.65f, false);
        AnimationClip labStrike = BuildTimedStrike(folder, "LabStrike", sourceIdle, fallback, scale, 1.05f, .85f, 1.28f, .72f);
        AnimationClip labCombo = BuildTimedStrike(folder, "LabComboStrike", sourceIdle, fallback, scale, .85f, .65f, 1.24f, .76f);
        AnimationClip labBurst = BuildTimedStrike(folder, "LabBurst", sourceIdle, fallback, scale, 1.05f, .85f, 1.4f, .64f);

        // 클립 생성 실패로 비어 있는 컨트롤러가 남지 않도록 모든 클립을 먼저 준비합니다.
        AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(path);
        AnimatorStateMachine machine = controller.layers[0].stateMachine;
        AnimatorState idleState = AddState(controller, machine, "BattleIdle", idle, 0);
        machine.defaultState = idleState;

        AddPose(controller, machine, idleState, "Attack", attack, 1);
        AddPose(controller, machine, idleState, "Skill", skill, 2);
        AddPose(controller, machine, idleState, "BattleMove", move, 3);
        AddPose(controller, machine, idleState, "BattleMoveBack", back, 4);
        AddPose(controller, machine, idleState, "Hurt", hurt, 5);
        AddPose(controller, machine, idleState, "Telegraph", telegraph, 6);
        AddPose(controller, machine, idleState, "Die", die, 7);
        AddPose(controller, machine, idleState, "LabStrike", labStrike, 8);
        AddPose(controller, machine, idleState, "LabComboStrike", labCombo, 9);
        AddPose(controller, machine, idleState, "LabBurst", labBurst, 10);
        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssetIfDirty(controller);
        return controller;
    }

    private static AnimationClip ResolveIdle(RuntimeAnimatorController source)
    {
        if (source == null) return null;
        AnimationClip[] clips = source.animationClips;
        for (int i = 0; i < clips.Length; i++)
        {
            if (clips[i] != null && clips[i].name.IndexOf("Idle", StringComparison.OrdinalIgnoreCase) >= 0)
                return clips[i];
        }
        return clips.Length > 0 ? clips[0] : null;
    }

    private static AnimatorState AddState(
        AnimatorController controller, AnimatorStateMachine machine, string name, AnimationClip clip, int index)
    {
        controller.AddParameter(name, AnimatorControllerParameterType.Trigger);
        AnimatorState state = machine.AddState(name, new Vector3(320f, 60f + 75f * index, 0f));
        state.motion = clip;
        state.writeDefaultValues = false;
        AnimatorStateTransition enter = machine.AddAnyStateTransition(state);
        enter.AddCondition(AnimatorConditionMode.If, 0f, name);
        enter.hasExitTime = false;
        enter.hasFixedDuration = true;
        enter.duration = 0f;
        enter.canTransitionToSelf = false;
        return state;
    }

    private static void AddPose(
        AnimatorController controller, AnimatorStateMachine machine, AnimatorState idle,
        string name, AnimationClip clip, int index)
    {
        AnimatorState state = AddState(controller, machine, name, clip, index);
        AnimatorStateTransition restore = state.AddTransition(idle);
        restore.hasExitTime = true;
        restore.exitTime = 1f;
        restore.hasFixedDuration = true;
        restore.duration = 0f;
    }

    private static AnimationClip BuildPose(
        string folder, string name, AnimationClip source, Sprite fallback, Vector3 scale,
        float duration, float width, float height, bool loop)
    {
        string path = folder + "/BunnySlime_" + name + ".anim";
        AnimationClip existing = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
        if (existing != null) return existing;
        if (AssetDatabase.LoadMainAssetAtPath(path) != null)
            throw new InvalidOperationException("애니메이션 생성 위치에 다른 자산이 있습니다: " + path);

        AnimationClip clip = new AnimationClip { name = "BunnySlime_" + name, frameRate = 30f };
        try
        {
            CopySpriteFrames(source, clip, fallback, duration);
            // 이동 서비스가 소유한 루트 위치/회전은 바인딩하지 않습니다. 마지막 키는 반드시 원래 스케일입니다.
            AddScaleCurve(clip, "m_LocalScale.x", scale.x, width, duration);
            AddScaleCurve(clip, "m_LocalScale.y", scale.y, height, duration);
            AddScaleCurve(clip, "m_LocalScale.z", scale.z, 1f, duration);
            AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = loop;
            AnimationUtility.SetAnimationClipSettings(clip, settings);
            AssetDatabase.CreateAsset(clip, path);
            return clip;
        }
        catch
        {
            if (!AssetDatabase.Contains(clip)) Object.DestroyImmediate(clip);
            throw;
        }
    }

    private static void AddScaleCurve(AnimationClip clip, string property, float baseline, float peak, float duration)
    {
        AnimationCurve curve = new AnimationCurve(
            new Keyframe(0f, baseline),
            new Keyframe(duration * 0.38f, baseline * peak),
            new Keyframe(duration * 0.72f, baseline * (2f - peak)),
            new Keyframe(duration, baseline));
        AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve("", typeof(Transform), property), curve);
    }

    private static AnimationClip BuildTimedStrike(
        string folder, string name, AnimationClip source, Sprite fallback, Vector3 scale,
        float duration, float impactTime, float width, float height)
    {
        string path = folder + "/BunnySlime_" + name + ".anim";
        AnimationClip existing = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
        if (existing != null) return existing;
        if (AssetDatabase.LoadMainAssetAtPath(path) != null)
            throw new InvalidOperationException("애니메이션 생성 위치에 다른 자산이 있습니다: " + path);

        AnimationClip clip = new AnimationClip { name = "BunnySlime_" + name, frameRate = 60f };
        try
        {
            CopySpriteFrames(source, clip, fallback, duration);
            AddTimedScaleCurve(clip, "m_LocalScale.x", scale.x, .92f, width, impactTime, duration);
            AddTimedScaleCurve(clip, "m_LocalScale.y", scale.y, 1.08f, height, impactTime, duration);
            AddTimedScaleCurve(clip, "m_LocalScale.z", scale.z, 1f, 1f, impactTime, duration);
            AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = false;
            AnimationUtility.SetAnimationClipSettings(clip, settings);
            AssetDatabase.CreateAsset(clip, path);
            return clip;
        }
        catch
        {
            if (!AssetDatabase.Contains(clip)) Object.DestroyImmediate(clip);
            throw;
        }
    }

    private static void AddTimedScaleCurve(
        AnimationClip clip, string property, float baseline, float windup, float impact, float impactTime, float duration)
    {
        AnimationCurve curve = new AnimationCurve(
            new Keyframe(0f, baseline),
            new Keyframe(impactTime * .35f, baseline * windup),
            new Keyframe(impactTime - .12f, baseline * windup),
            new Keyframe(impactTime, baseline * impact),
            new Keyframe(impactTime + .075f, baseline * (2f - impact)),
            new Keyframe(duration, baseline));
        AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve("", typeof(Transform), property), curve);
    }

    private static void CopySpriteFrames(AnimationClip source, AnimationClip target, Sprite fallback, float duration)
    {
        ObjectReferenceKeyframe[] frames = null;
        if (source != null)
        {
            EditorCurveBinding[] bindings = AnimationUtility.GetObjectReferenceCurveBindings(source);
            for (int i = 0; i < bindings.Length; i++)
            {
                if (bindings[i].type == typeof(SpriteRenderer) && bindings[i].propertyName == "m_Sprite")
                {
                    frames = AnimationUtility.GetObjectReferenceCurve(source, bindings[i]);
                    break;
                }
            }
        }

        if (frames == null || frames.Length == 0)
        {
            frames = new[] { new ObjectReferenceKeyframe { time = 0f, value = fallback } };
        }
        else
        {
            float sourceLength = Mathf.Max(0.001f, source.length);
            for (int i = 0; i < frames.Length; i++)
                frames[i].time = frames[i].time / sourceLength * duration;
        }
        AnimationUtility.SetObjectReferenceCurve(
            target, EditorCurveBinding.PPtrCurve("", typeof(SpriteRenderer), "m_Sprite"), frames);
    }

    private static string NormalizeRoot(string root)
    {
        root = (root ?? "").Replace('\\', '/').TrimEnd('/');
        const string allowed = "Assets/_Game/Content/Maps/Development/";
        if (!root.StartsWith(allowed, StringComparison.Ordinal) || root.Contains(".."))
            throw new ArgumentException("실습 자산은 Maps/Development 아래에만 생성할 수 있습니다.");
        return root;
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        int slash = path.LastIndexOf('/');
        if (slash <= 0) throw new ArgumentException("유효하지 않은 생성 폴더입니다: " + path);
        string parent = path.Substring(0, slash);
        EnsureFolder(parent);
        string guid = AssetDatabase.CreateFolder(parent, path.Substring(slash + 1));
        if (string.IsNullOrEmpty(guid))
            throw new InvalidOperationException("생성 폴더를 만들지 못했습니다: " + path);
    }
}
#endif
