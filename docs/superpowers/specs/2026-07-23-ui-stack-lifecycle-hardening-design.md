# UI Stack Lifecycle Hardening Design

## Goal

`HUBTOHOME-90`의 첫 안정화 단위로 현재 `UIManager`와 `UIPanel` API를 유지하면서 패널 중복, 파괴된 참조, 입력 포커스 유실, 씬 전환 뒤 잔존 UI와 tween을 제거한다.

## Decision

전체 UI 프레임워크 교체나 Scene/Prefab 재구성은 하지 않는다. `UIManager`가 패널 스택과 이전 선택 대상을 함께 관리하고, 모든 정리 경로가 같은 내부 함수로 수렴하도록 보강한다.

## Runtime Contract

- 같은 패널을 다시 열면 스택에 중복 삽입하지 않고 최상단으로 이동한다.
- 패널을 닫으면 열기 직전 선택 대상이 아직 유효할 때 복원한다.
- `UIPanel`은 선택형 기본 선택 대상을 제공하며, 지정되지 않은 기존 Prefab은 현재 동작을 유지한다.
- 파괴된 패널은 조회, 닫기, 씬 전환 시 스택과 등록소에서 제거된다.
- Scene 언로드 시 이전 스택의 유효한 패널은 즉시 숨기고 잔존 참조를 정리한다.
- `UIPanel`의 Fade tween은 비활성화 또는 파괴 시 완료 콜백 없이 정리된다.
- 상태를 소유하는 설정 패널과 오버월드 메뉴는 즉시 닫기에서도 열기 전 상태를 복원한다.

## Compatibility

- 기존 `RegisterPanel`, `OpenPanel`, `CloseTopPanel`, `CloseAllPanels` 호출부를 유지한다.
- 기존 직렬화 필드 이름과 Prefab 계층은 변경하지 않는다.
- 기본 선택 대상은 비어 있어도 안전한 새 선택형 참조다.
- Scene과 Prefab 자산은 이번 단위에서 수정하지 않는다.

## Verification

- 패널 재열기 중복 방지
- 파괴된 최상단 패널 자동 제거
- 등록 해제 시 스택 정리
- 이전 EventSystem 선택 복원
- 비활성화 시 Fade tween 정리
- 전체 EditMode, Content Validation, Missing Script 검사
