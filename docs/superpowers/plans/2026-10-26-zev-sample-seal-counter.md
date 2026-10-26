# ZEV 단독 샘플과 반복 잠금 Implementation Plan

> **For agentic workers:** REQUIRED: Use superpowers:subagent-driven-development (if subagents available) or superpowers:executing-plans to implement this plan. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 토끼 UI를 되돌릴 수 있게 숨기고 ZEV 대사·2행동마다 3스킬 잠금·C 성공 포커스/돌파를 완성한다.

**Architecture:** LabEntry의 선택적 설정과 기존 phase flag/실제 행동 완료 이벤트를 사용한다. 배우의 임시 잠금은 원본 스킬/저장과 분리하고 표시와 실행이 동일 가부를 읽는다. 반격은 기존 서비스·소유 카메라 scope만 확장한다.

**Tech Stack:** Unity6 C#, uGUI/TMP, DOTween, Odin, 기존 Scenario/Dialogue/Localization.

---

## Chunk 1: 기존 데이터와 수명 보존

### Task 1: Lab 진입·대사

**Files:** `Battle/Development/BunnySlimeBattleLabData.cs`, `BunnySlimeBattleLabSession.cs`, `BunnySlimeBattleLabSession.View.cs`, LabData 자산, `Dialogue_ZEVLab_opening.asset`, `Dialogue_ZEVLab_phase2.asset`, `Assets/Resources/LocalizationTable.csv`.

- [x] 기본 false `HiddenInMenu`, 선택적 `BattleSkillLockSettings SkillLock`을 Entry에 추가한다. 표시 인덱스와 원래 Encounter 인덱스를 분리한다.
- [x] 토끼만 숨기고 ZEV 설정에 `ActivationFlag=lab.zev.chain_phase`, `SkillCount=3`, `RefreshAfterActions=2`를 연결한다.
- [x] 세션 시작/실패/복귀/Disable/Destroy에 잠금 scope를 phase presentation과 같은 경계에서 연결·해제한다.
- [x] 개전 ZEV→위젤 2문장, 2페이즈 ZEV→ZEV→위젤 3문장으로 기존 화자/표정·키를 사용해 4언어를 맞춘다. Source/Runtime hash는 보존한다.
- [x] 기존 Lab 데이터 검사 기대값과 숨김 선택 회귀를 갱신한다. 전체 Builder 재실행은 하지 않는다.

### Task 2: 반복 스킬 잠금 규칙

**Files:** 새 `BattleSkillLockSettings`, 순수 잠금 선택 상태, `BattleSkillLockScope`; `Characters/Runtime/PlayerCharacter.cs` 또는 partial, `BattleTurnQteModuleControllerService.cs`, 관련 Editor tests.

- [x] 서로 다른 스킬을 중복 없이 최대3개 선택하는 순수 상태 검사(0~2개/중복/2행동 교체/취소 미소비)를 먼저 작성한다.
- [x] `PlayerCharacter.IsBattleSkillLocked(SkillData)`와 가부 변경 이벤트를 제공한다. 잠금 소유자별 해제/배우 파괴/구형 scope 정리가 새 scope에 영향을 주지 않게 한다.
- [x] scope는 battle start/phase flag/정상 아군 행동 완료를 구독한다. phase 즉시 추첨하고 해당 배우 행동2회마다 원자 교체한다. flag해제/종료/Dispose에 자기 잠금만 해제한다.
- [x] 메뉴 선택·대상 확정·AP 소모 직전의 실행 가부도 검사한다. 거부는 턴/AP를 소모하지 않는다.

## Chunk 2: 표시와 성공 반격

### Task 3: 회색 목록·거부 피드백

**Files:** `UI/Runtime/BattleMenuEntryPresentation.cs`, `BattleSubMenu.cs`, `BattleMenuUI.cs`, `OptionRowUI.cs`, 관련 테스트.

- [x] CanUse와 Describe가 배우 임시잠금을 읽게 한다. 바인딩 중 availability 변경 구독으로 목록을 갱신한다.
- [x] 잠긴 행을 고르고 확정하면 공용 `PlayDeniedFeedback`을 호출한다. 비모달 설명에 현지화 사용불가 문구를 표시한다.
- [x] 행 포인터는 기존 입력 허용/대화·pause/프레임 소비를 공유한다. 정상 클릭도 기존 확정·대상 선택 흐름으로 보낸다.
- [x] 거부 빨강/좌우흔들림은 소유 Tween과 원래 배치·색 복원으로 연타·재사용·Disable에 안전하게 만든다.

### Task 4: C 성공 포커스·돌파

**Files:** `BattleLinkCounterService.cs`, `PositionManager.cs`, `CameraController` partial 및 viewport/픽셀 보정, 기존 반격·카메라 tests.

- [x] C 후퇴거리 확대, 1초 포커스 대기, 적뒤 돌파 수치를 PositionManager에서 편집 가능하게 둔다. 기존 필드명은 유지한다.
- [x] 반격 서비스에서 후퇴→플레이어포커스→대기→접근/공격/피해1회→돌파→양측복귀를 연결한다. pause/취소·파괴 경계를 보존한다.
- [x] CameraController의 명시 scope에만 정수2배 확대를 허용한다. 일반 fixed Lens/HUD/PP는 유지하고 최신명령 토큰일 때만 복원한다.
- [x] 대기중피해0/공격피해1/대기·돌파취소/pairedreturn/카메라취소 검사를 갱신한다.

## Chunk 3: 통합 마감

- [x] `pwsh -NoProfile -File Library/CodexChecks/Chapter01Foundation/Compile.ps1 -Target All` 오류0.
- [x] 신규 잠금/반격/메뉴 관리형 회귀 및 기존 DefensePolicyReview/ParryRearm/EnemyScreenPass 검사. 테스트 경계는 native Unity와 구분한다.
- [x] 새 meta32hex/유일성, 관련 UTF8/BOM, Source/Runtimehash, 대사화자/번역/원본스킬 보존을 검사한다.
- [x] `AIAssets/2026-10-26-update.md`, feedback, Lab guide와 시나리오skill을 현행화한다. 커밋/푸시/강제Play·reimport는 하지 않는다.

검토: 조사/설계 리뷰 승인 후3개 구현 단위로 병행했다. 코드 리뷰에서 메인 탭 포인터의 이전 entry 인계 결함과 비기본 카메라 설정의 거짓 focus scope를 수정하고 회귀를 추가했다. 최종 수치·엔진 미검증 경계는 [feedback](../../../AIAssets/yjlim/feedback/2026-10-26-zev-sample-seal-counter.md)에 기록했다.
