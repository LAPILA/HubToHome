# 적 공격 제작 구조 인수인계

## 기획자 사용 흐름

1. EnemyOnly `SkillData`를 새로 만들거나 기존 자산을 복제한다.
2. 빈 자산이면 Inspector의 `샘플 전조 공격 블록 구성`을 눌러 기본 흐름을 만든다.
3. `Action_Move`의 `AttackStaging`으로 공격자를 대상 앞의 자동 공격 위치에 배치한다.
4. `Action_DefenseWindow`에서 전조 방식, 전조 시간, 판정 시작 지연, 판정창, 요구 행동을 설정한다.
5. 방어 실패 시 카메라 효과는 `GameplaySafe`를 기본으로 두고 강도와 지속 시간을 조절한다.
6. Inspector의 시간축과 검증 요약을 확인한 뒤 Project Content Validation을 실행한다.

## 시간축 해석

- 각 블록은 목록 순서대로 실행되며 Inspector에 시작·종료 시각이 표시된다.
- 비활성 블록은 실행 시간에서 제외된다.
- `TelegraphThenWindow`는 전조 시간과 준비 지연 후 방어 판정창을 연다.
- 참조 누락, 음수 시간, 전조보다 빠른 판정 종료, 과도한 카메라 값은 저장 전에 표시된다.
- Custom Block은 `GetAuthoringTiming()`을 재정의해야 정확한 시간축에 포함된다.

## 설계 경계

- 한 번의 적 공격은 `SkillData.ActionTimeline`이 담당한다.
- 페이즈 전환, 대사, 음악, 전투 모듈 전환 같은 전투 전체 흐름은 Battle Scenario와 Action Sequence가 담당한다.
- 공격 위치는 `PositionManager`가 계산한다. 기획 데이터에 월드 절대 좌표를 넣지 않는다.
- 화면 흔들림은 방어 입력 중 읽기 어려워지지 않도록 `GameplaySafe`를 기본값으로 사용한다.
- 공격자와 대상이 멀 때 자동 줌아웃하고 다시 복귀하는 프레이밍은 향후 `ICameraPresentationService`에서 공통 처리한다.

## 검증 결과

- Unity 전체 EditMode 716/716
- TestMap 심리스 전투 6/6
- Project Content Validation 오류 0건, 기존 선택 아트 경고 10건
- `_Game` Prefab 59개 Missing Script 0건
- Scene, Prefab, ScriptableObject 변경 없음
