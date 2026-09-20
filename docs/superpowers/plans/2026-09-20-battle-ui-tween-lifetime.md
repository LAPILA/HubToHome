# Battle UI Tween Lifetime Implementation Plan

사용자의 바로 수정/최종 정리 요청에 따라 현재 브랜치에서 진행한다. 서브 에이전트, Unity 실행/Refresh/씬 저장, 커밋/푸시는 하지 않는다.

**Goal:** 파괴된 Image를 DOColor가 갱신하는 오류의 프로젝트 내 경로를 제거하고 전투 UI의 트윈 수명을 명확히 한다.

**Architecture:** Image DOColor의 프로젝트 호출자는 BattleMenuUI와 PartySlotUI뿐이다. PartySlotUI를 직렬화 이름 그대로 독립 파일로 분리하고 트윈 핸들을 직접 소유한다. BattleUIController와 BattleMenuUI의 숨김/해제 수명주기에서 정리한다. 이미지/텍스트 콜백은 생성 당시 대상과 Unity null 검사를 사용한다. 전체 DOTween KillAll/예외 로그 억제/외부 플러그인 수정은 하지 않는다.

**Tech Stack:** Unity 6, C#, DOTween, 기존 NUnit 회귀 파일, 직렬 dotnet build.

## 검증 가능한 근거 및 제한

- 사용자 스택은 DOTweenModuleUI의 Image 전용 DOColor setter(69행)다. Graphic/TMP 색상 트윈과 SpriteRenderer는 다른 overload다.
- 파티 슬롯은 생성 트윈을 보관하지 않고 Hide는 SetActive만 수행한다. BattleUIController.OnDestroy는 슬롯 트윈을 해제하지 않는다.
- BattleMenuUI는 기반 UIPanel의 CanvasGroup 트윈만 정리한다. 선택 Image 트윈 및 세 곳의 DelayedCall은 해제 대상에서 빠져 있다.
- 실제 런타임의 어느 Image인지 스택만으로 확정할 수는 없다. 기존 사용자 제한으로 Unity 재현/회귀 실행은 생략하고 해당 두 호출 경로 모두 수정한다. 정적 검사 및 컴파일 결과를 실제 재현 해결로 표현하지 않는다.

## 작업

- [x] 파티 슬롯 Hide/소유자 종료, 외부 트윈 보존, Image 단독 파괴, 반복 Highlight 회귀 코드를 먼저 추가. 이후 메뉴/플래시 교체까지 총 9건. Unity 실행 없이 컴파일만 검증.
- [x] PartySlotUI 분리 및 HP/AP/초상화/펀치 트윈 직접 소유·정리, 즉시 값 반영과 완료 콜백 안전화.
- [x] BattleUIController의 슬롯/패널 트윈 정리와 이벤트 구독 대상 수명 정리.
- [x] BattleMenuUI의 버튼 색상/크기/이동/지연 콜백 수명을 한 종료 경로로 통합. 강제 Complete로 이전 액션을 실행하는 경로 제거.
- [x] Runtime/Editor 최종 컴파일 오류 0(기존 경고 2, 최초 의존성 컴파일에는 패키지 경고 1 추가), 정적 검사 12건, diff 공백 검사 및 문서 업데이트. 실제 재현/Unity 회귀는 미실행.
