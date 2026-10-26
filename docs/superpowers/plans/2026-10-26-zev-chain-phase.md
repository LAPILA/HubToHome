# 제브 관통·사슬 2페이즈 Implementation Plan

> **For agentic workers:** 승인 설계의 파일 소유권을 나눠 병렬 구현하고 독립 검토한다. 기존 dirty 작업을 보존한다. Unity Play/Refresh/전체생성/커밋/푸시는 하지 않는다.

**Goal:** 읽을 수 있는 방어 시계를 보존하며 날렵한 제브와 사슬2페이즈를 구현한다.

**Architecture:** 적 관통은 기존 방어 연출 경로, 페이즈는 Scenario Source/Runtime, 사슬은 프로필+플래그 관찰 표시, 배경은 전투 대화 레이아웃 lease가 소유한다.

**Tech Stack:** Unity6/C#/DOTween/Odin/uGUI/Scenario Source/기존 SFX mixer.

## Chunk1 · 적 화면 관통

- [x] 관통 담당: SkillActionBlocks의 Defense 필드/Prepare 호출, SkillContext.EnemyMeleePresentation, BattleEnemyMeleeDashPath, BattleEnemyMeleePresentationScope와관련테스트를 변경한다.3종 Lab자산연결은 데이터 담당이 수행한다.
- [x] 실제 복사된 방어시계/교차/guard stop/dodge pass/일반Local회귀/취소·프레이밍·중앙C를 관리형 검사한다. 필드/API확정 후 데이터 담당에게 전달한다.

## Chunk2 · 사슬 표시와 시나리오

- [x] 표시 담당: BattlePhaseOverlayProfile/Presentation/메시·음량 정책/테스트와 ZEV_ChainPhase.asset/meta를 추가한다. Activate는Prepare(profile)→Show(),Hide() API로 한다. source에판단을넣지않는다.
- [x] 데이터 담당: ZEV_Lab HP1800,3스킬wide,50%phase2/pose/flag/30%rule제거·Runtime동기화·해시·대사4언어와 Builder/자료검사를 갱신한다. Lab Encounters의 PhaseOverlay 참조를 연결한다.
- [x] 데이터 담당: BattleDialogueLayout/DialogueUI의 명시 배경 참조·settings·테스트로 전투배경만 검정/복원한다. 필드 prefab은수정하지않는다.

## Chunk3 · 이벤트 통합·최종 확인

- [x] Root: IBattleSessionFlagEvents/BattleSessionState의 변경 이벤트, BattleManager의읽기전용Session접근, BattleFlagPresentationScope와 LabEntry.PhaseOverlay/Session 수명연결을 추가한다. 동일값중복/clear/재진입/abort/종료를 검사한다.
- [x] Root: SkillMaker/EnemyAttackAuthoring의새화면범위 한글저작/유효성을 맞추고 flags·원본보존·전체컴파일·GUID/UTF8/diff·독립검토를 수행한다.
- [x] Root: docs/현재AIAssets/규칙·시나리오저작스킬과자료구조·설정위치를 갱신하고 Play/GPU/소리체감 미검증을 명시한다.

## 최종 검증

Runtime478/Editor388소스 CLI 오류0/0·경고227/0. 관리형: 적관통22사례87단언, 기존아군16사례55단언, 사슬44,424+14소스계약, 플래그2,601+9소스계약, 명시포커스11사례15단언, Source/Runtime/번역170, 전체15스킬1,683/762,1인세션10사례25단언, Z재준비479+8소스계약 통과. meta1,733개32hex유일성·보호20원본 해시 일치·UTF8/CSV BOM/소유파일diff 확인. 경고227에는 새 선택적 배경Image의CS0649가 포함된다. 마무리에 관찰된 직접 작성하지 않은 Lab씬 직렬화 변경과 그 공백 경고는 보존했다.

명시포커스가 전체참가자bounds에 눌리는 문제는 actual Constrain baseline3실패를 재현한 뒤, 기본대상/ordinaryShot/Timeline을제외한등록추적배우 필터로 수정했다. 픽셀Lens/새카메라/새저장필드는없다. [인수인계](../../../AIAssets/yjlim/feedback/2026-10-26-zev-chain-phase.md). Unity Play/GPU/실제소리·시각·기기성능은 미검증이다.
