# Stable Battle Presentation Implementation Plan

> 승인된 전투 정리안을 현재 작업 트리에 직접 구현한다. 사용자 지시에 따라 서브 에이전트, Unity 재생/Refresh, 커밋은 하지 않는다.

**Goal:** Pixel Perfect 고정 구도에서 짧은 수평 리액션, 후한 Z/X/C 판정, 공격자만의 0.1초 감속과 동기화된 전조/피해를 제공한다.

**Architecture:** 기존 CameraController/방어 판정/QTE 실행 소유권을 유지한다. 일반 자동 카메라 연출만 차단하고 명시적 시나리오 카메라는 보존한다. 순수 공격 시간 변환을 판정 시계에 적용하며 공격자의 Animator만 소유 범위 내에서 감속하고 종료/취소/파괴 시 복구한다. 전역 Time.timeScale과 PixelPerfectCamera 설정은 수정하지 않는다.

**Tech Stack:** Unity 6, C#, Cinemachine, URP Pixel Perfect, DOTween.

## Chunk 1: 고정 구도와 움직임

- [x] CameraController에 기본 전투 고정 정책을 두고 자동 framing/legacy impact만 차단. 명시적인 TryFocus/TryImpulse는 유지.
- [x] PositionManager의 기본 접근을 수평으로 제한. PlayerController 회피/피격은 짧은 수평 이동, 가드는 제자리.
- [x] Pixel Perfect PPU=32, 기준 640×480, CinemachinePixelPerfect 참조를 그대로 보존.

## Chunk 2: 판정과 공격 시계

- [x] DefenseJudgementPolicy에 실시간 전투용 최소 여유와 순수 국소 감속 시간 변환 추가.
- [x] QTEManager: 공격 시계/실제 입력 시계 분리, X 준비 신호/정밀 핑, 이른 입력 재시도, 종료 시 감속 복구.
- [x] BattleTelegraphCue: 약한 준비 표시와 정밀 핑 분리, 풀링/취소 유지.
- [x] SkillActionBlocks 및 기본 적 공격 연결: 해당 공격자 Animator 수명 범위, 시나리오 취소 시 원상 복구. 기본 타격과 샘플 8타 연결.
- [x] 인접 방어→투사체/연쇄 근접을 같은 타격 시계로 연결. 자산 수정 대신 런타임 복사, 미리보기 시간 중복 제거.

## Chunk 3: 검증과 인수인계

- [x] 순수 C# 계산 검증: 감속 시간 변환, 경계, 난이도, 조기 입력, 짧은 공격, Z/X/C 역할. NUnit 103건, 조합 55,944건 통과.
- [x] `dotnet build Assembly-CSharp-Editor.csproj --no-restore -m:1 -nodeReuse:false -p:BuildInParallel=false -p:UseSharedCompilation=false -v:q` 직렬 실행. 새 파일은 임시 targets로 포함.
- [x] 이번 변경 범위 `git diff --check` 통과, 사용 문서/전투 계약/시나리오 참조/당일 기록 갱신. 전체 작업 트리의 기존 Aseprite/Telegraph YAML 공백 경고는 보존.
- [x] 실제 입력 체감·Pixel Perfect 화면·씬 전환은 Unity에서 사용자가 확인해야 함을 명시. Unity 재생/Refresh/씬 저장/커밋 하지 않음.
