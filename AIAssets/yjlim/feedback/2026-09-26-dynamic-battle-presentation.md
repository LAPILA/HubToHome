# 전투 동적 카메라·HUD·대사·C 반격

> 후속 요청으로 카메라의 상시 드리프트/호흡 줌은 제거했습니다. 현재 카메라/HUD 설정은 [짧은 카메라 전환·고정 HUD·5초 연격](2026-09-26-battle-camera-beats-rapid-skill.md)을 따릅니다. 아래 수치는 당시 구현 기록입니다.

## 요청과 구현 범위

- 행동자/피격자 중심 동적 추적과 줌. 전조부터 방어 결과 직후까지는 구도를 안정시킨다.
- PixelPerfectCamera는 유지한다. CinemachinePixelPerfect의 렌즈 양자화만 전투 숏 소유 기간에 완화하고 복구한다. 연속 줌 중 모든 픽셀의 정수 배율을 보장하는 기능은 아니다.
- 메뉴 전체 확대/축소 없이 초상·턴 큐·선택 표시에 조절 가능한 반응을 적용한다.
- 월드 대사는 카메라 줌과 무관한 읽기 크기, 넓은 줄폭, 턴 큐/하단 HUD 사이 안전 영역을 사용한다.
- C 성공은 아군 패링+후퇴 → 재접근 → 기존 반격 피해/복귀. 적에게 패링 넉백을 적용하지 않는다.

## 확인된 원인

- 공유 GameplayCameraRig의 `_staticBattlePresentation`이 켜져 있다.
- CinemachinePixelPerfect는 Body 단계에서 `CorrectCinemachineOrthoSize` 결과로 렌즈를 덮는다. 가상 카메라 Lens 값만 검사하면 최종 출력 줌 문제를 놓친다.
- 대사 프리팹은 최대 폭 480, 글자 92로 한 줄 가용 글자 수가 작고 화면 안전 영역 제한이 없다.
- 일반 패링 색상 트윈의 OnKill은 방어 앵커로 위치를 복원한다. C 재접근 중 뒤늦은 위치 복구가 개입하지 않도록 전용 반격 경로에서는 위치 복원을 소유하지 않는다.

## 검증 방침

Unity Play/Test 실행, 강제 Refresh, 열린 씬 저장 없이 코드/직렬화 참조 점검과 CLI 컴파일만 수행한다. 체감과 화면 배치는 사용자 플레이 확인이 필요하다. 커밋/푸시하지 않는다.

## 조절 위치

| 컴포넌트 / Inspector 묶음 | 기본값과 용도 |
| --- | --- |
| GameplayCameraRig → CameraController → 전투 동적 카메라 | 줌 비율 0.70(작을수록 확대), 추적 비중 0.62, 부드러움 0.24초 |
| 같은 곳 | 이동 강도 0.16, 미세 줌 0.018, 높이 -0.45, 방어 후 고정 0.12초 |
| 전투 호스트 → PositionManager → C 반격 연출 | 아군 후퇴 0.85 / 0.13초, 재접근 0.16초 |
| 전투 호스트 → BattleUIController → 전투 UI 반응 | 강도 1, 시간 배율 1, 대형 초상 진입 8px. 강도 0이면 추가 반응 끔 |
| Player/Enemy BattleSpeechBubble → 화면 가독성 | 기준 글자 14px, 줌과 무관한 크기 유지 |

UI는 턴 큐 초상 3px/테두리 강조, 대형 초상 진입/페이드, 목록 커서·테두리, HP/AP 색 반응을 사용한다. 메뉴/행 전체의 확대 축소는 없다. 대사는 최대 폭 1080 로컬 단위와 64 소스 폰트로 넓히고, 카메라 및 상위 캐릭터 스케일을 상쇄해 표시한다. 현재 값의 최대 본문 폭은 640×480 기준 약 236px이다. 긴 문장은 박스 높이를 늘려 잘림을 막고, 안전 영역보다 클 때만 전체 축소한다. 긴 독백은 여러 발화로 분리하는 것이 좋다.

## 소유권과 성능

- BattleCameraActionScope가 최신 명령일 때만 기본 구도로 복귀한다. 새 Timeline/focus 명령을 오래된 정리 루틴이 덮지 않는다.
- CameraController는 재사용 추적 타깃 하나와 캐시한 피벗 목록을 사용한다. 카메라 LateUpdate는 활성 전투 숏에서만 계산한다.
- CinemachineContinuousPixelZoom은 출력 카메라를 끄지 않는다. 원래 CinemachinePixelPerfect 활성 상태를 소유한 경우에만 끄고 복구한다. URP의 Cinemachine 호환 모드를 유지하여 렌더 직전 재보정도 방지한다.
- 정상 종료는 PP 호환 기본 크기까지 부드럽게 복귀한 뒤 양자화를 복원한다. 취소/명시적 카메라 교체/Timeline/비활성/파괴는 즉시 해제한다.
- QTE 전조 시작에서 구도를 고정하고, 결과/취소에서 소유한 고정 핸들을 해제한다. C 후퇴도 이 핸들을 사용하고 재접근부터 기존 숏의 행동자를 아군으로 바꾼다. 판정 시간/보상/AP/피해 공식은 변경하지 않았다.
- UI 트윈은 자신의 핸들만 정리하며 파괴된 Image 접근을 가드한다. 말풍선 화면 계산은 표시 중에만 수행하고 매 프레임 배열/검색/GetComponent를 만들지 않는다.

## 이번 변경 파일

- 새 파일: `CameraController.BattleMotion.cs`, `CinemachineContinuousPixelZoom.cs`와 각각의 meta.
- 카메라: `CameraController.cs`, `CameraController.Framing.cs`, `BattleCameraActionScope.cs`, `QTEManager.cs`.
- C 반격: `BattleLinkCounterService.cs`, `PlayerController.cs`, `PositionManager.cs`.
- UI/대사: `BattleUIController.cs`, `BattleTurnQueueIcon.cs`, `OptionRowUI.cs`, `PartySlotUI.cs`, `BattleSpeechBubble.cs`, `BattleSpeechBubbleLayout.cs`.
- 직렬화: `GameplayCameraRig.prefab`의 고정 모드 해제, `BattleSpeechBubble_Player.prefab`/`BattleSpeechBubble_Enemy.prefab`의 최대 크기·글꼴 크기·팝 강도 변경. 기존 GUID/fileID/Inspector 연결은 유지. 새 런타임 카메라 보조 컴포넌트는 자동 생성하므로 수동 연결 불필요.
- 검사 코드: `CameraFramingTests.cs`, `BattleLinkCounterServiceTests.cs`, `BattleSpeechBubbleLayoutTests.cs`. 런타임 실행하지 않음.
- 문서: 당일 update, AIAssets index, CONTEXT, battle/dialogue 규칙, 실험실 사용법.

## 검증 결과와 남은 확인

- Runtime + Editor CLI 컴파일 오류 0. 기존 ConfigPanelUI.CategoryLabel 미할당 경고 2개.
- 회귀 검사 코드: 실제 PixelPerfect 출력 카메라/URP 렌더 시작 경로의 Lens 유지, Timeline 전환/오래된 방어 핸들 무효화, 아군만 후퇴 후 반격/단일 피해, 대사 화면 경계. **코드 추가·컴파일만 했으며 Unity 테스트 통과를 주장하지 않는다.**
- 최종 diff 검사 통과, 변경/신규 39개 파일 UTF-8 유효성 통과, 이번 수정 프리팹 3개의 fileID/GUID 참조 보존 확인. 마지막 Runtime+Editor 재빌드도 오류 0/기존 경고 2. 씬/DB/스킬 자산/조명 자산은 이번 범위에서 수정하지 않았다.
- 사용자 확인: 일반 공격 줌 → 전조에서 구도 정지 → C 후퇴/재접근 → 행동 종료 줌 복구; 긴 대사와 3인 파티에서 턴 큐 겹침; 4:3 이외 화면과 모바일/콘솔 가독성. 실기 성능/손맛/픽셀 흔들림은 미검증.
- 브랜치 `codex/gameplayEdit`, 커밋/푸시 없음. 앞선 중앙 교전/근접 패링 수정 보존.

## 하네스 교훈 (2026-09-26)

1. Unity가 열려 있으면 새 .cs 생성 뒤 csproj가 자동 재생성될 수 있다. 수동 Compile Include를 추가한 첫 빌드에서 CS2002 중복 소스 경고가 발생했다. 생성된 항목을 확인하고 이번에 수동 추가한 두 줄만 제거한 후 재빌드하여 중복 경고가 사라졌다. 다음에는 현재 Include를 먼저 검사한다. 강제 Refresh는 필요하지 않았다.
2. 다중 파일 apply_patch가 실패하거나 사용자 중단이 들어오면 부분 반영될 수 있다. 실패 메시지만 보고 전체 미적용으로 가정한 재시도가 이미 바뀐 문맥과 충돌했다. 각 파일의 실제 문구를 rg/Get-Content로 재확인하고 미반영 파일만 개별 패치한 후 컴파일했다. 같은 수정 전체를 무조건 재전송하지 않는다.
3. 이 환경의 전역 지침 경로 `C:/Users/yjlim/.codex/memories/HARNESS_LESSONS.md`는 확인되지 않았고 허용된 작업 경로 밖이다. 이번 교훈은 프로젝트의 이 문서와 당일 update에 남긴다. 기존 사용자 프로필 밖의 경로를 임의 생성하지 않았다.
