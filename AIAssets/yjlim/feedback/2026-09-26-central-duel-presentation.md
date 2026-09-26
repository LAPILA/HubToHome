# 중앙 교전·후방 회피·전투 UI 피드백

## 확정 범위

사용자 요청: X 회피는 왼쪽으로 크게 후퇴/복귀, 교전 당사자 둘은 전투 중앙에 진입하며 카메라가 자연스럽게 줌인. 기존 전투 규칙과 픽셀퍼펙트는 유지한다. 재승인/서브 에이전트/Unity 실행 검증 없이 구현하고 정적 검사와 CLI 컴파일로 확인한다.

## 구현 판단

전체 전열을 재배치하거나 카메라가 회피 캐릭터를 매 프레임 추적하는 방식 대신, PositionManager의 CenterPos 양쪽에 해당 아군/적 한 쌍을 배치한다. 남은 파티는 원위치. 광역 방어는 기존 대표 방어자만 전진하고 실제 피해 대상 목록은 변경하지 않는다. 지원 스킬/아이템은 적을 끌어오지 않는다.

- PositionManager: 중앙 교전 좌표·간격·진입 시간.
- BattleDefenderPresentationScope: 두 캐릭터 이동과 방어 기준점의 일시 소유, 정상 동시 복귀/중단 즉시 복구. 기존 클래스 이름과 호출 계약은 보존.
- SkillContext: 일반 타임라인/투사체/연쇄 타격에서 같은 배치 사용. OriginalPos는 교전 중의 기준점이며 실제 홈 복귀는 전체 행동 종료 때 처리.
- BattleCameraActionScope/CameraController: CenterPos 고정 추적 + 기본 Lens의 82%로 0.34초 줌. Timeline lease/CommandToken을 지키고 새 카메라 연출을 오래된 cleanup이 덮어쓰지 않는다. 일반 타격 자동 흔들림/히트스톱은 다시 켜지 않는다.
- PlayerController: 회피는 월드 왼쪽 1.5유닛(32 PPU에서 48픽셀), 수평 이동만 사용. 판정 창은 변경하지 않는다.
- UI: 목록 커서의 2px 진입, 선택 테두리 짧은 밝기 반응, HP/AP 변화·대상 테두리의 한 번짜리 피드백. 지속 흔들림/메뉴 스케일 팝은 금지. 각 view가 자신의 트윈만 취소하고 원래 상태로 복구한다.

## 작업 순서

- [x] 중앙 배치와 큰 후방 회피 연결.
- [x] 기본 공격/스킬/원거리/대표 광역/반격 및 취소 복귀 코드 경로 점검.
- [x] 중앙 고정 줌과 카메라 토큰 복구 연결.
- [x] UI 피드백 및 파괴/재사용 수명 정리.
- [x] 회귀 검사 코드 추가·정적 검사·CLI 컴파일, 사용법/계약 갱신.

실제 화면/전투 조작은 사용자가 확인한다. 수동 확인 전 결과를 검증 완료로 표현하지 않는다.

## 조절과 적용 범위

기존 `PositionManager.CenterPos`와 CameraController를 그대로 사용하며 프리팹/씬 YAML은 수정하지 않았다. 새 직렬화 필드는 코드 초기값으로 시작한다. 별도 오브젝트 생성이나 Inspector 참조 연결은 없다. 실험실뿐 아니라 같은 Turn QTE 전투 경로에 공통 적용된다.

| Inspector | 필드 | 기본값 |
| --- | --- | --- |
| PositionManager → 중앙 교전 | 중앙 기준 좌우 간격 / 중앙 이동 시간 | 1.25 / 0.28초 |
| CameraController → 중앙 교전 줌 | 기본 크기 대비 줌 비율 / 줌 전환 시간 | 0.82 / 0.34초 |
| PlayerController | 전투 회피 후퇴 거리 | 1.5 |

회피는 후퇴 0.12초 → 유지 0.12초 → 복귀 0.18초. 32 PPU 기준 48픽셀은 월드 스프라이트 픽셀 기준이며 화면상 크기는 현재 줌에 따라 달라진다. Lens 비율 0.82는 약 1.22배 확대다. 카메라 기본 크기, 픽셀퍼펙트, 조명/볼륨/아트는 수정하지 않았다.

UI는 OptionRowUI의 선택 커서/테두리와 PartySlotUI의 HP/AP/초상/대상 테두리만 수정했다. 대상과 현재 행동자 강조는 분리되며 메뉴 확정/취소 키·대상 선택·피해 계산·방어 판정은 유지한다. HP/AP 중복 알림은 같은 값이면 애니메이션을 재시작하지 않는다. 새 트윈은 자신의 핸들만 해제하고 파괴된 UI 접근을 막는다.

## 변경 파일

- `PositionManager.cs`: 중앙 좌우 배치 좌표 및 Inspector 설정.
- `BattleDefenderPresentationScope.cs`: 아군/적 쌍 이동·동시 복귀·취소 복구.
- `SkillActionBlocks.cs`: 양측 타임라인 배치 및 기본 이동 목적지 연결.
- `BattleTurnQteModuleControllerService.cs`: 기본 공격/적 대표 방어/스킬 연결, 카메라 무조건 reset 제거, 대기 후 객체 생존 재확인.
- `BattleCameraActionScope.cs`, `CameraController.cs`: 고정 중앙 줌과 최신 토큰만 복구.
- `PlayerController.cs`: 월드 왼쪽 큰 후퇴/복귀.
- `OptionRowUI.cs`, `PartySlotUI.cs`: 고정 레이아웃을 유지하는 단발 UI 반응.
- `QTEManagerDefensePipelineTests.cs`, `CameraFramingTests.cs`, `BattleHudPresentationTests.cs`: 중앙 좌표/양측 복구·적 파괴·Timeline 우선권·줌 복귀·UI 재사용 검사 코드 추가.
- `CONTEXT.md`, `RuleFileforAI/battle.clinerules`, `docs/bunny-slime-battle-lab.md`, AIAssets 인덱스/일지/본 문서: 이전 정적 카메라 계약을 새 중앙 줌 예외와 일치시킴.

## 검증과 남은 확인

- Runtime을 포함하는 `Assembly-CSharp-Editor.csproj` CLI 직렬 빌드: 오류 0, 기존 ConfigPanelUI 미할당 필드 경고 2. 새 테스트 코드도 해당 프로젝트 Compile 목록에 포함된 것을 확인했다.
- `git diff --check` 통과. C# 및 문서 UTF-8 검사. 방어 입력 여유/피해·AP 계산 파일과 씬/프리팹/조명 자산은 변경하지 않았다.
- Unity Play/EditMode 테스트, Refresh, 재임포트, 씬 저장 명령은 실행하지 않았다. 테스트는 추가/컴파일만 했고 통과했다고 주장하지 않는다.
- 실기 확인 필요: 근접/원거리, 3인 대표 광역, C 반격, X 연타, 중간 F8 종료, 승패 후 원위치/기본 줌. 큰 보스는 중앙 간격, HUD에 가리는 구도는 기존 CenterPos와 줌 비율을 조정해야 한다.
- CenterPos가 없는 기존 호스트는 아군의 종전 1유닛 전진 방식으로 대체한다. 상태 전용/지원 스킬에 불필요한 적 이동을 넣지 않았다.

브랜치 `codex/gameplayEdit`. 커밋/푸시 없음.
