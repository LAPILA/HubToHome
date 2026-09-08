# 01. 이 프로젝트로 배우는 C#·알고리즘·자료구조

목표는 알고리즘 이름을 외우는 것이 아니라, 코드가 무엇을 보관하고 어떤 순서로 찾는지 설명하는 것입니다.
예시는 현재 저장소의 자체 코드를 기준으로 합니다. 아래 실습은 학습 제안이며 실행 완료 기록이 아닙니다.
처음에는 파일 전체를 외우지 말고, 안내한 메서드의 입력·반환값·반복문부터 읽으세요.

## 1. 먼저 구분할 C# 표현

- `class`: 상태와 행동을 묶습니다. `ObjectPoolManager`는 풀 목록과 대여·반납 동작을 함께 가집니다.
- `struct`: 값 형식입니다. 무조건 더 빠르거나 무조건 스택에 저장되는 것은 아닙니다.
- `interface`: 호출자가 기대하는 계약입니다. `IActionAdapter`는 구체적인 대사 UI를 알지 않습니다.
- `List<T>`의 `T`: 담을 값의 형식입니다. `List<DialogueNode>`와 `List<string>`은 같은 목록 기능을 재사용합니다.
- `private`: 그 클래스 내부에서만 접근합니다. 다른 클래스가 내부 목록을 마음대로 바꾸는 것을 줄입니다.
- `readonly List<T>`: 목록 변수의 재할당을 막습니다. `Add`, `Remove`, 내부 객체 수정까지 금지하지 않습니다.
- `out`: 성공 여부와 추가 결과를 함께 돌려줄 때 쓰입니다. `TryGetValue`가 대표적입니다.
- `Action`: 반환값 없는 함수 참조입니다. `Action<int>`는 정수 하나를 받습니다.
- `Func<A, B>`: `A`를 받아 `B`를 반환하는 함수 참조입니다. CSV 참조 해석기를 바꿔 끼울 때 사용합니다.

[ActionAdapterRegistry.cs](../../Assets/_Game/Scripts/Scenario/Runtime/ActionAdapterRegistry.cs)의 `TryGet`을 읽고,
`bool` 반환값과 `out IActionAdapter adapter`가 각각 무엇을 뜻하는지 구분해 보세요.

## 2. 자료구조를 고르는 질문

| 필요한 행동 | 이 프로젝트의 예 | 적합한 출발점 |
|---|---|---|
| 순서대로 보여주고 중간을 편집 | 대사 줄, 메이커 방 목록 | `List<T>` |
| ID로 한 항목 찾기 | 액션 ID → 실행 어댑터 | `Dictionary<TKey, TValue>` |
| 이미 들어 있는지만 확인 | 같은 객체의 풀 이중 반납 방지 | `HashSet<T>` |
| 먼저 들어온 것을 먼저 꺼내기 | 풀의 대기 객체 | `Queue<T>` |
| 마지막에 연 것을 먼저 닫기 | 겹쳐 열린 UI 패널 | `Stack<T>` |
| 부모·자식 조건을 순서대로 평가 | all/any 조건 그룹 | 트리 순회 |

자료구조의 이름보다 `순서`, `중복`, `검색 기준`, `추가·삭제 위치`를 먼저 적습니다.
같은 데이터가 여러 구조에 동시에 들어가면, 함께 갱신해야 할 규칙도 생깁니다.

## 3. List: 순서가 데이터일 때

[ContentMakerWindow.cs](../../Assets/_Game/Scripts/Editor/ContentMaker/ContentMakerWindow.cs)의 `LoadAssets`와 `FilterLists`를 봅니다.
`_allRooms`는 읽어 온 방 목록이고 `_rooms`는 현재 검색 조건에 맞는 표시 목록입니다.
검색어가 바뀔 때 전체 자산을 다시 찾지 않고, 읽어 둔 목록에서 표시 대상을 고릅니다.

목록 인덱스는 현재 위치일 뿐 영구 ID가 아닙니다.
대사 3번을 앞으로 옮기면 인덱스가 달라지지만, 화자나 연결 대상의 정체성까지 바뀌어서는 안 됩니다.

- `list[i]`: 유효한 인덱스의 항목을 직접 읽습니다.
- `Add`: 끝에 넣습니다. 여유 공간이 부족하면 내부 배열을 키우고 복사할 수 있습니다.
- `Insert(0, x)`: 기존 항목들을 뒤로 이동시킵니다.
- `RemoveAt(i)`: 뒤의 항목들을 앞으로 당깁니다.
- `Contains(x)`: 일치 항목이 나올 때까지 차례로 비교합니다.

연습: `[A, B, C]`에서 `RemoveAt(1)` 후 `Insert(0, D)`의 결과를 종이에 써 보세요.
목록 10개와 10만 개에서 같은 중간 삽입이 갖는 비용 차이도 설명해 보세요.

## 4. Dictionary: 이름표로 직접 찾기

[ActionAdapterRegistry.cs](../../Assets/_Game/Scripts/Scenario/Runtime/ActionAdapterRegistry.cs)의 `_adapters`를 봅니다.
키는 `dialogue.wait` 같은 문자열이고 값은 실제 `IActionAdapter` 객체입니다.
실행기는 모든 어댑터를 순서대로 물어보는 대신 키로 조회합니다.

```csharp
// 학습용 축약: 실제 등록·실행 코드는 원본을 확인합니다.
if (registry.TryGet(actionId, out IActionAdapter adapter))
{
    // 이 ID를 처리할 구현을 찾았습니다.
}
else
{
    // 오타 또는 등록 누락입니다. 성공한 척 진행하면 안 됩니다.
}
```

여기서 `Register`는 같은 키가 있으면 값을 덮어씁니다.
따라서 “Dictionary를 썼으니 중복 등록도 자동 검증한다”는 설명은 틀립니다.
덮어쓰기 허용 여부는 별도의 서비스 정책입니다.
문자열 키의 대소문자·앞뒤 공백 처리도 계약에 포함됩니다.

## 5. Queue와 HashSet: 같은 풀에 둘 다 필요한 이유

[ObjectPoolManager.cs](../../Assets/_Game/Scripts/Core/Runtime/ObjectPoolManager.cs)의 `PoolState`, `Spawn`, `Despawn`을 읽습니다.

- `Available`: 다음 대여 후보를 꺼낼 `Queue<GameObject>`입니다.
- `InPool`: 이미 반납된 객체인지 확인할 `HashSet<GameObject>`입니다.
- `_instanceOwners`: 어떤 풀이 만든 객체인지 찾는 Dictionary입니다.

한 객체가 두 번 반납되면 Queue에 같은 객체가 두 번 들어갈 수 있습니다.
그 상태에서 두 번 대여하면 서로 다른 효과가 같은 객체를 동시에 쓰는 문제가 생깁니다.
`Despawn`은 `InPool.Contains`로 중복 반납을 먼저 막습니다.
`Spawn`은 후보를 꺼낸 뒤 집합에서 제거하고, 파괴되었거나 소유자가 다른 후보를 건너뜁니다.

주의: 반납 객체 수 제한은 `maxRetainedPerPool`입니다. 동시에 활성화할 수 있는 객체 수 제한과 다릅니다.
큐가 비면 `CreateNew`가 호출되므로, 풀을 쓴다고 생성 비용이 언제나 사라지지는 않습니다.
학습 질문: Queue만 남기거나 HashSet만 남기면 어떤 정책을 직접 보완해야 할까요?

## 6. Stack: 마지막에 연 창부터 닫기

[UIManager.cs](../../Assets/_Game/Scripts/Core/Runtime/UIManager.cs)의 `OpenPanel`, `CloseTopPanel`을 봅니다.
`메뉴 → 아이템 → 확인창`으로 열었으면 일반적인 닫기 순서는 그 반대입니다.
각 `PanelStackEntry`에는 패널뿐 아니라 이전 선택 UI도 함께 담습니다.
따라서 패널을 닫는 일에는 입력 포커스 복원도 포함됩니다.

`Stack`은 중간 항목 제거에 특화된 구조가 아닙니다.
이 파일의 `RemovePanelFromStack`, `RebuildStackFromBuffer`는 중간 삭제 시 추가 작업이 필요함을 보여줍니다.
“주요 사용 순서는 LIFO지만 예외적으로 중간 제거도 지원한다”가 정확한 설명입니다.

## 7. 선형 검색이 정렬보다 나은 실제 사례

[FlagDialogueSelector.cs](../../Assets/_Game/Scripts/Overworld/Runtime/State/FlagDialogueSelector.cs)의 `Resolve`를 봅니다.
조건에 맞는 규칙 중 우선순위가 가장 높은 하나만 필요합니다.
이 메서드는 전체 목록을 정렬하지 않고, 순회하면서 현재 최선의 규칙을 바꿉니다.
같은 우선순위면 먼저 나온 규칙을 유지합니다.

규칙 수를 `n`, 조건 검사 하나의 비용을 `c`라 하면 선택 비용은 대략 `O(n × c)`입니다.
정렬해서 첫 항목을 고르는 방법은 비교 비용을 더 지불하고 원래 순서도 건드릴 수 있습니다.
항목이 적으면 Dictionary를 새로 만들기보다 목록 몇 개를 비교하는 편이 단순하고 빠를 수도 있습니다.
다만 실제 시간 차이는 호출 빈도·문자열 길이·캐시 상태를 측정해야 알 수 있습니다.

## 8. 조건 트리와 대화 그래프는 다릅니다

[TriggerConditionRegistry.cs](../../Assets/_Game/Scripts/Scenario/Runtime/TriggerConditionRegistry.cs)의 `TryEvaluateGroup`을 봅니다.
all 그룹은 하나라도 거짓이면 중단하고, any 그룹은 하나라도 참이면 중단합니다.
이를 단락 평가라고 합니다. 뒤의 조건을 모두 읽어야 하는 것은 아닙니다.
현재 빈 all 그룹은 참, 빈 any 그룹은 거짓이 됩니다. 이 경계값도 데이터 계약입니다.

대화는 선택지가 앞선 대화로 되돌아갈 수 있어 단순한 트리가 아니라 그래프가 될 수 있습니다.
[ContentMakerDialogueCsv.cs](../../Assets/_Game/Scripts/Editor/ContentMaker/Dialogue/ContentMakerDialogueCsv.cs)의 `HasCycle`, `Visit`을 봅니다.

- `visiting`: 현재 재귀 경로에 있는 대화입니다. 다시 만나면 순환입니다.
- `complete`: 이미 끝까지 검사한 대화입니다. 합류 지점을 다시 깊게 검사하지 않습니다.
- 방문 횟수·깊이 제한: 잘못된 데이터가 편집기를 오래 붙잡는 것을 줄입니다.

이 검사는 순환과 검사 한도 초과를 함께 경고합니다. 경고가 곧 무조건 오류라는 뜻은 아닙니다.
두 갈래가 같은 대화로 합류하는 모양과, 자기 자신으로 돌아오는 모양을 각각 그려 비교해 보세요.

## 9. CSV 파싱은 Split이 아니라 상태 해석입니다

같은 CSV 파일의 `ReadRows`에서 `quoted`, `closed`, `recordStarted`를 찾습니다.
`"안녕, 위젤"`의 쉼표는 열 구분자가 아니며, 따옴표 안 줄바꿈도 다음 레코드가 아닙니다.
셀 안의 따옴표 하나는 CSV에서 `""`로 표현됩니다.
`Split(',')`와 `Split('\n')`만으로 읽으면 이 구분을 잃습니다.

`ReadRows`는 문자를 순서대로 읽고 현재 상태에 따라 같은 쉼표를 다르게 처리합니다.
본문 조립에는 `StringBuilder`를 사용합니다. 매 글자마다 새 문자열 전체를 복사하는 일을 줄입니다.
그다음 `Parse`가 헤더·버전·행 순서·참조·enum·bool을 검사하여 임시 결과를 만듭니다.
형식 검사와 실제 자산 반영을 분리하는 점이 파서 구현만큼 중요합니다.

## 10. Big-O를 말할 때 붙여야 하는 조건

| 연산 | 기본 비용 설명 | 빠뜨리면 안 되는 조건 |
|---|---|---|
| List 인덱스 조회 | `O(1)` | 범위 안의 인덱스 |
| List 끝 추가 | 분할상환 `O(1)` | 확장되는 한 번은 `O(n)` |
| List 검색·중간 삭제 | `O(n)` | 비교 자체 비용은 별도 |
| Dictionary·HashSet 조회 | 평균 `O(1)`, 최악 `O(n)` | 해시 분포·충돌·키 비교 비용 |
| Queue·Stack 넣기 | 분할상환 `O(1)` | 내부 저장 공간 확장 가능 |
| 비교 정렬 | 보통 `O(n log n)` 규모 | 비교기 비용·정렬 계약 별도 |
| 트리 전체 순회 | `O(v)` | 노드당 검사 비용과 깊이 고려 |
| 방문 집합이 있는 그래프 순회 | 평균 `O(v + e)` | 정점·간선 수, 해시 연산 가정 |
| CSV 문자 해석 | 입력 길이 `m`에 대체로 `O(m)` | 자산 조회·검증 비용은 별도 |

문자열 해시는 문자열 길이에 영향을 받습니다. 키가 매우 길면 “조회 O(1)”만으로 시간을 설명할 수 없습니다.
`AssetDatabase.LoadAssetAtPath`, 물리 검사, Instantiate 같은 엔진 호출은 단순 배열 접근이 아닙니다.
풀의 자료구조 연산이 빠르다는 사실과 `Spawn` 전체 실행 시간이 일정하다는 주장은 다릅니다.

## 11. 이벤트는 자료 저장소가 아닙니다

[EventManager.cs](../../Assets/_Game/Scripts/Core/Runtime/EventManager.cs)의 `Subscribe`, `Trigger`, `Unsubscribe`를 읽습니다.
이벤트는 지금 구독 중인 함수에 알림을 보냅니다. 과거 이벤트를 나중에 구독자가 자동으로 받지는 않습니다.
이미 완료한 사건은 저장 상태가 기억하고, 이벤트는 그 변화에 반응하는 계기로 쓰는 식으로 나눕니다.
구독 해제를 빼먹으면 수명이 끝난 객체가 호출되거나 같은 반응이 중복될 수 있습니다.

## 12. 직접 해 볼 작은 과제

1. `FlagDialogueSelector.Resolve`에 규칙 세 개를 가정하고, 동점·불일치·전부 불일치 결과를 손으로 추적합니다.
2. 풀 객체 A를 대여·반납·중복 반납할 때 Queue와 HashSet 내용을 표로 적습니다.
3. CSV 한 셀에 쉼표·줄바꿈·따옴표를 각각 넣고 `ReadRows`의 상태 변화를 설명합니다.
4. 대화 그래프 `A → B → D`, `A → C → D`가 왜 순환이 아닌지 `visiting`과 `complete`로 설명합니다.
5. 이 중 하나를 별도 연습 코드로 구현한 뒤 예상값을 확인합니다. 본편 자산을 바꾸는 과제는 아닙니다.

통과 기준: “Dictionary라 빠릅니다” 대신, 어떤 키를 얼마나 자주 찾고 어떤 실패를 처리하는지 말할 수 있습니다.
다음 읽기: [에디터 도구와 자산](05-editor-tools-and-assets.md), [설계·성능·검증](06-design-patterns-performance-testing.md).
