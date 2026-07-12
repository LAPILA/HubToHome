# Sequence Maker Safe Sequence Deletion

## Goal

Sequence Maker에서 선택한 Action Sequence를 사람이 안전하게 완전 삭제할 수 있게 한다.

## Decision

참조 차단형 완전 삭제를 사용한다.

- 삭제 명령은 Sequence Inspector 아래의 위험 영역에 둔다.
- `SequenceUsageIndex`가 발견한 Trigger Rule, legacy rule, 다른 `sequence.call` 참조가 하나라도 있으면 삭제를 차단한다.
- Battle Scenario 소유 관계는 삭제 대상 자체의 소유권이므로 차단 참조에서 제외한다.
- 차단 시 사용 위치와 해당 위치로 이동할 수 있는 기존 navigator 정보를 보여준다.
- 자동 연쇄 삭제는 하지 않는다.

## Deletion Transaction

### Battle-owned Sequence

1. 현재 Battle Scenario와 선택 Sequence를 확인한다.
2. ownership을 제외한 참조가 0건인지 검사한다.
3. 사용자에게 Sequence ID, source path, 되돌릴 수 없는 범위를 확인받는다.
4. 현재 Battle Scenario recovery snapshot을 만든다.
5. Sequence를 Battle Scenario의 `Sequences` 목록에서 제거한다.
6. 전체 Battle Scenario를 export, validation, temporary round-trip, conflict check 후 YAML에 원자적으로 저장한다.
7. 저장 실패 시 목록 변경을 복원하고 Runtime Asset을 유지한다.
8. 저장 성공 뒤 persisted sub-asset이면 AssetDatabase에서 제거한다.
9. index, usage, validation, navigator와 현재 선택을 새로 읽는다.

### Standalone Sequence

1. ownership을 포함한 모든 참조가 0건인지 검사한다.
2. 사용자에게 source YAML과 Runtime Asset 경로를 표시하고 재확인한다.
3. recovery snapshot을 만든다.
4. 현재 Sequence를 export/round-trip validation한다.
5. source hash가 disk YAML과 일치하는지 확인한다. 외부 변경이 있으면 삭제를 차단한다.
6. Runtime Asset과 source YAML을 삭제한다.
7. index와 navigator를 새로 읽고 무대상 상태로 돌아간다.

## Failure Rules

- 참조가 있으면 아무 파일도 변경하지 않는다.
- validation 또는 source conflict가 있으면 아무 파일도 삭제하지 않는다.
- Battle YAML 저장 실패 시 in-memory ownership을 원래 위치로 복원한다.
- Runtime Asset 제거 실패는 명시적 error와 recovery 위치를 남긴다.
- 자동으로 Trigger Rule, legacy rule, sequence.call을 수정하거나 삭제하지 않는다.

## UX

- Sequence Inspector 마지막에 구분된 `위험 작업` 영역을 둔다.
- 버튼 이름은 `시퀀스 삭제`다.
- 참조가 있으면 버튼을 비활성화하고 `N곳에서 사용 중이라 삭제할 수 없음`을 표시한다.
- 확인 대화상자의 기본 버튼은 `취소`, 위험 버튼은 `완전 삭제`다.
- 완료 후 상태 표시줄에 삭제한 Sequence ID와 결과를 표시한다.

## Tests

- usage ownership만 있는 Battle Sequence는 삭제 가능하다.
- Trigger Rule, legacy rule, sequence.call 참조는 각각 삭제를 차단한다.
- Battle YAML 저장 실패는 ownership과 asset을 복원한다.
- 성공한 Battle 삭제는 YAML과 Runtime sub-asset에서 제거한다.
- standalone source conflict는 삭제를 차단한다.
- standalone 성공은 YAML과 Runtime Asset을 제거한다.
- 취소는 아무 상태도 변경하지 않는다.
- 삭제 뒤 index, selection, dirty, recovery 상태가 일관된다.
