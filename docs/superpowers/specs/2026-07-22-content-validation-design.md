# Content Validation Design

## 목적

`HUBTOHOME-18`은 캐릭터, 적, 스킬, 아이템, 시나리오 콘텐츠가 런타임에 들어가기 전에 ID 충돌과 필수 참조 누락을 발견하고, 기획자가 오류 자산을 즉시 열 수 있게 만드는 작업이다.

기존 `ContentValidationWindow`의 메뉴, 누락 ID 생성, 프리팹 링크 복구, Runtime Catalog 재구축은 유지한다. 검증 규칙만 Editor 전용 서비스로 분리해 테스트 가능성과 확장성을 확보한다.

## 선택한 접근

### 채택: 구조화된 Editor 검증 서비스

- `ProjectContentValidator`가 자산 목록을 받아 `ContentValidationReport`를 반환한다.
- 각 `ContentValidationIssue`는 코드, 심각도, 메시지, 선택 가능한 대상 자산과 대체 자산 경로를 보유한다.
- `ContentValidationWindow`는 보고서 표시, 자산 선택, Ping, 재검사와 기존의 명시적 수정 명령을 담당한다.
- 누락 ID 생성, 프리팹 링크 복구, Runtime Catalog 재구축의 소유권과 메뉴 경로는 기존 창에 유지한다.
- `AssetDatabaseContentSource`가 Unity `AssetDatabase` 접근을 소유한다.
- 단위 테스트는 메모리 내 `ScriptableObject`와 가짜 소스를 사용해 규칙을 검증한다.
- 새 형식과 서비스는 모두 `Assets/_Game/Scripts/Editor` 아래에 두어 런타임 어셈블리에 포함하지 않는다.

### 제외한 접근

1. 기존 EditorWindow에 조건문을 계속 추가하는 방식은 빠르지만 UI와 규칙이 결합되어 회귀 테스트가 어렵다.
2. 속성이나 리플렉션으로 모든 검증기를 자동 등록하는 방식은 현재 콘텐츠 종류에 비해 복잡하고 오류 추적이 불명확하다.

## ID 규칙

런타임 데이터베이스가 `StringComparer.Ordinal`을 사용하므로 ID 비교도 대소문자를 구분한다. 혼동을 줄이기 위해 작성 규칙은 다음과 같이 고정한다.

- 앞뒤 공백 금지
- 소문자 영문 또는 숫자로 시작
- 이후 문자는 소문자 영문, 숫자, `.`, `_`, `-`만 허용
- 동일 콘텐츠 종류 안에서 중복 금지
- 기존 ID에 강제 접두사는 요구하지 않는다. 현재 `player_001`, `zev.basic`, `consumable.small_potion` 형식을 모두 허용한다.

자동 생성은 기존 GUID 기반 방식을 유지한다. 이미 입력된 ID는 자동 변경하지 않으며, 중복 ID도 임의 수정하지 않는다.

## 검사 범위

### 캐릭터

- `CharacterID` 필수, 형식, 중복
- `BattlePrefab` 필수 및 `PlayerCharacter` 컴포넌트 확인
- `DefaultSkills`의 null 및 프로젝트 Skill 목록 외 참조 확인
- Portrait와 TurnOrderPortrait 누락은 제작을 막지 않는 Warning으로 보고

### 적

- `EnemyId` 필수, 형식, 중복
- `BattlePrefab` 필수 및 `EnemyCharacter` 컴포넌트 확인
- 일반/강한 스킬의 null 및 프로젝트 Skill 목록 외 참조 확인
- 구조화 드롭과 legacy 드롭 ID가 Item 목록을 참조하는지 확인
- 드롭 수량 범위와 확률 범위 확인
- Portrait와 TurnOrderPortrait 누락은 제작을 막지 않는 Warning으로 보고

### 스킬과 아이템

- `SkillID`, `ItemID` 필수, 형식, 중복
- 스킬 타임라인의 null 블록과 필수 프리팹 참조 확인
- 소비 아이템 효과, 대상 스탯, 상태이상 ID, 스택 수치 확인
- Icon 누락은 제작을 막지 않는 Warning으로 보고

### 시나리오

- `BattleScenarioData.ScenarioId` 필수, 형식, 중복
- Sequence, Dialogue, Audio 직접 참조의 null 및 로컬 ID 중복 확인
- 적 참가 ID는 프로젝트 Enemy ID와 대조
- 아군 참가 ID는 canonical `player` 별칭을 허용하며, 그 외 값은 프로젝트 Character ID와 대조한다.
- 참가 ID의 빈 값, 중복, 알 수 없는 ID는 Error로 보고한다.
- 액션 카탈로그가 하나로 결정되면 기존 `ScenarioCatalogValidator.ValidateBattleScenario`를 호출하고 결과만 구조화 보고서로 변환한다.
- 액션 파라미터, `dialogue.wait`, Sequence 호출 그래프, Trigger, Timeline 내부 규칙은 기존 `ScenarioCatalogValidator`가 계속 소유하며 새 검사기에서 재구현하지 않는다.

### Runtime Catalog

- 카탈로그와 기본 UI 폰트 존재 확인
- 카탈로그의 null, 중복 참조 확인
- 프로젝트에서 발견한 각 콘텐츠 자산이 카탈로그에 포함됐는지 확인
- 카탈로그 재구축 전에는 누락을 오류로 보여주고, 재구축 후 해소한다.

## 오류 정책

- `Error`: ID 누락/중복/형식 오류, 필수 프리팹·참조 누락, 알 수 없는 콘텐츠 ID. 자동 검증 메뉴를 실패시킨다.
- `Warning`: 선택적 표현 자산이나 권장 구성 누락. 로그는 남기지만 자동 검증을 실패시키지 않는다.
- 모든 문제는 안정적인 코드와 가능한 경우 대상 자산을 가진다. UI 행 클릭 시 `Selection.activeObject`와 `EditorGUIUtility.PingObject`를 사용한다.
- 카탈로그 자체 누락처럼 대상 자산이 없는 문제는 대체 자산 경로를 표시하고 선택 버튼을 비활성화한다.
- 같은 규칙과 대상에서 발생한 문제는 결정적인 순서로 정렬한다.

## 안전성

- Scan은 자산을 수정하지 않는다.
- 자동 생성과 복구 버튼은 `Undo.RecordObject`, `EditorUtility.SetDirty`, `AssetDatabase.SaveAssets`를 사용한다.
- 사용자 Scene, Prefab, ScriptableObject는 명시적 버튼을 누르지 않는 한 변경하지 않는다.
- 런타임 코드와 데이터베이스 API는 이번 작업에서 변경하지 않는다.

## 검증 전략

- ID 형식, 중복, 외부 참조, 카탈로그 누락, 시나리오 직접 참조를 Editor 단위 테스트로 검증한다.
- 기존 메뉴를 실행해 현재 프로젝트의 Error 수가 0인지 확인한다. 선택적 Sprite 경고는 허용한다.
- 전체 EditMode 테스트와 TestMap PlayMode 회귀를 실행한다.
- Prefab Missing Script 검사와 `git diff --check`를 다시 수행한다.
