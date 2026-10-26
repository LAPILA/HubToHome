# 전투 카메라 안전영역·픽셀 안정화 구현 기록

**목표:** 고정 HUD를 피하는 구도와 정수 픽셀 배율을 함께 보장하고, 이동·홉·복귀·타격마다 반복되는 렌즈 확대·축소를 제거한다.

**구조:** BattleUIController가 실제 화면 안전영역을 제공한다. CameraController가 몸체 캐시·전투 고정 렌즈·정책 수명을 소유한다. CinemachineContinuousPixelZoom은 기존 이름을 보존하되 Body·Finalize·Brain 출력의 픽셀 보정 어댑터로 사용한다. Unity6 /Cinemachine3 /DOTween /Odin을 유지하고 외부 플러그인은 수정하지 않는다.

## 원인 확인

- [x] 기존 적응형 Fit이 캐릭터 이동·점프마다 최종 렌즈를 변경하는 반복 배율 문제 확인.
- [x] 네이티브 확장 비활성화와 PPC 보정 반환값 무시로 호환 모드만 켜지는 경로 확인.
- [x] Finalize 이후 Brain 보간이 다시 비정수 렌즈를 만들 수 있는 출력 경계 확인.
- [x] HUD가 숨겨진 동안 정책을 해제하면 raw 복원 렌즈3.75가 노출되고 HUD 재표시 때7.5로 재점프할 수 있는 수명 경계 확인.
- [x] PPU32 /480기준에서 허용값7.5/n, Lens4의1.875원본픽셀 배율과 안전 시야4→7.5올림 규칙 확정.

## 구현

- [x] 기존640×480 UI 안전영역·Screen.safeArea·말풍선 API와 몸체 캐시 보존.
- [x] 순수 CameraPixelGridPolicy와 SnapSizeAtLeast·좌표 정렬 추가. 원본 축소 없이 기준 렌즈를 상한으로 사용.
- [x] 기존 Fit 보존, FitAtFixedSize 추가. 크기는 고정하고 중심만 보정하며 불가능한 배치도 자동 확대하지 않음.
- [x] 전투 진입 시 배율1 /기준7.5에서 안전 Fit→정수 배율 선택 후 session동안 고정. 최소값3.75.
- [x] 이동·홉·복귀·행동 교체의 반복 줌 제거. 엄격 픽셀 전투의 타격 줌·스킬 줌·Dutch 회전 억제.
- [x] PPC 비활성→활성 등 모드 변화와 픽셀 기준 변경 시 고정값 재계산. 용량 초과는 전투당 한 번 경고.
- [x] Body·Finalize의 보정 반환값 적용, XY 정렬, Dutch0. 네이티브 확장 토글 제거.
- [x] CameraUpdatedEvent로 Brain 보간 후 렌즈 재보정. Hold 우선, 위치는 렌더 직전 기존 PPC 정렬 이용.
- [x] 명령 Release와 정책 소유권 분리. 임시 HUD 숨김에도 같은 session의 정책·고정 렌즈 유지. UI 화면 영역이 없으면 중심 보정만 생략하고 출력 렌즈 보정은 계속 적용.
- [x] Timeline·컴포넌트 비활성화·실제 전투 종료에서 명시적 정책 해제·재획득. Timeline에서도 격자는 유지. 비활성화·파괴 시 이벤트 구독 해제.
- [x] 기존 UI 정수 Overlay·URP Render Scale1·AA 비활성 유지. 새 카메라·RT 시스템·GPU 패스·추가 Inspector 연결·이번 턴의 씬/프리팹 변경 없음.

## 검증 완료

- [x] 생산 정책·프레이밍 및 공유 Editor 소스 연결 하네스52케이스 /73,689단언 통과.
- [x] NaN/Inf·극단값·렌즈 격자·네이티브 상한·XY격자·이동/홉/복귀의 동일 렌즈·안전 중심 검사.
- [x] Runtime447 /Editor370소스 CLI 컴파일 오류0, 경고225 /0.
- [x] 메타1,555개 GUID32hex·중복 검사. 새 정책/테스트 GUID 및 UTF8 noBOM 확인.
- [x] 엔진 의존 CameraFramingTests의 출력·Hold·소유권 회귀 소스 컴파일 확인. 엔진 실행 통과로 표현하지 않음.
- [x] [제작 규칙·조정값·한계](../../../AIAssets/yjlim/feedback/2026-10-23-camera-pixel-stability.md)와 설계 문서 갱신. 브랜치gameplayEdit, 커밋·푸시 없음.

## 남은 실제 화면 확인

- [ ] Unity Play에서 전투 진입·연타·홉·회피·반격·복귀의 체감 확인.
- [ ] HUD 숨김·Timeline·출력 크기 변경 중 Hold와 실제 카메라 전환 확인.
- [ ] GPU·모바일·실기기의 렌더 선명도와 프레임 시간 확인.

이번 작업에서는 Play·강제 Refresh/Reimport·열린 씬 저장을 하지 않았다.640×480미만 출력 축소, 의도적인 ScreenSplit/배경 왜곡, 스프라이트 자체 회전·스케일은 별도 한계다.
