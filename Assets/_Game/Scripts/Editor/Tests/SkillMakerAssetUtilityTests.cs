#if UNITY_EDITOR
using System;
using System.IO;
using System.Text;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace HubToHome.EditorTools.SkillMaker
{
    internal sealed class SkillMakerAssetUtilityTests
    {
        private const string TestPrefix = "Assets/_Game/Content/__SkillMakerTests_";
        private string _folder;

        [SetUp]
        public void SetUp() => _folder = TestPrefix + Guid.NewGuid().ToString("N");

        [TearDown]
        public void TearDown()
        {
            // Only this test's uniquely named content directory may be removed.
            if (_folder != null && _folder.StartsWith(TestPrefix, StringComparison.Ordinal)
                && _folder.Length == TestPrefix.Length + 32 && AssetDatabase.IsValidFolder(_folder))
                AssetDatabase.DeleteAsset(_folder);
        }

        [Test]
        public void MatchesSearchesNameIdAndPathWithExactUsageFilter()
        {
            SkillData skill = SkillMakerAssetUtility.CreateAtPath(_folder + "/WindmillStrike.asset");
            skill.SkillName = "압력 베기";
            skill.SkillID = "skill.pressure_cut";
            skill.UsageProfile = SkillUsageProfile.PlayerOnly;
            Assert.That(SkillMakerAssetUtility.Matches(skill, " 압력 ", null), Is.True);
            Assert.That(SkillMakerAssetUtility.Matches(skill, "PRESSURE_CUT", SkillUsageProfile.PlayerOnly), Is.True);
            Assert.That(SkillMakerAssetUtility.Matches(skill, "windmillstrike.asset", null), Is.True);
            Assert.That(SkillMakerAssetUtility.Matches(skill, "", SkillUsageProfile.EnemyOnly), Is.False);
            Assert.That(SkillMakerAssetUtility.Matches(skill, "없는 이름", null), Is.False);
            Assert.That(SkillMakerAssetUtility.Matches(null, "", null), Is.False);
        }

        [Test]
        public void FindSkillsIncludesContentAssetsSortedByDisplayName()
        {
            SkillData second = SkillMakerAssetUtility.CreateAtPath(_folder + "/B.asset");
            SkillData first = SkillMakerAssetUtility.CreateAtPath(_folder + "/A.asset");
            first.SkillName = "AAA Skill Maker";
            second.SkillName = "ZZZ Skill Maker";
            var skills = SkillMakerAssetUtility.FindSkills();
            Assert.That(skills.IndexOf(first), Is.GreaterThanOrEqualTo(0));
            Assert.That(skills.IndexOf(second), Is.GreaterThan(skills.IndexOf(first)));
        }

        [TestCase("Assets/_Game/Content/../Scripts/Skill.asset")]
        [TestCase("Assets/_Game/ContentBackup/Skill.asset")]
        [TestCase("Assets/_Game/Content//Skill.asset")]
        [TestCase("Assets/_Game/Content/Skill.prefab")]
        [TestCase("C:/Outside/Skill.asset")]
        [TestCase("")]
        public void CreateRejectsUnsafePathsBeforeWriting(string path)
        {
            Assert.Throws<ArgumentException>(() => SkillMakerAssetUtility.CreateAtPath(path));
            Assert.That(AssetDatabase.IsValidFolder(_folder), Is.False);
        }

        [Test]
        public void CreateRejectsExistingAssetWithoutChangingItsIdentity()
        {
            string path = _folder + "/Existing.asset";
            SkillData original = SkillMakerAssetUtility.CreateAtPath(path);
            string id = original.SkillID;
            string guid = AssetDatabase.AssetPathToGUID(path);
            Assert.Throws<IOException>(() => SkillMakerAssetUtility.CreateAtPath(path));
            Assert.That(AssetDatabase.LoadAssetAtPath<SkillData>(path), Is.SameAs(original));
            Assert.That(original.SkillID, Is.EqualTo(id));
            Assert.That(AssetDatabase.AssetPathToGUID(path), Is.EqualTo(guid));
        }

        [Test]
        public void CreateRejectsOrphanMetaWithoutOverwritingIt()
        {
            SkillMakerAssetUtility.CreateAtPath(_folder + "/Seed.asset");
            string path = _folder + "/Orphan.asset";
            string metaPath = Path.GetFullPath(Path.Combine(Application.dataPath, "..", path + ".meta"));
            string meta = "fileFormatVersion: 2\nguid: " + Guid.NewGuid().ToString("N") + "\n";
            File.WriteAllText(metaPath, meta, new UTF8Encoding(false));
            Assert.Throws<IOException>(() => SkillMakerAssetUtility.CreateAtPath(path));
            Assert.That(File.ReadAllText(metaPath, Encoding.UTF8), Is.EqualTo(meta));
            Assert.That(File.Exists(metaPath.Substring(0, metaPath.Length - 5)), Is.False);
        }

        [Test]
        public void CreateAllocatesUniqueIdsEvenForEqualFileNamesInDifferentFolders()
        {
            SkillData first = SkillMakerAssetUtility.CreateAtPath(_folder + "/First/Same.asset");
            SkillData second = SkillMakerAssetUtility.CreateAtPath(_folder + "/Second/Same.asset");
            Assert.That(first.SkillID, Is.Not.Empty);
            Assert.That(second.SkillID, Is.Not.EqualTo(first.SkillID));
        }

        [Test]
        public void CreateCanInitializeCounterTemplateBeforeFirstSave()
        {
            bool wasTransientDuringInitialization = false;
            SkillData skill = SkillMakerAssetUtility.CreateAtPath(_folder + "/Counter.asset", created =>
            {
                wasTransientDuringInitialization = !EditorUtility.IsPersistent(created);
                created.UsageProfile = SkillUsageProfile.EnemyOnly;
                created.ActionTimeline = EnemyAttackTemplateFactory.CreateCounterableStrike();
            });

            Assert.That(wasTransientDuringInitialization, Is.True);
            Assert.That(EditorUtility.IsPersistent(skill), Is.True);
            Assert.That(skill.UsageProfile, Is.EqualTo(SkillUsageProfile.EnemyOnly));
            Assert.That(((Action_DefenseWindow)skill.ActionTimeline[1]).Requirement, Is.EqualTo(DefenseRequirement.Counterable));
            Assert.That(EditorUtility.IsDirty(skill), Is.False);
        }

        [Test]
        public void FailedTemplateInitializationDoesNotCreatePartialSkillAsset()
        {
            string path = _folder + "/Failed.asset";
            Assert.Throws<InvalidOperationException>(() => SkillMakerAssetUtility.CreateAtPath(path,
                _ => throw new InvalidOperationException("template failure")));

            Assert.That(AssetDatabase.LoadAssetAtPath<SkillData>(path), Is.Null);
            Assert.That(File.Exists(Path.GetFullPath(Path.Combine(Application.dataPath, "..", path))), Is.False);
        }

        [Test]
        public void DuplicatePreservesManagedReferenceBlocksWithoutSharingNestedLists()
        {
            SkillData source = SkillMakerAssetUtility.CreateAtPath(_folder + "/Source.asset");
            source.SkillName = "원본 이름";
            source.Description = "효과와 연출 보존";
            var block = new Action_QTE { DesignerLabel = "연속 입력" };
            block.Nodes.Add(new SkillQTENode { PosX = 0.3f, PosY = 0.6f, TargetKey = "x" });
            source.ActionTimeline.Add(block);
            EditorUtility.SetDirty(source);
            SkillMakerAssetUtility.Save(source);

            SkillData copy = SkillMakerAssetUtility.DuplicateAtPath(source, _folder + "/Copy.asset");
            Assert.That(copy.SkillID, Is.Not.EqualTo(source.SkillID));
            Assert.That(copy.SkillName, Is.EqualTo(source.SkillName));
            Assert.That(copy.Description, Is.EqualTo(source.Description));
            Assert.That(copy.ActionTimeline[0], Is.TypeOf<Action_QTE>());
            var copiedBlock = (Action_QTE)copy.ActionTimeline[0];
            Assert.That(copiedBlock, Is.Not.SameAs(block));
            Assert.That(copiedBlock.Nodes, Is.Not.SameAs(block.Nodes));
            Assert.That(copiedBlock.Nodes[0].TargetKey, Is.EqualTo("x"));
            copiedBlock.Nodes.Clear();
            Assert.That(block.Nodes.Count, Is.EqualTo(1));
        }

        [Test]
        public void DuplicateRejectsDirtySourceInsteadOfLosingItsUnsavedChanges()
        {
            SkillData source = SkillMakerAssetUtility.CreateAtPath(_folder + "/Source.asset");
            source.Description = "아직 저장하지 않은 내용";
            EditorUtility.SetDirty(source);
            string target = _folder + "/Copy.asset";
            Assert.Throws<InvalidOperationException>(() => SkillMakerAssetUtility.DuplicateAtPath(source, target));
            Assert.That(AssetDatabase.LoadAssetAtPath<SkillData>(target), Is.Null);
            Assert.That(EditorUtility.IsDirty(source), Is.True);
        }

        [Test]
        public void SaveOnlyCommitsTheSelectedSkill()
        {
            SkillData selected = SkillMakerAssetUtility.CreateAtPath(_folder + "/Selected.asset");
            SkillData other = SkillMakerAssetUtility.CreateAtPath(_folder + "/Other.asset");
            selected.Description = "선택한 스킬 변경";
            other.Description = "다른 스킬의 미저장 변경";
            EditorUtility.SetDirty(selected);
            EditorUtility.SetDirty(other);
            SkillMakerAssetUtility.Save(selected);
            Assert.That(EditorUtility.IsDirty(selected), Is.False);
            Assert.That(EditorUtility.IsDirty(other), Is.True);
        }

        [Test]
        public void CanEditRejectsTransientObjectsWithoutSavingThem()
        {
            SkillData transient = ScriptableObject.CreateInstance<SkillData>();
            try
            {
                Assert.That(SkillMakerAssetUtility.CanEdit(transient, out string reason), Is.False);
                Assert.That(reason, Is.Not.Empty);
                Assert.Throws<InvalidOperationException>(() => SkillMakerAssetUtility.Save(transient));
            }
            finally { Object.DestroyImmediate(transient); }
        }
    }
}
#endif
