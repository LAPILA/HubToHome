# 공식 시퀀스 메이커 작업

## Unity 메뉴

- `HubToHome > 시나리오 > 시퀀스 메이커`
- 기존 시퀀스 메이커 메뉴도 공식 창으로 연결
- Odin 시퀀스 편집 메뉴 제거, migration test 코드만 유지

## 시퀀스 편집

- 세로 Block Flow
- 순차/병렬 중첩 표시
- 다중 선택, 이동, 복사, 붙여넣기, 복제, 삭제
- 병렬 묶기, Action 교체, 별도 Sequence 추출
- Action 이름, 설명, 사용 시점, typed parameter, binding 편집
- Sequence 설명, 용도, 태그, lifecycle, Primary Mode, typed input 편집
- Input ID 변경 시 연결된 binding 자동 변경

## 이벤트 규칙

- 왼쪽 이벤트 규칙 목록
- 중앙 `WHEN / IF / DO` 편집
- all/any 중첩 조건, 부정, 이동, 삭제
- Event payload와 context 값을 넣는 규칙 시뮬레이터
- 조건별 match 결과와 target input 결과 표시
- 기존 Battle Event Rule을 확장 Trigger Rule로 변환
- Event, Condition, target Sequence 검색 선택

## 미리보기와 테스트

- 안전 미리보기
- 선택 Block까지 빠른 준비 실행
- Play Mode 실동작 테스트
- 선택 Block 이전 상태 준비 후 정상 재생
- 일시정지, 계속, 한 Block 실행, 중지
- 실행 기록과 Block 이동
- BattleManager, Scene Action Sequence Trigger runtime context 지원

## 검증과 저장

- Problems 필터, 검색, 복사, 문제 위치 이동
- YAML 외부 변경 충돌 표시
- source reload, 확인 후 overwrite, 파일 열기
- `Library` 로컬 복구 기록 자동 생성
- 복구 기록 restore/delete
- 검증, 임시 파일 readback, round-trip 후 atomic YAML 저장

## 현재 검증 상태

- C# 전체 빌드 오류 0개
- Unity EditMode 전체 `485/485` 통과
- 공식 시퀀스 메이커 창 재오픈 후 UI Toolkit/C# 콘솔 오류 0개
- 200 Block 세로 canvas 생성/선택/복사/삭제 자동 검증 통과
- Overworld 지하철 Preparation Run final-state/복구 검증 통과
- ZEV 복제 테스트 씬 Play Mode에서 `turn_qte -> HP Trigger -> aim_shooter` 전환 PASS
- ZEV phase2 YAML에서 누락됐던 fade, module switch/start, shooter 대사/BGM, battle flag를 복원하고 Runtime Asset 재동기화

## 편집 안정성 강화

- 여러 시퀀스를 번갈아 열어도 현재 시퀀스의 미저장 상태만 표시
- 한 시퀀스를 저장해도 다른 시퀀스의 Undo/미저장 상태 유지
- Battle 저장 시 해당 Battle과 포함 시퀀스만 저장 완료 처리
- `저장하지 않음` 대신 실제 동작에 맞는 `로컬 변경 유지하고 이동` 안내
- 텍스트 입력 중 Ctrl+Z/Ctrl+Y는 글자 편집에 사용
- 캔버스 포커스의 Ctrl+Z/Ctrl+Y는 시퀀스 편집에 사용
- 저장 실패, 외부 YAML 충돌, 명시적 덮어쓰기, 창 UI 재생성 회귀 검증 추가
- Unity EditMode 전체 `512/512` 통과
