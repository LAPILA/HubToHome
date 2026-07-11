# 선택 블록 준비 실행

## 무엇이 추가됐나

- 선택한 블록부터 재생하기 전에 앞 블록의 결과 상태를 빠르게 만드는 Preparation Run
- 화면, BGM, Game Module을 실제 게임 서비스와 분리한 Safe Preview context
- 중첩 시퀀스와 병렬 블록 준비
- 선택값이 필요한 블록의 입력 대기와 재개
- 실패, 취소, 에디터 상태 전환 시 원래 상태 복원

## 정책

| 정책 | 준비 동작 |
| --- | --- |
| `ApplyFinalState` | 시간 연출 없이 최종 상태 적용 |
| `ExecuteIsolated` | 분리된 상태에서 하위 흐름 실행 |
| `SkipPresentation` | 대기, 일반 대사 같은 일시 표현 생략 |
| `RequireInput` | 기본값 사용 또는 입력을 받을 때까지 일시정지 |
| `Unsupported` | 정확한 Block ID와 함께 실행 차단 |

## 안전 규칙

- Safe Preview는 production context를 받지 않음
- 저장, 보상, Scene 전환, 외부 효과 실행 전 차단
- 미리보기 중 Runtime Asset과 Block ID를 수정하지 않음
- 화면, BGM, Game Module은 격리된 preview 서비스 사용
- Scene 오브젝트는 상태 스냅샷과 Unity Undo 그룹으로 복원

## 병렬 블록

- `all`: 모든 자식 최종 상태 준비
- `any`, `race`: `previewWinner`로 준비할 직접 자식 Block ID 지정
- `previewWinner`가 없으면 에디터가 결과를 추측하지 않고 중단

## 실제 검증

- 정책과 안전성 집중 테스트 29개 통과
- 오버월드 지하철 Stage 최종 위치, 카메라 줌 적용과 원상 복원 통합 테스트 1개 통과
- 총 30/30 통과
