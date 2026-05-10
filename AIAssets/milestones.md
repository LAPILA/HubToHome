# HubToHome 마일스톤 요약

> 기준 시각: 2026-05-10 (KST)  
> 기준 데이터: 최근 커밋 로그, 현재 코드 구조, `AIAssets/todo.md`  
> 참고: 실제 `git push` 시각은 로컬에서 확인할 수 없어, 커밋 날짜를 푸시일 대용으로 사용했습니다.

## 현재 진행 단계

| 시스템 | 상태 | 판단 근거 |
| --- | --- | --- |
| 타이틀 -> 인트로 -> 이름 입력 | 구현됨 | `TitleMenuManager`, `IntroManager`, `NameInputUI`, `DialogueManager` 흐름이 연결됨 |
| 설정/입력 계층 | 구현 중이지만 usable | `GameConfigManager`, `ConfigPanelUI`, `GameInput`가 실제 런타임 경로를 가짐 |
| 오버월드 순찰 적 조우 | 프로토타입 구현 | `OverworldEnemy`, `BattleEncounterService`, `GlobalDataManager`가 접촉 전투를 구성함 |
| 대화 선택지 -> 전투 분기 | 구현됨 | `DialogueManager.OnChoiceSelected(...)`와 `CoStartBattleFromChoice(...)`가 활성화됨 |
| 전투 진입/복귀 | 프로토타입 구현 | `BattleManager`가 심리스/전용 배틀 씬 둘 다 처리함 |
| 저장/이어하기 | 미완료 | `OnClickContinue(...)`가 실질적인 로드 흐름을 호출하지 않음 |
| 상태이상 아이템 구조 | 기술 부채 단계 | `InventoryManager`와 `BattleManager`가 문자열 분기에 의존함 |

## 푸시일 기준 작업 히스토리

| 날짜 | 커밋 수 | 핵심 주제 | 결과 |
| --- | ---: | --- | --- |
| 2026-05-10 | 7 | 설정/입력 계층, 텍스트 속도, 오버월드 적 조우, 대화-전투 연결 | 플레이 가능한 시스템 수가 늘었고, 설정과 조우 전투가 실제 게임 루프에 붙기 시작함 |
| 2026-05-09 | 4 | 폴더 구조 재정리, 타이틀/인트로, 현지화, 기본 UX 루프 | 프로젝트 구조가 `Core / Features / Presentation / Shared` 중심으로 정돈되고 초반 UX 루프가 완성됨 |

## 2026-05-10 마일스톤 해석

- 오늘 작업의 핵심은 "단순 UI 추가"가 아니라 `GameConfigManager -> GameInput -> ConfigPanelUI`로 이어지는 설정 계층을 실제 시스템으로 승격한 점입니다.
- 전투 진입도 더 이상 오버월드 접촉 한 경로에 묶여 있지 않습니다. `BattleEncounterService`와 `DialogueEncounterContext`가 들어오면서 오버월드, 이벤트, 대사 선택지가 같은 전투 진입 파이프라인을 공유합니다.
- 오버월드 적은 `OverworldEnemy`를 통해 순찰, 접촉, 전투 후 재등장/제거 상태를 다루기 시작했습니다. 이제 단순 배치형 적이 아니라 영속 상태를 가진 월드 엔티티로 바뀌는 중입니다.

## 다음 마일스톤 게이트

1. 타이틀 `Continue`를 실제 세이브 복구 경로에 연결해 "처음부터만 가능한 빌드" 상태를 벗어나기
2. 오버월드 적의 도주 후 재활성/쿨다운 버그를 플레이테스트 기준으로 안정화하기
3. 설정 패널 문자열과 세부 옵션을 현지화/정리해 설정 시스템을 마감하기
4. 상태이상 적용을 `StatusFactory` 또는 레지스트리 기반으로 합쳐 아이템/전투의 중복 분기를 제거하기
