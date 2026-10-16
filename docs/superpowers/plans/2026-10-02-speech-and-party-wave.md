# 전투 말풍선·후열 진입 구현 계획

> 승인: 사용자 — 텍스트/아이콘/혼합 선택, 전열 전멸 후 0.5초 페이드와 왼쪽 화면 밖 후열 진입. 기존 요청대로 서브 에이전트와 Unity Play/Refresh 없이 수행한다. 커밋·푸시는 하지 않는다.

**Goal:** 기존 전투 규칙을 유지하며 말풍선 콘텐츠와 후열 교체의 시인성을 개선한다.

**Architecture:** BattleSpeechRule에 선택적 Sprite와 표시 방식을 추가하고 기존 BattleSpeechBubble에서 재사용한다. 파티 승계 규칙은 BattleManager에 두고, 색/좌표/트윈 수명은 전용 BattlePartyWavePresentation이 소유한다.

**Tech Stack:** Unity, uGUI/TMP, Odin, DOTween, NUnit 회귀 코드, CLI C# 빌드.

## 1. 말풍선

- [x] 기존 텍스트·폰트·레이아웃·픽셀 크기 및 프리팹 연결 조사.
- [x] BattleSpeechRule에 텍스트(기본)/아이콘/혼합 모드와 Sprite 추가. CloneRuntime에서도 보존.
- [x] 기존 Show 오버로드 보존, 아이콘 전용 출력과 혼합 레이아웃 지원. 미지정 아이콘은 텍스트로 안전하게 폴백.
- [x] 생성된 Image를 캐시/재사용하고 아이콘→텍스트 전환 시 잔여 표시 제거.
- [ ] 글자 깨짐은 사용자 증상 또는 화면 증거를 확인한 뒤 수정. 폰트 전체나 픽셀퍼펙트 카메라는 임의로 교체하지 않는다.
- [x] BattleSpeechBubbleLayoutTests에 아이콘/혼합/기존 텍스트 회귀 검사 추가.

## 2. 후열 승계

- [x] 전열 전멸·유효 후열 존재를 먼저 검사. 전열 사망 페이드 0.5초 동안 즉시 교체하지 않는다.
- [x] 기존 TryPromoteReservePartyWave를 사용해 승계한 뒤 화면 왼쪽 밖에서 Move 상태로 배치.
- [x] DOTween으로 약 0.55초 동안 각 지정 슬롯에 진입, 도착 후 BattleIdle. 수치는 Inspector에서 조절.
- [x] 전환 동안 턴 계산/메뉴 선택을 진행하지 않는다. 완료 후 기존 내레이션과 턴 계산 재개.
- [x] 취소/전투 종료/파괴 시 소유 트윈만 정리하고 스프라이트 원색·알파·진입 좌표·Idle 복원.
- [x] BattlePartyWaveRuntimeTests에서 승계 규칙, 취소·파괴·화면 밖 좌표 검증 코드 추가(컴파일만).

## 3. 검증·전달

- [x] Runtime/Editor CLI 컴파일 오류0. 새 파일은 임시 MSBuild include로 검사, Unity Refresh 없음.
- [x] 실제 콘텐츠 레이아웃 소스 관리형 검증6,002항목 통과. 화면 좌표/DOTween/Animator는 실제 재생하지 않음.
- [x] UTF-8 및 scoped git diff --check.
- [x] AIAssets 업데이트, 조정 위치와 수동 확인 항목 기록.

미완료: 글자 깨짐 유형/스크린샷 답변 대기. 폰트 선명도 수정과 실제 렌더링 검증은 완료로 취급하지 않는다.

## 2026-10-05 안정화 후속

- [x] 초기화 멱등성, 빈 콘텐츠 제외, 실제 출력 성공 이후 1회 발동 기록.
- [x] 후열 전체 수명 finally 정리, 비활성화 취소, 파티 이벤트 뒤 세대 검사, 연출 트윈 중단 시 승계 진행.
- [x] 회귀 검사 코드 추가/컴파일. Unity 실제 재생은 여전히 수동 확인 대상.
- 상세: `AIAssets/yjlim/feedback/2026-10-05-battle-presentation-cleanup.md`.
