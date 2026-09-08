# 06. 실제 코드로 배우는 설계 패턴·성능·검증

목표는 코드에 패턴 이름을 붙이는 것이 아니라, 바뀌기 쉬운 부분과 지켜야 하는 경계를 찾는 것입니다.
현재 저장소에서 확인한 구현을 예로 들되, 설계 의도와 실제 검증 결과는 구분합니다.
이 문서 작성 과정에서 Unity 플레이·테스트·기기 성능 측정은 실행하지 않았습니다.

## 1. 패턴보다 문제를 먼저 말합니다

다음 문장을 먼저 완성합니다.
“현재 ___가 ___를 직접 알아서, ___를 바꿀 때 ___까지 고쳐야 한다.”
그다음 그 연결을 줄이는 데 필요한 가장 작은 변경을 고릅니다.
클래스를 늘리는 것, 인터페이스를 붙이는 것, 파일을 쪼개는 것만으로 문제가 해결되지는 않습니다.

예를 들어 액션 실행기가 구체적인 대사 UI를 직접 제어하면 UI 교체와 테스트가 어려워집니다.
프로젝트는 액션이 필요한 기능을 `IDialogueRunner`라는 작은 계약으로 요청하도록 나눴습니다.
여기서 가치는 “인터페이스 사용” 자체가 아니라 실행기와 UI의 변경 이유를 분리한 데 있습니다.

## 2. Registry: ID를 구현에 연결하기

[ActionAdapterRegistry.cs](../../Assets/_Game/Scripts/Scenario/Runtime/ActionAdapterRegistry.cs)의 `Register`, `TryGet`을 읽습니다.
`dialogue.wait`라는 데이터의 ID를 실행 가능한 어댑터 객체로 연결합니다.
이는 구현 목록을 검색 가능한 표로 관리하는 Registry 방식입니다.

- 장점: 새 액션마다 실행기의 거대한 switch를 늘리는 일을 줄입니다.
- 비용: 등록 단계가 필요하고, 등록 누락은 실행 시 오류가 됩니다.
- 경계: Registry는 실행 의미·입력 형식·편집기 설명을 대신하지 않습니다.

실제 `Register`는 같은 ID를 덮어씁니다. 중복을 거부하는 Registry라고 소개하면 안 됩니다.
등록 정책과 Action Catalog 검증을 각각 확인해야 합니다.
학습 질문: ID 오타, 미등록, 같은 ID의 다른 구현 등록은 어떤 지점에서 발견할 수 있을까요?

## 3. Adapter: 기존 시스템을 새 계약으로 호출하기

[DialogueWaitActionAdapter.cs](../../Assets/_Game/Scripts/Scenario/Runtime/Adapters/DialogueWaitActionAdapter.cs)는 액션 데이터를 해석하고,
[DialogueManagerRunner.cs](../../Assets/_Game/Scripts/Scenario/Runtime/Presentation/DialogueManagerRunner.cs)는 기존 DialogueManager 호출을 연결합니다.
[IDialogueRunner.cs](../../Assets/_Game/Scripts/Scenario/Runtime/Presentation/IDialogueRunner.cs)는 그 사이의 작은 계약입니다.

```text
Action Director → DialogueWaitActionAdapter → IDialogueRunner
                                            └─ DialogueManagerRunner → DialogueManager
```

Adapter는 새 대화 시스템을 다시 만드는 것이 아닙니다.
기존 시스템의 시작·바쁨·완료·취소 정책을 새 호출부에서 다룰 수 있도록 맞춥니다.
호출자가 기다리는 기능이라면 시작 실패를 조용히 무시해서는 안 됩니다.
“대사가 끝나면 콜백”과 “대사 시작에 실패”를 구분하지 못하면 무한 대기가 생길 수 있습니다.

현재 `ShowAndWait`는 완료 콜백 계약이며 선택 결과 전체를 반환하는 범용 스토리 분기 계약은 아닙니다.
이미 있는 추상화에 실제로 없는 능력까지 있다고 설명하지 않는 습관이 중요합니다.

## 4. Observer: 변화 알림과 상태 보관을 분리하기

[EventManager.cs](../../Assets/_Game/Scripts/Core/Runtime/EventManager.cs)는 이름별 콜백 구독·해제·호출을 제공합니다.
[ContentMakerWindow.cs](../../Assets/_Game/Scripts/Editor/ContentMaker/ContentMakerWindow.cs)는 프로젝트 변경과 Undo 알림을 구독합니다.
이런 구조는 관찰자가 변화에 반응하는 Observer 방식으로 이해할 수 있습니다.

좋은 사용은 “변경이 발생했으니 목록을 다시 읽는다”입니다.
위험한 사용은 “이 문자열 이벤트를 받았으니 영구 진행이 반드시 완료됐다”고 가정하는 것입니다.
이벤트를 받지 못한 새 씬이나 늦게 생성된 NPC도 저장 상태를 읽어 올바르게 나타나야 합니다.

- 구독 시점과 해제 시점을 짝으로 찾습니다.
- 같은 객체가 두 번 구독하면 중복 실행되지 않는지 봅니다.
- 콜백 안에서 다시 이벤트를 발생시키는 경우 재진입을 고려합니다.
- 문자열 기반 ID의 오타는 컴파일러가 잡지 못합니다.

모든 기능을 전역 이벤트로 바꾸면 호출 관계를 추적하기 어려워집니다.
호출자와 대상이 명확한 단일 작업은 직접 호출이 더 읽기 쉬울 수 있습니다.

## 5. Command: 실행과 되돌리기를 한 작업으로 묶기

[SequenceEditCommandStack.cs](../../Assets/_Game/Scripts/Scenario/Editor/SequenceMaker/SequenceEditCommandStack.cs)의 `ISequenceEditCommand`를 읽습니다.
명령은 `Execute`, `Undo`, 이름, 선호 선택 Block ID를 가집니다.
실행 이력은 변경 전후 선택과 상태 ID도 보관합니다.

이 클래스는 이름에 Stack이 있지만 내부 이력은 `List<HistoryEntry>`의 끝을 사용하는 방식입니다.
“Stack 자료형을 썼다”와 “후입선출 규칙으로 이력을 처리한다”는 다른 설명입니다.

- `Execute`: 명령 실행 후 Block ID 계약을 재검사하고 이력을 추가합니다.
- `Undo`: 한 작업의 하위 명령을 역순으로 되돌립니다.
- `Redo`: 정방향으로 다시 실행합니다.
- `MarkSaved`: 현재 상태를 저장 기준으로 기록합니다.
- transaction: 여러 작은 편집을 사용자에게 한 작업으로 보이게 묶습니다.

시퀀스 트리 전체를 무조건 복제해 두는 것과, 변경을 되돌릴 정보를 기록하는 것은 비용·실패 방식이 다릅니다.
기존 Sequence Maker에 자체 이력이 있다고 일반 Inspector 편집까지 전부 같은 체계로 갈아탈 필요는 없습니다.

## 6. Object Pool: 생성 횟수와 보관 메모리를 맞바꾸기

[ObjectPoolManager.cs](../../Assets/_Game/Scripts/Core/Runtime/ObjectPoolManager.cs)의 `RegisterPool`, `Spawn`, `Despawn`을 봅니다.
반복 생성되는 객체를 보관했다가 재사용하여 Instantiate·Destroy 빈도를 줄이려는 구조입니다.
사용한 객체를 계속 보관하므로 메모리 비용이 사라지는 것은 아닙니다.

실제 구현은 대기 객체 수에 상한을 두고, 초과 반납분은 폐기합니다.
대여할 객체가 부족하면 새로 만들기 때문에 최대 활성 객체 수 제한과도 다릅니다.
`Despawn`이 모든 사용자 스크립트 필드를 초기화하는 것은 아닙니다.
효과의 타이머·대상·이벤트 구독·Tween 상태는 각 객체의 수명주기 계약을 확인해야 합니다.

면접식 설명보다 중요한 질문: 같은 피해 팝업을 재사용할 때 이전 캐릭터의 정보가 남을 수 있을까요?
풀의 장점과 함께 이중 반납·외부 파괴·초기화 누락을 설명할 수 있어야 합니다.

## 7. 공통 흐름과 다른 정책을 분리합니다

콘텐츠 메이커의 Window는 선택·표시를, MapService는 생성 작업을, DeletionService는 삭제 안전 규칙을 맡습니다.
공통 흐름이 있다는 이유로 생성과 삭제를 한 “범용 자산 조작기”로 무리하게 합치지 않습니다.
생성 실패 시 새로 만든 파일만 정리하는 정책과, 기존 자산 삭제를 막는 정책은 서로 다르기 때문입니다.

시나리오도 “무엇을 실행할지”와 “언제 실행할지”를 구분합니다.
Action Sequence가 실행 절차를 표현해도 모든 스토리 상태의 소유자가 되는 것은 아닙니다.
전투 중 임시 상태와 저장 가능한 사건 완료 상태도 별도의 수명 범위를 갖습니다.
경계를 먼저 적으면 어느 Manager에 변수를 추가해야 할지 판단하기 쉬워집니다.

## 8. 과한 설계를 피하는 기준

| 제안 | 먼저 확인할 질문 |
|---|---|
| 인터페이스 추가 | 실제 교체 구현·테스트 대역·경계가 필요한가? |
| Manager 추가 | 기존 소유자가 있는데 다른 진실을 하나 더 만드는가? |
| 모든 if를 전략 객체로 교체 | 조건이 정말 독립적으로 확장되는가? |
| 범용 그래프 도구 작성 | 현재 Sequence Maker의 기능을 다시 만들고 있지 않은가? |
| 캐시 추가 | 갱신·무효화 시점을 정확히 알 수 있는가? |
| ECS·Jobs 전환 | 측정한 대량 데이터 병목이 있는가? |

작은 NPC 몇 개를 관리하는 코드와 수만 개의 시뮬레이션 코드는 같은 최적화가 필요하지 않습니다.
이 프로젝트에서 확인되지 않은 패턴을 이름만 맞춰 “이미 적용됐다”고 설명하지 않습니다.

## 9. 성능은 호출 횟수와 작업량으로 가설을 세웁니다

[InteractionSystem.cs](../../Assets/_Game/Scripts/Overworld/Runtime/InteractionSystem.cs)의 `Update`와 탐색 메서드를 봅니다.
현재 코드는 플레이어 참조를 캐시하고, 정지 중에는 낮은 주기로 검사하며, 결과 배열을 재사용합니다.
오래된 규칙 문서의 설명보다 현재 메서드 구현을 우선 확인해야 합니다.

이 설계는 검색·할당 횟수를 줄이려는 근거이지 “발열이 해결됐다”는 측정 결과가 아닙니다.
`NonAlloc` 형태의 물리 조회도 물리 연산 자체는 수행합니다.
고정 결과 버퍼를 쓰면 버퍼보다 많은 후보가 있을 때 어떤 대상이 누락되는지 확인해야 합니다.
LINQ도 한 번 실행하는 Editor 작업과 매 프레임 대량 실행을 같은 위험으로 취급하지 않습니다.

측정 전에는 “느린 것 같다”를 재현 가능한 문장으로 바꿉니다.
예: “같은 방에서 이동하지 않아도 특정 탐색 함수가 매 프레임 호출되는가?”
이 질문은 소스로 빈도를 확인하고, 필요하면 Profiler로 실제 비용을 측정할 수 있습니다.

## 10. 성능 측정 순서

1. 목표 기기·해상도·품질·목표 프레임을 고정합니다.
2. 같은 방·같은 카메라·같은 입력 경로를 반복할 수 있게 정합니다.
3. CPU 시간, GPU 시간, GC 할당, 메모리, 프레임 급증을 나눠 봅니다.
4. 평균뿐 아니라 드문 긴 프레임과 충분히 플레이한 뒤의 상태를 기록합니다.
5. 가장 큰 병목 하나만 바꾸고 같은 조건으로 비교합니다.
6. 기능·화질·입력 반응이 바뀌지 않았는지 함께 확인합니다.

30fps의 프레임 예산은 약 33.3ms, 60fps는 약 16.7ms입니다.
CPU 시간이 줄어도 GPU가 병목이면 프레임 수가 오르지 않을 수 있습니다.
Editor 결과를 곧바로 저사양 휴대폰·Nintendo Switch 결과로 옮기지 않습니다.
Development Build나 깊은 계측 자체의 오버헤드도 비교 조건에 남깁니다.
이 문서에는 모바일·Switch 실측 수치가 없으며, 발열·배터리·출시 성능을 보장하지 않습니다.

## 11. 테스트가 확인하는 것은 이름이 아니라 단언입니다

| 실제 테스트 소스 | 확인할 대표 메서드 | 범위 |
|---|---|---|
| [CSV 테스트](../../Assets/_Game/Scripts/Editor/ContentMaker/Dialogue/ContentMakerDialogueCsvTests.cs) | `RoundTripPreservesKoreanMultilineQuotesAndEveryField` | 한글·특수문자·필드 왕복 |
| [마커 테스트](../../Assets/_Game/Scripts/Editor/ContentMaker/Markers/ContentMakerMarkerServiceTests.cs) | `BattleChoice_CyclicDialogueReferencesTerminateWithoutChangingData` | 순환 연결 검사 종료 |
| [삭제 테스트](../../Assets/_Game/Scripts/Editor/ContentMaker/Maps/ContentMakerDeletionServiceTests.cs) | `RoomCandidatesCannotEscapeTheirOwnedFolder` | 소유 경로 경계 |
| [경로 테스트](../../Assets/_Game/Scripts/Editor/ContentMaker/ContentMakerAssetUtilityTests.cs) | 테스트별 입력과 Assert 확인 | 파일명·경로 보정 계약 |

테스트는 준비 → 한 행동 → 결과 확인 순서로 읽습니다.
메서드 이름에 “안전”이 있어도 실제 Assert가 검사하지 않는 동작까지 증명하지는 않습니다.
삭제 경로 helper 테스트 통과와 실제 운영체제 휴지통 복구 성공은 다른 검증입니다.
컴파일 성공, 테스트 작성, 테스트 실행 성공, Unity 수동 확인을 별도 기록합니다.

## 12. 디버깅은 가설 하나를 작게 확인합니다

1. 발생 화면·입력·직전 상태·정확한 오류를 남깁니다.
2. 성공하는 경우와 실패하는 경우의 차이를 하나로 줄입니다.
3. 입력 데이터 → 조건 검사 → 상태 변경 → UI 반영 순서로 호출을 추적합니다.
4. 어떤 값이 잘못됐을 것인지 가설을 세우고 필요한 값만 확인합니다.
5. 원인을 수정한 뒤 기존 실패 사례와 인접한 정상 사례를 함께 확인합니다.

예: 대화가 안 나올 때 UI 크기를 먼저 바꾸기보다 `CanInteract`, 선택된 DialogueData, 재생 시작 결과를 구분합니다.
예: 타입을 못 찾는 오류는 namespace뿐 아니라 파일 포함·asmdef·Unity import·메타데이터 문제일 수 있습니다.
로그 한 줄만으로 Awake 순서나 GC를 원인이라고 단정하지 않습니다.
확인용 로그도 반복 경로에 남기면 비용과 소음을 만들므로 사용 범위와 제거 시점을 정합니다.

## 13. 직접 해 볼 과제와 설명 기준

1. 대사 액션의 데이터 ID부터 DialogueManager까지 호출 경로를 빈 종이에 그립니다.
2. SequenceEditCommandStack에서 Undo 후 새 명령을 실행하면 Redo 목록이 어떻게 되는지 찾습니다.
3. 풀 재사용에서 초기화해야 할 값 세 가지를 실제 효과 객체 기준으로 적습니다.
4. CSV 실패 사례 하나의 테스트를 읽고, 어떤 실패를 아직 확인하지 않는지 적습니다.
5. 최적화 제안 하나를 고르고 “관찰·가설·측정 조건·변경·비교 결과” 양식만 먼저 작성합니다.

기여 설명도 정확해야 합니다. 팀·선배 코드, 본인 수정, AI 보조, 외부 패키지를 구분합니다.
DOTween·Odin·Unity 내부 기능을 사용한 것은 통합 경험이며, 해당 라이브러리 자체를 만든 경험은 아닙니다.
통과 기준: 패턴 이름보다 선택 이유·대안·실패 경계·실제로 확인한 범위를 설명할 수 있습니다.
