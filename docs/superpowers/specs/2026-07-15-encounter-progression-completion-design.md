# Encounter And Progression Completion Design

## Goal

오버월드의 필드 공격부터 전투 진입, 승리 보상, 인벤토리 소비, 저장 복구까지 하나의 실패 안전한 게임 루프로 완성한다. 기존 `BattleManager`, Action Sequence, Encounter Memory 계약은 유지하고, 비어 있던 연결부를 작은 정책과 서비스로 보강한다.

## Chosen Approach

기존 시스템을 대체하지 않고 다음 경계만 추가한다.

- `BattleEncounterService`는 조우 요청 검증, 런타임 컨텍스트 준비, 전환 실패 롤백을 소유한다.
- `FieldEncounterPolicy`는 일반 선공과 즉시처치를 순수 데이터로 판정한다.
- `BattleRewardService`는 적 보상 집계와 단 한 번의 지급을 담당한다.
- `CharacterProgressionService`는 경험치 요구량, 다중 레벨업, 성장 수치 반영을 담당한다.
- `GameContentCatalog`는 빌드에서도 캐릭터와 아이템을 안정적으로 ID 조회하게 한다.
- `ContentValidationWindow`는 누락 ID, 중복 ID, 누락 프리팹과 드롭 참조를 한 화면에서 검사한다.
- `SeamlessBattleHost`는 기획자가 Room Prefab에 복사 배치할 수 있는 전투 런타임 묶음이다.

대안으로 모든 책임을 `BattleManager`에 직접 추가하거나 Addressables로 전면 전환하는 방법이 있지만, 전자는 현재 1,800줄 클래스의 결합을 키우고 후자는 프로젝트 전체 자산 이관이 필요하므로 이번 범위에서는 채택하지 않는다.

## Encounter Flow

1. 플레이어가 기존 `GameInput.PreemptiveAttackPressed`의 F 필드 공격을 실행한다.
2. 대상은 Encounter Memory, 파티 최고 레벨, 적 Threat Level을 기준으로 `PreemptiveBattle` 또는 `InstantVictory`를 판정한다. 즉시처치는 EnemyData와 해당 조우가 모두 명시적으로 허용해야 한다.
3. 즉시처치는 전투 씬을 열지 않고 일반 승리와 같은 보상 및 처치 기억 서비스를 사용한다.
4. 전투 진입은 먼저 심리스 호스트의 준비 상태를 확인한다. 사용할 수 없으면 명시적으로 전용 씬을 사용하며, 두 경로 모두 시작할 수 없으면 플레이어 상태와 Pending 데이터를 복구한다.
5. 선공 정보는 첫 Turn QTE 턴 계산에서 한 번만 소비하여 살아 있는 플레이어를 첫 행동자로 보장한다.

## Reward And Inventory Flow

- 승리한 적의 EXP와 Gold를 합산하고 Drop Item ID를 수량으로 집계한다.
- 보상 지급은 전투당 한 번만 허용한다.
- EXP는 저장된 모든 파티원에게 지급한다. 레벨업은 `CharacterData`의 경험치 곡선과 스탯 성장값을 사용한다.
- 전투 아이템 목록은 실제 인벤토리와 Content Catalog에서 구성한다.
- 아이템은 대상과 효과가 유효한 경우에만 한 개 소비하고, 광역 효과도 행동당 한 개만 소비한다.
- 결과 UI는 EXP, Gold, 실제 인벤토리에 추가된 드롭 수량, 레벨업을 표시하고 실시간 안전 타임아웃 후 닫힌다. 모든 텍스트는 TMP와 Content Catalog의 Silver SDF 폰트를 사용한다.

## Data And Save Compatibility

- `CharacterData.BattlePrefab`을 우선 사용하고 기존 `_playerBasePrefab`은 호환 fallback으로 유지한다.
- 새 직렬화 필드는 기본값을 가지며 기존 저장 파일의 null 컬렉션을 허용한다.
- 오버월드 적의 영구 처치 상태는 SaveData에 저장한다. 런타임 cooldown 시간은 저장하지 않는다. 파티와 장착 스킬 목록은 깊은 복사하여 저장 스냅샷과 런타임 상태가 서로 오염되지 않게 한다.
- 빈 ID는 에디터에서 안정 ID를 제안하고 생성할 수 있다. 중복 ID는 자동 변경하지 않고 오류로 표시해 외부 참조 파손을 막는다.

## Error Handling

- 잘못된 씬, 사용 중인 SceneLoader, 준비되지 않은 심리스 호스트는 성공으로 보고하지 않는다.
- 비동기 씬 로드 실패 시 Pending 전투 데이터, Encounter Context, Player Battle Mode, Game State를 되돌린다.
- 누락 Item ID는 보상 지급에서 건너뛰고 명확한 경고와 Content Validation 오류를 남긴다.
- 전투 프리팹에 필수 컴포넌트가 없으면 조우 시작 전에 거부한다.

## Verification

- 정책, 성장, 보상, JSON 저장 호환, 아이템 소비, 선공 턴 순서를 EditMode 테스트한다.
- 생성된 Content Catalog와 SeamlessBattleHost 프리팹을 에디터 자산 테스트로 검사한다.
- TestMap에서 심리스 호스트 구성과 전투 진입 실패 롤백을 EnterPlayMode 통합 테스트한다.
- Runtime MSBuild, Unity Content Validation, Unity Test Framework 전체 회귀를 실행한다.
