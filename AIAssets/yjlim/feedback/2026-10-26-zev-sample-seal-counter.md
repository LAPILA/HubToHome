# ZEV 단독 샘플 · 반복 스킬 잠금 · C 반격

## 후속 수정: C 대기 뒤 접근·공격이 생략되던 원인

1초 대기를 `Sequence().AppendInterval(...)`만으로 만들었지만 현재 로컬 DOTween은 실행 항목·콜백 없는 빈 Sequence의 시작을 실패로 처리합니다. `SetAutoKill(false)`도 이 실패를 막지 못합니다. 반격 서비스가 실패한 대기를 취소로 판단해 원위치로 정리하면서 후속 접근·공격·돌파를 건너뛰었습니다.

대기만 실제 float Tween으로 교체했습니다. 기존 pause/취소/Target/카메라 소유권과 피해1회는 그대로입니다. 앞선 관리형 검사에서는 대체 Sequence가 시작 실패를 모델링하지 않아 놓쳤습니다. 실제 플러그인 DLL로 실패를 재현하고 대체 조건을 보강하자 수정 전4실패→수정 후10사례108단언0실패가 됐습니다. 실제 DLL Startup/완료 계약3검증, Runtime487/Editor390 컴파일 오류0/0도 확인했습니다. Unity Play/실제 프레임·화면 체감은 별도 확인 대상입니다.

## 바로 확인하기

1. 기존 `BunnySlimeBattleLab` 씬을 새 Play 세션으로 시작합니다. 입구는 **ZEV 하나만 표시**합니다.
2. Z로 진입하면 ZEV “전투 실험 계약, 진행하겠습니다.” → 위젤 “제가 이길 듯요. ㅎ” 뒤 기존 전투를 시작합니다.
3. HP50%의 기존 2페이즈 연출에서 ZEV “2페이즈에 돌입해야겠군요.” / “패시브로 당분간 일부 스킬은 못 쓸 겁니다.” → 위젤 “아오, 귀찮아.”가 나옵니다.
4. 사슬 flag가 켜지면 스킬 최대3개를 회색으로 잠급니다. **위젤의 실제 행동 2회마다 새3개로 교체**합니다. 이전 잠금은 누적하지 않으며 무작위 결과가 우연히 같을 수는 있습니다.
5. 잠긴 스킬은 탐색할 수 있지만 Z/클릭 확정 시 빨강 좌우 흔들림과 설명창의 사용불가 안내만 나옵니다. AP/턴은 소비하지 않습니다. 일반 공격·아이템·Z/X/C는 유지합니다.
6. C 성공은 큰 후퇴 → 위젤 정수2배 포커스와1초 대기 → 접근·공격 → 적 뒤 돌파 → 양측 복귀입니다. 이동에만 잔상을 사용합니다.

종료/F8/실험실 비활성/이탈에는 잠금을 해제합니다. 다시 시작하면 새로 준비합니다. 저장 파일은 읽거나 쓰지 않습니다.

## 제작자가 조절하는 곳

| 대상 | 수정 위치 |
| --- | --- |
| 토끼 UI 다시 켜기 | `BunnySlimeBattleLab.asset → Encounters → 토끼 → 목록에서 임시 숨김` 해제 |
| 잠금 발동/개수/주기 | 같은 자산의 `ZEV → SkillLock`: `ActivationFlag=lab.zev.chain_phase`, `SkillCount=3`, `RefreshAfterActions=2` |
| 개전/2페이즈 대사 | Lab의 `Data/Scenario/Dialogue_ZEVLab_opening.asset`, `Dialogue_ZEVLab_phase2.asset` 및 기존 `lab.zev.*` 번역 키 |
| 사용불가 번역 | `Assets/Resources/LocalizationTable.csv → battle.skill_locked` |
| C 후퇴·대기·돌파 | `PositionManager`의 C 반격 설정. 후퇴1.7, 대기1초, 돌파거리1.2/시간0.12초가 기본 |

토끼 항목/ID/적·스킬 데이터는 삭제하지 않았습니다. 메뉴는 표시 인덱스와 원래 조우 인덱스를 구분하므로 첫 표시 항목이 ZEV여도 토끼를 잘못 시작하지 않습니다.

## 코드 구성과 영향 범위

- `BattleSkillLockCycle<T>`: 중복 없는 일부 추첨과 정상 행동2회 교체만 소유합니다. 전체 정렬 대신 부분 Fisher–Yates를 사용합니다.
- `PlayerCharacter.SkillLocks`: 효과 소유자별 잠금 집합을 합쳐 가부를 판단합니다. 이전 scope 해제가 새 효과를 지우지 않으며 원본 Skills/SkillData/저장 목록은 바꾸지 않습니다.
- `BattleSkillLockScope`: 기존 전투 flag·행동 시작/완료·파티/종료 이벤트를 연결합니다. 2페이즈를 발생시킨 진행 중 공격은 첫 행동으로 세지 않습니다. 매 프레임 폴링은 없습니다.
- `BattleMenuEntryPresentation`의 같은 가부를 목록 회색과 입력이 사용합니다. 서비스도 메뉴 선택·대상 확정·AP 소모 전 경계를 검사합니다.
- `BattleSubMenu`/`OptionRowUI`: 잠금 변경을 이벤트로 갱신하고 행 포인터를 기존 확정 경로에 연결합니다. 빨강 흔들림은 자식 좌표/색을 복구하며 재사용·연타·Disable에 종료합니다. 새 입력 면은 행 생성 때 한 번만 만듭니다.
- 메인 탭 클릭의 기존 결함도 수정했습니다. ATTACK 표시 중 SKILL 클릭으로 이전 ATTACK 항목을 스킬로 전달해 입력이 잠기던 경로를, 다른 탭은 목록 전환만/현재 탭은 기존 확정으로 분리했습니다. 방향키 조작과 대상 선택은 유지합니다.
- `BattleLinkCounterService`는 기존 피해1회·압력 보상·AP/턴 비소모를 유지합니다. 치명타 뒤에도 소유 전투가 살아 있으면 돌파를 마무리하고 죽은 적에게 이동 모션을 재생하지 않습니다.
- `CameraController.BattleCounterFocus`는 C 성공의 명시적 수명에만 정수2배를 사용합니다. 기본 Lens 값/PixelPerfect/HUD는 유지하고 최신 명령·Timeline을 오래된 scope가 복원하지 않습니다. 일반 적 공격에 정지 준비를 다시 넣지 않습니다. 전투 시야 안전 보정을 명시적으로 끈 설정에서는 확대 scope를 만들지 않으며, 도중 OFF도 해제합니다. 현재 Lab 기본값은 ON입니다.

Scenario YAML/규칙/Action ID/SourceHash와 기존 공격 스킬은 보존했습니다. 변경한 직렬화 자산은 LabEntry 설정과 대사2개뿐입니다. `HiddenInMenu`의 기본false, `SkillLock` 기본null이므로 기존 다른 진입은 미사용입니다. 새 스크립트 meta6개는32hex/유일성을 검사했습니다. CSV의 기존 UTF8 BOM은 보존합니다.

## 검증과 한계

- 전체 CLI Runtime487/Editor390 오류0/0, 기존 경고229/0.
- 실제 관리형 잠금 cycle/배우 집합/scope/flag gate429단언+실행5소스계약 통과.
- 실제 UI 가부·행 포인터 메서드19단언, 메뉴 전환·거부·정상 인계12사례 통과.
- C 반격 서비스·카메라 focus/locked getter10사례108단언+3소스계약, 기존 카메라 비C11사례15단언 통과.
- 방어/QTE144사례, 패링 재준비479단언+8계약, 무정지 공격49사례255단언+7계약 통과.
- Lab세션12사례35단언, ZEV설정168단언(13대사52번역), Lab스킬/참조·번역799/1,762단언 통과. SourceHash `0f54764021249c9c5efabb9a3635903535eb2d9652189add5f5d63146f82b736` 동일.
- 난무/원본11파일 해시와 전체 CSV 키·5열 구조934단언도 통과했습니다. 이전 고정 바이트 prefix 검사는 승인된 대사 행 축소와 양립하지 않아 실제 parser 기반 구조 검사로 바꿨습니다.
- Unity native NUnit은 컴파일만 확인했습니다. 실제 GraphicRaycaster/CanvasGroup/모바일 터치, DOTween/Animator 스케줄, Cinemachine/PixelPerfect 최종 렌더와 체감은 Play 검증이 남습니다. 관리형 검사는 해당 엔진 경계를 대체합니다.

브랜치 `gameplayEdit`. 커밋·푸시·강제 리임포트·Play 전환·씬 재저장 없음. [설계](../../../docs/superpowers/specs/2026-10-26-zev-sample-seal-counter-design.md) · [구현 계획](../../../docs/superpowers/plans/2026-10-26-zev-sample-seal-counter.md).
