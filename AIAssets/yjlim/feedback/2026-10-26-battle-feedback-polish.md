# 전투 피드백 후속 정리

브랜치 `gameplayEdit` · 커밋/푸시 없음

## 바뀐 화면과 조작

| 항목 | 현재 동작 |
| --- | --- |
| 턴 안내 | 상단의 ‘누구의 턴’ 글자만 제거. 파티 강조·턴 큐·입력 단계는 그대로 |
| ZEV 표시 이름 | 실험실 EnemyName과 입구 제목을 `ZEV`로 통일. 내부 ID/시나리오 Source/저장은 변경하지 않음 |
| 난무 속도 | 지상0.60→0.34 / 공중0.34→0.30 / 혼합0.30→0.24초. 후반 기준 시간을 이전0.08초보다3배 늘림 |
| 난무 흐름 | 끊김 없는 이동·타수10/2/8·마지막 C·탈진1턴 유지. 사용자는 탈진이 아니라 표시 문구만 제거하는 것으로 확인 |
| 아군 QTE 성공 | 현재 키/링을 흰색으로20ms 점멸하고 나머지80ms 동안 사라짐. HIT/MISS/FAIL 결과 글자는 없음 |
| 아군 QTE 실패·오입력 | 현재 키/링을 빨강으로 바꾸고4px 픽셀 정렬 흔들림과 함께0.16초에 사라짐 |

난무의 실제 접점 시각은 위치에 따라 달라집니다. 연속 모드에서는 일반 모드의 판정 대기값으로 이동을 잘라서는 안 됩니다. 이를 실제 경로 검사에서 재현해 별도 보정했습니다. 공격 후 이동은 기존 늦은 입력 유예를 보존합니다. 난이도 체감은 실제 Play에서 추가 확인이 필요합니다.

## 실루엣 생명주기 오류

`BattleSkillAccentSpriteView.Bind`는 원본 크기·회전·비균등 배율을 정확히 복제하려 배우의 자식으로 뷰를 붙입니다. 기존 `Unbind`는 배우가 비활성화되는 도중 다시 부모를 바꿔 Unity의 활성 상태 전환 제한을 위반했습니다.

이제 숨길 때는 원본 참조·렌더·활성만 해제하고 부모는 그대로 둡니다. 다음 정상 `Bind`에서만 부모를 변경합니다. 배우가 먼저 삭제되면 다음 표시에 새 뷰를 만들고, 표시 소유자가 먼저 삭제되면 휴면 뷰도 명시적으로 삭제합니다. 실제 크기·음수 배율·스프라이트 프레임 동기화는 유지합니다.

## QTE 수명과 입력

- 다음 프롬프트/취소/비활성화가 이전 트윈과 세대 토큰을 무효화합니다. 이전 완료 콜백이 새 키를 숨기지 않습니다.
- 정상 스킬 종료는 마지막 피드백을 끝까지 재생합니다. 전투 취소·모듈 종료는 즉시 숨깁니다.
- 리듬형 QTE도 오입력을 즉시 실패로 처리합니다. 성공·실패의 기존 피해 배율은 변경하지 않습니다.
- Sequence 결과 대기는 기존0.35초 대신 성공0.10/실패0.16초입니다. 적 방어 결과 UI는 별도 경로이므로 기존 규칙을 유지합니다.

## 변경 위치

- `UI/Runtime/BattleUIController.cs`: 턴 문구 제거·QTE 정상 종료 API.
- `UI/Runtime/DefenseQTEUI.cs`, `QTEManager.RhythmSkill.cs`, `QTEManager.cs`의 Sequence 루틴: 결과 피드백·오입력·정리.
- `UI/Runtime/BattleSkillAccentSpriteView.cs`, `BattleSkillAccentPresentation.cs`: 비활성화/삭제 중 부모 변경 제거.
- `Battle/Data/SkillContext.EnemyMeleePresentation.cs`: 연속 이동을 일반 모드 판정 시간으로 자르지 않음.
- Lab의 `Zev_ChainFrenzy.asset`, `ZEV_Lab.asset`, `BunnySlimeBattleLab.asset`: 속도/표시 이름만 조정.
- `BattleHudPresentationTests`, `DefenseQTEUIPresentationTests`, `PresentationAccentTests`, `BattleEnemyMeleePresentationTests`: 관련 회귀.

## 검증과 한계

전체CLI Runtime482/Editor389 오류0/0(기존경고229/0). 실제 생산 코드 관리형 검사: 실루엣18,429단언+14소스계약, QTE피드백16단언+3계약, 이동39사례212단언+4계약, Lab데이터225단언, 전체스킬799단언, 기존사슬/SourceHash178단언, 방어/QTE144사례·재준비479+8계약 통과. 보호 원본11파일 해시와 시나리오 SourceHash는 동일합니다.

Native NUnit은 추가/컴파일했지만 실행하지 않았습니다. 관리형 검사는 Unity/TMP/DOTween/입력 경계에 대역을 씁니다. **Unity Play의 실제 활성화 순서·흔들림·점멸·입력·난이도 체감은 미검증**입니다. Refresh/씬 저장/세이브 I/O/외부 플러그인 수정은 하지 않았습니다.

하네스 교훈: `OnDisable` 정리에서 `SetParent`를 호출하는 것은 유니티 활성 상태 전환 중에는 실패할 수 있습니다. 단순 Hide 검사뿐 아니라 부모의 전환 잠금이 걸린 상태로 실제 Unbind를 호출해 금지 작업을 검출해야 합니다. 다음 Bind까지 휴면 상태를 유지하는 수명 분리를 검사합니다.
