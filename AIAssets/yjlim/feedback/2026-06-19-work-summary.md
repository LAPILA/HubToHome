# HubToHome 지금까지 한 것 / 안 한 것 / 더 해야 할 것 정리

> 기준 시각: 2026-06-19 KST  
> 기준 자료: `AIAssets` 기존 문서, `CONTEXT.md`, 최근 Git 로그, yjlim feedback/Patchnote 문서 목록  
> 주의: 이 문서는 코드/에셋을 새로 검증한 결과가 아니라, 저장소에 남은 문서와 커밋 이력을 기준으로 한 인수인계 정리입니다.

## 1. 지금까지 한 것

### 1.1 기본 플레이 루프

- 타이틀 → 인트로 → 이름 입력 → 오버월드 → 대화/전투 진입 흐름이 프로토타입 수준으로 연결됨.
- `TitleMenuManager`, `IntroManager`, `NameInputUI`, `DialogueManager` 중심의 초기 UX 루프가 존재함.
- 설정 버튼은 빈 버튼이 아니라 실제 `ConfigPanelUI` 경로로 연결된 상태로 정리됨.

### 1.2 설정 / 입력 / 현지화 기반

- `GameConfigManager`, `GameInput`, `ConfigPanelUI` 축으로 설정 저장/적용/키 입력 갱신 구조를 잡음.
- 텍스트 속도 설정이 `DialogueUI` 출력과 미리보기 양쪽에 반영되도록 연결됨.
- 남은 fallback 문자열과 세부 옵션 마감은 아직 필요함.

### 1.3 오버월드 조우와 대화 선택지 전투

- `BattleEncounterService`를 통해 오버월드 적 접촉과 대화 선택지 전투가 공통 전투 진입 경로를 사용하기 시작함.
- `OverworldEnemy`는 단순 트리거가 아니라 enemy id, 승리 제거, 도주 쿨다운, 재등장 알파 같은 월드 상태를 다루는 엔티티로 확장됨.
- `DialogueBattleNPC` → `DialogueEncounterContext` → `DialogueManager` → `BattleEncounterService`로 대화 선택지 전투 데이터 전달 경로가 살아 있음.

### 1.4 전투 안정화

- 방어 입력이 실제 방어 창이 열렸을 때만 작동하도록 재조정함.
- 방어 연타 시 anchor가 누적 갱신되어 플레이어가 밀리는 문제를 수정함.
- 방어 성공 시 피해를 0으로 통일하고, 패링 보상은 MP 보상 중심으로 남김.
- 플레이어/적 사망 뒤 `BattleIdle`이 다시 덮는 문제를 생존 체크와 트리거 정리로 완화함.
- 일반 공격 카메라 과축소, BattleScene 첫 프레임 UI/카메라 어긋남, BattleScene 직렬화된 카메라/Canvas 값 문제를 보정함.
- ZEV 계열 스킬에 방어형 telegraph/QTE 패턴과 신규 참격 스킬을 추가한 이력이 있음.

### 1.5 시나리오 파이프라인 / Action Sequence / Game Module

- `Action Sequence`, `Action Director`, `Battle Scenario Data`, `Battle Event Rule`, `Scenario Source YAML`, `Scenario Runtime Asset` 용어와 방향이 `CONTEXT.md`에 정리됨.
- Scenario YAML을 source of truth로 보고, Unity ScriptableObject를 runtime representation으로 동기화하는 방향을 잡음.
- `BattleScenarioRuntime`, `BattleScenarioExecutionGate`, `BattleScenarioActionBridge`, `BattleEventRuleEvaluator` 중심으로 전투 이벤트 규칙 평가와 Action Sequence 실행 게이트를 분리함.
- `BattleSessionState`, `Battle Session Flag`, `Battle Participant Command Runner`, `Battle Participant Snapshot` 등 전투 세션 상태 seam을 추가함.
- `module.switch`, `module.start`, `GameModuleActionRunner`, `GameModuleRegistry`, `IGameModuleRuntime`, `GameModuleRuntimeContext`로 Game Module 전환/실행 seam을 구성함.
- 기존 QTE 전투는 `turn_qte` Game Module로 이관되기 시작했고, BattleManager 내부 controller가 기존 serialized field와 legacy flow를 감싸는 adapter 역할을 함.
- 첫 비-QTE 모듈 `aim_shooter`는 입력/프레젠테이션 소유권 shell과 순수 규칙 코어(`BattleAimShooterCombatSession`)까지 구현됨. 단, 완성된 슈터 gameplay loop는 아님.

### 1.6 ZEV 시나리오 클론 수직 슬라이스

- `ZEV_ArchitectureClone`용 EnemyData, DialogueData, Scenario Source YAML, generated BattleScenario asset, Action Catalog sample을 추가한 이력이 있음.
- 오버월드/대화 진입점에서 per-encounter BattleScenarioData를 넘기는 optional 경로가 생김.
- clone phase transition, dummy module vertical slice, safe reimport 등에 대한 Editor/PlayMode 테스트가 추가된 이력이 있음.
- `ZEV_ArchitectureClone_TestScene`과 캡처 이미지가 만들어진 상태로 보임.

### 1.7 Sequence Maker / 시나리오 에디터 UX

- `ScenarioAuthoringWindow`가 Korean UI Toolkit 기반 `HubToHome/시나리오/시퀀스 메이커` surface로 정리됨.
- 좌측 flow map, 중앙 timeline, 우측 action inspector + YAML/sync tools 형태의 3패널 board 모델로 개편됨.
- Catalog 기반 action picker, validation badge, raw JSON advanced foldout, YAML preview/export, source metadata sync, safe runtime reimport 흐름이 들어간 이력이 있음.
- 시나리오 파이프라인 skill 문서와 reference 문서도 함께 갱신됨.

### 1.8 MapFieldStarter / 오버월드 맵 샘플

- 최근 커밋 `13735f75 Map`에서 `MapFieldStarter` 관련 방/프리팹/씬/README/샘플 빌더가 추가됨.
- `RuleFileforAI/overworld.clinerules`, `RuleFileforAI/codebase-reference.md`도 맵 관련 변경을 받은 것으로 보임.

## 2. 아직 안 한 것 / 미완료

### 2.1 저장 / Continue

- 타이틀 `Continue`는 실제 저장 슬롯 로드와 씬 복구 흐름에 완전히 연결되지 않은 것으로 정리되어 있음.
- `SaveManager`, `SaveData`, `GlobalDataManager`, `TitleMenuManager`를 함께 봐야 함.

### 2.2 오버월드 조우 안정화

- 도주 후 적 collider, 이동 상태, 재조우 쿨다운, 알파/깜빡임, encounter id 정리 상태는 플레이테스트 검증이 더 필요함.
- 승리/도주/패배 각각에서 `CurrentEncounterEnemyId`와 encounter memory가 어떻게 남는지 확인 필요.

### 2.3 설정 시스템 마감

- `ConfigPanelUI` fallback 문자열을 `LocalizationTable.csv` 기준으로 줄이는 작업이 남음.
- Voice 볼륨 옵션 필요 여부와 `AudioManager`/`GameConfigManager` 저장 키 정책을 확정해야 함.
- Config UI와 일반 UI 입력 예외 경로를 더 줄일 필요가 있음.

### 2.4 BattleManager 구조 부채

- `BattleManager`는 아직 상태 전이, 행동 실행, 방어 판정, 종료 처리, legacy QTE 흐름이 크게 몰려 있음.
- `EnemyActionRoutine`, player action 실행부, battle end flow, defense policy를 분리해야 유지보수성이 올라감.
- 기본공격 방어와 스킬 `Action_DefenseWindow` 방어 정책은 더 깊게 공용화해야 함.

### 2.5 전투 카메라 위치 부채

- 전투용 `CameraController`가 TMP 샘플 폴더(`Assets/TextMesh Pro/Examples & Extras/...`)에 있는 구조적 위험이 남아 있음.
- first-party 폴더로 이동할지, 기존 참조 migration을 어떻게 할지 결정 필요.

### 2.6 시나리오 에디터 / YAML sync 실사용 검증

- Sequence Maker는 기능이 많이 붙었지만, 사람이 실제로 여러 sequence를 편집/저장/반영하는 장시간 UX 검증은 별도 필요함.
- safe reimport는 validation-first 방향이지만, 기존 sub-asset reuse/분리/미삭제 정책이 실제 에셋 운용에서 혼란을 만들지 확인해야 함.

### 2.7 Aim Shooter 완성도

- `aim_shooter`는 모듈 shell과 순수 rule core 수준임.
- 마우스 조준, projectile/VFX, module-specific UI, 실제 enemy target selection UX, outcome presentation은 아직 미완료로 보는 게 맞음.

### 2.8 상태이상 / 아이템 구조

- `InventoryManager`의 상태이상 TODO가 남아 있음.
- 문자열 분기 대신 `StatusFactory` 또는 registry 기반으로 통합해야 함.

### 2.9 GameOver / 패배 정책

- 패배 문구 hold와 전환 전 delay는 보정했지만, GameOver UI, 리스폰, 세이브 복구 정책은 아직 기획적으로 닫히지 않음.

## 3. 더 해야 할 것 우선순위

### P0 - 바로 플레이 흐름이 끊기는 것

1. `Continue`를 실제 저장 복구로 연결.
2. 오버월드 적 도주/재조우 상태 안정화.
3. BattleScene/오버월드 전투 왕복 smoke test 정리.

### P1 - 이미 붙은 시스템 마감

1. 설정 패널 현지화와 옵션 정책 확정.
2. BattleManager에서 방어 판정/종료 처리/행동 실행부 분리.
3. 기본공격/스킬 방어 정책 통합.
4. CameraController first-party 이전 계획 수립.

### P2 - 시나리오 파이프라인 실사용화

1. Sequence Maker로 ZEV clone scenario를 열고 편집 → 저장 및 반영 → runtime asset diff 확인.
2. Catalog validation 누락 액션/파라미터를 정리.
3. `aim_shooter`를 실제 input/projectile/VFX/UI loop로 확장하되, `GameModuleRuntimeContext` seam 밖으로 정책이 새지 않게 유지.
4. Scenario YAML source와 generated ScriptableObject가 stale 상태로 갈라지는지 체크하는 반복 검증 추가.

### P3 - 확장 전 정리

1. 상태이상/아이템 적용을 registry로 통합.
2. 선택지 텍스트 현지화 키 기반 전환.
3. MapFieldStarter 샘플을 실제 오버월드 production 구조로 가져갈지, 샘플/프로토타입으로 둘지 결정.

## 4. 지금 작업자가 주의할 점

- 시나리오 YAML / Action Sequence / Battle Scenario Data / Sequence Maker를 건드리면 반드시 `.agents/skills/hubtohome-scenario-authoring/SKILL.md`와 references를 같이 확인해야 함.
- Unity scene/prefab/ScriptableObject는 고위험 파일이므로, 직접 serialized YAML을 고치는 대신 에디터/생성기/명시적 계획을 우선해야 함.
- 현재 worktree에는 `AIAssets/2026-06-14~18-update.md` 삭제가 이미 잡혀 있었음. 이 문서 정리는 그 삭제를 복구하지 않고 yjlim 아래 종합 문서로 대체하는 방식으로 진행함.
- 이 문서 작업은 코드/에셋 기능 변경이 아니므로 Unity 실행 검증은 하지 않음.