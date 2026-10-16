# Chapter 1 Foundation Implementation Plan

> **For agentic workers:** Use the available collaboration tools for independent owned file groups; root integrates and reviews. Steps use checkbox syntax. No commits/push/Unity Play without a new explicit request.

**Goal:** 사용자 확정 챕터1 규칙을 기존 시스템과 저장 호환을 유지하며 연결한다.

**Architecture:** GlobalDataManager는 저장 상태, 좁은 progression/item/rest/retry 서비스는 규칙, 기존 전투/메뉴는 실행과 표시를 소유한다. 공유 파일은 root 단독 수정하고 나머지는 분리된 소유권으로 병렬 구현한다.

**Tech Stack:** Unity6 C#, uGUI/TMP, Input System, DOTween, Odin, 기존 JSON 원자 저장과 Scenario/Dialogue.

## Chunk 1: 성장·합류·전투 규칙 (root)

Files: Core/Runtime/GlobalDataManager.cs 및 새 partial progression; SaveData.cs/SaveDataCodec.cs; Characters/Runtime/CharacterProgressionService.cs/PlayerCharacter.cs; Battle/Runtime/Services/BattleRewardService.cs; DefenseJudgementPolicy.cs; 관련 Tests/Editor. 새 Overworld/Runtime/PartyRecruitmentTrigger.cs.

- [x] 현재 성장/저장/전투 진입과 수정 중 파일을 읽고 소유권 확정.
- [x] 공용 레벨/EXP와 이전 저장 이전 규칙, 중복 합류 테스트 추가.
- [x] 공용 EXP 한 번 지급, 캐릭터별 포인트 동기화와 직접 투자만 유지.
- [x] 이벤트 ID/UnityEvent로 호출 가능한 합류 컴포넌트와 저장 연결.
- [x] 방어0.35와 성공QTE1.0/실패감소 경로 통일, 부활 대상/패시브 훅 통합.
- [x] 세이브 v5 복사·정규화·legacy 테스트, 수동저장 적용 API 통합.

## Chunk 2: 저장·월드·재도전 (chapter01_save_world)

Files: SaveManager.cs; 새 ManualSaveRestPolicy.cs; OverworldEnemy.cs; EnemyData.cs; GameLoadCoordinator.cs; BattleEncounterService.cs; GameOverUI.cs; SceneActionSequenceTrigger.cs; 관련 테스트. GlobalDataManager/SaveData/Codec은 root에게 필드/API 요청.

- [x] 저장 복사본을 회복/일반적 초기화 후 원자 저장, 성공 후만 런타임 반영.
- [x] 패배 때문에 숨긴 일반 적만 복원, 이야기/방 비활성은 유지.
- [x] 보스/필수전투 직전 스냅샷 캡처와 재도전, 오래된 세션 해제.
- [x] 보스·진행 이벤트 완료 자동 저장을 기존 안정 경계에 예약.
- [x] 대화 스킵의 상태 계약을 합의하고 완료/취소 경로 정적 검토 및 테스트 작성. Unity 실행 검증은 별도.

## Chunk 3: 아이템·장비 (chapter01_items_equipment)

Files: Items/Data/ItemData.cs, Items/Runtime 서비스들, Characters/Data/EquipmentData.cs, ShopUI.cs 판매목록, 관련 검사와 Tests/Editor. GlobalDataManager/BattleRewardService/BattleUIController/TurnQteService는 root 수정.

- [x] 기존 enum값/아이템 참조 보존한 장비 데이터 연결과 재고 라우터.
- [x] 구매·판매·보상·마커 호출자 연결 및 장착 수량/판매금지 보호.
- [x] 정화/부활 효과, 필드/전투 대상 유효성, 소비 롤백 테스트 작성·컴파일.
- [x] 조건부 HP/AP 장비 효과의 데이터 계약·실행 서비스·호출 훅 통합. 모든 패시브 종류 구현을 의미하지 않음.

## Chunk 4: 입력·메뉴·일지·현지화 (chapter01_ui_input)

Files: GameInput.cs, BattleInputHintView.cs, OverworldMenuUI.cs, LocalizationManager.cs, DialogueData.cs/SpeakerData.cs/DialogueUI.cs; 새 모바일 입력/일지 표시 데이터·서비스. PowerGrowthPanelView/GlobalDataManager는 root.

- [x] 기존 InputAction에 패드 바인딩과 기기 감지, 터치는 전용 Rect에서만 전달.
- [x] 실제 필드 아이템 목록 선택→동료 대상→사용을 아이템 서비스와 연결.
- [x] 일지/목표와 현재 능력치 표시, 약점/저항은 노출하지 않음. 실제 목표 데이터는 미정으로 비워 둠.
- [x] 메뉴 시간 정지·음악 유지, 입력 소비/해제/복구 코드 정적 검토·검사 작성. 실기기/Play 검증은 별도.
- [x] 화자/선택지/메뉴 번역 키와4언어 기본 안내, 안전 CSV 파싱. 전체 본편 번역은 미포함.

## Chunk 5: 통합·검증·인수인계 (root)

- [x] 각 소유자 변경 검토, 공유 API/직렬화 enum/메타 파일 일치 확인.
- [x] 가능한 CLI 컴파일을 현재 프로젝트 검증 스크립트로 실행. Unity refresh/Play 미실행.
- [x] 순수 테스트/정적 연결 점검을 실행하고 미실행 테스트와 구분.
- [x] CONTEXT/도메인 규칙/시나리오 계약 중 바뀐 부분 갱신.
- [x] AIAssets/2026-10-16-update.md 및 기획·사용법·미완료 경계를 기록.

## 검증 명령 결정

기존 docs/AIAssets의 CLI 빌드 명령과 설치 런타임을 먼저 확인하고 그 경로를 사용한다. 임의 Unity 실행이나 AssetDatabase 재생성으로 검증을 대체하지 않는다. 구현 후 정확한 명령/결과를 update에 남긴다.

## 최종 확인

Runtime 405소스/Editor·테스트 343소스 CLI 컴파일 오류0. Runtime 경고225/Editor 경고0. 관리형 성장·방어·공격QTE·저장코덱 검사165/165, CSV 독립3사례 통과. 자체 C#712파일 UTF8 오류0/meta누락0. 위 체크는 구현·명시된 정적 검사 완료를 뜻하며 Unity Play나 실제 기기의 품질 보장을 뜻하지 않는다.

[제작 인수인계](../../../AIAssets/yjlim/feedback/2026-10-16-chapter01-foundation.md)와 [검증 기록](../../../AIAssets/2026-10-16-update.md)에 연결 방법, 미정 콘텐츠, 미실행 통합 검사를 구분했다.
