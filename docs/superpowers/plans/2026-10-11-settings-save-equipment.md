# 설정·저장·장비 안정화 Implementation Plan

> 실행: executing-plans 절차로 현재 작업 트리에서 순차 수행. 사용자 요청에 따라 서브에이전트, Unity 실행, 자동 커밋은 사용하지 않는다.

**Goal:** 기존 연결을 유지하며 설정 저장 빈도, 저장 지점/이벤트 저장, 장비 소유·자원 일관성을 정리한다.

**Architecture:** 환경 설정은 즉시 적용하되 디스크 기록을 모은다. 진행도는 SaveManager의 공통 진입점에서 수동 0~2 / 자동 99를 분리하고, 자동 요청은 안전한 탐색 상태까지 기다린다. 장비 검증과 저장 수치 보정은 EquipmentLoadoutService에 모은다.

**Tech Stack:** Unity 6, C#, uGUI/TMP, DOTween, Odin, Newtonsoft.Json, 기존 원자 저장소.

## Chunk 1: 설정

- [x] GameConfigManager: 변경 후 지연 저장, 닫기/일시 중지/종료 시 flush. 공개 Save는 즉시 저장 유지.
- [x] GameConfigPolicy/ConfigPanelUI: 데스크톱 전용 해상도 정책 공유, 입력 중복 차단, 모달 소유권과 긴 라벨 배치 정리.

## Chunk 2: 진행도

- [x] SaveDataCodec/SaveManager: 호출자의 SaveData를 수정하지 않는 스냅샷 직렬화. 스키마 v4, 복구 순서 유지.
- [x] SaveManager/GlobalDataManager: 저장 지점 공통 진입점과 대기 가능한 자동 저장. 새 게임/불러오기 시 이전 요청 취소.
- [x] SavePoint/SavePointMarker: 기존 대화 선택창을 통한 저장 확인. 자동 저장은 수동 칸에 쓰지 못함.
- [x] ProgressSaveTrigger: UnityEvent/기존 대사 EventTriggerID 요청 및 지정 전투 승리 연결. 모든 전투 자동 저장 금지.
- [x] TitleMenuManager: 기존 대화 패널로 기록 선택/오류 안내. 패널 없는 타이틀은 기존 최근 기록 로드 유지.

## Chunk 3: 장비

- [x] EquipmentLoadoutService: 읽기 전용 조회, 파티 소속 검증, 장비 변경 후 HP/AP 상한 보정. 현재량 유지, 자동 회복 없음.
- [x] GlobalDataManager.RemoveEquipment: 장착 수량 보호.
- [x] OverworldMenuUI: 실패 시 런타임 갱신 금지, 성공 시 기존 동기화 재사용.

## 검증·기록

- [x] 기존 테스트에 비변이 저장, 장비 수량·HP/AP, 플랫폼 정책·설정 모달 회귀 항목 추가.
- [x] CLI 런타임/에디터 컴파일 및 순수 관리형 검사 155/155. Unity Play/EditMode 실행은 하지 않음.
- [x] git diff --check, 기존 변경 보존, 사용법·미검증 사항을 AIAssets 업데이트와 피드백에 기록.

백업: Library/CodexChecks/SettingsSaveEquipment/Original. 외부 플러그인/씬/프리팹은 수정하지 않는다.
