# AIAssets Index

최신 진입 연출: [심리스 전투 — 경고·짧은 줌·사선 절단·상하 개방·HUD 등장](yjlim/feedback/2026-10-02-battle-entry-reference.md). 후속 요청으로 약 2초/줌 0.5초, 완전 암전 뒤 캐릭터·NPC·UI·카메라 준비로 수정했습니다. 공용 Host 값과 조우별 켜기/끄기 제공, 전용 BattleScene 유지. Runtime/Editor 컴파일 오류 0, Unity Play는 미실행입니다.

최신 제작 문서: [Google Docs 콘텐츠 제작 가이드 갱신](yjlim/feedback/2026-10-02-content-guide-refresh.md) · [원본 가이드 열기](https://docs.google.com/document/d/1kY_qJeiVUwpHJNuCOCk_ac3LsDe85bAiR0iBLx3Py2E/edit). 기존 14장/18표를 보존하고 스킬 13종·전조·압력·상점·캐릭터 데이터 안내를 현행화했습니다. PDF 22쪽 검토 및 표 경로 넘침 수정 완료. [검증 기록](2026-10-02-update.md).

최신 제작 도구: [스킬 블록 한국어·분류·방어 설정 정리](yjlim/feedback/2026-09-28-skill-block-authoring.md). 기존 메이커에서 실행 블록 추가 메뉴와 사용 안내를 정리했습니다. 전투 실행 로직·스킬 자산은 유지합니다.

최신 이펙트 수정: [전조 재생 완료 시 숨김·방어창 종료 시 풀 반환](yjlim/feedback/2026-09-26-telegraph-animation.md). 마지막 프레임 잔류를 제거하고 판정 시간은 유지했습니다. 제작 중인 Aseprite/프리팹은 변경하지 않았습니다. [당일 검증 기록](2026-09-28-update.md).

최신 전투 규칙: [속도 기반 턴 — 최종 규칙·편집 위치·턴 큐 연출](yjlim/feedback/2026-09-28-speed-turns.md) · [2026-09-28 변경/검증 기록](2026-09-28-update.md). SPD는 순서와 행동 빈도에 반영됩니다. 자연 만료를 예고에 미리 반영해 이미 표시한 연속 턴이 만료 뒤 사라지는 오류를 수정했습니다. 순수 검사 19개 통과(100조합/4,800행동), Unity 연결 검사는 컴파일만 확인했습니다.

최종 정리: **증기 가속 샘플 스킬과 DB·카탈로그 연결은 삭제**, 스킬 기획은 보류합니다. 공용 속도 상태·턴 계산·DOTween 재정렬은 유지합니다. 원작 내부 공식 복제가 아닌 프로젝트의 누적 준비량 방식이며, 사용자 PUSH 요청에 따라 현재 브랜치에 반영합니다.

환경 연출: [픽셀 풀 바람 — UV 픽셀 변형·실험실 적용·미동작 원인 수정](yjlim/feedback/2026-09-27-pixel-wind.md). 기존 PixelWind/밝기 파동은 삭제했습니다. 혼합 텍스처 검사로 꺼지던 원인을 수정해 BunnySlimeBattleLab의 Grid/Tilemap(잔디 252칸·바람)과 Tilemap_Walls(벽 168칸·정적)를 분리했습니다.

바람 제작 편의: [7가지 시작값·위/아래 고정·엇박자 조절·아트 분리 기준](../Assets/_Game/Presentation/PixelFoliage/README.md). 프리셋은 명시적으로 적용할 때만 기존 값을 바꿉니다.

고유 자원 및 표시: [위젤 압력 — 데이터·스킬 메이커·단계 애니메이션 수정 위치](yjlim/feedback/2026-09-27-wizel-pressure.md). 오른쪽 ActorLargePortrait는 선택 아군의 자원 표시입니다. 후속: [ZEV 스킬 이동 데이터 미세 조정](yjlim/feedback/2026-09-27-zev-movement-tuning.md) · [C 압력·피해 숫자·대사 위치 및 레터박스 앞 표시](yjlim/feedback/2026-09-27-counter-popups-dialogue.md).

`AIAssets` 루트의 오래된 개별 정리 문서는 `AIAssets/yjlim/` 스타일로 재정리했습니다.

## 먼저 볼 문서

최신 통합: [2026-09-27 토끼 슬라임·ZEV 전투 실험실](yjlim/feedback/2026-09-27-battle-lab-consolidation.md) · [당일 검증/변경 기록](2026-09-27-update.md). 종합/가드·회피/C 입구를 통합했고, ZEV 원본 5스킬·전조를 연결했습니다. 임시 스킬도 Content/Skills로 통합했으며 3+3 전멸 실습은 별도로 유지합니다.

최신 버그 수정: [빈 공격 안내 프레임·확인 입력 중복](yjlim/feedback/2026-09-26-battle-narration-input-fix.md). 빈 내레이션은 창을 켜지 않으며, 버튼 콜백에도 입력 소비/활성화 프레임 가드를 적용합니다.

전투 시인성: [이동 중에만 잔상 표시](yjlim/feedback/2026-09-27-movement-only-ghosts.md). 판정: [2026-09-26 근접 첫 전조부터 패링·즉시 입력 반응](yjlim/feedback/2026-09-26-melee-parry-cue.md).

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
