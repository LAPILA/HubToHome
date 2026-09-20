# 토끼 슬라임 종합 전투 샘플

## 공격하지 않던 문제 후속 수정

사용자 생성 결과와 Editor.log에서 원인을 확인했습니다. `OverworldEnemy`가 `EnemyCharacter`를 필수로 요구하는데 생성기가 원래 AI부터 제거해 Unity가 삭제를 거절했습니다. 그 결과 대기 전용 `BunnySlimeCharacter`와 공격용 `BunnySlimeShowcaseEnemy`가 함께 남았고, BattleManager가 앞의 대기 AI를 선택했습니다.

- 이미 생성된 `Prefabs/BunnySlime_Showcase.prefab`에서 대기 AI 컴포넌트 하나와 해당 목록 참조만 제거했습니다. 프리팹 GUID/루트/공격 AI/Animator/스킬/대사는 보존했습니다. 다른 자산이 제거한 컴포넌트를 참조하지 않음을 먼저 확인했습니다.
- 생성기는 의존 컴포넌트 먼저 제거 → 기존 AI 제거 성공 확인 → 공격 AI 하나만 연결 순서로 수정했습니다. 재호출은 같은 공격 AI를 유지합니다.
- 기존 결과를 다시 열 때와 실험실 시작 때도 중복/잘못된 AI를 검사합니다. 무조건 대기하는 전투를 조용히 시작하지 않습니다.
- 일반 내려찍기, X 회피 전용 쓸기, 3연타, 젖음, 독 시약, 광역 공격, C 연계 반격의 기존 7패턴은 그대로 연결되어 있습니다.
- 소스 회귀 검사는 실제 `RequireComponent` 관계, 이전 중복 형태, 재호출, Session 진입 차단을 포함합니다. Unity 실행 없이 생성된 prefab 정적 검사를 전후 비교했습니다. 제거한 대기 AI는 원본 프리팹에 그대로 보존되어 있으며 샘플의 중복만 제거했습니다.

## 실행

Unity Edit Mode → **Hub To Home / 테스트 / 토끼 슬라임 종합 전투 열기** → 생성된 씬에서 Play → ↑↓ / Z. 전투 중 F8 중단. 기존 자산은 재생성으로 덮어쓰지 않습니다.

[상세 사용 안내](../../../docs/bunny-slime-battle-lab.md)

## 구현

- 원본 BunnySlime 더미/Prefab/Idle Controller/TestMap 보존. 별도 개발씬/파티/스킬/적 데이터의 생성기.
- 공방 안전시험 상황극: 오프닝, HP 66%/33% 전환, 합격 대화. 대화 5개/화자 2개/연출 시퀀스 4개.
- 일반/회피 전용/3연타/광역/상태 기믹/연계 반격의 7개 적 스킬. HP 페이즈별 순차 AI로 패턴을 재현.
- 기존 위젤 스킬 10개, 속성탄 4개, 상태 부여 9개를 6명×4슬롯에 배치. 원본 아이템 7개, 수치 장비 3개.
- 독립 연습 모드 3개(가드·회피/반격/후열). 후열은 기존 3+3 자동 등장 흐름 사용.
- 공용 전투/대사/카메라 시스템 사용. 적 스킬은 기존 블록으로 실행하며 새 시나리오 액션 문법은 없음.
- 패배의 선택적 `IEncounterDefeatPolicy`로 실험실만 메뉴 복귀. 원래 조우의 게임오버 정책은 유지.
- fresh Play 검사/샘플 ID 소유권 검사/파일 저장 미연결. 결과 뒤 1프레임 대기해 Host 정리 후 메뉴/소모품/HP/AP 복구.

## 주요 코드

- `Battle/Development/BunnySlimeBattleLabData`, `BunnySlimeBattleLabSession(.View)`, `BunnySlimeShowcaseEnemy`.
- `Editor/BattleLab/BunnySlimeBattleLabBuilder`, `BunnySlimeLabContentBuilder`, `BunnySlimeLabVisualBuilder`, `BunnySlimeLabStoryBuilder`, `BunnySlimeLabSceneBuilder`.
- 공용 변경은 `EnemyCharacter.SelectSkill` 확장 지점과 Telegraph 정리, `BattleManager` 선택 위임/패배 분기, `BattleEncounterService` 선택적 패배 정책 인터페이스.
- canonical Source는 `Content/Scenarios/Source/Battle/BunnySlimeBattleLab`, 생성 결과는 `Content/Maps/Development/BunnySlimeBattleLab`.

## 검증과 남은 확인

Runtime/Editor/신규 테스트 소스 정적 컴파일 오류 0. 원본 BunnySlime 데이터/프리팹/Animator diff 없음. 신규 UTF-8 및 메타 존재, 변경 공백 검사 통과.

**Unity 생성기·Play·테스트는 실행하지 않았습니다.** 씬/ScriptableObject/Animator 생성 결과는 사용자가 메뉴를 실행해야 만들어집니다. 실제 UI·음악·판정 체감 및 장시간 전투는 미검증입니다. 신규 외형/장비 패시브/적 자기버프 타깃 지원은 이번 샘플에 포함하지 않습니다. 커밋·푸시는 하지 않았습니다.
