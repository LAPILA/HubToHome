#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using HubToHome.EditorTools.SkillMaker;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public sealed class SkillMakerBlockEditorTests
{
    private string _folder;
    private SkillData _skill;
    private SkillMakerBlockEditor _editor;
    private int _changes;

    [SetUp]
    public void SetUp()
    {
        string folderName = "__SkillMakerBlockTests_" + Guid.NewGuid().ToString("N");
        _folder = "Assets/_Game/Content/" + folderName;
        AssetDatabase.CreateFolder("Assets/_Game/Content", folderName);
        _skill = ScriptableObject.CreateInstance<SkillData>();
        _skill.SkillID = "test.skill_maker.blocks";
        AssetDatabase.CreateAsset(_skill, _folder + "/Skill.asset");
        _editor = new SkillMakerBlockEditor();
        _editor.SetSkill(_skill);
        _editor.Changed += () => _changes++;
    }

    [TearDown]
    public void TearDown()
    {
        _editor?.Dispose();
        if (_skill != null)
            Undo.ClearUndo(_skill);
        if (!string.IsNullOrEmpty(_folder) && AssetDatabase.IsValidFolder(_folder))
            AssetDatabase.DeleteAsset(_folder);
    }

    [Test]
    public void InsertUndoRedoRestoresManagedReferenceBlock()
    {
        Assert.That(_editor.Insert(0, typeof(Action_Wait)), Is.Zero);
        Assert.That(_skill.ActionTimeline[0], Is.TypeOf<Action_Wait>());
        Assert.That(_changes, Is.EqualTo(1));

        Undo.PerformUndo();
        _editor.Invalidate();
        Assert.That(_skill.ActionTimeline, Is.Empty);

        Undo.PerformRedo();
        _editor.Invalidate();
        Assert.That(_skill.ActionTimeline[0], Is.TypeOf<Action_Wait>());
    }

    [Test]
    public void DuplicateCopiesQteNodesWithoutSharingTheList()
    {
        var source = new Action_QTE
        {
            DesignerLabel = "입력 확인",
            Nodes = new List<SkillQTENode>
            {
                new SkillQTENode { TargetKey = "z", PosX = 0.2f, PosY = 0.3f }
            }
        };
        _skill.ActionTimeline.Add(source);

        Assert.That(_editor.Duplicate(0), Is.EqualTo(1));
        var duplicate = (Action_QTE)_skill.ActionTimeline[1];
        Assert.That(duplicate, Is.Not.SameAs(source));
        Assert.That(duplicate.Nodes, Is.Not.SameAs(source.Nodes));
        Assert.That(duplicate.DesignerLabel, Is.EqualTo(source.DesignerLabel));
        SkillQTENode changedNode = duplicate.Nodes[0];
        changedNode.TargetKey = "x";
        duplicate.Nodes[0] = changedNode;
        Assert.That(source.Nodes[0].TargetKey, Is.EqualTo("z"));

        Undo.PerformUndo();
        _editor.Invalidate();
        Assert.That(_skill.ActionTimeline.Count, Is.EqualTo(1));
        Assert.That(((Action_QTE)_skill.ActionTimeline[0]).Nodes[0].TargetKey, Is.EqualTo("z"));
    }

    [Test]
    public void DuplicateSeparatesNestedObjectsButRetainsUnityAssetReferences()
    {
        var source = new NestedTestBlock
        {
            Payload = new NestedPayload { Label = "원본", Values = new List<int> { 1, 2 } },
            LinkedAsset = _skill
        };
        _skill.ActionTimeline.Add(source);

        Assert.That(_editor.Duplicate(0), Is.EqualTo(1));
        var duplicate = (NestedTestBlock)_skill.ActionTimeline[1];
        Assert.That(duplicate.Payload, Is.Not.SameAs(source.Payload));
        Assert.That(duplicate.Payload.Values, Is.Not.SameAs(source.Payload.Values));
        Assert.That(duplicate.LinkedAsset, Is.SameAs(_skill));
        duplicate.Payload.Label = "복제본";
        duplicate.Payload.Values[0] = 99;
        Assert.That(source.Payload.Label, Is.EqualTo("원본"));
        Assert.That(source.Payload.Values[0], Is.EqualTo(1));
    }

    [Test]
    public void MoveUsesFinalIndexAndUndoRestoresOrder()
    {
        _skill.ActionTimeline.Add(new Action_Wait());
        _skill.ActionTimeline.Add(new Action_Move());
        _skill.ActionTimeline.Add(new Action_Damage());

        Assert.That(_editor.Move(0, 2), Is.EqualTo(2));
        Assert.That(_skill.ActionTimeline[0], Is.TypeOf<Action_Move>());
        Assert.That(_skill.ActionTimeline[2], Is.TypeOf<Action_Wait>());

        Undo.PerformUndo();
        _editor.Invalidate();
        Assert.That(_skill.ActionTimeline[0], Is.TypeOf<Action_Wait>());
        Assert.That(_skill.ActionTimeline[1], Is.TypeOf<Action_Move>());
        Assert.That(_skill.ActionTimeline[2], Is.TypeOf<Action_Damage>());
    }

    [Test]
    public void RemoveSupportsMissingBlocksAndRestoresThemWithUndo()
    {
        _skill.ActionTimeline.Add(new Action_Wait());
        _skill.ActionTimeline.Add(null);

        Assert.That(_editor.Duplicate(1), Is.EqualTo(-1));
        Assert.That(_editor.Remove(1), Is.Zero);
        Assert.That(_skill.ActionTimeline.Count, Is.EqualTo(1));

        Undo.PerformUndo();
        _editor.Invalidate();
        Assert.That(_skill.ActionTimeline.Count, Is.EqualTo(2));
        Assert.That(_skill.ActionTimeline[1], Is.Null);
    }

    [Test]
    public void InvalidOperationsDoNotChangeTheAssetOrRaiseChanged()
    {
        _skill.ActionTimeline.Add(new Action_Wait());
        Assert.That(_editor.Insert(-1, typeof(Action_Move)), Is.EqualTo(-1));
        Assert.That(_editor.Insert(2, typeof(Action_Move)), Is.EqualTo(-1));
        Assert.That(_editor.Insert(0, typeof(SkillActionBlock)), Is.EqualTo(-1));
        Assert.That(_editor.Insert(0, typeof(string)), Is.EqualTo(-1));
        Assert.That(_editor.Remove(1), Is.EqualTo(-1));
        Assert.That(_editor.Duplicate(-1), Is.EqualTo(-1));
        Assert.That(_editor.Move(0, 1), Is.EqualTo(-1));
        Assert.That(_editor.Move(0, 0), Is.Zero);
        Assert.That(_skill.ActionTimeline.Count, Is.EqualTo(1));
        Assert.That(_changes, Is.Zero);
    }

    [Test]
    public void TransientAndDisposedTargetsCannotBeModified()
    {
        SkillData transient = ScriptableObject.CreateInstance<SkillData>();
        try
        {
            _editor.SetSkill(transient);
            Assert.That(_editor.Insert(0, typeof(Action_Wait)), Is.EqualTo(-1));
            Assert.That(transient.ActionTimeline, Is.Empty);

            _editor.SetSkill(_skill);
            _editor.Dispose();
            _editor.Dispose();
            Assert.That(_editor.Insert(0, typeof(Action_Wait)), Is.EqualTo(-1));
            Assert.That(_skill.ActionTimeline, Is.Empty);
        }
        finally
        {
            Object.DestroyImmediate(transient);
        }
    }

    [Test]
    public void DeletedAssetCannotBeModified()
    {
        AssetDatabase.DeleteAsset(_folder + "/Skill.asset");
        _editor.Invalidate();
        Assert.That(_editor.Insert(0, typeof(Action_Wait)), Is.EqualTo(-1));
        Assert.That(_changes, Is.Zero);
    }

    [Test]
    public void NullTimelineCanBeInitializedAndLastRemovalClearsSelection()
    {
        _skill.ActionTimeline = null;
        Assert.That(_editor.Insert(0, typeof(Action_Wait)), Is.Zero);
        Assert.That(_editor.Remove(0), Is.EqualTo(-1));
        Assert.That(_skill.ActionTimeline, Is.Empty);
        Assert.That(_changes, Is.EqualTo(2));
    }

    [Test]
    public void BlockCatalogIsCachedAndUsesExistingKoreanNames()
    {
        Assert.That(SkillMakerBlockEditor.BlockTypes, Is.SameAs(SkillMakerBlockEditor.BlockTypes));
        Assert.That(SkillMakerBlockEditor.BlockTypes, Does.Contain(typeof(Action_DefenseWindow)));
        Assert.That(SkillMakerBlockEditor.BlockTypes, Has.No.Member(typeof(SkillActionBlock)));
        Assert.That(SkillMakerBlockEditor.BlockTypes, Has.No.Member(typeof(NestedTestBlock)));
        Assert.That(SkillMakerBlockEditor.GetBlockName(typeof(Action_DefenseWindow)), Is.EqualTo(new Action_DefenseWindow().BlockName));
        Assert.That(SkillMakerBlockEditor.GetBlockName(typeof(Action_Damage)), Is.EqualTo(new Action_Damage().BlockName));
    }

    [Serializable]
    private sealed class NestedTestBlock : SkillActionBlock
    {
        public NestedPayload Payload;
        public Object LinkedAsset;
        public override IEnumerator Execute(SkillContext context) { yield break; }
    }

    [Serializable]
    private sealed class NestedPayload
    {
        public string Label;
        public List<int> Values;
    }
}
#endif
