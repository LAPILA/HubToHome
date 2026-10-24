# 날씨·시작 흐름·피격 표시 구현 계획

> **For agentic workers:** executing-plans 절차로 파일 소유를 분리한다. 이름/인트로, 날씨, 피격/저장 조사는 각각 별도 담당. root는 통합·검증·문서를 소유한다. 사용자 요청 없는 커밋/push·Unity실행 금지.

**Goal:** 아트만 교체하면 쓸 수 있는 날씨 샘플과 고정 WIZZEL/창가 시작, 명확한 피격 피드백을 제공한다.

**Architecture:** 방 수명 Weather prefab, 기존 Global/Intro 경로 정리, 캐릭터 피격 표시 소유권을 분리한다. 기존 저장 및 시나리오 데이터 형식은 유지한다.

**Tech Stack:** Unity6 URP2D, ParticleSystem, SpriteRenderer/HLSL, Odin, 기존 DOTween 피격 반동·저장 서비스.

## Chunk 1: 읽기·분담

- [x] 기존 날씨 없음/이름UI 호출 제거상태지만컴포넌트잔류/국가4컷+현재+창가6페이지/직접·상태피해공통훅/저장atomic·backup 확인.
- [x] 사용자 시작선택: 창가 애니메이션 유지. 비는2D 착지점 고정, 안개는월드원형 구멍으로 승인 설계 확정.
- [x] 세 담당의 구현 경계/관련 테스트를 정하고 실행. 기존 사용자 작업 보존.

## Chunk 2: 병렬 구현

- [x] Overworld/Presentation 날씨 런타임·셰이더·재사용Prefab·편집 테스트. Sprite 슬롯 미연결시 임시 도형, 비 landing 수치/안개 alpha·수명 검증.
- [x] IntroManager/Prologue_Opening/NameInputUI/DialogueManager/GlobalDataManager 및 관련Prefab·테스트: 나라설명/현재페이지 제거, 창가0번/주행음유지, legacy 이름표시차단. 원본 아트·저장스키마 보존.
- [x] 캐릭터 공통 피격 표시: 직접타격만 local hit pause, true-white silhouette, 기존 반동/죽음정리 보존. 상태틱구분·재사용/취소·외부speed검사. 검토에서 overlay정렬 문제를 발견해 개별 재질 참조 방식으로 단순화.
- [x] 저장은 기존회귀/호환경계검토로 검증하고 확정 결함만 수정. 실제 사용자save I/O 금지. 메모리33검사 통과, 저장 재작성 없음.

## Chunk 3: 통합·인계

- [x] 전체 CLI Runtime/Editor 오류0. 자산GUID/meta/UTF8/직렬화참조와 셰이더·수치·수명 검사.
- [x] 분담코드 읽기전용 검토 및 범위내 보완. Unity/GPU 미검증 범위 명시. 레거시 틱/선호 스킬 취소/정렬/테스트 연결 수정.
- [x] AIAssets/2026-10-23-update.md, 사람용 사용법, CONTEXT/Rule/색인/계획 갱신. 새 아트 교체 위치·Prefab 배치·설정 위치 안내.
