# AIAssets Index

`AIAssets` 루트의 오래된 개별 정리 문서는 `AIAssets/yjlim/` 스타일로 재정리했습니다.

## 먼저 볼 문서

최신 버그 수정: [빈 공격 안내 프레임·확인 입력 중복](yjlim/feedback/2026-09-26-battle-narration-input-fix.md). 빈 내레이션은 창을 켜지 않으며, 버튼 콜백에도 입력 소비/활성화 프레임 가드를 적용합니다.

최신 판정 수정: [2026-09-26 근접 첫 전조부터 패링·즉시 입력 반응](yjlim/feedback/2026-09-26-melee-parry-cue.md).

최신 스킬: [2026-09-26 압력 난무 독립 QTE·공중 회전 베기](yjlim/feedback/2026-09-26-independent-barrage-aerial-skill.md). 난무는 0.1초 간격 50타, Z/X/C는 독립 주기입니다. 360도 회전은 별도 공중 스킬의 카메라에만 적용하며 캐릭터 자체는 회전시키지 않습니다.

교전 연출: [짧은 카메라 전환·고정 HUD](yjlim/feedback/2026-09-26-battle-camera-beats-rapid-skill.md). 상시 드리프트/호흡 줌 없음. [앞선 동적 연출](yjlim/feedback/2026-09-26-dynamic-battle-presentation.md)의 대사/UI 반응/C 후퇴 반격과 [중앙 교전](yjlim/feedback/2026-09-26-central-duel-presentation.md)의 캐릭터 배치는 유지합니다.

최근 전투 작업: [2026-09-20 중앙 궁극기·피격 대상 전진·지상 복귀](2026-09-20-update.md) · [사용자 확인 및 미결정 사항](yjlim/feedback/2026-09-20-battle-timing.md) · [실험실 사용법](../docs/bunny-slime-battle-lab.md).

최신 UI 수정: [2026-09-26 목업 기준 전투 HUD 개편](yjlim/feedback/2026-09-26-battle-hud.md) · [작업/검증 기록](2026-09-26-update.md). 이전: [파괴된 Image 트윈 수명 정리](yjlim/feedback/2026-09-20-battle-ui-lifetime.md).

최신 전조 연결: [2026-09-26 Telegraph Aseprite 애니메이션·전투 메뉴 SFX](yjlim/feedback/2026-09-26-telegraph-animation.md).

최신 화면 샘플: [2026-09-26 토끼 실험실 2D 조명·Volume 조절법](yjlim/feedback/2026-09-26-bunny-lab-lighting.md).

1. `yjlim/README.md` - 현재 문서 구조와 읽는 순서
2. `yjlim/feedback/2026-06-19-work-summary.md` - 지금까지 한 것 / 안 한 것 / 더 해야 할 것 종합 정리
3. `yjlim/TODO.md` - 다음 작업 체크리스트
4. `yjlim/Patchnote/2026-06-19-aiassets-reorganization.md` - 이번 문서 정리 패치노트
5. 최신 `YYYY-MM-DD-update.md` - 당일 작업 update note
6. [프로젝트 학습 자료](../docs/learning/README.md) - 실제 코드 기반 C#·알고리즘·자료구조·Unity·전투·시나리오·제작 도구·패턴·실습

## 운영 규칙

- 분석/리뷰/아키텍처/인수인계 문서는 `yjlim/feedback/`에 둡니다.
- 패치노트형 요약은 `yjlim/Patchnote/`에 둡니다.
- 루트 문서는 중복된 장문 기록을 쌓지 않고 `yjlim/` 문서로 안내하는 얇은 진입점으로 유지합니다.
- 시나리오 파이프라인 작업은 `.agents/skills/hubtohome-scenario-authoring/` 규칙을 먼저 확인합니다.
