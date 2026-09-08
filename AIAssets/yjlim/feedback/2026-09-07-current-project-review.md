# HUB TO HOME 현재 구현 점검

검토일: 2026-09-07 · 브랜치: `codex/gameplayEdit` · 기준 커밋: `bbe71ab1`

> 후속 수정: 사용자가 최종 답변의 3·4·5·6번(R03·R04·R05·R06)을 승인하여 해당 코드 수정과 회귀 테스트 소스 추가를 완료했다. C# 컴파일은 오류 0개이며 Unity/테스트 실행은 하지 않았다. 아래 본문은 수정 전 감사 기록이다. Windmill의 실제 곡 참조는 여전히 비어 있으며 임의 곡을 넣지 않았다. 상세 변경은 오늘 update의 후속 수정 절을 참조한다.

## 결론

**맵·전투·대화·저장·제작 도구의 기반은 상당 부분 있다. 지금 필요한 것은 새 범용 시스템이 아니라, 기존 전투 규칙과 맵 진입 경로를 마감하고 Chapter 1의 짧은 실제 플레이 구간을 연결하는 일이다.**

다만 기반 코드가 존재하는 것과 본편 콘텐츠가 완성된 것은 다르다. 현재 런타임 카탈로그에는 아군 1명, 적 4종, 스킬 18개, 아이템 9개, 장비 0개가 등록되어 있다. 테스트 자산도 포함된 수량이며, 완성된 본편 콘텐츠 수가 아니다.

이번에는 수정 요청이 아니라 검토 요청으로 처리했다. 게임 코드·씬·프리팹·데이터는 변경하지 않았고, Unity 실행·Play Mode·빌드·자동 테스트도 수행하지 않았다. 아래 오류는 소스와 직렬화된 연결을 대조한 결과이며 화면 재현 결과가 아니다.

## 1. 현재 만들어진 것

| 영역 | 확인한 구현 | 아직 구분해야 할 점 |
|---|---|---|
| 탐색·상호작용 | 이동, 방향, 상호작용, 필드 선공, 대화형 조우, 마커 | 상호작용의 과거 매 프레임 플레이어 검색 문제는 이미 개선됨 |
| 맵 | Region Scene + Room Prefab + RoomDefinition, 방 교체, 도착점, 카메라 경계 | Windmill의 초기 진입·저장 복원·실내 출입구 연결은 미완료 |
| Chapter 1 | Windmill 씬 1개, 외부/실내 Room 2개, ZEV 배치, 대화 2개, 심리스 전투 Host | 두 Room이 현재 양방향으로 연결된 것은 아님 |
| ZEV 샘플 | 대화 6노드 → 동시 접근 → 공격 모션 → 후속 대화 2노드 → 필수 심리스 전투 | 피격 넉백은 현재 체크아웃에 없음 |
| 턴 전투 | 공격/스킬/아이템/도주, QTE 방어, 보상, 심리스 종료·복귀 | 턴 순환·일부 상태이상·피해 적용 경로 수정 필요 |
| 3+3 | 전열 최대 3명, 후열 선생성 후 비활성화, 전열 전멸 후 후열 전체 투입 | 수동 교대 없음. 실제 서로 다른 아군 데이터는 아직 1개 |
| 성장 | 경험치, 레벨, 능력치 포인트 투자/환불, 스킬 트리·장착 | 최종 밸런스가 검증된 것은 아님 |
| 장비·아이템 | 장비 6칸, 소유/장착 제한과 스탯 적용, 소비 아이템, 상점 세션 | 장비 데이터 0개. 패시브/착용 반응 대사는 필드만 존재 |
| 저장·이어하기 | 스키마 v4, 구버전 변환, Primary/Backup/Temporary 복구, Continue, 게임오버 재시도 | 시스템은 있으나 각 실제 지역의 진입 연결도 맞아야 함 |
| 설정·UI | 공통 640×480 viewport, 음량/언어/텍스트/흔들림·플래시/화면/FPS/키 설정 | 씬 카메라 교체 감지 결함이 남아 있음 |
| 환경 기능 | 위험지형 피해, 퍼즐 런타임 연결, 상점, 열차 이동 예제 | 예제 구현과 본편 배치는 별도 |
| 시나리오 제작 | YAML, Action Sequence, 시퀀스 메이커, 취소·병렬 실행, 검증·안전 저장 | 원본 해시와 줄바꿈 정책 보강 필요 |
| 전투 변주 | `aim_shooter` 등록·전환, 입력 소유권, 기초 규칙 | 실제 조준·투사체·전용 UI까지 완성된 슈터는 아님 |

자산 집계는 `Assets/_Game/Content`와 `Assets/_Game/Resources`의 스크립트 GUID를 기준으로 했다. 대화 데이터 24개, 독립 Action Sequence 자산 4개, BattleScenarioData 자산 0개가 확인됐다. **전투 시나리오 실행 기반은 있지만 실제 저장된 전투 시나리오 콘텐츠는 없는 상태**다. 기존 SkillData/DialogueBattleNPC 기반 조우가 없다는 의미는 아니다.

등록 근거: [GameContentCatalog.asset](C:/Documents/GitHub/HubToHome/HubToHome/Assets/_Game/Resources/HubToHome/GameContentCatalog.asset:15)

## 2. 먼저 고칠 코드

### R01. 턴 표시 길이가 실제 행동 횟수를 바꾼다 — 높음

현재 생존자를 SPD순으로 정렬하고, 그 목록을 8칸까지 반복해 행동 큐를 만든다. 8칸을 모두 쓰면 처음부터 다시 만든다.

생존자가 A/B/C 3명이면 `ABCABCAB → ABCABCAB`가 반복된다. 매 8행동마다 A·B는 3회, C는 2회 행동한다. 참가자 수와 표시용 길이 때문에 행동 횟수가 달라지는 구조다.

- 근거: [BattleTurnQteModuleControllerService.cs](C:/Documents/GitHub/HubToHome/HubToHome/Assets/_Game/Scripts/Battle/Runtime/Services/BattleTurnQteModuleControllerService.cs:64), 같은 파일 85행.
- 수정 방향: 실제 턴 순환과 상단 미리보기 8칸을 분리한다. 웨이브 전환 시 새 큐를 만드는 기존 흐름은 보존한다.
- 확인 조건: 생존자 3/5/6명에서 반복 행동 횟수가 표시 개수에 의해 달라지지 않아야 한다.

### R02. 기절·속박·출혈의 행동 연결이 빠져 있다 — 높음

`CanTakeTurn()`, `CanDodgeOrJump()`, `NotifyActionExecuted()`는 선언되어 있지만 프로젝트 Scripts 검색에서 호출이 없다. 기절·속박 플래그와 출혈 이벤트 구독은 있어도 실제 턴/방어 입력/행동 완료가 이를 사용하지 않는다.

- 근거: [CharacterBase.cs](C:/Documents/GitHub/HubToHome/HubToHome/Assets/_Game/Scripts/Characters/Runtime/CharacterBase.cs:162), [StatusEffect.cs](C:/Documents/GitHub/HubToHome/HubToHome/Assets/_Game/Scripts/Characters/Runtime/StatusEffect.cs:93), 턴 서비스 91–115행.
- 수정 방향: 행동 가능 검사, 방어 입력 제한, 행동 완료 알림을 실제 전투 경계에 연결한다.
- 주의: 현재 `ProcessEffects()`는 행동 시작 전에 지속시간을 줄이고 만료 효과를 제거한다. 검사 함수만 추가하면 1턴 기절이 검사 전에 풀릴 수 있으므로 **효과 처리 순서도 함께** 정리해야 한다.

### R03. 적 기본공격·일부 광역공격이 일반 피해 계산을 우회한다 — 높음

적 기본공격과 스킬이 없는 광역 fallback은 `TakePureDamage(enemy.ATK)`를 호출한다. 반면 아군 기본공격과 일반 스킬 타격은 `TakeDamage`를 사용한다.

따라서 해당 적 공격에는 DEF, 받는 피해 감소, 물리 저항, 공격자의 피해 배율이 적용되지 않는다. 광역 fallback에는 방어 QTE도 없다. 모든 적 스킬이 같은 문제라는 뜻은 아니다.

- 근거: [턴 서비스의 적 기본공격](C:/Documents/GitHub/HubToHome/HubToHome/Assets/_Game/Scripts/Battle/Runtime/Services/BattleTurnQteModuleControllerService.cs:319), 같은 파일 383행·588행.
- 수정 방향: 일반 공격은 공통 피해 계산을 거치고, 방어 무시는 명시적인 공격 속성으로 구분한다. 이 공격들이 의도적으로 방어 무시인지 먼저 확인하되, 이름 없는 예외 경로로 남기지는 않는 것이 좋다.
- 구분: 현재 **속성 피해에는 일반 DEF를 적용하지 않는 것**은 코드 주석과 테스트에 명시된 별도 정책이다. 이것까지 버그로 묶어 변경하면 안 된다.

### R04. 상태이상 잔존이 전투 방식·캐릭터에 따라 달라진다 — 높음

심리스 종료는 Tween, Animator, 위치, BattleMode 등을 복구하지만 기존 캐릭터의 활성 상태이상 목록은 해제하지 않는다. 같은 플레이어 객체는 다음 전투에서도 효과를 유지할 수 있지만, 전투용으로 생성한 동료는 파괴되어 효과가 사라진다.

- 근거: [BattleManager.cs](C:/Documents/GitHub/HubToHome/HubToHome/Assets/_Game/Scripts/Battle/Runtime/BattleManager.cs:2195), 같은 파일 2280행, [PlayerCharacter.cs](C:/Documents/GitHub/HubToHome/HubToHome/Assets/_Game/Scripts/Characters/Runtime/PlayerCharacter.cs:139).
- 수정 방향: 전투 한정 효과는 공통 종료 지점에서 해제한다. 전투 밖 유지가 필요하다면 객체 재사용 여부에 의존하지 않도록 저장/복원까지 통일한다.
- 기획 결정: 전투 밖 상태이상 유지 여부. 결정과 관계없이 주인공/동료/전용씬 간 비대칭은 정리 대상이다.

### R05. 같은 해상도의 씬 이동에서 UI 카메라 재연결이 누락될 수 있다 — 높음

`ResolveSharedCamera()`가 새 카메라를 `_sharedCamera`에 먼저 대입한다. 이후 변경 검사에서 `_sharedCamera != camera`를 비교하므로 카메라 교체 조건이 항상 false가 된다. 해상도와 camera.rect도 같으면 기존 Canvas 전체 갱신이 예약되지 않는다.

DDOL 메뉴/설정 Canvas는 Awake 때 등록되며, 별도 해상도 갱신 서비스의 씬 로드 처리는 레이아웃/TMP 갱신이지 출력 카메라 재연결이 아니다.

- 근거: [UIViewportService.cs](C:/Documents/GitHub/HubToHome/HubToHome/Assets/_Game/Scripts/UI/Runtime/UIViewportService.cs:115), 같은 파일 133–145행.
- 영향 조건: 같은 화면 크기의 다른 씬으로 이동한 뒤 기존 메뉴·설정 Canvas가 이전 카메라를 참조할 수 있다. 실제 잘림/미표시 모양은 실행 검증하지 않았다.
- 수정 방향: 마지막으로 Canvas에 적용한 카메라를 별도로 추적하고 씬 전환 시 전체 등록 Canvas를 갱신한다. 개별 패널에 임시 크기 보정을 더하지 않는다.

## 3. Windmill에서 마감할 것

### R06. 첫 입장 BGM은 적용 경로와 실제 곡 연결이 모두 필요하다

`RoomContainer.Start → LoadRoom → OnRoomEntered`에는 BGM 적용이 없다. 기존 `RegionEntryCoordinator` 진입도 BGM을 적용하지 않는다. `MapTransitionService`의 방 이동 경로에만 적용 코드가 있다.

또한 **현재 외부·실내 RoomDefinition의 `_bgmOverride`는 모두 비어 있다.** 이전 대화에서 연결했다고 한 곡이 현재 브랜치에도 연결되어 있다고 가정하면 안 된다.

- 근거: [RoomContainer.cs](C:/Documents/GitHub/HubToHome/HubToHome/Assets/_Game/Scripts/Overworld/Runtime/Map/RoomContainer.cs:17), [MapTransitionService.cs](C:/Documents/GitHub/HubToHome/HubToHome/Assets/_Game/Scripts/Overworld/Runtime/Map/MapTransitionService.cs:434), [외부 RoomDefinition](C:/Documents/GitHub/HubToHome/HubToHome/Assets/_Game/Content/Maps/Regions/Chapter01/Data/Rooms/Room_Chapter01_WindmillExterior_Definition.asset:18).
- 수정 방향: 첫 입장·방 이동·저장 복원이 같은 Room 오디오 적용 처리를 사용하게 하고 곡을 재연결한다.
- 구조 판단: 기본 곡을 RoomDefinition에 두는 구조는 유지해도 된다. 영역별 음악이 실제로 필요할 때만 덮어쓰기 범위를 추가하면 된다. 현재 영역별 BGM Zone 완성은 확인되지 않았다.

### R07. 실내는 자산만 있고 외부에서 들어가는 출입구가 없다

외부에는 `default`/`from_interior` 도착점이 있으나 실내로 가는 AreaConnectionMarker/DoorTransition이 없다. 실내에는 외부로 돌아오는 연결이 있다. README의 “서로 연결되어 있음”은 현재 자산과 다르다.

- 근거: [Chapter01 README](C:/Documents/GitHub/HubToHome/HubToHome/Assets/_Game/Content/Maps/Regions/Chapter01/Notes/README_Chapter01_Map.md:15), [실내 반환 마커](C:/Documents/GitHub/HubToHome/HubToHome/Assets/_Game/Content/Maps/Regions/Chapter01/Prefabs/Rooms/Room_Chapter01_WindmillInterior.prefab:1389).
- 처리: 실내를 사용할 때 외부 진입 연결을 추가한다. 외부 한 맵만 먼저 제작한다면 실내를 억지로 완성하지 않고 문서에 “예시 자산·연결 전”이라고 표시하면 된다.

### R08. Windmill의 저장 Room 복원과 본편 빌드 연결이 남아 있다

Windmill은 `_loadInitialRoomOnStart=1`로 항상 외부를 만들며 `RegionEntryCoordinator`가 연결되어 있지 않다. 따라서 실내 저장 상태로 재진입하면 외부 Room에 실내 좌표를 적용할 수 있다. 기존 RegionEntryCoordinator에는 저장 Room ID를 해석하는 구현이 이미 있다.

현재 EditorBuildSettings는 TestMap이 첫 씬인 QA 구성이고, 활성 8개 씬에 Windmill이 없다. 현재 빌드 설정만으로는 Chapter 1 본편 시작 흐름이 아니다. QA 목적이라면 이 설정 자체는 오류가 아니다.

- 근거: [Windmill 씬](C:/Documents/GitHub/HubToHome/HubToHome/Assets/_Game/Content/Maps/Regions/Chapter01/Scenes/Region_Chapter01_Windmill.unity:415), [RegionEntryCoordinator.cs](C:/Documents/GitHub/HubToHome/HubToHome/Assets/_Game/Scripts/Overworld/Runtime/Map/RegionEntryCoordinator.cs:85), [EditorBuildSettings.asset](C:/Documents/GitHub/HubToHome/HubToHome/ProjectSettings/EditorBuildSettings.asset:8).
- 처리: 실제 본편 경로를 연결할 때 기존 진입 Coordinator를 사용하고 RoomContainer의 자동 시작과 중복되지 않게 한다. QA 구성은 보존하면서 본편 씬 목록을 별도 결정한다.

### R09. 요청했던 피격 넉백은 현재 파일에 없다

현재 Staged Encounter는 접근 → 공격 모션 → 대기 → 후속 대화다. 이 흐름에 넉백 필드·Tween·호출이 없다. “구현되어 있는데 카메라 때문에 보이지 않는다”는 진단은 현재 체크아웃에는 맞지 않는다.

- 근거: [DialogueBattleNPC.cs](C:/Documents/GitHub/HubToHome/HubToHome/Assets/_Game/Scripts/Overworld/Runtime/DialogueBattleNPC.cs:345), 같은 파일 446행.
- 처리: 공격 직후의 짧은 DOTween 연출만 기존 취소/복원 흐름에 포함하면 된다. 별도 연출 시스템을 새로 만들 사안은 아니다.
- 이미 해결된 것: 접근 Tween 완료 후 AutoKill을 중단으로 오인하던 문제는 현재 405–425행에서 완료 플래그로 처리한다. 과거 경고를 그대로 재지적하지 않는다.

## 4. 제작 도구·문서 정리

### R10. 시퀀스 저장의 줄바꿈 해시 충돌

독립 시퀀스 4개의 현재 원문 해시가 저장된 SourceHash와 불일치한다. 추가 비교 결과 TravelTrain 출발과 ShowcaseStation 도입/종료 3개는 **LF 줄바꿈으로 계산하면 정확히 일치**한다. 현재 체크아웃은 CRLF이고 `core.autocrlf=true`다.

ScenarioSourceHash는 줄바꿈을 정규화하지 않는다. SequenceSaveCoordinator는 저장 해시와 디스크 원문이 다르면 외부 변경 충돌로 저장을 중단한다. 따라서 이 3개는 내용이 바뀌지 않아도 줄바꿈 차이로 충돌할 수 있다. 지하철 도입 1개는 LF/CRLF 모두 불일치하므로 별도 메타데이터 대조가 필요하다. 불일치만으로 액션 내용이 잘못됐다고 단정하지 않는다.

- 근거: [ScenarioSourceHash.cs](C:/Documents/GitHub/HubToHome/HubToHome/Assets/_Game/Scripts/Scenario/Data/ScenarioSourceHash.cs:6), [SequenceSaveCoordinator.cs](C:/Documents/GitHub/HubToHome/HubToHome/Assets/_Game/Scripts/Scenario/Editor/SequenceMaker/SequenceSaveCoordinator.cs:179).
- 처리: 소스 줄바꿈과 해시 계산 기준을 통일하고 기존 메타데이터를 안전하게 맞춘다. 외부 변경 보호를 끄거나 무조건 덮어쓰기하는 해결은 피한다.

### 대사를 엑셀/JSON으로 관리할 수 있는 범위를 정확히 구분해야 한다

- 대사는 Prefab마다 모든 문장을 쓰는 구조가 아니라 `DialogueData`를 참조한다.
- 각 노드는 화자, 표정, 번역 키, 기본 문장, 이벤트, 선택지 정보를 가진다.
- `LocalizationTable.csv`는 문장 번역 테이블이다. 이것이 대화 전체의 화자/표정/분기/연출을 가져오는 대본 importer는 아니다.
- 시나리오 YAML은 대화 ID를 통해 기존 DialogueData를 연결한다. 현재 Scripts에서 대화 전체 구조의 CSV/JSON 일괄 입출력기는 확인되지 않았다.
- CSV 로더는 물리적인 줄 단위로 먼저 분리한다. 엑셀 셀 내부의 실제 여러 줄 CSV 대신 현재 규약의 문자 `\n`을 써야 한다. 긴 대본을 본격 투입하기 전에 입력 규약 또는 importer를 확정하는 것이 좋다.

근거: [DialogueData.cs](C:/Documents/GitHub/HubToHome/HubToHome/Assets/_Game/Scripts/Dialogue/Runtime/DialogueData.cs:19), [LocalizationManager.cs](C:/Documents/GitHub/HubToHome/HubToHome/Assets/_Game/Scripts/Core/Runtime/LocalizationManager.cs:43).

### 오래된 문서는 실제 코드에 맞춰 정정해야 한다

| 문서의 설명 | 현재 확인한 사실 |
|---|---|
| `AIAssets/yjlim/TODO.md`: Continue 구현 필요 | TitleMenuManager → GameLoadCoordinator 연결이 있음 |
| 같은 TODO: CameraController를 TMP 폴더에서 이동할 계획 필요 | 현재 first-party 코드 경로에 있음 |
| `RuleFileforAI/overworld.clinerules`: InteractionSystem 매 프레임 Player 검색 | 캐싱·주기 탐색·확인 입력 재탐색 적용됨 |
| 같은 규칙: DestroyOnVictory가 시작 직후 적을 파괴 | 현재 AreaTrigger는 승리 결과에서 파괴 여부 처리 |
| 맵 공통 가이드의 구 `Regions/MapFieldStarter` 등 | 예제는 현재 `Development/Templates` 또는 `Development/Regions`에 있음 |
| Chapter01 README: 외부/실내 양방향 연결 완료 | 외부→실내 진입 연결 없음 |
| 마스터 제작 계획: 주인공 종족 후보를 다시 결정 | 사용자 대화에서 족제비로 결정된 내용과 맞춰야 함 |

계획 문서의 체크 항목만 보고 새 작업을 시작하면 이미 끝난 기능을 다시 만들 위험이 있다. 이번에는 보고서만 작성했으며 기존 문서 전체나 외부 작업 보드는 수정하지 않았다.

## 5. 버그 수정과 별도로 남겨둘 기획 결정

다음은 현재 구현을 기록한 것이며 새로운 확정안이 아니다.

| 항목 | 현재 구현 | 이후 결정할 것 |
|---|---|---|
| 기본 스탯 | HP/AP/ATK/DEF/SPD | 최종 캐릭터별 성장·전투 목표 수치 |
| AP | 저장 AP로 시작, 자기 차례 +5, Perfect Parry +20 | 전투 시작 초기화 여부와 공급량 |
| 성장 | 기본 최대 99레벨, 레벨당 능력치 3·스킬 1포인트 | Chapter 1 목표 레벨과 실제 전투 수 |
| 장비 | 6칸, 일반 스탯 적용 | 슬롯을 유지할지보다 우선 실제 사용할 장비 소수 제작 |
| 속성 피해 | 물리는 DEF 적용, 속성은 속성 저항 사용 | 최종 방어 규칙을 바꿀지 여부 |
| 상태 저항 | 0 이하는 차단, 양수는 그대로 적용 | 0.5를 확률로 사용할지 여부 |
| 속성 배율 0 | 최소 피해 1 규칙으로 1 피해 | 완전 면역 0 피해가 필요한지 |
| 보상 EXP | 전투 6명만이 아닌 Global Party 전체 지급 | 파티 성장 격차를 허용할지 |
| 3+3 전환 | 전열 전멸 후 후열 전체 투입, 적/전열 동시 전멸은 승리 우선 | 현재 사용자 요청과 맞으므로 수동 교대 추가 불필요 |

`PassiveEffectID`와 `EquipReactionDialogueID`는 현재 소비 코드가 없으므로 제작자에게 미지원임을 표시해야 한다. 이를 이유로 범용 패시브 프레임워크부터 크게 만들 필요는 없다.

이전 제안 문서에 적힌 수치가 현재 코드와 다르다는 이유만으로 모두 버그가 되는 것은 아니다. 특히 최대 레벨, 장비 슬롯, AP 초기화, 상태 최대 중첩 같은 변경은 별도의 기획 결정이다. 노션 페이지는 현재 연결에서 404로 조회되지 않아 최신 문서 내용·수정 상태를 확인하지 못했다.

## 6. 성능과 저장의 후순위 보강

- **FPS 적용 정책:** 휴대기기 목표 FPS는 30/60으로 제한하지만 VSync를 켜면 `targetFrameRate=-1`을 적용한다. 플랫폼별 30/60 선택의 의미가 유지되도록 적용 정책을 정리할 후보이다. 실제 발열·FPS 상승을 측정한 것은 아니다.
- **커서 검색:** BattleUIController의 활성 커서 갱신 중 부모 Canvas 검색은 바인딩 시 캐시 가능하다.
- **카메라 없는 씬:** SafeAreaFitter가 PixelPerfectCamera를 못 찾는 동안 반복 전체 검색할 가능성이 있다. 그런 씬을 유지한다면 씬 변경 기준 재탐색으로 제한한다.
- **UI 갱신 중복:** 해상도 변경 시 두 서비스가 전체 갱신을 수행한다. 상시 부하가 아니므로 먼저 R05를 수정한 뒤 필요할 때 정리한다.
- **저장 본문 검사:** `SaveDataCodec`은 버전 없는 legacy에만 본문 식별 검사를 한다. `{"schemaVersion":4}` 같은 본문 없는 유효 JSON이 Primary에 있으면 기본값으로 성공 처리되어 정상 Backup을 보지 않을 수 있다. 현재 실제 손상 사례가 아니라 방어적 검증 보강 항목이다.

성능 전면 개편이 우선이라고 판단할 측정 근거는 없다. 작은 캐시 개선과 플랫폼 설정 정리부터 하고, 대규모 변경은 실제 프로파일 결과에 따라 결정하는 것이 적절하다.

## 7. 권장 작업 순서

1. **전투 규칙 마감:** R01 턴 순환 → R02 상태이상 행동/만료 순서 → R03 일반 피해 경로 → R04 전투 종료 효과 정리.
2. **공통 복귀 경로 마감:** R05 UI 카메라 재연결, R06 초기 Room BGM. 개별 화면의 크기 보정은 그 뒤 판단.
3. **Windmill 짧은 구간 연결:** 외부 탐색 → ZEV 대화/공격/넉백 → 필수 전투 → 보상·탐색 복귀. 실내를 사용할 때만 출입구/저장 복원을 추가하고 본편 빌드 경로를 연결.
4. **제작 문서 정정:** 완료 항목과 오래된 경로 정리, 시퀀스 해시 문제 마감, 대본 입력 범위 명시.
5. **콘텐츠 투입:** 다음 아군 1명과 실제 적·스킬·아이템 소수로 기존 시스템을 사용. 처음부터 6명 전체와 모든 성장 시스템을 동시에 완성하려 하지 않기.

우선 보류해도 되는 것: 새 범용 연출 시스템, 수동 교대, 별도 슈터 전체 구현, 큰 BattleManager 재작성, 성장/장비 슬롯 전면 교체. 현재 확인된 문제를 고치기 위해 필요한 작업이 아니다.

## 검토 범위와 한계

- 추적 C# 파일 598개(Tests 경로 183개)의 파일 구성을 파악하고, 주요 런타임·서비스·데이터·관련 테스트 계약·씬/프리팹 연결을 집중 검토했다. 모든 파일의 모든 분기를 검증했다는 뜻은 아니다.
- Unity 6000.3.8f1, 현 브랜치·현 디스크 자산 기준. 다른 브랜치의 수정이나 작업자가 저장하지 않은 Editor 상태는 포함하지 않는다.
- 노션 최신 본문, Jira 최신 상태, Drive 문서 최신본은 이번 결과에 포함하지 않는다.
- 테스트 파일은 계약을 읽는 근거로만 사용했다. 테스트 통과/실제 플레이 정상/휴대기기 성능을 보증하지 않는다.
- 게임 파일 변경·커밋·푸시·외부 문서 변경 없음.

## 후속 현황 요약 — 진행률 요청에 대한 판단

기준: 2026-09-07 로컬 작업 트리, `codex/gameplayEdit` / HEAD `bbe71ab1` + 미커밋 변경. 외부 Drive 아트 원본·최신 대본·Jira 완료 상태와 저장하지 않은 Unity 상태는 포함하지 않았다. 이 절은 앞선 수정 전 감사와 달리 후속 수정/메이커 구현을 반영한 재조회다.

### 숫자의 의미와 한계

- 아래 %는 완료 작업 수를 나눈 실적률이 아니라, 확인된 구현·통합 연결·남은 콘텐츠와 검증을 보고 판단한 거친 개발 단계 범위다. 출시 일정이나 남은 공수를 계산하는 데 사용하지 않는다.
- 분야별 100%는 해당 기능이 필요한 실제 콘텐츠와 함께 연결되어 정상·실패 경로 및 지원 환경 검증을 마친 상태다. 코드·테스트 파일 존재만으로 100%를 주지 않는다.
- 전체 장편의 확정 제작 목록·가중치가 없어 전체 출시 진행률은 산정하지 않는다. 시스템 기반의 단계는 약 60~70%, 실제 본편 반영은 장면 단위 프로토타입으로 요약한다. 서로 다른 영역을 산술 평균하지 않는다.
- 로컬 마스터 플랜에는 Chapter 1 45~60분, 지역 역할 4개, 컷씬 4개 등의 잠정 목표가 있다. 그러나 종족 후보 등 오래된 결정도 섞였고 승인된 최신 완료 목록이 없어 공식 분모로 채택하지 않았다.
- 아트 제작 전체와 스토리 집필 전체의 %는 외부 원본 및 목표 목록을 확인하지 못해 보류한다. 아래 본편 콘텐츠 참고 범위는 프로젝트에 들어와 연결된 단계에 한정한다.

### 기능 기반

| 분야 | 추정 범위 | 현재 확인 / 남은 핵심 |
|---|---:|---|
| 맵·탐색·상호작용 | 60~70% | Region/Room/Definition, 마커, 방 전환, 상호작용. 실제 제작 방의 출입·저장 재진입 연결 필요 |
| 기본 턴 전투 | 45~60% | 공격·스킬·아이템·도주·결과 처리. 실행 큐 8칸 재생성의 행동 편향과 통합 밸런스 미완 |
| QTE 방어 | 55~70% | 패링·회피·점프와 판정 정책. 상태 제한 연결, 공격 패턴별 난이도·입력 실기 확인 필요 |
| 3+3 파티 | 55~70% | 후열 선생성/비활성, 전열 전멸 시 일괄 투입. 실제 6인 데이터 및 사망·보상·저장 조합 검증 필요. 수동 교대는 요구 범위가 아님 |
| 성장·장비·아이템 규칙 | 45~60% | 경험치·레벨·스탯 투자/환불·스킬 해금·장비 적용. 실제 장비 및 캐릭터별 최종 데이터·경제 밸런스 필요 |
| 상태이상 | 30~45% | 효과 적용/해제와 종료 정리. 기절·속박·출혈의 실제 행동 연결과 만료 순서 미완 |
| 대화·시퀀스 | 60~75% | 화자·표정·선택지·플래그, 액션 실행·취소·병렬, 일반 NPC 단일 조건 선택. 복합 스토리 상태·출현·위치와 번역 마감 필요 |
| 콘텐츠 제작 도구 | 70~80% | 챕터/방/NPC/대사 생성·목록 관리·삭제 보호·CSV/조건 편집. 실제 비개발자 제작/저장/Undo/재생 연속 검증 필요 |
| 저장·설정·공통 UI | 60~75% | Continue와 손상 후보 복구, 설정·viewport, 씬 카메라 재연결. 본편 저장 위치·플랫폼별 UI/성능·번역 마감 필요 |

### 콘텐츠와 아트

- 스토리 기획·집필 전체: 산정 보류. 세계관·톤·주인공·파티 방향은 대화에서 확인했으나 확정 전체 대본·비트 시트는 확인하지 못했다.
- Chapter 1 게임 반영: 대략 10~20% 단계라는 낮은 신뢰의 참고 판단. Windmill 씬 1/Room 2, 위젤–제브 대화 2개(6줄+2줄)와 접근·공격→필수 심리스 전투 연결. 시작부터 종료까지의 본편 빌드는 미확인이다.
- 맵·배경: 첫 장소 제작 중. 풍차 grass/wall 자산은 있으나 실내에 `Mill Machinery Placeholder` 등 임시 구성이 남는다. 개발/쇼케이스 씬을 본편 완성 맵으로 세지 않는다. 최종 아트 총량 대비 %는 산정 보류.
- 캐릭터 아트·애니메이션: Wizzel와 ZEV의 원본·Animator, 주인공 대화 얼굴 4종이 존재한다. 최신 6인 기획의 나머지 고유 아군 아트는 로컬에서 확인하지 못했다. 현재 개별 모션 재생/품질은 미검증이다. 실제 아트 제작률은 산정 보류.
- UI 아트: 메뉴·커서·QTE·전투 말풍선·각 패널 자산이 존재한다. 최종 화면 목록/다국어 스킨 적용률은 미확인이다.
- 음악·효과음: 곡·효과음 자산과 재생 기반은 존재하지만 Windmill 두 Definition의 BGM 참조는 비어 있다. 원본 폴더 이름만으로 자작/저작권 확인을 단정하지 않는다. 전체 OST/SFX 목표·믹싱·라이선스 완료율은 미확인이다.
- 번역: 언어/CSV 조회와 본문 번역 키 보조가 있다. 선택지·이름 및 번역표 여러 줄 처리, 본편 대본의 전 언어 교정 완료율은 미확인이다.
- QA·플랫폼: 테스트 소스·기존 정적 컴파일 기록은 존재하나 현황 조회에서는 실행하지 않았다. 전체 기기별 통과율·발열·FPS·메모리·출시 QA %는 산정하지 않는다.

### 재확인한 우선 사항

1. 턴 큐 재생성의 순환 편향과 기절/속박/출혈의 실제 행동 경계 연결이 남아 있다. `CanTakeTurn`/`CanDodgeOrJump`의 런타임 호출은 없고 `NotifyActionExecuted` 호출은 테스트에만 있다.
2. Windmill 외부→실내 진입, 저장 Room 복원용 진입 구성, 본편 빌드 연결이 남아 있다. `EditorBuildSettings.asset`에는 Windmill이 없고, 삭제 상태인 `Development/Regions/TravelTrain/Scenes/Region_TravelTrain.unity`가 여전히 등록되어 있다. 사용자의 삭제를 되돌리지 않았다.
3. 초기 Room 오디오 적용 코드는 보완됐지만 외부/실내 `_bgmOverride`는 모두 null이다. 재생 경로 수정과 곡 데이터 연결은 별개다.
4. 카탈로그 등록은 아군 1/적 4/스킬 18/아이템 9/장비 0이다. 테스트 자산을 포함한 등록 수이며 본편 완료 수나 6인 완성률이 아니다.
5. 다음 제작 목표는 새 범용 시스템 확장보다 Windmill의 탐색→제브 대화→전투→복귀→저장 재진입 한 구간 마감이다. 여섯 동료 전체를 Chapter 1 완료의 선행 조건으로 임의 추가하지 않는다.

주요 근거: `Assets/_Game/Resources/HubToHome/GameContentCatalog.asset`, `Assets/_Game/Scripts/Battle/Runtime/Services/BattleTurnQteModuleControllerService.cs`, `Assets/_Game/Scripts/Characters/Runtime/CharacterBase.cs`, `Assets/_Game/Scripts/Core/Runtime/AtomicSaveStorage.cs`, `Assets/_Game/Scripts/UI/Runtime/TitleMenuManager.cs`, `Assets/_Game/Content/Maps/Regions/Chapter01`, `Assets/_Game/Content/Art/Characters`, `Assets/_Game/Content/Art/Environment/WindMill`, `ProjectSettings/EditorBuildSettings.asset`, `docs/content-maker-guide.md`, `docs/production/hub-to-home-master-production-plan.md`.

확인 방식: 읽기 전용 코드/직렬화 참조·경로 존재·카탈로그 조회와 3개 독립 분야 검토. 게임 코드·자산 수정, Unity/빌드/테스트 실행, Jira/Notion/Drive 갱신, 커밋/푸시 없음. 이 현황 절과 일일 기록만 추가했다.

## 후속 기획 상담 — 방어·상태효과·스킬·장비·UI (미확정)

- 사용자 고민: 누르고 있으면 방어/타이밍 입력은 패링으로 통합할지, 현 패링·회피·점프를 유지할지; 버프·디버프 수와 림버스 참고 표시; 다인 파티의 스킬 창; 쯔꾸르형 장비; 언더테일과 차별화할 UI.
- 이번 범위는 비교·기획 조언이며 구현 승인이 아니다. 질문은 일반 전투에서 패링 타이밍 손맛과 대응/조합 전략 중 무엇이 중심인지다. 답을 확정된 것으로 기록하지 않는다.
- 재확인한 계약: 실제 Player_SkillTree의 장착 한도 6; 공통 AP(자기 턴 +5, Perfect Parry +20 기본)와 별도 압력/과열 루프는 다른 것; QTE는 Z/X/C 새 입력을 한 번 판정하며 홀드 방어는 연결되지 않음; 적 스킬 Requirement가 점프 전용 등 방어 방법을 지정함.
- 상태효과는 Stacks/DurationTurns이며 같은 효과 재적용은 기본 중첩+1/지속시간 큰 값 선택이다. 림버스의 개별 위력·횟수 소비를 그대로 구현한 구조가 아니다. 등록 효과 9종의 핵심 행동 연결과 전용 상태 UI 표시 계약부터 마감할 후보이다.
- 장비 6슬롯은 무기/장신구1/장신구2/머리/몸/신발이다. PassiveEffectID와 EquipReactionDialogueID는 소비자가 확인되지 않아 현 기능으로 안내하지 않는다.
- 잠정 제안: 타이밍 손맛을 우선한다면 홀드 방어+새 입력 패링을 후보로, 공격 종류별 대응 선택을 우선한다면 현 3입력을 후보로 비교한다. 홀드 패링의 자동 반복/연타 악용, 기존 전용 방어요구 데이터, 가드의 잔여 피해와 패링 보상은 결정이 필요하다. 어느 기능도 삭제하지 않았다.
- 잠정 제안: Chapter 1에서 실제 사용하는 공용 상태 6~8개로 시작하고 고유 자원은 분리한다. 종류 추가가 아니라 기존 목록에서 선별한다. 상태 숫자는 의미를 명확히 하고, 지휘 단계에는 상세 조회, 방어 단계에는 전조/피격 대상/판정에 시선을 모은다.
- 잠정 제안: 배운 스킬과 장착 스킬을 분리하는 기존 구조를 활용하고, 당장 Chapter 1의 3~4개 역할부터 만든다. 장착 상한 6이나 장비 6슬롯을 임의 변경하지 않는다. UI 스킨보다 전열3인 요약/현재 행동자 상세/후열 요약의 정보 구조를 먼저 검토한다.
- 기획 템플릿은 스킬의 역할·비용·대상·효과·상태 적용·다른 스킬과의 연결·사용할 이유, 장비의 슬롯·사용자·스탯·효과·입수시점·교체 이유를 분리하는 방향이다. 신규 자산/JSON 계약을 생성한 것은 아니다.
- 참고: Sandfall 개발자가 설명한 33원정대의 회피(넓은 판정)와 패링(정밀 판정/반격) 구분: https://blog.playstation.com/2024/08/28/new-clair-obscur-expedition-33-gameplay-fighting-and-exploring-the-flying-waters-region/ . 이 게임에 같은 구조를 확정한다는 의미가 아니다.
- 브레인스토밍 스킬에 따라 현행 계약/선택지/미확정 결정을 분리했다. 코드·자산·씬·입력/스탯/밸런스는 수정하지 않았으며 Unity 실행·테스트·커밋·푸시도 하지 않았다.

## 후속 상담 — 상인 중심 상점 제작 (미확정)

- 사용자 질문은 언더테일을 참고한 상점 제작 방법이며 구현 승인이 아니다. 현재 ShopDefinition/ShopUI/ShopSession/거래 서비스와 콘텐츠 메이커의 Vendor 연결을 읽었다.
- 현재 ShopDefinition은 Shop ID/이름/상품 Entry/단가/1회 수량/구매 횟수 제한/카운터 Flag를 갖는다. ShopUI는 구매·판매 목록/설명/소지금/결과 표시와 GameInput 내비게이션을 제공한다. 현지화되지 않은 한국어 UI/ItemName·Description 직접 표시 경로가 남아 있다. 상인 전용 배경·초상·대화 주제·거래 반응 데이터는 현재 확인한 ShopDefinition/ShopUI에 없다.
- 추천은 기존 거래 로직을 재사용하고 공용 상점 화면에 상인 그림/배경/대사/구매·판매·이야기·나가기 메뉴를 붙이는 것이다. 상인마다 UI 전체나 거래 코드를 복제하지 않는다. 일반 거래를 매번 Action Sequence로 만들지 않으며 특별한 스토리 사건만 기존 사건 시스템으로 연결한다.
- 대사는 별도 DialogueData/기존 상태 조건을 활용하는 추가 연결을 제안한다. 첫 방문/재방문/거래 성공/소지금 부족/품절/특정 사건 후 반응 및 선택 가능한 대화 주제를 두되, 판매 거절도 상인별 정책으로 표현할 후보이다. 현재 완성된 기능으로 안내하지 않는다.
- 제작 시작점: 프로젝트 창 Create → Hub To Home/아이템/상점 데이터로 ShopDefinition 생성, 상품/ID/구매 카운터 작성, 콘텐츠 메이커에서 방 열기 → 마커/NPC의 상점 연결 → 해당 데이터 지정. 런타임 AreaMarkerRuntimeService는 등록 런처가 없으면 ShopUI.EnsureGlobal을 호출한다.
- 첫 완성 범위 제안은 Windmill 상점 하나, 상품 3개, 대화 주제 3개, 첫 방문/재방문 반응이다. 마우스 없는 PC/패드 흐름과 번역키를 같이 설계하되, 모바일 가상 입력은 미완성임을 유지한다. 코드/자산/씬/빌드/Unity 실행/커밋·푸시 없이 이 상담 기록과 당일 메모만 추가했다.
- 독립 읽기 확인: 구매 제한은 구매 횟수 flag 기반이며 재입고 기능은 아니다. 돈/인벤토리/flag는 다음 실제 세이브에 포함되고 거래마다 자동 저장하지 않는다. 매입가는 현재 ItemData.Price의 절반(양수 최소 1)이며 상점별 매입 규칙은 없다. UI 닫기/파괴 복원은 있으나 OnDisable·씬 전환 종료 경로는 추가 연결 시 확인할 경계다. 실행으로 재현한 버그라고 표현하지 않는다.
