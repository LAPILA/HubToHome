# HubToHome TODO 중앙 정리

> 스캔 범위: `Assets`, `RuleFileforAI`  
> 태그 기준: `// TODO:`, `// FIXME:`, `// NOTE:`  
> 제외 규칙: 외부 샘플, 서드파티 예제, Unity 생성 폴더는 별도 참고만 하고 제품 TODO로는 추적하지 않음

## 코드 태그 스캔 요약

| 분류 | 건수 | 처리 상태 |
| --- | ---: | --- |
| First-party `TODO` | 1 | 활성 추적 |
| First-party `FIXME` | 0 | 없음 |
| First-party `NOTE` | 0 | 없음 |
| External sample `TODO` | 1 | 추적 제외 (`Assets/TextMesh Pro/Examples & Extras/...`) |

## 코드에서 직접 발견된 TODO

### Medium / Items

- [ ] `Assets/_Game/Features/Items/Scripts/InventoryManager.cs:87`  
  원문: `// TODO: 나중에 StatusFactory를 통해 문자열로 상태이상 클래스 매핑 로직을 작성해야 합니다.`  
  정리: 상태이상 적용이 문자열 분기로 남아 있어 아이템 추가 시 전투 로직과 데이터 정의가 같이 흔들립니다.  
  대상 파일: `Assets/_Game/Features/Items/Scripts/InventoryManager.cs`, `Assets/_Game/Features/Battle/Scripts/BattleManager.cs`, `Assets/_Game/Features/Characters/Scripts/StatusEffect.cs`

## 파생 작업 목록

### High / Encounter Stability

- [ ] 전투 도주 후 오버월드 적의 collider, 이동 상태, 재조우 쿨다운이 어긋나지 않도록 안정화하기  
  대상 파일: `Assets/_Game/Features/Overworld/Scripts/OverworldEnemy.cs`, `Assets/_Game/Features/Battle/Scripts/BattleManager.cs`, `Assets/_Game/Core/Scripts/GlobalDataManager.cs`  
  근거: `OverworldEnemy_v1` 커밋 설명에 남은 버그가 직접 언급돼 있고, 현재 로직도 플레이테스트 의존 구간이 많습니다.

### High / Save Flow

- [ ] 타이틀 `Continue`를 실제 세이브 슬롯 로드와 씬 복구 흐름에 연결하기  
  대상 파일: `Assets/_Game/Presentation/UI/Scripts/TitleMenuManager.cs`, `Assets/_Game/Core/Scripts/SaveManager.cs`, `Assets/_Game/Core/Scripts/SaveData.cs`, `Assets/_Game/Core/Scripts/GlobalDataManager.cs`

### Medium / Config Polish

- [ ] 설정 패널 문구를 `LocalizationTable.csv` 기준으로 정리하고 fallback 문자열 의존도를 줄이기  
  대상 파일: `Assets/_Game/Presentation/UI/Scripts/ConfigPanelUI.cs`, `Assets/_Game/Core/Scripts/LocalizationManager.cs`, `Assets/Resources/LocalizationTable.csv`

- [ ] Voice 전용 볼륨이 필요한지 결정하고, 필요하면 `GameConfigManager`와 `AudioManager` 스펙을 닫기  
  대상 파일: `Assets/_Game/Core/Scripts/GameConfigManager.cs`, `Assets/_Game/Core/Scripts/AudioManager.cs`, `Assets/_Game/Presentation/UI/Scripts/ConfigPanelUI.cs`

### Medium / Dialogue Authoring

- [ ] 선택지 텍스트도 현지화 키 기반으로 옮겨 대사 본문과 같은 번역 파이프라인에 태우기  
  대상 파일: `Assets/_Game/Features/Dialogue/Scripts/DialogueData.cs`, `Assets/_Game/Presentation/UI/Scripts/DialogueUI.cs`, `Assets/Resources/LocalizationTable.csv`

## 완료 또는 정리된 항목

- [x] 타이틀 `Settings` 버튼은 더 이상 빈 버튼이 아니며 `ConfigPanelUI`를 여는 실제 경로와 연결됐습니다.
- [x] `DialogueManager`의 선택지 분기와 선택지 기반 전투 시작 경로가 다시 살아났습니다.
- [x] 텍스트 속도 설정이 `DialogueUI` 런타임 출력과 미리보기 양쪽에 반영되도록 연결됐습니다.
- [x] 이번 스캔 기준 first-party `FIXME`, `NOTE` 태그는 없어 별도 추적 목록을 유지하지 않습니다.
