/// <summary>필드 조작의 고정 계약입니다. 표시 문구와 NPC 외형은 이 값과 별개입니다.</summary>
public enum PrologueTutorialInteraction
{
    InspectMachine = 0,
    AdjustValve = 1,
    TestMachine = 2,
    TalkToDragon = 3,
    EnterRear = 4,
    SealDoor = 5,
    UseEmergencyControl = 6,
    OptionalDialogue = 7
}

public enum PrologueTutorialRoom
{
    Repair = 0,
    Common = 1,
    Rear = 2,
    Control = 3,
    Completed = 4
}
