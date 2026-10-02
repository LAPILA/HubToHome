# 심리스 전투 진입 — 경고·절단·개방

## 적용 범위

사용자 승인대로 **심리스 전투에만** 기본 적용했습니다. 별도 `BattleScene`으로 이동하는 경로는 그대로입니다. 턴 계산·판정·적 스킬·전투 시작 대사도 바꾸지 않았습니다.

참고 GIF의 화면 흐름을 uGUI 도형과 DOTween으로 구현했습니다. 원본 화면을 캡처해서 자르는 방식은 아니며, 사선 검정 덮개와 밝은 절단선으로 장면 전환을 가립니다. 새 이미지·셰이더·RenderTexture가 필요하지 않습니다.

## 바로 확인하기

1. 기존 심리스 전투가 설정된 맵에서 적과 조우합니다. 새 컴포넌트를 직접 붙이거나 프리팹을 다시 만들 필요는 없습니다.
2. 플레이어 머리 위 경고 → 짧은 확대 → 왼쪽에서 오른쪽 사선 절단 → 암전 → 중앙에서 위아래 개방 → 하단 HUD 등장 순서입니다.
3. 개방 뒤 기존 전투 시작 대사/시나리오가 실행되고 명령 입력이 열립니다. 기존 대사 대기 시간은 아래 연출 시간에 포함하지 않습니다.

공용 설정은 `Assets/_Game/Content/Battle/Prefabs/System/SeamlessBattleHost.prefab`의 **SeamlessBattleHost → 일반 심리스 전투 진입**에서 변경합니다. 맵 인스턴스의 같은 필드로 해당 맵만 덮어쓸 수도 있습니다.

| 설정 | 기본값 | 의미 |
|---|---:|---|
| 빠른 진입 연출 사용 | 켜짐 | 끄면 기존 심리스 접근 연출 사용 |
| 경고 | 0.20초 | 기존 Top 피벗 우선, 없으면 스프라이트 머리 높이 |
| 줌 | 0.50초 | 필드 Follow/구도를 유지하며 카메라 크기 0.9배, 약 11% 확대 |
| 사선 절단 | 0.20초 | 화면 왼쪽에서 오른쪽으로 검정 덮개 이동 |
| 암전 최소 유지 | 0.35초 | 완전 검정 화면 아래에서 참가자/UI/카메라 준비와 동시에 진행 |
| 상하 개방 | 0.55초 | 중앙 띠에서 위아래로 개방 |
| 개방 시작 → UI 지연 | 0.45초 | 개방 시작으로부터의 지연 |
| UI 등장 | 0.30초 | 아래 24 기준 픽셀에서 원위치로 이동·페이드 |

기본 시간 합계는 중첩 구간을 포함해 **2.00초**입니다. 심리스에서는 기존 적의 조우 대기 대신 경고가 바로 시작됩니다. 참가자 생성·준비는 암전 0.35초 안에서 함께 처리하고, 더 오래 걸리면 준비 완료까지 검정을 유지합니다. 프레임 경계 및 기존 시작 대사는 별도라 항상 정확히 2초에 조작이 열리는 것은 아닙니다. PixelPerfectCamera는 유지하며 줌은 기존 CameraController의 연속 줌 복구 경계를 사용합니다. 전역 Time.timeScale은 변경하지 않습니다.

공용 Host 프리팹에도 위 값을 명시했습니다. 별도로 만든 씬/인스턴스에 설정 Override가 있다면 해당 Override는 유지되므로 그 인스턴스의 값을 확인합니다.

경고 색, 절단선 색, 줌 비율, UI 이동 거리도 같은 곳에서 조절합니다. **경고/절단 효과음은 미지정**이며 원하는 AudioClip을 넣으면 됩니다. 기존 OverworldEnemy의 조우 효과음은 유지되므로 경고음을 추가할 때 같은 음원이 중복되지 않게 선택합니다.

## 특정 적·대화 전투에서만 끄기

- `OverworldEnemy` 또는 `DialogueBattleNPC`의 **심리스 진입 절단 연출**을 끕니다. 보스 전용 연출과 대화 뒤 바로 이어지는 전투에 사용할 수 있습니다.
- 코드에서 시작한다면 기존 `BattleEncounterService.StartEncounter(...)`에 마지막 선택 인자 `playEntryPresentation: false`를 전달합니다.
- 대화 선택지는 `DialogueEncounterContext.PlayEntryPresentation`으로 동일한 정책을 전달합니다. DialogueData 자산을 런타임에 수정하지 않습니다.

## 구현 및 정리 경계

- `BattleEntrySettings`는 Host의 직렬화 설정입니다. 별도 카탈로그나 Action 문법을 추가하지 않았습니다.
- `SeamlessBattleEntryPresentation`은 Host마다 최초 진입 시 한 번 생성하고 작은 Canvas/도형을 재사용합니다. 유휴 Update/화면 캡처/전역 Tween Kill은 없습니다.
- `BattleManager`가 cover → 기존 참가자 준비 → 기존 OnBattleStarted → reveal → 기존 시나리오·전투 모듈 시작 순서를 소유합니다. 암전 중에는 기존 0.5초 플레이어 접근을 즉시 배치로 대체합니다.
- 후속 수정: 기존 조우 경로에서 암전 전에 `SetBattleMode(true)`를 호출하던 부분을 분리했습니다. `HoldForBattleEntry`와 Cutscene 상태로 필드 모습은 유지한 채 이동/입력만 잠그고, 완전 검정 패널을 한 프레임 표시한 뒤 `IsCovered`가 확인돼야 실제 Battle 모드로 바꿉니다. 잠금은 기존 SetBattleMode/정리 및 OnDisable에서 해제합니다.
- `DialogueBattleNPC`의 원본 숨김도 서비스 호출 전이 아닌 `IEncounterPreparationSource.OnEncounterPreparing` 경계로 옮겼습니다. 줌 중 NPC가 먼저 사라지는 현상을 피하고 기존 결과/중단 복원은 유지합니다. 준비 도중 암전 최소 시간을 모두 썼다면 Reveal은 추가 대기하지 않습니다.
- `BattleHudEntryMotion`은 하단 요소의 위치·alpha·입력 속성을 보관했다가 복구합니다. 중첩 부모/자식을 두 번 움직이지 않으며 턴 큐·대사 캔버스·카메라 투영에는 적용하지 않습니다.
- 서브메뉴 누락 수정: 공용 프리팹의 BattleMenu와 BattleSubMenu는 같은 부모를 가진 형제입니다. BattleMenuUI.SubMenuRoot로 기존 참조를 노출하고 BattleUIController.BeginEntryPresentation의 대상에 포함했습니다. 목록·설명·프레임은 이 루트 아래이므로 동일 진행률로 숨김/슬라이드/복원됩니다. 프리팹 재부모화나 새 Inspector 연결은 필요하지 않습니다. 형제 서브메뉴와 설명을 포함한 회귀 검사 코드를 추가했고, 기존 중복 계층 방어도 유지합니다.
- 진입 시작과 완료 시 기존 입력 소비 함수를 호출합니다. 명령 입력 해제 시점은 기존 전투 모듈이 결정합니다.
- 취소/Host 비활성/씬 종료에서는 소유 Tween, 덮개, HUD를 정리합니다. 카메라는 자신이 가진 명령 토큰이 아직 유효할 때만 복구합니다. 플레이어 위치·BGM·게임 상태는 기존 심리스 공통 정리 함수가 담당합니다.
- overlay 정렬은 32764로 전역 페이드(32767), 모달 대화(32766), 레터박스(32765)의 기존 우선권을 침범하지 않습니다. 새 시작 대사는 개방 뒤 실행합니다.

새 소스는 `Scripts/Battle/Runtime/Presentation/`의 설정/프레젠터와 `Scripts/UI/Runtime/BattleHudEntryMotion.cs`, Editor 테스트입니다. 연결 수정은 BattleManager/Host/EncounterService, CameraController, BattleUIController, OverworldEnemy, PlayerController, DialogueBattleNPC, DialogueEncounterContext/Manager입니다. 후속 요청으로 공용 SeamlessBattleHost.prefab에 연출 값만 추가했고 기존 카메라/UI 참조는 유지했습니다. 씬·Aseprite 및 다른 프리팹은 이번 작업에서 재저장하지 않았습니다.

## 검증과 남은 확인

- 새 파일을 포함한 Runtime/Editor C# 빌드: 오류 0. 기존 ConfigPanelUI 미할당 필드 경고 2개 유지.
- Editor 검사 코드는 2초 기본 시간·0.5초 줌·잘못된 시간값·HUD 위치/alpha/입력 복구·중복 계층·파괴된 대상·검정 프레임 이후 준비 허용·덮개 재사용/비활성화 정리·필드 모습 유지 잠금을 다룹니다. **컴파일만 확인했으며 NUnit/Unity Play 실행 결과가 아닙니다.**
- 수동 확인: 일반 조우 2회 반복, 진입 중 Host 비활성화, 개별 토글 끔, 전용 BattleScene 비적용, 4:3/16:9 화면 덮임과 HUD 등장. 저사양 기기 성능 및 화면 감각은 실제 플레이 확인이 필요합니다.
- 사용자 제작 중인 자산은 보존했습니다. 커밋·푸시는 하지 않았습니다.

관련: [당일 기록](../../2026-10-02-update.md) · [프로젝트 색인](../../index.md) · [설계](../../../docs/superpowers/specs/2026-10-02-seamless-battle-entry-design.md).
