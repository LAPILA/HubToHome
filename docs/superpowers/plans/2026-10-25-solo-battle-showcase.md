# 위젤 단독 전투 연출 실험실 Implementation Plan

> **For agentic workers:** 사용자가 승인한10+5 스킬 구성이다. 소유 파일을 분리해 병렬 구현하고 독립 검토한다. 현재 dirty 작업을 보존하며 Unity 실행·전체 재생성·커밋/푸시는 하지 않는다.

**Goal:** 위젤1인으로 토끼/제브 전투와 재사용 가능한15개 연출 스킬을 시험한다.

**Architecture:** 기존 Session의 임시 저장 경계를1인으로 정리한다. 스킬은 원본의 Lab 전용 사본이며 기존 SkillActionBlock 조합과 새 표시 전용 Accent 블록만 사용한다. 시나리오는 기존 Source/Runtime 동기화 계약을 유지한다.

**Tech Stack:** Unity6/C#/기존 SkillData·QTE·DOTween/Odin/uGUI/기존 카탈로그/Scenario Source.

## Chunk 1 · 솔로 세션

- [x] LabData·Session/View/Preview·관련 생성기/테스트의3/6인 전제를1인으로 정리한다. 시작·종료 복원과 F8 중단을 보존한다.
- [x] 실제 Lab.asset의 Party1명/Encounters2개를 연결한다. Source 두 개와 Runtime 시나리오의 인원·Lab스킬 참조·해시를 맞춘다. 본편 DB/Scene/Prefab을 수정하지 않는다.
- [x] 기존 ContentBuilder의 KnownIds/이름 배열 길이 불일치를 관련 범위에서 고친다.

## Chunk 2 · 공용 연출과15개 스킬

- [x] `Action_PresentationAccent`와 지연 생성/재사용 표시 서비스, 필요한 최소 실루엣 셰이더·meta·Odin 표시/검사를 구현한다. 토큰·취소·정수 viewport·HUD 보존을 검사한다.
- [x] 기존 위젤10/제브5를 새 경로/고유 ID로 복제한다. CutIn/Silhouette/Flash/ScreenSplit/이동/난무/QTE를 차별화한다. 제브 연쇄는 단독 대상에서도 다단으로 진행한다.
- [x] Labfront의 DefaultSkills10, LabZEV의5개, 카탈로그·강제 시나리오 참조를 동기화한다. 원본 스킬과 원본 DB 해시를 보존한다.

## Chunk 3 · 검증·인수인계

- [x] 실제 소스 기반 관리형 수명/데이터/시나리오 검사와 Runtime/Editor CLI 오류0을 확인한다.
- [x] 신규 GUID32hex/유일성,15개 ID/rid·로컬 타입/자산 연결·SourceHash·원본 해시·UTF-8·diff를 검사한다.
- [x] 독립 소스 검토 후 필요한 수정만 수행한다. `docs/bunny-slime-battle-lab.md`, 당일 AIAssets/사용자 안내/규칙·시나리오 스킬 문서를 갱신한다. 실제 Play/실기기 미검증을 구분한다.

## 완료 근거와 검증 범위

- 전체 CLI: Runtime466/Editor384소스, 오류0/0. Runtime의 기존 경고226건은 별도이며 경고0이라고 주장하지 않는다.
- 관리형 검사: 세션10사례25단언, 자산/시나리오1,780단언, YAML 저작760단언, 공용 연출 수학/설정14,561단언 통과. Unity 엔진 경계는 대역이다.
- 보호한 원본20파일 해시 일치,15개 Lab 전용 스킬·143블록·66GUID참조와4개 언어120문구를 검사했다.
- Unity Play, 실제 GPU 셰이더 컴파일, 연출 박자, 긴 번역 줄바꿈, 실기기 성능은 미검증이다. Refresh/전체 생성기/실제 저장 I/O/커밋/푸시는 실행하지 않았다.
- 인수인계: [실험실 사용법](../../bunny-slime-battle-lab.md), [변경·검증 기록](../../../AIAssets/yjlim/feedback/2026-10-25-solo-battle-showcase.md).
