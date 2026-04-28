# 📋 HubToHome 프로젝트 개요 (Project Overview)

> 이 문서는 프로젝트 전체 방향성과 구조를 기록하는 마스터 문서입니다.
> 각 시스템 세부 규칙은 개별 `.clinerules` 파일을 참조하세요.

---

## 🎮 게임 컨셉

- **장르:** (예: 2D 탑다운 RPG 여기에 작성)
- **핵심 레퍼런스:** 언더테일/델타룬 (연출, 감성), 림버스 컴퍼니 (전투 메커니즘)
- **타겟 플랫폼:** (예: PC / 모바일 여기에 작성)
- **목표 플레이타임:** (여기에 작성)

---

## 📖 스토리 요약

> (게임의 배경, 주인공, 핵심 갈등 등 여기에 작성)

---

## 🗺️ 게임 구조

### 주요 씬 목록
| 씬 이름 | 설명 |
|---------|------|
| TitleScene | 타이틀 화면 |
| OverworldScene | 탐색 씬 |
| BattleScene | 전투 씬 |
| (추가) | (설명) |

### 게임 플로우
```
타이틀 → 오버월드 탐색 → 전투 돌입 → 전투 결과 → 오버월드 복귀
```

---

## 🛠️ 기술 스택

- **엔진:** Unity 6000.3.8f1
- **렌더 파이프라인:** URP (Universal Render Pipeline)
- **주요 플러그인:**
  - DOTween (Demigiant) - 트윈 애니메이션
  - TextAnimator (Febucci) - 텍스트 연출
  - Odin Inspector (Sirenix) - 에디터 확장
  - Vefects - 비주얼 이펙트
- **입력 시스템:** Unity New Input System

---

## 📁 폴더 구조

```
Assets/
├── _Game/
│   ├── Battle/         → 전투 시스템 (battle.clinerules)
│   ├── Overworld/      → 오버월드 (overworld.clinerules)
│   ├── Characters/     → 캐릭터 (characters.clinerules)
│   ├── UI/             → UI (ui.clinerules)
│   ├── Items/          → 아이템
│   ├── Dialogue/       → 대화/연출 (dialogue.clinerules)
│   ├── Core/           → 코어 시스템 (core.clinerules)
│   └── Scenes/         → 씬 파일
├── Plugins/            → 외부 플러그인 (수정 금지)
├── Keyboard/           → 입력 설정
└── RenderSettings/     → URP 렌더 설정
```

---

## 👥 개발 협업 원칙

1. 코드 대량 수정 전 구조적 설계안 먼저 제안
2. 외부 플러그인 원본 코드 직접 수정 금지 (Partial Class / Adapter 사용)
3. 씬 전환 시 데이터 보존 필수 (GlobalDataManager 사용)
4. 시각적 연출 구현 후 반드시 테스트 요청
5. 최적화: Update 내 LINQ 금지, 오브젝트 풀링 적용

---

## 📅 개발 마일스톤

> 상세 마일스톤은 [`milestone.md`](./milestone.md) 를 참조하세요.

| 단계 | 내용 | 상태 |
|------|------|------|
| Phase 0 | 프로젝트 기반 세팅 (폴더 구조, 전체 골격 스크립트) | ✅ 완료 |
| Phase 1 | 코어 시스템 완성 (싱글톤 씬 연동 테스트) | 🔄 진행 중 |
| Phase 2 | 오버월드 기본 구현 (플레이어 이동, NPC 대화, 씬 전환) | ⬜ 예정 |
| Phase 3 | 전투 시스템 기본 구현 (1회 전투 완전 루프) | ⬜ 예정 |
| Phase 4 | 대화/연출 시스템 완성 (TextAnimator, 분기, 이벤트) | ⬜ 예정 |
| Phase 5 | UI 완성 및 통합 (인벤토리, HUD, 세이브/로드 UI) | ⬜ 예정 |
| Phase 6 | 콘텐츠 제작 (맵, 적, 스토리 도입부) | ⬜ 예정 |
| Phase 7 | 밸런싱 및 폴리싱 (QA, 최적화, 빌드) | ⬜ 예정 |

## 📚 관련 문서

| 문서 | 설명 |
|------|------|
| [`codebase-reference.md`](./codebase-reference.md) | 전체 코드 구조, 용법, API 레퍼런스 |
| [`milestone.md`](./milestone.md) | 상세 마일스톤 및 작업 체크리스트 |
| [`battle.clinerules`](./battle.clinerules) | 전투 시스템 설계 규칙 |
| [`characters.clinerules`](./characters.clinerules) | 캐릭터 시스템 설계 규칙 |
| [`overworld.clinerules`](./overworld.clinerules) | 오버월드 시스템 설계 규칙 |
| [`core.clinerules`](./core.clinerules) | 코어 시스템 설계 규칙 |
| [`dialogue.clinerules`](./dialogue.clinerules) | 대화/연출 시스템 설계 규칙 |
| [`design-notes.md`](./design-notes.md) | 게임 디자인 노트 및 아이디어 |
