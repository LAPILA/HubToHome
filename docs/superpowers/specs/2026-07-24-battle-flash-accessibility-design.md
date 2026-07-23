# Battle Flash Accessibility Design

## Goal

`HUBTOHOME-90`의 시각 접근성 설정을 전투 진입과 피격 연출까지 확장한다. 강도 100%에서는 기존 연출을 유지하고, 0%에서는 밝은 점멸과 흔들림을 제거하되 전환 시간과 전투 판정은 바꾸지 않는다.

## Approach

- `VisualAccessibilityPolicy`가 기준색과 연출색을 설정 강도로 혼합한다.
- `SceneLoader`의 전투 진입 전환은 흰색을 검정색 쪽으로 혼합한다. 화면은 항상 불투명하게 유지해 씬 로딩을 노출하지 않는다.
- `PlayerController`는 피격·패링·사망 색상을 흰색 기준으로 혼합한다.
- `CharacterBase`는 실제 전투 아군 `PlayerCharacter`와 `EnemyCharacter`가 공유하는 피격 색상·흔들림 배율을 제공한다.
- `OverworldEnemy`의 즉시 처치 점멸은 원래 SpriteRenderer 색을 안전색으로 사용한다.
- 각 컴포넌트는 공급자 인터페이스를 주입받아 설정 싱글톤 없이 테스트할 수 있다.

## Compatibility

- 기존 직렬화 필드와 Prefab 참조를 변경하지 않는다.
- DOTween 재생 시간과 루프 횟수는 유지한다.
- 접근성 설정이 없거나 공급자가 잘못된 값을 반환하면 기존 강도 100%를 사용한다.
- 사용자 작업 중인 Scene과 Prefab은 수정하지 않는다.

## Verification

- 색 혼합 정책의 0%, 50%, 100% 결과
- 전투 전환색이 점멸 설정을 따르는지 확인
- 플레이어와 적의 피격색·흔들림 배율 적용
- 즉시 처치 점멸이 0%에서 원래 색을 유지하는지 확인
- 관련 집중 테스트와 전체 EditMode
- Content Validation, Missing Script, TestMap 해시
