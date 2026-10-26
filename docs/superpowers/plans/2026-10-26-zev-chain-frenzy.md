# 제브 사슬 난무 Implementation Plan

> **For agentic workers:** 독립 파일 소유권에 따라 병렬 구현하고 전체 통합/상호 검토한다. 사용자 요청 없는 커밋·푸시는 하지 않는다.

**Goal:** Lab제브의2페이즈 첫 공격 뒤 다음 행동에20타+최종C검격과1턴탈진을 재사용 가능한 데이터로 연결한다.

**Architecture:** Scenario는기존phaseflag만소유한다. 선택/완료/회복은범용AI패턴state, 연출은기존방어/피해시계를사용하는EnemyAssault단계, C전용판정과최종VFX는공용DefenseWindow확장으로분리한다.

**Tech Stack:** Unity6, 기존SkillData/Scenario flags, Odin, DOTween, ObjectPoolManager, 관리형CLI.

## Chunk 1: 병렬 구현

- [x] EnemyData.FollowupPatterns/SkillData.RecoveryTurns·순수상태·선택적host읽기·TurnQte정상완료/회복수명. 기본기존적불변, 취소/이전phase행동제외, C정상중단에도회복1턴.
- [x] Action_EnemyAssaultCombo/단계목록·공중/가드통과옵션·기존근접scope확장.20타를기존방어/피해소비자로실행, 이동/잔상/취소복구검증.
- [x] CounterOnly7 append·판정/신호/표시/저작검사, 최종검격VFX성공/실패/취소수명과프리팹풀정리.
- [x] Lab새스킬10/2/8+최종·DB후속패턴·Catalog/4언어·GUID/참조. 원본스킬/본편DB보존.

## Chunk 2: 통합·인수인계

- [x] 실제생산관리형검사/기존방어·카운터·화면관통회귀와전체Runtime/EditorCLI오류0.
- [x] 독립review/원본보존·UTF8/GUID32hex유일성/ID/소유diff.
- [x] SkillMaker/문서/CONTEXT/도메인규칙/로컬Scenario skill에로컬옵션임을기록. 새Scenario문법은없음.
- [x] 재생방법/조정위치/실제Unity미검증범위를전달.

## 후속: 가속 체감·fallback 수정

- [x] 실제 데이터11타째 역감속 및 Local 고정 접근 속도를 회귀 검사로 먼저 재현.
- [x] 관통 `.60→.16→.13→.08초`, 판정·준비 단계 경계 단조성, Local 가속 전달, 최소값 끝점 보존.
- [x] 실제 이동/공유시계29사례158단언·데이터222단언·기존방어/QTE144사례·패링재준비479+8·전체스킬799 및 전체CLI오류0 확인. 실제Unity체감은미검증.

## 후속: 타격 간 정지 제거

- [x] 기본 모드3타 Ready3/반응대기3회·접근 전 대기 재현.
- [x] Lab전용 ContinuousMotion으로 실제 접점 판정/감속 해제/유예 중 관통/0거리 준비 제거/중간 반응대기 지연. 일반 모드·C준비는 유지.
- [x] 실제 경로/루틴38사례210단언+QTE연결4계약·데이터223·방어144·재준비479+8 통과. 실제Unity입력/렌더체감은미검증.

최초구현 기록: Runtime482/Editor389오류0, 방어·QTE144사례/근접27사례143단언/후속11,068+12/VFX8사례1,023+3/데이터160/전체스킬799·1,761/번역467통과. 메타1,739개32hex유일성. 실제Unity/native/GPU/입력체감은미검증이다. [인수인계](../../../AIAssets/yjlim/feedback/2026-10-26-zev-chain-frenzy.md).
