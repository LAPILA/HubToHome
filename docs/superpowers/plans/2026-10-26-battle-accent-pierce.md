# 전투 강조·Z 재준비·화면 관통 Implementation Plan

> **For agentic workers:** 승인 설계에 따라 파일 소유권을 나누어 병렬 구현하고 독립 검토한다. 기존 dirty 변경은 보존하며 커밋/푸시/Unity Play/Refresh는 하지 않는다.

**Goal:** 뜬금없는 확대·점멸과 Z 난타 성공을 줄이고 읽기 쉬운 화면 관통 참격을 제공한다.

**Architecture:** 표시 서비스는 표시만, SkillContext는 실제 명중 통지와 일회 Flash 요청만, 방어 정책은 저스트 재준비, 관통 블록은 기존 피해/QTE/복귀 계약을 사용한다. 본편 DB/저장 경계는 유지한다.

**Tech Stack:** Unity6/C#/DOTween/Odin/URP2D/기존 SkillContext·DefenseJudgementPolicy.

## Chunk1 · 실제 크기 실루엣과 명중 점멸

- [x] Accent 담당: Action_PresentationAccent/BattleSkillAccentPresentation/전용 shader/PresentationAccentTests를 수정한다. CutIn0 폐기·no-op, 실제 크기 월드 실루엣, Flash 비차단 명중 요청을 구현한다.
- [x] Accent 담당: SkillContext 표시 partial과 SkillElementalEffects.ApplySkillHit의 단일 실제 명중 경계를 연결한다. 기존 SkillActionBlocks의 편집 메타는 이 담당만 수정한다.
- [x] 실제 기하/불투명도/폐기 모드/명중 취소·중복 및 전체 컴파일로 확인한다.

## Chunk2 · 화면 관통과 실험실 데이터

- [x] 관통 담당: 아군 전용 재사용 블록·기하 정책·Editor 테스트/meta를 추가한다. 안전 끝점/교차 명중/자기 카메라 scope/취소·복귀/이동 잔상 계약을 구현한다. 기존 큰 공용 파일은 수정 전 조율한다.
- [x] 데이터 담당: 15개 Lab 자산의 CutIn8개 제거·Flash11개 명중 예약 배치·3종 관통 연결을 수행한다. SkillID/GUID/번역/DB/Catalog/Scenario는 유지한다. 기존 검사 도구의 새 계약을 조정한다.

## Chunk3 · Z 재준비·통합·기록

- [x] Root: 현재 정책에서 반복 Z가 무조건 성공하는 경로를 실제 관리형 정책으로 재현한다. 재준비 상태/정책·QTEManager·PlayerController·필요한 표시/Editor 테스트를 연결한다. 첫 입력/홀드 가드/X/C/일시정지/모달·빠른 연속 공격을 확인한다.
- [x] Root: SkillMakerBlockEditor/EnemyAttackAuthoring에 폐기 컷인 및 새 관통/명중 Flash 저작 규칙을 반영한다. 소유 충돌을 피한다.
- [x] Root: 전체 CLI·자산/GUID/원본20해시·diff/UTF8·독립 검토를 수행하고 사용법·일일 기록·학습용 인수인계·규칙/저작 가이드를 갱신한다. 실제 Play/GPU/기기 체감 미검증을 분리한다.

## 최종 근거

CLI Runtime473/Editor386 오류0/0. 관리형 Z정책479단언+8소스계약, 표시17,829+10소스계약, 관통16사례55단언, 자산YAML762/독립1,660단언 통과. meta1,713개32hex유일성·보호20원본 SHA256 일치·UTF8/diff 통과. NUnit 소스는 컴파일했지만 엔진 NUnit 실행은 아니다. Unity Play/실제 입력·렌더·GPU·기기 측정은 미실행이며 검증 완료로 표시하지 않는다. [인수인계와 수동 확인](../../../AIAssets/yjlim/feedback/2026-10-26-battle-accent-pierce.md).
