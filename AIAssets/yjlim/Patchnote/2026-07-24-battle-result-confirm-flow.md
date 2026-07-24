# 전투 결과 확인 입력 진행

## 변경 내용

- 승리 결과가 일정 시간 뒤 자동으로 사라지지 않고 확인키 입력을 기다린다.
- 첫 페이지에서 EXP, 골드, 드롭을 확인한다.
- 레벨업한 캐릭터는 한 명당 한 페이지로 분리해 레벨과 능력치 상승량을 확인한다.
- 일반 전투, 심리스 전투, 오버월드 즉시 처치가 같은 결과 흐름을 사용한다.
- 마지막 전투 입력이 결과 화면을 바로 넘기지 않도록 페이지별 최소 입력 지연을 적용했다.
- 지연값이 0이어도 같은 입력이 여러 결과 페이지를 넘지 않는다.
- 코루틴 강제 중단 시 Tween, 알파, Raycast 차단이 남지 않는 기존 정리 계약을 유지했다.
- 외부에서 결과 UI가 비활성화되면 입력 대기 코루틴도 즉시 취소된다.
- 중복 표시가 발생해도 오래된 결과 코루틴이 최신 패널이나 Fade를 정리하지 않는다.

## 검증

- 결과 UI 진행·중단·중복 실행: 6/6
- 실제 오버월드 즉시 처치 결과 흐름: 1/1
- 보상·레벨업 계산: 5/5
- TestMap 전투 조우: 6/6
- 전체 Unity EditMode: 844/844
- Project Content Validation: 오류 0건, 기존 경고 10건
- Prefab 59개, 하위 Transform 740개: Missing Script 0건
- 사용자 `TestMap.unity` 해시 유지

## 추적

- Jira: `HUBTOHOME-90`
- 설계: `docs/superpowers/specs/2026-07-24-battle-result-confirm-flow-design.md`
- 구현 계획: `docs/superpowers/plans/2026-07-24-battle-result-confirm-flow.md`
