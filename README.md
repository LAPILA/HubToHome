# HubToHome

Unity 6 기반 2D 탑다운 탐색 + 2.5D 전투 연출 RPG 프로젝트입니다. 대화, 오버월드 탐색, 전투, UI, 저장/복구 루프를 점진적으로 붙이고 있습니다.

## AI 작업 시작점

AI나 자동화 도구가 이 저장소에서 작업할 때는 먼저 아래 문서를 읽어야 합니다.

1. `AGENTS.md`
2. `CONTEXT.md`
3. 시나리오/전투 플로우/Action Sequence 작업이면 `.agents/skills/hubtohome-scenario-authoring/SKILL.md`
4. `AIAssets/index.md`
5. `RuleFileforAI/mainrule.clinerules`
6. 작업 영역에 맞는 `RuleFileforAI/*.clinerules`

작업 후에는 `AIAssets/YYYY-MM-DD-update.md`에 의도, 변경점, 검증, 후속 위험을 남깁니다. 사람이 읽을 리뷰/분석 문서는 `AIAssets/yjlim/feedback/`에 둡니다.

## 주요 폴더

```text
Assets/_Game/
├─ Content       # Art, Audio, Maps, 캐릭터·전투·시나리오 콘텐츠
├─ Core          # 전역 런타임 Prefab
├─ Presentation  # UI, VFX, 후처리 자산
├─ Resources     # 런타임 ID 조회용 콘텐츠 카탈로그
└─ Scripts       # 도메인별 Runtime, Editor, Tests C# 코드

AIAssets/        # AI/사람 공용 작업 기록, 분석, 업데이트 노트
.agents/         # 공유 AI 스킬과 작업 절차
RuleFileforAI/   # 도메인별 AI 작업 규칙
docs/            # 설계 문서와 구현 계획
```

## 현재 큰 흐름

타이틀/인트로 → 이름 입력 → 오버월드 → 대화/트리거 → 심리스 전투 또는 전용 BattleScene → 결과 복귀 흐름을 기준으로 합니다.

## 버전 관리

- 작업은 브랜치에서 진행합니다.
- 의미 있는 단위로 커밋합니다.
- 원격 push는 사람의 명시 승인 후 진행합니다.
