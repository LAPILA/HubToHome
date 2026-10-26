# 제브 사슬2페이즈 · 실행과 수정

후속 변경: [사슬2배·개별 반전·음량0.45·제브 일반4종·고정3줄 기록](2026-10-26-battle-chain-rolling-followup.md)을 우선합니다. 아래 초기 음량0.18/3종 관통 수치는 후속에서 대체했습니다.

브랜치 `gameplayEdit` · 커밋/푸시 없음 · 실험실 제브만 변경, 본편 제브/원본 스킬 보존

## 확인 순서

1. 기존 Play를 종료한 뒤 **Hub To Home → 테스트 → 전투 실험실 열기**에서 새 Play를 시작합니다.
2. **ZEV · 연출 전투**를 시작합니다. 기본 위젤1명 대 제브1명이며 제브HP는1800입니다.
3. 빙섬·잔뢰 이연·뇌광 순회의 화면 양끝 접근/관통을 확인합니다. 전조와 Z/X/C는 기존 방어 판정이며, 일반가드·저스트는 접촉 정지, 회피·실패는 반대편까지 지나갑니다. C 궁극기는 중앙 연출입니다.
4. 제브HP가 절반을 넘어서 내려가면 현재 행동 종료 뒤 **제브 포커스 → 대사 → 공격 모션 → 자리·카메라 복원 → 사슬UI/SFX**가 한 번 나옵니다.30%추가대화는 없습니다.
5. 사슬은 전투 영역 네 모서리에서±45도로 한 방향 반복 이동합니다. 시작음은 작게 재생해1.2초에 감쇠하고 BGM은 그대로입니다. F8/승리·패배/씬 종료 때 모두 정리합니다.

## 어디서 바꾸나요

| 변경 | 위치/설정 |
| --- | --- |
| 제브HP | Lab `Data/Enemy/ZEV_Lab.asset → BaseStats.MaxHP` |
| 화면 관통 | Lab `Content/Skills/Enemy/BattleLab/ZEV`의 방어 대응 → 근접 관통/화면 양끝, 전체 관통0.22초·끝 접근0.10초·여백12px |
| 사슬 그림·색·두께·속도 | Lab `Presentation/ZEV_ChainPhase.asset` |
| 시작음 크기·감쇠 | 같은 프로필의 효과음 크기0.18/감쇠1.2초.0이면 로드·재생 생략 |
| 대사 | Lab `Data/Scenario/Dialogue_ZEVLab_phase2.asset` 및 LocalizationTable17키/한영일중 |
| 페이즈 조건·순서 | 기존 시퀀스 메이커/Scenario Source의 `lab.zev.phase2` |
| 전투 대사 배경 | DialogueUI의 BattleDialogueLayoutSettings.BackgroundColor, 기본 검정 |

Source는 `Assets/_Game/Content/Scenarios/Source/Battle/BunnySlimeBattleLab/zev_lab.scenario.yaml`입니다. 실행 자산과 SourceHash도 함께 맞춰야 하므로 `.asset`의 복잡한 managed-reference 데이터를 직접 복붙하지 않습니다. 대사·그림·소리는 샘플 교체 대상입니다.

사슬은 조우의 선택적 `PhaseOverlay`에 연결됩니다. 다른 전투에 재사용하려면 프로필을 복제하고 표시 플래그를 정한 뒤, 시퀀스 마지막에 **전투 플래그 설정**으로 같은 키와 **문자열 `"true"`**를 설정합니다. 해제는 전투 플래그 해제 또는 문자열`"false"`입니다. 기존 Source 문법이며 새 UI Action/ActionLibrary 재생성은 없습니다.

## 코드 구조·성능

- 적 관통은 `Action_DefenseWindow`의 Local/ScreenWide와 `BattleEnemyMeleePresentationScope`가 소유합니다. 실제 복사된 TimeWindow/BattleImpactTiming을 써서 피해/VFX와 접촉을 맞추며, Damage/SequentialMelee가 유일하게 피해를 계산합니다. 화면 확보 실패는 기존 Local로 돌아갑니다.
- `IBattleSessionFlagEvents → BattleFlagPresentationGate/Scope → BattlePhaseOverlayPresentation`으로 연결합니다. 규칙/체력/적 이름 판단은 UI에 없습니다. 같은값은 중복 통지하지 않고, 준비 실패도 전투를 막지 않습니다. 실제 저장에는 플래그/코루틴/사슬 상태를 기록하지 않습니다.
- 실제16×96 Chain.png를4RawImage·단일재질·1샘플UI셰이더에서 반복합니다. 기존 Chain.mat은 바람 재질이므로 사용하지 않고 그림/meta도 바꾸지 않았습니다. Canvas-200은 HUD(-100)뒤이며 마스크는 y170~424에 제한합니다. 추가 카메라/RT/블러는 없습니다.
- 자체 AudioSource1개만 기존 SFX믹서에 연결합니다. 음량은0.18부터제곱곡선으로 감쇠, BGM/공유소스는 변경하지 않습니다. 음원로딩이 늦으면 UI를 먼저 보이고 준비 뒤 한 번 재생합니다. 해당mp3.meta의loadInBackground만1로 바꿨으며 강제 Refresh는 하지 않았습니다.
- 모달/일시정지는 스크롤·감쇠·소리를 함께 멈춥니다. 종료/F8/교체/소유자 비활성·파괴/씬 전환에서 자기 UI·소리·구독·코루틴만 정리합니다.
- 명시 카메라 배우 포커스는 해당 비기본 등록 배우만 안전영역 기준으로 삼습니다. 기본대상/일반 전투샷/Timeline/없어진배우는 전체기준이며 픽셀 퍼펙트·고정배율은 바꾸지 않았습니다.
- 전투 주대화만 배경Image의색/enabled를 빌려검정으로 바꾸고 해제시복원합니다. 필드/일반 시네마틱/초상/선택지/기존대화프리팹/재질은 보존합니다.

## 검증과 남은 확인

전체 C# Runtime/Editor 컴파일 오류0/0. 실제 생산코드 관리형 검사: 적 관통22사례87단언, 기존아군관통16사례55단언, 사슬44,424단언+14소스계약, 플래그2,601단언+9수명소스계약. 카메라 actual viewport baseline3실패를 재현한 뒤11사례15단언 통과. Source/Runtime/4hash·HP1800·50%/6단계·3관통6방어창·17대사68번역 연결170단언, 전체15스킬1,683/762단언, 1인세션10사례25단언도 통과했습니다. 보호20원본SHA256 일치와meta32hex유일성/UTF8·CSV BOM/소유파일diff를 확인했습니다. 마무리 중 별도로 관찰된 Lab씬 직렬화 편집과 그 공백 경고는 수정하지 않았습니다.

관리형 검사는 엔진/입력/카메라/UI/오디오 경계에 대역을 사용합니다. **Unity Play·실제 렌더/셰이더 GPU 컴파일·도트45°계단감/반복 이음새·입력/애니메이션 박자·SFX와BGM의실제혼합·기기발열/프레임은 미검증**입니다. 새Play에서50%대화와사슬,가드/회피/실패,일시정지·F8,전투뒤필드배경복원을 확인해야 합니다. 실제 저장I/O·전체생성기·씬저장도 실행하지 않았습니다.

[승인 설계](../../../docs/superpowers/specs/2026-10-26-zev-chain-phase-design.md) · [구현 계획](../../../docs/superpowers/plans/2026-10-26-zev-chain-phase.md) · [당일 기록](../../2026-10-26-update.md)
