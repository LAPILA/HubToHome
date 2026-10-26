#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;
using Object = UnityEngine.Object;

internal static class PrologueTutorialRoomFactory
{
    private const string Root=PrologueTutorialContentBuilder.Root;
    internal static void Build(PrologueTutorialDefinition definition)
    {
        var rooms=new Dictionary<PrologueTutorialRoom,RoomDefinition>();
        rooms[PrologueTutorialRoom.Repair]=PrologueTutorialContentBuilder.Require<RoomDefinition>(PrologueTutorialContentBuilder.RegionRoot+"/Data/Rooms/Room_ALL_TrainQ0.asset");
        foreach(PrologueTutorialRoom kind in new[]{PrologueTutorialRoom.Common,PrologueTutorialRoom.Rear,PrologueTutorialRoom.Control,PrologueTutorialRoom.Completed})
        {
            RoomDefinition room=PrologueTutorialContentBuilder.Asset<RoomDefinition>(Root+"/Rooms/Room_"+kind+".asset");
            AreaDefinition area=PrologueTutorialContentBuilder.Asset<AreaDefinition>(Root+"/Rooms/Area_"+kind+".asset");
            PrologueTutorialContentBuilder.Set(room,"_roomId",p=>p.stringValue=RoomId(kind));
            PrologueTutorialContentBuilder.Set(room,"_areaDefinition",p=>p.objectReferenceValue=area);
            PrologueTutorialContentBuilder.Set(area,"_areaId",p=>p.stringValue=RoomId(kind));
            PrologueTutorialContentBuilder.Set(area,"_roomDefinition",p=>p.objectReferenceValue=room);
            PrologueTutorialContentBuilder.Set(area,"_description",p=>p.stringValue="프롤로그 교체형 샘플 객차 · "+kind);
            PrologueTutorialContentBuilder.Save(area);
            rooms.Add(kind,room);
        }
        foreach(var pair in rooms) BuildRoom(pair.Key,pair.Value,definition);
        definition.Rooms.Clear();
        foreach(var pair in rooms) definition.Rooms.Add(new PrologueTutorialRoomBinding { Room=pair.Key,Definition=pair.Value,
            SpawnPointId=pair.Key==PrologueTutorialRoom.Repair?"tutorial_repair":"default" });
        definition.ResumePoints=new List<PrologueTutorialResumePoint> {
            Resume("",PrologueTutorialRoom.Repair,"tutorial_repair"),
            Resume(PrologueTutorialProgress.InspectCompleted,PrologueTutorialRoom.Repair,"tutorial_repair"),
            Resume(PrologueTutorialProgress.ValveCompleted,PrologueTutorialRoom.Repair,"tutorial_repair"),
            Resume(PrologueTutorialProgress.RepairCompleted,PrologueTutorialRoom.Repair,"tutorial_rest"),
            Resume(PrologueTutorialProgress.IncidentCompleted,PrologueTutorialRoom.Common,"after_incident"),
            Resume(PrologueTutorialProgress.RearCompleted,PrologueTutorialRoom.Rear,"default"),
            Resume(PrologueTutorialProgress.WithdrawalCompleted,PrologueTutorialRoom.Rear,"after_withdrawal"),
            Resume(PrologueTutorialProgress.RescueCompleted,PrologueTutorialRoom.Rear,"after_withdrawal"),
            Resume(PrologueTutorialProgress.SealCompleted,PrologueTutorialRoom.Rear,"after_withdrawal"),
            Resume(PrologueTutorialProgress.ForwardCompleted,PrologueTutorialRoom.Control,"default"),
            Resume(PrologueTutorialProgress.ControlCompleted,PrologueTutorialRoom.Control,"after_control"),
            Resume(PrologueTutorialProgress.Completed,PrologueTutorialRoom.Completed,"default")
        };
        PrologueTutorialContentBuilder.Save(definition);
    }

    private static void BuildRoom(PrologueTutorialRoom kind,RoomDefinition data,PrologueTutorialDefinition definition)
    {
        string path=kind==PrologueTutorialRoom.Repair?PrologueTutorialContentBuilder.OriginalRoom:
            PrologueTutorialContentBuilder.RegionRoot+"/Prefabs/Rooms/Room_ALL_TrainTutorial_"+kind+".prefab";
        // Clone only the authored Q0 template. Its original terrain and chest remain intact in Repair.
        GameObject room=PrefabUtility.LoadPrefabContents(PrologueTutorialContentBuilder.OriginalRoom);
        try
        {
            if(kind!=PrologueTutorialRoom.Repair)
            {
                room.name="Room_ALL_TrainTutorial_"+kind;
                foreach(ItemPickupMarker pickup in room.GetComponentsInChildren<ItemPickupMarker>(true))
                    if(pickup!=null) Object.DestroyImmediate(pickup.gameObject);
                foreach(PrologueTrainSequence legacy in room.GetComponentsInChildren<PrologueTrainSequence>(true)) Object.DestroyImmediate(legacy);
                PrologueTutorialContentBuilder.Set(room.GetComponent<RoomInstance>(),"_roomId",p=>p.stringValue=RoomId(kind));
            }
            Transform previous=room.transform.Find("Tutorial Authored");
            if(previous!=null) Object.DestroyImmediate(previous.gameObject);
            var authored=new GameObject("Tutorial Authored");authored.transform.SetParent(room.transform,false);
            Transform parent=authored.transform;
            switch(kind)
            {
                case PrologueTutorialRoom.Repair:
                    AddSpawn(parent,"tutorial_repair",new Vector2(-6,-.5f),FacingDirection.Right);
                    AddSpawn(parent,"tutorial_rest",new Vector2(5.5f,-.5f),FacingDirection.Right);
                    AddSpawn(parent,"tutorial_forward",new Vector2(5.5f,-.5f),FacingDirection.Right);
                    AddInteraction(parent,"Machine Inspection",new Vector2(-2,1),PrologueTutorialInteraction.InspectMachine);
                    AddInteraction(parent,"Pressure Valve",new Vector2(0,1),PrologueTutorialInteraction.AdjustValve);
                    AddInteraction(parent,"Machine Test",new Vector2(2,1),PrologueTutorialInteraction.TestMachine);
                    AddNPC(parent,definition,"conductor",new Vector2(-4,1),PrologueTutorialInteraction.OptionalDialogue,"optional_conductor");
                    AddDoor(parent,"To Common",new Vector2(7.35f,-.5f),PrologueTutorialRoom.Common,"from_left");
                    break;
                case PrologueTutorialRoom.Common:
                    MoveDefaultSpawn(room,new Vector2(-6,-.5f));
                    AddSpawn(parent,"after_incident",new Vector2(-6,-.5f),FacingDirection.Right);
                    AddNPC(parent,definition,"companion_dragon",new Vector2(1.75f,.7f),PrologueTutorialInteraction.TalkToDragon);
                    AddNPC(parent,definition,"companion_deer",new Vector2(-2.5f,.4f),PrologueTutorialInteraction.OptionalDialogue,"optional_deer");
                    AddNPC(parent,definition,"companion_medic",new Vector2(4.25f,.4f),PrologueTutorialInteraction.OptionalDialogue,"optional_medic");
                    AddDoor(parent,"To Repair",new Vector2(-7.35f,-.5f),PrologueTutorialRoom.Repair,"from_right");
                    AddDoor(parent,"To Rear",new Vector2(7.35f,-.5f),PrologueTutorialRoom.Rear,"default");
                    break;
                case PrologueTutorialRoom.Rear:
                    MoveDefaultSpawn(room,new Vector2(-6,-.5f));
                    AddSpawn(parent,"after_withdrawal",new Vector2(-6,-.5f),FacingDirection.Right);
                    GameObject dragon=AddNPC(parent,definition,"companion_dragon",new Vector2(-4.2f,.6f),null);
                    dragon.SetActive(false);
                    AddInteraction(parent,"Rear Lock",new Vector2(-6.5f,.5f),PrologueTutorialInteraction.SealDoor);
                    GameObject entity=AddVisual(parent,definition.GetRole("white_entity"),new Vector2(4,1));
                    entity.transform.localScale=Vector3.one*.2f;
                    AddDoor(parent,"To Common",new Vector2(-7.35f,-.5f),PrologueTutorialRoom.Common,"from_right");
                    AddDoor(parent,"To Control",new Vector2(7.35f,-.5f),PrologueTutorialRoom.Control,"default");
                    break;
                case PrologueTutorialRoom.Control:
                    MoveDefaultSpawn(room,new Vector2(-6,-.5f));
                    AddSpawn(parent,"after_control",new Vector2(0,-1),FacingDirection.Up);
                    AddInteraction(parent,"Emergency Control",new Vector2(0,.7f),PrologueTutorialInteraction.UseEmergencyControl);
                    AddNPC(parent,definition,"crew",new Vector2(-3.5f,.5f),PrologueTutorialInteraction.OptionalDialogue,"optional_crew");
                    AddDoor(parent,"To Rear",new Vector2(-7.35f,-.5f),PrologueTutorialRoom.Rear,"from_right");
                    break;
                case PrologueTutorialRoom.Completed:
                    MoveDefaultSpawn(room,Vector2.zero,FacingDirection.Down);
                    foreach(SpriteRenderer sprite in room.GetComponentsInChildren<SpriteRenderer>(true)) sprite.color=new Color(.05f,.06f,.09f,1);
                    foreach(Tilemap tilemap in room.GetComponentsInChildren<Tilemap>(true)) tilemap.color=new Color(.05f,.06f,.09f,1);
                    break;
            }
            PrologueTutorialContentBuilder.Track(path);
            GameObject saved=PrefabUtility.SaveAsPrefabAsset(room,path);
            if(saved==null)throw new InvalidOperationException("객차 저장 실패: "+path);
            if(kind!=PrologueTutorialRoom.Repair)
            {
                PrologueTutorialContentBuilder.Set(data,"_roomPrefab",p=>p.objectReferenceValue=saved.GetComponent<RoomInstance>());
                PrologueTutorialContentBuilder.Save(data);
            }
        }
        finally {PrefabUtility.UnloadPrefabContents(room);}
    }

    private static PrologueTutorialResumePoint Resume(string flag,PrologueTutorialRoom room,string spawn)
        =>new PrologueTutorialResumePoint {CompletedFlag=flag,Room=room,SpawnPointId=spawn};
    internal static string RoomId(PrologueTutorialRoom room)
    {
        switch(room) {
            case PrologueTutorialRoom.Repair:return "all.trainq0";
            case PrologueTutorialRoom.Common:return "all.train.common";
            case PrologueTutorialRoom.Rear:return "all.train.rear";
            case PrologueTutorialRoom.Control:return "all.train.control";
            case PrologueTutorialRoom.Completed:return "all.train.prologue_end";
            default:throw new ArgumentOutOfRangeException(nameof(room));
        }
    }
    private static void AddSpawn(Transform parent,string id,Vector2 position,FacingDirection facing)
    {
        GameObject point=Child(parent,"Spawn_"+id,position);
        SpawnPoint spawn=point.AddComponent<SpawnPoint>();
        PrologueTutorialContentBuilder.Set(spawn,"_spawnPointId",p=>p.stringValue=id);
        PrologueTutorialContentBuilder.Set(spawn,"_defaultFacing",p=>p.enumValueIndex=(int)facing);
    }
    private static void MoveDefaultSpawn(GameObject root,Vector2 position,FacingDirection facing=FacingDirection.Right)
    {
        foreach(SpawnPoint point in root.GetComponentsInChildren<SpawnPoint>(true))
            if(point.SpawnPointId=="default") {
                point.transform.localPosition=position;
                PrologueTutorialContentBuilder.Set(point,"_defaultFacing",p=>p.enumValueIndex=(int)facing);
            }
    }
    private static void AddDoor(Transform parent,string name,Vector2 position,PrologueTutorialRoom target,string spawn)
    {
        GameObject door=InteractionRoot(parent,name,position,new Vector2(.8f,1.5f));
        door.AddComponent<PrologueTutorialDoor>().Configure(target,spawn,true);
    }
    private static void AddInteraction(Transform parent,string name,Vector2 position,PrologueTutorialInteraction kind)
    {
        GameObject marker=InteractionRoot(parent,name,position,new Vector2(1,1));
        marker.AddComponent<PrologueTutorialInteractable>().Configure(kind);
        // A source tile is a temporary machine marker; replace this renderer without changing its interaction ID.
        SpriteRenderer renderer=marker.AddComponent<SpriteRenderer>();
        renderer.sprite=PrologueTutorialAssetFactory.FirstSprite("Assets/_Game/Content/Art/Environment/Train/In/SubwayIn.png");
        renderer.sortingLayerName="Characters";renderer.sortingOrder=3;renderer.spriteSortPoint=SpriteSortPoint.Pivot;
    }
    private static GameObject AddNPC(Transform parent,PrologueTutorialDefinition definition,string roleId,Vector2 position,
        PrologueTutorialInteraction? interaction,string dialogue=null)
    {
        GameObject root=AddVisual(parent,definition.GetRole(roleId),position);
        if(interaction.HasValue) {
            SetInteractionLayer(root);root.AddComponent<BoxCollider2D>().isTrigger=true;
            root.AddComponent<PrologueTutorialInteractable>().Configure(interaction.Value,dialogue);
        }
        return root;
    }
    private static GameObject AddVisual(Transform parent,PrologueTutorialRole role,Vector2 position)
    {
        GameObject root=PrefabUtility.InstantiatePrefab(role.VisualPrefab) as GameObject;
        if(root==null)throw new InvalidOperationException("역할 프리팹 배치 실패: "+role.RoleId);
        root.transform.SetParent(parent,false);root.transform.localPosition=position;return root;
    }
    private static GameObject InteractionRoot(Transform parent,string name,Vector2 position,Vector2 size)
    {GameObject root=Child(parent,name,position);SetInteractionLayer(root);BoxCollider2D collider=root.AddComponent<BoxCollider2D>();collider.isTrigger=true;collider.size=size;return root;}
    private static void SetInteractionLayer(GameObject root)
    {int layer=LayerMask.NameToLayer("Interactable");if(layer<0)throw new InvalidOperationException("Interactable 레이어가 없습니다.");root.layer=layer;}
    private static GameObject Child(Transform parent,string name,Vector2 position)
    {var root=new GameObject(name);root.transform.SetParent(parent,false);root.transform.localPosition=position;return root;}
}
#endif
