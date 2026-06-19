# yjlim 작업 정리 인덱스

> 기준: 2026-06-19 KST  
> 목적: `AIAssets` 루트에 흩어진 오래된 정리를 yjlim 스타일의 읽기 쉬운 문서로 재구성

## 읽는 순서

1. `feedback/2026-06-19-work-summary.md`  
   지금까지 한 것, 아직 안 한 것, 더 해야 할 것 종합 정리.
2. `TODO.md`  
   다음 작업자가 바로 이어갈 체크리스트.
3. `Patchnote/2026-06-19-aiassets-reorganization.md`  
   이번 문서 정리에서 무엇을 바꿨는지 요약.
4. 기존 상세 문서  
   - `feedback/scenario-authoring-pipeline-2026-06-14.md`
   - `feedback/scenario-runtime-architecture-progress-2026-06-15.md`
   - `feedback/scenario-runtime-architecture-progress-2026-06-16.md`
   - `feedback/zev-scenario-clone-architecture-map-2026-06-17.html`
   - `Patchnote/2026-06-15-qte-module-controller.md`
   - `Patchnote/2026-06-16-aim-shooter-module-shell.md`
   - `Patchnote/2026-06-16-scenario-runtime-reimport.md`
   - `Patchnote/2026-06-17-zev-scenario-clone.md`
   - `Patchnote/2026-06-18-scenario-editor-ux.md`

## 폴더 역할

- `feedback/`: 분석, 구조 지도, 인수인계, 검증 메모.
- `Patchnote/`: 사람이 빠르게 읽는 변경 요약.
- `TODO.md`: 다음 작업 우선순위와 체크리스트.

## 루트 AIAssets 처리 기준

- 루트 `index.md`, `context-briefing.md`, `architecture.md`, `todo.md`, `milestones.md`는 더 이상 장문 원본을 중복 보관하지 않습니다.
- 루트 문서는 yjlim 문서로 안내하는 얇은 진입점으로만 유지합니다.
- 날짜별 update note는 작업 단위 기록으로 남기되, 장기 인수인계는 yjlim 문서에 누적합니다.