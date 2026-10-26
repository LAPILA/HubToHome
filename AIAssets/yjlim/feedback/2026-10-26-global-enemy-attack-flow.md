# 전체 적 공격 — 이동 중 실시간 대응

`gameplayEdit` · 커밋/푸시 없음

현재 모든 적 공격은 **접근·비행·공격 모션 중에 전조와 방어 판정**이 뜹니다. 이전 ‘준비 자세 정지 → 전조 대기 → 공격’과 일부 스킬만 연속 이동하는 정책은 폐기했습니다.

| 공격 | 현재 동작 |
| --- | --- |
| 기본·단일 근접 | 기존 명중 시간 전체에 접근. 가드·패링 성공에도 관통 이동은 유지 |
| 화면 양끝 참격 | 안전 끝점과 기존 명중 시각 유지. 접점에 고정하지 않음 |
| 제브 난무 | 기존 단계별 이동 기준 유지:0.60→0.34→0.30→0.24초 |
| 투사체 | 비행 중 대응. 물리 도착에 시각 투사체 반환, 입력 유예 중 재생성 없음 |
| 다중 대상 연쇄 | 별도 접근 완료 후 전조를 기다리지 않고 같은 이동/방어 scope로 타격. 대상간 고정 후딜 제거 |
| C·광역 | 중앙 배치 유지. 창 시작부터 공격 모션을 진행하고 클립 명중 프레임을 실제 충돌에 맞춤 |

기존 성공 창·입력 유예0.06초·재준비·일반 가드65% 감소·저스트 무피해/AP·X/C 조건·피해1회는 유지합니다. 이야기 대화, 메뉴 일시정지, C 성공 후 반격/복귀와 강공격의 다음 턴 예약 자체는 별도 규칙으로 보존합니다.

## 새 스킬 제작

1. 스킬 메이커에서 **방어 대응 → 피해 / 투사체 / 연쇄 근접**을 작성합니다.
2. 일반 공격의 **명중까지 이동/공격 시간**으로 반응 시간을 조정합니다. 이동 옵션을 꺼 정지 전조로 돌아가는 기능은 없습니다.
3. **공격 시작 → 타격 시간**에는 클립의 실제 명중 프레임 시간을 넣습니다.0은 적 기본값입니다. C/광역은 이 프레임이 실제 충돌에 오도록 양수 속도로 재매핑하며 전역 TimeScale은 바꾸지 않습니다. VFX는 앞당겨진 모션 시작이 아니라 원래 명중 lead에1회 생성합니다.
4. 난무는 단계의 관통 시간을 조정합니다. 구형 판정/Ready·연속모드 토글은 저장 호환용으로 남지만 새 제작 화면에서는 숨깁니다.
5. 적 전투의 순수 Wait/Ready/Windup와 중복 지상 TargetFront/AttackStaging은 기다리지 않습니다. 실제 접근은 방어 scope가 소유합니다. Center/Top/Back/점프 특수 이동, 아군·순수 이야기 타임라인은 유지합니다.
6. 실루엣도 적 공격과 병행합니다. 명중 점멸은 기존처럼 실제 유효 명중1회입니다.

무시되는 준비 블록은 동기화 탐색에서도 건너뛰어 같은 Projectile/Sequence의 방어창을 두 번 만들지 않습니다. 실제 특수 이동은 임의로 생략하지 않습니다. Scene/Prefab/SkillData를 대량 재저장하거나 ID/enum/필드를 변경하는 마이그레이션은 필요 없습니다.

## 수명·변경 위치

`SkillActionBlocks`/`SkillContext.EnemyMeleePresentation`/`BattleEnemyMeleeDashPath`/`BattleEnemyMeleePresentationScope`/`Action_EnemyAssaultCombo`/`Action_DefenseWindow.Assault`가 접근·판정·반응 지연을 소유합니다. `QTEManager`/`BattleImpactTiming`은 무정지 시계와 명중 프레임을, `Action_EnemyWindup`/`Action_PresentationAccent`/`SkillContext.PresentationAccent`는 구형 대기 제거·병행 표시를 소유합니다.

`BattleTurnQteModuleControllerService`/기존 `BattleManager`/시나리오 `BattleSkillTimelineRunner` 모두 마지막 반응 소비 전에 지연 플래그를 내립니다. 중간 피격/회피가 다음 타격을 막지 않으며 정상 종료/취소 정리는 유지합니다. 투사체는 물리 도착·판정·취소·finally가 겹쳐도 반환/피해가 한 번이고, 목표는 회피 중 좌표가 아닌 배치+피벗 오프셋입니다. `SkillMakerBlockEditor`와 관련 Editor 회귀도 갱신했습니다.

로컬 SkillData 실행 정책이며 Scenario Source 문법·Action ID·ActionLibrary·저장 스키마는 변경하지 않았습니다. 저작 지침 Legacy Mapping만 동기화했습니다.

## 검증과 한계

전체CLI Runtime482/Editor389 오류0/0(기존경고229/0). 실제 생산 관리형 검사: 이동49사례255단언+QTE7소스계약, 투사체8사례36단언, 타임라인 분류/이야기·아군 보존12단언+외부root2계약, 실루엣18,441+14, Windup9+서비스19, VFX8사례1,023+4, 방어/QTE144사례·재준비479+8·아군QTE16+3 통과. Lab16스킬122블록·참조·번역은799/1,762단언, Lab데이터225·기존 사슬178단언을 통과했고 보호 원본11파일/Scenario SourceHash는 동일합니다.

관리형 경계는 Unity/TMP/DOTween/입력/풀 대역입니다. 서비스 소스계약은 실제 호스트 전투 실행이 아닙니다. Native NUnit은 추가/컴파일만 했고 **Unity Play·화면 손맛·입력 박자·애니메이션/VFX·기기 성능은 미검증**입니다. Refresh/씬 저장/실제 세이브 I/O는 수행하지 않았습니다.
