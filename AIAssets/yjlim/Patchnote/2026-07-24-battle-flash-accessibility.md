# 전투 시각 접근성 적용

## 변경 내용

- 전투 전용 Scene 진입의 흰색 전환을 저장된 점멸 강도에 연결했다.
  - 강도 100%는 기존 흰색 전환을 유지한다.
  - 강도 0%는 불투명 검은 전환으로 장면 가림은 보존하고 번쩍임만 제거한다.
- 플레이어의 패링·피격·사망 색 변화와 피격 흔들림이 점멸·흔들림 설정을 따른다.
- 적 피격 색 변화와 흔들림도 같은 설정을 따른다.
- 피격 색 Tween이 완료되거나 중단될 때 SpriteRenderer 색을 흰색으로 복구한다.
- `VisualAccessibilityPolicy`가 강도 정규화와 안전색-연출색 혼합 규칙을 한곳에서 관리한다.
- 공급자 인터페이스를 유지해 런타임 설정과 테스트 대역을 분리했다.

## 검증

- 접근성 정책: 19/19 통과
- SceneLoader: 5/5 통과
- 캐릭터 시각 접근성: 2/2 통과
- BattleUIController 접근성: 2/2 통과
- 카메라 프레젠테이션: 8/8 통과
- TestMap 전투 조우: 6/6 통과
- 전체 Unity EditMode: 835/835 통과
- Project Content Validation: 오류 0건, 기존 선택 아트 경고 10건
- `Assets/_Game` Prefab 59개, 하위 오브젝트 740개: Missing Script 0건
- `TestMap.unity` SHA256:
  `D456DEC931BA4C14E101A031B07880391958B0E9B65A84DE1E88F61ED1340164`

## 추적

- Jira: `HUBTOHOME-90`
