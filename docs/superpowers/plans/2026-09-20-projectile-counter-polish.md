# Projectile and Counter Polish Implementation Plan

> 사용자 요청에 따라 현재 작업 트리에서 바로 구현한다. 추가 기획 승인, 서브 에이전트, Unity 재생/Refresh, 커밋은 하지 않는다. C는 사용자 답변대로 피격 대상 1명만 수행한다.

**Goal:** 실험실 적 투사체 3패턴, 연타에 안전한 방어 연출, 빠른/늦은 입력 여유, 접근·패링·공격·복귀 C 연출.

**Architecture:** 기존 DefenseJudgementPolicy/QTEManager/PlayerController 소유권과 BattleLinkCounterService를 수정한다. 입력 수집과 미리보기 실행을 분리하고 타격에 선택된 대상만 연출한다. 판정 전 여유 확대 + 0.06초 늦은 입력 유예 동안 충돌 포즈를 유지하며 피해는 한 번만 확정한다. HP 롤백은 하지 않는다. 스킬/프리팹은 기존 풀링 투사체와 SkillActionBlock을 사용한다.

**Tech Stack:** Unity 6, C#, DOTween, NUnit 순수 정책 검증, 직렬 dotnet 컴파일.

## Chunk 1: 연타와 판정

- [x] PlayerController 입력→미리보기 호출 분리. 방어창 대상/완료 상태, 회피 진행/피격 상태로 연출 재시작 차단. 입력 버퍼 자체는 유지.
- [x] DefenseJudgementPolicy 최소 Z/X/C 구간 0.24/0.34/0.28초, 실제 타격 뒤 0.06초 유예. 유효 입력은 갱신 불가, 이전 타격 재사용 차단.
- [x] QTEManager에서 실제 타격/최종 확정 분리. 성공 입력이 있으면 타격 즉시, 없으면 최대 유예까지 공격자·투사체 진행 정지 후 확정. 취소/메뉴 정지 동일 처리.
- [x] 순수 정책 회귀 검사. Unity 실제 Animator/화면 재현은 사용자 제한으로 수행하지 않으므로 연타 원인은 정적 호출 경로 확인과 회귀 항목으로 기록.

## Chunk 2: C 교환 연출

- [x] BattleLinkCounterService 참가자를 defender 1명으로 제한. 방어 입력/미리보기는 닫고 접근→패링→공격→복귀를 소유된 DOTween으로 수행.
- [x] 적은 현재 전진 위치에서 마주보고, 공격 흐름의 기본 위치로 복귀. 사망/중단 시 위치·애니메이션·입력 상태 정리, 추가 AP/턴 소비 없음.
- [x] C 실험 스킬에 전조 이전 접근 추가. 기존 파티 전원 반격 설명/회귀 기대값 갱신.

## Chunk 3: 실험 데이터와 검증

- [x] BunnySlimeLabContentBuilder에 일반 단발/엇박자 2연발/회피 전용탄 추가. 기존 생성 데이터에도 새 SkillData 3개와 참조만 추가하고 씬 재생성하지 않음.
- [x] EnemyData 패턴 목록과 프리팹 HP 페이즈 경계 갱신, 가드·회피 연습에도 투사체를 포함. 기존 아군 스킬/상태/오프닝 유지.
- [x] Runtime+Editor 직렬 dotnet build, 수정 범위 diff check, 자산 GUID/패턴 연결 정적 검사.
- [x] CONTEXT/전투 규칙/시나리오 action 참조/실험실 문서/당일 기록 갱신. 실제 Unity 재생 미검증과 음원 미지정 명시.

## 추가 요청: 중앙 궁극기 / 피격 대상 전진 / 지상 복귀

- [x] C 실험 데이터와 생성 템플릿을 전투 중앙 접근으로 변경. 적의 이동 높이만 선택적으로 DOTween에 전달하며 기존 스킬 기본값은 0으로 유지.
- [x] 아이템과 같은 1유닛 전진을 기본 공격·적 스킬·시나리오 타임라인에 공통 적용. 임시 방어 기준점은 소유된 Scope로 관리하고 정상 종료/취소/비활성화에 원위치 복원.
- [x] C 성공 시 전진 위치를 거치지 않고 적·아군 모두 원래 자리로 함께 복귀. 기본 공격의 아군 DOJump 복귀를 DOMove로 변경.
- [x] 회귀 코드 추가·컴파일, Runtime/Editor 오류 0(최종 기존 경고 2), 데이터 정적 검사 12건 및 diff 공백 검사 통과. Unity 재생/강제 Refresh 없이 결과 기록.
