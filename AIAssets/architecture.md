# HubToHome 아키텍처 메모

루트 아키텍처 메모는 중복 방지를 위해 얇은 안내 문서로 유지합니다. 최신 종합 정리는 `AIAssets/yjlim/feedback/2026-06-19-work-summary.md`를 봅니다.

## 현재 큰 아키텍처 축

- `Core`: 전역 상태, 저장, 입력, 오디오, 씬 전환, 설정.
- `Features/Overworld`: 탐색, NPC/적 조우, 지역/방 기반 맵 프로토타입.
- `Features/Battle`: 기존 QTE/턴 전투, BattleManager 기반 전투 루프, 전투 결과 복귀.
- `Features/Scenario`: Scenario Source YAML, Battle Scenario Data, Action Sequence, Action Director, Game Module Runner, Sequence Maker.
- `Presentation`: UI, QTE, 전투 UI, 화면/오디오 연출 서비스.

## 최신 상세 문서

- 종합 현황: `yjlim/feedback/2026-06-19-work-summary.md`
- 다음 작업: `yjlim/TODO.md`
- 시나리오 파이프라인 규칙: `.agents/skills/hubtohome-scenario-authoring/SKILL.md`
- 공식 Sequence Maker 결정: `docs/adr/0006-single-sequence-maker-and-recoverable-runtime-editing.md`
- Sequence Maker 구현 계획/상태: `specs/002-sequence-maker-workbench/tasks.md`
