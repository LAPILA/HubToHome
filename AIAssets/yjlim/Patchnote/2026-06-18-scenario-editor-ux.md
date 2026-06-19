# 2026-06-18 시퀀스 메이커 UX 패치노트

## 요약

시퀀스 메이커를 사람이 읽고 편집하기 쉬운 3패널 보드형 에디터로 개선했습니다.

## 변경점

- 왼쪽에서 시나리오 개요, 규칙, 시퀀스 목록, 검증 요약을 봅니다.
- 가운데에서 선택한 시퀀스의 액션 타임라인을 봅니다.
- 오른쪽에서 선택한 액션의 설명과 파라미터를 편집합니다.
- 액션 row를 클릭하면 해당 액션이 선택되고 인스펙터가 갱신됩니다.
- 카탈로그에 파라미터 metadata가 있으면 한국어 라벨/설명/필수 표시를 사용합니다.
- 카탈로그 metadata가 부족한 액션은 현재 JSON 키를 읽어 fallback 입력칸을 보여줍니다.
- raw JSON 편집은 `고급 JSON` foldout 아래로 숨겼습니다.
- `저장 및 반영` 버튼을 추가했습니다.
  - YAML 저장
  - source metadata 갱신
  - 런타임 에셋 안전 반영
  - 순서로 실행합니다.
- ZEV 아키텍처 클론 샘플 카탈로그에 기본 파라미터 metadata를 추가했습니다.
- Unity 메뉴와 창 제목은 `시퀀스 메이커`로 통일했습니다.

## 사용 위치

- Unity 메뉴: `HubToHome/시나리오/시퀀스 메이커`
- 샘플 카탈로그: `Assets/_Game/Features/Scenario/Data/Catalogs/ScenarioActionCatalog_ZEV_ArchitectureClone.asset`

## 검증

- Unity MCP EditMode tests: 32/32 통과
- `dotnet build HubToHome.sln --no-restore -v:minimal` 통과
- 시퀀스 메이커 메뉴 실행 시 콘솔 오류 없음
- ZEV 아키텍처 클론 샘플 에셋 재생성 성공

## 남은 일

- DialogueData / AudioClip / Module ID 선택기를 붙이면 JSON 의존을 더 줄일 수 있습니다.
- 규칙 `when` 전용 편집 패널이 필요합니다.
- 시퀀스 액션 drag-and-drop reorder는 후속으로 두는 것이 좋습니다.
