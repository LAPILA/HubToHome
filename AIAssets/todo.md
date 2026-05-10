# HubToHome TODO 중앙 정리

> 스캔 범위: `Assets/_Game`, `RuleFileforAI`  
> 태그 기준: `// TODO:`, `// FIXME:`, `// NOTE:`  
> 제외 범위: 외부 플러그인/샘플/Unity 생성 폴더

## 태그 스캔 결과

| 태그 | 건수 |
| --- | ---: |
| TODO | 1 |
| FIXME | 0 |
| NOTE | 0 |

## 코드 태그 인벤토리

### Medium / Items

- [ ] `Assets/_Game/Features/Items/Scripts/InventoryManager.cs:87`  
  원문: `// TODO: 나중에 StatusFactory를 통해 문자열로 상태이상 클래스 매핑 로직을 작성해야 합니다.`  
  메모: `InventoryManager`가 문자열 기반 분기에 머물러 있어 상태이상 아이템 확장이 어렵습니다.

## 이번 실행에서 정리된 파생 작업

### High / Config

- [ ] 설정 패널 입력을 `GameConfigManager`의 실제 키 바인딩과 통일하기  
  대상 파일: `Assets/_Game/Presentation/UI/Scripts/ConfigPanelUI.cs`, `Assets/_Game/Core/Scripts/GameConfigManager.cs`

- [ ] 설정 패널 텍스트를 현지화하고 Voice 볼륨까지 설정 스펙을 닫기  
  대상 파일: `Assets/_Game/Presentation/UI/Scripts/ConfigPanelUI.cs`, `Assets/_Game/Core/Scripts/AudioManager.cs`, `Assets/_Game/Core/Scripts/LocalizationManager.cs`

### High / Title-Save

- [ ] `Continue` 버튼을 실제 저장 데이터 로드 흐름에 연결하기  
  대상 파일: `Assets/_Game/Presentation/UI/Scripts/TitleMenuManager.cs`, `Assets/_Game/Core/Scripts/SaveManager.cs`, `Assets/_Game/Core/Scripts/SaveData.cs`

### High / Dialogue

- [ ] `DialogueManager`의 선택지 분기 경로를 다시 활성화하기  
  대상 파일: `Assets/_Game/Features/Dialogue/Scripts/DialogueManager.cs`, `Assets/_Game/Presentation/UI/Scripts/DialogueUI.cs`, `Assets/_Game/Features/Dialogue/Scripts/DialogueData.cs`

## 완료 또는 정리된 항목

- [x] 타이틀 `Settings` 버튼은 더 이상 빈 껍데기가 아니며 `ConfigPanelUI`를 여는 경로로 연결됐습니다.
- [x] 이름 입력 길이 제한과 공백 정리가 들어가 예전의 느슨한 입력 상태는 정리됐습니다.
- [x] 이번 스캔에서 `FIXME`, `NOTE` 태그는 발견되지 않아 별도 항목을 유지하지 않습니다.
