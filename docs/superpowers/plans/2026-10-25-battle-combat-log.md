# 전투 상단 결과 기록 Implementation Plan

> **For agentic workers:** 승인한 설계의 구현이다. 독립 파일 소유권을 나눠 수행하고 소스/명세 검토를 받는다. 커밋·푸시·Unity Play/Refresh·실제 저장은 실행하지 않는다.

**Goal:** 피격 자동 말풍선 대신 전투 상단에 행동별 실제 결과를 읽기 쉽게 표시한다.

**Architecture:** 기존 BattleNarrationUI와 DamageBatches를 재사용한다. 표시 전용 관찰기가 HP/AP 실제 값과 상태 전후 종류만 수집한다. 전투 서비스는 유효 행동 메타데이터만 발행하고 결과 대기 전에 묶음을 닫는다.

**Tech Stack:** Unity6/C#/기존 이벤트와 Coroutine/TMP/Odin/기존4언어 LocalizationTable.

## Chunk 1 · 상태 변화와 결과 수집

- [x] `UI/Runtime/BattleStatusTransitionTracker.cs`에 해제 항목 조회를 하위 호환으로 추가하고, 기존 초기/갱신/신규 비교를 유지한다. 상태 변화 회귀를 Editor 테스트에 추가한다.
- [x] `UI/Runtime/BattleCombatLogPresenter.cs`에 배우 구독과 HP/AP 스냅샷, 행동별 대상 결과 합산, 배치 종료/취소/해제 수명을 작성한다. 기존 BattleManager/CharacterBase의 이벤트 시그니처를 사용한다.
- [x] `UI/Runtime/BattleCombatLogFormatter.cs`에 행동·HP/AP·상태·행동불가의 현지화 표시를 작성하고 `Assets/Resources/LocalizationTable.csv`에4언어 키를 추가한다. 각 새 C#의32hex 고유 meta를 작성한다.

## Chunk 2 · 기존 전투에 연결

- [x] `BattleManager.cs`와 표시용 호스트 계약에서 행동 시작을 전달한다. `BattleTurnQteModuleControllerService.cs`의 공격/스킬/아이템/적 실행에서 유효 행동을 알리고 기존 상태에 의한 턴 건너뛰기 이유를 표시한다. 아이템 배치를 기존 결과 대기 전에 닫는다.
- [x] `BattleUIController.cs`에서 관찰기를 시작/재바인딩/종료한다. DamageTaken/LowHp 자동 호출과 기존 회복 로그 중복만 제거하며 HP/AP바와 숫자 VFX는 유지한다.
- [x] 한 행동의 연타/광역은 결과를 합산해 자동 안내 한 묶음으로 내보내고, 실제 공격/QTE 중 새 안내가 Z를 소비하지 않는 경계를 확인한다. 기존 이야기 대화는 그대로다.

## Chunk 3 · 검증과 정리

- [x] ignored 관리형 하네스로 실제 새 수집/상태/문구 소스와 대역 경계를 검사한다. 기대: 연타50회 단일 대상 합계/실제 증감/상태 중복 없음/해제 후 이벤트 없음.
- [x] `Library/CodexChecks/Chapter01Foundation/Compile.ps1 -Target All` 기대 오류0. `git diff --check`, UTF-8/신규 meta32hex·중복 검사를 수행한다.
- [x] 독립 소스 검토 후 발견 사항만 수정한다. `AIAssets/2026-10-25-update.md`, 사용자 안내와 관련 규칙을 현행화한다. Unity 체감·4언어 글리프·실기기는 미검증으로 남긴다.

최종: Runtime465/Editor383 CLI 오류0, 결과13사례36단언·입력19사례61단언·실제 번역표452단언 통과. 신규meta3개 유일32hex, 기존 생성콘텐츠166해시 보존. 종료 상태 정리/모듈Clear 순서/중단상태HUD재활성화 검토 사항을 수정하고 독립 재검토했다. 엔진 Play/NUnit/실기기·실제 저장은 미실행이다.
