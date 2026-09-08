using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace HubToHome.EditorTools.ContentMaker
{
    internal static class ContentMakerAssetUtility
    {
        public const string ContentRoot = "Assets/_Game/Content";
        public const string RegionsRoot = ContentRoot + "/Maps/Regions";
        public const string SpeakersRoot = ContentRoot + "/Dialogue/Data/Speakers";

        public static string NormalizeContentPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("저장 폴더가 비어 있습니다.");
            string result = path.Replace('\\', '/').TrimEnd('/');
            if (result != ContentRoot && !result.StartsWith(ContentRoot + "/", StringComparison.Ordinal))
                throw new ArgumentException("콘텐츠 메이커는 Assets/_Game/Content 아래에만 저장합니다.");
            foreach (string part in result.Split('/'))
            {
                if (string.IsNullOrWhiteSpace(part) || part == "." || part == ".." || part != MakeFileName(part))
                    throw new ArgumentException("저장 경로에 사용할 수 없는 폴더명이 있습니다: " + part);
            }
            return result;
        }

        public static void EnsureFolder(string path)
        {
            string normalized = NormalizeContentPath(path);
            if (AssetDatabase.IsValidFolder(normalized)) return;
            int split = normalized.LastIndexOf('/');
            string parent = normalized.Substring(0, split);
            if (!AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
            if (string.IsNullOrEmpty(AssetDatabase.CreateFolder(parent, normalized.Substring(split + 1))))
                throw new IOException("폴더를 만들지 못했습니다: " + normalized);
        }

        public static string UniqueAssetPath(string folder, string name, string extension)
        {
            folder = NormalizeContentPath(folder);
            if (extension != ".asset" && extension != ".prefab" && extension != ".unity")
                throw new ArgumentException("지원하지 않는 자산 확장자입니다.");
            EnsureFolder(folder);
            string stem = folder + "/" + MakeFileName(name);
            string candidate = AssetDatabase.GenerateUniqueAssetPath(stem + extension);
            for (int suffix = 2; File.Exists(candidate) || File.Exists(candidate + ".meta") || Directory.Exists(candidate); suffix++)
                candidate = AssetDatabase.GenerateUniqueAssetPath(stem + "_" + suffix + extension);
            return candidate;
        }

        public static string MakeFileName(string value)
        {
            string source = (value ?? string.Empty).Trim();
            StringBuilder result = new StringBuilder(source.Length);
            foreach (char character in source)
                result.Append(char.IsControl(character) || "<>:\"/\\|?*".IndexOf(character) >= 0 ? '_' : character);
            string safe = result.ToString().Trim(' ', '.');
            if (string.IsNullOrEmpty(safe)) return "Untitled";
            string stem = safe.Split('.')[0].ToUpperInvariant();
            if (stem == "CON" || stem == "PRN" || stem == "AUX" || stem == "NUL"
                || (stem.Length == 4 && (stem.StartsWith("COM") || stem.StartsWith("LPT")) && stem[3] >= '1' && stem[3] <= '9'))
                safe = "_" + safe;
            return safe;
        }

        public static string MakeId(string value)
        {
            StringBuilder result = new StringBuilder();
            foreach (char character in (value ?? string.Empty).ToLowerInvariant())
            {
                if (character >= 'a' && character <= 'z' || character >= '0' && character <= '9') result.Append(character);
                else if ((character == '.' || character == '_' || character == '-' || char.IsWhiteSpace(character))
                    && result.Length > 0 && result[result.Length - 1] != '_') result.Append('_');
            }
            string id = result.ToString().Trim('_');
            return string.IsNullOrEmpty(id) ? "content_" + Guid.NewGuid().ToString("N").Substring(0, 8) : id;
        }

        public static void Save(Object target)
        {
            if (target == null || !EditorUtility.IsPersistent(target)) return;
            EditorUtility.SetDirty(target);
            AssetDatabase.SaveAssetIfDirty(target);
        }
    }
}
