# Current Resource Ownership Design

## Goal

`CurrentHP`와 `CurrentAP`를 레이어 계산용 `StatBlock`에서 제거하고, 기존처럼 각 전투 대상 인스턴스가 런타임 자원 상태를 직접 소유하도록 정리한다.

## Decision

- `StatBlock`은 `MaxHP`, `MaxAP`, `ATK`, `DEF`, `SPD`, 저항, 피해 배율처럼 기본·성장·장비·전투 레이어 계산의 대상이 되는 값만 보유한다.
- `CharacterBase`는 기존 `CurrentHP`, `CurrentAP` 런타임 값을 보유하고 피해·회복·소모·사망 이벤트를 처리한다.
- `CharacterSaveData.HP/AP`는 플레이어의 영속 상태로 계속 사용한다.
- 별도의 `ResourceState` 객체는 만들지 않는다.
- 최대 HP/AP가 바뀌어 스탯을 재계산할 때 현재 HP/AP는 `CharacterBase`에서 새 최대값에 맞춰 clamp한다.
- `ICharacterStatsReader`는 계산 스탯 조회 계약으로 한정하고 현재 자원 조회는 `CharacterBase.CurrentHP/AP` 및 기존 UI/전투 API를 사용한다.

## Impact

- `CharacterStatsCalculator.Resolve`는 현재 HP/AP를 인자로 받지 않는다.
- `CharacterStats`는 현재 자원을 저장·변경하지 않는다.
- `CharacterStatsProjectionService`는 계산 스탯과 세이브 HP/AP를 분리해서 투영한다.
- ScriptableObject의 `BaseStats`에는 현재 HP/AP를 저장하지 않는다.

## Verification

- 레이어 계산이 현재 HP/AP를 변경하지 않는지 테스트한다.
- `CharacterBase`의 피해·회복·AP 소비·세이브 복원이 기존 동작을 유지하는지 테스트한다.
- 기존 집중 EditMode 테스트와 정적 컴파일 진단을 실행한다.
