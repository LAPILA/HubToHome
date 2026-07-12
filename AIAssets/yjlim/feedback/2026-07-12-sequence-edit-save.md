# 시퀀스 편집 기록과 안전 저장

## 바뀐 점

시퀀스 블록 편집을 Runtime Asset 전체 복사나 Unity Undo에 맡기지 않고, 각 변경을 작은 명령으로 기록한다. 블록 추가, 이동, 복제, 삭제, 활성화 변경, 파라미터 변경을 되돌리거나 다시 적용할 수 있고, 중첩 블록도 Block ID를 기준으로 같은 방식으로 다룬다.

여러 블록을 한 번에 바꾸는 작업은 하나의 transaction으로 묶인다. 작업 도중 하나가 실패하면 앞에서 적용한 변경도 역순으로 취소된다. 블록을 이동하거나 삭제한 뒤에도 가능한 경우 선택 위치를 유지한다.

## YAML 저장 방식

Battle Scenario와 독립 Action Sequence가 같은 저장 절차를 사용한다.

1. 현재 Runtime Asset을 YAML text로 만들고 validation을 통과시킨다.
2. 기존 YAML hash가 마지막 동기화 시점과 같은지 확인한다.
3. 원본과 같은 폴더에 임시 파일을 만든다.
4. 임시 파일을 다시 읽어 내용이 정확한지 확인한다.
5. 임시 YAML을 실제 importer로 다시 불러와 구조와 catalog 계약을 검증한다.
6. 원본이 검증 중 다시 바뀌지 않았는지 확인한다.
7. 기존 파일은 원자적으로 교체하고, 새 파일은 검증된 임시 파일을 이동한다.
8. 파일 반영이 성공한 뒤에만 Runtime Asset의 source path, hash, 시각을 갱신한다.

외부에서 YAML이 바뀌었거나 기준 hash를 알 수 없으면 자동으로 덮어쓰지 않는다. 이후 Sequence Maker UI에서는 이 상태를 충돌 화면으로 보여주고, 원본 다시 불러오기, 다른 이름으로 저장, YAML 비교 흐름으로 연결한다.

## 확인 결과

- 편집 명령 테스트 14개 통과
- 안전 저장 테스트 14개 통과
- Battle Scenario와 독립 Action Sequence 모두 YAML 왕복 및 metadata 갱신 확인
- write, replace, parser validation 실패 시 기존 원본 유지 확인
- 저장 검증 도중 원본이 다시 바뀌는 경우도 교체 전에 중단 확인
