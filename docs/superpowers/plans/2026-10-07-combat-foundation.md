# 전투 기본 흐름 Implementation Plan

> **For agentic workers:** executing-plans로 현재 작업 트리에서 실행한다. 사용자 지시에 따라 서브 에이전트·Unity 실행·커밋 없이 진행한다.

**Goal:** 타격 신호와 판정을 일치시키고 후열 선택 및 장비/저장 경계를 안정화한다.

**Architecture:** 기존 QTEManager, BattleManager, EquipmentLoadoutService 및 SaveDataCodec 소유권을 유지한다. 선택 UI는 별도 컴포넌트로 분리하되 전투 관리자의 수명과 취소 세대를 따른다.

**Tech Stack:** Unity 6, C#, uGUI/TMP, Odin, DOTween, NUnit, dotnet CLI.

## Chunk 1: 전조와 후열 선택

- [x] QTEManager/DefenseJudgementPolicy 경로와 기존 회귀 검사를 읽고 공통 판정 시작점에만 전조를 표시한다.
- [x] `UI/Runtime/BattleReserveChoiceUI.cs`를 추가한다. 계속/포기 결과, 중복 입력/취소, Canvas 수명만 소유한다.
- [x] `Battle/Runtime/BattleManager.cs`의 교대 루틴 앞에 선택을 연결하고 중단·패배 경로를 정리한다.
- [x] `Core/Runtime/GameInput.cs`에서 선택창 동안 전투 입력을 차단한다.
- [x] `BattlePartyWaveRuntimeTests`, `QTEManagerDefensePipelineTests`에 승인 전 미승계·취소·판정 전 미표시 검사 소스를 추가한다. Unity 실행은 사용자 확인 대상.

## Chunk 2: 장비와 저항

- [x] EquipmentLoadoutService, PlayerCharacter, CharacterStatsProjectionService 및 기존 검사에서 슬롯/제한 검증 차이를 확인한다.
- [x] 공통 유효 장착 조회를 도입하여 런타임/표시 계산을 일치시키고 NUnit 회귀 검사를 추가한다.
- [x] CharacterStats의 null/비정상 저항 경계를 실제 소스 검사로 재현하고 필요한 보정만 추가한다.
- [x] EquipmentData/StatBlock Odin 설명을 한글로 정리한다. 필드명/기존 enum 값/자산 GUID 보존.

## Chunk 3: 저장 경계와 검증

- [x] SaveDataCodec/AtomicSaveStorage 기존 정책과 손상 데이터 정규화를 확인하고 회귀 검사를 보강한다. 공통 저장 진입 차단 및 실패 피드백도 정리.
- [x] 실제 순수 Runtime 소스를 링크한 `Library/CodexChecks` 검사 실행(실패→수정→통과):9,020조건.
- [x] `dotnet build Assembly-CSharp-Editor.csproj -m:1 -nodeReuse:false -p:BuildInParallel=false -p:UseSharedCompilation=false -p:RestoreIgnoreFailedSources=true -p:NuGetAudit=false -v:q` 검증. 신규 UI는 임시 CustomAfterMicrosoftCommonTargets include 사용. 오류0.
- [x] 변경 파일 인코딩·`git diff --check` 검증. AIAssets 당일 기록, 피드백, 도메인 규칙 갱신.

예상 결과: 컴파일 오류 0, 실행 가능한 순수 C# 검사 통과. Unity NUnit/Play 검사는 작성 여부와 실제 실행 여부를 구분하여 보고한다.
