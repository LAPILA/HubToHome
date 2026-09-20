# Z 통합 시간제 방어 구현 계획

> 이전 구현 기록입니다. 2026-09-13부터 기본 방어는 [Z 지속 가드 / X 회피 / C 연계 반격](../../../AIAssets/yjlim/Patchnote/2026-09-13-zxc-defense.md)입니다. 아래 0.4초 제한은 호환 모드에만 해당합니다.

**Goal:** 기본 전투의 방어를 Z 유지 방어와 새 입력의 퍼펙트 패링으로 통합한다.

**Architecture:** 기존 DefenseJudgementPolicy → QTEManager → 피해 소비자 경계를 유지한다. 기존 요구 입력 데이터와 공격 스킬 Z/X/C 입력은 보존한다. 일반 방어만 피해 배율을 전달하고, 완전 피해 방지와 AP 보상은 퍼펙트에 한정한다.

**Tech Stack:** Unity C#, 기존 Input System, DOTween, 기존 전투 모듈.

## 승인된 동작

- 방어창 안 첫 Z 입력부터 최대 0.4초 유지 방어. 누른 채 시간이 지나도 갱신하지 않는다.
- 타격 전 기존 Perfect 구간(기본 0.12초, 난이도 배율 적용)의 새 입력은 퍼펙트 패링.
- 일반 방어는 키를 유지해야 하며 기본 피해 배율 0.5. 퍼펙트는 피해 0 및 기존 AP 보상.
- 타격마다 입력 기회 한 번. 다음 타격은 새 입력 필요. 격자, 새 반격 피해, 자산 일괄변경은 제외.
- 수치는 QTEManager Inspector에서 조정한다.

## 작업

1. 판정 결과에 Guarded와 피해 배율을 추가하고 순수 시간제 방어 상태를 정의한다.
2. 입력 유지 판독, 실제 타격 시점 판정, 취소 정리와 새 입력 경계를 연결한다.
3. 기본 단일/광역 공격과 스킬 블록이 동일 결과를 소비하게 한다.
4. 방어 안내/결과와 제한 시간 종료 안내를 기존 UI에 연결한다.
5. 경계 조건 테스트 소스와 정적 컴파일을 확인하고 규칙/사용 기록을 갱신한다.

## 검증 제한

사용자 방침에 따라 Unity 실행, Play Mode, 씬/프리팹 변경 및 Unity 테스트 실행은 하지 않는다. 기존 작업 트리 변경은 보존하며 커밋하지 않는다.

## 구현 결과 및 사용 위치

- 브랜치: `codex/gameplayEdit`. 커밋·푸시 없음.
- 별도 컴포넌트 연결이나 프리팹 재배치 없이 기존 `QTEManager`에서 기본 활성화된다.
- Inspector의 `시간제 방어`에서 `Z 통합 방어 사용`, `최대 유지 시간 (초)`, `방어 시 받는 피해 배율`을 조정한다. 퍼펙트 구간은 기존 `Defense QTE Windows`의 `_perfectWindow`; 개별 스킬의 timing override와 난이도 배율은 유지된다.
- 방어창에서 첫 입력만 유효하다. 0.4초 이전에 너무 일찍 눌렀거나 일반 방어 도중 놓으면 피격된다. 다시 눌러도 같은 타격의 시도를 갱신하지 않는다. 다음 타격은 새 입력이 필요하다.
- 기본 단일/광역 공격과 `Action_DefenseWindow`에 연결했다. 기본 광역 공격은 대표 아군의 창 하나로 전열 생존자의 피해를 처리하고 AP는 한 번만 지급한다. 기존 점프 전용 데이터도 통합 방어가 켜져 있으면 Z로 대응한다.
- 방어 UI는 `Z 방어 → 방어 중 → 방어 종료`, 결과는 `퍼펙트 패링 / 방어 / 피격`으로 표시한다. 공격 스킬 UI 복귀 시 원래 위치·글꼴 설정을 복구한다.
- QTE 취소·모듈 종료·Action handle 취소 시 뒤늦은 피해를 막는 생명주기 확인을 추가했다. 입력 대상 파괴 시 다른 대상의 입력으로 바꾸지 않고 QTE를 취소한다.

## 변경 코드

경로는 `Assets/_Game/Scripts/` 기준이다. 이전 작업 트리 변경은 보존했다.

- `Battle/Runtime/DefenseJudgementPolicy.cs`: 시간제 시도 상태, Guarded, 부분 피해 결과.
- `Battle/Runtime/IDefenseInputSource.cs`: 선택적 held-state 인터페이스.
- `Core/Runtime/GameInput.cs`: Z 유지 입력.
- `UI/Runtime/QTEManager.cs`: 기본 모드/수치, 타격 시점 판정, 한 시도 제한.
- `UI/Runtime/DefenseQTEUI.cs`, `UI/Runtime/BattleUIController.cs`: 새 방어 안내/결과와 스킬 UI 기준값 복구.
- `Overworld/Runtime/PlayerController.cs`: 입력 중복 소유 방지, 일반 방어 피드백.
- `Battle/Data/SkillActionBlocks.cs`: 부분 피해·퍼펙트 보상·취소 보호, 구 방식 피해 방지 보존.
- `Battle/Runtime/Services/BattleTurnQteModuleControllerService.cs`: 기본 단일/광역 공격 및 모듈 종료 보호.
- `Scenario/Runtime/Presentation/BattleSkillTimelineRunner.cs`: Action handle 상태 전달.
- `Battle/Tests/Editor/DefenseJudgementPolicyTests.cs`, `Battle/Tests/Editor/QTEManagerDefensePipelineTests.cs`, `Scenario/Tests/Editor/BattleTurnQteModuleControllerServiceTests.cs`: 시간 경계·해제·반복 입력·기존 모드·부분 피해/AP·취소 회귀 테스트 소스.

## 확인 결과와 남은 확인

- `dotnet build Assembly-CSharp-Editor.csproj --no-restore --nologo --verbosity quiet -m:1 -nodeReuse:false`: **오류 0, 경고 2**. 기존 `ConfigPanelUI.CategoryLabel`의 Inspector 할당 필드 CS0649 경고다.
- 새 테스트 소스의 잘못된 enum 이름 `QTEGrade.Fail`을 실제 `Miss`/`Good`으로 수정한 뒤 런타임과 Editor 코드를 함께 재컴파일했다.
- 관련 변경 `git diff --check` 통과, 수정 C# 파일 UTF-8 확인. Unity 테스트 실행·실제 전투 체감·기기 성능 측정은 하지 않았다.
- 수치는 초깃값이다. 연속 공격 간격, 일반 방어로 받는 전투 전체 피해량, 저프레임 입력 감각, 공격 모션과 기존 `TimeWindow`의 동기화를 직접 확인해야 한다. 기존 스킬의 `AttackAnimDelay`/`DelayAfter`는 그대로여서 공격별 연출 타이밍까지 일괄 재조정한 것은 아니다.
- 새 반격 HP 피해, 전용 방어 애니메이션 자산, 격자, 버프/장비 개편은 추가하지 않았다. 퍼펙트 보상은 기존 AP와 피드백이다.

## 운영 검증 메모

프로젝트 시나리오 스킬은 기존 문법을 유지하고 기본 방어/취소 계약만 좁게 갱신했다. 번들 `quick_validate.py`는 이 호스트 Python의 PyYAML 미설치로 실행되지 않았다. 환경에 패키지를 추가하지 않고 frontmatter·참조 경로·diff를 수동 확인했다. 이는 Unity 코드 컴파일 결과와 별개다.
