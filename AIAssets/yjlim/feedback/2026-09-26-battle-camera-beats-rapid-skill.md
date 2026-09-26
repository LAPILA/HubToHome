# 전투 카메라 정리 · 고정 HUD · 5초 연격

후속 수정: 이 문서의 연격 10타/5회 Z 및 카메라 회전 샘플은 [독립 QTE·별도 공중 회전 베기](2026-09-26-independent-barrage-aerial-skill.md)로 대체되었습니다. 카메라/HUD 구현 기록은 유지합니다.

이 문서가 앞선 `2026-09-26-dynamic-battle-presentation.md`의 카메라 정책을 대체합니다. 중앙 교전, X 후퇴, C 반격, 대사 안전 영역은 유지합니다.

## 요청과 구현 결정

사용자는 상시 흔들림 대신 짧고 명확한 이동/확대/축소, 카메라에 흔들리지 않는 기본 HUD, 아군 스킬의 회전감, 공격 중 입력하는 약 5초 연속 공격 샘플을 요청했습니다. 회전 대상은 카메라/캐릭터 어느 쪽이어도 괜찮다고 답했습니다.

1. 상시 sin 이동/호흡 줌을 없애고 한 번 시작하면 끝나는 DOTween 구도 전환을 사용합니다. 일반 교전에는 회전이 없습니다.
2. 기본 타격의 반복 Impulse/전역 히트스톱을 작은 줌 반응으로 대체합니다. 명시적으로 작성한 특수 카메라 충격 명령은 남깁니다.
3. 180→360도 회전은 새 샘플 스킬의 중간 구간에만 사용합니다. 프로젝트 전체 아군 스킬을 일괄 회전시키지 않습니다.
4. 기존 SkillData 블록과 QTEManager의 단일 실행 소유권을 확장합니다. 시나리오 Action/YAML 문법이나 전투 엔진을 추가하지 않습니다.

## 바로 확인할 곳

토끼 슬라임 실험실의 **위젤 · 압력 → SKILL → 압력 난무 · 5초**. 기존 4개 스킬에 다섯 번째로 추가했습니다. 씬 재생성은 필요 없습니다. 기존 Play 중이었다면 새 Play에서 실험실 파티 데이터를 로드해야 합니다.

- 스킬 자산: `Assets/_Game/Content/Maps/Development/BunnySlimeBattleLab/Data/Skills/rapid_slash.asset`
- 파티 연결: 같은 Data 아래 `Party/front.asset`의 DefaultSkills.
- 12 AP, 적 한 명, 5초 동안 10타. 2타마다 Z 안내(총 5회), 입력창 기본 0.32초.
- 성공 타격은 1.6배, 놓쳐도 기본 타격은 계속합니다. 한 타의 기본 ATK 배율은 0.45이며 기존 방어/저항/피해 이벤트 계산을 사용합니다.
- 약 5초는 공격 본체의 시간입니다. 중앙 접근/귀환은 기존 연출 시간이 별도로 붙습니다. 대상 사망/취소 시 조기 종료합니다.
- 스킬 메이커의 새 블록 **실시간 연격 · Z QTE**에서 시간/타수/주기/판정창/피해/좌우 이동 폭/회전 여부를 수정합니다. 적 전용/잘못된 수치는 제작 검사에서 오류로 표시합니다.

## 카메라와 HUD 조절

`GameplayCameraRig → CameraController → 전투 카메라 · 짧은 구도 전환`:

| 값 | 기본값 | 의미 |
| --- | --- | --- |
| 행동 줌 비율 | 0.70 | 작을수록 확대. 여러 대상의 화면 여백은 보존 |
| 구도 전환 시간 | 0.16초 | 추가 감쇠 없이 전환 종료 |
| 재구도 최소 이동거리 | 0.75 | 이보다 작은 모션은 따라 흔들리지 않음 |
| 타격 줌 강도 | 0.025 | 0이면 자동 타격 줌도 없음 |
| 특수 스킬 회전 강도 | 1 | 0이면 회전만 끔. 스킬 자체에도 회전 체크 있음 |
| 방어 후 구도 유지 | 0.12초 | 방어 전조~판정 구간 고정 유지 |

기본 행동 복귀는 0.18초입니다. 접근/중앙 배치에 따라 큰 위치 변화만 재구도합니다. 전역 화면 흔들림 설정 0은 자동 타격 줌과 특수 회전도 끕니다. PixelPerfectCamera는 계속 켜져 있습니다. 분수 배율/회전 중 정수 픽셀 매핑까지 보장하지는 않습니다.

HUD는 런타임에 BattleHudViewport가 ScreenSpaceOverlay로 분리합니다. 기존 하위 오브젝트의 앵커/순서/참조를 유지하는 640×480 루트만 추가하고, 출력 카메라의 viewport 크기/위치를 맞춥니다. 카메라 회전/줌/Impulse를 받지 않으며 QTE도 정방향입니다. 월드 데미지 팝업·말풍선은 기존 월드 카메라를 유지합니다. HUD는 저장/결과/로딩 UI 뒤 순서로 그립니다. 기존 ScreenSpaceCamera 설정창이 가려지지 않도록 모달/일시정지 중에는 HUD Canvas만 숨기고, 게임 오브젝트/코루틴은 유지합니다.

## 수명과 입력

- CameraController가 Follow/Lens/Dutch와 트윈을 소유합니다. 스킬은 기존 행동 토큰으로만 구도 변경/종료를 요청합니다. 오래된 스킬 cleanup은 새 행동/Timeline 카메라를 바꾸지 못합니다.
- 카메라 트윈은 일반 Update에서 갱신하여 Cinemachine LateUpdate 이전에 목표를 제공합니다. 방어/정지 구간은 트윈과 최종 카메라 상태를 함께 고정합니다.
- 회전 360도는 종료할 때 동등한 0도로 정규화하여 거꾸로 한 바퀴 복귀하는 현상을 막습니다. 취소/다음 행동/비활성에서는 소유 트윈을 제거하고 각도를 복구합니다.
- QTEManager.RhythmSkill은 공격 준비 → 공격 → 타격과 입력을 한 시계로 관리합니다. 앞선 메뉴 확정 입력/누르고 있기는 성공으로 넘기지 않습니다. 잘못 누른 뒤에도 입력창 안에 Z를 새로 누르면 성공할 수 있습니다.
- QTE 결과를 기다리는 추가 대기가 없습니다. 표시는 공격 시계로 진행하므로 입력 성공/실패가 전체 공격 길이를 바꾸지 않습니다. 정지 중 시계와 입력을 진행하지 않습니다.
- Action_RapidStrikes는 소유한 이동 트윈과 QTE만 정리합니다. 정상/취소/대상 사망/배우 파괴에서 위치·Idle·배율·카메라를 정리하며 실제 피해는 기존 TakeDamage/InvokeDamageEvent를 사용합니다. 중앙 복귀/턴 종료/AP는 기존 소유자에 남깁니다.

## 변경 파일

- 카메라: CameraController.BattleMotion.cs, CameraController.cs, BattleCameraActionScope.cs.
- HUD: BattleHudViewport.cs(+meta), BattleUIController.cs, UIViewportService.cs, DefenseQTEUI.cs.
- 스킬: Action_RapidStrikes.cs(+meta), SkillActionBlocks.cs, EnemyAttackAuthoring.cs, QTEManager.cs, QTEManager.RhythmSkill.cs(+meta), SkillMakerBlockEditor.cs.
- 실험실: BunnySlimeLabContentBuilder.cs, rapid_slash.asset(+meta), front.asset, Resources/HubToHome/GameContentCatalog.asset. 기존 자산 참조는 보존하고 스킬 참조만 추가했습니다.
- 검사 코드: CameraFramingTests, UIViewportServiceTests, QTEManagerCancellationTests, SkillContentValidationTests.
- 문서: 이 문서, 일일 기록/index, CONTEXT, battle.clinerules, 실험실 사용법. 새 MonoBehaviour는 자동 추가하므로 Inspector 수동 연결은 필요 없습니다.

## 검증 및 남은 확인

Runtime/Editor CLI 최종 빌드 오류 0, 기존 ConfigPanelUI CS0649 경고 2입니다. 변경/신규 텍스트 58개의 UTF-8 및 git diff --check를 통과했고, 샘플 GUID/파티·카탈로그 각 1회 등록/직렬화 필드/새 C# 파일의 중복 없는 Compile Include를 확인했습니다. 회전 토큰/취소, 고정 HUD/형제 순서/화면 비율, QTE 교체/취소/타격 시간, 샘플 연결/제작 검사 테스트를 추가했습니다. **Unity 테스트 및 Play는 실행하지 않았습니다.** 실제 화면에서의 회전 체감, 임포트된 공격 클립과 0.08초 타격 선행 시간의 어울림, 기기별 프레임/발열은 미검증입니다.

브랜치 `codex/gameplayEdit`. 커밋/푸시, 강제 Refresh/재임포트, 씬 저장 없음.

## 이번 하네스 교훈

- 2026-09-26: 신규 C# 추가 직후 기존 CLI csproj에는 Compile 항목이 없어 CS0246가 발생했습니다. 실제 파일 유무와 csproj Include를 먼저 확인한 뒤 누락 항목만 임시 반영하여 빌드에 성공했습니다. 이후 Unity가 자동 재생성한 csproj에서는 각 항목이 한 번만 존재함을 확인했습니다. 강제 Refresh 없이 확인하는 로컬 빌드에 적용하며, 자동 재생성 후 중복 Include를 넣지 않습니다.
- 같은 날 여러 파일 apply_patch가 실패라고 반환했지만 일부 파일은 적용되어 있었습니다. 전체 패치를 다시 실행하지 않고 rg/diff로 적용 여부를 확인하여 누락된 파일만 패치합니다. 기존 [동적 연출 기록](2026-09-26-dynamic-battle-presentation.md)의 부분 적용 교훈을 재확인했습니다.
- Windows rg는 쉘 확장에 의존하는 `폴더/*.cs` 대신 `rg -g '*.cs' 폴더`를 사용합니다. 파일은 rg --files로 확인한 뒤 읽습니다.
- 지정된 전역 메모리의 yjlim 프로필 경로는 이 장치의 쓰기 허용 범위 밖이고 존재를 확인하지 못했으므로 프로젝트 기록만 남깁니다. 인증/개인 정보는 포함하지 않았습니다.
