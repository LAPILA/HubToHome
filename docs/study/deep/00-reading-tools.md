# 00 · 코드를 읽기 위한 최소 도구

> **문법을 전부 외우기보다, 값이 누구에게 있고 언제 살아 있는지부터 봅니다.** 아래 개념은 뒤의 실제 코드에서 반복됩니다.

[자료실로 돌아가기](README.md)

## 01 / 파일을 열면 먼저 보는 네 가지

| 질문 | 찾을 표현 | 이 프로젝트에서의 예 |
| --- | --- | --- |
| 누가 호출하는가? | public 메서드, 이벤트 구독, Update | `TrySaveProgress`, `AdvanceTurn` |
| 누가 값을 갖는가? | 필드, private set, 읽기 전용 interface | `SkillContext`, `CharacterStats` |
| 언제 다음 프레임으로 넘기는가? | `yield return`, 콜백, DOTween | `BattleSkillTimelineRunner` |
| 끝나면 무엇을 되돌리는가? | `finally`, `Dispose`, OnDisable | 대사 상태·카메라 토큰·트윈 |

모든 줄을 위에서 아래로 읽기 전에 이 네 질문을 메모하면 긴 파일에서도 중심을 놓치지 않습니다.

## 02 / 값 복사와 객체 참조를 구분하기

`class` 변수는 보통 객체를 가리키는 참조를 복사합니다. 리스트를 새로 만들었다고 안의 모든 캐릭터 객체까지 복제되는 것은 아닙니다. 반대로 아래 `Entry`는 `struct`이므로 리스트에서 꺼내면 값이 복사됩니다.

근거 · `Assets/_Game/Scripts/Battle/Runtime/Services/BattleSpeedTurnScheduler.cs` · 45–50행

```csharp
            else
            {
                Entry entry = _entries[index];
                entry.Speed = speed;
                _entries[index] = entry;
            }
```
`entry.Speed`만 고치고 마지막 대입을 빼면 리스트 안 값은 바뀌지 않습니다. 이 때문에 `entries[i] = entry`가 반복됩니다. `Actor` 필드가 참조라는 사실은 그대로이므로, 복사된 Entry 안의 Actor를 통해 HP를 바꾸면 실제 캐릭터가 바뀔 수 있습니다. 예측 코드가 배우는 것은 **얕은 복사와 깊은 복사의 차이**입니다.

## 03 / interface와 generic은 무엇을 분리하는가?

`BattleSpeedTurnScheduler<T>`는 `PlayerCharacter`를 몰라도 다음 턴을 계산합니다. `T`는 호출자가 넣는 배우의 타입이고 `Func<T, int>`는 “이 배우의 속도를 읽는 함수”입니다. `IReadOnlyList<T>`는 이 메서드가 목록을 읽는 계약을 나타냅니다. 내부 객체까지 불변으로 만드는 장치는 아닙니다.

`ISaveFileSystem`은 실제 디스크 대신 실패를 일으키는 가짜 파일 시스템을 테스트에 넣는 접점입니다. interface가 있다고 자동으로 좋은 설계는 아닙니다. **실제로 교체하거나 테스트할 경계**가 있을 때 의미가 있습니다.

## 04 / 코루틴은 별도 스레드가 아니다

근거 · `Assets/_Game/Scripts/Scenario/Runtime/Presentation/BattleSkillTimelineRunner.cs` · 126–135행

```csharp
                        if (!moved)
                            break;

                        yield return routine.Current;
                    }
                }
                finally
                {
                    (routine as IDisposable)?.Dispose();
                }
```
`MoveNext()`는 다음 중단 지점까지 실행합니다. `Current`는 이번에 Unity에 넘길 대기 대상입니다. `yield return null`은 보통 다음 프레임까지 양보합니다. 여러 코루틴이 동시에 보이더라도 이 예는 주로 메인 스레드에서 조금씩 번갈아 진행합니다.

위의 `Dispose()`는 블록 안의 정리 구문이 실행될 수 있도록 수명을 마감합니다. 단, 프로젝트 전체의 아무 코루틴이나 강제로 멈춰도 모든 복구가 완벽히 된다는 보장은 아닙니다. **어느 실행자가 Dispose와 취소를 책임지는지**까지 따라가야 합니다.

## 05 / Unity의 ‘이미 파괴됐지만 참조는 남은’ 객체

`UnityEngine.Object` 계열은 엔진 객체가 파괴되어도 관리 객체 참조가 잠시 남을 수 있습니다. 따라서 `ReferenceEquals(obj, null)`과 `obj == null`이 다를 수 있습니다. 캐릭터 컴포넌트에 접근하기 전에는 Unity null 판정을 확인하고, Dictionary에서 옛 키를 제거할 때는 남은 관리 참조가 필요한 경우도 있습니다.

`?.`만 붙이면 파괴된 Unity 객체가 모두 안전해진다고 외우지 마세요. 참조 자체가 null인지 검사하는 문법과 Unity가 오버로드한 동등 비교는 같은 검사가 아닙니다.

## 06 / 이벤트, finally, 세대 번호

| 장치 | 해결하는 문제 | 해결하지 않는 문제 |
| --- | --- | --- |
| `event` 구독/해제 | 상태 변경을 여러 표시자에게 알림 | 중복 구독을 자동으로 막지 않음 |
| `finally` | 해당 실행 경로의 정리를 한곳에 모음 | 프로세스 강제 종료까지 복구하지 않음 |
| 세대 번호 | 오래된 콜백이 새 작업을 건드리는 것을 차단 | 객체 자체의 존재 여부를 대신하지 않음 |
| DOTween 핸들 | 내가 만든 움직임을 개별 중단 | 게임 상태의 취소 규칙을 대신하지 않음 |
| Odin 속성 | Inspector 편집·검사·가독성 개선 | 런타임 데이터 검증을 대신하지 않음 |

## 07 / 내 것으로 만드는 연습

- [ ] `Entry`가 class였다면 마지막 대입이 왜 불필요해질 수 있는지 설명한다.
- [ ] 예측 목록을 복사한 뒤 HP를 건드리면 왜 실제 전투가 바뀔 수 있는지 설명한다.
- [ ] 코루틴 종료와 전투 종료의 책임자가 같은지, 실제 코드에서 찾는다.
- [ ] `finally`에 카메라 초기화를 넣기만 하면 새 연출을 깨뜨릴 수 있는 이유를 예상한다.

이 장은 개념 해설입니다. 위 항목은 직접 실행한 테스트 결과가 아닙니다. 다음은 [저장과 설정](01-save-settings.md)입니다.
