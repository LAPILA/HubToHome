using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

[Serializable]
public sealed class PrologueTutorialIntroduction
{
    [LabelText("상대 역할 ID")] public string RoleId = "conductor";
    [LabelText("도입 대사 ID")] public string DialogueId = "introduction";
    [LabelText("위젤 대화 위치 · 방 로컬")] public Vector2 PlayerMeetingPosition = new Vector2(-2.5f, -0.75f);
    [LabelText("상대 대화 위치 · 방 로컬")] public Vector2 PartnerMeetingPosition = new Vector2(-4f, -0.75f);
    [Min(0f), LabelText("접근 시간")] public float EntranceDuration = 0.6f;
    [Min(0f), LabelText("대화 전 정착 시간")] public float SettleDuration = 0.15f;
    [LabelText("대화 후 원래 위치로")] public bool ReturnToStart = true;
    [Min(0f), LabelText("복귀 시간")] public float ReturnDuration = 0.45f;
}

[Serializable]
public sealed class PrologueTutorialRole
{
    [LabelText("역할 ID")] public string RoleId;
    [LabelText("화자 · 표정")] public SpeakerData Speaker;
    [LabelText("표시 프리팹 (선택)")] public GameObject VisualPrefab;
    [LabelText("샘플 스프라이트")] public Sprite Sprite;
    [LabelText("샘플 애니메이터 (선택)")] public RuntimeAnimatorController Animator;
    [LabelText("표시 색상")] public Color Tint = Color.white;
}

[Serializable]
public sealed class PrologueTutorialDialogueBinding
{
    [LabelText("대사 ID")] public string Id;
    [LabelText("대사 데이터")] public DialogueData Dialogue;
}

[Serializable]
public sealed class PrologueTutorialRoomBinding
{
    [LabelText("구간")] public PrologueTutorialRoom Room;
    [LabelText("방 데이터")] public RoomDefinition Definition;
    [LabelText("기본 도착 ID")] public string SpawnPointId = "default";
}

[Serializable]
public sealed class PrologueTutorialSequenceBinding
{
    [LabelText("연출 ID")] public string Id;
    [LabelText("Action Sequence")] public ActionSequenceAsset Sequence;
}

[Serializable]
public sealed class PrologueTutorialResumePoint
{
    [LabelText("완료 플래그")] public string CompletedFlag;
    [LabelText("복귀 구간")] public PrologueTutorialRoom Room;
    [LabelText("안전 도착 ID")] public string SpawnPointId = "default";
}

/// <summary>프롤로그의 교체 가능한 작성 데이터. 진행·보상·현재 전투 값은 저장하지 않습니다.</summary>
[CreateAssetMenu(fileName = "PrologueTutorial", menuName = "Hub To Home/제작/프롤로그 튜토리얼")]
public sealed class PrologueTutorialDefinition : ScriptableObject
{
    [TitleGroup("참가자 · 전투"), LabelText("위젤 데이터"), Required]
    public CharacterData Protagonist;
    [TitleGroup("참가자 · 전투"), LabelText("백색체의 전투 부위"), Required]
    public EnemyData Enemy;
    [TitleGroup("참가자 · 전투"), LabelText("전투 전용 회복품"), Required]
    public ItemData HealingItem;
    [TitleGroup("참가자 · 전투"), LabelText("전투 시나리오")]
    public BattleScenarioData BattleScenario;

    [TitleGroup("역할 · 외형"), ListDrawerSettings(ShowIndexLabels = false), LabelText("교체형 역할")]
    public List<PrologueTutorialRole> Roles = new List<PrologueTutorialRole>();
    [TitleGroup("대사 · 연출"), ListDrawerSettings(ShowIndexLabels = false), LabelText("대사 목록")]
    public List<PrologueTutorialDialogueBinding> Dialogues = new List<PrologueTutorialDialogueBinding>();
    [TitleGroup("대사 · 연출"), LabelText("짧은 열차 도입")]
    public IllustratedStoryAsset OpeningStory;
    [TitleGroup("대사 · 연출"), LabelText("기관사와의 인게임 도입")]
    public PrologueTutorialIntroduction Introduction = new PrologueTutorialIntroduction();
    [TitleGroup("대사 · 연출"), LabelText("후미 공개 삽화")]
    public IllustratedStoryAsset RearRevealStory;
    [TitleGroup("대사 · 연출"), LabelText("열차 음향")]
    public PrologueTrainAudioProfile TrainAudio;
    [TitleGroup("대사 · 연출"), ListDrawerSettings(ShowIndexLabels = false), LabelText("교체형 시퀀스")]
    public List<PrologueTutorialSequenceBinding> Sequences = new List<PrologueTutorialSequenceBinding>();

    [TitleGroup("방 · 재개"), ListDrawerSettings(ShowIndexLabels = false), LabelText("객차 · 종료 방")]
    public List<PrologueTutorialRoomBinding> Rooms = new List<PrologueTutorialRoomBinding>();
    [TitleGroup("방 · 재개"), ListDrawerSettings(ShowIndexLabels = false), LabelText("완료별 안전 지점")]
    public List<PrologueTutorialResumePoint> ResumePoints = new List<PrologueTutorialResumePoint>();
    [TitleGroup("방 · 재개"), LabelText("타이틀 씬")]
    public string TitleSceneName = "00_TitleScene";

    [TitleGroup("진행 박자"), Min(0f), LabelText("방 이동 페이드")]
    public float RoomFadeDuration = 0.25f;
    [TitleGroup("진행 박자"), Min(0f), LabelText("이상 발생 전 정적")]
    public float IncidentQuietDuration = 0.35f;
    [TitleGroup("진행 박자"), Min(0f), LabelText("정전 길이")]
    public float IncidentDuration = 0.8f;
    [TitleGroup("진행 박자"), Min(0f), LabelText("구조 진입 시간")]
    public float RescueEntranceDuration = 0.45f;
    [TitleGroup("진행 박자"), LabelText("구조 진입 시작 오프셋")]
    public Vector2 RescueEntranceOffset = new Vector2(-5f, 0f);
    [TitleGroup("전투 학습"), Min(1), LabelText("방어 연습 최대 기회")]
    public int MaxDefenseAttempts = 3;
    [TitleGroup("전투 학습"), Min(1), LabelText("소모품 연습 최대 행동")]
    public int MaxItemPracticeActions = 3;

    public bool TryGetDialogue(string id, out DialogueData dialogue)
    {
        dialogue = GetDialogue(id);
        return dialogue != null;
    }

    public DialogueData GetDialogue(string id)
    {
        if (Dialogues == null || string.IsNullOrWhiteSpace(id)) return null;
        for (int i = 0; i < Dialogues.Count; i++)
            if (Dialogues[i] != null && string.Equals(Dialogues[i].Id, id, StringComparison.Ordinal))
                return Dialogues[i].Dialogue;
        return null;
    }

    public PrologueTutorialRole GetRole(string roleId)
    {
        if (Roles == null || string.IsNullOrWhiteSpace(roleId)) return null;
        for (int i = 0; i < Roles.Count; i++)
            if (Roles[i] != null && string.Equals(Roles[i].RoleId, roleId, StringComparison.Ordinal))
                return Roles[i];
        return null;
    }

    public RoomDefinition GetRoom(PrologueTutorialRoom room)
    {
        PrologueTutorialRoomBinding binding = GetRoomBinding(room);
        return binding != null ? binding.Definition : null;
    }

    public PrologueTutorialRoomBinding GetRoomBinding(PrologueTutorialRoom room)
    {
        if (Rooms == null) return null;
        for (int i = 0; i < Rooms.Count; i++)
            if (Rooms[i] != null && Rooms[i].Room == room) return Rooms[i];
        return null;
    }

    public ActionSequenceAsset GetSequence(string id)
    {
        if (Sequences == null || string.IsNullOrWhiteSpace(id)) return null;
        for (int i = 0; i < Sequences.Count; i++)
            if (Sequences[i] != null && string.Equals(Sequences[i].Id, id, StringComparison.Ordinal))
                return Sequences[i].Sequence;
        return null;
    }
}
