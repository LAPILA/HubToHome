# 재사용 시퀀스 입력과 호출

## 무엇이 추가됐나

- 시퀀스마다 외부에 공개할 입력을 선언할 수 있음
- 액션 값에 `${input.actor}`, `${event.subject}` 같은 안전한 값 참조 사용 가능
- `sequence.call` 액션으로 다른 시퀀스를 ID로 호출 가능
- 호출 대상의 필수 입력, 기본값, 타입을 실행 전에 확인
- 직접 또는 간접 재귀 호출을 저장 전 검증과 런타임 양쪽에서 차단

## 동작 흐름

1. 호출하는 시퀀스가 `sequence.call`의 `inputs` 값을 작성
2. `ActionDirector`가 부모 컨텍스트의 바인딩을 실제 값으로 해석
3. 대상 시퀀스 전용 자식 컨텍스트 생성
4. 대상 시퀀스가 선언한 입력만 자식 컨텍스트에 기록
5. 대상 시퀀스 실행
6. 성공, 실패, 취소 결과를 호출한 시퀀스로 전달

## YAML 예시

```yaml
sequences:
  shared.actor_move:
    inputs:
      - id: actor
        name: "이동 캐릭터"
        type: actorRef
        required: true
      - id: speed
        name: "이동 속도"
        type: number
        default: 1.5
    - actor.move:
        actor: ${input.actor}
        speed: ${input.speed}

  battle.phase_transition:
    - sequence.call:
        sequence: shared.actor_move
        inputs: {"actor":"zev","speed":2.0}
```

## 설계상 중요한 점

- 기존 액션 어댑터는 여전히 리터럴 JSON만 받으므로 QTE와 기존 연출 코드 변경 없음
- 지원하는 값 루트만 허용하며 임의 코드나 표현식 실행 없음
- 부모와 자식은 대화, 카메라, 오디오 같은 서비스를 공유하지만 입력과 실행 결과는 별도 스코프로 관리
- Block ID를 기준으로 없는 호출 대상과 순환 호출 위치를 정확히 표시 가능

## 검증

- 값 해석, 컨텍스트 상속, 필수 입력, 기본값, 타입 불일치 테스트
- YAML 입력과 `${...}` 바인딩 왕복 테스트
- 중첩 호출, 부모 값 전달, 없는 대상, 자식 실패 전파, 런타임 재귀 방어 테스트
- 직접/간접 순환 그래프와 없는 호출 대상 검증 테스트
- 집중 Unity EditMode 테스트 25/25 통과
