# CONFIG·POWER UI 레이아웃 보정

## 원인

- CONFIG 제목은 설정 패널의 프레임 상단 여백보다 높은 기준 위치에 배치되어 테두리와 겹쳤다.
- POWER 런타임 뷰는 `CategoryWindow/Content`의 실제 폭보다 큰 531 기준 좌표를 사용했다. 부모 Content는 좌우 24씩 안쪽 여백을 가지므로 사용 가능 폭은 483이다. 앵커가 부모 폭을 제한해도 자식의 고정 좌표/폭은 자동으로 줄어들지 않아 우측으로 넘쳤다.

## 변경

- `Assets/_Game/Core/Prefabs/CoreSettings/UIManager.prefab`
  - `SettingsTitle` y 위치: 160 → 140
- `Assets/_Game/Scripts/UI/Runtime/PowerGrowthPanelView.cs`
  - POWER 뷰의 기준 가로 폭을 483으로 고정
  - 헤더 탭, 행, 스킬/상세 패널, 상태 텍스트를 해당 부모 폭 안으로 재배치
  - 스탯 행의 이름/등급/값 열도 새 행 폭에 맞게 조정
- `Assets/_Game/Scripts/UI/Tests/Editor/ConfigPanelLayoutAssetTests.cs`
  - CONFIG 제목 위치 계약 갱신

## 검증

- Unity refresh 및 컴파일 완료
- 콘솔 오류 0개
- `ConfigRegionsUseTheApprovedTwoColumnLayoutInsideBackground` 통과
- `TitleAndPreviewTextStayInsideTheirVisualEnvelope` 통과
- 전체 CONFIG 레이아웃 실행 시 기존 `SettingPanel.localScale = 0` 기대값 테스트만 별도 실패

## 후속

Windows Player에서 C 메뉴의 CONFIG 진입 후 제목과 POWER 진입 후 우측 스탯/스킬 경계가 프레임 안에 유지되는지 육안 확인한다.
