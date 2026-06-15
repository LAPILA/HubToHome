# 2026-06-16 패치노트 - aim_shooter 모듈 셸

## 변경

- 전투 기본 Game Module registry에 `aim_shooter`를 추가했습니다.
- `BattleAimShooterGameModuleRuntime`을 추가해 비-QTE 모듈이 `module.switch` / `module.start` 대상이 될 수 있게 했습니다.
- `IBattleGameModulePresentationController`와 `BattleUIController` 연동으로 비-QTE 모듈 진입 시 기존 QTE 메뉴/타겟팅/방어 UI가 남지 않도록 했습니다.
- `IBattleAimShooterModuleController`를 추가해 실제 슈팅 루프가 나중에 `BattleManager`가 아니라 `aim_shooter` 모듈 뒤에서 커질 수 있게 했습니다.
- 관련 EditMode 성격 테스트를 추가했습니다.

## 주의

- `aim_shooter`는 아직 실제 슈팅 전투 구현이 아닙니다.
- 현재는 모듈 등록, UI/입력 소유권 전환, QTE 입력 차단을 검증하는 아키텍처 셸입니다.
