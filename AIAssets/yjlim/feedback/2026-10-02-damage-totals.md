# 속성별 누적 피해 숫자

## 적용 결과

한 행동 안에서 같은 대상·같은 타입의 피해는 하나의 숫자에 합산합니다. 예: 20 → 43 → 71. 타격마다 새 숫자를 격자로 만들지 않습니다. 다른 대상과 다음 행동은 별도 집계입니다.

| 위→아래 순서 | 포함되는 기존 피해 | 표시 색 |
|---|---|---|
| 물리 | Physical | 흰색 |
| 화염 | Fire + 화상 피해 | 기존 화상색 |
| 냉기 | Ice + 냉기 상태 피해 | 기존 냉기색 |
| 전기 | Electric + 감전 추가 피해 | 기존 감전색 |
| 중독 | Corrosion + 중독 피해 | 기존 중독색 |
| 출혈 | 출혈 피해 | 기존 출혈색 |

실제 화면에는 글자 없이 **숫자와 색상만** 표시합니다. 누적 상태 중첩값 자체는 더하지 않습니다. 일반 피해의 캐릭터 대표색 표시는 속성색으로 대체했으며, 캐릭터 HP바/파티 대표색은 유지했습니다. 기존 직접 피해 `FinalDamage`와 상태 피해 이벤트를 각각 한 번만 집계하며 계산·저항·방어·HP/사망 통지는 변경하지 않습니다.

- 위치: 캐릭터 화면 오른쪽의 세로 열. 같은 대상의 속성은 같은 X에 고정 순서로 정렬합니다. 다른 대상과 겹치면 열 전체를 옆으로 피합니다.
- 타격 반응: 현재 크기에서 0.04초 동안 일반1.12배/치명타1.18배로 확대 → 정점에서 최신 합계로 교체·살짝 밝게 강조 → 0.12초 동안 원래 크기/속성색으로 복귀합니다. 첫 숫자는 즉시 표시합니다. 치명타도 같은 속성 줄에 합칩니다.
- 확대 중 연속 입력은 같은 트윈의 정점에 최신 합계를 반영하며 갱신 시각을 뒤로 미루지 않습니다. 복귀 중 새 타격은 현재 크기에서 새 반응을 시작하며 배율을 곱하거나 트윈을 쌓지 않습니다. 카메라/숫자 위치 흔들림은 추가하지 않습니다.
- 스킬/행동 종료 후 0.7초 유지 + 기본 0.24초 페이드. 긴 연타는 타격 간격에 관계없이 행동이 끝날 때까지 유지합니다. 새 행동이 같은 대상을 공격하면 종료된 이전 묶음은 먼저 반환합니다.
- 시작/종료 상태 틱과 C 반격은 독립된 묶음입니다. 수명 범위 없는 외부 통지는 같은 프레임만 묶어 자동 종료합니다.
- MISS는 기존 짧은 별도 표시를 유지하되, 누적 피해 줄을 밀어내지 않습니다. 일반 피해 0 이하는 숫자를 만들지 않습니다.
- 한 대상·행동당 최대 6개 줄, 최대 12개 대상·행동 묶음으로 한정합니다. 기존 팝업 풀 재사용, 매 프레임 검색/레이아웃 계산 없음. 좁은 세로 공간에서는 줄 전체의 글꼴을 비례 축소합니다.

## 조절 위치

기존 Battle UI의 `BattleDamagePopupPresenter` → **속성별 누적**:

- 행동 종료 후 유지: 기본 0.7초
- 속성 줄 간격: 기본 4
- **타격 반응**: 일반/치명타 확대, 커지는 시간, 복귀 시간, 정점 밝기 강조. 기본값1.12/1.18배,0.04/0.12초,밝기20%. 배치도 설정된 최대 배율과 교체될 숫자의 폭을 먼저 예약합니다.

글자 크기는 기존 `BattleUIController`의 피해 숫자 설정(기본30)을 사용합니다. 기존 Presenter의 Motion/Critical 이동 설정은 호환용 단발/MISS 경로에만 적용됩니다. 새 컴포넌트 연결이나 씬/프리팹 수정은 필요 없습니다.

## 소유권 및 변경 파일

경로 기준은 `Assets/_Game/Scripts/`입니다.

- `Battle/Runtime/Presentation/BattleDamageFeedback.cs`: 표시 타입/속성/행동 ID. 기존 생성자 호출은 선택 매개변수로 호환.
- 신규 `Battle/Runtime/Presentation/BattleDamageBatchTracker.cs`: 행동 ID와 종료 통지. 중첩·순서가 바뀐 종료·늦은 Dispose를 구분하며 현재 HP/턴은 소유하지 않음.
- `Battle/Runtime/BattleManager.cs`, `Services/BattleTurnQteModuleControllerService.cs`, `Services/BattleLinkCounterService.cs`: 행동·상태 틱·반격·중단 경계.
- `Scenario/Runtime/Presentation/BattleSkillTimelineRunner.cs`: 시나리오 스킬도 고유 표시 묶음을 보유. 기존 Action ID/YAML/자산/실행 순서 변경 없음.
- `Battle/Data/SkillActionBlocks.cs`, `SkillElementalEffects.cs`, `Action_ElementalUtility.cs`, `Action_RapidStrikes.cs`, `Action_AerialCrossSlash.cs`: 기존 피해 알림에 속성/ID 전달. 동기적 상태 반응은 해당 스킬의 표시 문맥에서 발생.
- `UI/Runtime/BattleDamagePopupPresenter.cs`, 신규 `.Totals.cs`, `BattleDamagePopupView.cs`, `BattleUIController.cs`: 풀·집계·세로 배치·DOTween 반응·이벤트 구독/해제.
- 기존 `BattleDamageFeedbackTests`, `BattleDamageFeedbackEventTests`, `BattleDamagePopupPresenterTests`, `BattleDamagePopupViewTests`: 새 계약 회귀 코드.

## 검증과 남은 확인

- 실제 Tracker/Feedback/Totals 소스를 사용한 관리형 검사 **889개 assertion 통과**: 50타 합계/단일 줄, 6타입 역순 입력→고정 세로 정렬, 두 대상 분리, 전기 직접+반응 합산, 다음 행동 초기화, 100회 재사용, 화면 영역, 중첩/취소/늦은 정리. Unity/TMP는 대역이므로 실제 렌더링 검증은 아닙니다.
- Runtime/Editor 컴파일 확인. NUnit 코드는 컴파일만 하며 Unity Play/NUnit 실행·Refresh·씬 저장은 하지 않습니다.
- 쥬스 후속 검증: Runtime/Editor 오류0/기존 ConfigPanelUI 경고2, 최대 배율1.18을 반영한 집계/배치 관리형 검사889항목 재통과. 숫자 교체 전/정점/복귀, 확대 중30회 갱신,50연타 재시작,색·크기·풀 복원,파괴된 텍스트 회귀 코드를 추가·컴파일했습니다. 실제 DOTween 재생/화면 검증은 미실행입니다.
- 수동 확인: 토끼/ZEV에서 긴 연타, 냉기+감전, 상태 틱, 전투 종료. 숫자/줄 간격과 화면 가장자리의 실제 글꼴 배치는 확인이 남습니다.
- QTE 배치, 캐릭터/적 스탯, 스킬 수치, 방어 판정, 턴 큐, 씬·프리팹·데이터 자산은 이번 작업에서 변경하지 않았습니다.
- 브랜치 `codex/gameplayEdit`. 커밋·푸시 없음.
