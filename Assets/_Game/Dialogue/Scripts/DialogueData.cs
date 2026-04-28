using System;
using System.Collections.Generic;

/// <summary>
/// 대화 한 줄의 데이터 구조 (JSON 직렬화용).
/// </summary>
[Serializable]
public class DialogueLine
{
    public string id        = "";       // 대화 고유 식별자
    public string speaker   = "";       // 화자 이름
    public string portrait  = "";       // 초상화 스프라이트 키 (null이면 이름만 표시)
    public string text      = "";       // 대화 내용 (TextAnimator 태그 포함 가능)
    public float  speed     = 1f;       // 타이핑 속도 배율
    public bool   autoAdvance = false;  // true면 플레이어 클릭 없이 자동 진행
    public float  autoDelay = 1.5f;     // autoAdvance 시 대기 시간(초)
    public List<string> commands = new List<string>(); // 특수 명령 (예: "[bgm:boss_theme]")
    public List<DialogueChoice> choices = new List<DialogueChoice>(); // 선택지
}

/// <summary>
/// 대화 선택지 데이터.
/// </summary>
[Serializable]
public class DialogueChoice
{
    public string text          = "";   // 선택지 텍스트
    public string nextDialogueID = "";  // 선택 시 이동할 다음 대화 ID
    public string eventID       = "";   // 선택 시 실행할 이벤트 ID (예: "SET_FLAG:met_npc:1")
}

/// <summary>
/// 대화 묶음 (하나의 NPC 대화 또는 이벤트 대화).
/// </summary>
[Serializable]
public class DialogueSequence
{
    public string              sequenceID = "";
    public List<DialogueLine>  lines      = new List<DialogueLine>();
}
