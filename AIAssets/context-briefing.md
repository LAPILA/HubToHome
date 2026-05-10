# 다음 세션 브리핑

## 현재 단계 요약

- 초반 플레이 루프는 이미 보입니다. `타이틀 -> 인트로 -> 이름 입력 -> 다음 씬` 경로와 `오버월드 이동 -> 상호작용 -> 전투 진입` 경로가 둘 다 존재합니다.
- 오늘 기준으로 가장 큰 가치는 "새 기능을 넓히기"보다 "이미 열어놓은 시스템을 닫기"에 있습니다.
- 특히 현재 작업 트리의 중심이 설정 시스템이므로, 여기서 맥락 전환 없이 마감하는 것이 1인 개발 효율이 가장 좋습니다.

## 오늘 가장 먼저 할 일 3가지

### 1. 설정 시스템 통합 마감

- 이유: 지금 열려 있는 변경 파일 대부분이 설정/입력 계층이라 문맥 전환 비용이 가장 적습니다.
- 우선 수정할 파일:
  - `Assets/_Game/Core/Scripts/GameConfigManager.cs`
  - `Assets/_Game/Presentation/UI/Scripts/ConfigPanelUI.cs`
  - `Assets/_Game/Presentation/UI/Scripts/TitleMenuManager.cs`
  - `Assets/_Game/Core/Scripts/UIManager.cs`
  - `Assets/_Game/Core/Scripts/AudioManager.cs`
- 작업 가이드:
  - `ConfigPanelUI` 내부 이동/확정/취소도 `ConfigurableAction`을 따르도록 맞춥니다.
  - Voice 볼륨을 설정 항목에 포함할지 결정하고, 포함한다면 `GameConfigManager` 저장 키와 UI 행을 같이 추가합니다.
  - 패널 문자열을 `LocalizationManager`에서 읽게 바꿔서 설정 화면도 다국어 체인에 편입시킵니다.

### 2. 타이틀 `Continue`를 실제 로드 흐름으로 연결

- 이유: 지금 상태의 `Continue`는 UX상 가장 눈에 띄는 빈 버튼입니다.
- 우선 수정할 파일:
  - `Assets/_Game/Presentation/UI/Scripts/TitleMenuManager.cs`
  - `Assets/_Game/Core/Scripts/SaveManager.cs`
  - `Assets/_Game/Core/Scripts/SaveData.cs`
  - 필요 시 `Assets/_Game/Core/Scripts/GlobalDataManager.cs`
- 작업 가이드:
  - `PlayerPrefs.HasKey("SaveFileExists")` 검사로 끝내지 말고, 실제 저장 슬롯 존재 여부와 복구 경로를 연결합니다.
  - `Continue` 클릭 시 마지막 저장 슬롯 로드 -> `GlobalDataManager` 복원 -> 적절한 씬 이동 순서로 닫습니다.
  - 아직 저장 설계가 미완성이라면 버튼을 남겨두기보다 비활성/툴팁 처리하는 편이 더 안전합니다.

### 3. 대사 선택지 분기 다시 활성화

- 이유: 스토리 확장과 이벤트 브랜칭이 현재 구조에서 가장 큰 생산성 레버리지입니다.
- 우선 수정할 파일:
  - `Assets/_Game/Features/Dialogue/Scripts/DialogueManager.cs`
  - `Assets/_Game/Presentation/UI/Scripts/DialogueUI.cs`
  - `Assets/_Game/Features/Dialogue/Scripts/DialogueData.cs`
- 작업 가이드:
  - `DialogueManager`의 주석 처리된 `_activeUI.ShowChoices(...)` 경로를 다시 살립니다.
  - `ChoiceData`에서 다음 노드 이동, 플래그 설정, 자동 진행 차단이 어디까지 책임인지 먼저 정리합니다.
  - 현재 `DialogueUI`는 임시로 `Z/X/C`에 고정돼 있으므로, 이 입력 방식도 설정 시스템과 나중에 합칠 수 있게 분리해 두는 편이 좋습니다.

## 다음 후보

- 위 3개가 끝나면 `InventoryManager`의 `StatusFactory` TODO를 처리해 아이템/상태이상 구조를 데이터 주도형으로 바꾸는 것이 다음 순서입니다.
