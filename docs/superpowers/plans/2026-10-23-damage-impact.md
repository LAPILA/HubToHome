# 공통 명중 피드백 구현

**승인 설계:** [명중 피드백](../specs/2026-10-23-damage-impact-design.md)

- [x] 공통 직접/순수/상태 피해, 반격·연타·시간 소유권 조사
- [x] 기존 CharacterHitFeedback 즉시 표시·강도/연타 정책 보강
- [x] Player/Enemy VFX 접점 연결·상태 피해 상쇄와 순수 0피해 방어
- [x] 수명/감전/다중 명중 회귀 검사 및 전체 CLI 컴파일
- [x] 독립 검토·문서 갱신

제약: 기존 dirty 변경 보존, Unity Play/Refresh/Reimport/씬 저장·실제 save I/O·커밋/push 금지. 새로운 스킬 블록·시나리오 문법·자산 이동은 없음.

결과: Runtime436/Editor362 CLI 오류0(경고225/0), 생산 수명 코드 관리형196단언 통과. Editor 회귀는 소스 컴파일까지이며 엔진 NUnit·체감/GPU/기기는 미검증. [후속 기록](../../../AIAssets/2026-10-23-update.md) · [조정 안내](../../../AIAssets/yjlim/feedback/2026-10-23-damage-impact.md).
