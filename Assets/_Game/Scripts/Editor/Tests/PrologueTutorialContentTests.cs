using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

/// <summary>읽기 전용 작성 데이터 검사. 생성·Scene 변경·Play를 수행하지 않습니다.</summary>
public sealed class PrologueTutorialContentTests
{
    [Test]
    public void DirtyAssetInspectionDoesNotReadSceneContents()
    {
        Assert.DoesNotThrow(() => PrologueTutorialOpeningPatch.RequireNoDirtyAssets(new List<string>
        {
            PrologueTutorialContentBuilder.RegionScene,
            PrologueTutorialContentBuilder.IntroScene,
            "Assets/UnusedUppercase.UNITY"
        }));
    }

    [Test]
    public void BothConductorDirectionsPointRightInAllFourLanguages()
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        string[] directions = { "오른쪽", "right", "右", "右" };
        foreach (string[] row in LocalizationCsv.Parse(File.ReadAllText("Assets/Resources/LocalizationTable.csv", PrologueTutorialContentBuilder.StrictUtf8)))
        {
            if (row.Length != 5 || (row[0] != "HTH_PROLOGUE_DLG_INTRODUCTION_05"
                && row[0] != "HTH_PROLOGUE_DLG_OPTIONAL_CONDUCTOR_01")) continue;
            Assert.That(seen.Add(row[0]), Is.True);
            for (int i = 0; i < directions.Length; i++) StringAssert.Contains(directions[i], row[i + 1], row[0]);
        }
        Assert.That(seen.Count, Is.EqualTo(2));
    }

    [Test]
    public void DerivedCacheExceptionRequiresExactFrameOrderCountAndTime()
    {
        var original=new[]{new IllustratedStoryFrame{Time=0f},new IllustratedStoryFrame{Time=.1f}};
        var same=new[]{new IllustratedStoryFrame{Time=0f},new IllustratedStoryFrame{Time=.1f}};
        Assert.That(PrologueTutorialOpeningPatch.HaveIdenticalFrames(original,same),Is.True);
        same[1].Time=.10001f;
        Assert.That(PrologueTutorialOpeningPatch.HaveIdenticalFrames(original,same),Is.False,"No timing tolerance for authored cache");
        Assert.That(PrologueTutorialOpeningPatch.HaveIdenticalFrames(original,new[]{new IllustratedStoryFrame{Time=0f}}),Is.False);
        Assert.That(PrologueTutorialOpeningPatch.HaveIdenticalFrames(original,new[]{new IllustratedStoryFrame{Time=.1f},new IllustratedStoryFrame{Time=0f}}),Is.False);
        Assert.That(PrologueTutorialOpeningPatch.HaveIdenticalFrames(original,new IllustratedStoryFrame[]{new IllustratedStoryFrame{Time=0f},null}),Is.False);
    }

    [Test]
    public void EveryOwnedLocalizationRowHasExactlyFourNonemptyTranslationsAndUniqueKey()
    {
        var keys=new HashSet<string>(StringComparer.Ordinal);
        foreach(string[] row in LocalizationCsv.Parse(File.ReadAllText("Assets/Resources/LocalizationTable.csv",PrologueTutorialContentBuilder.StrictUtf8)))
        {
            if(row.Length==0 || !row[0].StartsWith("HTH_PROLOGUE_",StringComparison.Ordinal))continue;
            Assert.That(row.Length,Is.EqualTo(5),row[0]);
            Assert.That(keys.Add(row[0]),Is.True,"Duplicate: "+row[0]);
            for(int column=1;column<5;column++)Assert.That(string.IsNullOrWhiteSpace(row[column]),Is.False,row[0]+"/"+column);
        }
        Assert.That(keys.Count,Is.GreaterThanOrEqualTo(60));
    }

    [TestCase("incident")][TestCase("rescue")][TestCase("seal")]
    [TestCase("forward")][TestCase("control")][TestCase("derail")]
    public void SceneSequencesParseAndKeepTheirDialogueAtTheSameWaitableBoundary(string id)
    {
        string path=PrologueTutorialAssetFactory.SequenceSourceRoot+id+".sequence.yaml";
        ActionSequenceSourceImportResult result=ActionSequenceSourceSync.Import(File.ReadAllText(path),path);
        try
        {
            Assert.That(result.Success,Is.True,id);
            Assert.That(result.Sequence.SequenceId,Is.EqualTo("prologue.tutorial."+id));
            Assert.That(ScenarioBlockIdentity.TryValidateUnique(result.Sequence.Actions,out string error),Is.True,error);
            int dialogue=0;
            foreach(ScenarioActionData action in result.Sequence.Actions)
                if(action.ActionId=="dialogue.wait") {dialogue++;StringAssert.Contains("\""+id+"\"",action.ParametersJson);}
            Assert.That(dialogue,Is.EqualTo(1));
        }
        finally {if(result.Sequence!=null)UnityEngine.Object.DestroyImmediate(result.Sequence);}
    }

    [Test]
    public void AuthoringPathPolicyRejectsUnrelatedAndTraversalPaths()
    {
        Assert.That(PrologueTutorialContentBuilder.IsOwnedPath("Assets/_Game/Content/Maps/Regions/Chapter01/Room.asset"),Is.False);
        Assert.That(PrologueTutorialContentBuilder.IsOwnedPath(PrologueTutorialContentBuilder.Root+"/../User.asset"),Is.False);
        Assert.That(PrologueTutorialContentBuilder.IsOwnedPath(PrologueTutorialContentBuilder.DefinitionPath),Is.True);
        Assert.That(PrologueTutorialContentBuilder.IsOwnedPath(PrologueTutorialContentBuilder.OriginalRoom),Is.True);
    }

    [Test]
    public void GeneratedProfileContainsReplaceableRolesRoomsAndCompleteDialogueBindings()
    {
        PrologueTutorialDefinition definition=AssetDatabase.LoadAssetAtPath<PrologueTutorialDefinition>(PrologueTutorialContentBuilder.DefinitionPath);
        if(definition==null)Assert.Ignore("Explicit content authoring has not run yet.");
        foreach(string role in new[]{"protagonist","companion_dragon","companion_deer","companion_medic","crew","white_entity","conductor"})
        { Assert.That(definition.GetRole(role)?.Speaker,Is.Not.Null,role);Assert.That(definition.GetRole(role)?.VisualPrefab,Is.Not.Null,role); }
        foreach(PrologueTutorialRoom room in Enum.GetValues(typeof(PrologueTutorialRoom)))
        { Assert.That(definition.GetRoom(room)?.IsValid,Is.True,room.ToString()); }
        Assert.That(definition.HealingItem.UsableInOverworld,Is.False);
        Assert.That(definition.HealingItem.UsableInBattle,Is.True);
        Assert.That(definition.HealingItem.IsSellable,Is.False);
        Assert.That(definition.Enemy.Kind,Is.EqualTo(EnemyKind.WhiteEntity));
        Assert.That(definition.Enemy.EXPReward,Is.Zero);
        foreach(PrologueTutorialDialogueBinding binding in definition.Dialogues)
        {
            Assert.That(binding.Dialogue,Is.Not.Null,binding.Id);
            foreach(DialogueNode node in binding.Dialogue.Nodes)
                Assert.That(node.LocalizationKey,Does.StartWith("HTH_PROLOGUE_"),binding.Id);
        }
    }

    [Test]
    public void WindowRestorationKeepsOriginalThreeLinesAndAnimationFrames()
    {
        PrologueTutorialDefinition definition=AssetDatabase.LoadAssetAtPath<PrologueTutorialDefinition>(PrologueTutorialContentBuilder.DefinitionPath);
        if(definition==null)Assert.Ignore("Explicit content authoring has not run yet.");
        IllustratedStoryAsset original=AssetDatabase.LoadAssetAtPath<IllustratedStoryAsset>(PrologueTutorialContentBuilder.RegionRoot+"/Data/Prologue/Prologue_Opening.asset");
        Assert.That(definition.OpeningStory.Pages.Count,Is.EqualTo(1));
        IllustratedStoryPage page=definition.OpeningStory.Pages[0];
        Assert.That(page.Animation,Is.SameAs(original.Pages[0].Animation));
        Assert.That(page.LoopAnimation,Is.True);
        Assert.That(page.FullScreen,Is.False);
        Assert.That(page.Frames.Length,Is.EqualTo(original.Pages[0].Frames.Length));
        for(int i=0;i<page.Frames.Length;i++)
        { Assert.That(page.Frames[i].Sprite,Is.SameAs(original.Pages[0].Frames[i].Sprite));Assert.That(page.Frames[i].Time,Is.EqualTo(original.Pages[0].Frames[i].Time)); }
        Assert.That(page.Dialogue,Is.SameAs(definition.GetDialogue("window")));
        Assert.That(page.MuteTypingSound,Is.False);
        Assert.That(page.Dialogue.Style,Is.EqualTo(DialogueStyle.Cinematic));
        Assert.That(page.Dialogue.Nodes.Count,Is.EqualTo(3));
        string[] expected={"밖이 하나도 안 보이네.","첫 원정대도\n여길 지나갔겠지.","다들 무사해야 할 텐데."};
        for(int i=0;i<expected.Length;i++)
        {
            DialogueNode node=page.Dialogue.Nodes[i];
            Assert.That(node.DefaultText,Is.EqualTo(expected[i]));
            Assert.That(node.LocalizationKey,Is.EqualTo("HTH_PROLOGUE_WINDOW_"+(i+1).ToString("00")));
            Assert.That(node.Speaker,Is.SameAs(definition.GetRole("protagonist").Speaker));
        }
        Assert.That(definition.GetDialogue("introduction").Nodes.Count,Is.EqualTo(5));
        Assert.That(definition.GetDialogue("optional_conductor").Nodes.Count,Is.EqualTo(1));
        Assert.That(definition.Introduction.RoleId,Is.EqualTo("conductor"));
        Assert.That(definition.Introduction.DialogueId,Is.EqualTo("introduction"));
    }

    [Test]
    public void EveryCarriageDoorUsesContactAndOnlyRepairReplacesItsCrewWithConductor()
    {
        PrologueTutorialDefinition definition=AssetDatabase.LoadAssetAtPath<PrologueTutorialDefinition>(PrologueTutorialContentBuilder.DefinitionPath);
        if(definition==null)Assert.Ignore("Explicit content authoring has not run yet.");
        int totalDoors=0;
        foreach(PrologueTutorialRoomBinding binding in definition.Rooms)
        {
            GameObject room=binding.Definition.RoomPrefab.gameObject;
            foreach(PrologueTutorialDoor door in room.GetComponentsInChildren<PrologueTutorialDoor>(true))
            { Assert.That(new SerializedObject(door).FindProperty("_enterOnTrigger").boolValue,Is.True,binding.Room.ToString());totalDoors++; }
            int conductor=0,crew=0;
            foreach(PrologueTutorialRoleAnchor role in room.GetComponentsInChildren<PrologueTutorialRoleAnchor>(true))
            { if(role.RoleId=="conductor")conductor++;if(role.RoleId=="crew")crew++; }
            Assert.That(conductor,Is.EqualTo(binding.Room==PrologueTutorialRoom.Repair?1:0));
            if(binding.Room==PrologueTutorialRoom.Repair)Assert.That(crew,Is.Zero);
            if(binding.Room==PrologueTutorialRoom.Control)Assert.That(crew,Is.EqualTo(1));
        }
        Assert.That(totalDoors,Is.EqualTo(6));
    }

    [Test]
    public void MainRouteMovesRightAndResumesAfterSealingWithoutReturningToRepair()
    {
        PrologueTutorialDefinition definition=AssetDatabase.LoadAssetAtPath<PrologueTutorialDefinition>(PrologueTutorialContentBuilder.DefinitionPath);
        if(definition==null)Assert.Ignore("Explicit content authoring has not run yet.");
        AssertRoute(definition,PrologueTutorialRoom.Repair,PrologueTutorialRoom.Common,true,"from_left");
        AssertRoute(definition,PrologueTutorialRoom.Common,PrologueTutorialRoom.Repair,false,"from_right");
        AssertRoute(definition,PrologueTutorialRoom.Common,PrologueTutorialRoom.Rear,true,"default");
        AssertRoute(definition,PrologueTutorialRoom.Rear,PrologueTutorialRoom.Common,false,"from_right");
        AssertRoute(definition,PrologueTutorialRoom.Rear,PrologueTutorialRoom.Control,true,"default");
        AssertRoute(definition,PrologueTutorialRoom.Control,PrologueTutorialRoom.Rear,false,"from_right");
        bool foundSeal=false;
        foreach(PrologueTutorialResumePoint point in definition.ResumePoints)
            if(point.CompletedFlag==PrologueTutorialProgress.SealCompleted) {
                Assert.That(point.Room,Is.EqualTo(PrologueTutorialRoom.Rear));
                Assert.That(point.SpawnPointId,Is.EqualTo("after_withdrawal"));foundSeal=true;
            }
        Assert.That(foundSeal);
        foreach(PrologueTutorialRoom kind in new[]{PrologueTutorialRoom.Common,PrologueTutorialRoom.Rear,PrologueTutorialRoom.Control})
            AssertLeftEntry(definition.GetRoom(kind).RoomPrefab.gameObject,"default");
        AssertLeftEntry(definition.GetRoom(PrologueTutorialRoom.Repair).RoomPrefab.gameObject,"tutorial_repair");
        AssertLeftEntry(definition.GetRoom(PrologueTutorialRoom.Common).RoomPrefab.gameObject,"after_incident");
    }

    [Test]
    public void OnlyTutorialOwnedDialoguesAreAuthoredForSinglePressConfirm()
    {
        PrologueTutorialDefinition definition=AssetDatabase.LoadAssetAtPath<PrologueTutorialDefinition>(PrologueTutorialContentBuilder.DefinitionPath);
        if(definition==null)Assert.Ignore("Explicit content authoring has not run yet.");
        Assert.That(definition.Dialogues.Count,Is.EqualTo(27));
        foreach(PrologueTutorialDialogueBinding binding in definition.Dialogues)
            Assert.That(binding.Dialogue.RevealTextImmediately,Is.True,binding.Id);
        DialogueData introduction=definition.GetDialogue("introduction");
        Assert.That(introduction.Nodes.Find(node=>node.LocalizationKey=="HTH_PROLOGUE_DLG_INTRODUCTION_05").DefaultText,
            Does.Contain("오른쪽"));
    }

    private static void AssertRoute(PrologueTutorialDefinition definition,PrologueTutorialRoom from,
        PrologueTutorialRoom to,bool right,string spawn)
    {
        int found=0;
        foreach(PrologueTutorialDoor door in definition.GetRoom(from).RoomPrefab.GetComponentsInChildren<PrologueTutorialDoor>(true))
        {
            var data=new SerializedObject(door);
            if(data.FindProperty("_targetRoom").enumValueIndex!=(int)to)continue;
            if(right)Assert.That(door.transform.localPosition.x,Is.GreaterThan(0f),from+" → "+to);
            else Assert.That(door.transform.localPosition.x,Is.LessThan(0f),from+" → "+to);
            Assert.That(data.FindProperty("_spawnPointId").stringValue,Is.EqualTo(spawn));found++;
        }
        Assert.That(found,Is.EqualTo(1),from+" → "+to);
    }

    private static void AssertLeftEntry(GameObject room,string id)
    {
        int found=0;
        foreach(SpawnPoint point in room.GetComponentsInChildren<SpawnPoint>(true))
            if(point.SpawnPointId==id) {
                Assert.That(point.transform.localPosition.x,Is.LessThan(0f),id);
                Assert.That(point.DefaultFacing,Is.EqualTo(FacingDirection.Right));found++;
            }
        Assert.That(found,Is.EqualTo(1),id);
    }
}
