# 확장형 Trigger Rule 기반

## 이번 작업

- 기존 `BattleEventRuleData`는 그대로 유지했다.
- `BattleScenarioData`에 새 `TriggerRules` 목록을 나란히 추가했다.
- 이벤트는 고정 enum 대신 안정적인 Event ID와 타입이 보존되는 payload를 사용할 수 있다.
- 조건은 all, any, negate를 조합한 블록 트리로 구성할 수 있다.
- HP 임계치, 참가자, 모듈 결과, 만남 횟수, 저장 flag 같은 조건을 독립 어댑터로 추가했다.

## 의미

새 이벤트나 조건이 필요할 때 중앙 enum과 switch를 계속 키우지 않아도 된다. Trigger Library에 사람이 읽는 계약을 추가하고 조건 어댑터를 등록하면 전투, 오버월드, 미니게임, 저장 상태를 같은 `when -> do` 모델로 연결할 수 있다.

## 호환성

현재 단계에서는 기존 전투 규칙과 에셋을 자동 변환하지 않는다. 다음 단계에서 레거시 규칙을 새 모델로 읽어 주는 호환 매퍼를 추가하고, 기존 테스트와 동작이 동일한지 검증한다.

## 검증

- Unity EditMode 집중 테스트 12/12 통과.
- 기존 규칙 목록과 새 규칙 목록이 한 `BattleScenarioData` 안에서 함께 유지됨을 확인했다.

## 공식 Trigger Library

- `Assets/_Game/Content/Scenarios/TriggerLibrary/Source`의 YAML 3개가 사람이 읽는 원본이다.
- 전투 이벤트 6개와 조건 7개에 한국어 이름, 설명, 사용 시점, 문장형 요약, 검색어, 타입 파라미터를 작성했다.
- Unity 메뉴 `HubToHome > 시나리오 > Trigger Library 다시 만들기`로 생성 에셋을 갱신한다.
- 원본에 중복 ID나 잘못된 필드가 있으면 기존 생성 에셋은 교체되지 않는다.
- Runtime Condition과 Library 계약 중 어느 한쪽만 추가해도 검증에서 실패한다.

### 추가 검증

- YAML 파서/라이터/동기화 8/8 통과.
- 공식 원본/런타임/생성 에셋 일치 2/2 통과.

## 기존 전투 규칙과 통합

- 기존 `BattleEventRuleData` 에셋은 그대로 둔다.
- 전투 시작, HP 임계치, 적 쓰러짐, 스킬 종료, 모듈 완료 규칙을 전투 시작 시 새 Trigger Rule 형태로 한 번 변환한다.
- 기존 규칙과 새 규칙은 같은 evaluator, timing queue, Action Sequence 실행 게이트를 사용한다.
- 새 규칙은 이벤트가 발생한 시점과 실제 실행 시점을 분리할 수 있다. 현재 액션/스킬/모듈 종료 또는 이름 있는 checkpoint에서 실행 가능하다.
- 이벤트 payload 값을 대상 Sequence의 typed input으로 넘길 수 있다.
- Session, Encounter Memory, Save once 범위를 지원하지만 전투 중간 상태 자체는 저장하지 않는다.

### 통합 검증

- 신규 호환 테스트 15/15 통과.
- 기존 전투 시나리오 회귀 테스트 42/42 통과.
- Sequence Input 실행 연결 테스트를 포함한 Action Bridge 13/13 통과.
- 지연 규칙은 실제 checkpoint에서 꺼내기 전에는 Encounter Memory에 실행 완료로 기록되지 않는다.
