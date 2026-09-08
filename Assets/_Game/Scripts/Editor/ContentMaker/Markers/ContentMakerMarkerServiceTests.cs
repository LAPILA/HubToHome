using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace HubToHome.EditorTools.ContentMaker
{
    public sealed class ContentMakerMarkerServiceTests
    {
        private readonly List<Object> _temporary = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            for (int i = _temporary.Count - 1; i >= 0; i--)
                if (_temporary[i] != null) Object.DestroyImmediate(_temporary[i]);
            _temporary.Clear();
        }

        [Test]
        public void BattleChoice_FollowsReachableDialogueBranches()
        {
            DialogueData followup = Dialogue(new DialogueNode
            {
                IsChoiceNode = true,
                Choices = new List<ChoiceData> { new ChoiceData { StartBattleEncounter = true } }
            });
            DialogueData opening = Dialogue(new DialogueNode
            {
                IsChoiceNode = true,
                Choices = new List<ChoiceData> { new ChoiceData { NextDialogue = followup } }
            });

            Assert.That(ContentMakerMarkerService.HasBattleChoice(opening), Is.True);
        }

        [Test]
        public void BattleChoice_DoesNotClaimUnreachableRowsAfterAChoice()
        {
            DialogueData opening = Dialogue(
                new DialogueNode { IsChoiceNode = true, Choices = new List<ChoiceData> { new ChoiceData() } },
                new DialogueNode { IsChoiceNode = true, Choices = new List<ChoiceData> { new ChoiceData { StartBattleEncounter = true } } });

            Assert.That(ContentMakerMarkerService.HasBattleChoice(opening), Is.False);
        }

        [Test]
        public void BattleChoice_EmptyChoiceListDoesNotEndRuntimeDialogue()
        {
            DialogueData opening = Dialogue(
                new DialogueNode { IsChoiceNode = true },
                new DialogueNode { IsChoiceNode = true, Choices = new List<ChoiceData> { new ChoiceData { StartBattleEncounter = true } } });

            Assert.That(ContentMakerMarkerService.HasBattleChoice(opening), Is.True);
        }

        [Test]
        public void BattleChoice_CyclicDialogueReferencesTerminateWithoutChangingData()
        {
            DialogueData first = Dialogue(new DialogueNode { IsChoiceNode = true });
            DialogueData second = Dialogue(new DialogueNode { IsChoiceNode = true });
            first.Nodes[0].Choices.Add(new ChoiceData { NextDialogue = second });
            second.Nodes[0].Choices.Add(new ChoiceData { NextDialogue = first });

            Assert.That(ContentMakerMarkerService.HasBattleChoice(first), Is.False);
            Assert.That(first.Nodes[0].Choices[0].NextDialogue, Is.SameAs(second));
            Assert.That(second.Nodes[0].Choices[0].NextDialogue, Is.SameAs(first));
        }

        [Test]
        public void VisualValidation_RejectsSceneObjectWithoutMutatingIt()
        {
            var visual = new GameObject("scene visual");
            _temporary.Add(visual);
            SpriteRenderer renderer = visual.AddComponent<SpriteRenderer>();

            Assert.That(ContentMakerMarkerService.ValidateVisual(visual), Is.Not.Empty);
            Assert.That(visual.GetComponent<SpriteRenderer>(), Is.SameAs(renderer));
            Assert.That(visual.activeSelf, Is.True);
        }

        [Test]
        public void SpawnPicker_ReturnsSortedUniqueNonemptyIdsWithoutModifyingThem()
        {
            var root = new GameObject("room");
            _temporary.Add(root);
            RoomInstance room = root.AddComponent<RoomInstance>();
            SpawnPoint second = Spawn(root, "b");
            SpawnPoint first = Spawn(root, "a");
            Spawn(root, "b");
            Spawn(root, " ");
            RoomDefinition definition = ScriptableObject.CreateInstance<RoomDefinition>();
            _temporary.Add(definition);
            var serialized = new SerializedObject(definition);
            serialized.FindProperty("_roomPrefab").objectReferenceValue = room;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            CollectionAssert.AreEqual(new[] { "a", "b" }, ContentMakerMarkerService.GetSpawnIds(definition));
            Assert.That(first.SpawnPointId, Is.EqualTo("a"));
            Assert.That(second.SpawnPointId, Is.EqualTo("b"));
        }

        [Test]
        public void NoSelectedRoom_RejectsCreationContext()
        {
            Assert.That(ContentMakerMarkerService.TryGetEditableRoom(null, out RoomInstance room, out string error), Is.False);
            Assert.That(room, Is.Null);
            Assert.That(error, Is.Not.Empty);
        }

        private DialogueData Dialogue(params DialogueNode[] nodes)
        {
            DialogueData data = ScriptableObject.CreateInstance<DialogueData>();
            data.Nodes = new List<DialogueNode>(nodes);
            _temporary.Add(data);
            return data;
        }

        private static SpawnPoint Spawn(GameObject root, string id)
        {
            var child = new GameObject("spawn");
            child.transform.SetParent(root.transform);
            SpawnPoint point = child.AddComponent<SpawnPoint>();
            var serialized = new SerializedObject(point);
            serialized.FindProperty("_spawnPointId").stringValue = id;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return point;
        }
    }
}
