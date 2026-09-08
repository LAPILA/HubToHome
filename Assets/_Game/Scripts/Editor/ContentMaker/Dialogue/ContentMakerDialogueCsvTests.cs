using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NUnit.Framework;
using UnityEngine;

namespace HubToHome.EditorTools.ContentMaker
{
    public sealed class ContentMakerDialogueCsvTests
    {
        private DialogueData _source;
        private DialogueData _other;
        private SpeakerData _speaker;

        [SetUp]
        public void SetUp()
        {
            _source = ScriptableObject.CreateInstance<DialogueData>();
            _source.Style = DialogueStyle.Cinematic;
            _source.Nodes.Add(new DialogueNode
            {
                DefaultText = "위젤: \"정말, 가야 하나요?\"\r\n다음 줄\n세 번째 줄",
                Emotion = EmotionType.Confused,
                LocalizationKey = "chapter01.hello",
                EventTriggerID = "known.event",
                IsChoiceNode = true,
                Choices = new List<ChoiceData>
                {
                    new ChoiceData { ChoiceText = "네, 갑시다.", SetFlagOnSelect = "choice.accept", StartBattleEncounter = true },
                    new ChoiceData { ChoiceText = "\"아니요\"\n잠시만요." }
                }
            });
            _source.Nodes.Add(new DialogueNode { DefaultText = "마지막 대사", Emotion = EmotionType.None });
        }

        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(_source);
            if (_other != null) UnityEngine.Object.DestroyImmediate(_other);
            if (_speaker != null) UnityEngine.Object.DestroyImmediate(_speaker);
        }

        [Test]
        public void RoundTripPreservesKoreanMultilineQuotesAndEveryField()
        {
            string csv = ContentMakerDialogueCsv.Export(_source);
            ContentMakerDialogueImport parsed = ContentMakerDialogueCsv.Parse("\uFEFF" + csv);
            Assert.That(parsed.Style, Is.EqualTo(DialogueStyle.Cinematic));
            Assert.That(parsed.Nodes.Count, Is.EqualTo(2));
            Assert.That(parsed.ChoiceCount, Is.EqualTo(2));
            DialogueNode node = parsed.Nodes[0];
            Assert.That(node.DefaultText, Is.EqualTo(_source.Nodes[0].DefaultText));
            Assert.That(node.Emotion, Is.EqualTo(EmotionType.Confused));
            Assert.That(node.LocalizationKey, Is.EqualTo("chapter01.hello"));
            Assert.That(node.EventTriggerID, Is.EqualTo("known.event"));
            Assert.That(node.IsChoiceNode, Is.True);
            Assert.That(node.Choices[0].ChoiceText, Is.EqualTo("네, 갑시다."));
            Assert.That(node.Choices[0].SetFlagOnSelect, Is.EqualTo("choice.accept"));
            Assert.That(node.Choices[0].StartBattleEncounter, Is.True);
            Assert.That(node.Choices[1].ChoiceText, Is.EqualTo(_source.Nodes[0].Choices[1].ChoiceText));
            Assert.That(node.Choices[1].NextDialogue, Is.Null);
            Assert.That(parsed.Nodes[1].Emotion, Is.EqualTo(EmotionType.None));
        }

        [TestCase("=SUM(A1:A2)")]
        [TestCase("+12345")]
        [TestCase("-안녕하세요")]
        [TestCase("@hello")]
        [TestCase("'원래 작은따옴표")]
        [TestCase("'=원래 작은따옴표")]
        [TestCase("\t탭 시작")]
        [TestCase("\n줄바꿈 시작")]
        [TestCase("0012")]
        [TestCase("2026-09-07")]
        public void ExcelProtectionRoundTripsWithoutChangingAuthoredText(string text)
        {
            _source.Nodes[0].DefaultText = text;
            Assert.That(ContentMakerDialogueCsv.Parse(ContentMakerDialogueCsv.Export(_source)).Nodes[0].DefaultText, Is.EqualTo(text));
        }

        [Test]
        public void DisabledChoiceRowsArePreservedRatherThanSilentlyDropped()
        {
            _source.Nodes[0].IsChoiceNode = false;
            ContentMakerDialogueImport result = ContentMakerDialogueCsv.Parse(ContentMakerDialogueCsv.Export(_source));
            Assert.That(result.Nodes[0].IsChoiceNode, Is.False);
            Assert.That(result.Nodes[0].Choices.Count, Is.EqualTo(2));
            Assert.That(ContentMakerDialogueCsv.GetWarnings(_source, result.Nodes).Any(value => value.Contains("사용이 꺼져")), Is.True);
        }

        [Test]
        public void AcceptsLastRecordWithoutNewlineAndBlankTrailingLines()
        {
            string csv = ContentMakerDialogueCsv.Export(_source);
            Assert.That(ContentMakerDialogueCsv.Parse(csv.TrimEnd('\r', '\n')).Nodes.Count, Is.EqualTo(2));
            Assert.That(ContentMakerDialogueCsv.Parse(csv + "\r\n\n").Nodes.Count, Is.EqualTo(2));
        }

        [Test]
        public void RowLimitAlsoRejectsFinalRecordWithoutNewline()
        {
            var csv = new StringBuilder();
            for (int index = 0; index < 20000; index++) csv.Append("record\n");
            csv.Append("last record");
            FormatException error = Assert.Throws<FormatException>(() => ContentMakerDialogueCsv.Parse(csv.ToString()));
            Assert.That(error.Message, Does.Contain("레코드가 너무 많"));
        }

        [Test]
        public void EmptyChoiceNodeDoesNotWarnThatFollowingRowsAreUnreachable()
        {
            _source.Nodes[0].Choices.Clear();
            List<string> warnings = ContentMakerDialogueCsv.GetWarnings(_source, _source.Nodes);
            Assert.That(warnings.Any(value => value.Contains("선택지가 없습니다")), Is.True);
            Assert.That(warnings.Any(value => value.Contains("자동 진행하지 않습니다")), Is.False);
        }

        [TestCase("\"1\",\"document\"", "\"2\",\"document\"")]
        [TestCase("\"1\",\"node\",\"0\"", "\"1\",\"node\",\"3\"")]
        [TestCase("\"1\",\"choice\",\"0\",\"1\"", "\"1\",\"choice\",\"0\",\"7\"")]
        [TestCase("\"Confused\"", "\"UnknownEmotion\"")]
        [TestCase("\"Cinematic\"", "\"17\"")]
        [TestCase("\"false\"", "\"maybe\"")]
        public void InvalidSchemaOrderEnumOrBoolFailsWithoutChangingSource(string before, string after)
        {
            string snapshot = ContentMakerDialogueCsv.Export(_source);
            Assert.Throws<FormatException>(() => ContentMakerDialogueCsv.Parse(snapshot.Replace(before, after)));
            Assert.That(ContentMakerDialogueCsv.Export(_source), Is.EqualTo(snapshot));
        }

        [Test]
        public void MissingQuoteAndUnexpectedCellContentAreRejected()
        {
            string csv = ContentMakerDialogueCsv.Export(_source);
            Assert.Throws<FormatException>(() => ContentMakerDialogueCsv.Parse(csv + "\"unterminated"));
            Assert.Throws<FormatException>(() => ContentMakerDialogueCsv.Parse(csv.Replace("\"schema\",", "\"schema\"x,")));
            Assert.Throws<FormatException>(() => ContentMakerDialogueCsv.Parse(csv.Replace("\"schema\",", "schema\"x,")));
            Assert.Throws<FormatException>(() => ContentMakerDialogueCsv.Parse(csv.Replace("\"node\",\"0\",\"\"", "\"node\",\"0\",\"2\"")));
        }

        [Test]
        public void ReferencesUseResolverAndMissingReferenceFailsWholeImport()
        {
            _speaker = ScriptableObject.CreateInstance<SpeakerData>();
            string csv = ContentMakerDialogueCsv.Export(_source).Replace(
                "\"node\",\"0\",\"\",\"\",\"\",\"\",\"Confused\"",
                "\"node\",\"0\",\"\",\"\",\"\",\"Assets/TestSpeaker.asset\",\"Confused\"");
            ContentMakerDialogueImport result = ContentMakerDialogueCsv.Parse(csv, (guid, path, type) =>
            {
                Assert.That(path, Is.EqualTo("Assets/TestSpeaker.asset"));
                Assert.That(type, Is.EqualTo(typeof(SpeakerData)));
                return _speaker;
            });
            Assert.That(result.Nodes[0].Speaker, Is.SameAs(_speaker));
            Assert.Throws<FormatException>(() => ContentMakerDialogueCsv.Parse(csv, (guid, path, type) => null));
            Assert.That(_source.Nodes[0].Speaker, Is.Null);
        }

        [Test]
        public void RuntimeOnlyReferencesCannotBeExportedAsIfTheyWerePersistentAssets()
        {
            _speaker = ScriptableObject.CreateInstance<SpeakerData>();
            _source.Nodes[0].Speaker = _speaker;
            Assert.Throws<InvalidOperationException>(() => ContentMakerDialogueCsv.Export(_source));
        }

        [Test]
        public void CyclicBranchesWarnWithoutMutatingOrRejectingIntentionalDialogueLoops()
        {
            _other = ScriptableObject.CreateInstance<DialogueData>();
            _other.Nodes.Add(new DialogueNode
            {
                IsChoiceNode = true,
                Choices = new List<ChoiceData> { new ChoiceData { ChoiceText = "돌아가기", NextDialogue = _source } }
            });
            _source.Nodes[0].Choices[1].NextDialogue = _other;
            Assert.That(ContentMakerDialogueCsv.GetWarnings(_source, _source.Nodes).Any(value => value.Contains("순환")), Is.True);
            Assert.That(_source.Nodes[0].Choices[1].NextDialogue, Is.SameAs(_other));
        }
    }
}
