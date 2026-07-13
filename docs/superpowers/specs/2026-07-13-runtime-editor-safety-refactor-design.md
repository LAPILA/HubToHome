# Runtime and Sequence Maker Safety Refactor Design

## Goal

씬/맵 전환, 전투 QTE와 스킬 타임라인, 대화/화면 전환, 전투 참가자 식별, Sequence Maker 파일 작업이 성공뿐 아니라 실패와 취소에서도 일관되게 종료되도록 한다.

## Compatibility Boundary

- 기존 Scene, Prefab, ScriptableObject의 직렬화 필드명과 enum 값은 변경하지 않는다.
- 기존 `LoadScene`, `ForceStop`, Action ID, Scenario YAML 문법은 호환 API로 유지한다.
- `GameModuleActionRunner`, `BattleScenarioExecutionGate`, Trigger Context 조립은 선배 담당 구현을 재작성하지 않는다.
- 시나리오가 없는 적은 기존 전투 흐름을 그대로 수행한다.
- DOTween은 소유자가 명확한 연출에만 사용하고 취소 시 해당 Tween만 종료한다.
- Odin은 데이터 검증과 기획자 피드백에 사용하며 런타임 상태 저장소로 사용하지 않는다.

## Architecture

### Scene and Map Transition

`SceneLoader`가 로딩 작업의 유일한 소유자가 된다. 새 요청 API는 요청 수락과 최종 결과를 분리하고 `RejectedBusy`, `InvalidScene`, `LoadFailed`, `CancelledBeforeActivation`, `Succeeded`를 노출한다. 기존 void API는 호환 래퍼로 남긴다. 취소는 Scene activation 전까지만 허용하며 유효하지 않은 씬은 로딩 전에 거부한다. 모든 실패 종료 경로에서 암전, Raycast 차단, `_isLoading`을 복구하고 완료 callback 예외는 Loader cleanup을 막지 않는다.

`MapTransitionService`는 SceneLoader 완료까지 `GameState.Cutscene`과 재진입 잠금을 유지한다. 실패 시 출발 전에 변경한 Spawn 정보를 복구한다. Scene 전환용 도착 정보는 `GlobalDataManager.SpawnPointId`에 저장하고 새 씬의 `PlayerController`가 SpawnPoint를 우선 적용한 뒤 좌표 fallback을 사용한다.

### Battle Events and Participant Identity

실제 전투 경로에서 `EnemyDefeated`와 `SkillCompleted` 이벤트를 한 번 발행한다. `BattleScenarioRuntime` 또는 시나리오가 없으면 이벤트 발행을 생략하며 전투 결과에는 영향을 주지 않는다.

적의 데이터 ID는 authoring ID로 유지한다. 별도 `IBattleParticipantIdRegistry`가 현재 전투의 참가자 인스턴스와 런타임 ID 매핑을 소유하고, Context 조립 코드는 변경하지 않은 채 이 좁은 계약만 소비할 수 있게 한다. 적 배치 목록 순서로 첫 번째 적은 기존 ID, 이후 적은 `baseId#2`, `baseId#3` 접미사를 사용하며 충돌 시 다음 빈 번호를 선택한다. Registry는 전투 단위로 생성·폐기한다. Scenario Event의 `subjectId`는 런타임 ID를 사용하고 호환 조회에서 base ID는 첫 번째 적을 가리킨다.

### QTE and Skill Timeline Lifecycle

QTE 종료를 사용자 판정, 시간 초과, 시스템 취소, 실패로 구분하는 `QteExecution` 결과 계약을 추가한다. 모든 시작은 정확히 한 번 terminal 상태를 기록한다. 시스템 취소는 기존 Miss gameplay callback, 피해, 보상, 시나리오 이벤트를 만들지 않지만 대기 중인 Action에는 `Cancelled`를 통지한다. 시작 중 교체, 모듈 종료, 전투 종료, disable/destroy도 동일한 취소 경로를 사용한다. `ForceStop`은 호환성을 유지하되 안전한 시스템 취소로 위임한다.

`BattleSkillTimelineRunner`는 현재 블록 enumerator를 소유하고 `try/finally`에서 Dispose한다. 취소 가능한 블록은 별도 cleanup 계약을 구현해 QTE, telegraph, DOTween, 임시 VFX를 회수한다. 기존 블록은 동작을 유지한다.

### Dialogue and Screen Transition

대화 데이터의 null Node는 시작 전에 검증한다. 실행 중 발견해도 callback과 playing 상태를 종료해 `dialogue.wait`가 영구 대기하지 않게 한다.

화면 페이드는 요청별 세대 토큰과 Tween 소유권을 갖고 시작 시 `alpha`, 색상, `blocksRaycasts`를 캡처한다. 취소 시 캡처 상태를 정확히 복구하고 정상적인 fade-out 성공만 암전 소유권을 유지한다. 오래된 요청의 완료가 새 요청 상태를 덮지 못하게 한다.

### Sequence Maker Safety

Safe Preview는 자신이 등록한 대상의 상태만 복구한다. 전역 Undo stack을 시작 그룹까지 되돌리는 방식을 제거해 프리뷰 이후의 무관한 편집을 보존한다.

Scenario Source 경로는 `Assets/_Game/Content/Scenarios/Source/` 아래의 `.yaml` 파일만 인정한다. 절대 경로와 `..` 입력은 정규화 전에 거부하고 `Path.GetFullPath` 후 Windows 대소문자 무시 및 디렉터리 구분자 경계로 포함 여부를 재검증한다. read/write/temp/backup/delete/`.meta` 처리에 같은 정책을 적용한다. 허용 Source root 밖을 가리키는 junction/symlink는 지원하지 않는다.

Battle 목록에 연결되었다는 사실과 Battle sub-asset 소유권을 구분한다. Battle 소유 Sequence는 목록 제거, Battle YAML 원자 저장, 실제 sub-asset 삭제 순으로 처리하고 저장 실패 시 원래 인덱스를 복구한다. 독립 Runtime Asset이 Battle 목록에 연결된 경우 Battle 참조와 YAML만 갱신하고 Asset 자체는 유지한다. 독립 Sequence 삭제는 source와 `.meta`를 백업하고 Runtime Asset 삭제 실패 시 원본 바이트를 복원한다.

Export와 preview는 원본 Runtime Asset을 정규화하지 않는다. 누락·중복된 Action Block ID와 Trigger Condition ID는 저장 오류로 차단한다. 자동 복구는 Export와 분리된 command stack/Undo/recovery 경로의 명시적 Identity Repair 작업만 담당한다. Export는 원본의 Dirty/Undo 상태를 변경하지 않는다.

## Failure Handling

- 모든 장기 실행 작업은 성공, 실패, 취소 중 하나로 끝난다.
- cleanup은 callback 예외와 관계없이 실행한다.
- SceneLoader 실패는 현재 씬을 유지하고 화면과 입력을 복구한다.
- 시나리오 연동 실패는 기본 전투 진행을 막지 않되 명확한 진단을 남긴다.
- 파일 작업은 경로 검증 후에만 수행하고 삭제 전 소유권을 재검증한다.

## Verification

- EditMode: 경로 검증, export 비변경성, 삭제 소유권, 참가자 ID, 대화 null 처리.
- PlayMode 또는 coroutine test: SceneLoader 실패 복구, MapTransition 잠금, QTE 시스템 취소, 타임라인 cleanup, 페이드 취소.
- 전체 Unity EditMode 테스트와 컴파일 실행.
- Scene/Prefab/ScriptableObject YAML은 수정하지 않는다.
