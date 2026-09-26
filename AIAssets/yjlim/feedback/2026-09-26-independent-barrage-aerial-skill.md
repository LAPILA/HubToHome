# 압력 난무의 독립 QTE · 공중 회전 베기 분리

이 문서는 [앞선 5초 연격](2026-09-26-battle-camera-beats-rapid-skill.md)의 **10타/5회 Z/연격 중 카메라 회전** 설계를 대체합니다. 짧은 카메라 전환, 고정 HUD 및 기존 전투 규칙은 유지합니다.

## 바로 사용

회전 대상 정정: 공중 스킬의 360도는 캐릭터가 아니라 **카메라**입니다. Action_AerialCrossSlash의 캐릭터 회전을 제거하고 기존 CameraController 연출 토큰에 연결했습니다. 다음 줌에서 ±360도 각도를 정규화해 역회전하지 않습니다. 스킬 자산/생성기의 설명만 정정했으며 참조와 수치는 보존했습니다. CameraFramingTests에 양방향 한 바퀴 이후 정방향 전환 검사를 추가했습니다. CLI 오류 0/기존 경고 2, Play/Unity 테스트 미실행.

후속 위치 변경: 두 스킬의 QTE는 새 안내마다 전투 화면의 안전 영역 안에서 무작위 위치에 표시합니다. 같은 안내/결과는 고정하고 다음 안내가 직전 위치와 가까우면 반대쪽에 배치합니다. BattleUIController가 실제 턴 큐/하단 HUD와 화면 여백을 고려하며, 전투 규칙의 Unity 난수와 별도 난수 인스턴스를 사용합니다. 기존 SkillQTENode의 제작자가 지정한 좌표와 적 전조는 바꾸지 않습니다. 변경 파일: QTEManager.RhythmSkill.cs, BattleUIController.cs, BattleHudPresentationTests.cs. CLI 빌드 오류 0/기존 경고 2, Unity 테스트/Play 미실행.

토끼 슬라임 실험실에서 **위젤 · 압력 → SKILL**:

| 스킬 | 동작 | 입력 | 비용 |
| --- | --- | --- | --- |
| 압력 난무 · 5초 | 접근 후 0.1초 간격 50타, 짧은 지상 복귀 | 0.65초 주기로 Z → X → C 반복, 각 0.45초 입력창 | 12 AP |
| 공중 회전 베기 | 적 앞 접근 → 상승 → 카메라 360도 → QTE → 뒤/앞/뒤 3회 베기 → 복귀 | 공중에서 X 한 번, 0.45초 입력창 | 14 AP |

공격 중 상대 사망, 배우 비활성/파괴, 전투 종료/취소는 남은 타격을 중단합니다. 5초는 난무 본체이며 중앙 배치/접근/복귀 시간은 별도입니다. 실패해도 난무 타수나 공중 베기 동작은 줄지 않습니다. 난무 성공은 다음 QTE 결과까지 1.6배, 공중 성공은 뒤따르는 3타에 1.5배입니다. 샘플 밸런스는 각각 한 타 ATK 0.12/0.8이며 기존 방어·저항 공식을 거칩니다.

### 편집 위치

- `Assets/_Game/Content/Maps/Development/BunnySlimeBattleLab/Data/Skills/rapid_slash.asset`
  - 스킬 메이커 **고속 연격 · 독립 QTE**: 전체 시간, 타격 간격, QTE 표시 간격, 입력 허용 시간, 피해, 좌우 이동 폭.
- 같은 폴더 `aerial_cross_slash.asset`
  - **공중 회전 · 교차 베기**: 상승 높이, 앞뒤 거리, 접근/상승/회전/QTE/베기/복귀 시간 및 피해.
- `Data/Party/front.asset`의 DefaultSkills에 다섯 번째/여섯 번째로 연결, 공용 GameContentCatalog에도 등록. 기존 스킬/캐릭터 원본 DB/씬/Animator 자산은 변경하지 않았습니다. 새 Play에서 파티 데이터를 로드하면 됩니다.

## 구현과 수명

- `QTEManager.StartSkillInputStream`은 단일 QteExecution 소유권 안에서 입력/표시/활성 시간만 관리합니다. 타격 횟수는 알지 못합니다. 공격은 Action_RapidStrikes가 ElapsedSeconds로 별도 계산합니다. 같은 일시정지 기준만 공유하며, 성공/실패가 시계나 다음 타격 시간을 바꾸지 않습니다.
- 타격 시각은 0.1, 0.2, …, 5.0초입니다. 저프레임으로 여러 타격이 밀리면 피해 타수는 따라잡고, 애니메이션/VFX는 한 프레임에 중복 재생하지 않습니다. VFX는 추가로 약 0.2초 간격으로 제한합니다.
- PlayerCharacter는 고속 연격에서 기존 attack 상태를 직접 재시작하고 클립 속도를 타격 주기에 맞춥니다. Ready/Attack 트리거를 매 타격마다 쌓지 않습니다. 다른 전투 애니메이션/비활성/스킬 종료 시 자신이 소유한 속도만 복원합니다.
- 공중 베기는 DOTween 위치를 소유하며 적을 통과하는 시점에 피해 이벤트를 보냅니다. 상승 뒤 CameraController의 기존 행동 토큰으로 360도 카메라 회전을 요청합니다. 캐릭터 Transform은 회전하지 않습니다. 회전 뒤 일반 줌으로 전환할 때 각도를 정규화해 역회전을 방지하고, HUD/QTE는 정방향을 유지합니다.
- 취소 시 소유 QTE/트윈을 종료하고 위치·카메라 각도·좌우 반전·애니메이션 속도·피해 배율을 복구합니다. 사망 상태에 Idle을 덮어쓰지 않습니다. 중앙 배치/최종 귀환/AP/턴 종료는 기존 소유자가 담당합니다.
- 메뉴 확정 프레임과 누르고 있던 키는 QTE 성공으로 넘기지 않습니다. 틀린 키를 누른 뒤에도 창 안에서 올바른 키를 새로 누를 수 있습니다. QTE 입력이 전투 방어 버퍼로 중복 전달되지 않는 기존 IsSkillQteActive 차단을 사용합니다.

## 수정 파일

- 런타임: Action_RapidStrikes, 신규 Action_AerialCrossSlash(+meta), PlayerCharacter, QTEManager/RhythmSkill, SkillActionBlocks.
- 제작 도구: EnemyAttackAuthoring, SkillMakerBlockEditor, BunnySlimeLabContentBuilder.
- 데이터: rapid_slash.asset, 신규 aerial_cross_slash.asset(+meta), front.asset, GameContentCatalog.asset.
- 검사: QTEManagerCancellationTests, SkillContentValidationTests. 50타 시각/저프레임 누적, 입력 스트림 취소/교체, 비정상 수치, 세 키, 파티/카탈로그 연결 검사를 추가·갱신.
- 문서: CONTEXT, battle.clinerules, 실험실 사용법, 당일 기록/index/이 문서.

## 검증과 주의점

Runtime/Editor CLI 최종 컴파일 오류 0, 기존 ConfigPanelUI CS0649 경고 2입니다. 변경/신규 텍스트 64개의 UTF-8, git diff --check, 새 GUID 유일성, 두 스킬의 파티/카탈로그 각 1회 연결을 확인했습니다. **Unity 테스트와 Play는 실행하지 않았으므로 실제 애니메이션 프레임, 회전 중심, 손맛 및 기기별 성능은 화면 확인이 필요합니다.** 새 블록도 스킬 메이커와 제작 검사에 연결했습니다. 기존 연격의 StrikeCount/QteEvery/CameraRoll 직렬화 필드는 숨김 호환용으로 남겼으며 실행에서는 더 이상 사용하지 않습니다.

브랜치 `codex/gameplayEdit`. 커밋/푸시, 강제 Refresh/재임포트, 씬 저장은 하지 않았습니다.

## 하네스 교훈 — 2026-09-26

- 다중 apply_patch가 실패를 반환해도 앞선 파일 변경은 반영될 수 있습니다. 이번에도 제작 검사/공중 블록만 반영되고 테스트는 미적용된 상태를 rg로 확인한 뒤, 미적용 파일만 별도 패치하여 컴파일했습니다. 재시도 전 실제 파일을 확인합니다.
- Unity 로컬 CLI 프로젝트에 새 C#의 Compile Include가 없으면 누락 파일만 임시 추가합니다. 이후 자동 재생성본에서도 해당 항목이 한 번만 존재하는지 확인했습니다. csproj는 자동 생성/무시 파일이며 Unity Refresh를 강제하지 않습니다.
- Windows PowerShell에서 rg에 추측한 경로나 `경로/접두사*`를 파일 인자로 주면 읽기/검색이 실패합니다. 실제 파일은 `rg --files`로 찾고, 검색은 존재하는 디렉터리와 `-g '접두사*.cs'`로 수행하여 확인했습니다.
- 지정된 yjlim 전역 메모리 경로는 현재 허용된 쓰기 경로 밖이므로 이 프로젝트에만 기록합니다.
- 후속 QTE 배치 작업에서 바깥 작업 폴더 전체를 검색해 이전 문서 출력 임시 폴더 접근 오류가 발생했습니다. 확인된 Unity 저장소 하위로 작업 디렉터리/검색 범위를 제한하여 필요한 파일을 찾았습니다. 프로젝트 검색은 출력/임시 폴더까지 포함하는 상위 경로가 아니라 Unity 저장소와 관련 Scripts/Content로 제한합니다.
