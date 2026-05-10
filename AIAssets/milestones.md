# HubToHome 마일스톤 요약

> 기준 시각: 2026-05-10 (KST)  
> 기준 데이터: `origin/main`에 반영된 커밋, 현재 작업 트리 변경분, `AIAssets/todo.md`

## 현재 진행 단계

| 영역 | 상태 | 판단 근거 |
| --- | --- | --- |
| 타이틀 -> 인트로 -> 이름 입력 루프 | 구현됨 | `TitleMenuManager`, `IntroManager`, `NameInputUI`가 연결되어 있음 |
| 오버월드 이동 / 상호작용 | 구현됨 | `PlayerController`, `InteractionSystem`, `AreaTrigger`가 동작 경로를 가짐 |
| 전투 진입 / 기본 루프 | 프로토타입 완료 | `BattleManager` 중심 흐름은 있음, 데이터/연출 확장 여지 큼 |
| 설정 UX / 입력 리매핑 | 진행 중 | `GameConfigManager`, `ConfigPanelUI`가 추가되었지만 통합이 덜 끝남 |
| 대사 선택지 분기 | 미완료 | `DialogueManager`의 `ShowChoices(...)` 호출이 주석 처리 상태 |
| 저장/이어하기 | 미완료 | `OnClickContinue(...)`가 실제 로드 흐름으로 이어지지 않음 |

## 2026-05-10 작업 중 스냅샷

### 커밋 / 푸시 상태

- 2026-05-10 기준 신규 커밋 없음
- 따라서 오늘자 요약은 "현재 작업 트리 변경분" 기준으로 정리

### 변경된 주요 파일

- `Assets/_Game/Core/Scripts/GameConfigManager.cs` 신규
- `Assets/_Game/Presentation/UI/Scripts/ConfigPanelUI.cs` 신규
- `Assets/_Game/Core/Scripts/AudioManager.cs`
- `Assets/_Game/Core/Scripts/LocalizationManager.cs`
- `Assets/_Game/Core/Scripts/UIManager.cs`
- `Assets/_Game/Features/Overworld/Scripts/PlayerController.cs`
- `Assets/_Game/Presentation/UI/Scripts/IntroManager.cs`
- `Assets/_Game/Presentation/UI/Scripts/NameInputUI.cs`
- `Assets/_Game/Presentation/UI/Scripts/TitleMenuManager.cs`

### 작업 해석

- 타이틀과 인트로에서 공용으로 쓰는 설정 저장소(`GameConfigManager`)를 도입해 언어, 볼륨, 전체화면, 키 설정을 한 곳으로 모으는 중입니다.
- `ConfigPanelUI`가 런타임 패널 형태로 추가되어 설정 메뉴의 실체가 생겼습니다.
- 오버월드와 UI 입력 일부가 `ConfigurableAction`을 통해 커스터마이즈 가능한 입력 경로로 옮겨지는 중입니다.
- 아직 `Continue`, 설정 패널 현지화, 설정 패널 입력 통일은 마감되지 않았습니다.

## 2026-05-09 푸시 기준 요약

> 저장소에서 `git push` reflog를 직접 확인할 수 없어, `origin/main`에 반영된 2026-05-09 커밋을 푸시 기준으로 간주했습니다.

### 커밋 로그

- `f751936` `FileMove`
- `6007152` `RuleUpdate`
- `2558828` `TitleIntro`
- `946d014` `LocalizationTest+CameraJitering Fix`

### 작업 모듈

- `Assets/_Game/Features/**`
- `Assets/_Game/Presentation/**`
- `Assets/_Game/Core/**`
- `Assets/Resources/LocalizationTable.csv`
- `RuleFileforAI/**`

### 반영된 변화

- 대규모 폴더 재배치로 `_Game` 하위 구조가 `Features / Presentation / Shared / Core` 중심으로 재정리됐습니다.
- 타이틀 -> 인트로 -> 이름 입력 -> 다음 씬 로딩까지 이어지는 초반 UX 루프가 연결됐습니다.
- `LocalizationManager`가 CSV 기반 문자열 로딩과 줄바꿈/따옴표 정리를 처리하도록 보강됐습니다.
- `PlayerController`, `InteractionSystem`이 최근 구조에 맞춰 정리되며 오버월드 상호작용 흐름이 안정화됐습니다.
- Rule 문서도 최신 구조 기준으로 업데이트됐습니다.

## 다음 마일스톤 게이트

1. 설정 시스템을 실제 플레이 루프에 완전히 통합하기
2. 타이틀 `Continue`를 저장/불러오기 흐름에 연결하기
3. 대사 선택지 분기를 다시 활성화해 스토리 확장 가능 상태 만들기
4. `InventoryManager`의 상태이상 TODO를 데이터 주도형 구조로 정리하기
