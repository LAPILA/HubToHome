# 다음 세션 브리핑

## 현재 단계 요약

- 프로젝트는 이제 "타이틀/인트로 프로토타입" 단계를 지나 `설정 시스템`, `오버월드 적 조우`, `대화-전투 연결`까지 붙은 첫 번째 플레이어블 수직 슬라이스에 들어와 있습니다.
- 다음 효율은 새 시스템을 더 벌리기보다, 이미 붙은 루프에서 끊긴 지점과 불안정한 지점을 닫는 데서 나옵니다.
- 우선순위 기준은 `사용자 체감 버그 -> 끊긴 플레이 루프 -> 확장 전 리팩터링` 순서가 가장 낫습니다.

## 오늘 가장 먼저 할 일 3가지

### 1. `Continue`를 실제 저장 복구 흐름으로 연결

- 이유: 지금 빌드에서 가장 눈에 띄는 끊김은 타이틀 `Continue`가 실질적으로 비어 있다는 점입니다.
- 먼저 볼 파일:
  - `Assets/_Game/Presentation/UI/Scripts/TitleMenuManager.cs`
  - `Assets/_Game/Core/Scripts/SaveManager.cs`
  - `Assets/_Game/Core/Scripts/SaveData.cs`
  - `Assets/_Game/Core/Scripts/GlobalDataManager.cs`
- 작업 가이드:
  - 버튼 노출 조건을 `PlayerPrefs.HasKey("SaveFileExists")`만 보지 말고 실제 저장 슬롯 존재 여부로 바꿉니다.
  - `Continue` 클릭 시 마지막 저장 슬롯 로드 -> `GlobalDataManager` 복원 -> 저장된 씬 로드 순서로 닫습니다.
  - 저장 구조가 아직 덜 닫혔다면 최소한 "로드 불가 시 버튼 숨김/비활성" 정책부터 명확히 고정합니다.

### 2. 오버월드 적 조우/도주 후 상태를 플레이테스트 기준으로 안정화

- 이유: 오늘 추가된 조우 시스템은 볼륨이 크고, 여기서 남는 버그는 게임 진행 전체를 흔듭니다.
- 먼저 볼 파일:
  - `Assets/_Game/Features/Overworld/Scripts/OverworldEnemy.cs`
  - `Assets/_Game/Features/Battle/Scripts/BattleManager.cs`
  - `Assets/_Game/Core/Scripts/GlobalDataManager.cs`
  - 필요 시 `Assets/_Game/Features/Overworld/Scripts/PlayerController.cs`
- 작업 가이드:
  - 도주 후 재조우 대기, collider 비활성, 깜빡임 알파, 복귀 타이밍이 실제 플레이에서 맞는지 먼저 체크합니다.
  - 전투 승리/도주/패배 각각에서 `CurrentEncounterEnemyId`와 cooldown state가 어떻게 남는지 로그를 찍어 확인합니다.
  - 안정화 전까지는 기능 확장보다 상태 전이 표를 먼저 고정하는 편이 낫습니다.

### 3. 설정 시스템 마감

- 이유: 관련 파일이 이미 많이 열려 있고, 지금 닫아두면 이후 UI/대사/전투 입력 작업이 편해집니다.
- 먼저 볼 파일:
  - `Assets/_Game/Core/Scripts/GameConfigManager.cs`
  - `Assets/_Game/Core/Scripts/GameInput.cs`
  - `Assets/_Game/Presentation/UI/Scripts/ConfigPanelUI.cs`
  - `Assets/_Game/Core/Scripts/AudioManager.cs`
  - `Assets/_Game/Core/Scripts/LocalizationManager.cs`
- 작업 가이드:
  - 설정 패널의 fallback 문구를 줄이고 `LocalizationTable.csv`로 옮깁니다.
  - Voice 볼륨이 정말 필요한지 결정한 뒤, 필요하면 저장 키와 UI 행을 같이 추가합니다.
  - `ConfigPanelUI`와 일반 UI 입력의 예외 경로를 정리해, "설정 모달일 때만 따로 돌아가는 입력"을 최소화합니다.

## 다음 후보

- 위 3개가 정리되면 `InventoryManager`의 상태이상 TODO를 `StatusFactory` 또는 레지스트리 형태로 통합하는 것이 다음 리팩터링 포인트입니다.
