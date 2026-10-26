#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

/// <summary>기존 프롤로그의 창가·기관사·자동문만 수정하는 명시 실행 명령입니다.</summary>
public static class PrologueTutorialOpeningPatch
{
    /// <summary>저장된 clean Scene 구성만 잠시 비웠다가 복원합니다. Scene 파일은 저장하지 않습니다.</summary>
    public static void ApplyInEditor() => WithClosedSceneSetup(Apply);

    internal static void WithClosedSceneSetup(Action patch)
    {
        if (patch == null) throw new ArgumentNullException(nameof(patch));
        if (Application.isBatchMode)
            throw new InvalidOperationException("현재 Editor 실행에는 ApplyInEditor, 별도 batch 실행에는 ApplyBatch를 사용하세요.");
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating)
            throw new InvalidOperationException("Play 전환·컴파일·에셋 갱신 중에는 프롤로그 패치를 실행하지 않습니다.");
        if (PrefabStageUtility.GetCurrentPrefabStage() != null)
            throw new InvalidOperationException("열린 Prefab Stage는 변경하지 않습니다.");
        SceneSetup[] setup = EditorSceneManager.GetSceneManagerSetup();
        if (setup == null || setup.Length == 0)
            throw new InvalidOperationException("복원할 Scene 구성이 없습니다.");
        bool hasActiveLoadedScene = false;
        foreach (SceneSetup entry in setup)
        {
            if (string.IsNullOrWhiteSpace(entry.path) || !File.Exists(entry.path)
                || AssetDatabase.LoadAssetAtPath<SceneAsset>(entry.path) == null)
                throw new InvalidOperationException("저장 경로가 없는 Scene은 닫지 않습니다: " + entry.path);
            hasActiveLoadedScene |= entry.isLoaded && entry.isActive;
        }
        if (!hasActiveLoadedScene)
            throw new InvalidOperationException("복원할 활성 Scene을 확인하지 못했습니다.");
        for (int i = 0; i < SceneManager.sceneCount; i++)
            if (SceneManager.GetSceneAt(i).isDirty)
                throw new InvalidOperationException("저장되지 않은 Scene 편집을 닫지 않습니다: " + SceneManager.GetSceneAt(i).path);

        Exception patchFailure = null;
        try
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            patch();
        }
        catch (Exception exception)
        {
            patchFailure = exception;
            throw;
        }
        finally
        {
            try { EditorSceneManager.RestoreSceneManagerSetup(setup); }
            catch (Exception restoreFailure)
            {
                if (patchFailure == null) throw;
                Debug.LogError("[PrologueTutorialOpeningPatch] 원래 Scene 구성 복원 실패: " + restoreFailure.Message);
            }
        }
    }

    public static void ApplyBatch()
    {
        if (!Application.isBatchMode)
            throw new InvalidOperationException("ApplyBatch는 닫힌 편집기의 명시적인 Unity batch 실행 전용입니다.");
        for (int i = 0; i < SceneManager.sceneCount; i++)
            if (SceneManager.GetSceneAt(i).isDirty)
                throw new InvalidOperationException("수정 중인 Scene이 있어 패치를 중단합니다.");
        if (PrefabStageUtility.GetCurrentPrefabStage() != null)
            throw new InvalidOperationException("열린 Prefab Stage는 변경하지 않습니다.");
        bool targetRestored = false;
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            string path = SceneManager.GetSceneAt(i).path;
            targetRestored |= path == PrologueTutorialContentBuilder.RegionScene || path == PrologueTutorialContentBuilder.IntroScene;
        }
        if (targetRestored) EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        Apply();
    }

    // No import hook or menu: this command never invokes the complete content builder.
    public static void Apply()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("프롤로그 패치는 Edit Mode에서만 실행합니다.");
        PrologueTutorialContentBuilder.RequireClosedCleanTargets();
        if (!File.Exists(PrologueTutorialContentBuilder.ManifestPath))
            throw new InvalidOperationException("기존 생성 기록이 없으므로 좁은 패치를 적용할 수 없습니다.");
        List<string> previous = PrologueTutorialContentBuilder.ValidatePreviousManifest();
        RequireNoDirtyAssets(previous);
        PrologueTutorialAssetFactory.ValidateLocalization();
        PrologueTutorialAssetFactory.ValidateWindowSource();
        PrologueTutorialDefinition definition = PrologueTutorialContentBuilder.Require<PrologueTutorialDefinition>(PrologueTutorialContentBuilder.DefinitionPath);
        List<string> roomPaths = ValidateTargets(definition);

        // All reads, ownership checks and room structure checks precede the first write.
        PrologueTutorialContentBuilder.WrittenPaths.Clear();
        PrologueTutorialContentBuilder.WrittenPaths.AddRange(previous);
        Exception patchFailure = null;
        try
        {
            PrologueTutorialAssetFactory.BuildConductorRole(definition);
            PrologueTutorialAssetFactory.BuildOpeningDialogues(definition);
            PrologueTutorialAssetFactory.RestoreWindowPage(definition.OpeningStory.Pages[0], definition.GetDialogue("window"));
            PrologueTutorialContentBuilder.Save(definition.OpeningStory);
            definition.Introduction = new PrologueTutorialIntroduction();
            foreach (string path in roomPaths) PatchRoom(path, definition);
            PrologueTutorialContentBuilder.Save(definition);
        }
        catch (Exception exception)
        {
            patchFailure = exception;
            throw;
        }
        finally
        {
            // Preserve every validated prior file, and only this command's proven partial writes.
            try { PrologueTutorialContentBuilder.WriteManifest(); }
            catch (Exception manifestFailure)
            {
                if (patchFailure == null) throw;
                Debug.LogError("[PrologueTutorialOpeningPatch] 부분 수정 기록 저장 실패: " + manifestFailure.Message);
            }
        }
        Debug.Log("[PrologueTutorialOpeningPatch] 창가 대사·기관사 도입·객차 자동문 연결 완료");
    }

    internal static void RequireNoDirtyAssets(List<string> paths)
    {
        foreach (string path in paths)
        {
            // Scene contents cannot be read through the asset/sub-asset loader.
            // Callers guard open/dirty scenes separately; the manifest guards their disk bytes.
            if (path.EndsWith(".unity", StringComparison.OrdinalIgnoreCase)) continue;
            foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(path))
                if (asset != null && EditorUtility.IsDirty(asset))
                {
                    if (asset is IllustratedStoryAsset story && IsOnlyOpeningFrameCacheDirty(story, path)) continue;
                    throw new InvalidOperationException("저장되지 않은 자산 편집을 덮어쓰지 않습니다: " + path);
                }
        }
    }

    private static bool IsOnlyOpeningFrameCacheDirty(IllustratedStoryAsset live, string path)
    {
        // This sole exception is already manifest-hash-verified and will be saved by the opening patch itself.
        if (path != PrologueTutorialContentBuilder.Root + "/Story_TrainOpening.asset"
            || !PrologueTutorialContentBuilder.IsOwnedPath(path) || AssetDatabase.GetAssetPath(live) != path)
            return false;
        Object[] detached = null;
        try
        {
            detached = InternalEditorUtility.LoadSerializedFileAndForget(path);
            if (detached == null || detached.Length != 1 || !(detached[0] is IllustratedStoryAsset disk)
                || disk == live || AssetDatabase.Contains(disk) || !live.TryValidate(out _) || !disk.TryValidate(out _)
                || live.Pages.Count != disk.Pages.Count)
                return false;

            var liveSerialized = new SerializedObject(live);
            var diskSerialized = new SerializedObject(disk);
            SerializedProperty livePages = liveSerialized.FindProperty("Pages");
            SerializedProperty diskPages = diskSerialized.FindProperty("Pages");
            if (livePages == null || diskPages == null || livePages.arraySize != diskPages.arraySize) return false;
            bool pointerChanged = false;
            for (int i = 0; i < live.Pages.Count; i++)
            {
                IllustratedStoryPage livePage = live.Pages[i];
                IllustratedStoryPage diskPage = disk.Pages[i];
                if (livePage.Animation != diskPage.Animation || !HaveIdenticalFrames(livePage.Frames, diskPage.Frames)) return false;
                SerializedProperty livePointer = livePages.GetArrayElementAtIndex(i).FindPropertyRelative("_framesSourceClip");
                SerializedProperty diskPointer = diskPages.GetArrayElementAtIndex(i).FindPropertyRelative("_framesSourceClip");
                if (livePointer == null || diskPointer == null || livePointer.objectReferenceValue != livePage.Animation) return false;
                pointerChanged |= diskPointer.objectReferenceValue != livePointer.objectReferenceValue;
                // Only the detached copy is normalized. The live asset and its dirty flag stay untouched.
                diskPointer.objectReferenceValue = diskPage.Animation;
            }
            if (!pointerChanged) return false;
            diskSerialized.ApplyModifiedPropertiesWithoutUndo();
            return string.Equals(EditorJsonUtility.ToJson(live), EditorJsonUtility.ToJson(disk), StringComparison.Ordinal);
        }
        catch (Exception exception)
        {
            Debug.LogWarning("[PrologueTutorialOpeningPatch] 창가 캐시 동등성을 확인하지 못했습니다: " + exception.Message);
            return false;
        }
        finally
        {
            if (detached != null)
                foreach (Object copy in detached)
                    if (copy != null && !AssetDatabase.Contains(copy)) Object.DestroyImmediate(copy);
        }
    }

    internal static bool HaveIdenticalFrames(IllustratedStoryFrame[] left, IllustratedStoryFrame[] right)
    {
        if (ReferenceEquals(left, right)) return true;
        if (left == null || right == null || left.Length != right.Length) return false;
        for (int i = 0; i < left.Length; i++)
            if (left[i] == null || right[i] == null || left[i].Sprite != right[i].Sprite || !left[i].Time.Equals(right[i].Time))
                return false;
        return true;
    }

    private static List<string> ValidateTargets(PrologueTutorialDefinition definition)
    {
        if (definition.Roles == null || definition.Dialogues == null || definition.Rooms == null)
            throw new InvalidOperationException("프롤로그 역할·대사·방 목록이 없습니다.");
        var roleIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (PrologueTutorialRole role in definition.Roles)
            if (role == null || string.IsNullOrWhiteSpace(role.RoleId) || !roleIds.Add(role.RoleId))
                throw new InvalidOperationException("프롤로그 역할 ID가 없거나 중복됩니다.");
        var dialogueIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (PrologueTutorialDialogueBinding binding in definition.Dialogues)
            if (binding == null || string.IsNullOrWhiteSpace(binding.Id) || !dialogueIds.Add(binding.Id))
                throw new InvalidOperationException("프롤로그 대사 ID가 없거나 중복됩니다.");
        foreach (string roleId in new[] { "protagonist", "crew" })
        {
            PrologueTutorialRole role = definition.GetRole(roleId);
            if (role?.Speaker == null || role.VisualPrefab == null || role.Sprite == null)
                throw new InvalidOperationException("창가/기관사 원본 역할이 유효하지 않습니다: " + roleId);
        }
        if (LayerMask.NameToLayer("Interactable") < 0)
            throw new InvalidOperationException("Interactable 레이어가 없습니다.");
        IllustratedStoryAsset opening = definition.OpeningStory;
        if (opening == null || AssetDatabase.GetAssetPath(opening) != PrologueTutorialContentBuilder.Root + "/Story_TrainOpening.asset"
            || opening.Pages == null || opening.Pages.Count != 1 || !opening.TryValidate(out _)
            || opening.Pages[0].Animation == null || !opening.Pages[0].LoopAnimation)
            throw new InvalidOperationException("기존 창가 한 페이지/루프/프레임 연결을 확인하지 못했습니다.");
        foreach (string key in new[] { "HTH_PROLOGUE_NAME_CONDUCTOR", "HTH_PROLOGUE_WINDOW_01", "HTH_PROLOGUE_WINDOW_02",
            "HTH_PROLOGUE_WINDOW_03", "HTH_PROLOGUE_DLG_INTRODUCTION_01", "HTH_PROLOGUE_DLG_INTRODUCTION_02",
            "HTH_PROLOGUE_DLG_INTRODUCTION_03", "HTH_PROLOGUE_DLG_INTRODUCTION_04", "HTH_PROLOGUE_DLG_INTRODUCTION_05",
            "HTH_PROLOGUE_DLG_OPTIONAL_CONDUCTOR_01" }) PrologueTutorialAssetFactory.Korean(key);
        RequireExistingType<SpeakerData>(PrologueTutorialContentBuilder.Root + "/Speakers/Speaker_conductor.asset");
        RequireExistingType<GameObject>(PrologueTutorialContentBuilder.RegionRoot + "/Prefabs/NPCs/Prologue_conductor.prefab");
        foreach (string id in new[] { "window", "introduction", "optional_conductor" })
            RequireExistingType<DialogueData>(PrologueTutorialContentBuilder.Root + "/Dialogue/Dialogue_" + id + ".asset");

        var paths = new List<string>();
        var uniquePaths = new HashSet<string>(StringComparer.Ordinal);
        foreach (PrologueTutorialRoom kind in Enum.GetValues(typeof(PrologueTutorialRoom)))
        {
            RoomDefinition room = definition.GetRoom(kind);
            if (room?.RoomPrefab == null) throw new InvalidOperationException("객차 참조가 없습니다: " + kind);
            string path = AssetDatabase.GetAssetPath(room.RoomPrefab);
            if (!PrologueTutorialContentBuilder.IsOwnedPath(path) || !uniquePaths.Add(path))
                throw new InvalidOperationException("중복되거나 소유 범위 밖인 객차입니다: " + path);
            if ((kind == PrologueTutorialRoom.Repair) != (path == PrologueTutorialContentBuilder.OriginalRoom))
                throw new InvalidOperationException("수리 객차 원본 연결이 다릅니다: " + path);
            GameObject contents = PrefabUtility.LoadPrefabContents(path);
            try
            {
                if (kind == PrologueTutorialRoom.Repair) FindConductorTarget(contents);
                foreach (PrologueTutorialDoor door in contents.GetComponentsInChildren<PrologueTutorialDoor>(true))
                    if (new SerializedObject(door).FindProperty("_enterOnTrigger") == null)
                        throw new InvalidOperationException("자동문 연결 필드가 없습니다: " + path);
            }
            finally { PrefabUtility.UnloadPrefabContents(contents); }
            paths.Add(path);
        }
        return paths;
    }

    private static void RequireExistingType<T>(string path) where T : Object
    {
        if (File.Exists(path)) PrologueTutorialContentBuilder.Require<T>(path);
    }

    private static PrologueTutorialRoleAnchor FindConductorTarget(GameObject root)
    {
        PrologueTutorialRoleAnchor result = null;
        foreach (PrologueTutorialRoleAnchor anchor in root.GetComponentsInChildren<PrologueTutorialRoleAnchor>(true))
        {
            if (anchor.RoleId != "crew" && anchor.RoleId != "conductor") continue;
            if (result != null || anchor.GetComponent<PrologueTutorialInteractable>() == null
                || anchor.GetComponent<BoxCollider2D>() == null || anchor.transform.parent == null)
                throw new InvalidOperationException("Q0 기관사 교체 대상은 상호작용 가능한 단일 승무원이어야 합니다.");
            result = anchor;
        }
        return result != null ? result : throw new InvalidOperationException("Q0의 승무원/기관사 인스턴스가 없습니다.");
    }

    private static void PatchRoom(string path, PrologueTutorialDefinition definition)
    {
        GameObject contents = PrefabUtility.LoadPrefabContents(path);
        try
        {
            bool changed = false;
            if (path == PrologueTutorialContentBuilder.OriginalRoom)
            {
                PrologueTutorialRoleAnchor previous = FindConductorTarget(contents);
                if (previous.RoleId == "crew")
                {
                    GameObject replacement = PrefabUtility.InstantiatePrefab(definition.GetRole("conductor").VisualPrefab, contents.scene) as GameObject;
                    if (replacement == null) throw new InvalidOperationException("기관사 인스턴스 생성 실패");
                    Transform oldTransform = previous.transform;
                    replacement.transform.SetParent(oldTransform.parent, false);
                    replacement.transform.SetSiblingIndex(oldTransform.GetSiblingIndex());
                    replacement.transform.localPosition = oldTransform.localPosition;
                    replacement.transform.localRotation = oldTransform.localRotation;
                    replacement.transform.localScale = oldTransform.localScale;
                    replacement.layer = previous.gameObject.layer;
                    replacement.SetActive(previous.gameObject.activeSelf);
                    EditorUtility.CopySerialized(previous.GetComponent<BoxCollider2D>(), replacement.AddComponent<BoxCollider2D>());
                    replacement.AddComponent<PrologueTutorialInteractable>().Configure(PrologueTutorialInteraction.OptionalDialogue, "optional_conductor");
                    Object.DestroyImmediate(previous.gameObject);
                    changed = true;
                }
            }
            foreach (PrologueTutorialDoor door in contents.GetComponentsInChildren<PrologueTutorialDoor>(true))
            {
                if (new SerializedObject(door).FindProperty("_enterOnTrigger").boolValue) continue;
                PrologueTutorialContentBuilder.Set(door, "_enterOnTrigger", property => property.boolValue = true);
                changed = true;
            }
            if (!changed) return;
            PrologueTutorialContentBuilder.Track(path);
            if (PrefabUtility.SaveAsPrefabAsset(contents, path) == null)
                throw new IOException("객차 좁은 패치 저장 실패: " + path);
        }
        finally { PrefabUtility.UnloadPrefabContents(contents); }
    }
}
#endif
