# Sequence Maker Rule Deletion UX

## Problem

Action Sequence 삭제가 규칙 참조 때문에 차단될 때 사용자는 해당 규칙으로 이동해도 삭제 명령을 찾기 어렵다.

- 확장 Trigger Rule 삭제는 왼쪽 목록의 작은 `×`에만 있다.
- 선택한 규칙 편집 화면에는 삭제 명령이 없다.
- legacy Battle Rule은 삭제 명령 자체가 없다.
- 따라서 참조 차단은 정확하지만 사용자가 차단 원인을 해결할 수 없는 막다른 흐름이 된다.

## Decision

확장 Trigger Rule과 legacy Battle Rule 편집 화면 마지막에 동일한 `위험 작업` 영역을 표시한다.

- 버튼 이름은 `규칙 삭제`다.
- 삭제 전에 규칙 이름/ID와 실행 대상 Sequence를 보여준다.
- 확인창은 `규칙 삭제 / 취소` 두 버튼만 표시한다.
- 삭제는 `BattleScenarioEditCommandStack` 명령으로 실행해 Undo/Redo와 dirty/recovery/save 흐름을 유지한다.
- Trigger Rule은 안정적인 Rule ID로 삭제한다.
- legacy Rule은 현재 index의 값을 복사해 제거하고 Undo 시 정확한 원래 index에 복구한다.
- 삭제 후 가능하면 방금 규칙이 참조하던 Action Sequence를 선택한다. 대상 Sequence가 없으면 가장 가까운 규칙 또는 Battle 개요로 이동한다.
- 왼쪽 목록의 기존 `×` 삭제도 같은 Window 명령을 사용한다.

## UX

```text
규칙 편집 내용
...
------------------------------
위험 작업
이 규칙을 삭제하면 더 이상 <Sequence>를 실행하지 않습니다.
[ 규칙 삭제 ]
```

삭제는 Sequence나 다른 규칙을 연쇄 삭제하지 않는다. Rule만 제거한다.

## Verification

- Trigger Rule 삭제/Undo가 원래 index와 데이터를 복구한다.
- legacy Rule 삭제/Undo가 원래 index와 데이터를 복구한다.
- 두 규칙 편집 화면에 `규칙 삭제` 버튼이 보인다.
- 취소 시 아무 상태도 바뀌지 않는다.
- 삭제 성공 후 참조하던 Sequence가 선택되고 Battle 문서가 dirty가 된다.
- 전체 EditMode 회귀 테스트와 실제 Window 픽셀을 확인한다.
