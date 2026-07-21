# Encounter And Progression Completion Implementation Plan

> **For agentic workers:** REQUIRED: Use superpowers:subagent-driven-development (if subagents available) or superpowers:executing-plans to implement this plan. Steps use checkbox (`- [x]`) syntax for tracking.

**Goal:** 필드 조우부터 전투 보상과 저장까지 실제 플레이 가능한 하나의 안정된 루프로 완성한다.

**Architecture:** 기존 조우와 전투 진입점은 유지하고 순수 정책, 보상 서비스, 콘텐츠 카탈로그를 추가한다. Unity 직렬화 자산은 전용 Editor Builder로 생성하고 테스트로 필수 참조를 고정한다.

**Tech Stack:** Unity 6, C#, Unity Input System, Unity Test Framework, TextMeshPro, DOTween, Odin Inspector

---

## Chunk 1: Encounter Safety And Field Attack

### Task 1: Encounter start result and rollback

- [x] 조우 시작 실패 테스트를 추가한다.
- [x] 전용 씬 로드 결과를 받아 실패 시 Pending 데이터와 플레이어 상태를 복구한다.
- [x] 심리스 호스트 준비 상태와 중복 시작을 검증한다.
- [x] Runtime/Editor 빌드를 실행한다.

### Task 2: Preemptive and instant victory policy

- [x] `FieldEncounterPolicy`의 경계값 테스트를 추가한다.
- [x] EnemyData에 Threat Level과 즉시처치 설정을 추가한다.
- [x] 기존 `GameInput.PreemptiveAttackPressed`의 F 입력을 선공/즉시처치 정책에 연결한다.
- [x] 선공을 첫 턴에서 한 번만 소비한다.
- [x] 즉시처치를 일반 승리 정산과 Encounter Memory에 연결한다.

## Chunk 2: Progression, Rewards, And Inventory

### Task 3: Character progression

- [x] 다중 레벨업과 최대 레벨 테스트를 추가한다.
- [x] CharacterData 성장 설정과 CharacterProgressionService를 구현한다.
- [x] 성장 스탯 전체를 SaveData에 동기화한다.

### Task 4: Battle rewards

- [x] EXP/Gold/Drop 집계와 중복 지급 방지 테스트를 추가한다.
- [x] BattleRewardService를 구현하고 일반 승리와 즉시처치에서 공유한다.
- [x] BattleResultUI를 연결하고 실시간 안전 타임아웃 후 전투 흐름을 복구한다.

### Task 5: Inventory consumption

- [x] 실제 인벤토리 메뉴 구성과 유효 사용 시 한 번 소비 테스트를 추가한다.
- [x] ItemEffectService로 오버월드와 전투 효과 적용을 통합한다.
- [x] BattleMenuUI의 샘플 아이템 의존을 제거한다.

## Chunk 3: Content Data And Prefabs

### Task 6: Runtime content catalog

- [x] GameContentCatalog와 CharacterDatabase/EnemyDatabase/SkillDatabase/ItemDatabase를 구현하고 프로젝트 콘텐츠 검사로 조회 대상을 검증한다.
- [x] CharacterData별 BattlePrefab 해석을 구현한다.
- [x] 전용 씬과 심리스 전투의 파티 생성을 캐릭터별 프리팹으로 변경한다.

### Task 7: Content validation tools

- [x] ID 누락/중복/참조 누락 프로젝트 검사를 추가한다.
- [x] ContentValidationWindow와 빈 ID 생성 명령을 구현한다.
- [x] 현재 Enemy/Skill 자산 ID를 충돌 없이 정리한다.

### Task 8: SeamlessBattleHost and sample content

- [x] SeamlessBattleHost 필수 참조 검사를 구현한다.
- [x] Editor Builder로 재사용 프리팹을 생성하고, 런타임 결과 UI·기본 아이템·Content Catalog를 연결한다.
- [x] TestMap에 심리스 호스트와 검증용 조우를 배치한다.

## Chunk 4: End-To-End Verification

### Task 9: Save and PlayMode regression

- [x] 오버월드 적 영구 처치 저장/복구 테스트를 추가한다.
- [x] TestMap PlayMode 조우 루프 테스트를 추가한다.
- [x] TestMap EnterPlayMode 통합 테스트, 전체 EditMode 테스트, Unity 컴파일과 MSBuild를 실행한다.
- [x] AIAssets 업데이트 및 사용자용 배치 설명을 갱신한다.

## Completion

- Unity Content Validation: 문제 0건.
- 대상 회귀 테스트: 17/17 통과.
- Sequence Maker 경로 정책 테스트: 44/44 통과.
- TestMap EnterPlayMode 통합 테스트: 2/2 통과, `Temp/__Backupscenes` 잔여물 0건.
- 전체 Unity EditMode: 627/627 통과.
- `dotnet build Assembly-CSharp.csproj`: 오류 0개, 기존 경고 3개.