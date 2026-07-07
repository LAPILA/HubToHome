# ZEV 시네마틱 전투 연출 정리 - Letterbox Clash Direction

## 한 줄 목표

ZEV 전투는 **일반 턴 전투가 시작되기 전에 영화식 레터박스가 닫히고, 카메라가 ZEV와 플레이어를 빠르게 잡은 뒤, 실제 HP는 깎지 않는 가짜 격돌과 대사를 통해 긴장을 만든 다음, 서로 벌어져 원래 BattleScene 전투로 들어가는 연출**이어야 한다. 페이즈 전환도 같은 언어를 반복해, **레터박스 → ZEV 포커스 → 강한 스킬 준비 대사 → 원래 전투 화면 복귀**로 읽히게 한다.

참고 감성은 다음과 같다.

- **림버스 컴퍼니**: 전투 중 컷인/대사/강한 공격 예고가 빠르고 과장되게 들어오는 느낌.
- **델타룬**: 전투 규칙이 대사와 연출로 자연스럽게 전환되고, UI/전투가 장면의 일부처럼 느껴지는 구성.

## 현재 프로젝트 기준 핵심 판단

현재 ZEV Architecture Clone 쪽은 이미 시나리오 Action Sequence 경로를 타고 있으며, 다음 액션들이 런타임 등록되어 있다.

- `cinematic.letterbox`: 위/아래 레터박스 표시/해제.
- `battle.camera.focus`: 특정 전투 참가자 포커스/줌.
- `battle.camera.reset`: 전투 카메라 복귀.
- `battle.actor.pose`: 전투 참가자 포즈/타격감 연출.
- `battle.actor.fake_attack`: **실제 HP를 깎지 않는 가짜 공격/격돌 연출**.
- `battle.actor.return_slots`: 플레이어/적을 각자 기본 전투 슬롯으로 복귀.
- `dialogue.wait`, `flow.wait`, `bgm.crossfade`, `screen.fade`, `module.switch`, `module.start` 등 기존 시나리오 액션.

따라서 이 연출은 `BattleManager`에 새 하드코딩을 추가하는 방향보다, **Scenario Source YAML의 opening / phase transition Action Sequence를 시네마틱 액션 중심으로 재구성**하는 방향이 맞다.

중요: 현재 `zev_clone_opening_clash`에는 `battle.skill.timeline`이 들어가 있다. 이 액션은 기존 `SkillData.ActionTimeline`을 실행하므로, 스킬 구성에 따라 실제 HP 변화나 QTE 정책이 섞일 수 있다. 사용자가 요청한 “attack!처럼 보이지만 진짜 HP는 줄어들면 안 됨” 조건에는 `battle.actor.fake_attack`가 더 적합하다.

## 플레이어가 느껴야 하는 장면 흐름

### 1. 전투 진입 직후 - Opening Clash

목표 감정: “이건 그냥 랜덤 인카운터가 아니라 보스가 나를 시험하는 장면이다.”

1. BattleScene 세팅과 UI 준비가 끝난 직후, 아직 본격 턴/QTE 입력을 받기 전에 컷씬 시작.
2. 위/아래 레터박스가 빠르게 들어온다.
3. BGM을 약간 낮추거나 긴장감 있는 BGM으로 짧게 크로스페이드한다.
4. 카메라가 ZEV를 빠르게 줌인한다.
5. ZEV 대사: “숨 쉬어. 아직 본게임은 아니야.” 같은 도발.
6. 카메라가 플레이어를 짧게 잡는다.
7. ZEV가 `battle.actor.fake_attack`으로 플레이어에게 순간 돌진한다.
   - 화면상으로는 `ATTACK!`, 타격 셰이크, 잔상, 멈칫 프레임이 있어도 된다.
   - **실제 HP/MP 변경 액션은 절대 호출하지 않는다.**
8. 플레이어도 바로 반격하듯 `battle.actor.fake_attack`을 실행한다.
9. 두 캐릭터가 2~3번 빠르게 주고받는다.
10. 중간중간 짧은 대사가 들어간다.
    - ZEV: “좋아. 반응은 하네.”
    - Player 쪽: 말 없는 반격 또는 짧은 시스템/캐릭터 대사.
11. 둘이 동시에 뒤로 빠지며 각자 전투 슬롯으로 복귀한다.
12. 카메라 리셋.
13. 레터박스가 열린다.
14. 기존 BattleScene/QTE/턴 전투가 정상 시작된다.

### 2. 본 전투 시작 - Normal Battle Return

목표 감정: “방금 컷씬은 프리뷰였고, 이제 실제 규칙으로 싸운다.”

- 컷씬 종료 후 `openingModule: turn_qte`가 시작된다.
- UI는 평소 Battle UI처럼 돌아온다.
- 이 시점부터 실제 공격/방어/QTE는 기존 전투 규칙을 따른다.
- Opening Clash에서 보인 공격은 모두 연출용이므로 HP 변화 로그가 없어야 한다.

### 3. 페이즈 전환 - Phase Shift / Strong Skill Telegraph

트리거: ZEV HP가 50% 아래로 내려간 뒤, 현재 스킬/액션 처리가 끝난 후.

목표 감정: “ZEV가 규칙을 바꾼다. 다음 공격은 위험하다.”

1. 현재 스킬 연출이 끝난 뒤 시나리오 게이트가 페이즈 전환 시퀀스를 실행한다.
2. 레터박스가 다시 들어온다.
3. 카메라가 ZEV를 크게 잡는다.
4. ZEV 포즈를 `charge`, `ready`, `strong` 같은 의미로 잡는다.
5. ZEV 대사:
   - “여기서부터는 네 차례가 아니야.”
   - “도망칠 틈은 줄게. 피할 방법은 네가 찾아.”
   - “강한 걸 보여 줄게.”
6. 강한 스킬 준비감:
   - BGM 2페이즈로 전환.
   - 화면 살짝 암전/플래시.
   - 짧은 wait로 텐션 유지.
7. 필요한 경우 `module.switch`로 `aim_shooter` 같은 다른 Game Module로 전환한다.
8. 전환 모듈 시작 전, 레터박스를 열고 카메라를 리셋한다.
9. 본래 전투/새 모듈 화면처럼 플레이 가능한 상태로 복귀한다.

## 권장 Action Sequence 구조

아래는 기획 의도를 보여 주는 YAML 초안이다. 실제 적용 시에는 Action Catalog와 DialogueData 매핑을 함께 맞춰야 한다.

```yaml
sequences:
  zev_clone_opening_clash:
    - cinematic.letterbox:
        mode: show
        thickness: 0.14
        duration: 0.18
    - bgm.crossfade:
        clip: zev_clone_shooter_loop
        duration: 0.35
    - battle.camera.focus:
        subject: zev_architecture_clone
        zoom: 3.2
        duration: 0.14
    - dialogue.wait:
        id: zev.clone.opening_clash
    - battle.camera.focus:
        subject: player_001
        zoom: 3.0
        duration: 0.12
    - battle.actor.fake_attack:
        actor: zev_architecture_clone
        target: player_001
        approach: 0.85
        lunge: 0.08
        hold: 0.04
        recover: 0.12
        impact: 0.75
    - battle.actor.fake_attack:
        actor: player_001
        target: zev_architecture_clone
        approach: 0.75
        lunge: 0.08
        hold: 0.04
        recover: 0.12
        impact: 0.65
    - dialogue.wait:
        id: zev.clone.opening_after
    - parallel:
        - battle.actor.fake_attack:
            actor: zev_architecture_clone
            target: player_001
            approach: 0.7
            lunge: 0.07
            hold: 0.03
            recover: 0.1
            impact: 0.85
        - battle.actor.fake_attack:
            actor: player_001
            target: zev_architecture_clone
            approach: 0.7
            lunge: 0.07
            hold: 0.03
            recover: 0.1
            impact: 0.85
    - battle.actor.return_slots:
        duration: 0.25
    - battle.camera.reset:
        duration: 0.25
    - cinematic.letterbox:
        mode: hide
        thickness: 0
        duration: 0.16

  zev_clone_phase2_transition:
    - cinematic.letterbox:
        mode: show
        thickness: 0.16
        duration: 0.2
    - bgm.crossfade:
        clip: zev_clone_phase2
        duration: 0.8
    - battle.camera.focus:
        subject: zev_architecture_clone
        zoom: 3.4
        duration: 0.18
    - battle.actor.pose:
        actor: zev_architecture_clone
        pose: charge
        duration: 0.28
        impact: 0.6
    - dialogue.wait:
        id: zev.clone.phase2_intro
    - screen.fade:
        mode: out
        color: black
        duration: 0.18
    - module.switch:
        to: aim_shooter
    - battle.flag.set:
        flag: zev.clone.phase
        value: shooter
    - screen.fade:
        mode: in
        color: black
        duration: 0.18
    - dialogue.wait:
        id: zev.clone.shooter_start
    - battle.camera.reset:
        duration: 0.25
    - cinematic.letterbox:
        mode: hide
        thickness: 0
        duration: 0.18
    - bgm.crossfade:
        clip: zev_clone_shooter_loop
        duration: 0.6
    - module.start:
        module: aim_shooter
```

## 대사 톤 가이드

### Opening Clash

- ZEV: “숨 쉬어. 아직 본게임은 아니야.”
- ZEV: “좋아. 반응은 하네.”
- ZEV: “이번엔 네 선택으로 버텨 봐.”

### Phase 2

- ZEV: “여기서부터는 네 차례가 아니야.”
- ZEV: “강한 걸 준비할게. 피할 수 있으면 피해 봐.”
- ZEV: “규칙을 바꾸자.”

### Shooter / 다른 모듈 시작

- ZEV: “총구를 맞춰 봐. 이번 규칙은 네 손에 달렸어.”
- 시스템: “전투 규칙이 바뀌었다.”

## 구현 우선순위

### 1단계 - 시나리오만 재구성

- `zev_clone_opening_clash`에서 `battle.skill.timeline`을 제거하거나 뒤로 미루고, `battle.actor.fake_attack` 중심으로 바꾼다.
- `cinematic.letterbox`, `battle.camera.focus`, `battle.camera.reset`, `battle.actor.return_slots`를 opening / phase transition 양쪽에 넣는다.
- 실제 HP 변경 액션인 `battle.participant.damage`는 opening clash에는 넣지 않는다.

### 2단계 - Catalog / Sequence Maker 표시 보강

- ZEV Clone Action Catalog에 시네마틱 액션들을 추가한다.
- Korean display name 예시:
  - `cinematic.letterbox`: “시네마틱 레터박스”
  - `battle.camera.focus`: “전투 카메라 포커스”
  - `battle.camera.reset`: “전투 카메라 복귀”
  - `battle.actor.fake_attack`: “연출용 가짜 공격”
  - `battle.actor.return_slots`: “전투 슬롯 복귀”
- Sequence Maker에서 사람이 봤을 때 “진짜 피해”와 “연출용 공격”이 헷갈리지 않게 설명을 분리한다.

### 3단계 - 실제 에셋 동기화

- Scenario Source YAML 수정 후 `HubToHome/시나리오/샘플/ZEV 아키텍처 복제 에셋 재생성` 또는 Sequence Maker의 `런타임 에셋 반영`으로 runtime asset을 맞춘다.
- DialogueData 문구도 위 톤에 맞게 조정한다.

### 4단계 - Play 검증

- 전투 시작 직후 opening clash가 `turn_qte` 시작 전에 재생되는지 확인한다.
- opening clash 중 HP가 줄지 않는지 확인한다.
- 레터박스가 끝에 반드시 사라지는지 확인한다.
- 카메라가 전투 기본 위치로 돌아오는지 확인한다.
- 페이즈 전환 후 `aim_shooter` 또는 목표 모듈이 정상 시작되는지 확인한다.

## 리스크와 주의점

- `battle.skill.timeline`은 기존 스킬 타임라인을 실행하므로, 연출용 격돌에 쓰면 실제 데미지/QTE가 섞일 수 있다.
- `battle.actor.fake_attack`은 연출용이므로, 체력 변화가 필요한 실제 승리/처형 장면에는 별도의 `battle.participant.damage`를 명시적으로 써야 한다.
- `battle.camera.focus`의 subject ID는 Scenario Subject ID와 실제 참가자 해석이 맞아야 한다. ZEV Clone은 `zev_architecture_clone`, 플레이어는 현재 예시 기준 `player_001`이다.
- 레터박스/카메라/배우 이동 액션이 실패하면 시나리오 실행 게이트가 실패 로그를 내므로, Play Mode에서 Console 확인이 필요하다.

## 최종 연출 문장

“전투가 시작되면 화면 위아래가 닫히고 ZEV가 카메라를 빼앗는다. ZEV와 플레이어는 실제 체력 피해 없이 눈 깜짝할 사이에 서로를 베고 밀어붙이며 대사를 주고받는다. 곧 둘은 각자 자리로 튕기듯 돌아가고, 레터박스가 열리며 평소 전투 UI가 살아난다. HP가 절반 아래로 내려가면 같은 영화 문법이 다시 들어온다. 이번에는 ZEV가 화면 한가운데서 강한 스킬을 준비하고, 규칙을 바꾸겠다고 선언한다. 짧은 암전 후 레터박스가 사라지고, 전투는 새 페이즈/모듈로 자연스럽게 이어진다.”