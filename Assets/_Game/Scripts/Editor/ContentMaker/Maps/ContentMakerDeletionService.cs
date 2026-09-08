using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace HubToHome.EditorTools.ContentMaker
{
    internal sealed class ContentMakerDeletionPreview
    {
        public string Title;
        public string RegionPath;
        public RoomDefinition Room;
        public string[] Paths = Array.Empty<string>();
        public string[] Blockers = Array.Empty<string>();
        public bool CanDelete => Paths.Length > 0 && Blockers.Length == 0;

        internal bool IsRegion;
        internal string[] DeleteRoots;
        internal string[] Snapshot;
    }

    /// <summary>Explicit, reference-checked OS trash commands. Never disconnects another asset.</summary>
    internal static class ContentMakerDeletionService
    {
        private static readonly UTF8Encoding StrictUtf8 = new UTF8Encoding(false, true);
        private static readonly HashSet<string> ReferenceTextExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        { ".asset", ".prefab", ".unity", ".yaml", ".yml", ".json", ".csv", ".tsv", ".cs" };

        public static ContentMakerDeletionPreview PreviewRoom(string regionPath, RoomDefinition room)
        {
            return BuildPreview(regionPath, room, false);
        }

        public static ContentMakerDeletionPreview PreviewRegion(string regionPath)
        {
            return BuildPreview(regionPath, null, true);
        }

        public static string MoveToTrash(ContentMakerDeletionPreview preview)
        {
            if (preview == null || preview.Snapshot == null || preview.DeleteRoots == null || !preview.CanDelete)
                throw new InvalidOperationException("먼저 삭제 대상·참조 검사를 완료하세요.");

            // A preview is not permission to delete assets added/replaced since the user reviewed it.
            ContentMakerDeletionPreview current = BuildPreview(preview.RegionPath, preview.Room, preview.IsRegion);
            if (!current.CanDelete)
                throw new InvalidOperationException("현재 상태에서는 삭제할 수 없습니다.\n" + string.Join("\n", current.Blockers));
            if (!SameValues(preview.Paths, current.Paths) || !SameValues(preview.DeleteRoots, current.DeleteRoots)
                || !SameValues(preview.Snapshot, current.Snapshot))
                throw new InvalidOperationException("검사 이후 대상 파일이나 연결이 변경되었습니다. 삭제 대상·참조 검사를 다시 실행하세요.");

            var failures = new List<string>();
            bool success = false;
            string failureReason = string.Empty;
            try { success = AssetDatabase.MoveAssetsToTrash(current.DeleteRoots, failures); }
            catch (Exception exception) { failureReason = "\n원인: " + exception.Message; }
            var remaining = new List<string>();
            int moved = 0;
            foreach (string path in current.DeleteRoots)
            {
                if (File.Exists(path) || Directory.Exists(path)) remaining.Add(path);
                else moved++;
            }
            if (!success || failures.Count > 0 || remaining.Count > 0)
            {
                foreach (string path in failures)
                    if (!remaining.Contains(path)) remaining.Add(path);
                throw new InvalidOperationException("휴지통 이동이 일부 또는 전부 실패했습니다. 이동 완료 " + moved + "개 / 요청 "
                    + current.DeleteRoots.Length + "개. 이미 이동한 파일은 운영체제 휴지통 또는 버전 관리에서 복원하세요.\n남은/실패 대상:\n"
                    + string.Join("\n", remaining) + failureReason);
            }
            return current.Title + "을(를) 휴지통으로 이동했습니다. 복원은 운영체제 휴지통 또는 버전 관리를 사용하세요. 다른 자산의 연결은 변경하지 않았습니다.";
        }

        private static ContentMakerDeletionPreview BuildPreview(string regionPath, RoomDefinition room, bool isRegion)
        {
            var preview = new ContentMakerDeletionPreview { RegionPath = regionPath, Room = room, IsRegion = isRegion,
                Title = isRegion ? "챕터" : "방" };
            var blockers = new List<string>();
            try
            {
                string region = RequireRegion(regionPath);
                preview.RegionPath = region;
                preview.Title = isRegion ? "챕터 '" + Path.GetFileName(region) + "'" : "방 '" + (room != null ? room.name : "미선택") + "'";
                var roots = new List<string>();
                var paths = new List<string>();
                var tokens = new HashSet<string>(StringComparer.Ordinal);
                if (isRegion)
                {
                    roots.Add(region);
                    CollectFolder(region, paths);
                    foreach (string path in paths)
                    {
                        AddToken(tokens, path);
                        AddToken(tokens, AssetDatabase.AssetPathToGUID(path));
                        if (path.EndsWith(".unity", StringComparison.OrdinalIgnoreCase))
                            AddToken(tokens, Path.GetFileNameWithoutExtension(path));
                        RoomDefinition member = AssetDatabase.LoadAssetAtPath<RoomDefinition>(path);
                        if (member != null) AddToken(tokens, member.RoomId);
                        AreaDefinition area = AssetDatabase.LoadAssetAtPath<AreaDefinition>(path);
                        if (area != null) AddToken(tokens, area.AreaId);
                        DialogueData dialogue = AssetDatabase.LoadAssetAtPath<DialogueData>(path);
                        if (dialogue != null) AddToken(tokens, dialogue.name);
                    }
                }
                else
                {
                    if (room == null || !EditorUtility.IsPersistent(room))
                        throw new InvalidOperationException("삭제할 방 데이터를 선택하세요.");
                    string definitionPath = AssetDatabase.GetAssetPath(room);
                    RequireOwnedFile(definitionPath, region + "/Data/Rooms", ".asset");
                    if (AssetDatabase.LoadMainAssetAtPath(definitionPath) != room)
                        throw new InvalidOperationException("다른 파일 안의 하위 방 데이터는 자동 삭제하지 않습니다: " + definitionPath);
                    roots.Add(definitionPath);
                    if (room.RoomPrefab != null)
                    {
                        string prefabPath = AssetDatabase.GetAssetPath(room.RoomPrefab.gameObject);
                        if (IsInFolder(prefabPath, region + "/Prefabs/Rooms"))
                        {
                            RequireOwnedFile(prefabPath, region + "/Prefabs/Rooms", ".prefab");
                            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                            if (prefab == null || prefab.GetComponent<RoomInstance>() != room.RoomPrefab)
                                throw new InvalidOperationException("방 프리팹 루트의 RoomInstance 연결을 확인하세요: " + prefabPath);
                            roots.Add(prefabPath);
                        }
                    }
                    if (room.AreaDefinition != null && room.AreaDefinition.RoomDefinition == room)
                    {
                        string areaPath = AssetDatabase.GetAssetPath(room.AreaDefinition);
                        if (IsInFolder(areaPath, region + "/Data/Rooms"))
                        {
                            RequireOwnedFile(areaPath, region + "/Data/Rooms", ".asset");
                            if (AssetDatabase.LoadMainAssetAtPath(areaPath) != room.AreaDefinition)
                                throw new InvalidOperationException("하위 구역 데이터는 자동 삭제하지 않습니다: " + areaPath);
                            roots.Add(areaPath);
                        }
                    }
                    paths.AddRange(roots);
                    AddToken(tokens, room.RoomId);
                    foreach (string path in paths)
                    {
                        AddToken(tokens, path);
                        AddToken(tokens, AssetDatabase.AssetPathToGUID(path));
                        AreaDefinition area = AssetDatabase.LoadAssetAtPath<AreaDefinition>(path);
                        if (area != null) AddToken(tokens, area.AreaId);
                    }
                }

                paths.Sort(StringComparer.Ordinal);
                roots.Sort(StringComparer.Ordinal);
                preview.Paths = paths.ToArray();
                preview.DeleteRoots = roots.ToArray();
                preview.Snapshot = CaptureSnapshot(paths);
                var deleting = new HashSet<string>(paths, StringComparer.OrdinalIgnoreCase);
                CheckEditorState(deleting, blockers);
                if (blockers.Count == 0)
                    CheckReferences(deleting, tokens, blockers);
            }
            catch (OperationCanceledException)
            {
                blockers.Add("삭제 검사를 취소했습니다. 아무 파일도 변경하지 않았습니다.");
            }
            catch (Exception exception)
            {
                blockers.Add("삭제 범위를 안전하게 확인하지 못했습니다: " + exception.Message);
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
            preview.Blockers = blockers.ToArray();
            return preview;
        }

        private static string RequireRegion(string path)
        {
            string region = ContentMakerAssetUtility.NormalizeContentPath(path);
            string prefix = ContentMakerAssetUtility.RegionsRoot + "/";
            if (!region.StartsWith(prefix, StringComparison.Ordinal) || region.IndexOf('/', prefix.Length) >= 0
                || !AssetDatabase.IsValidFolder(region))
                throw new InvalidOperationException("Regions 바로 아래의 챕터 폴더만 삭제할 수 있습니다. Regions 자체나 하위 폴더는 삭제하지 않습니다.");
            RequirePhysicalPath(region);
            return region;
        }

        private static void RequireOwnedFile(string path, string folder, string extension)
        {
            if (!IsInFolder(path, folder) || !path.EndsWith(extension, StringComparison.OrdinalIgnoreCase)
                || !File.Exists(path) || string.IsNullOrEmpty(AssetDatabase.AssetPathToGUID(path)))
                throw new InvalidOperationException("현재 챕터의 전용 자산인지 확인할 수 없습니다: " + path);
            RequirePhysicalPath(path);
        }

        internal static bool IsInFolder(string path, string folder)
        {
            if (string.IsNullOrWhiteSpace(path) || string.IsNullOrWhiteSpace(folder) || path.IndexOf('\\') >= 0)
                return false;
            foreach (string part in path.Split('/'))
                if (part.Length == 0 || part == "." || part == "..") return false;
            return path.StartsWith(folder + "/", StringComparison.Ordinal);
        }

        private static void RequirePhysicalPath(string path)
        {
            string project = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string full = Path.GetFullPath(Path.Combine(project, path));
            string regions = Path.GetFullPath(Path.Combine(project, ContentMakerAssetUtility.RegionsRoot));
            if (!full.StartsWith(regions + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("삭제 경로가 Regions 범위를 벗어납니다: " + path);
            for (string current = full; !string.Equals(current, project, StringComparison.OrdinalIgnoreCase); current = Path.GetDirectoryName(current))
            {
                if (string.IsNullOrEmpty(current)) throw new InvalidOperationException("삭제 경로를 확인할 수 없습니다: " + path);
                if ((File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                    throw new InvalidOperationException("링크/정션 경로는 자동 삭제하지 않습니다: " + path);
            }
        }

        private static void CollectFolder(string folder, List<string> paths)
        {
            RequirePhysicalPath(folder);
            paths.Add(folder);
            foreach (string child in Directory.GetFileSystemEntries(folder))
            {
                string path = child.Replace('\\', '/');
                RequirePhysicalPath(path);
                if (Directory.Exists(path)) CollectFolder(path, paths);
                else if (!path.EndsWith(".meta", StringComparison.OrdinalIgnoreCase)) paths.Add(path);
            }
        }

        private static string[] CaptureSnapshot(List<string> paths)
        {
            var result = new List<string>();
            foreach (string path in paths)
            {
                AddSnapshot(result, path);
                if (File.Exists(path + ".meta")) AddSnapshot(result, path + ".meta");
                if (Directory.Exists(path))
                {
                    // Include orphan metadata too: a folder trash command would remove it even without an imported asset.
                    foreach (string meta in Directory.GetFiles(path, "*.meta", SearchOption.TopDirectoryOnly))
                    {
                        string normalized = meta.Replace('\\', '/');
                        if (!File.Exists(normalized.Substring(0, normalized.Length - 5))
                            && !Directory.Exists(normalized.Substring(0, normalized.Length - 5)))
                            AddSnapshot(result, normalized);
                    }
                }
            }
            result.Sort(StringComparer.Ordinal);
            return result.ToArray();
        }

        private static void AddSnapshot(List<string> result, string path)
        {
            RequirePhysicalPath(path);
            if (Directory.Exists(path)) result.Add(path + "|folder|" + AssetDatabase.AssetPathToGUID(path));
            else
            {
                var file = new FileInfo(path);
                result.Add(path + "|" + file.Length + "|" + file.LastWriteTimeUtc.Ticks + "|" + AssetDatabase.AssetPathToGUID(path));
            }
        }

        private static void CheckEditorState(HashSet<string> deleting, List<string> blockers)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating)
                blockers.Add("Play Mode·컴파일·자산 가져오기가 끝난 뒤 다시 검사하세요.");
            if (PrefabStageUtility.GetCurrentPrefabStage() != null)
                blockers.Add("Prefab Mode를 저장하고 종료한 뒤 삭제하세요. 열린 프리팹을 자동으로 저장하거나 닫지 않습니다.");
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene scene = SceneManager.GetSceneAt(i);
                if (!scene.isLoaded) continue;
                if (scene.isDirty)
                    blockers.Add("열린 씬에 저장하지 않은 변경이 있습니다. 직접 저장하거나 되돌린 뒤 검사하세요: " + scene.name);
                else if (deleting.Contains(scene.path))
                    blockers.Add("삭제 범위의 씬이 열려 있습니다. 다른 씬으로 이동한 뒤 검사하세요: " + scene.path);
            }
            foreach (EditorBuildSettingsScene scene in EditorBuildSettings.scenes)
                if (deleting.Contains(scene.path))
                    blockers.Add("빌드 씬 목록에 등록되어 있습니다. 먼저 목록에서 직접 제외하세요: " + scene.path);
        }

        private static void CheckReferences(HashSet<string> deleting, HashSet<string> tokens, List<string> blockers)
        {
            Regex tokenPattern = MakeTokenPattern(tokens);
            string[] allPaths = AssetDatabase.GetAllAssetPaths();
            for (int i = 0; i < allPaths.Length; i++)
            {
                string owner = allPaths[i];
                if ((i & 63) == 0 && EditorUtility.DisplayCancelableProgressBar("삭제 대상·참조 검사", owner, (float)i / allPaths.Length))
                    throw new OperationCanceledException();
                if (deleting.Contains(owner) || AssetDatabase.IsValidFolder(owner)) continue;
                foreach (string dependency in AssetDatabase.GetDependencies(owner, false))
                {
                    if (!deleting.Contains(dependency)) continue;
                    blockers.Add("참조 연결을 먼저 정리하세요: " + owner + " → " + dependency);
                    break;
                }
                if (tokenPattern == null || !ReferenceTextExtensions.Contains(Path.GetExtension(owner)) || !File.Exists(owner)) continue;
                try
                {
                    using (var reader = new StreamReader(owner, StrictUtf8, true))
                    {
                        int lineNumber = 0;
                        while (!reader.EndOfStream)
                        {
                            string line = reader.ReadLine();
                            lineNumber++;
                            if (line == null || line.IndexOf('\0') >= 0) break; // Binary Unity data is covered by AssetDatabase dependencies.
                            Match reference = tokenPattern.Match(line);
                            if (!reference.Success) continue;
                            blockers.Add("ID·경로 문자열이 사용 중입니다. 내용을 확인하고 직접 정리하세요: " + owner + ":" + lineNumber + " (" + reference.Value + ")");
                            break;
                        }
                    }
                }
                catch (DecoderFallbackException)
                {
                    // Unity binary containers still have dependency information. Source text must be readable to validate IDs.
                    string extension = Path.GetExtension(owner);
                    if (extension != ".asset" && extension != ".prefab" && extension != ".unity")
                        blockers.Add("UTF-8 원문을 읽을 수 없어 ID·경로 참조를 검사하지 못했습니다: " + owner);
                }
            }

            // AssetDatabase dependencies describe saved files. Inspect only unsaved persistent objects additionally.
            foreach (Object target in Resources.FindObjectsOfTypeAll<Object>())
            {
                if (target == null || !EditorUtility.IsPersistent(target) || !EditorUtility.IsDirty(target)) continue;
                string owner = AssetDatabase.GetAssetPath(target);
                if (deleting.Contains(owner))
                {
                    blockers.Add("삭제할 자산에 저장하지 않은 변경이 있습니다. 먼저 저장하거나 되돌리세요: " + owner);
                    continue;
                }
                foreach (Object dependency in EditorUtility.CollectDependencies(new[] { target }))
                {
                    if (dependency == null || !deleting.Contains(AssetDatabase.GetAssetPath(dependency))) continue;
                    blockers.Add("저장하지 않은 참조가 있습니다. 해당 자산을 먼저 정리하세요: " + owner);
                    break;
                }
                if (tokenPattern == null || target is MonoScript || target is AssetImporter) continue;
                using (var serialized = new SerializedObject(target))
                {
                    SerializedProperty property = serialized.GetIterator();
                    while (property.Next(true))
                    {
                        if (property.propertyType != SerializedPropertyType.String
                            || string.IsNullOrEmpty(property.stringValue) || !tokenPattern.IsMatch(property.stringValue)) continue;
                        blockers.Add("저장하지 않은 ID·경로 참조가 있습니다. 해당 자산을 먼저 정리하세요: " + owner + " / " + property.propertyPath);
                        break;
                    }
                }
            }
        }

        private static void AddToken(HashSet<string> tokens, string value)
        {
            if (!string.IsNullOrWhiteSpace(value)) tokens.Add(value);
        }

        internal static Regex MakeTokenPattern(IEnumerable<string> tokens)
        {
            var escaped = new List<string>();
            foreach (string token in tokens)
                if (!string.IsNullOrWhiteSpace(token)) escaped.Add(Regex.Escape(token));
            if (escaped.Count == 0) return null;
            return new Regex(@"(?<![\p{L}\p{N}_./-])(?:" + string.Join("|", escaped) + @")(?![\p{L}\p{N}_./-])",
                RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));
        }

        private static bool SameValues(string[] left, string[] right)
        {
            if (left == null || right == null || left.Length != right.Length) return false;
            for (int i = 0; i < left.Length; i++)
                if (!string.Equals(left[i], right[i], StringComparison.Ordinal)) return false;
            return true;
        }
    }
}
