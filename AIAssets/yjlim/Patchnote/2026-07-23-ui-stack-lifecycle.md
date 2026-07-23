# HUBTOHOME-90 UI 스택 수명주기 안정화

## 변경 사항

- `UIManager` 패널 스택에 패널별 이전 선택 오브젝트를 보관하고 닫을 때 포커스를 복원한다.
- 같은 패널을 다시 열면 스택 항목을 중복 생성하지 않고 최상단으로 이동한다.
- 파괴되거나 등록 해제된 패널 참조를 스택과 등록 목록에서 즉시 제거한다.
- 씬 언로드 시 열린 패널을 애니메이션 없이 정리하는 `CloseAllPanelsImmediate` 경로를 추가했다.
- `UIPanel`이 비활성화되거나 파괴될 때 자신이 만든 fade tween을 종료한다.
- 설정 패널은 열기 전 `timeScale`과 게임 상태를 보관하고 일반 닫기, 즉시 닫기, 외부 비활성화에서 동일하게 복원한다.
- 오버월드 메뉴는 자신이 획득한 일시정지 상태만 복원하고 진행 중인 메뉴 및 카테고리 tween을 정리한다.
- 기본 선택 오브젝트를 Inspector에서 지정할 수 있도록 `UIPanel` 포커스 진입점을 추가했다.

## 검증

- `UIManagerStackTests`: 7/7
- `DefenseQTEUIPresentationTests`: 2/2
- Unity 전체 EditMode: 811/811
- Project Content Validation: 오류 0건, 기존 선택 아트 경고 10건
- `Assets/_Game` Prefab 59개, 하위 오브젝트 740개: Missing Script 0건
- 사용자 수정 중인 `TestMap.unity` 해시 유지

## 테스트 하네스 주의점

- Unity 6 EditMode에서는 테스트 중 새로 만든 `EventSystem`이 항상 `EventSystem.current`로 등록되지 않는다.
- `EventSystem.current = null` 할당은 오류 로그를 남기므로 테스트 격리에 사용하지 않는다.
- UI 포커스 테스트는 `UIManager.ResolveEventSystem()` 재정의 지점을 사용해 명시적으로 EventSystem을 주입한다.

## 후속 범위

- 결과, 레벨업, 메뉴, 설정, 접근성 UI의 실제 화면 구성은 각 기능별 데이터와 화면 명세가 확정된 순서대로 이어서 구현한다.
- 신규 패널은 기본 선택 오브젝트와 즉시 닫기 시 해제해야 하는 상태 소유권을 함께 정의한다.
