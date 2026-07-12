# 주요 런타임 코드 하드코딩 정리

## 범위

제품 런타임의 Core, Battle, Character, Item, Overworld, UI를 점검했다. ZEV Architecture Clone 전용 시나리오/생성기/프로브와 선배 담당인 Sequence Maker 실사용, 게임 전체 화면 효과 UI, Room Prefab 전투 시작 경로는 제외했다.

## 완료

- SceneName을 전투/대화/저장 기본 씬 계약으로 사용하도록 중복 문자열을 제거했다.
- 언어 PlayerPrefs 저장 소유권을 GameConfigManager로 통합했다.
- GameInput이 생성된 Input System 강타입 접근자를 사용하도록 바꿔 Action Map/Action 이름 문자열 탐색을 제거했다.
- 실제 저장 파일 기준 SaveManager.HasAnySave()를 추가하고 타이틀 Continue 표시에서 사용되지 않는 SaveFileExists PlayerPrefs 키를 제거했다.
- 오래된/부분 세이브의 null 컬렉션과 빈 씬 이름을 GlobalDataManager가 안전하게 복구하도록 했다.
- 상태이상 ID와 생성 로직을 StatusEffectIds/StatusEffectFactory로 통합했다. 전투, 스킬, 인벤토리가 같은 생성 경로를 사용한다.
- ItemData Inspector에서 등록된 상태이상 ID를 선택/검증할 수 있게 했다.
- 캐릭터 피벗 ID를 CharacterPivotId로 통합하고 CharacterBase에서 계층 탐색 결과를 캐시한다.
- 전투 턴 큐 표시 정책을 BattleTurnQueueProjection으로 분리했다.
- 도주 확률을 BattleRunPolicy로 분리하고 기존 60%를 Inspector 설정으로 노출했다.
- QTE Module에 남아 있던 사용되지 않는 별도 도주 코루틴을 제거했다.
- 오버월드 선공 판정을 Unity 6 ContactFilter2D 기반 OverlapCircle API로 교체했다.
- PlayerController의 값만 쓰고 읽지 않던 방어 잠금 필드를 제거했다.
- UI 패널 문자열을 UIPanelId로 통합했다.
- Pixel Perfect Camera 전체 탐색을 씬 단위 캐시로 변경했다.
- QTE 서비스 테스트가 PositionManager 위치를 구성하지 않던 픽스처 결함을 수정했다.

## 검증

- Unity 스크립트 컴파일: 성공, 오류/경고 없음.
- EditMode 40개 통과:
  - StatusEffectFactory 11
  - CharacterPivot 2
  - BattleRunPolicy 4
  - BattleTurnQueueProjection 3
  - GlobalDataManager save compatibility 2
  - BattleTurnQteModuleControllerService 1
  - EncounterMemorySave 5
  - BattleSkillTimelineRunner 6
  - CameraPresentation 5
  - TimelineCameraLeaseIntegration 1
- 최종 에디터 상태: Play Mode 아님, Region_MapFieldStarter 유지.
- 씬/프리팹/ScriptableObject 직렬화 파일은 이번 정리 작업에서 수정하지 않았다.

## 남은 우선순위

1. CharacterDatabase는 Resources.LoadAll<CharacterData>(string.Empty)를 사용하지만 현재 Resources 아래 CharacterData가 없다. 명시적 Character Catalog 또는 Addressables/런타임 등록 정책을 먼저 결정해야 한다.
2. BattleManager는 여전히 전투 조립, 참가자 생성, 결과 복귀, 아이템 효과 이벤트를 함께 소유한다. 다음 분리 후보는 Encounter Lifecycle과 Item Effect 적용이다.
3. PlayerController는 이동, 선공, 전투 방어 연출을 함께 소유한다. serialized field를 유지한 채 Defense Presentation Module을 먼저 추출하는 편이 안전하다.
4. F 선공 입력은 Input Actions/키 설정 목록에 별도 Action이 없어 직접 키 fallback으로 남아 있다. 입력 에셋과 Config UI를 함께 변경하는 별도 기능 작업으로 처리해야 한다.
5. 전투 내레이션의 한국어 런타임 문자열은 Localization Table 키 설계 후 데이터화해야 한다.
6. ZEV 생성 에셋의 trailing whitespace는 Unity 생성 직렬화 결과이며 이번 범위에서 손대지 않았다.

## 문서

- 설계: docs/superpowers/specs/2026-07-12-runtime-code-hardcoding-cleanup-design.md
- 실행 계획: docs/superpowers/plans/2026-07-12-runtime-code-hardcoding-cleanup.md