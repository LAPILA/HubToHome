# 속도 기반 턴 — 최종 정리

## 현재 범위

- **증기 가속 샘플 스킬은 사용자 요청으로 삭제했습니다.** 원본 위젤 DB, 실험실 전열 DB, 콘텐츠 카탈로그의 연결과 샘플 전용 검사도 제거했습니다.
- 속도 기반 턴 계산, 자연 만료를 반영한 턴 예고, DOTween 턴 큐 이동·동시 확대는 유지합니다.
- 공용 Haste 상태효과와 상태 부여 블록의 속도 보정 항목은 남깁니다. 실제 가속 스킬의 수치·획득·밸런스 설계는 보류합니다.
- 저장 파일, 캐릭터 능력치, 기존 공격·QTE·교대 규칙은 이번 정리에서 변경하지 않았습니다.
- 이번 작업 대상은 `codex/gameplayEdit`이며 사용자의 PUSH 요청에 따라 `origin/codex/gameplayEdit`에 반영합니다. 기존 ZEV DB 2개 및 자동 생성 폰트 변경은 이번 커밋에서 제외합니다.
- **배포 상태:** 로컬 커밋 완료, GitHub 사용자 인증이 필요해 PUSH는 미완료입니다. 원격은 `73bba856`이며 인증 완료 뒤 일반 push를 다시 실행해야 합니다.

## 턴 계산 규칙

1. 각 생존 전열 참가자는 남은 준비량 1000에서 시작합니다.
2. 다음 행동까지의 시간은 `남은 준비량 / max(1, 실제 SPD)`입니다.
3. 가장 먼저 도착하는 시간만큼 전원의 준비량을 줄이고, 선택된 캐릭터만 다시 1000을 부여받습니다.
4. SPD가 높으면 순서뿐 아니라 행동 빈도도 증가합니다. 실제 행동은 한 명씩 실행하며 실시간 ATB는 아닙니다.
5. 같은 도착 시점은 최초 등록 순서를 유지합니다. 선제공격은 가장 빠른 생존 아군의 첫 행동 한 번만 앞당깁니다.
6. 메뉴·QTE·연출의 실제 재생 시간은 준비량에 영향을 주지 않습니다.

예: A의 SPD 20, B의 SPD 10이고 A가 먼저 등록되면 `A → A → B`가 반복됩니다. 별도의 연속 행동 횟수 제한은 없습니다. AP 회복 및 상태 지속시간은 기존 개인 턴 기준이므로 빠른 캐릭터는 이 처리도 자주 받습니다.

33원정대의 공개되지 않은 내부 공식을 그대로 복제했다는 뜻은 아닙니다. 본 프로젝트의 결정적인 누적 준비량 방식입니다.

## 상단 예고의 약속

- 0번은 현재 행동자, 이후는 같은 계산기의 상태 복사본으로 만든 미래 순서입니다.
- **새로운 상태 부여·해제·사망 등 새 사건이 없다면 예고한 미래 순서가 턴 종료 후 그대로 이어집니다.**
- 기존 속도 상태의 자연 만료는 예고를 만들 때 미리 반영합니다. 현재 행동의 남은 종료 처리와 미래 각 캐릭터의 턴 시작/종료 횟수를 구분합니다.
- 현재 SPD가 같아도 상태 기간이 갱신되면 다시 예고합니다. 실제 표시 순서가 같으면 중복 알림과 트윈 재시작을 생략합니다.
- 가까운 3칸을 강제로 예약하거나, 가속 효과를 임의로 연장하는 방식은 아닙니다.
- 예고 계산은 실제 OnTick/OnRemove, 피해, HP/AP, 상태 지속시간을 변경하지 않습니다. 미래에 새로 선택할 스킬이나 피해 사망까지 예측하지는 않습니다.

이전 오류는 예고가 현재 가속 SPD를 영구 유지한다고 가정한 것이었습니다. 가속 마지막 턴에 연속 행동을 표시했다가 실제 만료 뒤 사라졌습니다. 준비량 보존만으로 해결되지 않아 상태 수명을 읽는 예고 경로를 추가했습니다.

## 전투 생명주기

| 상황 | 처리 |
| --- | --- |
| SPD 변경 | 누적 준비량과 현재 행동자를 유지하고 미래 순서 재계산 |
| 사망·파괴·퇴장 | 다음 동기화에서 계산기에서 제거 |
| 부활·신규 합류 | 새 준비량으로 합류 |
| 전열 전멸 → 후열 | 후열은 새로 준비, 살아 있는 적의 진행량은 유지 |
| 기절·행동 불가 | 해당 개인 턴을 소비하고 기존 상태 처리 |
| 중복 턴 시작 요청 | 현재 행동이 진행 중이면 무시 |
| 모듈 종료·전투 종료·파괴 | 상태 구독 해제, UI 트윈 중지 및 표시 이력 정리 |

## 편집 위치

### 실제 속도

- 일반 위젤: `Assets/_Game/Content/Characters/AllyDB/WizzelDB.asset`
- 일반 ZEV: `Assets/_Game/Content/Characters/EnemyDB/ZEV/Enemy_ZEV.asset`
- **BunnySlimeBattleLab은 별도 DB를 사용합니다.** 위젤은 `Content/Maps/Development/BunnySlimeBattleLab/Data/Party/front.asset`, ZEV는 `Data/Enemy/ZEV_Lab.asset`입니다. 원본 DB를 바꿔도 이 실험실에 자동 전달되지 않습니다.
- 각 데이터의 Base Stats → SPD를 편집합니다. 성장·장비·저장 데이터·상태 보정을 거친 실제 `CharacterBase.SPD`가 계산에 사용됩니다.
- 이미 생성된 적은 스탯을 복사해 사용하므로 DB를 수정한 뒤 전투를 새로 시작해야 합니다.

### 턴 큐 연출

`Assets/_Game/Content/Battle/Prefabs/System/SeamlessBattleHost.prefab`의 BattleUIController:

- Turn Queue → 재정렬 시간: 기본 0.24초
- 동시 확대 배율: 기본 1.06
- 공통 전투 UI 반응의 강도/시간 배율도 적용. 강도 0이면 즉시 정렬

최대 6개 슬롯을 재사용합니다. GridLayoutGroup은 부모 슬롯, DOTween은 안쪽 초상만 움직입니다. 같은 캐릭터가 여러 번 나와도 실제 참조와 출현 순서로 구분합니다. RectMask2D 여백 -4로 확대 잘림을 완화했고, 0번 노란 강조와 DB 초상 연결은 유지합니다.

## 주요 코드

| 위치 (Assets/_Game/Scripts 아래) | 책임 |
| --- | --- |
| Battle/Runtime/Services/BattleSpeedTurnScheduler.cs | Unity 비의존 실제/예고 공통 계산기 |
| Battle/Runtime/Services/BattleTurnQteModuleControllerService.cs | 참가자 동기화, 턴 시작/종료, 구독 및 기존 전투 연결 |
| Battle/Runtime/Services/BattleTurnQueueProjection.cs | 계산된 예고만 표시, 임의 라운드 채우기 제거 |
| Characters/Runtime/StatusEffect.cs | 미래 턴 경계의 상태 수명 읽기 |
| Characters/Runtime/CharacterBase.cs | 실제 상태를 바꾸지 않는 미래 SPD 조회 |
| Characters/Runtime/CharacterStats.cs | 기존 레이어·반올림 규칙을 공유하는 단일 스탯 계산 |
| UI/Runtime/BattleTurnQueueView.cs | 최대 6개 슬롯 매칭·재사용·레이아웃 |
| UI/Runtime/BattleTurnQueueIcon.cs | 개별 이동/확대 트윈 및 수명 관리 |

새 컴포넌트를 씬에 붙이거나 Inspector 참조를 수동으로 추가할 필요는 없습니다.

## 검증과 남은 확인

- 순수 계산 검사 **19개 통과**. 고정 seed의 100조합·4,800행동에서 만료 전부터 저장한 예고가 실제 실행 및 후속 예고와 이어지는지 검사했습니다.
- Editor C# CLI 빌드로 컴파일 확인. Unity 의존 서비스·상태·UI 검사는 코드 추가 및 컴파일까지만 진행했습니다.
- Unity Play/EditMode 테스트, 강제 Refresh/Reimport, 열린 씬 저장은 하지 않았습니다.
- 수동 확인 대상: 실제 SPD 차이와 연속 턴, 상단 순서와 행동 일치, 사망·기절·후열 합류, 초상 이동/마스크. 샘플 가속 스킬을 사용하는 수동 확인 절차는 폐기했습니다.
- 삭제한 샘플은 미커밋 신규 자산이어서 Git 이력에 없습니다. 재제작할 경우 기존 SkillData의 Action_ApplyStatus로 만들 수 있습니다. 기존 실험값은 AP 8 / Haste +100% / 자기 턴 종료 3회였으며 확정 밸런스가 아닙니다.

이전 조사·실패→수정 과정과 하네스 교훈은 [당일 기록](../../2026-09-28-update.md)에 보존했습니다. 관련: [계산기 계획](../../../docs/superpowers/plans/2026-09-28-speed-turn-scheduling.md) · [턴 예고 계획](../../../docs/superpowers/plans/2026-09-28-haste-turn-queue.md) · [공유 용어](../../../CONTEXT.md).
