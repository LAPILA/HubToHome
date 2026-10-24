# Battle Impact Cuts Implementation Plan

**Goal:** 기존 전투 판정은 유지하고 준비–타격 카메라 컷·피격·음향·HP/상태 피드백을 통합한다.

**Architecture:** CameraController가 현재 shot/token의 유한한 시각 전환을, CharacterHitFeedback이 대상 표시 수명을, AudioManager가 타격 보이스 상한을 소유한다. PartySlotUI/StatusIconStrip은 이벤트에서 표시만 갱신한다. 기존 조작/상태/사망 처리 계층을 유지한다.

**Tech Stack:** Unity6/C#9, Cinemachine3, DOTween, Odin, uGUI. Unity 엔진은 실행하지 않는다.

## Chunk 1: 독립 표시 모듈

- [x] 기존 코드/규칙/이전 피격 설계 조사, 사용자 카메라 의도 확인
- [x] Camera/Runtime의 CameraController partial 및 Camera Editor tests: 준비 hold/명중 cut/token·방어 우선권
- [x] Core/Runtime/AudioManager + CombatHitAudioPolicy 및 Core Editor tests: 기존음원 보이스4개/명중 중복 제한
- [x] UI/Runtime/PartySlotUI·BattleStatusIconStrip·BattleEnemyStatusPresenter 및 UI Editor tests: 즉시HP+지연 잔여바/상태 전환 국소 표시

## Chunk 2: 공통 접점과 정리

- [x] Characters/Runtime/CharacterHitFeedback·CharacterBase·PlayerCharacter·EnemyCharacter 및 Editor tests: 기본 Animator 정지 해제/방향 반동/처치 표시/카메라 접점
- [x] Battle/Runtime/BattleManager의 기존 PlayCombatHitSfx 호출만 강조 정보 전달
- [x] Library/CodexChecks의 기존 생산 코드 검사 및 전체 CLI C# 컴파일, 독립 소유권 검토
- [x] AIAssets 당일 기록/사용법/CONTEXT/Characters·Battle 규칙 갱신

기존 dirty 변경과 자산을 보존한다. 씬/Prefab/DB/시나리오 문법은 변경하지 않는다. 단위 수치/수명 검사는 실제 카메라 체감·GPU/기기 검증을 대신하지 않는다.

결과: Runtime440/Editor366 CLI 오류0(경고225/0), 생산 피격201·카메라곡선10,027·오디오236·상태전환16,398 관리형 단언 통과. [설정/제외/검증 범위](../../../AIAssets/yjlim/feedback/2026-10-23-battle-impact-cuts.md).
