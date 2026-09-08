# Selected Runtime Fixes Implementation Plan

> **For agentic workers:** Use independent subagents for the damage, UI camera and Room audio changes; the parent owns battle status cleanup and integration. No Unity or test execution, commits, or asset rewrites are authorized for this task.

**Goal:** 사용자 승인 항목 3·4·5·6만 수정한다.

**Architecture:** 기존 피해 계산, BattleManager 종료 경계, UIViewportService, Room 진입 경로를 재사용한다. 상태이상/임시 버프는 전투 한정으로 해제하며 HP/AP·성장·장비는 보존한다. BGM 미지정 값의 기존 상속/명시 무음 정책을 보존한다.

**Tech Stack:** Unity 6000.3, C#, 기존 NUnit 회귀 테스트 소스.

## 승인된 범위

- 앞선 검토 보고서와 사용자 `3,4,5,6 고쳐주라`를 구현 승인으로 사용한다. 추가 기획/승인 단계는 반복하지 않는다.
- 턴 큐, 상태이상 행동 연결, Windmill 출입구/진입 Coordinator/Build Settings, 넉백, 곡 선택은 제외한다.
- 사용자 지시에 따라 diagnose의 Unity 재현/테스트 실행은 생략한다. 이미 확인한 호출 경로를 대조하고 회귀 테스트 코드는 남기되 실행했다고 보고하지 않는다.

## 작업

- [x] 피해: `BattleTurnQteModuleControllerService.cs`의 두 일반 적 공격을 `TakeDamage(... Physical, enemy)`로 연결하고 기존 결과·피드백에 최종 피해량을 전달한다.
- [x] 상태: `CharacterBase.cs`에 멱등적인 전투 상태 해제를 추가한다. `OnRemove`로 구독·VFX를 해제하고 상태 플래그·계산 스탯을 복구한다. `BattleManager.cs`에서 정상 종료/도주/패배/중단/파괴에 호출하며 후열도 포함한다.
- [x] UI: `UIViewportService.cs`가 마지막으로 적용한 카메라와 새 카메라를 비교하고 씬 변경 시 기존 등록 Canvas를 재연결한다. 기존 안정화 대기를 재사용한다.
- [x] BGM: 초기 진입/Room 이동/저장 복원의 성공한 Room 진입에서 같은 오디오 처리를 실행한다. 실패한 후보는 기존 음악에 영향을 주지 않는다.
- [x] 검증: 적절한 기존 테스트 경계에 회귀 사례 추가, diff/인코딩/참조 정적 검토. Unity를 실행하지 않는 C# 컴파일 확인(오류0, 기존 경고3). 테스트 실행은 사용자 지시로 생략.
- [x] 관련 규칙·검토 문서·오늘 기록에 변경된 계약과 미검증 범위를 남긴다. 커밋/푸시하지 않는다.
