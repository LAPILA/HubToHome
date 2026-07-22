# 심리스 전투 Host 수명주기 인수인계

## 결론

`HUBTOHOME-34`의 Room 내 심리스 전투 종료 경로를 하나로 통합했다. 승리, 도주, Host 비활성화 중단 모두 오버월드 플레이어 위치와 입력, 카메라, 전투 UI, 임시 참가자, 조우 문맥을 정리한다. 전용 `BattleScene` 진입과 복귀 흐름은 변경하지 않았다.

## 소유권과 종료 순서

- `SeamlessBattleHost`는 Room의 전투 런타임 구성 루트이며 활성 Host는 하나만 허용한다.
- 중복 Host는 자식 singleton 일부가 아니라 중복 루트 전체를 제거한다.
- 정상 결과 결정은 기존 `BattleManager`가 유지하고, 심리스 승리·도주·강제 중단은 공통 정리 경계를 사용한다.
- 정리 순서는 QTE 취소, 참가자 잠금 해제, 오버월드 위치 복구, 조우 콜백, 임시 전투 오브젝트 제거, UI·카메라 복구, 전투 세션 초기화, Exploration 복귀다.
- 중단 경로는 보상이나 승패 콜백을 만들지 않으며 반복 호출해도 추가 부작용이 없다.

## 연출 수명주기

- 전투 말풍선은 비활성화·파괴·즉시 숨김에서 자신이 만든 CanvasGroup 및 크기 트윈을 종료한다.
- `SceneLoader`는 진행 중인 fade 트윈을 명시적으로 소유하고 복구·파괴 시 제거한다.
- `DefenseQTEUI`는 바, 결과 Sequence, 콜백 펀치 트윈에 컴포넌트 ID를 지정하고 비활성화·파괴 시 `DOTween.Kill(ownerId)`로 일괄 종료한다.
- 이 DOTween 버전에서는 첫 업데이트 전 열린 Sequence에 대한 인스턴스 `Kill()`이 무시될 수 있다. 즉시 취소 가능해야 하는 Sequence는 소유자 ID를 부여하고 ID 기반으로 종료한다.

## 검증

- `DefenseQTEUIPresentationTests`: 2/2 통과
- `TestMapEncounterPlayModeTests`: 6/6 통과
- 전체 Unity EditMode: 710/710 통과
- Project Content Validation: 오류 0건, 기존 선택 자산 경고 10건
- `Assets/_Game` Prefab 58개: Missing Script 0건
- Scene, Prefab, ScriptableObject 변경 없음

## 남은 수동 확인

- 실제 키보드로 일반 승리, 도주, 전투 중 Room 이탈을 각각 한 번 확인한다.
- 맵 BGM 복구는 현재 `MapSettings`에 안정적인 BGM 소유권 계약이 없어 별도 오디오 라우팅 업무로 남긴다.

## 도구 교훈

- 코드 변경 직후 Test Runner가 이전 어셈블리를 한 번 실행할 수 있다. 테스트 개수가 갱신되지 않으면 `Editor.log`의 컴파일·도메인 재로드 완료를 확인한 뒤 다시 실행한다.
- EditMode의 `DestroyImmediate`에 런타임 수명주기 호출을 의존하지 않는다. 테스트 픽스처는 소유 트윈을 명시적으로 정리하거나 테스트 더블로 정리 경계를 호출한다.
