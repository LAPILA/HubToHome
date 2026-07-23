# Enemy Attack Authoring Design

## Goal

기존 `SkillData.ActionTimeline`을 적 공격 제작 표면으로 정리한다. 기획자는 코드 수정 없이 전조, 요구 입력, 판정창, 피해, 이동, 카메라 피드백을 조립하고 저장 전에 오류를 확인할 수 있어야 한다.

## Current Findings

- `SkillActionBlock`의 다형 목록이 이미 공통 블록과 특수 블록 확장점을 제공한다.
- `Action_Move`는 타겟 피벗과 `PositionManager`를 사용하므로 절대 월드 좌표를 새 데이터에 넣을 필요가 없다.
- `SkillData.ValidateSkillData`는 로그만 남기며 구조화된 결과나 실시간 Inspector 상태를 제공하지 않는다.
- `TelegraphThenWindow`도 전조 지속 시간을 기다리지 않아 이름과 실제 실행 순서가 다르다.
- 방어 실패 카메라 피드백은 강도와 안전 등급이 하드코딩돼 있다.
- 공격 블록별 예상 시간이 없어 긴 연계 공격의 리듬을 Inspector에서 비교하기 어렵다.

## Chosen Design

### Existing Timeline As The Single Authoring Surface

새 `EnemyAttackPatternData`나 별도 실행기를 만들지 않는다. `SkillData.UsageProfile == EnemyOnly`인 자산을 적 공격 패턴으로 취급하고 기존 실행 경로를 유지한다.

`SkillActionBlock.GetAuthoringTiming()`을 공통 확장 계약으로 추가한다. 기본 구현은 미지원 상태를 반환하고, 기본 제공 블록은 예상 시간과 변동 여부를 제공한다. 새 Custom Block은 실행 코드와 함께 이 메서드만 재정의하면 Inspector 시간축에 참여한다.

### Structured Analysis And Validation

`EnemyAttackAuthoringAnalyzer`는 Unity Editor에 의존하지 않는 읽기 전용 분석기다.

- 활성 블록의 누적 시작·종료 시간
- 비활성 블록 표시
- 방어창, 피해 블록 수
- 오류·경고 코드, 블록 인덱스, 메시지
- 예상 총 길이와 런타임 대상 수에 따라 변하는 구간

검사 범위는 음수·0 시간, 역전된 판정 구간, 판정창보다 긴 Good 구간, 선택한 전조 방식의 누락 참조, 음수 피해 배율, 미지원 Custom Block, 과도하거나 Cinematic인 방어 피드백을 포함한다.

기존 Project Content Validation은 같은 분석 결과를 소비한다. `SkillData`의 Odin Inspector에는 목록 자체의 `ValidateInput`, 읽기 전용 시간축 미리보기, 검사 버튼을 제공해 저장 전에 같은 결과가 보이게 한다.

### Telegraph Runtime Order

직렬화 enum 값은 유지한다.

- `ImmediateReaction = 0`: 기존 자산 동작을 보존한다.
- `TelegraphThenWindow = 1`: 전조 생성, 전조 지속, 추가 준비 지연, 판정창 순으로 실행한다.
- `TelegraphThenNextTurnWindow = 2`: 현재처럼 전조 단계만 실행하며 다음 턴 예약은 상위 적 행동 시스템이 소유한다.

전조 지속 시간과 공격 애니메이션 대기는 미리보기 계산에도 같은 규칙을 사용한다.

### Relative Movement And Future Camera Framing

`Action_Move.MoveDest` 끝에 `AttackStaging`을 추가한다. 이 값은 `PositionManager.GetAttackStagingPos(actor, target)`을 사용하며 기존 enum 숫자와 자산을 바꾸지 않는다.

공격 데이터에는 카메라 좌표나 고정 줌을 넣지 않는다. 캐릭터 간 거리가 변하거나 향후 카메라가 자동 줌을 사용해도 공격 위치 계산은 actor/target/PositionManager 기준으로 유지한다. 두 대상 자동 프레이밍은 `ICameraPresentationService`가 소유할 후속 작업이다.

### Camera Safety

`Action_DefenseWindow`의 실패 피드백에 강도, 지속 시간, `CameraShakeSafety`를 추가한다. 기본은 `GameplaySafe`이며 `CameraController.TryImpulse`가 현재 카메라 프로필의 최대 강도로 제한한다.

분석기는 다음을 저장 전에 표시한다.

- 0 이하 강도·지속 시간
- Gameplay Safe 프로필 한도를 넘는 요청
- 방어 반응에서 Cinematic 안전 등급 사용

카메라 피드백은 판정 결과가 확정된 뒤에만 실행하며 입력 구간 중에는 실행하지 않는다.

### Designer Template

빈 EnemyOnly `SkillData`에는 샘플 공격 블록 구성 버튼을 제공한다.

1. 자동 공격 위치로 접근
2. 전조 후 방어 판정
3. 피해
4. 원래 전투 슬롯으로 복귀

샘플은 외부 프리팹 참조를 임의로 연결하지 않는다. 기획자가 전조 표현 방식을 선택하면 누락 참조가 즉시 검사된다.

## Compatibility

- 기존 `SkillData`, `SkillActionBlock`, enum 숫자, 직렬화 필드 이름과 실행 진입점을 유지한다.
- 기존 ImmediateReaction 자산의 공격 박자는 바꾸지 않는다.
- Scene, Prefab, 기존 ScriptableObject를 자동 수정하지 않는다.
- ZEV 전용 시나리오나 선배 담당 전투 흐름을 수정하지 않는다.
- 기본 적 근접 공격을 SkillData로 옮기는 작업은 후속 범위다.

## Verification

- 분석기의 시간축, 비활성 블록, Custom Block 확장 계약 단위 테스트
- 누락 전조, 잘못된 판정 구간, 카메라 제한 검사 테스트
- 샘플 블록 생성과 상대 기준 이동 테스트
- Project Content Validation 연동 테스트
- 전체 Unity EditMode 테스트
- 실제 Project Content Validation과 Missing Script 검사
- 사용자 `TestMap.unity` 변경 제외 확인
