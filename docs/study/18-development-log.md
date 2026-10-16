# 개발 기록과 설계 결정

[학습 홈](README.md) · [전체 코드 색인](20-code-atlas.md)

개발 기록은 **당시 문제와 판단을 보존**하고, 기능 설명은 **현재 코드**를 설명합니다. 두 문서를 섞으면 과거 버전의 저장 스키마나 이미 삭제된 샘플을 현재 기능으로 오해하게 됩니다.

## 주요 변화

| 기록일 | 변화 | 공부할 질문 | 원문 |
| --- | --- | --- | --- |
| 2026-07-12 | 공식 Sequence Maker, 명령 이력·복구·실행 관찰 | 왜 여러 편집기를 유지하지 않았는가 | ADR 0006 및 같은 날 피드백 |
| 2026-07-22 | 심리스 Host 수명과 방어 판정 경계 | 전투 중단의 정리 책임은 누구에게 있는가 | seamless-battle-host-lifecycle / defense-judgement-pipeline |
| 2026-07-23 | 저장 버전·임시 파일·백업 복구 | 본 파일 직접 덮어쓰기의 위험은 무엇인가 | atomic-save-recovery |
| 2026-08-09 | 스탯 레이어와 속성 피해 계약 | 장비와 전투 보정의 순서가 왜 중요한가 | character-stats-layered-model / attribute-damage-contract |
| 2026-09-07 | 콘텐츠 메이커와 학습 경로 | 제작 도구의 검증 책임은 어디에 둘까 | content-maker-handoff |
| 2026-09-26 | 전조 애니메이션·전투 HUD·카메라 타이밍 | 전조 시간과 판정 시간이 달라지면 어떤가 | telegraph-animation / battle-hud |
| 2026-09-27 | 위젤 압력·이동 잔상·픽셀 바람 | 공유 데이터와 현재량을 어떻게 나눌까 | wizel-pressure / movement-only-ghosts / pixel-wind |
| 2026-09-28 | 속도 턴과 버프 만료 예고 | 실제 큐를 소비하지 않고 미래를 계산하려면 | speed-turns |
| 2026-10-02 | 속성 누적·피해 합계·대상 선정·후열 연출 | 판정 상태와 표시 묶음은 왜 다른가 | element-status-lab / damage-totals / speech-and-party-wave |
| 2026-10-07 | 방어 입력·전조·후열 선택 등 기반 정리 | 게임오버와 후열 이어하기 경계는 무엇인가 | combat-foundation |
| 2026-10-08 | 전투 메인 대화를 서브메뉴에 연결 | 레이아웃 복구와 입력 복구를 누가 소유하는가 | battle-submenu-dialogue |
| 2026-10-09 | 자체 스크립트 중복·미사용 정리 | 삭제 안전성을 어떻게 증명했는가 | own-scripts-cleanup |
| 2026-10-11 | 설정 기록·수동/자동 저장·장비 안정화 | 실패 시 원본 불변성을 어떻게 검사하는가 | settings-save-equipment |

원문은 `AIAssets/yjlim/feedback/` 아래 날짜가 붙은 파일입니다. 표는 기록의 주제를 묶은 것으로, 각 기능이 그날 처음 개발됐다는 뜻은 아닙니다.

## 기록을 읽을 때 주의

- 7월 저장 문서의 schema v1은 당시 상태입니다. 현재 버전은 실제 SaveData/Codec를 확인해야 합니다.
- 증기 가속 샘플 스킬은 9월 기록에서 삭제됐고 공용 Haste 구현은 유지됐습니다. 샘플과 메커니즘을 분리합니다.
- 과거 문서의 push 예정·실패는 당시 기록입니다. 현재 원격 상태를 확인하지 않고 최신 배포 상태로 옮겨 쓰지 않습니다.
- 10월 9일 691→685개 정리 후 ProgressSaveTrigger가 추가돼 현재 자체 C#은 686개입니다.
- YAML 3,844개 비교와 관리형 155개 검사는 서로 다른 검사입니다. 합산해 “게임 전체 3,999개 테스트 통과”라고 쓰지 않습니다.

## ADR을 읽는 순서

1. `docs/adr/0001-battle-scenario-rules-and-save-scope.md`: 전투 시나리오와 저장 범위.
2. `0002-scenario-authoring-source-and-sync.md`: 제작 원본과 동기화.
3. `0003-scenario-yaml-parser.md`: 파서 의존성 경계.
4. `0004-sequence-maker-dual-authoring-surfaces.md`: 당시 편집 표면 결정.
5. `0005-catalog-driven-triggers-and-finite-sequence-orchestration.md`: 카탈로그와 유한 흐름.
6. `0006-single-sequence-maker-and-recoverable-runtime-editing.md`: 공식 창 통합과 복구.

새 ADR이 이전 결정을 대체할 수 있습니다. “모든 문서가 동시에 현재 정답”인 것은 아닙니다.

## 본인 개발 노트 템플릿

### 관찰한 문제
재현 장소, 입력 순서, 기대 결과와 실제 결과.

### 원인
확인한 호출 경로와 상태. 아직 추측인 부분은 별도 표시.

### 선택과 대안
택한 방식, 버린 방식, 비용·위험·호환성.

### 검증
실행 명령, 데이터, 성공/실패 결과, 실제 기기 여부.

### 내가 이해한 것
AI 설명 없이 5문장으로 설명. 아직 설명 못 하는 질문을 남김.

전체 날짜별 원문 목록은 [개발 기록 색인](development-log-index.md)에 있습니다.
