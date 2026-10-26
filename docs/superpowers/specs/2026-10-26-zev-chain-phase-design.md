# 제브 관통·사슬 2페이즈

2026-10-26 사용자 승인: 실험실 제브 HP3600→1800,50%에서1회 포커스→대사→공격 모션→자리/카메라 복원→네 모서리 사슬 UI.30% 추가대화는 제외한다.5스킬 중3종을 큰 좌우 관통으로 조정하고 전투 주 대사창은 검정, 필드 대화는 유지한다.

## 적 관통

아군 Action_ScreenPassSlash를 적에게 풀지 않는다. 기존 Action_DefenseWindow의 UseMeleePassThrough에 Local/ScreenWide 선택과 화면 여백·총 관통/접근 시간을 추가하고, BattleEnemyMeleePresentationScope의 이동 경로만 확장한다. QTEManager의 실제 공격 시계에서 대상X 교차 시점을 맞추고 기존 Damage/SequentialMelee만 피해를 계산한다. 빙섬·잔뢰 이연·뇌광 순회3종에 적용한다. 일반가드/저스트 성공은 접촉에서 멈추고 회피/실패는 화면 끝까지 관통한다. C 궁극기는 기존 중앙 반격을 보존한다. 기하/프레이밍 확보 실패는 기존 Local 경로로 대체한다.

## 페이즈와 자료

기존 lab.zev.phase2 Rule을50% crossing/alive>0/after_current_action/once session으로 유지한다. 포커스는 기존 gameplay_safe 카메라/정수PP 규칙을 따르며 임의 연속 줌/회전을 다시 켜지 않는다. 공격 pose는 표시 전용이며 피해는 없다. 카메라 복원 뒤 battle.flag.set으로 `lab.zev.chain_phase`를 활성화한다. 기존 phase3 dialogue 자산은 보존하되30% Rule/Sequence 실행은 제거한다. 현재 사용 대사와 유지된 기존 키의 한·영·일·중 문구를 실제CSV에 연결한다. 본편 Enemy_ZEV/원본 스킬/실제저장은 변경하지 않는다.

Scenario YAML/Runtime/소유 시퀀스 SourceHash를 함께 동기화한다. 새 전역 Scenario Action ID나 ActionLibrary 생성은 없다. 사슬은 규칙 판단 없이 설정된 플래그에 반응하는 표시다.

## 사슬·오디오

재사용 `BattlePhaseOverlayProfile`은 ActivationFlag, ChainSprite, StartSfx, 낮은음량·감쇠시간, UI폭/모서리 여백·스크롤속도 등을 소유한다. 실험실 조우에 선택적 PhaseOverlay 참조를 연결한다. 전투 SessionState의 플래그 변경 이벤트를 읽는 scene/encounter 수명 scope가 프로필 표시를 시작·종료한다. 적/스킬 이름에 따른 런타임 분기를 만들지 않는다.

Chain.png의 세로16×96 Point 그림을4모서리에±45도로 배치하고 반복이 끊기지 않도록 제한된 UI메시/반복 인스턴스를 재사용한다. 중앙 캐릭터·QTE·명령/조작 안내를 덮지 않으며 필요한 가장자리 영역만 사용한다. 최초 시작에 freesound_community-chains-30762를 SFX믹서의 낮은음량(약0.18)으로 재생하고 약1.2초 감쇠시킨다. BGM음량/믹서를 변경하지 않는다. 별도1개 AudioSource만 소유하고 반복재생하지 않는다. 전투준비 때 음원 LoadAudioData를 요청하되 파일/전투를 동기 블록하지 않는다.

전투 종료/F8/새 전투/씬이탈/소유자 비활성에서 이벤트 구독·자기Tween·UI·소리만 정리한다. 추가 카메라/RT/블러·영구저장 필드는 없다. 일시정지/모달 동안 스크롤과 감쇠 시간을 멈춘다.

## 대사 배경

BattleDialogueLayout 수명에서만 기존 배경Image의 색/상태를 캡처해 검정으로 바꾸고 해제 시 복원한다. 텍스트/초상/선택지와 필드/일반시네마틱 원본 배경은 건드리지 않는다. 향후 도트 프레임 교체를 막는 전용 재질이나 하드코딩 텍스처는 넣지 않는다.

## 검증

구현 중 명시 배우 포커스도 기존 전체참가자 viewport보정에 눌리는 경로를 실제 관리형 계산에서 재현했다. 포커스가 기본Follow대상과 다른 등록 배우이고 ordinary battle shot/Timeline이 없을 때만 그 배우의 bounds를 사용한다. 복원·기본대상·소실배우는 기존전체기준이고 PP/고정Lens는 유지한다. 승인된 제브 포커스를 충족하는 보완이며 새카메라/범위밖줌은 추가하지 않는다.

실제 경로/공격시계 기하·Source와 Runtime/키·플래그 이벤트·UI메시/소리상태·취소/종료/모달 관리형 검사와 전체 C# CLI를 수행한다. 셰이더/실제렌더·입력체감·광원·SFX음량과 GPU/발열은 Unity Play/실기기 미검증으로 분리한다. 강제 Refresh/전체 생성기/씬저장/실제저장I/O/커밋·푸시는 하지 않는다.
