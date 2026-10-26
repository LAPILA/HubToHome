# HubToHome

## 전투 공통 피드백 기반 (2026-10-26 최신)

평타·스킬 구성·전조 말풍선 기획은 보류한다. 공통 판정은 Z0.12/X0.22/C0.18초와 늦은 유예0.03초이며 기존 긴 전조 표시 시계와 분리한다. 예고 구간 첫 대응을 확정하고 Z/X/C 공통 재준비0.18초(짧은 공격 길이 절반 이하)를 사용한다. 홀드 Z는 여러 타격에도65%감소이며 비모달 기록창이 대응 Z를 소비하지 않는다. Counterable6/CounterOnly7 모두 C전용이다. 아래 과거의 성공창 .24/.40초·X/C 혼용·빠른 키 교체 허용 설명보다 이 정책이 우선한다.

기본 AP는 턴+2/저스트+2/적행동당 아군별 최대+6이다. `BattleManager.BeginParryRewardAction`의 중첩/세대 scope를 일반·구형·시나리오 경로가 공유한다. Lab만 최대40/시작20이고 본편 성장·장비·저장은 유지한다. 누적10/25/50에 상태 피해×1.25/1.5/2, 선택적 아이템 누적 부여/소비와 ItemUsed5 장비 발동을 제공한다. 공격 QTE 자동 옵션은 기본false/기존시계/방어제외다. `BattleBossProgressPresenter`는 보스/선택적 적 HP만 관찰하며 페이즈는 기존 Scenario가 소유한다. Source 문법/Action ID/SkillData를 변경하지 않는다. [설정·코드·검증](AIAssets/yjlim/feedback/2026-10-26-combat-feedback-foundation.md).

## ZEV 단독 샘플 · 임시 반복 잠금 · C 성공 포커스 (2026-10-26)

Lab토끼입구는삭제아닌HiddenInMenu로숨기고표시index를분리한다. ZEVEntry.SkillLock는기존phaseflag부터본인정상행동2회마다최대3스킬을중복없이교체한다. BattleSkillLockScope/Cycle/PlayerCharacter의소유자별집합이임시상태를소유하며원본Skills/SkillData/저장은바꾸지않는다. 메뉴CanUse와실행3경계가같은잠금을검사한다. 행포인터도기존대상선택/입력소비로전달하며거부는비모달빨강흔들림이다. 다른메인탭클릭은목록전환만하며이전entry를확정하지않는다.

C성공은큰후퇴→정수2배위젤포커스1초→접근명중1회→적뒤돌파→양측복귀다. CameraController의명시scope가viewport/최종출력에같은배율을제공하며일반sessionLens/HUD/PP를변경하지않는다. 적공격의무정지대응정책은유지한다. [설정·수명·검증](AIAssets/yjlim/feedback/2026-10-26-zev-sample-seal-counter.md).

## 전체 적 공격 기본 정책 — 이동 중 실시간 대응 (2026-10-26)

이 정책이 아래의 이전 opt-in/준비·가드 접점 정지 설명을 대체한다. 실제 적 공격은 접근/비행/모션 진행 중 대응 창과 전조가 뜬다. 일반은 기존 TimeWindow를 보존해 접근 전체에 쓰며 Combo 런타임 복사본만 이동 기준 접점을 계산한다. C·광역은 중앙 배치와 명중 시각을 유지하며 창 시작부터 클립을 양수 속도로 재매핑한다. 해당 VFX는 원래 impact lead에1회다. 공격 감속/impact freeze/Ready·전조·끝 대기·중간 방어자 반응 완료 대기는 없다.

적 전투의 Wait/Ready/Windup/중복 지상 앞 접근은 생략하고 실루엣도 병행한다. 이야기·아군 대기, 명시 특수 이동, 모달 pause, C 반격/복귀와 예약 턴은 유지한다. 무시되는 준비 블록은 동기화 탐색에서도 건너뛰어 Projectile/Sequence 방어창을 중복하지 않는다. 모듈·기존·시나리오 root는 최종 반응 소비 전 Defer=false로 내린다. [제작·코드·검증](AIAssets/yjlim/feedback/2026-10-26-global-enemy-attack-flow.md).

## 제브 사슬 광란 · 로컬 AI 후속 (2026-10-26)

Lab난무는 `Action_EnemyAssaultCombo.ContinuousMotion` 선택 모드를 사용한다. 매타 Ready·접근 전 정지·공격자 감속/접점 고정은 생략하고 이동 접점에서 판정한다. 늦은 입력 유예 중 관통은 계속하며 방어자 반응 완료/정리만 전체 난무 끝으로 미룬다. 목표는 진행 중 회피 좌표가 아닌 방어 배치다. 마지막C와 기본 false인 다른 공격은 기존 경로를 유지한다.

최신 조정: 관통 기준 `.60→.34→.30→.24초`로 반응 시간을 늘렸고 이름은 ZEV다. 상단 ‘누구의 턴’ 글자만 제거하고 턴/탈진은 유지한다. 아군QTE는 결과 글자 없이 흰색 성공/빨강 흔들림 실패로 표시한다. 실루엣은 Hide/OnDisable에서 재부모하지 않고 다음 Bind에서만 연결한다. [최신 수명·검증 계약](AIAssets/yjlim/feedback/2026-10-26-battle-feedback-polish.md).

Lab새스킬 `lab.showcase.zev.chain_frenzy`는2페이즈flag뒤정상공격1회완료→다음자기행동1회로연결된다. `EnemyData.FollowupPatterns`/`BattleEnemyActionPatternState`가행동시작flag스냅샷·정상완료·다음선택을소유하고phase전환은기존Scenario가유지한다. Source/Runtimehash/ActionId문법·저장은변하지않는다.

새EnemyOnly `Action_EnemyAssaultCombo`의단계목록이왕복10/공중2/혼합8을기존DefenseWindow→Damage로한번씩실행한다. 공중높이/가드관통은선택적이며기존default0/false는보존한다. 최종`CounterOnly=7`만C전용이고기존6은X/C다. 공격시작검격VFX는세대lease/풀수명으로C성공/취소를처리한다. `SkillData.RecoveryTurns=1`은정상/C중단완료뒤다음자기턴1회휴식이며기절저항과독립/전투수명내만유지한다. [실행·설정·검증](AIAssets/yjlim/feedback/2026-10-26-zev-chain-frenzy.md).

## 사슬·제브 이동·전투 기록 후속 (2026-10-26)

최신 정책은 [후속 기록](AIAssets/yjlim/feedback/2026-10-26-battle-chain-rolling-followup.md)을 따른다. Lab 사슬은 배율2/폭32/길이256/반복주기192·코너별 대칭·시작음0.45/1.2초 감쇠다. 제브 일반4종7방어창이ScreenWide이며 중복AttackStaging4개/대기1개를 데이터에서 제거했다. C중앙·가드접점정지·피격아군전진은 유지한다.

상단 기록의 이전 즉시전체표시/내용별자동크기는 폐기했다. BattleNarrationUI가 고정480×72/14px/최대3줄·줄별타이핑·Z현재줄완성/다음줄진행·자동진행을 소유한다. TMP전체rich text와firstVisibleCharacter0을 유지하고 최초lineInfo의문자범위/기준선을 캐시해본문을 마스크안에서 올린다. CombatLogPresenter의 실제HP/AP/상태집계는 그대로이며3대상씩페이지분할하지않는다. UI의모달/입력소비/종료정리를 유지한다. 본편원본DB/스킬/Scene/저장은 이번 수정에 포함하지 않는다.

## 위젤 단독 연출 실험실

현재 BunnySlimeBattleLab은 흰 기본 위젤1명/토끼·제브 두 입구다. 본편3+3와 저장은 유지하며, Lab Session만 자기 임시 파티를 준비/복구한다. 원본 위젤10·제브5스킬은 보존하고 Lab 전용15사본을 연결한다. 컷인은 전부 제거했으며 enum0은 저장 호환용 no-op이다. `Action_PresentationAccent`의 실루엣은 실제 배우 Renderer를 동일 크기로 복제하고, Flash는 SkillContext의 다음 실제 유효 명중1회에 비차단 예약한다. `Action_ScreenPassSlash`는 아군 전용 화면 좌우 관통/교차 시점 피해/자기 카메라 구도·위치 복원을 소유한다. Lab3스킬에 연결했다. 엄격 픽셀 Lens/Dutch0과 기존 피해/QTE는 유지한다. 반복 Z만 기본0.18초 재준비(짧은 공격은 길이 절반 이하), 홀드 가드와 X/C는 유지하며 배우 옆 회색→노랑 표시가 준비 상태를 알린다. Source/Runtime party와 SkillID/GUID는 유지한다. [실행·재사용·검증 한계](docs/bunny-slime-battle-lab.md).

## 제브 사슬2페이즈 · 명시 카메라 포커스 (2026-10-26)

Lab 제브만HP1800/50%교차1회 전환으로 바꿨다. Source는 제브 포커스→대사→표시용 Attack→자리/카메라 복원→`battle.flag.set`으로`lab.zev.chain_phase="true"`를 켠다.30%추가대화는 실행하지 않고 원본 대화 자산은 보존한다. 기존5스킬 중빙섬/잔뢰이연/뇌광순회는 기존 DefenseWindow 시계의 ScreenWide 경로다. 피해는 기존 소비자만 계산하며 guard/parry는접촉정지, dodge/실패는끝까지관통, C는중앙연출이다.

`IBattleSessionFlagEvents`는 실제 플래그 변경을 통지한다. `BattleFlagPresentationGate/Scope`가 선택적 LabEntry.PhaseOverlay를 연결하고 `BattlePhaseOverlayPresentation`은4개모서리UI/단일SFX만 소유한다. HP/저장/분기는 판단하지 않는다. 프로필은 그림·속도·색·음량·감쇠를 편집한다. BGM은변경하지않고 전투끝/F8/비활성/씬이탈에정리한다. 전투 주대화 BackgroundColor는black이며 lease해제시원래Image상태를복원해필드는보존한다.

명시적 단일 배우 추적 중에는 전투 viewport가 해당 배우만 기준으로 삼는다. 기본대상/일반battleShot/Timeline/소실된대상은기존전체기준이다. Lens고정/PP/Dutch0은유지한다. [설정·검증·미확인사항](AIAssets/yjlim/feedback/2026-10-26-zev-chain-phase.md).

## 전투 행동 결과 기록 (2026-10-25)

`BattleCombatLogPresenter`는 기존 `DamageBatches`와 CharacterBase의 실제 HP/AP·상태 이벤트를 관찰해 대상별 결과를 행동 종료에 요약한다. 손실/회복은 별도 합산하고 기존 Damage/Elemental 숫자를 중복 가산하지 않는다. 반격 하위 결과는 현재 행동 배치에 모으며, 종료·모듈 중단·상태 일괄 정리 전에 구독과 대기 결과를 버린다. `BattleCombatLogFormatter`의4언어 문구를 기존 중앙 BattleNarrationUI에 즉시 표시·짧은 자동 종료로 보낸다. 새 `RevealImmediately`는 기본false이며 기존 타이핑/Z확인은 유지한다. 피격/저체력 자동 말풍선만 제거했고 이야기·스킬/턴 말풍선·전투 계산은 보존한다. 새 스턴은 없으며 기존 상태 전환과 행동 불가 이유만 안내한다. [사용법·검증 한계](AIAssets/yjlim/feedback/2026-10-25-battle-combat-log.md).

## 고정 전투 배율 · 엄격 픽셀 출력 (2026-10-23 최종 후속)

앞선 HUD 보정의 매 프레임 자동 Lens 확대·축소와 연속 줌 우회를 제거했다. `CameraController.BattleViewport`는 전투 시작에 안전영역을 계산한 뒤 한 session의 Lens를 고정하고 중심만 제한한다. PPU32/640×480에서 허용값은7.5/n(정수n≥1), 기본 전투는 원본1배/7.5, 최소 요청값은3.75다. 수용 범위를 넘는 배치는 경고하고 자동 줌으로 숨기지 않는다. 하단162px/상단48px+각8px 예약·몸체 캐시·방어 Hold·Timeline lease는 유지한다.

`CinemachineContinuousPixelZoom`은 이름만 호환 유지하며 native CinemachinePixelPerfect를 끄지 않는다. Body/Finalize의 보정 반환값·최종XY격자/Dutch0을 적용하고 Brain 블렌드 후 출력 Lens도 보정한다. 명령 Release와 전투 정책 owner 수명은 분리한다. 엄격 모드의 명중/스킬 줌·카메라 회전은 억제하며 이동/피격/VFX/HUD는 보존한다. 새 카메라/RT/GPU패스는 없다. [규칙·설정·파일·검증 한계](AIAssets/yjlim/feedback/2026-10-23-camera-pixel-stability.md). CLI447/370소스 오류0·관리형52사례/73,689단언·메타1,555개 통과. Unity 재생/GPU/실기기는 미검증이다.

## 선택형 적 근접 돌파 (2026-10-23 후속)

제브 빙섬/개별 연쇄 참격의 기존 `Action_DefenseWindow.UseMeleePassThrough`만 선택 적용한다. 적 중심 준비 구도 → 방어 시계 마지막0.12초 접근 → 접촉 위치에서 기존 피해 → 실패/회피 시 뒤0.95거리로0.09초 통과 → 기존 복귀다. 가드/저스트가드는 접촉에서 멈추고 C/투사체/직접 광역은 제외한다. 목표는 최초 포착점으로 고정한다. 카메라는 방어 hold 동안 고정, 결과에만 짧은 방향 이동/줌을 낸다. HUD/전조 창/피해 계산/저장은 유지한다.

`SkillContext`가 이동·잔상·카메라 scope를 소유하고 실제 피해 후 `WaitForPendingDefenseReaction`에서 통과 이동을 소비한다. 취소/새 범위/연쇄 복원 순서를 보존하며 전역 시간·새 Update/GPU 패스는 없다. Windup이 Ready를 이미 표시했어도 카메라 진입에 필요한 준비 시간만 판정창 전에 보장한다. [조정·제작·검증](AIAssets/yjlim/feedback/2026-10-23-enemy-melee-pass.md). CLI Runtime444/Editor368 오류0, 경로871관리형 단언 통과; Unity 체감은 미검증이다.

## 오버월드 동료 동행 (2026-10-23)

이번 승인으로 기존 ‘필드에는 위젤만’ 제한을 대체한다. 실제 저장 파티의 리더를 제외한 순서대로 기본 동료2명(리더 포함3명), 설정 시 최대5명(전체6명)을 표시한다. 새 게임은 여전히 위젤 한 명이며 합류/탈퇴·성장·저장은 기존 GlobalDataManager가 소유한다. 표시를 위해 실제 파티나 전투 인원을 생성하지 않는다.

`OverworldPartyFollowerController` / `OverworldFollowerActor`는 리더가 실제 지나간 꺾은선을 `OverworldFollowerTrail`의 고정1024점 순환 버퍼에 기록하고 누적 거리 커서로 추종한다. Collider/입력/상호작용을 복제하지 않으며 허용한 SpriteRenderer/표시용 Animator만 복사한다. CharacterData의 선택적 OverworldVisualPrefab, BattlePrefab 루트, 리더 표시 순으로 해석한다. 임의 프리팹 전체 Instantiate나 매 프레임 전역 탐색은 하지 않는다. 기존 BattleLab 미리보기는 자동 연결에서 제외한다.

Follow/Hold/Detached와 Visible은 방 수명의 표시 상태다. 명시 경유점 이동은 DOTween 자기 명령만 소유하며, 재합류는 남아 있는 실제 기록 경로 위일 때만 허용한다. 경로 만료/이탈은 Hold/명확한 실패이고 벽을 가로지르는 자동 복귀를 하지 않는다. Gather는 암전/도착용 명시 재배치다. 맵 도착·전투·리더 종료·명령 교체는 이동과 대기 명령을 함께 정리한다. 카메라는 기존 리더/Confiner/연출 제어권을 유지한다.

기존 Scene Action Sequence에 `overworld.party.mode / visibility / move / gather`를 추가했다. Preview/Preparation에서는 실제 필드 상태를 변경하지 않는다. 카메라 Stage/샷 없는 일반 필드 진입 시퀀스는 허용하되 샷만 지정하고 Stage가 없으면 실패한다. [제작 사용법](docs/overworld-party-follow-guide.md) · [자료구조·취소 학습](docs/study/deep/10-overworld-party-follow.md). Runtime/Editor CLI 오류0과 관리형 검사를 확인했으며 Unity Play/엔진 NUnit/실기기는 미검증이다.

## 방 날씨 샘플·피격·고정 이름 (2026-10-23)

`Content/Maps/Shared/Weather/WeatherSample.prefab`은 방 자식으로 넣는 표시 전용 샘플이다. `WeatherSample`이 최초 생성한 SpriteRenderer/ParticleSystem과 개별 재질을 재사용한다. 안개는 영역 안의 대상 주변만 월드 XY 원형으로 투명하게 만들며, 활성화 시 플레이어를 한 번 찾거나 `SetTarget`으로 명시한다. 비는 2D 착지점을 먼저 골라 시작점을 역산하고 Death sub-emitter로 물튀김을 낸다. 매 프레임 탐색/충돌/추가 카메라/RT/저장 필드는 없고 기존 씬에 자동 배치하지 않는다. 그림 슬롯3개는 후속 아트로 교체한다. [배치·교체 안내](AIAssets/yjlim/feedback/2026-10-23-weather-opening-hit.md).

후속 사용자 승인으로 직접 피격의 기본 Animator 정지는0이며 즉시 Hurt/백색/기존 Hit VFX·공격 방향 수평 반동을 낸다. CameraController가 공격 직전0.05초 구도 고정→명중0.025진입/0.025유지/0.075복귀로 박자를 맡는다. 실제 방어 hold/Timeline/모달/토큰이 우선하고, 명중이 진행 중 구도 전환을 끊지 않도록 일시정지 후 재개한다. HP 실제 숫자/바는 즉시, 잃은 구간만 지연표시하며 상태 부여/빙결 전환과 명중음4보이스도 공통 표시 계층에서 처리한다. 상태 틱은 새 정지/카메라 컷을 만들거나 직접 피격을 취소하지 않는다. `CharacterHitFeedback`은 원본 Y정렬/공유 재질을 보존하고 양수 정지 옵션의 기존 수명 계약도 유지한다. 공격자 이동/전조/QTE·전역 timeScale/음악/HUD배치는 변경하지 않는다. [조정·범위·검증](AIAssets/yjlim/feedback/2026-10-23-battle-impact-cuts.md). 수동/자동 저장 구조는 재작성하지 않았으며 실제 사용자 파일 대신 메모리 파일시스템으로 손상·교체 실패·옛 형식·미래 버전 보호를 검사했다. Unity/GPU/실기기는 미검증이다.

## 실험실 저스트가드 배경 파장 (2026-10-19)

`PlayerController.ConfirmDefenseSuccess`의 Z Parry만 Center 월드 위치를 `ParryBackgroundRipple`에 요청한다. 일반가드/X/C·피해·보상·전투 대기시간은 그대로다. Lab Renderer2D의 `ParryBackgroundRippleRendererFeature`만 연결했으며 기본0.3초/16→110px/폭14/변위1.25px/링alpha0.04다. 조정은 Lab Presentation의 `BunnySlimeLab_ParryRipple.asset`에서 한다.

BeforePP에서 같은 크기 R8 Point 허용마스크(Background white→상위 정렬레이어 black)를 그리고 source/destination이 모두 배경일 때만 원형 UV변형을 적용한다. 캐릭터·전경·HUD 보존을 우선하며 GPU SpriteSkin/커스텀 정점변형은 미지원이다. 카메라별 최신1개/동일owner동일frame중복차단·프레임당unscaled clock·BattleDelay pause·owner/scene/feature cleanup을 적용한다. 휴면GPU0패스, 활성mask+warp2패스, 추가카메라/상시CameraSortingLayerTexture/블러/깊이prepass/copyback은 없다. 기존 ScreenSplit/기본Renderer/본편DB/저장/시나리오는 보존한다. [설정·검증 한계](AIAssets/yjlim/feedback/2026-10-19-parry-background-ripple.md).

## 실험실 사선 화면 참격 (2026-10-19)

흰 외곽선은 검정 틈의 양쪽 바깥 경계에만 그린다. 기존LineColor/LineWidthPixels로 색상·알파·두께를 편집하고 기본white/1.25px이다. OutlinePulseStrength(.65)는 최대폭 도달 전후 짧은 두께 강조이며 별도 Bloom/블러/패스를 추가하지 않는다. 검정 gap 마스크를 최종 적용해 선이 틈 안으로 들어가지 않는다.

`Action_ScreenSplit`은 피해 뒤0.18초 확대→1초 유지→0.3초 복원하는 로컬 스킬 블록이다. 초기0강도/cubic smoothstep으로 자연스럽게 열고 닫으며, 기본 양쪽폭22/검정틈44기준픽셀이다. ExpandDuration/HoldDuration/기존Duration은 확대/유지/복원 시간이다. `ScreenSplitEffect`가 카메라별 토큰/표시 스냅샷을 소유하고 Lab Renderer2D의 `ScreenSplitRendererFeature`가 활성일 때만 RenderGraph1패스로 읽기 좌표를 바꾼다. 캡처/잔상패스 없이 표시를 유지하며 Overlay HUD는 그대로다. 활성 Color 입력으로 중간 색상 타깃을 요청하고 CopyColor/깊이-copy는 추가하지 않는다. 재질은 지연생성/재사용하며 첫 위젤7번째 `단층 베기`/AP12는 원소18개와 별도다. 본편DB/저장은 변경하지 않는다.

대상이 모두 사망하면 기본 블록은 기존처럼 중단하되 `CanRunWithoutLivingTargets`를 명시한 표시 블록만 실행한다. 현재 opt-in은 ScreenSplit뿐이며 취소/시전자 사망을 우회하지 않는다. Turn QTE 스킬의 실행 콜백은 현재 action scope와도 연결되어 강제중단 후 렌더 조회가 잔여효과를 제거한다. 시나리오 `battle.skill.timeline`도 동일한 마지막 대상 정책을 따르고 Source 문법/Action ID는 유지한다. [사용법·성능 한계](AIAssets/yjlim/feedback/2026-10-19-diagonal-screen-slash.md).

## 교체형 프롤로그 튜토리얼 (2026-10-24)

`PrologueTutorialDefinition`은 역할·대사·Room·전투·삽화·오디오·시퀀스 참조를 소유한다. Scene 수명의 `PrologueTutorialController`는 기존 서비스를 연결하고 `PrologueTutorialProgress`는 완료 플래그에서 단계를 읽는 순수 투영이다. Q0의 `PrologueTrainSequence`는 같은 Scene의 새 제어기가 있는 경우에만 옛 3초 사건/후미 자동 진입을 양보한다. 다른 기존 Scene은 그대로다.

진행은 창가 루프·원래3줄 대사→기관사·위젤의 자동 접근/마주보기 대화→직접 수리3조작→선택 탐색/드래곤 대화 완료→이상 발생→후미 공개→학습 전투→압박/구조1회→문 잠금→전방 침입→위젤 단독 감속→파손/탈선→저장 가능한 완료 Room이다. NPC/샘플 그림/문구는 데이터 교체 대상이며 드래곤은 실제 파티에 합류하지 않는다. 완료 사실만 기존 GlobalDataManager 플래그에 저장하고 실행 중 코루틴/전투는 저장하지 않는다.

기관사 도입의 로컬 위치·시간·역할/대사 ID는 Definition.Introduction에서 작성한다. PlayerController의 세대별 표시 이동 lease는 일반 Idle 갱신이 연출의 걷기를 덮지 못하게 하며, Room·세대·lease가 일치할 때만 취소 좌표를 복구한다. 객차 문은 접촉으로 이동하고 복수 Collider의 완전 Exit 이후 재무장한다. 도착 겹침은 시간이 지나도 Exit 전까지 재진입하지 않는다. 기존 수리 완료 저장에서는 새 도입을 강제로 반복하지 않는다.

최신 동선 수정: 첫 Q0 수리칸의 왼쪽에서 시작해 Repair→Common→Rear→Control을 모두 오른쪽으로 진행한다. 봉쇄 후 Repair 강제 복귀를 제거하고 Rear에 남는다. 저장 재개도 같은 순방향 Spawn을 사용한다. 기존 Room ID/enum/완료 플래그는 보존한다. `DialogueData.RevealTextImmediately`는 기본 false이며 전용 프롤로그27개만 true로 작성한다. 즉시 본문 표시 후 Z1회에 페이지/문장1단계씩 넘기고 선택지·첫프레임 입력 방어는 유지한다.

`BattleEncounterOutcome.StoryWithdrawn=4`는 opt-in 이야기 후퇴이며 Abort/도주/전멸이 아니다. 조기 처치도 이 정책에서만 후퇴로 변환한다. 보상·적 처치 기록·GameOver를 발생시키지 않고 기존 심리스 종료/복구를 사용한다. `IEncounterRosterPolicy`는 CharacterData ID의 부분집합만 임시 참가시키며 저장 파티를 재편하지 않는다. `OnPlayerActionCompleted`는 실제 행동/아이템 효과 완료 경계이고 방어 학습은 기존 DefenseQteResult를 소비한다.

끝은 `all.train.prologue_end/default`에서 자기 잠금을 해제한 후 자동저장 요청 버전의 완료를 기다리고 완료 선택창을 연다. 완료 저장을 다시 불러오면 탈선/최종 저장을 반복하지 않는다. SaveManager는 기존 완료 이벤트와 함께 버전을 포함한 알림을 제공한다. 진행 목표는 기존 JournalCatalog에 투영한다. [제작/검수 안내](AIAssets/yjlim/feedback/2026-10-24-prologue-tutorial.md), [승인 설계](docs/superpowers/specs/2026-10-24-prologue-tutorial-design.md)를 먼저 본다.

## 이전 임시 프롤로그 (2026-10-19, 위 튜토리얼 Scene에서는 대체됨)

2026-10-23 개정: 국가 설명4컷과 '그리고 현재' 자막을 Opening에서 제거했다. 새 게임은 창가 위젤 반복 장면 한 페이지부터 시작한다. Two empire BGM 재생 코드/프로필 필드는 제거했지만 원본 그림·음원·옛 대본과 범용 StoryPlayer는 보존했다. 새 기계 수리 장면/대본은 아직 만들지 않았다. 일반 DialogueUI의 Cinematic/Cutscene14cps 분리와 공유 Text Animator 자산을 직접 수정하지 않는 정책은 유지한다.

열차 음향은 Prologue_TrainAudio 공통 프로필과 AudioManager 자식의 PrologueTrainAudioSession이 소유한다. 창가부터 주행loop가 인트로→객차 씬 전환에도 이어지고, 사고에 파워다운1회, 후미 걷기 시작에 주행1.8초fade-out/발걸음2.4초를 재생한다. 전용2D소스2개/SFX믹서를 사용하며 MapSettings의 공용 환경음·BGM은 변경하지 않는다. 페이지 시작 알림은 표시 전용 이벤트이고 새 시나리오 문법이나 저장 데이터가 아니다.

창가 반복 장면은 원본128×96을3배(384×288), 화면 중심보다60px 위에 표시한다. 후미 이동/Monster만 Page.FullScreen으로640×480을채운다. 창가 위젤 대사의 소리는 유지한다. 기본내레이션소리0.85/.25, 위젤0.9/.28(피치/음량)이며기존SFX믹서를따른다. DialogueTextAnimationPolicy는실효공유설정을개별로복사해appearance/disappearance를끄고타이핑/음성/Behaviors를유지한다. 외부공유설정asset을직접변경하지않는다.

새 게임은 타이틀 → IntroScene 창가 반복 → Region_Train/all.trainq0/default → 탐색3초 후 이상 징후 → 오른쪽 후미 진입/이동 애니메이션1회/Monster 공개로 연결된다. NameInputUI 코드/전용 오브젝트와 참조를 제거했으며 주인공 정체성은 WIZZEL/한국어 위젤이다. 옛 SaveData.playerName 필드는 호환용으로 남지만 FromSaveData는 기본 이름을 사용한다. 동료/NPC 표시명과 런타임 교체 API는 유지한다. IllustratedStoryAsset/Player는 그림 표시 Presentation이고 기존 DialogueData/DialogueManager/DialogueUI를 재사용한다. 애니메이션은 원본 클립에서 미리 추출한 Frames(Sprite/Time)를 직접 선택하며 숨김 Renderer/런타임 SampleAnimation에 의존하지 않는다. 시나리오 문법·Action Sequence 원본을 대체하지 않는다. 사건 지연/위치 진입은 방 수명의 PrologueTrainSequence가 소유하며 성공 시만 기존 전역 플래그를 확정한다.

## 플레이어 정렬 (2026-10-19)

플레이어의 기본/탐색 Sorting Order는 3 고정이며 Y×100 값을 더하지 않는다. 같은 Characters/order3에서 실제 Y깊이로 정렬한다. Player SpriteSortPoint는 Pivot, 상자는 바닥 루트의 SortingGroup3을 사용하며 자식 Visual의 내부 order는 보존한다. URP2D의 RendererData는 CustomAxis(0,1,0)를 명시한다(Default는 ProjectGraphicsSettings를 따르지 않음). 기존 전투 foreground boost5000과 복원은 유지한다. 기존 OverworldEnemy의 동적 정렬이나 샘플 생성기 Y베이크는 이번 수정 대상이 아니므로 새 동일 Order 소품 규칙과 혼용 범위를 확인한다. [상세](AIAssets/yjlim/feedback/2026-10-19-player-sorting.md).

## 아이템 상자 (2026-10-19)

`ItemPickupMarker`의 선택적 열린 외형 유지 모드를 상자에 사용한다. `InventoryEntryService`로 등록/전체 수량 여유를 검사하고 지급 성공 직후 기존 `AreaMarkerStateService` 완료 플래그를 확정한다. 대사는 보상 commit 이후의 표현이며 취소/누락으로 재지급하지 않는다. 기본 바닥 아이템은 기존처럼 숨긴다. 열린 상자는 실제 GlobalDataManager.FlagChanged로 이전 저장의 닫힘까지 복원하며 새 SaveData 필드나 개봉 자동저장은 없다. 복제한 상자는 Room ID/markerId를 각각 확인한다. [제작·검증](AIAssets/yjlim/feedback/2026-10-19-item-chest.md).

## 고정 UI 조명 분리 (2026-10-19)

`UIRuntimeGuard` → `UIViewportService`에 등록된 화면 UI는 최상위 Canvas의 `ScreenSpaceOverlay`/worldCamera=null로 렌더링한다. 일반 UI의 `FixedCanvasViewport`와 기존 HUD/대사 viewport가 640×480 콘텐츠/정수 배율을 보존하고 게임 카메라의 출력 경계만 따른다. 화면 좌표 변환 카메라(null)와 경계 조회용 `GetOutputCamera`를 혼동하지 않는다. 루트 Default 정렬 레이어와 기존 창별 order를 사용하며 대사/레터박스/페이드 우선순위를 보존한다. SafeAreaFitter는 공통 콘텐츠 안에서 동작한다. 독립 WorldSpace 말풍선·전조 VFX·월드 조명/Volume는 변경하지 않는다. [범위·검증](AIAssets/yjlim/feedback/2026-10-19-ui-light-isolation.md).

## 필드 메뉴 계약 (2026-10-19)

`UI/Runtime/FieldMenu/OverworldMenuUI`는 기존 패널 등록·프리팹 참조를 유지하는 진입점이다. `FieldMenuView` / `FieldMenuGrowthView`를 한 번 생성해 재사용하며 가방·장비·동료·일지 4탭을 표시한다. 출력640×480/실제창592×416이며 글자를 비정수 축소하지 않는다. 방향키·확인·취소만으로 주요 버튼에 접근하고 설정은 위 모달로 열어 포커스/초안을 유지한다. 동료 상태와 성장은 통합하고 `SkillTreeDefinition`의 실제 좌표·선행 조건을 사용한다. 능력치 미리 배분은 캐릭터 저장 인스턴스별 초안이며 `TryInvestGrowthStats` → `CharacterGrowthService.TryInvestBatch`가 전체 검증 후 한 번 확정한다. 실제 저장·재화·공용 레벨·개인 포인트만 표시하며 샘플 동료를 생성하지 않는다.

일지는 소지한 `RequiredItemId`와 표시 조건을 만족하는 `JournalEntry`만 읽는다. 퀘스트 진행/수락/보상을 만들지 않으며 목표 한 줄은 기존 `IsObjective` 플래그 투영이다. `FieldMenuTouchInput`은 메뉴/설정에만 Mouse/Touchscreen을 받아 같은 명령으로 전달한다. 공용 EventSystem의 포인터 액션은 계속 비연결이며 가상패드는 메뉴/설정 수명 동안 숨긴다. 안전 영역은 공용 출력 Rect와 `Screen.safeArea` 교집합의 정수 fit이고 숫자 키보드는 별도 위치 이동으로 회피한다. 기준 미만 화면은 축소 예외다. 배경은 열기전1회 저해상도 흐림이며 실패·초고해상도·리사이즈에는 어두운 막만 쓴다. [현재 사용법·검증 한계](AIAssets/yjlim/feedback/2026-10-19-field-menu-navigation.md).

## 픽셀 출력 계약 (2026-10-16)

고정 UI는 `UIViewportService`의 실제 화면/카메라 교집합과 정수 fit 배율을 공유한다. CanvasScaler는 런타임 ConstantPixelSize, Canvas.pixelPerfect=true; HUD/대사는 같은640×480 콘텐츠 Rect와 정수 시작점을 사용한다. 공유 카메라·타이틀·인트로 PPC는 Windowbox/Point、RenderScale=1、AA 없음이다. 기준 미만 출력의 축소는 가시성 예외이며 무손실이 아니다. 10/23부터 일반 Focus/그룹/Odin 확대도 최종 출력은 정수 허용 Lens로 맞춘다. 임의 연속 줌·회전과 원본 격자 보존을 동시에 보장하지 않는다. 전투는 위 고정 배율 계약이 우선한다. 렌더러 슬롯·조명·SDF 폰트는 유지한다. [최신 규칙](AIAssets/yjlim/feedback/2026-10-23-camera-pixel-stability.md) · [이전 진단](AIAssets/yjlim/feedback/2026-10-16-pixel-output.md).

## 챕터 1 공통 계약 (2026-10-16)

- **파티 공용 성장**: `PartyProgressionService` / `GlobalDataManager.GrantPartyExperience`가 파티 EXP와 최대 99레벨을 한 번만 계산한다. 개인별 저장 레벨/EXP는 호환용 투영이며, 능력치 투자·스킬 구매는 캐릭터별이다. 레벨업은 포인트만 지급하고 능력치를 자동 증가시키지 않는다. 초기화는 무료·반복 가능하되 전투 중 변경은 차단한다.
- **이벤트 합류**: 새 게임은 주인공 한 명이다. `PartyRecruitmentTrigger` → `TryRecruitPartyMember`가 안정적인 CharacterID로 중복 합류를 막고 현재 파티 성장에 맞춘 동료를 저장한다. 필드에는 위 동행 표시 계층을 사용하며, 실제 파티를 복제하거나 수동 전투 교대를 추가하지 않는다.
- **수동 저장 휴식**: `ManualSaveRestPolicy`가 독립 저장 복사본의 HP/AP와 일반 적 재등장 상태를 준비한다. 원자 파일 저장 성공 후에만 런타임에 반영한다. 취소·실패·자동저장은 회복/재등장하지 않는다. 보스·엘리트, 상자, 완료 플래그와 Encounter Memory는 초기화하지 않는다.
- **자동저장과 재도전**: 보스 승리와 연결된 진행 이벤트 성공은 자동저장을 예약하고 안정된 탐색에서 기록한다. 일반 잡담/모든 일반전 종료에는 적용하지 않는다. `BattleRetryCheckpoint`는 보스/도주 불가 필수전의 진입 전 상태를 메모리에만 보관해 아이템 등까지 복구하며, 일반 패배는 최근 저장을 사용한다. 이는 진행 중 전투의 디스크 저장이 아니다.
- **입력과 전투 결과**: 일반 방어는 65% 감소, 저스트가드는 피해 0 + AP, 회피는 피해 0이며 기본 AP 보상은 없다. `PlayerAttackQtePolicy`의 성공은 정상 피해 1.0, 실패는 감소 피해이며 QTE 자체의 AP/고유 자원 보상은 없다. 스킬·장비 고유 효과는 별도다. 전투 후 HP/AP는 유지하고 전투 상태만 정리한다.
- **저장 호환**: 스키마 v5의 공용 성장은 이전 파티 중 가장 진행한 멤버로 초기화하며 EXP를 합산하지 않는다. 투자·해금·장비·HP/AP를 보존한다. 기존 적 상태의 `RespawnOnManualSave` 누락은 false로 읽고 필드 적 등록 때 등급에 따라 갱신한다. 적의 `EnemyRank`와 `EnemyKind`는 독립 분류이며 기존 보스 데이터는 등급을 명시해야 한다.
- **대화와 정보 UI**: `FlagDialogueSelector`는 진행 플래그와 소지 아이템 조건을 함께 검사하되 조회로 아이템을 소비하지 않는다. X 대화 스킵은 확정된 노드 이벤트를 보존하고 선택/이름 입력에서 멈춘다. `JournalCatalog`의 목표는 플래그 조건으로, 일지는 소지한 기록 아이템과 플래그 조건으로 표시한다. 본문·선택지·화자에 번역 키를 연결한다.
- **전투 메뉴 정지**: Escape/패드 Start/모바일 일시정지로 기존 메뉴를 열며 C 반격과 분리한다. `BattleDelay`, 스킬 블록 경계와 애니메이션 완료 제한시간은 메뉴/설정 중 판정 진행을 멈춘다. 음악과 표시용 unscaled 장식은 별도다. 전투 진입/결과/대사/후열 선택 도중 메뉴를 중첩하지 않는다.

세부 범위와 미확정 콘텐츠는 [챕터 1 구현 계약](docs/superpowers/specs/2026-10-16-chapter01-foundation-design.md)을 따른다. 실제 기기 측정 없이 무발열·프레임 저하 없음·즉시 로딩 달성을 선언하지 않는다.

**Battle Main Dialogue / Submenu Dock**:
Main character conversations in an active Turn QTE battle reuse `DialogueManager` / portrait-capable `DialogueUI`, fitting the resting union of command and submenu rectangles through `BattleDialogueLayout`. `BattleMenuUI` owns both sibling panels' unscaled slide/fade and input lock; the dialogue dock stays stationary. A scenario-hidden menu is shown only for this exit motion, restored to hidden on close, then slides back when the scenario gate releases it. Newer turn/input state is retained. Party/resource/queue chrome remains visible during docked dialogue even in cinematic mode; camera and scenario ownership remain unchanged. Settings/pause/reserve modals still take priority. Global modal sorting and `dialogue.wait` completion/cancellation remain unchanged. Overworld geometry is restored after the scope. Non-modal `BattleSpeechBubble` is untouched. See [authoring and verification](AIAssets/yjlim/feedback/2026-10-08-battle-submenu-dialogue.md).

Dialogue slide defaults to0.3s. BattleHudViewport fills the extra bottom output margin without moving its640×480 content; the fill follows chrome/Canvas visibility. While docked, the lower letterbox is limited to the HUD bottom+20 reference pixels (hints2..18, party24+), while the top and authored thickness remain unchanged. A confirmed skill/item dispatch is distinct from accepting new input: the callback retains modal/presentation guards but does not reject its own Confirm-time input lock.

**Damage Display Batch**:
`BattleDamageBatchTracker` owns only presentation lifetime IDs, not combat rules. Ordinary actions, skill timelines, link counters and status boundaries close their own batches. `BattleDamageFeedback` carries the actual DamageElement and batch ID; status reactions use the synchronous skill context. `BattleDamagePopupPresenter` aggregates by runtime target + batch + normalized display type, displaying one numeric-only vertical row per type (Physical/Fire/Ice/Electric/Poison/Bleed), then holds 0.7 seconds after completion. Direct/status Electric amounts are each counted once. Character symbol colors still belong to party HUD, not damage numbers. See [damage totals](AIAssets/yjlim/feedback/2026-10-02-damage-totals.md).
_Avoid_: grouping by color, sprite or character data ID, accumulating across turns/targets, changing HP to reduce visual clutter, or timing long-skill lifetime from the interval between hits.

**Enemy Target Selection**:
`BattleEnemyTargetSelector<T>` owns weighted randomness and recent-four history per battle, shared by ordinary and single-target enemy actions. The turn service filters living active frontline actors and supplies a bounded missing-HP preference. Recent/last targets lose weight; with alternatives, three consecutive selections of the same actor are forbidden. This is not round-robin or player-input prediction. AoE/wait/preparation do not consume history and explicit scenario targets remain authored. `Action_SequentialMelee.ApproachEachTarget` is opt-in enemy movement: shuffled targets are each approached/defended/hit once at their own position, rather than being staged at center. See [ZEV authoring](AIAssets/yjlim/feedback/2026-10-02-zev-cold-electric.md).

**Elemental Buildup (누적 상태)**:
`ElementalStatusState` is actor-owned, battle-only state for Burn/Chill/Electric/Bleed/Poison, separate from legacy N-turn `StatusEffect` and `DamageElement`. `ElementalStatusProfile` is shared authoring data (null uses prototype defaults), never current values. `SkillData.ElementalApplications` applies on confirmed hits through `SkillContext.ApplySkillHit`; `StatusProcCoefficient` scales Electric reaction damage only. Frozen consumes Chill, skips one actor turn, and disables Z/X/C. Cleanse removes buildup/Frozen but retains the one-completed-action CC recovery fence. Status damage cannot trigger another status hit or shatter. Ordinary direct-hit notifications include their Electric reaction; standalone DOT/consumption notifies HP rules separately without duplicating popup/sound. `BattleStatsRevision` prevents buildup-only HUD events from rebuilding speed forecasts.
_Avoid_: chance-roll resistance for buildup, shared asset mutation, DOT through the invincible direct-hit path, per-hit Bleed action notifications, or rewriting legacy status IDs/enums. See [lab usage](docs/bunny-slime-battle-lab.md) and [implementation handoff](AIAssets/yjlim/feedback/2026-10-02-element-status-lab.md).

**Battle Speed Turn Schedule**:
`BattleSpeedTurnScheduler<T>` owns discrete remaining readiness inside the Turn QTE controller. SPD determines both order and long-run action frequency; this is not a speed-sorted round repeated at a queue-size boundary. Actors execute one at a time. The real turn and read-only forecast use the same calculation. Menus, QTE and presentation duration never advance readiness. Speed changes retain progress; newly active/revived participants start fresh, while surviving enemies keep their progress across a reserve wave. Ties use initial registration order. AP and status durations remain actor-turn based.
_Avoid_: consuming readiness to draw the HUD, rescheduling every frame, restarting everyone on a wave change, assuming all actors act once per round, or describing this as concurrent actions. See [speed-turn handoff](AIAssets/yjlim/feedback/2026-09-28-speed-turns.md).

**Live Speed Forecast / Haste**:
The Turn QTE service observes participant status changes, including duration refresh at unchanged SPD, without taking another turn or interrupting the active actor. Turn-boundary changes are batched; unchanged status-driven forecasts do not rebroadcast. Haste adds SPD percent without stacking and lasts through the target's next authored number of turn endings, excluding its already-active granting turn. Reapplication refreshes duration and retains the stronger rate. Forecasts include deterministic expiry of existing stat effects: CharacterBase.GetForecastSpeed reads start/end boundary counts through StatusEffect.IsActiveAfterTurnBoundaries, including the active actor's pending end. The scheduler increments only copied per-actor forecast counts and uses the same scheduling algorithm as execution. It never ticks live effects, damage, AP or status flags. Ordinary turn completion/known expiry must preserve the previously displayed suffix; new effects, dispels, deaths and new actions can change it. No reservation/free-turn rule extends expired Haste. BattleTurnQueueView owns bounded display-slot reuse and reference/occurrence matching; GridLayoutGroup owns slot placement, while BattleTurnQueueIcon owns unscaled DOTween visual movement/pulse. Identical visible forecasts do not restart feedback.
_Avoid_: assuming temporary SPD lasts forever in a forecast, gating refresh only on current SPD, ticking real effects to simulate a preview, animating layout-driven RectTransforms directly, identifying repeated turns by sprite or CharacterID, or retaining subscriptions/tweens after cleanup.

**Character Battle Resource**:
`BattleResourceDefinition` is shared authoring data; `CharacterBattleResource` owns one actor's battle-only steps. `SkillResourceEffect` is an optional bonus, not a skill availability gate. SkillContext snapshots/spends once at start and grants completion gain only on normal finish. Resource and QTE damage multipliers remain separate. ActorLargePortrait now displays the selected actor's resource stages, not a large portrait. Wizel pressure is the first authored resource; other companions' unique rules are not implemented yet.
_Avoid_: shared/static current steps, save-file pressure, character-name checks, per-hit spending, resetting on reserve disable, or mutating the definition in combat.

HubToHome is a Unity RPG project whose player-facing flow moves between overworld exploration, battle presentation, dialogue, and menu-driven actions.

**Overworld Menu Shell**:
The existing registered field-menu entry point. Its cached 640×480 surface has Bag, Equipment, Companion and Journal pages, shared progression/currency, party cards and a separate settings shortcut. Companion unifies status, drafted stat investment and the authored skill tree. The serialized legacy enum/anchors remain compatibility data, not the current navigation model.
_Avoid_: Options panel, pause menu, inventory window

**Category Window**:
The framed content area associated with Bag, Equipment, Companion or Journal. Settings opens the separate Config Panel rather than occupying a fifth page. Journal means collected reading items, not quests.
_Avoid_: Main menu, dialogue box

**Config Panel**:
The existing options surface for changing game settings. It is distinct from the Overworld Menu Shell even when settings are reached from an overworld menu category.
_Avoid_: Overworld menu

**Enemy Runtime Prefab**:
The single enemy prefab referenced by `EnemyData.Prefab` and reused for both Overworld placement and Battle spawning. Battle setup disables the cloned prefab's Overworld encounter behaviour instead of swapping to a second visual prefab.
_Avoid_: `_Overworld` / `_Battle` prefab pairs, mode-specific sprite scale forks, or separate animator controllers when one enemy identity is intended.

**Gameplay Camera Rig**:
The shared prefab that owns the one real Main Camera, Cinemachine Brain, 32 PPU / 640x480 Pixel Perfect Camera, gameplay Cinemachine Camera, Follow driver, Impulse components, and `CameraController`. Scenes override only scene-owned targets or bounds.
_Avoid_: scene-local copies of the gameplay camera hierarchy, a second real Camera for cutscenes, or code-driven camera transforms when Cinemachine Lens, Follow, Group Framing, Impulse, Priority, and Brain Blend already provide the behaviour.

The BunnySlime development lab explicitly overrides only its existing real camera's URP renderer to slot 1 (BunnySlimeLab_Renderer2D). PC/Mobile default renderer slot 0 and the shared rig prefab remain unchanged. Scene-owned Lab Lighting - Neon supplies Light2D and a priority-10 Volume profile; no global pipeline swap or runtime lighting controller is involved. See [lab lighting handoff](AIAssets/yjlim/feedback/2026-09-26-bunny-lab-lighting.md).

## Example Dialogue

Developer: "Pressing C should open the Overworld Menu Shell, not the Config Panel."

Designer: "Then the player chooses Bag, Equipment, Companion, or Journal; settings has its own shortcut."

Developer: "The Companion page unifies status and growth. The field menu and Config Panel still have separate lifetimes."

**Seamless Battle Host**:
The shared scene-local composition root used by both Room-based seamless Battle and the dedicated BattleScene. Its prefab is the single source for one BattleManager, PositionManager, battle UI root, duplicate-root prevention, and emergency abort delegation; the dedicated scene keeps only scene presentation objects and explicit dedicated-mode overrides. BattleManager still owns combat rules and the shared seamless cleanup boundary.
_Avoid_: copying BattleManager, PositionManager, or battle UI into the dedicated scene, putting encounter result policy in the Host, leaving multiple Host roots active, or destroying individual child singletons as duplicate cleanup

**Seamless Entry Presentation**:
Default timing is 2s with 0.5s zoom. Input/movement is held without changing the overworld pose or camera composition until a black frame is displayed; only then enter Battle mode, notify IEncounterPreparationSource to hide the original NPC, and prepare the battle. Preparation overlaps the minimum black hold.
Ordinary seamless encounter entry uses Host-owned `BattleEntrySettings` and a reusable `SeamlessBattleEntryPresentation`: warning/zoom/diagonal cover → existing preparation under black → HUD reveal → existing scenario/module start. Dedicated BattleScene entry is unchanged. `IEncounterEntryPresentationPolicy`/DialogueEncounterContext allow opt-out. CameraController owns zoom tokens; BattleManager owns abort cleanup; the presenter owns only unscaled visuals and HUD restoration. See [entry handoff](AIAssets/yjlim/feedback/2026-10-02-battle-entry-reference.md).

**Primary Mode**:
The top-level playable space. Current planning treats only `Overworld` and `Battle` as Primary Modes.
_Avoid_: treating QTE, shooter, boxing, dialogue, cinematic, menu, or minigame variants as Primary Modes.

**Game Module**:
A replaceable rule/input/UI package that runs inside a Primary Mode. Examples include QTE combat, aim-shooter combat, boxing combat, bullet-hell defense, and town minigame interactions.
_Avoid_: assuming all Game Modules are turn-based or battle-only.

**Game Module Runner**:
The runtime seam used by Action Sequences and battle setup to switch, enter, exit, and start Game Modules through stable module IDs. `GameModuleRegistry` maps IDs to `IGameModuleRuntime`, and `GameModuleActionRunner` implements `IGameModuleActionRunner` for `module.switch` / `module.start`. In Battle, the runner instance must persist for the whole battle so `CurrentModuleId` survives across separate Action Sequence triggers.
_Avoid_: hard-coding QTE, shooter, boxing, or minigame transition branches inside `BattleManager` or inside one action adapter.

**Game Module Runtime Context**:
The small context object passed into `IGameModuleRuntime.Enter`, `Exit`, and `Start`. It wraps the current Action Execution Context, previous/target module IDs, Battle Session State reader, Battle Participant Command Runner, battle flag store, and Game Module event sink so a concrete Game Module can inspect battle state, request HP/MP changes, write shared battle facts, and report module outcomes without reaching into `BattleManager`.
_Avoid_: making each Game Module manually unpack broad Action Execution Context services or call `BattleManager.Instance`.

**Game Module Outcome**:
The result a Game Module reports when a module-local game loop or challenge completes, such as `victory`, `escaped`, `failed`, `timeout`, or a module-specific authored outcome. Concrete modules report these through `IGameModuleEventSink` / `GameModuleRuntimeContext.ModuleEvents`; Battle Event Rules can match `GameModuleCompleted` by module ID and optional outcome ID.
_Avoid_: hard-coding shooter/boxing/QTE completion branches inside `BattleManager` or making every module transition immediately decide whole-battle progression.

**Battle Game Module Presentation Controller**:
The battle UI seam that lets the active Game Module apply or clear module-specific presentation state, including whether legacy Turn QTE menu/targeting input is accepted. The current adapter is `BattleUIController` through `IBattleGameModulePresentationController`.
_Avoid_: letting a non-QTE module leave the old QTE menu, targeting cursor, or defense QTE input active by accident.

**Action Sequence**:
An authored sequence of actions for transitions, interactions, presentation, and gameplay beats. It must support sequential actions, parallel groups, waits, dialogue pauses, cancellation, and reuse from both Overworld and Battle.
_Avoid_: binding this concept only to skill execution or only to battle transitions.

**Action Director**:
The global runtime that executes, pauses, cancels, and coordinates Action Sequences regardless of the current Primary Mode.
_Avoid_: placing this responsibility inside `BattleManager`, `DialogueManager`, or a specific combat module.

**Action**:
An authorable unit inside an Action Sequence. Actions may be generic, such as moving an actor or fading UI, or specific, such as drawing a sword or switching to a particular combat module.
_Avoid_: forcing all actions into a tiny shared abstraction when author discoverability would suffer.

**Presentation Service**:
Globally callable systems for dialogue, cinematic, UI, camera, audio, and VFX. These may be invoked by any Primary Mode, Game Module, or Action Sequence.
_Avoid_: making dialogue or cinematic systems subordinate to a combat module.

Battle HUD uses the shared SeamlessBattleHost prefab at 640x480: six top-center queue icons, up to three stable party rows, fixed command/list/description panels, an actor portrait, and context-sensitive keyboard hints. Display-only WIZEL-first ordering uses the serialized lead CharacterData reference (legacy ID fallback) and must not reorder the real party or target indices. Acting-row background and target border are separate. Enemy turns retain the HUD while command input is locked. Horizontal movement switches tabs directly, vertical movement selects rows, and Z submits the selected command through the existing target-selection seam; there is no separate tab-entry confirmation. X cancels targeting back to the remembered tab/row. ATTACK/RUN preview rows add no confirmation step. CharacterData.BattleLargePortrait falls back to the existing battle portrait. CharacterBase status change events expose presentation data without changing effect rules. See [HUD handoff](AIAssets/yjlim/feedback/2026-09-26-battle-hud.md).

Battle UI tween lifetime stays local to each view. PartySlotUI owns vital/legacy portrait handles and its actual actor event subscriptions; production rows use stable colors and no selection punch. BattleMenuUI owns button color and delayed input resume handles; production menus do not slide or scale. Hide/rebind/disable/destroy kill only owned handles without completing gameplay callbacks. Captured Image/TMP references use Unity null guards. BattleUIController releases its slots/presentation handles and unbinds the actual BattleManager source. GameInput consumes menu transition presses (including held Z until a new press); battle keyboard cancel is X, matching other screens. Do not use global KillAll or change DOTween Safe Mode to hide lifetime bugs.

**Battle Session State**:
The battle-scoped truth that persists while Game Modules switch, including party/enemy survival, resources, status, current Game Module, phase progress, already-fired battle beats, and battle-scoped flags. The first concrete runtime class is `BattleSessionState`, currently focused on scenario identity, Primary Mode, opening/current module continuity, read-only participant snapshots bridged from the current `CharacterBase` runtime objects, and `Battle Session Flag` values. Runtime actions and Game Modules should read it through `IBattleSessionStateReader` from `ActionExecutionContext` rather than reaching back into `BattleManager`.
_Avoid_: storing battle-wide facts inside a single combat module.

**Battle Session Flag**:
A battle-scoped key/value fact that survives Game Module switches and Action Sequence batches but is not save-restored mid-battle. Examples include `phase.two`, `shooter.unlocked`, or `enemy.refused_qte`. Scenario actions write these through `IBattleSessionFlagStore` using `battle.flag.set` / `battle.flag.clear`; Game Modules read them through `IBattleSessionStateReader`.
_Avoid_: using Encounter Memory for temporary in-battle phase facts, or using module-local booleans for facts other modules must see.

**Battle Participant Command Runner**:
The narrow command seam exposed to runtime actions and Game Modules for requesting battle participant HP/MP changes. The first concrete adapter is owned by `BattleManager` because `CharacterBase` and existing battle events still own mutation; callers should resolve `IBattleParticipantCommandRunner` from `ActionExecutionContext` instead of touching `BattleManager.Instance`.
_Avoid_: letting shooter, boxing, QTE, or one-off Action adapters apply damage/heal/MP changes through their own private BattleManager branches.

**Save Scope**:
The game's save/load scope. Current planning saves outside battle only; battle results and Encounter Memory may persist, but an in-progress Battle Session State is not restored from a save.
_Avoid_: treating mid-battle state as save-bound.

**Progress Save** (updated 2026-10-16):
Manual slots 0–2 are written from save-object interaction, not an anywhere-save menu; successful storage commits the prepared party rest and normal-enemy respawn. Auto slot99 is requested on boss victory, successful scene progress sequences and configured progress/recruitment events, then captured after battle/results/dialogue/transition return to exploration. Plain NPC dialogue and ordinary victories do not automatically request it. Requests coalesce and are cancelled on new game/load. Global snapshots and codec normalization never mutate the caller. Config PlayerPrefs are independent of progress slots. See the chapter contract above and [save usage](AIAssets/yjlim/feedback/2026-10-11-settings-save-equipment.md).

**Battle Event Rule**:
The legacy battle-specialized authored rule represented by `BattleEventRuleData`. Runtime construction maps it once into the general Trigger Rule model so existing assets retain behavior while new work uses stable Scenario Event IDs and Trigger Conditions.
_Avoid_: embedding enemy phase changes inside one skill timeline or hard-coding them inside a specific combat module.

**Battle Event**:
A named gameplay beat emitted during battle, such as crossing an HP threshold, completing a skill, changing phase, defeating an enemy, or ending a Game Module.
_Avoid_: treating battle events as only C# callbacks or only UI narration.

**Battle Scenario Execution Gate**:
The battle-side Module that queues ready Battle Scenario Triggers, drains deferred triggers at explicit battle checkpoints, runs their Action Sequences through the Action Director, and blocks battle flow until those sequences succeed, fail, or cancel.
_Avoid_: starting scenario trigger coroutines directly from scattered BattleManager call sites.

**Encounter Memory**:
The save-bound remembered history of a specific encounter, enemy, or meeting context, used to vary dialogue, rules, and outcomes across first meetings, rematches, escapes, victories, and prior phase changes. Current runtime storage is `GlobalDataManager` encounter memory, serialized through `SaveData.EncounterMemory` as `EncounterMemorySaveData`. Battle setup/result flow uses `BattleEncounterMemoryRecorder` to seed `PerEncounterMemory` rules, increment meet count, remember fired beat IDs, and mark victory as defeated.
_Avoid_: treating every encounter with the same enemy data as stateless.

**Encounter Definition**:
The authored definition of a concrete battle or meeting context, including participants, opening module, presentation setup, Battle Event Rules, and outcome handling.
_Avoid_: putting one-off encounter flow entirely in Enemy Data or inside a combat module.

**Battle Scenario Data**:
The authored scenario layer for a battle sequence, especially when the battle changes modules, phases, backgrounds, dialogue, music, or victory return behavior.
_Avoid_: using Skill Data as the owner of whole-battle story progression.

**Scenario Source**:
The human/AI-readable YAML source for Encounter Definitions, Battle Scenario Data, Battle Event Rules, and Action Sequences.
_Avoid_: treating generated Unity asset serialization as the primary authored scenario text.

**Scenario Runtime Asset**:
The Unity-facing runtime representation synchronized from Scenario Source and consumed by game systems.
_Avoid_: making humans edit runtime asset serialization directly to author scenario flow.

**Sequence Maker**:
The Korean human-facing Unity editor surface for viewing, validating, reordering, inserting, and lightly editing Scenario Source-backed flow.
_Avoid_: exposing raw GUIDs, fileIDs, or managed reference internals as the normal editing experience.

**Sequence Maker Document Session**:
The editor-side Module that owns target-scoped Sequence/Battle command histories, saved checkpoints, and recovery-restored dirty state for the official Sequence Maker. A standalone save checkpoints one Sequence; a Battle save checkpoints that Battle and its contained Sequences without changing unrelated open documents.
_Avoid_: computing dirty state across every history the Window has ever opened, or marking every history saved after one target succeeds.

**Action Catalog**:
The discoverable catalog of Action grammar, Korean labels, parameters, examples, validation expectations, and runtime adapter ownership.
_Avoid_: adding actions that only exist as undocumented C# classes or one-off YAML keys.

**Action Library**:
The human-facing resolved collection of Action Catalog definitions used to search, understand, configure, validate, and preview Actions. Multiple owned catalogs may contribute to one library, but one stable Action ID has only one active contract.
_Avoid_: asking a normal Sequence Maker user to choose a catalog asset manually or treating an undocumented runtime adapter as discoverable authoring grammar.

**Action Block ID**:
The stable identity of one authored Action instance inside an Action Sequence. It remains the same when the block is reordered and is distinct from the Action ID that names the Action type.
_Avoid_: identifying authored blocks only by list index, display name, or current hierarchy path.

**Sequence Input**:
A typed value accepted by a reusable Action Sequence and supplied by its caller, triggering event, or supported execution context.
_Avoid_: duplicating an entire sequence only to substitute an actor, position, dialogue, duration, or similar authored value.

**Value Binding**:
A constrained reference such as `${input.actor}` that resolves an Action parameter from an explicit execution-context value. Runtime data stores it as `{"$bind":"input.actor"}` and supports only documented roots; it is not an expression language.
_Avoid_: evaluating arbitrary expressions, reflection paths, or scene-object lookups from scenario data.

**Sequence Call**:
The `sequence.call` Action that runs another Action Sequence by stable ID, binds only its declared Sequence Inputs into a child execution context, and propagates completion, failure, and cancellation to the caller.
_Avoid_: copying shared beats between scenarios or allowing recursive call graphs.

**Trigger Rule**:
The general `when -> do` rule that observes one Scenario Event, evaluates Conditions and execution policy, then requests an Action Sequence with typed target inputs. Existing Battle Event Rules are mapped compatibility inputs to this runtime rather than a second evaluator layer.
_Avoid_: requiring every new rule to add one central enum member and a new set of unrelated optional fields.

**Scenario Event**:
A stable, typed description of something that occurred in a domain system and may be observed by Trigger Rules, such as participant HP changing, a Game Module completing, or an interaction beginning.
_Avoid_: using an unrestricted global string event bus or letting scenario code own the domain behavior that emits the event.

**Trigger Condition**:
A typed predicate used by a Trigger Rule to compare Scenario Event payload, session state, Encounter Memory, or other explicitly supported read-only state.
_Avoid_: embedding arbitrary code expressions in scenario data.

**Execution Session**:
An observable run of an Action Sequence with current Action Block ID, lifecycle, pause, step, cancellation, completion result, and diagnostics.
_Avoid_: treating sequence execution as a fire-and-forget coroutine with no author-facing state.

**Action Sequence Live Context Source**:
A runtime scene owner that can provide an Action Director, Action Execution Context, coroutine host, priority, and human-readable label for Sequence Maker Play Mode testing. `BattleManager` and `SceneActionSequenceTrigger` are the first two adapters through `IActionSequenceLiveContextSource`; the editor discovers the Interface and does not branch on concrete Primary Modes or scene bridge types.
_Avoid_: adding `if battle`, `if overworld`, or one-off minigame lookups inside Sequence Maker playback code.

**Preparation Run**:
An editor-only fast-forward of blocks preceding a selected start block, used to establish required preview or test state without replaying their full presentation. Production gameplay does not use Preparation Run semantics.
_Avoid_: applying real save, reward, or scene-transition side effects while preparing an editor preview.

**Cinematic Stage**:
A scene-local, offstage presentation rig that owns a temporary Cinemachine camera, named subject bindings, and reusable Cinematic Shots. It prepares under SceneLoader's black reveal gate, takes camera ownership only while a sequence requests it, then releases back to the normal gameplay camera.
_Avoid_: hiding gameplay actors with `SetActive`, duplicating scenes for short transitions, or making culling layers the default way to separate a cinematic from gameplay.

**Cinematic Shot**:
A reusable ScriptableObject definition inside a Cinematic Stage: camera rail subject, orthographic lens motion, and parallel subject motions. It is invoked from an Action Sequence through stable `stage` and `shot` IDs.
_Avoid_: embedding individual transform tween values in a scene trigger or treating Timeline as mandatory for every camera movement.

**Scene Action Sequence Trigger**:
A scene-local bridge that prepares a Cinematic Stage while `SceneLoader` holds the screen covered, then starts a standalone Action Sequence after `SceneRevealCompleted`. A successful run records its optional one-shot `GlobalDataManager.eventFlags` value and, by default, requests an auto save for stable exploration. Failed/cancelled runs do neither; `_autoSaveOnComplete` can be disabled for non-progress presentation. Checkpoint retry skips scene-entry playback. Only completion facts persist, never the running sequence.
_Avoid_: writing scene-intro completion into Battle Session State or starting presentation before its camera/subjects are prepared.

**Turn QTE Combat Module**:
The migrated Game Module for the existing QTE/turn battle. `turn_qte` starts through the Game Module Runner, and `BattleTurnQteGameModuleRuntime` delegates to `IBattleTurnQteModuleController`. `BattleTurnQteModuleControllerService` owns QTE lifecycle, turn calculation/advancement, player/enemy turn begin, player input/actions, enemy action, defense resolution, action completion, inactive-module guards, and pending QTE cleanup. It uses `IBattleTurnQteHost` to access BattleManager's serialized settings, event bridges, and presentation helpers without scene or asset migration. The host shares one `BattleLinkCounterService` with the turn controller and scenario skill runner.
_Avoid_: adding new QTE state/input/action branches directly to battle setup or bypassing the controller when switching modules.

**Defense Judgement Pipeline**:
The shared defense contract. Realtime combat uses persistent Z guard (damage multiplier0.35), fresh Z Just Guard (zero damage and base AP2), X dodge (zero damage without default reward), and C Link Counter. Counterable6 and CounterOnly7 both require C; serialized enum values are preserved. Success windows default Z0.12/X0.22/C0.18s and late grace0.03s, separate from the preserved authored cue duration. The first cue-period attempt commits even when early, wrong, ambiguous or not rearmed; later key switching cannot replace it. Z/X/C share actor-owned0.18s rearm, capped to half short-attack duration. A held Z remains ordinary guard across impacts, while consumed menu-confirm holds remain blocked. Input event timestamps determine precision. Enemy motion/animation continues during defense and grace; damage has one consumer. QTEManager owns execution, PlayerController owns actor input, and DefenseJudgementPolicy owns results. Normal/legacy/scenario enemy action roots share one nested, generation-safe AP budget (base maximum6 per ally per enemy action); equipment/resource rewards remain separate. Attack QTE auto preference affects offense only. Reactions drain after final impact, C successful choreography and paired return remain separately owned.
_Avoid_: recomputing defense success from raw input and grade inside basic attacks, skill blocks, UI, or character controllers.

Melee assistance (2026-09-26, cue policy updated 2026-10-07): `WithBattleAssistance(..., parryFromPreparation: true)` raises allowed Z to max(authored perfect, authored dodge/preparation, 0.40s after difficulty scaling). START/ping and fresh-Z acceptance begin together. X is at least as wide as Z. `StartBattleDefenseWindow.useMeleeParryAssistance` defaults true; projectiles/ranged fallback opt out of the wider melee Z window, not the common cue contract. Counterable/dodge-only restrictions remain. Accepted melee Z briefly tints the defender; actual parry animation/VFX/reward still resolve once at impact. Preheld Z remains ordinary guard.

The per-impact cue is visible only during `GetActiveCueWindow` (normal Z, dodge-only X, Counterable min(X,C)). The wider X window can stabilize the camera early but no longer spawns a dim, non-actionable cue. X is not narrowed. QTEManager owns pooled cue cleanup and uses its SeamlessBattleHost default prefab unless a skill overrides it. Telegraph.aseprite START and its assigned ping begin once at the common window; SynchronizeToImpact samples the same defense clock. Preserve prefab scale, actor sorting order -1 and pool reset. Short windows compress playback, never judgement. ImpactCueLeadTime remains ignored compatibility metadata. BattleImpactTiming slows only the owning attack for0.10s at0.5 rate; no global time change. Adjacent defense→projectile/sequence-melee blocks share their impact clock; each target/impact consumes fresh input. Pause/cancel/destruction restore owned cue/motion. New defense blocks default additional preparation off; old explicit/next-turn previews are compatible. No universal rhythm-preview mode.

**Reserve Decision** (2026-10-07):
After every active ally is defeated and living reserves exist, BattleManager waits for BattleReserveChoiceUI. Continue (default) starts the existing fade/promotion/offscreen entry; GiveUp enters GameOver without activating reserves, overriding an encounter's training return-on-defeat policy. Cancel selects Continue but never commits abandonment. Choice/transition generation owns input suppression and cleanup; the lab does not reopen its menu behind GameOver. No manual swaps or disk save of the choice.

**Equipment / Persistence Validation** (2026-10-07):
EquipmentLoadoutService.GetEquipped/CanApply shares slot/character restrictions across read-only projection and runtime. Explicit equip/unequip marks initialization to preserve the player's choice. Base resistance values are multipliers (1 normal,0 immune); equipment values are additive deltas. Non-finite modifiers are ignored; invalid base multipliers become1, negative final multipliers clamp0. Missing status maps are safe. SaveManager blocks writes during battle/reserve choice/recovery. SaveDataCodec rejects non-finite coordinates for backup recovery rather than inventing a spawn. Equipment-aware HP/AP caps remain; schema v5 adds shared party progression and explicit manual-save respawn policy while preserving legacy saves.

Telegraph playback uses the actual START AnimationClip and its length, sampled directly from the defense clock rather than seeking a zero-speed Animator state. The Animator is disabled only for leased playback and restored on pool return; standalone prefab playback is not frozen in Awake. Default and lab skill pivots are Center, world offset is zero, and Telegraph prefab scale is restored to 3. Legacy fallback controls remain serialized but hidden from the current Inspector.

Ordinary battle stages the opposing ally/enemy pair at `PositionManager.CenterPos` ±1.25 units over 0.28s. `BattleDefenderPresentationScope` owns both temporary positions and the PlayerController defense anchor; default Approach/OriginalPos skill moves use these staging positions until the action ends. X dodge retreats world-left 1.5 units (48px at 32 PPU), then returns to staging; hurt recoil stays at most 0.1875 units. Completion animates both home returns; cancellation/module exit/disable restores synchronously. Sequential melee stages its actual target; AoE stages one representative defender without changing the damage roster. Support skills/items do not move an enemy. Central approach preserves each actor's Z and uses CenterPos ground Y. Authored special Top/Back/Center moves remain explicit. Player returns stay on the ground. Camera framing uses finite 0.16s DOTween transitions and a 0.18s return, overview ×0.70 zoom and multi-target visibility margins. No constant drift/breathing zoom; meaningful displacement (0.75 units) triggers reframing. Ordinary impacts use a small zoom response instead of repeated random Impulse/global hit-stop. QTE preparation holds the camera through resolution plus 0.12s grace. Shared GameplayCameraRig disables the legacy static policy; that toggle remains a compatibility option. Output PixelPerfectCamera keeps 32 PPU / 640×480; a scoped CinemachineContinuousPixelZoom bypasses only orthographic quantization during action/reset and restores the original extension. Exact integer pixel mapping during fractional zoom/roll is not promised. Timeline leases remain exclusive.

**Battle HUD Viewport / Rhythm Skill**:
BattleHudViewport owns battle HUD/QTE in ScreenSpaceOverlay with a runtime 640×480 child root fitted to the output camera viewport. General fixed UI now uses the same overlay isolation through FixedCanvasViewport; world-tracked popups keep their camera. Existing anchors, sibling order and object references remain intact. The HUD does not inherit camera rotation/zoom/impulse. It renders behind global result/save/fade and hides only its Canvas during system modal/pause to preserve modal/input ownership. UIViewportService respects its specialized owner. Action_RapidStrikes is a PlayerOnly local SkillData block: five seconds, hits every 0.1s (50 hits), independent Z/X/C prompts every 0.65s with 0.45s windows. QTEManager.StartSkillInputStream owns input/presentation and a pausable elapsed clock, not damage; the block schedules damage separately. Results change damage multipliers, never timing. PlayerCharacter owns temporary rapid attack animation speed and restores it when interrupted. Action_AerialCrossSlash is a separate skill: approach/rise/camera 360-degree roll/one X prompt/three crossing strikes/return. The actor Transform never rotates; HUD/QTE stay upright. Rapid strikes do not roll the camera. Before an upright follow-up camera beat, normalize completed turns so 360-to-zero does not rewind. Existing TakeDamage/events/AP/turn/staging owners remain unchanged. Authored camera beats use the existing action token, never directly write Lens/Follow, and stale cleanup cannot affect newer shots. Owned movement/rotation/flip/animation speed/QTE state is restored on completion or cancellation.

**Link Counter (연계 반격)**:
A successful C defense against an explicitly Counterable enemy skill. The lab and authoring template stage this ultimate at PositionManager's center X, preserving ground Y/Z, before its telegraph. `Action_DefenseWindow` interrupts the remaining skill timeline and calls `BattleLinkCounterService`, which uses only the attacked living front-line defender. That defender immediately parries while recoiling world-left (0.85 units / 0.13s), lunges back in (0.16s), plays attack-ready and attacks in place, then returns together with the attacker directly to both home positions. The enemy is not knocked back by the parry. `PlayerController.PlayCounterParry` keeps ordinary parry VFX but does not let the color tween restore an obsolete defense anchor during the lunge. The counter owns its recoil camera hold and retargets the existing action shot without replacing the root token. Other allies and reserves stay put. Damage uses the defender's ATK (default 1.5 multiplier). This is a defense reaction, not an extra turn, AP reward, player skill execution, or action-bound status tick. The counter service owns paired return and cancellation cleanup; surrounding Turn QTE flow retains fallback enemy return, outcome checks, and turn advancement.
_Avoid_: making every strong attack counterable, spawning reserve actors for the reaction, recursively invoking normal player turns, or adding counter actions to global Scenario Source grammar.

**Aim Shooter Combat Module**:
The first registered non-QTE battle Game Module ID, `aim_shooter`. The current implementation is a presentation and input-ownership shell plus a testable combat-session core. It can be entered or started through `module.switch` / `module.start`, disables legacy Turn QTE input through the Battle Game Module Presentation Controller, and proves the default battle registry can host more than QTE. It can delegate lifecycle to `IBattleAimShooterModuleController`, and `BattleAimShooterCombatSession` handles the pure rule slice for target validation, participant damage requests, shot counts, and module outcome reporting. It is not yet the full mouse-aim input/projectile/VFX/UI gameplay loop.
_Avoid_: treating `aim_shooter` as a complete shooter implementation until its input, target, projectile, damage, outcome, and UI contracts are implemented.

**Skill Timeline Adapter**:
A compatibility Action adapter that invokes an existing `SkillData.ActionTimeline` through a narrow runner seam. `BattleSkillTimelineRunner` is the current battle-side adapter: it resolves scenario `skill` / `actor` / `targets` IDs against the active `BattleManager`, builds a `SkillContext`, and executes existing `SkillActionBlock` entries. It allows current QTE/skill blocks to be called from an Action Sequence without making Skill Data the owner of whole-battle scenario flow.
_Avoid_: rewriting or renaming existing `SkillActionBlock` classes just to connect them to Scenario Source.

**Enemy Attack Authoring Report**:
The editor-facing, read-only analysis of one `SkillData.ActionTimeline`, including cumulative block times, unsupported custom-block timing, missing references, defense-window ordering, and gameplay-camera safety. `EnemyAttackAuthoringAnalyzer` supplies the same report to the Odin Inspector and Project Content Validation.
_Avoid_: creating a second enemy-attack executor, duplicating validation per editor surface, or storing absolute camera/world coordinates in the attack data.

**BunnySlime Battle Lab**:
An explicit Edit Mode generator and fresh-Play-only development encounter, not a replacement for the passive BunnySlime asset or TestMap. Four entries cover full combat, guard/dodge, link counter, and automatic reserve promotion. It reuses common combat hosts and adds sample-only ordered HP-phase AI through `EnemyCharacter.SelectSkill`. `IEncounterDefeatPolicy` allows only opting-in seamless encounters to return to their source on defeat, after normal cleanup. No save-file I/O is attached. Canonical YAML remains inside the existing Scenario Source path policy. See `docs/bunny-slime-battle-lab.md`.

**Scenario Subject ID**:
A stable authored ID used by Scenario Source and Battle Event Rules to refer to runtime subjects such as enemies, actors, modules, UI targets, and positions. Enemy rules should resolve against `EnemyData.EnemyId`, not display names.
_Avoid_: using localized display names, Unity GUID/fileID values, or scene object names as the authored scenario identity.

**Project Content Validation**:
The editor-only, read-only scan that checks stable IDs, required references, Runtime Catalog membership, Battle Scenario links, battle prefabs, drops, skill blocks, and item rules through a structured `ContentValidationReport`. Optional portraits and icons are warnings; broken runtime contracts are errors. Repair commands remain explicit and separate from scanning.
_Avoid_: silently mutating assets during validation, treating every optional visual as a build-blocking error, or duplicating scenario contract logic outside `ScenarioCatalogValidator`.

**Camera Presentation Ownership**:
The rule that `CameraController` is the sole gameplay owner of its Cinemachine Camera tracking target, orthographic Lens, runtime Target Group, Group Framing, and Timeline lease. Battle and Overworld callers provide semantic targets or bounds instead of searching for cameras or writing camera transforms.
_Avoid_: calling `FindFirstObjectByType<CinemachineCamera>()` for gameplay ownership, storing world camera coordinates in authored actions, or resetting a newer Timeline/focus command from an older action.

**Camera Framing Settings**:
The Inspector-authored contract for multi-target framing: minimum and maximum orthographic Lens, framing size, damping, target radius, center offset, and shot style. Zero-initialized legacy serialization normalizes to battle-safe defaults.
_Avoid_: calculating midpoint and zoom independently in each attack, QTE, or skill block.

**Battle Camera Action Scope**:
The disposable, token-owned camera lifetime for one battle action. Opposing combatants use CameraController's reusable dynamic tracking target; QTE preparation holds final camera pose/lens until defense resolves. Same-side actions retain group framing. Restore only while this token is newest; module exit, disposal, run, battle end, seamless cleanup and BattleManager destruction cancel it. Pixel quantization remains active through Reset/Cancel/Timeline. Presentation Release clears its hold but not the separate battle viewport policy owner; Timeline/disable/session end explicitly unbind that policy. Temporary HUD hiding/pause retains the fixed Lens and skips center fitting only. Strict Pixel Perfect battle size remains locked across actions. Old defense holds cannot release a newer shot or Timeline.
_Avoid_: unconditional global camera resets in action cleanup or allowing an old action to overwrite a newer Timeline/focus command.

**Overworld Camera Binding**:
The shared Room/Map binding that registers the Player as `CameraController`'s default target and configures the Confiner on that same Virtual Camera.
_Avoid_: assigning Follow or Confiner to whichever Cinemachine Camera happens to be found first, because a Cinematic Stage camera may also be present.

**Authored Battle Lab Entry**:
`BattleLabEncounterEntry` stores the menu text, stable encounter ID, EnemyData, optional BattleScenarioData, and front-line weakening flag. The lab session only seeds its isolated party and starts/returns through the existing encounter service; it does not clone enemies or replace skill lists according to a menu index. Bunny integrated combat, ZEV presentation combat, and the separate reserve-wave exercise share this path.
_Avoid_: enemy-name checks, mode-index combat overrides, or embedding dialogue/camera choreography in the lab session.

**Enemy Windup Beat**:
`Action_EnemyWindup` is an EnemyOnly local SkillActionBlock: attacker closeup → shared attack-ready pose → return to the action's opposing-combatant frame. CameraController retains the existing action token; the following defense block owns the input/impact clock and freezes the camera. This is not a new global Scenario Action ID.
_Avoid_: stealing camera ownership from Timeline, using global Time.timeScale for the preparation, or starting defense during the zoom-out transition.

**Authored Battle Lab Entry**:
`BattleLabEncounterEntry` stores the menu text, stable encounter ID, EnemyData, optional BattleScenarioData, and front-line weakening flag. The lab session only seeds its isolated party and starts/returns through the existing encounter service; it does not clone enemies or replace skill lists according to a menu index. Bunny integrated combat, ZEV presentation combat, and the separate reserve-wave exercise share this path.
_Avoid_: enemy-name checks, mode-index combat overrides, or embedding dialogue/camera choreography in the lab session.

**Enemy Windup Beat**:
`Action_EnemyWindup` is an EnemyOnly local SkillActionBlock: attacker closeup → shared attack-ready pose → return to the action's opposing-combatant frame. CameraController retains the existing action token; the following defense block owns the input/impact clock and freezes the camera. This is not a new global Scenario Action ID.
_Avoid_: stealing camera ownership from Timeline, using global Time.timeScale for the preparation, or starting defense during the zoom-out transition.
