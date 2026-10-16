# Player Directional Idle Implementation Plan

> **For agentic workers:** Follow the existing PlayerController and Animator contracts; keep the user's Aseprite and scene changes intact. Separate asset inspection and regression-test authoring from runtime edits.

**Goal:** 위젤이 이동을 멈추면 마지막으로 바라본 상하좌우 방향의 대기 애니메이션을 유지한다.

**Architecture:** 기존 FacingDirection(0 아래/1 위/2 왼쪽/3 오른쪽)을 단일 방향값으로 유지한다. MoveX/MoveY는 이 방향의 단위 벡터, IsMoving은 이동 여부만 전달한다. 기존 idle 상태의 Motion만 4방향 Blend Tree로 교체하고 전투 상태·트리거는 보존한다.

**Tech Stack:** Unity 6, Animator 2D Simple Directional Blend Tree, 기존 Aseprite importer, NUnit Editor tests.

---

## 승인 범위와 변경 대상

사용자가 즉시 구현을 요청했다. 별도 승인 대기·커밋·push·Unity Play/강제 refresh 없이 진행한다. 시작 HEAD eb42e18, 브랜치 codex/gameplayEdit. Player.aseprite 삭제와 Wizel_overworld.aseprite 추가, Animator/실험실 씬 변경은 사용자 작업이다.

- `PlayerController.cs`: 정지 시 방향 파라미터 동기화, 불법 방향값 아래로 정규화, 저장 방향 로드, 명시적 전투 종료 시 기존 idle 복귀. 기존 대각선 좌우 우선 정책 유지.
- `Player_Animator.controller`: idle 상태의 이름/ID/전이 유지, 네 제작 클립을 참조하는 Blend Tree 추가. 기본 MoveY=-1. 전투 Motion/전이는 수정하지 않는다.
- `PlayerOverworldIdleTests.cs`: 네 방향·입력 해제·대각선·저장 복원 및 실제 Animator 자산 계약 검사.
- `AIAssets/2026-10-16-update.md`와 제작 안내: 조절 위치와 검증 한계 기록.

## 작업

- [x] 제작 클립 이름/fileID 및 사용자 자산 변경 확인.
- [x] 방향 기억과 Animator 동기화 경로 수정.
- [x] 기존 idle 상태에 네 방향 클립 연결.
- [x] 회귀 검사26사례 추가, CLI Runtime/Editor 컴파일 및 자산 참조 정적 검사.
- [x] diff/인코딩 확인 후 사용법과 Unity 미실행 범위 기록.

## 결과

Runtime405/Editor344소스 CLI 컴파일 오류0, 경고225/0. 실제 Unity 재생과 NUnit 실행은 하지 않았다. 자세한 참조와 알려진 기존 자산 위험은 [제작 안내](../../../AIAssets/yjlim/feedback/2026-10-16-player-directional-idle.md)에 기록했다.
