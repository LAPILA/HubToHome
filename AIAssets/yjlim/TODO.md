# yjlim TODO 체크리스트

> 기준: 2026-06-19 KST  
> 상세 배경: `feedback/2026-06-19-work-summary.md`

## P0 - 플레이 루프 차단/체감 버그

- [ ] 타이틀 `Continue`를 실제 저장 슬롯 로드와 저장된 씬 복구로 연결
  - 대상: `TitleMenuManager`, `SaveManager`, `SaveData`, `GlobalDataManager`, `SceneLoader`
- [ ] 오버월드 적 도주 후 collider/이동/알파/재조우 쿨다운 상태 검증 및 안정화
  - 대상: `OverworldEnemy`, `BattleManager`, `GlobalDataManager`, 필요 시 `PlayerController`
- [ ] 승리/도주/패배 결과별 encounter id와 encounter memory 남는 값 로그 검증

## P1 - 이미 붙은 시스템 마감

- [ ] 설정 패널 fallback 문자열을 `LocalizationTable.csv` 기준으로 정리
- [ ] Voice 볼륨 옵션 필요 여부 결정 및 저장 키/UI 행/AudioManager 정책 확정
- [ ] `BattleManager`에서 적 행동 실행, 방어 판정, 전투 종료 처리 분리 계획 수립 및 단계 적용
- [ ] 기본공격 방어와 스킬 `Action_DefenseWindow` 피해/보상 정책을 공용 defense policy로 통합
- [ ] 전투용 `CameraController`를 TMP 샘플 폴더에서 first-party 폴더로 옮기는 migration 계획 수립

## P2 - 시나리오 파이프라인 실사용 검증

- [ ] Sequence Maker에서 ZEV clone scenario 열기 → 액션 편집 → 저장 및 반영 → runtime asset 반영 확인
- [ ] Scenario Source YAML과 generated ScriptableObject가 stale 상태로 갈라지지 않는지 반복 검증
- [ ] Catalog validation에서 누락된 action parameter metadata / Korean label / example 정리
- [ ] `aim_shooter`를 실제 마우스 조준, projectile, VFX, 전용 UI loop로 확장
- [ ] Game Module outcome 이벤트가 Battle Event Rule 후속 sequence로 안정적으로 이어지는지 추가 테스트

## P3 - 확장 전 구조 정리

- [ ] `InventoryManager` 상태이상 문자열 분기를 `StatusFactory` 또는 registry로 전환
- [ ] 대화 선택지 텍스트를 현지화 키 기반으로 전환
- [ ] GameOver/패배 후 리스폰/저장 복구 정책 확정
- [ ] MapFieldStarter 샘플을 production 오버월드 구조로 승격할지, 샘플로 분리 유지할지 결정

## 문서 관리

- [ ] 의미 있는 작업 후 `AIAssets/YYYY-MM-DD-update.md` 작성 또는 갱신
- [ ] 분석/리뷰/인수인계는 `AIAssets/yjlim/feedback/`에 추가
- [ ] 패치노트형 요약은 `AIAssets/yjlim/Patchnote/`에 추가
- [ ] 시나리오 파이프라인 규칙 변경 시 `.agents/skills/hubtohome-scenario-authoring/` references 동시 갱신