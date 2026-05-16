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

### High / Battle Stability

- [ ] `CameraController`가 현재 `Assets/TextMesh Pro/Examples & Extras/Scripts/CameraController.cs`에 있어 first-party 전투 코드와 위치가 어긋납니다.  
  정리: 전투 카메라 컨트롤러를 `Assets/_Game/Presentation` 또는 `Assets/_Game/Features/Battle/Scripts` 아래로 이동하고 TextMesh Pro 샘플 폴더와 분리해야 합니다.  
  대상 파일: `Assets/TextMesh Pro/Examples & Extras/Scripts/CameraController.cs`, `Assets/_Game/Features/Battle/Scripts/BattleManager.cs`

- [ ] 방어 QTE 입력 표기를 코드/기획/UI에서 `Z=패링, C=회피, Space=점프`로 통일하기  
  정리: 런타임 판정은 수정했지만, `GameInput` 주석과 InputAction 이름(`QTE_X`, `QTE_C`)은 과거 `Z/X/C` 관성이 남아 있습니다. 리바인딩 UI까지 고려해 액션명과 표시 문자열을 정리해야 합니다.  
  대상 파일: `Assets/_Game/Core/Scripts/GameInput.cs`, `Assets/_Game/Presentation/UI/Scripts/QTEManager.cs`, `Assets/_Game/Presentation/UI/Scripts/DefenseQTEUI.cs`

- [ ] 적 스킬 타임라인의 방어 QTE 블록도 기본공격 QTE와 동일한 입력 정책/락 해제 정책을 공유하도록 통합하기  
  정리: 이번 수정은 `QTEManager`와 기본 적 공격 루프를 안정화했습니다. 스킬 `Action_DefenseWindow` 쪽도 같은 실패/중복 시작/연출 락 정책을 쓰는지 플레이테스트가 필요합니다.  
  대상 파일: `Assets/_Game/Features/Battle/Data/Scripts/SkillActionBlocks.cs`, `Assets/_Game/Presentation/UI/Scripts/QTEManager.cs`, `Assets/_Game/Features/Overworld/Scripts/PlayerController.cs`

- [ ] 전조증상 아트 파이프라인 확정하기 (`Sprite` vs `AnimatorTrigger` vs `PrefabVFX`)  
  정리: `Action_EnemyTelegraph`는 이제 세 표현 방식을 모두 받을 수 있게 열어두었습니다. 실제 프로젝트에서는 pixel 단일 일러, 깜빡임 애니메이션, 프리팹 VFX 중 어떤 조합을 메인으로 쓸지 정해야 합니다.  
  대상 파일: `Assets/_Game/Features/Battle/Data/Scripts/SkillActionBlocks.cs`, `Assets/_Game/Features/Characters/Data/Scripts/EnemyData.cs`

- [ ] 전조 후 방어 허용 윈도우(`open delay`, `window duration`)를 `DefenseWindow`와 실제로 연결하기  
  정리: 현재 필드는 추가했지만, 실제 판정 윈도우는 여전히 `Action_DefenseWindow.TimeWindow`가 직접 담당합니다. 전조 블록의 시간 데이터와 판정 블록을 자동 연동하는 후속 정리가 필요합니다.  
  대상 파일: `Assets/_Game/Features/Battle/Data/Scripts/SkillActionBlocks.cs`, `Assets/_Game/Features/Battle/Scripts/BattleManager.cs`

- [ ] 패배 연출과 씬 전환 정책 확정하기  
  정리: 패배 문구가 보이도록 hold 시간을 늘리고 전환 전 대기를 추가했지만, 전용 GameOver UI/리스폰/세이브 복구 정책은 아직 기획적으로 닫히지 않았습니다.  
  대상 파일: `Assets/_Game/Features/Battle/Scripts/BattleManager.cs`, `Assets/_Game/Core/Scripts/SceneLoader.cs`, `Assets/_Game/Core/Scripts/GlobalDataManager.cs`

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
