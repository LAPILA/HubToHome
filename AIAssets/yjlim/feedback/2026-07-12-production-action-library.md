# 공식 Action Library

## 결과

- Sequence Maker가 사용할 공식 Action 계약 28개 작성
- 10개 카테고리로 검색과 필터 가능
- 각 Action에 한국어 이름, 설명, 사용 시점, 한 줄 요약, 태그, 파라미터 설명, 미리보기 정책 포함
- Runtime Adapter와 Action 계약의 양방향 누락 자동 검증
- 카테고리 YAML 검증 성공 후에만 통합 Unity 에셋 갱신

## 원본과 생성 에셋

- 원본: `Assets/_Game/Content/Scenarios/ActionLibrary/Source/*.actions.yaml`
- 생성 에셋: `Assets/_Game/Content/Scenarios/ActionLibrary/Generated/ActionLibrary.asset`
- Unity 메뉴: `HubToHome > 시나리오 > Action Library 다시 만들기`

## 카테고리

- 흐름: 대기, 병렬, 시퀀스 호출
- 대화: 공통 대화 실행과 완료 대기
- 오디오: BGM 전환
- 화면: 전체 화면 페이드
- 시네마틱: Stage 준비/재생/해제, 레터박스
- 카메라: 전투 대상 강조와 복귀
- 캐릭터: 포즈, 방향, 이동, 낙하 등장, 연출 공격, 슬롯 복귀
- 게임 모듈: 전환과 시작
- 전투: 기존 스킬 호환, HP/MP 명령, 전투 플래그
- Timeline: 등록된 Timeline 컷신 재생

## 안전장치

- 원본 중복 ID는 양쪽 파일 경로와 함께 오류 표시
- 어댑터만 있고 설명이 없거나 설명만 있고 실행 코드가 없으면 오류 표시
- 검증 실패 시 기존 생성 에셋 유지
- 생성 에셋에 전체 원본 경로와 source hash 기록
