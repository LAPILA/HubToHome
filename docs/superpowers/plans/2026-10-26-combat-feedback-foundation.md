# 전투 피드백 공통 기반 Implementation Plan

> **For agentic workers:** REQUIRED: Use superpowers:subagent-driven-development (if subagents available) or superpowers:executing-plans to implement this plan. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 평타/스킬/전조 기획을 건드리지 않고 방어·AP·QTE감상·누적 상태·아이템·보스 진행 표시를 공용 기반으로 마감한다.

**Architecture:** 기존 입력/판정/전투 서비스와 데이터에 좁은 정책을 추가한다. 모듈·배우·행동 수명에 상태를 묶고 UI는 이벤트 관찰만 한다. 제브 이름 분기나 원본 스킬 대량 수정은 하지 않는다.

**Tech Stack:** Unity6/C#/Odin/DOTween/uGUI/TMP/PlayerPrefs/기존 Scenario Source.

---

## Chunk 1: 방어 입력·판정

**Files:** `Battle/Runtime/DefenseJudgementPolicy.cs`, `ParryRearmState.cs` 또는 공통 대응 상태, `IDefenseInputSource.cs`, `UI/Runtime/QTEManager.cs`, `Overworld/Runtime/PlayerController.cs`, 관련 Editor tests/관리형 검사.

- [x] 현 성공창·교대 연타·홀드 끊김의 실제 메서드 재현 검사를 먼저 만든다.
- [x] Perfect .24/.40 하한과 성공창 밖 무제한 재시도를 제거하고 전조 표시 시계는 보존한다.
- [x] 공통 대응 확정/재준비와 지속 홀드 일반 방어를 분리한다. pause/종료/소비 프레임/다음 타격을 보호한다.
- [x] C6/7의 C전용 가부·제작설명을 일치시킨다. 기존 수치65%감소와 방어/보상 호출 횟수는 유지한다.

## Chunk 2: AP·아이템

**Files:** `BattleManager.cs`, `BattleTurnQteModuleControllerService.cs`, 관련 optional host seam/순수 보상 예산, 공용 Host.prefab AP설정; Lab Data/Session/front.asset; `ItemData.cs`, `ItemEffectService.cs`, 새 누적 payload, `EquipmentTriggeredEffect.cs`/`EquipmentEffectService.cs`, 관련 tests/샘플.

- [x] 적 행동별/아군별 저스트 AP 예산과 중단/초기화 검사를 만든다.
- [x] 공통+2/+2/적행동최대6, Lab만 최대40/시작20을 연결한다. 평타/스킬·본편 성장·저장은 보존한다.
- [x] 선택적 아이템 누적 부여/소비 기반 피해를 기존 검증·적용·소비에 연결한다.
- [x] ItemUsed 장비발동을 정상 아이템 행동당1회 연결하고 무효/취소/AoE 중복을 막는다.
- [x] Lab 샘플 2종 이상과 장비 연계 예시·4언어 키를 연결한다. 필드 사용 거부·원본 보존을 검사한다.

## Chunk 3: 누적 상태 강화

**Files:** `ElementalStatusProfile.cs`, `ElementalStatusState.cs`, 상태 표시/설명 관련 UI, 순수 계산기/Editor tests.

- [x] 누적/피해/임계/감소·소수이월·저항/빙결 경계 검사를 작성한다.
- [x] 단계10/25/50,배율1.25/1.5/2와상한을데이터화하고 실제 상태 피해에만 적용한다.
- [x] 상태 설명과 단계 상승 강조를 연결하고 숫자 텍스트/피격 중복/매프레임 스캔을 추가하지 않는다.

## Chunk 4: QTE 감상 설정·보스 진행 UI

**Files:** `GameConfigManager.cs`/`ConfigPanelUI.cs`, 공격 QTE runner/`QTEManager.RhythmSkill.cs`, `BattleUIController.cs`/공통 QTE표시; `EnemyData.cs`, 새 boss progress UI, LocalizationTable.csv 및 관련 tests.

- [x] 설정 자동QTE 기본false/지연 저장·초기화·4언어 UI를 연결한다.
- [x] 원래 공격 시계를 유지하며 공격 QTE만 성공 처리한다. 방어 이벤트/입력/AP보상과 분리하고 기존 수동 실패 피해를 보존한다.
- [x] 공통 공격 QTE의 안전한 고정 위치를 사용한다. 스킬 패턴/타수/횟수는 바꾸지 않는다.
- [x] 이벤트 기반 보스 HP/비율·체력 마커를 상단 예약 공간에 한 대상만 표시한다. 시나리오 페이즈를 새로 결정하지 않는다.
- [x] ZEV Lab에 선택적 보스 표시를 연결하고 종료/재바인딩/파괴·대화 입력 소유권을 검사한다.

## Chunk 5: 통합 검증·문서

- [x] 각 단위 리뷰 후 전체 CLI Runtime/Editor 오류0, 관리형 판정·입력·보상·아이템·상태·QTE·UI 회귀를 실행한다.
- [x] 실제 DLL 계약/대체 경계/native 미검증을 구분한다. meta/UTF8/SourceHash/기존 원본·저장 보존 검사.
- [x] 현재 규칙/가이드/피드백/당일 개발록과 시나리오 skill의 로컬 매핑을 갱신한다.
- [x] 평타·스킬 구성·전조/말풍선 및 실제 보스 패턴 기획의 보류 항목을 최종 인계에 분리한다. 커밋·푸시 없음.
