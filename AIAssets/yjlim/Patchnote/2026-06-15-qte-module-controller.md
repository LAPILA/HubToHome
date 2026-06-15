# QTE 전투 모듈화 패치노트 - 2026-06-15

## 요약

- 기존 QTE 전투가 전투 시작 시 직접 `BattleState.TurnCalc`로 진입하던 흐름을 Game Module Runner 기반으로 정리했습니다.
- `turn_qte` 모듈의 핵심 entry와 실행 본문을 `IBattleTurnQteModuleController` 뒤로 모았습니다.
- 다른 Game Module로 전환된 뒤 기존 QTE 액션 종료가 턴을 계속 넘기는 위험을 막았습니다.

## 변경된 책임

- `turn_qte` controller가 담당:
  - 모듈 enter / exit / start
  - 턴 계산 진입
  - 턴 진행
  - player turn begin / enemy turn begin
  - 적 행동 진입
  - 플레이어 행동 선택
  - 스킬/아이템 하위 선택
  - 취소 / 타겟 확정
  - 플레이어 공격/스킬/아이템 실행
  - 적 행동 실행
  - 방어 QTE 판정
  - 액션 완료
  - 모듈 비활성 상태에서 QTE 흐름 중단
  - 모듈 exit 시 보류 중인 QTE 액션/스킬/아이템 정리

## 아직 남은 것

- controller는 아직 `BattleManager` nested class입니다. 인스펙터 serialized field와 씬 참조를 안전하게 재사용하기 위한 중간 형태입니다.
- 다음 안전 단계는 별도 Module/파일 분리를 위한 context object 설계입니다.
- Battle UI의 모듈별 표시/비표시 ownership은 아직 추가 정리가 필요합니다.

## 검증

- `dotnet build HubToHome.sln --no-restore` 통과
- C# LSP diagnostics 통과:
  - `BattleManager.cs`
  - `GameModuleActionRunner.cs`
  - `GameModuleActionRunnerTests.cs`
