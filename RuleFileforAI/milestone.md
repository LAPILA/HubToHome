# 🗓️ HubToHome 프로젝트 마일스톤 (Project Milestone)

> **작성일:** 2026-04-29  
> 언더테일/델타룬 감성 + 림버스 컴퍼니 전투 연출을 융합한 2D 탑다운 액션 RPG  
> 각 Phase는 이전 Phase가 완료된 후 진행합니다.

---

## 📊 전체 진행 현황

| Phase | 이름 | 상태 | 완료 기준 |
|-------|------|------|-----------|
| Phase 0 | 프로젝트 기반 세팅 | ✅ **완료** | 폴더 구조, 코어 스크립트 골격 완성 |
| Phase 1 | 코어 시스템 완성 | 🔄 **진행 중** | 모든 싱글톤 씬 연동 테스트 통과 |
| Phase 2 | 오버월드 기본 구현 | ⬜ 예정 | 플레이어 이동 + NPC 대화 + 씬 전환 동작 |
| Phase 3 | 전투 시스템 기본 구현 | ⬜ 예정 | 1회 전투 완전 루프 (진입→턴→결과→복귀) |
| Phase 4 | 대화/연출 시스템 완성 | ⬜ 예정 | 분기 대화 + 이벤트 브릿지 + 연출 효과 동작 |
| Phase 5 | UI 완성 및 통합 | ⬜ 예정 | 모든 UI 패널 연동 및 인게임 HUD 완성 |
| Phase 6 | 콘텐츠 제작 | ⬜ 예정 | 맵 1개 + 적 3종 + 스토리 도입부 완성 |
| Phase 7 | 밸런싱 및 폴리싱 | ⬜ 예정 | QA 통과, 빌드 배포 가능 상태 |

---

## ✅ Phase 0 — 프로젝트 기반 세팅 `완료`

> **목표:** 개발 환경 구축 및 전체 아키텍처 골격 확립

### 완료된 작업
- [x] Unity 6000.3.8f1 + URP 프로젝트 생성
- [x] 폴더 구조 확립 (`_Game/Battle`, `Characters`, `Core`, `Dialogue`, `Items`, `Overworld`, `UI`)
- [x] 외부 플러그인 설치 (DOTween, TextAnimator, Odin Inspector, Vefects, Cinemachine)
- [x] 코어 스크립트 골격 작성
  - [x] `GameBootstrap` — 싱글톤 초기화 진입점
  - [x] `GlobalDataManager` — 전역 데이터 허브
  - [x] `SceneLoader` — 비동기 씬 전환 (Fade/Flash)
  - [x] `AudioManager` — BGM/SFX/Voice + Seamless Transition
  - [x] `ObjectPoolManager` — 오브젝트 풀
  - [x] `SaveManager` + `SaveData` — JSON 세이브/로드 (4슬롯)
  - [x] `EventFlags` + `SceneName` — 상수 관리
- [x] 전투 시스템 골격
  - [x] `BattleStateMachine` (열거형)
  - [x] `BattleManager` — 전투 흐름 총괄
  - [x] `QTEManager` — 타이밍/연타/방어 QTE
  - [x] `SkillData` (ScriptableObject)
- [x] 캐릭터 시스템 골격
  - [x] `CharacterBase` — 공통 추상 클래스
  - [x] `PlayerCharacter` — 레벨/장비/EXP
  - [x] `EnemyCharacter` — AI 패턴/드롭
  - [x] `StatusEffect` — 상태 이상 베이스
  - [x] `EnemyData`, `EquipmentData` (ScriptableObject)
- [x] 오버월드 골격
  - [x] `PlayerController` — 이동/입력/전투 연출
  - [x] `InteractionSystem` — OverlapBox 상호작용 감지
  - [x] `InteractableBase` + `IInteractable`
  - [x] `AreaTrigger` — 씬 전환/이벤트/전투 진입
- [x] 대화 시스템 골격
  - [x] `DialogueData` — JSON 직렬화 구조
  - [x] `DialogueManager` — 대화 흐름 총괄
  - [x] `DialogueEventBridge` — 이벤트 실행기
  - [x] `DialogueNPC` — 조건부 대화 NPC
- [x] UI 골격
  - [x] `UIPanel` — DOTween 페이드 베이스
  - [x] `UIManager` — Stack 기반 패널 관리
- [x] `ItemData` (ScriptableObject)
- [x] RuleFileforAI 문서 작성 (clinerules, 코드 레퍼런스)

---

## 🔄 Phase 1 — 코어 시스템 완성 `완료`

> **목표:** 모든 코어 싱글톤이 실제 씬에서 정상 동작하는지 검증

### 작업 목록
- [x] **Bootstrap Scene 구성**
  - [x] Bootstrap.unity에 `GameBootstrap` 오브젝트 배치
  - [x] 각 싱글톤 프리팹 생성 및 Inspector 연결
  - [x] 씬 빌드 세팅에 Bootstrap → Overworld 순서 등록
- [x] **SceneLoader 연동 테스트**
  - [ ] Fade 전환 (Overworld ↔ Title) 동작 확인
  - [ ] Flash 전환 (Overworld → Battle) 동작 확인
  - [x] FadeCanvas CanvasGroup 프리팹 연결
- [x] **GlobalDataManager 연동 테스트**
  - [ ] 씬 전환 후 데이터 유지 확인 (HP, 인벤토리, 플래그)
  - [x] `ToSaveData()` / `FromSaveData()` 왕복 테스트
- [x] **SaveManager 연동 테스트**
  - [x] Manual Slot 0~2 저장/불러오기/삭제 테스트
  - [ ] Auto Slot 자동 저장 테스트
  - [ ] 저장 파일 경로 확인 (`persistentDataPath`)
- [x] **AudioManager 연동 테스트**
  - [ ] BGM 재생 / CrossFade / SeamlessTransition 테스트
  - [ ] SFX / Voice 재생 테스트
  - [x] AudioMixer 볼륨 설정 테스트
- [x] **ObjectPoolManager 연동 테스트**
  - [x] 더미 이펙트 프리팹으로 Spawn/Despawn 테스트
  - [x] 풀 소진 시 자동 확장 확인

### 완료 기준
- Bootstrap Scene에서 게임 시작 시 모든 싱글톤이 오류 없이 초기화됨
- 씬 전환 시 GlobalDataManager 데이터가 유지됨
- 세이브/로드 왕복 테스트 통과

---

## ⬜ Phase 2 — 오버월드 기본 구현

> **목표:** 플레이어가 맵을 이동하고 NPC와 대화하며 씬을 전환할 수 있는 상태

### 작업 목록
- [ ] **타일맵 기반 맵 제작**
  - [ ] Ground / Obstacle / Decor 레이어 구성
  - [ ] Tilemap Collider 2D + Composite Collider 2D 설정
  - [ ] Y-Sort 정렬 설정 (Transparency Sort Mode: Y Axis)
- [ ] **PlayerController 완성**
  - [ ] 8방향 이동 + Last-Input Priority 검증
  - [ ] Animator 연결 (Idle_Down/Up/Left/Right, Walk 4방향)
  - [ ] `LoadPositionFromGlobal()` 씬 로드 후 자동 호출 연결
- [ ] **InteractionSystem 완성**
  - [ ] Interaction 레이어 마스크 설정
  - [ ] Gizmo 시각화 확인
- [ ] **DialogueNPC 배치 및 테스트**
  - [ ] 테스트용 JSON 대화 파일 작성 (`Resources/Dialogues/test.json`)
  - [ ] `DialogueManager.LoadDialogueFile()` 호출 시점 결정
  - [ ] NPC 오브젝트 배치 + Z키 대화 동작 확인
  - [ ] 조건부 대화 (플래그 분기) 테스트
- [ ] **AreaTrigger 씬 전환 테스트**
  - [ ] SceneTransition 타입 동작 확인
  - [ ] 스폰 위치 복원 확인
  - [ ] AutoSave 트리거 확인
- [ ] **카메라 설정**
  - [ ] Cinemachine Virtual Camera 배치
  - [ ] 플레이어 추적 설정
  - [ ] 맵 경계 Confiner 설정

### 완료 기준
- 플레이어가 맵에서 자유롭게 이동 가능
- NPC와 Z키로 대화 가능 (분기 포함)
- AreaTrigger로 씬 전환 후 올바른 위치에 스폰

---

## ⬜ Phase 3 — 전투 시스템 기본 구현

> **목표:** 1회 전투 완전 루프 (진입 → 플레이어 턴 → 적 턴 → 결과 → 복귀)

### 작업 목록
- [ ] **BattleScene 구성**
  - [ ] 배경 환경 (3D Plane 또는 2D 배경 스프라이트)
  - [ ] 플레이어/적 기본 위치 Transform 배치
  - [ ] 중앙 위치(CenterPosition) Transform 배치
  - [ ] BattleManager Inspector 연결
- [ ] **BattleUI 기본 구현**
  - [ ] 플레이어 메뉴 (공격/스킬/아이템/도망) UI
  - [ ] HP 바 (플레이어/적)
  - [ ] `BattleManager.OnPlayerActionSelected()` 연동
- [ ] **QTE UI 구현**
  - [ ] 타이밍 바 UI (QTEManager.StartTimingQTE 연동)
  - [ ] 연타 게이지 UI (QTEManager.StartMashingQTE 연동)
  - [ ] 방어 타이밍 인디케이터 UI (QTEManager.StartDefenseQTE 연동)
- [ ] **전투 연출 완성**
  - [ ] 캐릭터 중앙 이동 DOTween 확인
  - [ ] 타격 이펙트 ObjectPool 연동
  - [ ] 카메라 쉐이크 (Cinemachine Impulse) 연동
  - [ ] 플레이어 전투 애니메이션 연동 (`PlayBattleAnim`)
- [ ] **EXP/드롭 처리**
  - [ ] `EnemyCharacter.GetDrops()` → `GlobalDataManager.AddItem()` 연동
  - [ ] `PlayerCharacter.GainEXP()` 레벨업 연출
- [ ] **전투 결과 화면**
  - [ ] 승리/패배 UI
  - [ ] AutoSave 후 오버월드 복귀 확인
- [ ] **AreaTrigger BattleEncounter 연동**
  - [ ] GlobalDataManager에 적 그룹 ID 저장 구현
  - [ ] BattleScene에서 적 그룹 ID로 적 생성

### 완료 기준
- 오버월드에서 전투 진입 → 1회 전투 완전 루프 → 오버월드 복귀 동작
- QTE 3종 (타이밍/연타/방어) 모두 동작
- EXP 획득 및 레벨업 동작

---

## ⬜ Phase 4 — 대화/연출 시스템 완성

> **목표:** 풍부한 연출이 포함된 대화 시스템 완성

### 작업 목록
- [ ] **DialogueController 구현**
  - [ ] Febucci TextAnimator 연동 (타이핑 효과)
  - [ ] 캐릭터별 타이핑 사운드(Beep) 구현
  - [ ] `CompleteTyping()` 콜백 연결
- [ ] **대화 UI 완성**
  - [ ] 대화창 레이아웃 (화자 이름, 초상화, 텍스트)
  - [ ] 선택지 UI (ChoiceHandler)
  - [ ] 자동 진행(autoAdvance) 동작 확인
- [ ] **인라인 명령어 완성**
  - [ ] `[bgm:xxx]` → AudioManager.CrossFadeBGM 연동
  - [ ] `[shake]` → Cinemachine Impulse 연동
  - [ ] `[flash]` → SceneLoader FadeCanvas 연동
- [ ] **DialogueEventBridge 확장**
  - [ ] `GIVE_ITEM` 실제 인벤토리 UI 반영
  - [ ] `START_BATTLE` 적 그룹 ID 전달 완성
- [ ] **조건부 대화 테스트**
  - [ ] 플래그 분기 대화 시나리오 작성 및 테스트
- [ ] **BGM 덕킹 테스트**
  - [ ] 대화 시작/종료 시 BGM 볼륨 자동 조절 확인

### 완료 기준
- TextAnimator 태그가 포함된 대화 정상 출력
- 선택지 분기 및 이벤트 실행 동작
- 대화 중 연출 명령어 동작

---

## ⬜ Phase 5 — UI 완성 및 통합

> **목표:** 모든 게임 UI 완성 및 시스템 통합

### 작업 목록
- [ ] **인벤토리 UI**
  - [ ] 아이템 목록 표시 (ItemData 아이콘/이름/설명)
  - [ ] 아이템 사용 (Consumable → Heal)
  - [ ] 장비 장착 (Equipment → PlayerCharacter.Equip)
- [ ] **전투 HUD 완성**
  - [ ] 플레이어 HP/MP 바 실시간 업데이트
  - [ ] 적 HP 바 실시간 업데이트
  - [ ] 상태 이상 아이콘 표시
  - [ ] 턴 순서 표시
- [ ] **세이브/로드 UI**
  - [ ] 세이브 슬롯 목록 (저장 시각, 플레이 시간 표시)
  - [ ] 세이브 포인트 오브젝트 구현 (`InteractableBase` 상속)
  - [ ] 타이틀 화면 로드 슬롯 선택
- [ ] **설정 UI (Pause Menu)**
  - [ ] BGM/SFX/Voice 볼륨 슬라이더
  - [ ] AudioManager 볼륨 연동
  - [ ] 게임 종료 버튼
- [ ] **타이틀 화면**
  - [ ] 새 게임 / 불러오기 / 종료
  - [ ] 타이틀 BGM 재생
- [ ] **전환 연출 통합**
  - [ ] 모든 씬 전환 Fade/Flash 최종 확인
  - [ ] UIManager Stack 동작 최종 확인

### 완료 기준
- 인벤토리, 전투 HUD, 세이브/로드, 설정 UI 모두 동작
- 타이틀 → 게임 → 세이브 → 로드 전체 플로우 동작

---

## ⬜ Phase 6 — 콘텐츠 제작

> **목표:** 실제 플레이 가능한 게임 콘텐츠 제작

### 작업 목록
- [ ] **맵 제작**
  - [ ] 오버월드 맵 1개 (마을 또는 던전 도입부)
  - [ ] 씬 전환 포인트 배치
  - [ ] 세이브 포인트 배치
- [ ] **캐릭터 제작**
  - [ ] 플레이어 캐릭터 스프라이트 + 애니메이션 완성
  - [ ] `StatGrowthCurve` ScriptableObject 구현 및 적용
  - [ ] 파티원 1명 추가 (Follower Logic)
- [ ] **적 제작**
  - [ ] 적 캐릭터 3종 (EnemyData SO 작성)
  - [ ] 각 적 스킬 1~2개 (SkillData SO 작성)
  - [ ] 드롭 아이템 설정
- [ ] **아이템 제작**
  - [ ] 소모품 3종 (포션 등)
  - [ ] 장비 5종 (무기 2, 방어구 3)
  - [ ] 중요 아이템 1종 (스토리 연동)
- [ ] **스토리 대화 작성**
  - [ ] 도입부 이벤트 대화 (JSON 작성)
  - [ ] 마을 NPC 대화 5개 이상
  - [ ] 첫 번째 보스 전투 전 대화
- [ ] **이벤트 플래그 설계**
  - [ ] `EventFlags.cs`에 스토리 플래그 상수 등록
  - [ ] 플래그 기반 NPC 대화 분기 구현

### 완료 기준
- 타이틀 → 오버월드 탐색 → 전투 → 스토리 대화 → 세이브 전체 플로우 플레이 가능

---

## ⬜ Phase 7 — 밸런싱 및 폴리싱

> **목표:** 게임 품질 완성 및 배포 준비

### 작업 목록
- [ ] **전투 밸런싱**
  - [ ] 적 스탯 조정 (HP, ATK, DEF, SPD)
  - [ ] QTE 난이도 조정 (QTEDifficultyMultiplier)
  - [ ] EXP 곡선 조정
  - [ ] Speed Gap Logic 실전 테스트
- [ ] **연출 폴리싱**
  - [ ] 모든 DOTween 이펙트 최종 조정
  - [ ] 카메라 쉐이크 강도 조정
  - [ ] BGM/SFX 볼륨 밸런싱
  - [ ] 전투 진입/종료 연출 완성
- [ ] **버그 수정 및 QA**
  - [ ] 씬 전환 중 데이터 유실 없음 확인
  - [ ] 세이브/로드 엣지 케이스 테스트
  - [ ] 메모리 누수 확인 (ObjectPool 반납 누락 등)
  - [ ] 모바일 입력 대응 테스트 (가상 패드)
- [ ] **최적화**
  - [ ] Update 내 불필요한 연산 제거
  - [ ] 텍스처 압축 및 아틀라스 적용
  - [ ] 빌드 크기 최적화
- [ ] **빌드 및 배포**
  - [ ] PC 빌드 테스트
  - [ ] 빌드 세팅 최종 확인
  - [ ] 배포 패키지 준비

### 완료 기준
- 처음부터 끝까지 오류 없이 플레이 가능
- 빌드 배포 가능 상태

---

## 📋 현재 미구현 항목 요약 (Phase 1~3 우선순위)

| 우선순위 | 항목 | 관련 Phase |
|----------|------|-----------|
| 🔴 높음 | Bootstrap Scene 구성 및 싱글톤 프리팹 연결 | Phase 1 |
| 🔴 높음 | BattleUI 기본 구현 (메뉴 + HP 바) | Phase 3 |
| 🔴 높음 | QTE UI 3종 구현 | Phase 3 |
| 🟡 중간 | DialogueController (TextAnimator 연동) | Phase 4 |
| 🟡 중간 | 타일맵 맵 제작 | Phase 2 |
| 🟡 중간 | StatGrowthCurve SO 구현 | Phase 6 |
| 🟢 낮음 | 인벤토리 UI | Phase 5 |
| 🟢 낮음 | 세이브/로드 UI | Phase 5 |
| 🟢 낮음 | 모바일 가상 패드 | Phase 7 |

---

## 🔖 버전 히스토리

| 날짜 | 버전 | 내용 |
|------|------|------|
| 2026-04-28 | v0.1 | 프로젝트 생성, 폴더 구조 확립 |
| 2026-04-29 | v0.2 | 전체 코어/전투/캐릭터/대화/UI 골격 스크립트 완성, 마일스톤 문서 작성 |
