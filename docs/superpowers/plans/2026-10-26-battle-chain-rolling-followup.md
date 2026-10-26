# 사슬·제브·순차 전투 기록 Implementation Plan

> **For agentic workers:** 승인 설계를 기준으로 독립 소유 파일별 병렬 구현과 상호 검토를 수행한다. 커밋은 사용자 요청이 없어 하지 않는다.

**Goal:** 사슬 크기/대칭/음량, 제브 일반4종 화면 관통, 고정3줄 순차 기록을 기존 데이터와 표시 경계에서 개선한다.

**Architecture:** PhaseOverlayProfile과 기존 적 DefenseWindow 데이터를 재사용한다. BattleNarrationUI는 TMP 줄 정보로 표시 범위만 관리하며 CombatLogPresenter는 실제 결과 집계만 담당한다.

**Tech Stack:** Unity6, uGUI/TMP, Odin, DOTween, 관리형 CLI 검사.

## Chunk 1: 독립 구현

- [x] 사슬: BattlePhaseOverlayProfile/Policy/Presentation/Tests·ZEV_ChainPhase.asset — 배율/모서리별 반전/음량을 데이터화하고 기존1재질/수명 유지. CheckOverlay.ps1 회귀와 확대/반전/0~1음량 검사.
- [x] 제브: Enemy/BattleLab/ZEV의4스킬 — Crash 확장 및 중복 AttackStaging RID/참조 정리. CheckData/ShowcaseAuthoring/SoloShowcase/EnemyScreenPass 검사.
- [x] 기록: BattleNarrationUI/CombatLogPresenter/UI tests·DialogueCanvas.prefab — 고정3줄/TMP 줄 범위/스크롤/줄별Z와 자동 진행, 크기/폰트 고정. 실제 루틴 관리형 재현·입력/색태그/취소 회귀.

## Chunk 2: 통합·인수인계

- [x] 독립 코드 검토 후 지적사항 수정.
- [x] 전체 Runtime/Editor CLI 오류0, 관련 메타/참조/UTF8/소유 경로 diff 확인.
- [x] AIAssets/2026-10-26-update.md·feedback·CONTEXT·도메인 규칙·실험실 가이드 최신화. 씬/다른 dirty 변경 보존.
- [x] 실제 엔진/음량/렌더 미검증을 명시하고 조정 위치와 함께 전달.

결과: Runtime478/Editor388오류0, 순차기록20사례360단언/집계13사례36단언/사슬44,481+14/적관통22사례87단언/데이터178 및15스킬114RID정합 검사 통과. 상세 [인수인계](../../../AIAssets/yjlim/feedback/2026-10-26-battle-chain-rolling-followup.md). Unity Play/GPU/TMP실제기하·음향·기기검사는 미실행이다.
