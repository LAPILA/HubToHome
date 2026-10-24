# Unity 코드 후속 검수 — 저장·대화·필드 전환

## 범위와 원칙

사용자가 검수 대상으로 지정한 Unity 게임 코드에 한정했다. 외부 플러그인, 별도 프롤로그 제작실 웹사이트, 아트·씬·프리팹·DB는 수정하지 않았다. 기존 미커밋 작업도 유지했다.

전체 C# 컴파일을 기준선으로 삼고 저장·장비·성장, 대화·UI 입력, 필드 전환·동료 수명, 최근 카메라·피격·음향 코드의 중단 경계를 검토했다. 모든 코드의 모든 실행 조합을 검증했다는 뜻은 아니다. 확인된 결함만 기존 소유자 안에서 수정했으며 일괄 스크립트 삭제나 구조 재설계는 하지 않았다.

## 확인한 결함과 수정

| 경로 | 기존 문제 | 수정 경계 |
| --- | --- | --- |
| 임시 저장 복구 | 정상 Primary/Backup 없이 `.tmp`만 복구 가능한 상태에서 다음 저장 쓰기가 실패하면 유일한 복구본까지 사라짐 | 명시적 Save가 유효한 유일 `.tmp`를 먼저 Primary로 보존. 새 쓰기를 시작하기 전 실패에는 기존 임시 파일을 정리하지 않음 |
| 저장·불러오기 선택창 | X가 취소와 일반 대화의 두 번째 선택지 단축키를 겸해 Update 순서에 따라 슬롯 선택·저장/불러오기가 실행될 수 있음 | 독립 선택창만 직접 선택 단축키를 끔. 방향키/Z 선택과 X 취소를 분리하고 일반 이야기 선택지의 단축키는 유지 |
| 일반 대화 패널 손실 | 전투 도킹/삽화 외 일반 패널이 강제 비활성화·파괴되면 Manager에 취소를 통지하지 않아 상태·대기 작업이 남을 수 있음 | 정상 닫기를 제외한 패널 손실은 기존 `CancelPresentation(source)`로 통지. 다른 패널과 새 재생은 source 소유권으로 보호 |
| 필드 선제공격 중단 | 플레이어 비활성화로 코루틴이 중단되면 Cutscene/진행 플래그가 남거나, 늦은 종료가 새 상태를 덮을 수 있음 | 자기 실행 세대와 코루틴을 한 종료 경로로 정리. 다른 상태 소유자의 Dialogue/Paused/Battle을 덮지 않음 |
| Room 도착 문 억제 | 도착 적용 후 전역 SpawnPoint ID를 다시 찾으면 프레임 끝까지 남은 이전 방의 같은 ID를 선택할 수 있음 | 이미 적용한 플레이어 도착 좌표와 목적 방 내부의 활성 문·마커만 사용. 좌표 fallback에도 적용 |

저장 복구 순서 Primary → Backup → Temporary, 읽기 전용 Load, 미래 버전 Primary 보호, 장비 포함 HP/AP 상한 정책은 유지한다. 저장 스키마 변경은 없다. 대화의 일반 선택지와 짧은 전투 말풍선, 전조/방어/QTE 판정도 바꾸지 않았다.

## 테스트 정리

`RegionEntryCoordinatorTests.RoomTransition_AppliesDestinationBgm`이 이전 private `LoadRoom(request, player)`를 호출하고 있었다. 현재는 내부 context 1인자이므로 컴파일만 통과해도 실행 시 인자 수 예외가 발생한다. 공개 `TryRequestTransition`의 완료 콜백으로 전환·음악·잠금 해제를 확인하도록 변경했다.

회귀 소스는 기존 테스트 파일에 추가했다. 새 Inspector 참조 연결이나 자산 마이그레이션은 필요 없다.

## 변경 파일

`Assets/_Game/Scripts` 기준:

- `Core/Runtime/AtomicSaveStorage.cs`, `Core/Tests/Editor/AtomicSaveStorageTests.cs`
- `UI/Runtime/DialogueUI.cs`, `Dialogue/Runtime/DialogueManager.cs`, 관련 Dialogue Editor 회귀
- `Overworld/Runtime/PlayerController.cs`, `Overworld/Tests/Editor/PlayerPreemptiveAttackStateTests.cs`
- `Overworld/Runtime/Map/MapTransitionService.cs`, `Overworld/Tests/Editor/MapTransitionServiceTests.cs`, `RegionEntryCoordinatorTests.cs`

문서: 당일 기록·색인·브리핑, Core/Dialogue/Overworld 규칙 및 기존 저장 설계의 복구 보존 절차를 함께 갱신한다.

## 검증과 한계

- 수정 전 실제 저장 코드+메모리 파일시스템에서 `.tmp` 복구 → 쓰기 실패 → NotFound를 재현했다. 수정 후 저장/Codec 관리형 검사 48건을 통과했다. 사용자 저장 파일을 읽거나 쓰지 않았다.
- Room 억제는 실제 production 메서드를 추출해 이전 구현의 목적 문 억제 실패를 확인했다. 수정 후 목적 문/마커, 외부 방·비활성·먼 문 제외 6단언을 통과했다. Transform 검색은 관리형 대체 타입이며 실제 Unity 생명주기 실행이 아니다.
- 기존 픽셀 정책 52사례/73,689단언, 피격 수명 201단언, 명중음 236단언, HP/상태 표시 16,398단언을 재통과했다. 모두 관리형 검사다.
- 실제 DialogueManager/DialogueUI 두 파일을 직접 연결한 관리형 하네스에서 수정 전 2건 실패를 재현했고 수정 후 11시나리오/22단언을 통과했다. X/Z 양쪽 Update 순서, C 오선택 방지, 중복 취소·외부 상태·정상 완료·일반 이야기 X 선택 복구를 포함한다.
- 선제공격의 실제 메서드 본문을 추출한 동일 관리형 검사에서 HEAD 기준 9사례 중 8실패를 확인했고 수정 후 9사례/42단언을 통과했다. 비활성화, 외부 상태, 이전 코루틴 재개, 전투 인계, 즉시승리, 동기 target 콜백을 포함한다. 물리·Animator·Unity 코루틴 스케줄러는 실행하지 않았다.
- 전체 CLI 컴파일은 Runtime447소스/393참조, Editor370소스/422참조 모두 오류0이다. 경고225/0은 검수 전 기준선과 동일하다. `_Game` 메타1,555개의32hex/유일성/엄격UTF8 검사와 대상 diff 공백 검사도 통과했다.

독립 읽기 리뷰에서도 저장/대화/선제공격의 세대·source 소유권과 Room commit/rollback·완료 콜백 계약의 추가 치명 회귀는 확인되지 않았다. 이는 실제 엔진 회귀 테스트를 대체하지 않는다.

Unity Play, 엔진 NUnit, Refresh/Reimport, 열린 씬 저장, 실제 GPU/모바일 성능·발열·입력 체감은 실행하지 않았다. 이 검수로 무결점이나 모든 기기 동작을 보장하지 않는다. 브랜치는 `gameplayEdit`이며 커밋·push하지 않았다.

## 하네스 교훈

2026-10-23: 컴파일이 통과하는 reflection 테스트도 private 메서드의 인자 수 변경을 놓칠 수 있다. 공개 완료 API를 검증하고, 관리형 테스트와 엔진 실행 여부를 별도로 기록한다. Room별로 재사용하는 ID는 전역 검색의 안정적인 키가 아니다. commit된 목적 범위/실제 좌표로 확인해야 한다.

이번 검사 도구 실행에서도 폴더명과 `.csproj`명이 같다고 추측해 `PreemptiveLifetime.csproj`를 읽었으나 경로 없음으로 실패했다. `Get-ChildItem`으로 확인한 실제 `PreemptiveLifetimeCheck.csproj`를 사용하자9사례/42단언을 통과했다. Windows 파일·검사 스크립트 위치는 `rg --files`/목록 결과를 먼저 확인하고 그 결과의 경로를 사용한다. 기존 전역 `yjlim` 프로필 경로에는 쓰지 않고 현재 프로젝트 기록에 남겼다.
