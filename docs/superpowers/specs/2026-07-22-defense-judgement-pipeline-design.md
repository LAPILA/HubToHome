# Defense Judgement Pipeline Design

## Goal

패링·회피·점프의 입력 수집, 시간 등급, 요구 입력 일치, 결과 의미를 하나의 데이터 기반 판정 파이프라인으로 통합한다. 기존 전투 흐름과 콜백은 유지하면서 공격마다 규칙을 교체하고 결과 이벤트를 확장할 수 있게 한다.

## Current Problem

- `QTEManager`가 남은 시간 비율을 초 단위 설정값과 비교한다.
- `Time.deltaTime`을 사용해 일시정지나 느린 시간 배율에서 방어창이 멈춘다.
- 큰 프레임에서 공격 시점을 넘긴 입력이 Perfect로 처리될 수 있다.
- 요구 입력 일치와 성공 조건이 기본 적 공격과 `Action_DefenseWindow`에 중복돼 있다.
- 동시 입력은 무시되지만 무효 시도로 기록되지 않는다.
- 결과가 `(DefenseInput, QTEGrade)`뿐이라 성공·근접 성공·실패·무효를 구분하기 어렵다.
- `DefenseQTEUI.ShowQTE/ShowResult`가 실제 방어 QTE 경로에서 호출되지 않는다.

## Chosen Design

### Domain Model

`DefenseJudgementPolicy`는 Unity 프레임과 UI에 의존하지 않는 순수 판정기다.

- `DefenseInputReadStatus`: 입력 없음, 단일 입력, 동시 입력
- `DefenseOutcome`: 성공, 근접 성공, 실패, 무효
- `DefenseTimingProfile`: Perfect·Great·Good 구간을 초 단위로 정의
- `DefenseQteRequest`: 지속 시간, 난이도, 요구 입력, 판정 구간, 근접 성공 허용 여부
- `DefenseQteResult`: 입력, 등급, 결과, 남은 시간, 요구 일치, 피해 방지 여부

기존 `DefenseRequirement`의 직렬화 값 0·1은 유지하고 `Any`, `ParryOnly`, `DodgeOnly`, `DodgeOrJump`를 뒤에 추가한다.

### Input Policy

- 한 프레임에 정확히 하나의 방어 입력만 유효하다.
- 둘 이상이 동시에 들어오면 `Ambiguous`로 판정하고 해당 방어 시도를 `Invalid`로 종료한다.
- 기존 `GameInput.TryReadDefenseInputThisFrame`는 호환용으로 유지한다.
- 플레이어 입력 버퍼는 입력 시각을 함께 반환해 Update와 Coroutine 실행 순서가 등급을 바꾸지 않게 한다.
- 새 방어창을 열 때 이전 버퍼를 지우는 기존 동작을 유지한다.

### Timing Policy

- 방어창은 `Time.realtimeSinceStartup`으로 시작·종료 시각을 기록한다.
- 공격 시각 이상에서 읽힌 입력은 받지 않고 timeout으로 처리한다.
- 등급은 입력 시점의 실제 `secondsBeforeImpact`와 초 단위 구간을 비교한다.
- 난이도 배율은 판정 구간만 좁히며 0 이하 값과 역전된 구간은 정규화한다.
- 일치 입력의 Bad 등급은 `NearSuccess`다. 기본값은 피해를 막아 기존 게임 감각을 보존하며 공격 데이터에서 비활성화할 수 있다.

### Runtime Ownership

`QTEManager`는 입력 수집, 단일 실행 소유권, 취소, UI 전달만 담당한다.
`IDefenseInputSource`는 대상 캐릭터의 입력 버퍼와 즉시 연출만 QTE 계층에 노출한다.

- 새 구조화 결과 콜백과 `DefenseWindowOpened`, `DefenseResolved`, `DefenseWindowClosed` 이벤트를 제공한다.
- 기존 `(DefenseInput, QTEGrade)` 콜백은 새 API를 감싸는 호환 계층으로 유지한다.
- 취소된 실행은 결과 이벤트나 gameplay 콜백을 발생시키지 않는다.
- Sequence QTE도 unscaled 시간으로 진행한다.

### Consumer Integration

- `Action_DefenseWindow`는 요구 입력, 근접 성공 정책, 선택적 개별 판정 구간을 Inspector에서 설정한다.
- 기본 근접 적 공격은 기존처럼 세 입력 모두 허용하는 `Any` 요청을 사용한다.
- 두 경로 모두 `DefenseQteResult.PreventsDamage`를 사용해 성공을 판단한다.
- Perfect Parry MP 회복, 카메라, VFX, 피해 처리는 기존 소유 코드에 남긴다. QTE 계층은 이 효과를 직접 실행하지 않는다.

### Presentation

- `BattleUIController`에 방어 QTE 표시·결과·숨김 API를 추가한다.
- `QTEManager`가 방어창 수명에 맞춰 기존 `DefenseQTEUI`를 호출한다.
- 카운트다운과 결과 DOTween은 unscaled 업데이트를 사용한다.
- Invalid와 timeout을 별도 결과 문구로 표시한다.

## Compatibility

- 기존 `StartDefenseQTE`와 `StartDefenseQTEWithResult(float, float, Action<...>)`를 유지한다.
- 기존 ScriptableObject의 `DefenseRequirement` 숫자 값과 필드명을 유지한다.
- `BattleManager`, `IBattleTurnQteHost`, 전투 상태 머신의 공개 계약을 바꾸지 않는다.
- Scene, Prefab, Animator, ScriptableObject를 자동 수정하지 않는다.
- 적 전조 제작(`HTH-45`)과 캐릭터별 고유 자원 HUD(`HTH-43/46`)는 범위에서 제외한다.

## Verification

- 입력 일치 조합과 동시 입력 정책 단위 테스트
- 초 단위 등급 경계와 잘못된 프로필 정규화 테스트
- 성공·근접 성공·실패·무효 결과 테스트
- `Time.timeScale = 0`에서 방어창 timeout 통합 테스트
- 취소 시 결과 이벤트 미발행과 연속 실행 수명 테스트
- 두 기존 전투 호출부 컴파일·회귀
- 전체 Unity EditMode, TestMap, Content Validation, Missing Script 검사
