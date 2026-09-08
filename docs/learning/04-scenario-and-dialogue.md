# 04. 대사·조건·시퀀스·번역을 연결하기

2026-09-07 실제 소스 기준 학습 안내입니다. 팀의 기존 구현과 이후 보완을 묶어 설명하며, 개인 한 명이 전부 작성한 것으로 간주하지 않습니다. 이번 문서 작업은 읽기 검토이며 Unity 재생·테스트 실행 결과를 새로 주장하지 않습니다.

## 이 장의 목표

“NPC에게 대사를 넣기”에서 시작해 “플레이어가 한 일에 따라 사건이 달라지기”까지 역할을 분리합니다. **문장은 DialogueData, 사건 실행은 Action Sequence, 실행 조건은 Trigger/상태 선택, 기억은 저장되는 상태**에 둡니다.

| 만들고 싶은 것 | 현재 중심 도구·데이터 |
|---|---|
| 주민의 짧은 대화·표정·선택지 | 콘텐츠 메이커의 대사·화자, DialogueData |
| 진행 상태에 따라 다른 대화 | 조건·NPC 작업 화면, FlagDialogueSelector + 전역 플래그 |
| 조건에 따라 대상 표시·숨김 | FlagStateBinder |
| 접근·카메라·대화·전투 순서 | 시퀀스 메이커, ActionSequenceAsset |
| 전투 HP 변화 뒤 사건 | BattleScenarioData의 TriggerRules |
| 본문 다국어 출력 | LocalizationKey + LocalizationTable |

**메이커에서 한글 입력이 가능하다는 것과 완전한 분기 스토리 제작 환경은 다릅니다.** NPC의 상태별 위치·대사를 하나의 통합 목록으로 관리하는 전체 시스템, 선택 결과의 범용 시퀀스 분기, 모든 문자열의 다국어 연결은 완성 범위와 구분해야 합니다.

## 1. 가장 작은 대사부터 만들기

1. `Hub To Home → 제작 → 콘텐츠 메이커`를 엽니다.
2. 챕터를 선택하고 대사·화자 화면의 `문서` 작업에서 대화 문서를 생성합니다.
3. `화자·표정`에서 고정 SpeakerID·표시 이름·표정별 이미지를 연결합니다.
4. `대사`에서 한 줄에 화자·표정·본문을 지정하고 순서대로 줄을 추가합니다.
5. 필요할 때만 선택지와 다음 대화를 연결합니다.
6. 저장한 DialogueData를 해당 NPC 또는 대화 마커에 연결합니다.

현재 대사 화면은 `대사 / 화자·표정 / 조건·NPC / CSV·번역 / 문서` 작업을 선택하는 구조입니다. `문서`는 생성·파일 관리, `CSV·번역`은 대본 입출력과 번역 준비를 담당합니다. [실제 화면 코드](../../Assets/_Game/Scripts/Editor/ContentMaker/Dialogue/ContentMakerDialoguePanel.cs)

처음에는 기본 대사 2줄만 만들어 “상호작용 → 표시 → 종료 → 다시 이동”이 되는지 사용자가 직접 확인하는 것이 좋습니다. 번역·전투·조건을 한꺼번에 넣으면 어느 연결에서 실패했는지 구분하기 어렵습니다. 구체적인 현행 UI는 [콘텐츠 메이커 사용법](../content-maker-guide.md)을 기준으로 합니다.

대사 원본은 NPC 프리팹 내부에 긴 문자열을 계속 복사하는 방식이 아닙니다. NPC는 외형·상호작용 연결을 갖고, 여러 줄의 실제 내용은 별도 `DialogueData` 자산이 소유합니다.

## 2. DialogueData는 줄의 목록이다

[DialogueData](../../Assets/_Game/Scripts/Dialogue/Runtime/DialogueData.cs)의 핵심은 `List<DialogueNode>`입니다.

| 필드 | 의미 | 주의 |
|---|---|---|
| Speaker | 화자 자산 | NPC GameObject와 별개 |
| Emotion | 표정 키 | 화자 Portraits에 이미지가 있어야 함 |
| LocalizationKey | 번역 조회 키 | 순서가 바뀌어도 고정하는 방향 권장 |
| DefaultText | 기본 본문 | 키가 없거나 조회되지 않으면 사용 |
| EventTriggerID | 실행부가 해석하는 사건 표시 | 임의 문자열로 모든 사건이 생기지 않음 |
| Choices | 선택지 목록 | 다음 대화·플래그·전투 등 현재 계약 범위 |

[SpeakerData](../../Assets/_Game/Scripts/Dialogue/Runtime/SpeakerData.cs)는 화자 ID, 표시 이름, 음성 설정, `Dictionary<EmotionType, Sprite>`를 갖습니다. 여러 대화가 같은 화자를 참조하므로 초상화를 한 번 교체하면 함께 반영됩니다. 이 Dictionary는 Odin 직렬화를 사용하므로 일반 Unity 직렬화 리스트로 생각하고 구조를 임의 변경하면 안 됩니다.

현재 DialogueNode에는 별도의 불변 Node ID가 없습니다. 노드는 목록 순서로 진행하고 LocalizationKey도 선택 입력입니다. 메이커의 `빈 본문 키만 생성`으로 키 없는 줄에 고유한 번역 키를 부여할 수 있지만, 노드 ID·번역표 행·번역문을 자동 생성하는 기능은 아닙니다.

## 3. 실제 대화 실행 흐름

```text
NPC / 마커 / 시퀀스의 요청
  → DialogueManager.TryStartDialogue
  → 이전 게임 상태 보관, 대화 상태 획득
  → PlayNode: 번역 조회·본문 가공
  → DialogueUI: 화자·표정·타이프라이터·선택지 표시
  → 확인: 출력 중이면 즉시 표시, 끝났으면 다음 줄
  → 종료 / 취소: UI와 콜백·소유한 게임 상태 정리
```

실행은 [DialogueManager](../../Assets/_Game/Scripts/Dialogue/Runtime/DialogueManager.cs), 화면은 [DialogueUI](../../Assets/_Game/Scripts/UI/Runtime/DialogueUI.cs)입니다. `TryStartDialogue()`는 시작 실패 여부를 알려줍니다. 재생 중인데 요청만 보내고 무한정 완료를 기다리는 문제가 없도록 “시작 성공”과 “끝남”을 구분합니다.

관리자는 `PlaybackGeneration`으로 대화 재생 세대를 구분하고 취소 시 해당 세대를 확인하는 API를 제공합니다. 예전 사건이 취소되면서 방금 시작한 새 대화까지 닫아 버리지 않도록 하는 개념입니다.

[DialogueTextAnimationPolicy](../../Assets/_Game/Scripts/Dialogue/Runtime/DialogueTextAnimationPolicy.cs)는 기본 타이프라이터 정책을 보정합니다. 글자 출력 속도와 글자가 확대·축소하는 시각 효과는 같은 설정이 아닙니다. 외형 문제는 데이터·타이프라이터·레이아웃을 구분해 확인합니다.

## 4. 선택지로 현재 할 수 있는 일

`ChoiceData`에는 `ChoiceText`, `NextDialogue`, `SetFlagOnSelect`, `StartBattleEncounter`가 있습니다.

- 선택하면 지정 플래그를 1로 설정할 수 있습니다.
- 다음 DialogueData로 넘어갈 수 있습니다.
- 현재 조우 컨텍스트를 사용해 전투를 시작할 수 있습니다.
- 선택 결과가 없는 일반 대화 완료와 취소는 별도로 처리합니다.

전투에 필요한 적·시나리오 등 런타임 정보는 [DialogueEncounterContext](../../Assets/_Game/Scripts/Dialogue/Runtime/DialogueEncounterContext.cs)로 전달합니다. 대화 원본 자산을 재생할 때마다 수정해 적 정보를 덮어쓰는 방식이 아닙니다.

`DialogueManager`에는 선택 프롬프트 API도 있지만, 이것만으로 `dialogue.wait`가 선택 인덱스를 결과값으로 돌려주지는 않습니다. 현재 [IDialogueRunner](../../Assets/_Game/Scripts/Scenario/Runtime/Presentation/IDialogueRunner.cs)의 `ShowAndWait`는 완료 콜백 계약입니다. **대화 선택 → 임의 결과값 → 다음 Action 분기**의 일반 제작 경로가 완성되었다고 설명하면 안 됩니다.

## 5. NPC 반응과 저장 플래그

[FlagDialogueSelector](../../Assets/_Game/Scripts/Overworld/Runtime/State/FlagDialogueSelector.cs)는 각 규칙의 플래그 비교를 검사하고, 맞는 규칙 중 우선순위가 가장 높은 대화를 고릅니다. 동률이면 먼저 나온 규칙이 유지됩니다. 없으면 지정된 기본 대화 또는 호출자의 기본 대화로 돌아갑니다.

현재 규칙 한 개는 플래그 하나와 값 비교를 가집니다. 범용 AND/OR 조건 트리인 전투 Trigger Condition과 동일한 데이터 모델은 아닙니다.

메이커의 `조건·NPC`에서 다음 순서로 편집합니다. [조건 제작·연결 코드](../../Assets/_Game/Scripts/Editor/ContentMaker/Dialogue/ContentMakerDialogueRulesPanel.cs)

1. 왼쪽에서 기본 대화를 선택하고 새 조건 문서를 만듭니다. 선택한 챕터의 `Data/Dialogue`에 저장됩니다.
2. 조건을 추가해 플래그 키·비교 방법·값·우선순위·재생 대화를 지정합니다. 목록의 이동·삭제도 가능합니다.
3. 검사 오류를 해결하고 `조건 문서 저장`을 누릅니다. 이 작업은 게임의 실제 플래그 값을 변경하지 않습니다.
4. `마커 · NPC`에서 사용할 방을 선택해 프리팹 편집 모드로 열고, Hierarchy에서 일반 `NPCMarker` 본체를 선택합니다.
5. `조건·NPC`로 돌아와 `이 조건 문서를 선택한 NPC에 연결`을 누른 뒤 **방 프리팹도 저장**합니다. 기존 NPC의 조건 문서를 가져와 편집할 수도 있습니다.

연결 버튼은 선택한 방의 Prefab Stage 내부 NPC만 수정합니다. 다른 씬·다른 방·Project 원본을 자동으로 바꾸지 않으며, 제브의 접근·공격을 담당하는 `DialogueBattleNPC`는 이 일반 NPC 연결 대상이 아닙니다. 조건 문서 저장과 방 프리팹의 참조 저장은 서로 다른 저장입니다.

예를 들어 제작 메모에는 다음처럼 상태표를 먼저 작성할 수 있습니다. 아래 이름과 값은 설명용이며 실제 자산을 생성한 것이 아닙니다.

| 조건 | 반응 | 우선순위 예 |
|---|---|---|
| `ch01.zev.finished >= 1` | 대결 이후 대화 | 20 |
| `ch01.zev.met >= 1` | 다시 만난 대화 | 10 |
| 그 외 | 첫 만남 대화 | 기본 |

[FlagStateBinder](../../Assets/_Game/Scripts/Overworld/Runtime/State/FlagStateBinder.cs)는 조건에 따른 표시 상태를 연결합니다. 위치를 바꾸며 등장하는 NPC의 전체 제작 관리 도구와 같지는 않습니다. “다음 방문에 안 보임”은 배치 상태, “지금 눈앞에서 걸어 나감”은 연출이라는 구분을 유지하세요.

저장할 사실은 `GlobalDataManager`의 플래그/Encounter Memory와 구분해 관리합니다. 전투 중 임시 `BattleSessionState` 플래그는 다른 모듈로 넘어가도 유지되지만 진행 중 전투를 세이브에서 복구하는 용도가 아닙니다. **선택 확정**과 **사건 성공 완료**도 같은 플래그로 합치지 않는 편이 안전합니다.

## 6. 시퀀스는 유한한 사건 순서다

[ActionDirector](../../Assets/_Game/Scripts/Scenario/Runtime/ActionDirector.cs)는 [ActionSequenceAsset](../../Assets/_Game/Scripts/Scenario/Data/ActionSequenceAsset.cs)의 블록을 실행합니다. 각 Action ID의 실제 실행기는 [ActionAdapterRegistry](../../Assets/_Game/Scripts/Scenario/Runtime/ActionAdapterRegistry.cs)에 등록된 `IActionAdapter`입니다.

```text
ActionSequenceAsset
  → ActionDirector: 순서·병렬·중단 관리
  → ActionAdapterRegistry: ID로 실행기 찾기
  → IActionAdapter: 파라미터 읽기
  → ActionExecutionContext: 필요한 서비스 찾기
  → 실제 대화 / 카메라 / 오디오 / 전투 서비스
```

Registry는 Dictionary 기반 ID 조회이며 평균 O(1)을 기대할 수 있습니다. 현재 `Register()`는 같은 ID에 다시 등록하면 기존 항목을 대체합니다. Dictionary라는 이유만으로 중복 ID 등록을 자동 차단한다고 설명하면 안 됩니다. 제작 카탈로그의 검증과 런타임 등록 규칙은 별개입니다.

이 연결은 Adapter와 Registry를 학습하기 좋습니다. 대화 실행기가 바뀌어도 시퀀스 제작자가 매 블록의 구현체를 고치지 않도록 ID와 좁은 서비스 계약을 사용합니다.

## 7. Context·Handle·Session의 차이

| 이름 | 질문에 답하는 역할 |
|---|---|
| [ActionExecutionContext](../../Assets/_Game/Scripts/Scenario/Runtime/ActionExecutionContext.cs) | 이번 실행에서 어떤 서비스와 값에 접근할 수 있나? |
| [ActionExecutionHandle](../../Assets/_Game/Scripts/Scenario/Runtime/ActionExecutionHandle.cs) | 실행 중인가, 끝났나, 실패·취소됐나? |
| [ActionExecutionSession](../../Assets/_Game/Scripts/Scenario/Runtime/ActionExecutionSession.cs) | 어떤 블록이 어떤 순서로 시작·종료됐나? |

Context는 서비스 타입을 키로 한 Dictionary와 값 경로를 키로 한 Dictionary를 갖습니다. 자식 실행은 부모 값 조회를 상속하면서 로컬 입력/결과를 가집니다. 일반 context의 서비스 공유와 Safe Preview의 분리된 context는 목적이 다릅니다.

취소는 코루틴을 멈추는 한 줄만의 문제가 아닙니다. 대화·카메라·오디오·DOTween이 잡은 상태를 각 소유자가 해제하고, 자식 실행도 정리되어야 합니다. Handle 취소 이벤트와 서비스의 취소 구현, `finally` 복구를 함께 읽으세요. 모든 서비스가 무조건 완벽히 복구된다고 추정하지 않습니다.

## 8. 순차·병렬·재사용에서 배우는 트리와 그래프

`ScenarioActionData.Children`은 재귀 트리이며 `[SerializeReference]` 직렬화를 사용합니다. 자식이 없는 일반 Action과 자식이 있는 병렬 그룹을 같은 흐름에서 다룰 수 있습니다.

- `flow.parallel / all`: 모든 자식 성공을 기다립니다.
- `any`: 처음 성공한 자식을 채택합니다. 먼저 실패한 자식 하나만으로 곧바로 성공/실패를 확정하는 뜻이 아닙니다.
- `race`: 처음 종료한 자식의 성공·실패·취소 결과를 채택합니다.
- 승자가 정해졌거나 부모가 종료되면 남은 자식을 취소하고 정리합니다.

이는 같은 프레임들에 코루틴을 번갈아 진행하는 동시 실행이지, 무조건 CPU 스레드를 늘리는 병렬 계산이 아닙니다.

공통 연출은 [sequence.call](../../Assets/_Game/Scripts/Scenario/Runtime/Adapters/SequenceCallActionAdapter.cs)로 호출하고, 선언된 typed input을 전달합니다. `${input.actor}` 같은 binding은 제한된 데이터 조회이지 C# 식 실행이 아닙니다. [SequenceCallGraphValidator](../../Assets/_Game/Scripts/Scenario/Data/SequenceCallGraphValidator.cs)는 A→B→A 같은 재귀 호출 그래프를 검증합니다.

트리의 Block ID는 순서 변경 때 유지하고 복제할 때 새로 발급합니다. 인덱스를 ID처럼 쓰면 중간에 한 줄 삽입할 때 선택·오류 위치·실행 기록이 전부 어긋납니다.

## 9. When과 Do: 전투 사건을 발생시키는 흐름

```text
참가자 HP 변경 같은 도메인 사실
  → Scenario Event: 안정적인 Event ID + payload
  → Trigger Condition: 조건을 읽기 전용으로 평가
  → 즉시 실행 또는 지정 체크포인트까지 대기
  → BattleScenarioExecutionGate
  → BattleScenarioActionBridge → ActionDirector
```

[ScenarioTriggerEvaluator](../../Assets/_Game/Scripts/Scenario/Runtime/ScenarioTriggerEvaluator.cs)와 [TriggerConditionRegistry](../../Assets/_Game/Scripts/Scenario/Runtime/TriggerConditionRegistry.cs)는 조건을 계산할 때 보상이나 플래그를 변경하지 않습니다. 조건은 “맞는가”만 답하고, 실제 변경은 실행 단계가 담당합니다.

[BattleScenarioEventRouter](../../Assets/_Game/Scripts/Scenario/Runtime/Battle/BattleScenarioEventRouter.cs)는 이벤트 발생 시 맞은 Trigger를 보관했다가 `AfterCurrentAction`, `AfterCurrentSkill`, `AfterCurrentModule` 또는 이름 있는 체크포인트에서 꺼냅니다. 한번 실행 기록도 단순히 대기 목록에 넣은 때가 아니라 실제 전달 시점에 확정합니다.

옛 `BattleEventRuleData`와 새 `TriggerRules`는 공존합니다. 현재는 호환 매핑 뒤 같은 평가 경로를 사용합니다. 새 타입이 있다는 이유로 기존 직렬화 데이터를 일괄 변환하거나 삭제하면 안 됩니다.

## 10. 시네마틱의 담당 범위

- 씬 진입 사건: [SceneActionSequenceTrigger](../../Assets/_Game/Scripts/Overworld/Runtime/Cinematics/SceneActionSequenceTrigger.cs). 화면 공개가 끝난 뒤 시작하며 성공한 일회성 완료만 전역 플래그에 남깁니다.
- 별도 구도와 대상 움직임: [OverworldCinematicStage](../../Assets/_Game/Scripts/Overworld/Runtime/Cinematics/OverworldCinematicStage.cs)와 CinematicShotAsset. 검은 화면 아래 준비한 뒤 원래 게임 카메라로 돌아옵니다.
- 고정된 컷신 타임라인: [TimelineCutsceneRunner](../../Assets/_Game/Scripts/Scenario/Runtime/Presentation/TimelineCutsceneRunner.cs). 재생 중 카메라 소유권을 갖고 완료·취소·파괴 시 반환합니다.
- 전투 중 동적 이동·가짜 공격·레터박스 등: [BattleTweenCinematicService](../../Assets/_Game/Scripts/Battle/Runtime/Services/BattleTweenCinematicService.cs). DOTween 기반 연출 서비스이며 사건 조건을 소유하지 않습니다.

Timeline의 Signal은 소리·흔들림·VFX 같은 표시 타이밍에 사용합니다. 스토리 확정·저장·분기를 여기저기 Signal에 숨기지 않습니다. 과거 삭제한 CinematicPresentationRig 확장 제안을 현재 완성 기능으로 합산하지 마세요. 현재 연결된 Action Catalog와 런타임 서비스를 확인해야 합니다.

## 11. 시퀀스 메이커와 YAML의 관계

공식 창은 `Hub To Home → 시나리오 → 시퀀스 메이커`입니다. 사람은 한국어 블록을 편집하고, 교환·버전 관리용 원문은 Scenario YAML, Unity 실행 표현은 ScriptableObject입니다.

```text
YAML → 파싱·참조·카탈로그 검증 → Runtime Asset → 에디터 표시
에디터 변경 → 명령 기록 → 검증·충돌 확인 → YAML 안전 저장
```

[SequenceEditCommandStack](../../Assets/_Game/Scripts/Scenario/Editor/SequenceMaker/SequenceEditCommandStack.cs)은 편집 명령과 역연산을 기록합니다. 재귀 자산 전체를 매번 Unity Undo로 복제하는 것과 다릅니다. 이것이 Command 패턴의 실제 사례이며, 되돌릴 정보와 대상 Block ID가 필요합니다.

[SequenceSaveCoordinator](../../Assets/_Game/Scripts/Scenario/Editor/SequenceMaker/SequenceSaveCoordinator.cs)는 검증 → 기존 해시 확인 → 임시 파일 작성·재읽기·재검증 → 교체 직전 재확인 → 원문 교체 → 메타데이터 반영 순서를 소유합니다. 복구 파일은 `Library/HubToHome/SequenceMakerRecovery`의 로컬 안전장치이며 원문을 대체하지 않습니다.

Safe Preview는 분리된 context와 허용된 부작용만 사용하고, Live Test는 실제 Play Mode context를 사용합니다. Safe Preview가 성공해도 게임 입력·씬 이동·보상이 실제로 완성됐다는 뜻은 아닙니다. 원문·자산 직접 수정 대신 공식 저장·검사 경로를 사용하세요. [동기화 계약](../../.agents/skills/hubtohome-scenario-authoring/references/editor-and-sync.md)

## 12. 대본 CSV와 번역 CSV는 다르다

[ContentMakerDialogueCsv](../../Assets/_Game/Scripts/Editor/ContentMaker/Dialogue/ContentMakerDialogueCsv.cs)는 한 DialogueData를 표 형식으로 편집하고 **검증 후 전체 교체**하는 도구입니다. 줄바꿈·쉼표·따옴표가 있는 대본을 다루지만 `.xlsx` 직접 입력이나 자동 동기화는 아닙니다.

[LocalizationManager](../../Assets/_Game/Scripts/Core/Runtime/LocalizationManager.cs)는 별도의 `Resources/LocalizationTable`을 읽고 KR/EN/JP/CN 본문을 조회합니다. 현재 로더는 물리적 줄바꿈으로 먼저 행을 나누므로 quoted multiline을 완전히 지원하지 않습니다. 현재 포맷에서는 셀 안의 문자 `\n`을 실제 줄바꿈으로 변환합니다.

`CSV·번역 → 번역 준비`의 `빈 본문 키만 생성`은 비어 있는 LocalizationKey에만 `dlg.`로 시작하는 고유 키를 넣고 기존 키를 유지합니다. 대화를 저장한 뒤 번역표에 같은 Key와 언어별 본문을 따로 작성하세요. 줄 이동·파일명 변경에도 키는 유지되지만, **대사를 복제하면 기존 키도 복제**되므로 다른 문장으로 바꿀 때는 키를 분리해야 합니다. `번역표 위치 찾기`는 파일을 찾아줄 뿐 자동 동기화하지 않습니다.

현재 본문은 LocalizationKey로 조회하지만 **화자 이름과 선택지 문구는 DialogueUI에서 원문 문자열을 표시**합니다. 주석에 “번역 Key 권장”이 있어도 실제 번역 조회 코드와 같지 않습니다. 빈 번역값의 fallback 정책, 키 중복·누락, 모든 언어의 폭·줄바꿈·글꼴도 별도 마감 대상입니다.

번역을 늘리기 전 권장 작업은 복제·분기를 포함한 고정 대사 ID 정책 마감, 선택지·화자 이름 조회 연결, 대본에서 번역표로 내보내기, 번역 누락 검사입니다. 이는 빈 키 생성 기능과 별개인 **다음 작업 제안**입니다.

## 13. 직접 해볼 작은 과제

1. 주민 한 명의 첫 대화/재방문 대화를 종이에 적고 어떤 플래그가 필요한지 상태표를 만드세요.
2. “주민 구조를 선택했다”와 “구조 연출이 끝났다”가 서로 다른 저장 시점인 이유를 설명하세요.
3. 제브 장면을 조건·대본·이동 연출·전투 결과의 네 부분으로 분리해 담당 클래스를 적으세요.
4. 병렬 자식 A가 실패하고 B가 나중에 성공할 때 all/any/race의 결과를 각각 예측하세요.
5. 대사에 번역 키를 적었다고 이름·선택지도 번역되지 않는 이유를 호출 흐름으로 찾아보세요.

읽을 테스트 소스: [대화 상태 복구](../../Assets/_Game/Scripts/Dialogue/Tests/Editor/DialogueStateRestoreTests.cs), [대화 실행 어댑터](../../Assets/_Game/Scripts/Scenario/Tests/Editor/DialogueManagerRunnerTests.cs), [호출 그래프](../../Assets/_Game/Scripts/Scenario/Tests/Editor/SequenceCallGraphValidatorTests.cs), [편집 명령](../../Assets/_Game/Scripts/Scenario/Tests/Editor/SequenceEditCommandStackTests.cs), [안전 저장](../../Assets/_Game/Scripts/Scenario/Tests/Editor/SequenceSaveCoordinatorTests.cs). 테스트 파일 존재와 이번 작업의 테스트 통과는 별개입니다.

설명할 수 있어야 할 질문: **대화 한 줄, 스킬 하나, 사건 하나, 챕터 하나를 왜 다른 단위로 나누는가? 안정적인 ID는 무엇을 보호하는가? 취소됐는데 완료 플래그가 남으면 어떤 플레이 문제가 생기는가?**
