# HUBTOHOME-18 콘텐츠 검증기 인수인계

## 목적

기획자가 Character, Enemy, Skill, Item, Battle Scenario를 추가한 뒤 런타임 진입 전에 ID와 참조 문제를 한 창에서 찾도록 만든 Editor 전용 안전망이다. 일반 스캔은 자산을 바꾸지 않으며, 복구 작업은 별도 버튼에서만 실행한다.

## 사용법

1. Unity 메뉴에서 `Hub To Home > Content > Validation Window`를 연다.
2. `Scan`으로 현재 프로젝트를 검사한다.
3. 검색과 Error/Warning 토글로 결과를 좁힌다.
4. `Select`로 문제가 있는 자산을 Project 창에서 선택한다.
5. 필요한 경우에만 `Generate Missing IDs`, `Repair Prefab Links`, `Rebuild Catalog`를 실행한다.

자동 검증은 `Hub To Home > Content > Validate Project Content`를 사용한다. Error가 있으면 실패하고 Warning만 있으면 통과한다.

## 구조

- `AssetDatabaseContentSource`: 프로젝트 자산을 정렬된 Snapshot으로 수집
- `ProjectContentValidator`: 규칙 실행 순서만 조정
- `ContentIdentityRules`: ID 누락·형식·중복
- `RuntimeCatalogContentRules`: Runtime Catalog 일관성
- `ScenarioContentRules`: 참가자와 Sequence·Dialogue·Audio·Action Catalog 계약
- `BattleContentRules`: 캐릭터·적 프리팹, 스킬, 드롭, 전투 초상화
- `SkillItemContentRules`: Skill Action block, 소비 아이템, 아이콘
- `ContentValidationReport`: 오류 코드·심각도·대상·경로를 정렬해 보관

## 현재 결과

- Error: 0건
- Warning: 10건
- 슬라임 턴 순서 초상화: 1건
- SmallPotion 아이콘: 1건
- 스킬 아이콘: 8건

이 경고들은 런타임을 막지 않는 아트 작업 목록이다. 임시 이미지를 코드에서 자동 할당하지 않는다.

## 확장 규칙

- 새 검증은 도메인별 Rules 클래스에 추가한다.
- 오류 코드는 기존 규칙처럼 `domain.subject.problem` 형태로 안정적으로 유지한다.
- 선택 가능한 Unity Object를 issue owner로 전달한다.
- 스캔 중 `SetDirty`, `SaveAssets`, ID 생성, Prefab 수정은 금지한다.
- 시나리오 액션 문법과 매개변수 검증은 새로 복제하지 않고 `ScenarioCatalogValidator`를 확장한다.

## 검증 결과

- Unity EditMode 704/704
- TestMap 조우 2/2
- 실제 콘텐츠 스캔 0 errors / 10 warnings
- `_Game` Prefab 58개 Missing Script 0
