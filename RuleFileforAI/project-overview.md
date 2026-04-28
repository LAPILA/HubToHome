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

| 단계 | 내용 | 상태 |
|------|------|------|
| Phase 0 | 프로젝트 세팅 및 폴더 구조 정리 | ✅ 완료 |
| Phase 1 | 코어 시스템 구현 (GlobalDataManager, SceneLoader) | ⬜ 예정 |
| Phase 2 | 오버월드 기본 구현 (플레이어 이동, 맵) | ⬜ 예정 |
| Phase 3 | 전투 시스템 기본 구현 | ⬜ 예정 |
| Phase 4 | 대화/연출 시스템 구현 | ⬜ 예정 |
| Phase 5 | UI 완성 및 통합 | ⬜ 예정 |
| Phase 6 | 콘텐츠 제작 및 밸런싱 | ⬜ 예정 |
