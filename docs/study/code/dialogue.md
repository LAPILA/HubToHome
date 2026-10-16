# Dialogue 전체 파일 색인

[학습 홈](../README.md) · [Dialogue 해설](../08-dialogue.md)

13개 C# 파일입니다. 아래 선언은 소스의 public/internal 타입을 기계적으로 추출한 탐색용 정보이며 전체 API 문서나 정밀 코드 리뷰가 아닙니다. partial 파일의 타입 이름이 반복되는 것은 정상입니다. 파일 링크는 로컬 저장소용입니다.

## Dialogue/Runtime

| 파일 | 구분 | 주요 선언 |
| --- | --- | --- |
| [BattleSpeechBubble.cs](../../../Assets/_Game/Scripts/Dialogue/Runtime/BattleSpeechBubble.cs) | 런타임 | `BattleSpeechTrigger`, `BattleSpeechBubbleDirection`, `BattleSpeechContentMode`, `BattleSpeechRule` 외 1개 |
| [BattleSpeechBubbleLayout.cs](../../../Assets/_Game/Scripts/Dialogue/Runtime/BattleSpeechBubbleLayout.cs) | 런타임 | `BattleSpeechBubbleLayoutResult`, `BattleSpeechBubbleLayout` |
| [BattleSpeechConfig.cs](../../../Assets/_Game/Scripts/Dialogue/Runtime/BattleSpeechConfig.cs) | 런타임 | `BattleSpeechConfig` |
| [DialogueData.cs](../../../Assets/_Game/Scripts/Dialogue/Runtime/DialogueData.cs) | 런타임 | `DialogueStyle`, `DialogueData`, `DialogueNode`, `ChoiceData` |
| [DialogueEncounterContext.cs](../../../Assets/_Game/Scripts/Dialogue/Runtime/DialogueEncounterContext.cs) | 런타임 | `DialogueEncounterContext` |
| [DialogueManager.cs](../../../Assets/_Game/Scripts/Dialogue/Runtime/DialogueManager.cs) | 런타임 | `DialogueManager` |
| [DialoguePlaybackPolicy.cs](../../../Assets/_Game/Scripts/Dialogue/Runtime/DialoguePlaybackPolicy.cs) | 런타임 | `DialoguePlaybackPolicy` |
| [DialogueTextAnimationPolicy.cs](../../../Assets/_Game/Scripts/Dialogue/Runtime/DialogueTextAnimationPolicy.cs) | 런타임 | `DialogueTextAnimationPolicy` |
| [SpeakerData.cs](../../../Assets/_Game/Scripts/Dialogue/Runtime/SpeakerData.cs) | 런타임 | `SpeakerData`, `EmotionType` |

## Dialogue/Tests/Editor

| 파일 | 구분 | 주요 선언 |
| --- | --- | --- |
| [BattleSpeechBubbleLayoutTests.cs](../../../Assets/_Game/Scripts/Dialogue/Tests/Editor/BattleSpeechBubbleLayoutTests.cs) | 테스트 소스 | `BattleSpeechBubbleLayoutTests` |
| [DialogueChoiceLayoutTests.cs](../../../Assets/_Game/Scripts/Dialogue/Tests/Editor/DialogueChoiceLayoutTests.cs) | 테스트 소스 | `DialogueChoiceLayoutTests` |
| [DialogueStateRestoreTests.cs](../../../Assets/_Game/Scripts/Dialogue/Tests/Editor/DialogueStateRestoreTests.cs) | 테스트 소스 | `DialogueStateRestoreTests` |
| [WindmillZevDialogueContentTests.cs](../../../Assets/_Game/Scripts/Dialogue/Tests/Editor/WindmillZevDialogueContentTests.cs) | 테스트 소스 | `WindmillZevDialogueContentTests` |

