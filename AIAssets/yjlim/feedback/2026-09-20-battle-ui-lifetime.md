# 전투 UI Image 파괴 오류 및 최종 정리

## 원인과 범위

사용자 스택은 DOTweenModuleUI 69행의 **Image 전용 DOColor setter**다. 프로젝트의 해당 호출은 파티 슬롯(초상화, HP 점멸)과 전투 메뉴(선택 색상)에 있었다. 두 곳 모두 자식 UI가 파괴될 때 모든 트윈을 명시적으로 해제하지 않았다. 스택만으로 실제 어느 Image였는지까지는 확정할 수 없어 해당 두 경로 모두 수정했다.

정리 범위는 전투 UI와 그 수명주기다. Z/X/C 판정, 적 스킬, 이번에 맞춘 중앙 궁극기·대상 전진·지상 복귀는 변경하지 않았다.

## 변경된 코드

- `Assets/_Game/Scripts/UI/Runtime/PartySlotUI.cs` 신규 분리: 기존 직렬화 타입/필드와 MP→AP 별칭 유지. 초상화·게이지·숫자·펀치를 슬롯이 소유한다. 숨김/다른 동료 재바인딩/소유자 종료에서 해제하고 HP/AP는 최신 확정값으로 맞춘다. 같은 강조 상태는 재시작하지 않으며 펀치 기준점도 복구한다.
- `BattleUIController.cs`: 슬롯 클래스 분리, 활성화/비활성화의 이벤트 구독 대칭화, 실제 구독 BattleManager 보관, 패널/플래시/흔들림 트윈 정리. 외부 target으로 등록된 플래시 Sequence도 새 플래시가 시작되면 정확히 교체한다.
- `BattleMenuUI.cs`: 버튼 Image 캐시, 색상/크기/이동/지연 복귀 콜백을 직접 소유. Hide/Disable/Destroy 공통 정리. 콜백을 강제로 완료하던 DOKill(true) 제거. 숨긴 메뉴의 늦은 콜백/입력 차단, 누락 버튼 탐색 안전화, 중단한 이동의 기본 위치 복구.
- 색상·채움·텍스트 콜백은 생성 당시 대상을 캡처하고 Unity null 판정을 한다. GameObject가 아니라 Image 컴포넌트만 파괴되어도 접근하지 않는다. 다른 소유자의 트윈은 정리하지 않는다.

씬/프리팹/Inspector 참조는 수정하지 않았다. 새로 연결할 항목도 없다. DOTween 플러그인이나 Safe Mode 설정은 변경하지 않았다.

## 검증

- Runtime/Editor 최종 직렬 dotnet 컴파일 오류 0, 기존 ConfigPanelUI 경고 2. 최초 전체 의존성 컴파일에는 기존 TextAnimator 패키지 경고 1개가 추가로 있었다.
- UI 수명·직렬화 별칭·GUID·Image DOColor 제거 등 정적 검사 12건과 diff 공백 검사 통과.
- 회귀 코드 9개 추가: 파티 Hide/중복 강조/파괴된 Image/다른 소유자 트윈 보존/부모 종료, 메뉴 숨김·지연 콜백/완료 콜백 비실행/Image 파괴, 커스텀 target 플래시 교체. 컴파일만 확인했다.
- **Unity Play/Edit 테스트는 실행하지 않았다.** 현재 수정은 소스 경로와 컴파일 기준이며 실제 오류 재현·해소까지 검증했다고 주장하지 않는다. 사용자 확인은 전투 중 HP 변화 직후 종료/재진입, 메뉴 연타 후 닫기, 전열 교체 정도면 된다.

브랜치 `codex/gameplayEdit`, 커밋/푸시 없음. [당일 기록](../../2026-09-20-update.md) · [작업 계획](../../../docs/superpowers/plans/2026-09-20-battle-ui-tween-lifetime.md).
