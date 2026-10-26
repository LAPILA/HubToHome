# 전체 적 공격 연속 대응 Implementation Plan

> **For agentic workers:** 승인된 연속 공격 방향을 기본 공격·스킬·투사체·연쇄 공격에 확장한다. 파일 영역별 구현과 독립 회귀 검토를 병행한다. 사용자 요청 없는 커밋·푸시는 하지 않는다.

**Goal:** 정지 후 전조를 기다리는 적 공격을 없애고 실제 접근·비행·공격 모션 중 전조와 방어 판정을 동기화한다.

**Architecture:** 방어 판정 정책과 연출 시계를 분리한다. 공통 적 방어 창은 감속/접점 freeze 없는 시계이며, 일반 근접은 기존 명중 시간 전체에 접근을 분산하고 제브 난무처럼 명시적 이동 시계를 쓰는 스킬은 현재 속도를 유지한다. C/광역의 제자리 공격은 기존 중앙 배치를 유지하고 공격 모션의 명중 프레임을 창 시작부터 명중 시각까지 재매핑한다. 예약 강공격의 턴 의미·피해·AP·가드·입력 유예·스토리/메뉴/성공 반격은 보존한다.

**Tech Stack:** Unity6, DOTween, 기존 SkillContext/DefenseWindow/QTEManager, Odin, 관리형 실제 소스 검사와 전체 CLI 컴파일.

## Chunk 1: 공통 시계와 근접 경로

- [x] 기존 중단 지점을 확인한다: DefenseWindow 사전 전조/Ready, 일반 근접의 DashStart 대기, QTE 감속/impact freeze, 적 타임라인 Ready/Wait/반응 대기.
- [x] `SkillActionBlocks.cs`의 ExecuteImpact에서 실제 공격의 사전 대기만 제거한다. NextTurnWindow는 예고만 하고 타격하지 않는 의미를 유지한다.
- [x] `SkillContext.EnemyMeleePresentation.cs`/`BattleEnemyMeleeDashPath.cs`에서 기본 연속 경로를 사용하되 일반 공격은 authored impact를 보존한다. 기존 Combo 이동 기준 계산은 보존한다.
- [x] `QTEManager.cs`/`BattleImpactTiming.cs`에 제자리 C/광역의 양수 애니메이션 재매핑을 연결한다. 전역 TimeScale은 변경하지 않는다.
- [x] 적 타임라인의 순수 Wait/Ready/중복 준비는 호환 no-op으로 처리하고 이동 완료 시 Idle을 강제로 끼우지 않는다. 아군 블록은 그대로다.

## Chunk 2: 빠지는 실행 경로

- [x] `BattleTurnQteModuleControllerService.cs`: 기본 근접/광역, 스킬 진입, 예약 예고의 강제 확인을 정리한다. 기본 피해/파티 광역 보상은 기존 한 번만 적용한다.
- [x] `Action_EnemyWindup.cs`: 직렬 준비 카메라/홀드는 실행하지 않고 저장 호환 필드는 유지한다.
- [x] `Action_Projectile`: 비행과 방어를 동시에 진행하고 실제 비행 종료에 시각 투사체를 반환한다. 늦은 입력 결과와 피해는 한 번만 확정한다.
- [x] `Action_SequentialMelee`: 적의 별도 접근→정지 방어를 없애고 기존 방어/이동 scope로 통합한다. 중간 반응/Recovery 대기를 제거하고 끝에서 한 번 정리한다. 아군은 보존한다.

## Chunk 3: 검증과 인수인계

- [x] `pwsh -NoProfile -File Library/CodexChecks/EnemyScreenPass/Check.ps1`: 일반 authored 시간 보존/시작부터 이동/가드 관통/모달/취소/투사체/연쇄/C 경계 검증. 이전 정지 기대값은 최신 승인 정책으로 교체한다.
- [x] `pwsh -NoProfile -File Library/CodexChecks/Chapter01Foundation/Compile.ps1 -Target All`: Runtime/Editor 오류0을 확인한다.
- [x] 방어144사례·재준비479+8, Lab데이터·스킬 참조·아군 QTE피드백 회귀를 다시 실행한다.
- [x] 실행/원본보존/UTF8/소유diff를 검토하고 AIAssets 당일 기록·피드백·CONTEXT·전투/저작 규칙을 갱신한다.

Unity Play/Refresh/씬 저장/네이티브 입력·GPU·실제 저장 I/O는 실행하지 않는다. 실제 체감/입력 박자와 관리형 경계 검증을 구분해 전달한다. Scene/Prefab/SaveID/Scenario Source 문법은 변경하지 않는다.
