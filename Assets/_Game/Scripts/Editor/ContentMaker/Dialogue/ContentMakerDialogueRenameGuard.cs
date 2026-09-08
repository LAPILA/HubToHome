using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;

namespace HubToHome.EditorTools.ContentMaker
{
    /// <summary>Dialogue source mappings use names/paths, unlike Unity's GUID references.</summary>
    internal static class ContentMakerDialogueRenameGuard
    {
        public static void EnsureCanRename(DialogueData dialogue)
        {
            if (dialogue == null) throw new ArgumentNullException(nameof(dialogue));
            string path = AssetDatabase.GetAssetPath(dialogue);
            var sources = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string guid in AssetDatabase.FindAssets("t:BattleScenarioData"))
            {
                string scenarioPath = AssetDatabase.GUIDToAssetPath(guid);
                BattleScenarioData scenario = AssetDatabase.LoadAssetAtPath<BattleScenarioData>(scenarioPath);
                if (scenario == null) continue;
                AddSource(sources, scenario.Source);
                if (scenario.Dialogues == null) continue;
                foreach (ScenarioDialogueReferenceData mapping in scenario.Dialogues)
                    if (mapping != null && (mapping.Dialogue == dialogue || Matches(mapping.DialogueDataId, dialogue.name, path)))
                        throw InUse(scenarioPath);
            }
            foreach (string guid in AssetDatabase.FindAssets("t:ActionSequenceAsset"))
            {
                ActionSequenceAsset sequence = AssetDatabase.LoadAssetAtPath<ActionSequenceAsset>(AssetDatabase.GUIDToAssetPath(guid));
                if (sequence != null) AddSource(sources, sequence.Source);
            }
            foreach (string assetPath in AssetDatabase.GetAllAssetPaths())
                if (assetPath.EndsWith(".scenario.yaml", StringComparison.OrdinalIgnoreCase) ||
                    assetPath.EndsWith(".scenario.yml", StringComparison.OrdinalIgnoreCase) ||
                    assetPath.EndsWith(".sequence.yaml", StringComparison.OrdinalIgnoreCase) ||
                    assetPath.EndsWith(".sequence.yml", StringComparison.OrdinalIgnoreCase))
                    sources.Add(assetPath);

            var parser = new ScenarioSourceYamlParser();
            foreach (string sourcePath in sources)
            {
                if (!File.Exists(sourcePath)) continue;
                string source = File.ReadAllText(sourcePath, new UTF8Encoding(false, true));
                ScenarioSourceParseResult parsed = parser.Parse(source, sourcePath);
                if (parsed.Document?.Dialogues != null)
                    foreach (ScenarioSourceDialogueDocument mapping in parsed.Document.Dialogues)
                        if (mapping != null && Matches(mapping.DialogueDataId, dialogue.name, path))
                            throw InUse(sourcePath);
                if (!parsed.Success && (source.IndexOf(dialogue.name, StringComparison.Ordinal) >= 0 || source.IndexOf(path, StringComparison.Ordinal) >= 0))
                    throw new InvalidOperationException("대화 이름/경로가 등장하는 시나리오 원문을 검증할 수 없어 이름 변경을 중단했습니다: " + sourcePath);
            }
        }

        private static void AddSource(HashSet<string> paths, ScenarioSourceMetadata metadata)
        {
            if (metadata != null && !string.IsNullOrWhiteSpace(metadata.SourcePath))
                paths.Add(metadata.SourcePath);
        }

        private static bool Matches(string reference, string name, string path)
        {
            return string.Equals(reference, name, StringComparison.Ordinal) || string.Equals(reference, path, StringComparison.OrdinalIgnoreCase);
        }

        private static InvalidOperationException InUse(string owner)
        {
            return new InvalidOperationException("이 대화는 시나리오에서 사용 중이라 이름을 바꿀 수 없습니다. 기존 시퀀스 메이커/원문의 대화 연결부터 정리하세요. 자동으로 원문을 변경하지 않습니다. 사용 위치: " + owner);
        }
    }
}
