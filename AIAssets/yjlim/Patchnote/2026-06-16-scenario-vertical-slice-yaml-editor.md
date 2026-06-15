# 2026-06-16 패치노트 - 시나리오 vertical slice / YAML / 에디터

## 변경

- 더미 Game Module 기반 vertical slice 테스트를 추가했습니다.
  - Action Sequence에서 `module.switch` / `module.start` 실행
  - 더미 모듈이 `module.completed` outcome 보고
  - `BattleScenarioExecutionGate`가 후속 Action Sequence 실행
  - 현재 모듈 상태가 다음 시퀀스까지 유지되는지 확인
- `ScenarioSourceYamlParser`를 추가했습니다.
  - 현재 writer가 내보내는 Scenario YAML subset을 다시 읽을 수 있습니다.
  - writer -> parser -> importer -> `BattleScenarioData` 왕복 테스트를 추가했습니다.
- `ScenarioAuthoringWindow`를 보강했습니다.
  - 원본 YAML 검증 버튼 추가
  - 시퀀스 액션 삽입 / 위아래 이동 / 복제 / 켜기 / 끄기 / 삭제 추가

## 주의

- 이번 parser는 범용 YAML parser가 아니라 프로젝트 writer가 내보내는 deterministic subset용입니다.
- 커스텀 에디터는 아직 catalog 기반 액션 선택기, row별 validation badge, source YAML edit-back 저장까지는 지원하지 않습니다.
- Unity MCP 테스트 실행은 Editor instance 미연결로 실패했습니다. 컴파일과 C# LSP 진단은 통과했습니다.
