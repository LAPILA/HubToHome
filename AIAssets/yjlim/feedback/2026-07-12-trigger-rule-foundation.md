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
