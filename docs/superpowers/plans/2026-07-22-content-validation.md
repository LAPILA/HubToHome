# Content Validation Implementation Plan

> **For agentic workers:** REQUIRED: Use superpowers:subagent-driven-development (if subagents available) or superpowers:executing-plans to implement this plan. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 캐릭터·적·스킬·아이템·시나리오의 ID와 필수 참조를 구조적으로 검사하고 오류 자산을 즉시 선택할 수 있는 Editor 도구를 완성한다.

**Architecture:** Unity `AssetDatabase` 접근은 `AssetDatabaseContentSource`에 격리하고, `ProjectContentValidator`는 입력 자산을 `ContentValidationReport`로 변환한다. 기존 `ContentValidationWindow`는 보고서 표시와 명시적 복구 명령을 담당하며 런타임 데이터베이스는 변경하지 않는다.

**Tech Stack:** Unity 6, C#, UnityEditor, NUnit EditMode, Odin Inspector 데이터 모델

---

## Chunk 1: 검증 도메인과 콘텐츠 규칙

### Task 1: 구조화된 보고서 모델

**Files:**
- Create: `Assets/_Game/Scripts/Editor/ContentValidation/ContentValidationIssue.cs`
- Create: `Assets/_Game/Scripts/Editor/ContentValidation/ContentValidationReport.cs`
- Create: `Assets/_Game/Scripts/Editor/Tests/ContentValidationReportTests.cs`

- [ ] 코드, 심각도, 메시지, 선택 대상과 대체 경로를 보존하는 실패 테스트를 작성한다.
- [ ] 대상이 없으면 선택 불가능하고 경로만 표시되는 실패 테스트를 작성한다.
- [ ] 보고서 모델을 최소 구현하고 대상 테스트를 통과시킨다.
- [ ] Error/Warning 집계와 결정적 정렬 실패 테스트를 작성한다.
- [ ] 집계와 정렬을 구현하고 대상 테스트를 통과시킨다.

### Task 2: ID 규칙

**Files:**
- Create: `Assets/_Game/Scripts/Editor/ContentValidation/ContentIdPolicy.cs`
- Create: `Assets/_Game/Scripts/Editor/ContentValidation/ProjectContentSnapshot.cs`
- Create: `Assets/_Game/Scripts/Editor/ContentValidation/ProjectContentValidator.cs`
- Create: `Assets/_Game/Scripts/Editor/Tests/ProjectContentValidatorTests.cs`

- [ ] 정상 ID와 공백·대문자·잘못된 문자를 구분하는 실패 테스트를 작성한다.
- [ ] `ContentIdPolicy`와 네 콘텐츠 종류의 필수/형식 검사를 구현하고 테스트를 통과시킨다.
- [ ] 동일 종류 중복 ID 실패 테스트를 작성한다.
- [ ] 중복 검사를 구현하고 테스트를 통과시킨다.
- [ ] GUID suffix와 예약 ID를 사용하는 생성 ID 실패 테스트를 작성한다.
- [ ] ASCII slug와 충돌 회피 생성을 구현하고 테스트를 통과시킨다.

### Task 3: 캐릭터·적 직접 참조

**Files:**
- Modify: `Assets/_Game/Scripts/Editor/ContentValidation/ProjectContentValidator.cs`
- Modify: `Assets/_Game/Scripts/Editor/Tests/ProjectContentValidatorTests.cs`

- [ ] 잘못된 Character/Enemy 전투 프리팹과 프로젝트 외 Skill 참조 실패 테스트를 작성한다.
- [ ] 프리팹 컴포넌트와 Skill 소속 검사를 구현하고 테스트를 통과시킨다.
- [ ] 구조화 드롭·legacy 드롭의 알 수 없는 Item ID, 수량, 확률 실패 테스트를 작성한다.
- [ ] 드롭 규칙을 구현하고 테스트를 통과시킨다.
- [ ] Portrait/TurnOrderPortrait 누락이 Warning인 실패 테스트를 작성한다.
- [ ] 표현 자산 Warning을 구현하고 테스트를 통과시킨다.

### Task 4: 스킬·아이템 규칙

**Files:**
- Modify: `Assets/_Game/Scripts/Editor/ContentValidation/ProjectContentValidator.cs`
- Modify: `Assets/_Game/Scripts/Editor/Tests/ProjectContentValidatorTests.cs`

- [ ] 스킬 null 블록과 VFX/Projectile 필수 프리팹 누락 실패 테스트를 작성한다.
- [ ] 최소 Skill 블록 검사를 구현하고 테스트를 통과시킨다.
- [ ] 소비 아이템 효과·대상 스탯·상태이상·스택 수치 실패 테스트를 작성한다.
- [ ] Item 규칙을 구현하고 테스트를 통과시킨다.
- [ ] Skill/Item Icon 누락이 Warning인 실패 테스트를 작성한다.
- [ ] Icon Warning을 구현하고 테스트를 통과시킨다.

## Chunk 2: 시나리오와 Runtime Catalog

### Task 5: 시나리오 직접 참조와 기존 검사기 연결

**Files:**
- Modify: `Assets/_Game/Scripts/Editor/ContentValidation/ProjectContentValidator.cs`
- Modify: `Assets/_Game/Scripts/Editor/Tests/ProjectContentValidatorTests.cs`

- [ ] Scenario ID 형식/중복, 빈 참가 ID, 중복 참가 ID 실패 테스트를 작성한다.
- [ ] Scenario identity와 참가 목록 검사를 구현하고 테스트를 통과시킨다.
- [ ] canonical `player`, CharacterID, EnemyID의 정상/알 수 없는 참조 테스트를 작성한다.
- [ ] 참가자 해석을 구현하고 테스트를 통과시킨다.
- [ ] Sequence, Dialogue, Audio의 null과 로컬 ID 중복 실패 테스트를 작성한다.
- [ ] 직접 참조 검사를 구현하고 테스트를 통과시킨다.
- [ ] Action Catalog가 있을 때 기존 `ScenarioCatalogValidator` 오류가 보고서로 변환되는 실패 테스트를 작성한다.
- [ ] `ValidateBattleScenario` 결과 변환만 구현하고 테스트를 통과시킨다. 내부 규칙은 재구현하지 않는다.

### Task 6: Runtime Catalog 일치

**Files:**
- Create: `Assets/_Game/Scripts/Editor/ContentValidation/AssetDatabaseContentSource.cs`
- Modify: `Assets/_Game/Scripts/Editor/ContentValidation/ProjectContentValidator.cs`
- Modify: `Assets/_Game/Scripts/Editor/Tests/ProjectContentValidatorTests.cs`

- [ ] 카탈로그 또는 기본 UI 폰트 누락 실패 테스트를 작성한다.
- [ ] 카탈로그 존재와 Font 검사를 구현하고 테스트를 통과시킨다.
- [ ] 프로젝트 자산 누락, 카탈로그 null/중복 참조 실패 테스트를 작성한다.
- [ ] 카탈로그 집합 일치 검사를 구현하고 테스트를 통과시킨다.
- [ ] AssetDatabase source가 모든 지원 자산과 Action Catalog를 경로 순으로 읽는 테스트를 작성한다.
- [ ] 읽기 전용 Snapshot 생성을 구현하고 테스트를 통과시킨다.

## Chunk 3: Editor UX와 안전한 수정 명령

### Task 7: 기존 창을 구조화 보고서에 연결

**Files:**
- Modify: `Assets/_Game/Scripts/Editor/ContentValidationWindow.cs`
- Create: `Assets/_Game/Scripts/Editor/Tests/ContentValidationWindowTests.cs`

- [ ] Scan 전후 자산 상태가 같은 실패 테스트를 작성한다.
- [ ] 창의 Scan을 읽기 전용 Snapshot/Validator 호출로 교체하고 테스트를 통과시킨다.
- [ ] 대상 없는 오류는 선택할 수 없고 대상 있는 오류는 선택 가능한 UI 정책 테스트를 작성한다.
- [ ] Error/Warning 필터, 검색, 경로 표시, 선택/Ping 행을 구현하고 테스트를 통과시킨다.
- [ ] `Validate Project Content`가 Error만 실패시키고 Warning을 로그만 남기는 테스트를 작성한다.
- [ ] 메뉴 검증 정책을 구현하고 테스트를 통과시킨다.

### Task 8: ID 생성과 기존 복구 명령 보강

**Files:**
- Modify: `Assets/_Game/Scripts/Editor/ContentValidationWindow.cs`
- Modify: `Assets/_Game/Scripts/Editor/Tests/ContentValidationWindowTests.cs`

- [ ] 기존 ID와 충돌하는 생성 후보가 다음 suffix로 이동하는 실패 테스트를 작성한다.
- [ ] `ContentIdPolicy`를 누락 ID 생성에 연결하고 테스트를 통과시킨다.
- [ ] 이미 존재하거나 중복인 ID를 자동 변경하지 않는 회귀 테스트를 작성하고 통과시킨다.
- [ ] 명시적 수정 명령에 `Undo.RecordObject`, `EditorUtility.SetDirty`, `AssetDatabase.SaveAssets`가 적용됐는지 코드 검토한다.
- [ ] 기존 메뉴 경로와 프리팹 링크 복구, 카탈로그 재구축 동작을 유지한다.

## Chunk 4: 통합 검증과 저장소 기록

### Task 9: 프로젝트 검증 및 기록

**Files:**
- Modify: `AIAssets/2026-07-22-update.md`
- Modify: `AIAssets/todo.md`
- Create: `AIAssets/yjlim/feedback/2026-07-22-content-validation.md`
- Modify: `CONTEXT.md`
- Modify: `RuleFileforAI/mainrule.clinerules`

- [ ] Content Validation 대상 테스트를 실행한다.
- [ ] Unity 전체 EditMode 테스트를 실행한다.
- [ ] `Hub To Home/Content/Validate Project Content`를 실행해 Error 0건을 확인한다.
- [ ] TestMap PlayMode 2건을 실행한다.
- [ ] Prefab Missing Script와 `git diff --check`를 확인한다.
- [ ] AIAssets와 리뷰 메모에 검사 범위와 수동 확인점을 기록한다.
- [ ] 저장소 규칙과 사용자 요청에 따라 Jira 상태와 로컬 커밋을 갱신한다.
- [ ] 사용자 작업 파일을 제외하고 명시적 경로만 스테이징하며 원격 push는 하지 않는다.
