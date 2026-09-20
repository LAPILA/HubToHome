using System;
using System.Collections.Generic;
using System.IO;
using HubToHome.EditorTools.ContentMaker;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace HubToHome.EditorTools.SkillMaker
{
    internal static class SkillMakerAssetUtility
    {
        public static List<SkillData> FindSkills()
        {
            var result = new List<SkillData>();
            if (!AssetDatabase.IsValidFolder(ContentMakerAssetUtility.ContentRoot)) return result;
            string[] guids = AssetDatabase.FindAssets("t:SkillData", new[] { ContentMakerAssetUtility.ContentRoot });
            foreach (string guid in guids)
            {
                SkillData skill = AssetDatabase.LoadAssetAtPath<SkillData>(AssetDatabase.GUIDToAssetPath(guid));
                if (skill != null) result.Add(skill);
            }
            result.Sort((left, right) =>
            {
                int order = StringComparer.OrdinalIgnoreCase.Compare(DisplayName(left), DisplayName(right));
                return order != 0 ? order : StringComparer.Ordinal.Compare(
                    AssetDatabase.GetAssetPath(left), AssetDatabase.GetAssetPath(right));
            });
            return result;
        }

        public static bool Matches(SkillData skill, string query, SkillUsageProfile? profile)
        {
            if (skill == null || (profile.HasValue && skill.UsageProfile != profile.Value)) return false;
            string search = (query ?? string.Empty).Trim();
            return search.Length == 0 || Contains(skill.SkillName, search) || Contains(skill.SkillID, search)
                || Contains(skill.name, search) || Contains(AssetDatabase.GetAssetPath(skill), search);
        }

        public static SkillData CreateAtPath(string path, Action<SkillData> initialize = null)
        {
            EnsureEditMode();
            string targetPath = PrepareNewPath(path);
            SkillData skill = ScriptableObject.CreateInstance<SkillData>();
            try
            {
                skill.name = Path.GetFileNameWithoutExtension(targetPath);
                skill.SkillName = skill.name;
                skill.SkillID = CreateUniqueSkillId(skill.name);
                // 템플릿도 새 자산의 첫 저장 전에 구성합니다. 선택한 기존 스킬은 변경하지 않습니다.
                initialize?.Invoke(skill);
                AssetDatabase.CreateAsset(skill, targetPath);
                if (!EditorUtility.IsPersistent(skill)) throw new IOException("스킬 자산을 생성하지 못했습니다: " + targetPath);
                AssetDatabase.SaveAssetIfDirty(skill);
                return skill;
            }
            catch
            {
                if (skill != null && !EditorUtility.IsPersistent(skill)) Object.DestroyImmediate(skill);
                throw;
            }
        }

        public static SkillData DuplicateAtPath(SkillData source, string path)
        {
            EnsureEditMode();
            string sourcePath = ExistingSkillPath(source);
            if (EditorUtility.IsDirty(source))
                throw new InvalidOperationException("수정한 원본 스킬을 먼저 저장한 뒤 복제해 주세요.");
            string targetPath = PrepareNewPath(path);
            string uniqueId = CreateUniqueSkillId(Path.GetFileNameWithoutExtension(targetPath));
            if (!AssetDatabase.CopyAsset(sourcePath, targetPath))
                throw new IOException("스킬을 복제하지 못했습니다: " + targetPath);

            SkillData copy = AssetDatabase.LoadAssetAtPath<SkillData>(targetPath);
            if (copy == null) throw new IOException("복사된 파일을 스킬로 읽지 못했습니다. 파일을 확인해 주세요: " + targetPath);
            copy.name = Path.GetFileNameWithoutExtension(targetPath);
            copy.SkillID = uniqueId;
            EditorUtility.SetDirty(copy);
            AssetDatabase.SaveAssetIfDirty(copy);
            return copy;
        }

        public static bool CanEdit(SkillData skill, out string reason)
        {
            try
            {
                EnsureEditMode();
                string path = ExistingSkillPath(skill);
                if ((File.GetAttributes(AbsolutePath(path)) & FileAttributes.ReadOnly) != 0
                    || !AssetDatabase.IsOpenForEdit(skill, StatusQueryOptions.UseCachedIfPossible))
                    throw new InvalidOperationException("읽기 전용이거나 버전 관리에서 편집이 잠긴 스킬입니다.");
                reason = string.Empty;
                return true;
            }
            catch (Exception exception) when (exception is ArgumentException || exception is InvalidOperationException
                || exception is IOException || exception is UnauthorizedAccessException)
            {
                reason = exception.Message;
                return false;
            }
        }

        public static void Save(SkillData skill)
        {
            if (!CanEdit(skill, out string reason)) throw new InvalidOperationException(reason);
            AssetDatabase.SaveAssetIfDirty(skill);
        }

        private static string PrepareNewPath(string path)
        {
            string normalized = ValidatePath(path);
            RequireUnusedPath(normalized);
            ContentMakerAssetUtility.EnsureFolder(Path.GetDirectoryName(normalized).Replace('\\', '/'));
            // Folder creation may invoke editor callbacks. Recheck before the asset write.
            ValidatePath(normalized);
            RequireUnusedPath(normalized);
            return normalized;
        }

        private static string ExistingSkillPath(SkillData skill)
        {
            if (skill == null || !EditorUtility.IsPersistent(skill) || !AssetDatabase.IsMainAsset(skill))
                throw new ArgumentException("저장된 스킬 자산을 선택해 주세요.");
            string path = ValidatePath(AssetDatabase.GetAssetPath(skill));
            if (!File.Exists(AbsolutePath(path))) throw new IOException("스킬 파일이 삭제되거나 이동되었습니다: " + path);
            return path;
        }

        private static string ValidatePath(string path)
        {
            string normalized = ContentMakerAssetUtility.NormalizeContentPath(path);
            if (!string.Equals(Path.GetExtension(normalized), ".asset", StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("스킬은 콘텐츠 폴더 안의 .asset 파일로 저장해야 합니다.");
            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string fullPath = AbsolutePath(normalized);
            string contentRoot = AbsolutePath(ContentMakerAssetUtility.ContentRoot).TrimEnd(Path.DirectorySeparatorChar);
            if (!fullPath.StartsWith(contentRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("저장 경로가 콘텐츠 폴더 밖으로 벗어났습니다.");
            for (DirectoryInfo folder = new DirectoryInfo(Path.GetDirectoryName(fullPath)); folder != null; folder = folder.Parent)
            {
                if (string.Equals(folder.FullName, projectRoot, StringComparison.OrdinalIgnoreCase)) break;
                if (folder.Exists && (folder.Attributes & FileAttributes.ReparsePoint) != 0)
                    throw new ArgumentException("연결된 폴더(심볼릭 링크/정션)에는 스킬을 저장하지 않습니다.");
            }
            if (File.Exists(fullPath) && (File.GetAttributes(fullPath) & FileAttributes.ReparsePoint) != 0)
                throw new ArgumentException("연결된 파일은 스킬 메이커에서 편집하지 않습니다.");
            return normalized;
        }

        private static void RequireUnusedPath(string path)
        {
            string fullPath = AbsolutePath(path);
            if (File.Exists(fullPath) || File.Exists(fullPath + ".meta") || Directory.Exists(fullPath)
                || !string.IsNullOrEmpty(AssetDatabase.AssetPathToGUID(path)))
                throw new IOException("같은 경로의 파일 또는 메타데이터가 이미 있습니다. 다른 이름을 선택해 주세요: " + path);
        }

        private static string CreateUniqueSkillId(string name)
        {
            var used = new HashSet<string>(StringComparer.Ordinal);
            foreach (string guid in AssetDatabase.FindAssets("t:SkillData"))
            {
                SkillData skill = AssetDatabase.LoadAssetAtPath<SkillData>(AssetDatabase.GUIDToAssetPath(guid));
                if (skill != null && !string.IsNullOrWhiteSpace(skill.SkillID)) used.Add(skill.SkillID.Trim());
            }
            string stem = ContentMakerAssetUtility.MakeId(name);
            if (!stem.StartsWith("skill_", StringComparison.Ordinal)) stem = "skill_" + stem;
            string result = stem;
            for (int suffix = 2; used.Contains(result); suffix++) result = stem + "_" + suffix;
            return result;
        }

        private static void EnsureEditMode()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("게임 재생을 종료한 뒤 스킬을 편집해 주세요.");
        }

        private static string AbsolutePath(string path) => Path.GetFullPath(Path.Combine(Application.dataPath, "..", path));
        private static string DisplayName(SkillData skill) => string.IsNullOrWhiteSpace(skill.SkillName) ? skill.name : skill.SkillName;
        private static bool Contains(string value, string search) => value != null && value.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0;
    }
}
