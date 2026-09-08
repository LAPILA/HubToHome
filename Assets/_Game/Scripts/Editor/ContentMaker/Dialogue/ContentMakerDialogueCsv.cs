using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace HubToHome.EditorTools.ContentMaker
{
    internal sealed class ContentMakerDialogueImport
    {
        public DialogueStyle Style;
        public readonly List<DialogueNode> Nodes = new List<DialogueNode>();
        public int ChoiceCount;
    }

    /// <summary>Editor-only, versioned whole-dialogue CSV exchange. Parsing never changes assets.</summary>
    internal static class ContentMakerDialogueCsv
    {
        public const int MaximumCharacters = 4 * 1024 * 1024;
        private const int MaximumRows = 20000;
        private const string Version = "1";
        private static readonly string[] Header =
        {
            "schema", "row", "node", "choice", "style", "speaker_guid", "speaker_path", "emotion",
            "text", "localization_key", "event_id", "is_choice", "next_guid", "next_path", "set_flag", "start_battle"
        };

        public static string Export(DialogueData dialogue)
        {
            if (!DialoguePlaybackPolicy.TryValidate(dialogue, out string error))
                throw new InvalidOperationException("내보낼 대화를 확인하세요: " + error);

            var rows = new List<string[]> { Header };
            string[] document = NewRow("document");
            document[4] = dialogue.Style.ToString();
            rows.Add(document);
            for (int index = 0; index < dialogue.Nodes.Count; index++)
            {
                DialogueNode node = dialogue.Nodes[index];
                string[] row = NewRow("node");
                row[2] = index.ToString(CultureInfo.InvariantCulture);
                WriteReference(node.Speaker, row, 5, 6);
                row[7] = node.Emotion.ToString();
                row[8] = ProtectExcelText(node.DefaultText);
                row[9] = ProtectExcelText(node.LocalizationKey);
                row[10] = ProtectExcelText(node.EventTriggerID);
                row[11] = node.IsChoiceNode ? "true" : "false";
                rows.Add(row);
                // Preserve authored choices even when their node is temporarily disabled.
                if (node.Choices == null)
                    continue;
                for (int choiceIndex = 0; choiceIndex < node.Choices.Count; choiceIndex++)
                {
                    ChoiceData choice = node.Choices[choiceIndex];
                    if (choice == null)
                        throw new InvalidOperationException($"대사 {index + 1}의 선택지 {choiceIndex + 1}이 비어 있습니다.");
                    row = NewRow("choice");
                    row[2] = index.ToString(CultureInfo.InvariantCulture);
                    row[3] = choiceIndex.ToString(CultureInfo.InvariantCulture);
                    row[8] = ProtectExcelText(choice.ChoiceText);
                    WriteReference(choice.NextDialogue, row, 12, 13);
                    row[14] = ProtectExcelText(choice.SetFlagOnSelect);
                    row[15] = choice.StartBattleEncounter ? "true" : "false";
                    rows.Add(row);
                }
            }
            return WriteRows(rows);
        }

        public static ContentMakerDialogueImport Parse(
            string text,
            Func<string, string, Type, Object> referenceResolver = null)
        {
            List<string[]> rows = ReadRows(text);
            if (rows.Count < 3)
                throw new FormatException("헤더, document 행, node 행이 하나 이상 필요합니다.");
            CheckColumns(rows[0], 1);
            for (int index = 0; index < Header.Length; index++)
                if (!string.Equals(rows[0][index], Header[index], StringComparison.Ordinal))
                    throw new FormatException($"헤더 {index + 1}열은 '{Header[index]}'이어야 합니다. 창에서 내보낸 서식을 사용하세요.");

            referenceResolver = referenceResolver ?? ResolveAsset;
            var result = new ContentMakerDialogueImport();
            DialogueNode current = null;
            for (int rowIndex = 1; rowIndex < rows.Count; rowIndex++)
            {
                string[] row = rows[rowIndex];
                int line = rowIndex + 1;
                CheckColumns(row, line);
                if (row[0] != Version)
                    throw At(line, "지원하는 schema 버전은 1입니다.");
                if (rowIndex == 1)
                {
                    if (row[1] != "document")
                        throw At(line, "첫 데이터 행은 document여야 합니다.");
                    EnsureUnusedEmpty(row, line, 0, 1, 4);
                    result.Style = ParseEnum<DialogueStyle>(row[4], line, "style");
                    continue;
                }

                if (row[1] == "node")
                {
                    EnsureUnusedEmpty(row, line, 0, 1, 2, 5, 6, 7, 8, 9, 10, 11);
                    RequireIndex(row[2], result.Nodes.Count, line, "node");
                    current = new DialogueNode
                    {
                        Speaker = Resolve<SpeakerData>(row[5], row[6], referenceResolver, line),
                        Emotion = ParseEnum<EmotionType>(row[7], line, "emotion"),
                        DefaultText = RestoreExcelText(row[8]),
                        LocalizationKey = RestoreExcelText(row[9]),
                        EventTriggerID = RestoreExcelText(row[10]),
                        IsChoiceNode = ParseBool(row[11], line, "is_choice"),
                        Choices = new List<ChoiceData>()
                    };
                    result.Nodes.Add(current);
                }
                else if (row[1] == "choice")
                {
                    EnsureUnusedEmpty(row, line, 0, 1, 2, 3, 8, 12, 13, 14, 15);
                    if (current == null)
                        throw At(line, "choice 앞에 연결할 node 행이 필요합니다.");
                    RequireIndex(row[2], result.Nodes.Count - 1, line, "node");
                    RequireIndex(row[3], current.Choices.Count, line, "choice");
                    current.Choices.Add(new ChoiceData
                    {
                        ChoiceText = RestoreExcelText(row[8]),
                        NextDialogue = Resolve<DialogueData>(row[12], row[13], referenceResolver, line),
                        SetFlagOnSelect = RestoreExcelText(row[14]),
                        StartBattleEncounter = ParseBool(row[15], line, "start_battle")
                    });
                    result.ChoiceCount++;
                }
                else
                {
                    throw At(line, "row는 node 또는 choice여야 합니다. document는 첫 행에만 둡니다.");
                }
            }
            if (result.Nodes.Count == 0)
                throw new FormatException("대사가 비어 있습니다. node 행이 하나 이상 필요합니다.");
            return result;
        }

        public static List<string> GetWarnings(DialogueData target, IList<DialogueNode> nodes)
        {
            var warnings = new List<string>();
            if (nodes == null || nodes.Count == 0)
            {
                warnings.Add("대사가 없습니다. 한 줄 이상 작성하세요.");
                return warnings;
            }
            for (int index = 0; index < nodes.Count; index++)
            {
                DialogueNode node = nodes[index];
                if (node == null)
                {
                    warnings.Add($"대사 {index + 1}이 비어 있습니다.");
                    continue;
                }
                if (string.IsNullOrWhiteSpace(node.DefaultText) && string.IsNullOrWhiteSpace(node.LocalizationKey))
                    warnings.Add($"대사 {index + 1}: 본문과 번역 키가 모두 비어 있습니다.");
                if (node.Speaker != null && node.Emotion != EmotionType.None &&
                    (node.Speaker.Portraits == null || !node.Speaker.Portraits.ContainsKey(node.Emotion) || node.Speaker.GetPortrait(node.Emotion) == null))
                    warnings.Add($"대사 {index + 1}: {node.Speaker.DisplayName}의 {node.Emotion} 초상화가 없습니다.");
                int choices = node.Choices != null ? node.Choices.Count : 0;
                if (node.IsChoiceNode && choices == 0)
                    warnings.Add($"대사 {index + 1}: 선택지 사용은 켜져 있지만 선택지가 없습니다.");
                if (!node.IsChoiceNode && choices > 0)
                    warnings.Add($"대사 {index + 1}: 저장된 선택지 {choices}개는 사용이 꺼져 있습니다.");
                if (node.IsChoiceNode && choices > 0 && index < nodes.Count - 1)
                    warnings.Add($"대사 {index + 1}: 선택하면 다른 대화/전투로 이동하거나 종료하므로 뒤의 행으로 자동 진행하지 않습니다.");
                if (node.Choices == null)
                    continue;
                foreach (ChoiceData choice in node.Choices)
                {
                    if (choice == null)
                    {
                        warnings.Add($"대사 {index + 1}: 비어 있는 선택지를 삭제하거나 수정하세요.");
                        continue;
                    }
                    if (string.IsNullOrWhiteSpace(choice.ChoiceText))
                        warnings.Add($"대사 {index + 1}: 선택지 문구가 비어 있습니다.");
                    if (choice.StartBattleEncounter && choice.NextDialogue != null)
                        warnings.Add($"대사 {index + 1}: 전투 시작이 우선이며 다음 대화 연결은 사용되지 않습니다.");
                }
            }
            if (HasCycle(target, nodes))
                warnings.Add("선택지에 순환 연결이 있거나 검사 한도를 넘는 긴 연결이 있습니다. 의도한 반복 대화인지, 종료할 수 있는 선택지가 있는지 확인하세요.");
            return warnings;
        }

        private static bool HasCycle(DialogueData target, IList<DialogueNode> replacement)
        {
            var visiting = new HashSet<DialogueData>();
            var complete = new HashSet<DialogueData>();
            int remaining = 2048;
            return Visit(target, replacement, target, replacement, visiting, complete, ref remaining);
        }

        private static bool Visit(DialogueData document, IList<DialogueNode> nodes, DialogueData target,
            IList<DialogueNode> replacement, HashSet<DialogueData> visiting, HashSet<DialogueData> complete, ref int remaining)
        {
            if (document != null)
            {
                if (visiting.Contains(document)) return true;
                if (complete.Contains(document)) return false;
                visiting.Add(document);
            }
            // Bound traversal and avoid pathological authored graphs blocking the editor.
            if (--remaining < 0 || visiting.Count > 128)
                return true;
            if (nodes != null)
                foreach (DialogueNode node in nodes)
                {
                    if (node == null || !node.IsChoiceNode || node.Choices == null) continue;
                    foreach (ChoiceData choice in node.Choices)
                    {
                        if (choice == null || choice.StartBattleEncounter || choice.NextDialogue == null) continue;
                        DialogueData next = choice.NextDialogue;
                        if (Visit(next, next == target ? replacement : next.Nodes, target, replacement, visiting, complete, ref remaining))
                            return true;
                    }
                }
            if (document != null)
            {
                visiting.Remove(document);
                complete.Add(document);
            }
            return false;
        }

        private static string[] NewRow(string type)
        {
            var row = new string[Header.Length];
            for (int index = 0; index < row.Length; index++) row[index] = string.Empty;
            row[0] = Version;
            row[1] = type;
            return row;
        }

        private static void WriteReference(Object value, string[] row, int guidColumn, int pathColumn)
        {
            if (value == null) return;
            string path = AssetDatabase.GetAssetPath(value);
            string guid = AssetDatabase.AssetPathToGUID(path);
            if (string.IsNullOrEmpty(guid) || AssetDatabase.LoadMainAssetAtPath(path) != value)
                throw new InvalidOperationException($"'{value.name}'은 프로젝트에 저장된 독립 자산이어야 CSV로 내보낼 수 있습니다.");
            row[guidColumn] = guid;
            row[pathColumn] = path;
        }

        private static T Resolve<T>(string guid, string path, Func<string, string, Type, Object> resolver, int line) where T : Object
        {
            if (string.IsNullOrEmpty(guid) && string.IsNullOrEmpty(path)) return null;
            try
            {
                Object result = resolver(guid, path, typeof(T));
                if (result is T typed) return typed;
                throw new InvalidOperationException(typeof(T).Name + " 참조를 찾지 못했습니다: " + path);
            }
            catch (Exception error)
            {
                throw At(line, error.Message);
            }
        }

        private static Object ResolveAsset(string guid, string path, Type type)
        {
            string resolvedPath;
            if (!string.IsNullOrEmpty(guid))
            {
                if (!Guid.TryParseExact(guid, "N", out _))
                    throw new InvalidOperationException("자산 GUID 형식이 올바르지 않습니다.");
                resolvedPath = AssetDatabase.GUIDToAssetPath(guid);
                if (string.IsNullOrEmpty(resolvedPath))
                    throw new InvalidOperationException("GUID의 자산이 없습니다. 삭제되거나 다른 프로젝트의 CSV인지 확인하세요: " + path);
                Object oldPathAsset = string.IsNullOrEmpty(path) ? null : AssetDatabase.LoadMainAssetAtPath(path);
                if (oldPathAsset != null && !string.Equals(AssetDatabase.AssetPathToGUID(path), guid, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("GUID와 경로가 서로 다른 자산을 가리킵니다: " + path);
            }
            else
            {
                if (!path.StartsWith("Assets/", StringComparison.Ordinal) || path.Contains("\\") || path.Contains("/../"))
                    throw new InvalidOperationException("참조 경로는 Assets/로 시작하는 프로젝트 자산 경로여야 합니다.");
                resolvedPath = path;
            }
            Object asset = AssetDatabase.LoadMainAssetAtPath(resolvedPath);
            if (asset == null || !type.IsInstanceOfType(asset))
                throw new InvalidOperationException(type.Name + " 자산을 찾지 못했습니다: " + resolvedPath);
            return asset;
        }

        private static T ParseEnum<T>(string value, int line, string column) where T : struct
        {
            if (Enum.TryParse(value, out T result) && Enum.IsDefined(typeof(T), result) && result.ToString() == value)
                return result;
            throw At(line, column + " 값이 올바르지 않습니다: " + value);
        }

        private static bool ParseBool(string value, int line, string column)
        {
            if (string.Equals(value, "true", StringComparison.OrdinalIgnoreCase)) return true;
            if (string.Equals(value, "false", StringComparison.OrdinalIgnoreCase)) return false;
            throw At(line, column + "에는 true 또는 false를 입력하세요.");
        }

        private static void RequireIndex(string value, int expected, int line, string column)
        {
            if (!int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out int actual) || actual != expected)
                throw At(line, $"{column} 번호는 0부터 연속된 순서여야 합니다. 이 행의 예상 값: {expected}");
        }

        private static void CheckColumns(string[] row, int line)
        {
            if (row.Length != Header.Length)
                throw At(line, $"열 수가 {row.Length}개입니다. {Header.Length}개여야 합니다.");
        }

        private static void EnsureUnusedEmpty(string[] row, int line, params int[] allowed)
        {
            for (int index = 0; index < row.Length; index++)
                if (Array.IndexOf(allowed, index) < 0 && !string.IsNullOrEmpty(row[index]))
                    throw At(line, $"{row[1]} 행에서는 '{Header[index]}' 열을 비워야 합니다. 값이 유실되지 않도록 반영을 중단했습니다.");
        }

        private static FormatException At(int row, string error) => new FormatException($"CSV 레코드 {row}: {error}");

        private static string ProtectExcelText(string value)
        {
            value = value ?? string.Empty;
            return value.Length > 0 && IsExcelSpecial(value[0]) ? "'" + value : value;
        }

        private static string RestoreExcelText(string value)
        {
            return value.Length > 1 && value[0] == '\'' && IsExcelSpecial(value[1]) ? value.Substring(1) : value;
        }

        private static bool IsExcelSpecial(char value) => value == '\'' || value == '=' || value == '+' || value == '-' || value == '@' || value == '\t' || value == '\r' || value == '\n';

        private static string WriteRows(List<string[]> rows)
        {
            if (rows.Count > MaximumRows)
                throw new InvalidOperationException("CSV 레코드가 너무 많습니다. 대화를 나누어 작성하세요.");
            var result = new StringBuilder();
            foreach (string[] row in rows)
            {
                for (int index = 0; index < row.Length; index++)
                {
                    if (index > 0) result.Append(',');
                    result.Append('"').Append((row[index] ?? string.Empty).Replace("\"", "\"\"")).Append('"');
                }
                result.Append("\r\n");
            }
            if (result.Length > MaximumCharacters)
                throw new InvalidOperationException("CSV가 4백만 글자 제한을 초과합니다. 대화를 나누어 작성하세요.");
            return result.ToString();
        }

        private static List<string[]> ReadRows(string source)
        {
            if (string.IsNullOrEmpty(source)) throw new FormatException("CSV가 비어 있습니다.");
            if (source.Length > MaximumCharacters) throw new FormatException("CSV는 4백만 글자 이하로 나누어 가져오세요.");
            var rows = new List<string[]>();
            var cells = new List<string>();
            var cell = new StringBuilder();
            bool quoted = false;
            bool closed = false;
            bool recordStarted = false;
            int start = source[0] == '\uFEFF' ? 1 : 0;
            for (int index = start; index < source.Length; index++)
            {
                char value = source[index];
                if (value == '\0') throw new FormatException("CSV에 NUL 문자가 있습니다. UTF-8 CSV로 저장하세요.");
                if (quoted)
                {
                    if (value == '"')
                    {
                        if (index + 1 < source.Length && source[index + 1] == '"') { cell.Append('"'); index++; }
                        else { quoted = false; closed = true; }
                    }
                    else cell.Append(value);
                    continue;
                }
                if (value == ',' || value == '\r' || value == '\n')
                {
                    cells.Add(cell.ToString());
                    cell.Clear();
                    closed = false;
                    if (value == ',') { recordStarted = true; continue; }
                    if (recordStarted || cells.Count != 1 || cells[0].Length != 0)
                        rows.Add(cells.ToArray());
                    cells.Clear();
                    recordStarted = false;
                    if (value == '\r' && index + 1 < source.Length && source[index + 1] == '\n') index++;
                    if (rows.Count > MaximumRows) throw new FormatException("CSV 레코드가 너무 많습니다. 대화를 나누어 가져오세요.");
                    continue;
                }
                if (closed) throw new FormatException("닫는 따옴표 다음에는 쉼표 또는 줄바꿈만 올 수 있습니다.");
                if (value == '"')
                {
                    if (cell.Length != 0) throw new FormatException("셀 안의 따옴표는 두 번 쓰고 셀 전체를 따옴표로 감싸야 합니다.");
                    quoted = true;
                }
                else cell.Append(value);
                recordStarted = true;
            }
            if (quoted) throw new FormatException("닫히지 않은 따옴표가 있습니다. 기존 대화는 변경하지 않았습니다.");
            if (recordStarted || cells.Count > 0 || cell.Length > 0 || closed)
            {
                cells.Add(cell.ToString());
                rows.Add(cells.ToArray());
            }
            if (rows.Count > MaximumRows) throw new FormatException("CSV 레코드가 너무 많습니다. 대화를 나누어 가져오세요.");
            return rows;
        }
    }
}
