# HUBTOHOME-44 방어 판정 파이프라인 검토 메모

## 결론

패링·회피·점프의 입력 선택, 요구 입력 일치, 시간 등급, 피해 방지 여부를 `DefenseJudgementPolicy` 한 곳으로 통합했다. 기존 전투 흐름과 공개 콜백은 유지하며, 기본 근접 공격과 `Action_DefenseWindow`가 동일한 구조화 결과를 사용한다.

## 런타임 구조

- `DefenseQteRequest`: 방어창 지속 시간, 난이도, 요구 입력, 판정 구간, BAD 판정 피해 방지 여부
- `DefenseQteResult`: 입력 상태, 실제 입력, 등급, 결과, 남은 시간, 요구 일치, 피해 방지 여부
- `DefenseJudgementPolicy`: Unity 프레임과 UI에 의존하지 않는 순수 판정
- `IDefenseInputSource`: 대상 캐릭터의 입력 버퍼와 즉시 연출만 QTE에 노출하는 좁은 계약
- `QTEManager`: 단일 실행 수명, 절대 실시간 시계, 취소, 결과 이벤트, UI 전달

## 기획자 설정

`Action_DefenseWindow`에서 다음 항목을 조정할 수 있다.

- 요구 입력: 패링 또는 회피, 점프만, 세 입력 모두, 패링만, 회피만, 회피 또는 점프
- `BAD 판정도 피해 방지`: 기존 감각을 유지하려면 활성화, 엄격한 공격은 비활성화
- `개별 판정 구간 사용`: 비활성화하면 `QTEManager` 공통값, 활성화하면 Perfect·Great·Good 초 단위 값을 공격별로 사용

기존 `ParryOrDodge=0`, `JumpOnly=1` 직렬화 값과 기존 공개 QTE 콜백은 유지했다. Scene, Prefab, Animator, ScriptableObject는 변경하지 않았다.

## 동작 규칙

- 한 프레임에 입력 하나만 유효하다. 동시 입력은 즉시 `Invalid`다.
- 입력이 없으면 시간초과 `Failure`, 요구 입력이 다르면 `Invalid`다.
- 일치 입력의 BAD 등급은 `NearSuccess`이며 공격 설정에 따라 피해를 막거나 받는다.
- 취소는 결과 이벤트와 gameplay 콜백을 발생시키지 않는다.
- 방어창과 UI는 `Time.timeScale=0`에서도 진행한다.
- 기본 근접 공격은 세 입력을 모두 허용하며 기존 Perfect Parry MP 보상을 유지한다.

## 검증

- 전체 Unity EditMode: 676/676 통과
- 방어 판정 정책: 23/23 통과
- QTE 실행·취소·명시 대상 입력: 3/3 통과
- `Time.timeScale=0` Play Mode 통합: 1/1 통과
- 방어 UI 결과 구분: 1/1 통과
- TestMap Play Mode: 2/2 통과
- Content Validation: 문제 0건
- `Assets/_Game` Prefab 58개: Missing Script 0건

## 남은 수동 확인

실제 키보드로 패링·회피·점프 각각의 애니메이션, Perfect Parry MP 증가, 공격별 BAD 피해 방지 토글을 한 차례 확인하면 된다. 현재 열려 있는 TestMap과 아트 변경은 사용자 작업으로 간주해 건드리지 않았다.
