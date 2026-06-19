# 코드베이스 요약

## Core
- `GameStateManager`: 전체 게임 상태 관리. `CanPlayerMove`로 이동 가능 여부 판단.
- `GlobalDataManager`: 이름, 파티, 인벤토리, 플래그, 스폰 좌표, PendingEnemies 보관.
- `SaveData`, `SaveManager`: JSON 저장/로드.

## Dialogue / UI
- `DialogueData`: ScriptableObject 대화 데이터.
- `DialogueManager`: 대화 시작/진행/종료, 이름 입력 이벤트 처리.
- `DialogueUI`: Typewriter 출력, 초상화/화자/보이스 블립 처리.
- `IntroManager`: 인트로 대화 → 이름 입력 → 다음 씬 이동.

## Overworld
- `PlayerController`: 즉각 반응형 8방향 이동, 전투 모드 전환, 전투 연출 일부.
- `InteractionSystem`: 전방 상호작용 대상 감지.
- `AreaTrigger`: 씬 전환, 자동 대화, 심리스 전투, BattleScene 전환.
- `Scripts/Map`: Room 기반 오버월드 전환 시스템. `DoorTransition`은 요청만 만들고, `MapTransitionService`가 Scene/Room 전환을 통합 처리한다.
- 맵 월드 산출물 위치: `Assets/_Game/Scenes/Overworld/MapWorlds`. 생성기는 `RoomMapSampleBuilder`, 검사는 `RoomMapValidator`.
- 기획자용 맵 가이드: `Assets/_Game/Scenes/Overworld/README_OverworldMapGuide.md`.

## Battle
- `BattleManager`: 전투 전체 흐름, 턴 큐, 행동 실행, QTE 결과 적용, 전투 종료.
- `BattleUIController`: BattleManager 이벤트 기반 UI 갱신.
- `BattleStateMachine`: 상태/커맨드/QTE 입력 enum.
- `SkillData`: 스킬 데이터. `ActionTimeline`으로 액션 블록 실행.

## Characters / Items
- `CharacterBase`: 공통 전투 단위.
- `PlayerCharacter`: 플레이어 전투/성장/장비/저장 연동.
- `EnemyCharacter`: 적 데이터 세팅과 AI 행동.
- `ItemData`: 아이템 타입, 대상, 효과, 상태이상 데이터.

## 주의할 점
- `Data/Scritps` 폴더명 오타가 있지만 Unity meta 참조 때문에 함부로 이름 변경하지 않는다.
- 선택지 대화는 현재 연결이 미완성이다.
- RuleFileforAI는 이 문서들을 기준으로 간단히 유지하고, 코드 변경 시 필요한 부분만 업데이트한다.