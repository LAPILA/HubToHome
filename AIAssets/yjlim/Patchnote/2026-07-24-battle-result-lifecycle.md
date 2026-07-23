# 전투 결과 UI 수명주기 안정화

## 변경 내용

- `BattleResultUI.Show`가 완료·중단·폐기되는 모든 경로에서 같은 정리 함수를 실행한다.
- 결과 UI가 남긴 CanvasGroup 페이드 Tween을 즉시 종료한다.
- 알파, 상호작용, Raycast 차단 상태를 숨김 기본값으로 복구한다.
- 비활성화·파괴 시에도 같은 정리 경로를 사용한다.
- 글로벌 결과 UI가 파괴되면 정적 인스턴스 참조를 해제한다.
- 기존 표시 시간, 보상 계산, 레벨업 계산, 화면 문구는 변경하지 않았다.

## 검증

- 결과 UI 코루틴 강제 폐기: 1/1 통과
- 보상·레벨업 계산: 5/5 통과
- TestMap 전투 조우: 6/6 통과
- 전체 Unity EditMode: 838/838 통과
- Project Content Validation: 오류 0건, 기존 선택 아트 경고 10건
- `Assets/_Game` Prefab 59개, 하위 오브젝트 740개: Missing Script 0건
- `TestMap.unity` SHA256:
  `D456DEC931BA4C14E101A031B07880391958B0E9B65A84DE1E88F61ED1340164`

## 추적

- Jira: `HUBTOHOME-90`
