# 07 · 제작 도구의 Undo와 안전한 저장

> **중심 질문: 편집한 만큼 되돌리고, 다른 사람이 고친 원본을 덮지 않을 수 있는가?** 툴의 가치는 버튼 수보다 제작 데이터를 안전하게 다루는 데 있습니다.

[자료실](README.md) · [Sequence Maker 입문](https://app.notion.com/p/3f6641f619018147803ccdf5d8b1dc56) · [에디터 도구 입문](https://app.notion.com/p/3f6641f6190181a9a464f4f91ad7ed0c)

## 01 / 창과 편집 모델을 나눈다

현재 공식 SequenceMakerWindow는 `EditorWindow` 기반이며 UI Toolkit 제작 경로를 사용합니다. Odin을 쓴 다른 Inspector/도구가 있다고 이 창 전체를 Odin 창으로 설명하지 않습니다. UI 기술과 데이터 편집 규칙은 별개입니다.

| 구성 | 책임 |
| --- | --- |
| SequenceMakerWindow | 목록·선택·명령 연결 |
| SequenceEditCommandStack | Execute/Undo/Redo, 선택 복구, 저장 상태 |
| ISequenceEditCommand | 특정 편집의 실행과 역연산 |
| SequenceSaveCoordinator | 내보내기·원문 충돌·임시 검증·교체 |
| ISequenceSaveTarget | 문서별 export/round-trip/메타데이터 연결 |

## 02 / Undo는 숫자 하나 감소시키는 일이 아니다

근거 · `Assets/_Game/Scripts/Scenario/Editor/SequenceMaker/SequenceEditCommandStack.cs` · 175–187행

```csharp
        HistoryEntry entry = _undo[_undo.Count - 1];
        _undo.RemoveAt(_undo.Count - 1);
        for (int i = entry.Commands.Count - 1; i >= 0; i--)
        {
            entry.Commands[i].Undo(_sequence);
        }

        RestoreSelection(entry.BeforeSelection);
        _currentStateId = entry.BeforeStateId;
        _redo.Add(entry);
        EditorUtility.SetDirty(_sequence);
        Raise(SequenceEditChangeReason.Undo, entry.Name);
        return true;
```
마지막 HistoryEntry를 꺼내고 그 안 명령을 **역순으로** 되돌립니다. 삽입→이동이라는 복합 작업은 이동 취소→삽입 취소 순서여야 원래 위치로 돌아갑니다. 선택 상태도 함께 복구해 사용자가 보던 블록을 잃지 않게 합니다.

Undo/Redo는 `List<HistoryEntry>`의 끝을 스택처럼 사용합니다. 기본 끝 추가/제거는 상각 O(1)이지만, 명령 자체가 블록 트리를 찾거나 복제하는 비용은 별도입니다. “Undo 전체가 O(1)”이라고 쓰면 틀립니다.

근거 · `Assets/_Game/Scripts/Scenario/Editor/SequenceMaker/SequenceEditCommandStack.cs` · 136–147행

```csharp
            long beforeStateId = _currentStateId;
            long afterStateId = ++_nextStateId;
            _undo.Add(new HistoryEntry(
                command.Name,
                new[] { command },
                beforeSelection,
                afterSelection,
                beforeStateId,
                afterStateId));
            _redo.Clear();
            _currentStateId = afterStateId;
            Raise(SequenceEditChangeReason.Execute, command.Name);
```
새 편집이 들어오면 Redo를 비웁니다. Undo 후 다른 내용을 쓰면 이전 미래 경로로 다시 가는 것이 아니라 새로운 편집 갈래가 생겼기 때문입니다. 블록은 목록 인덱스만이 아니라 안정적인 Block ID로 식별합니다. 순서를 옮겨도 같은 블록을 가리키려는 목적입니다.

## 03 / ‘저장됨’은 Undo 개수와 같지 않다

근거 · `Assets/_Game/Scripts/Scenario/Editor/SequenceMaker/SequenceEditCommandStack.cs` · 77–80행

```csharp
    public bool CanUndo => _undo.Count > 0;
    public bool CanRedo => _redo.Count > 0;
    public bool IsDirty => _currentStateId != _savedStateId
        || (_transaction != null && _transaction.Commands.Count > 0);
```
`_currentStateId`와 `_savedStateId`를 비교합니다. Undo가 하나 있다고 미저장인 것이 아닙니다. 저장 시점으로 돌아왔으면 저장된 상태입니다.

| 행동 | 현재 ID | 저장 ID | Dirty |
| --- | --- | --- | --- |
| 시작 | 0 | 0 | false |
| 편집 A | 1 | 0 | true |
| 저장 | 1 | 1 | false |
| 편집 B | 2 | 1 | true |
| Undo | 1 | 1 | false |
| 다시 Undo | 0 | 1 | true |

열린 트랜잭션 안에 명령이 있으면 ID가 아직 확정되지 않아도 Dirty입니다. 여러 세부 편집을 사용자의 한 번 행동으로 묶는 것이 트랜잭션이며, 실패 시 역연산으로 되돌리는 경로도 있습니다. 아무 외부 부작용이나 완전 복구하는 데이터베이스 트랜잭션은 아닙니다.

## 04 / YAML 원본이 밖에서 바뀌었다면?

```mermaid
flowchart LR
 A["Save"] --> B["Export / 검증"]
 B --> C["원본 hash 충돌 확인"]
 C --> D["임시 파일 쓰고 다시 읽기"]
 D --> E["ValidateRoundTrip"]
 E --> F["직전 hash 재검사"]
 F --> G["Replace / Move"]
 G --> H["SourceHash 메타데이터 갱신"]
```

근거 · `Assets/_Game/Scripts/Scenario/Editor/SequenceMaker/SequenceSaveCoordinator.cs` · 176–191행

```csharp
            destinationExisted = _fileSystem.FileExists(result.SourcePath);
            if (destinationExisted)
            {
                string currentText = _fileSystem.ReadAllText(result.SourcePath);
                observedDiskHash = ScenarioSourceHash.Compute(currentText);
                result.Conflict = SequenceSourceConflict.Detect(
                    result.SourcePath,
                    target.StoredSourceHash,
                    currentText);
                if (result.Conflict != null && !options.OverwriteExternalChanges)
                {
                    return Fail(
                        result,
                        SequenceSaveStatus.Conflict,
                        result.Conflict.Message);
                }
```
처음 읽었던 hash와 현재 디스크 내용을 비교해 외부 수정을 감지합니다. 이를 낙관적 동시성 검사라고 부를 수 있습니다. 파일을 편집 내내 잠그는 대신 저장 직전에 충돌을 확인하는 선택입니다.

임시 파일을 쓰고 문자열이 일치하는지 확인한 다음, `ValidateRoundTrip()`으로 다시 읽을 수 있는지 검사합니다. 저장 직전에도 한 번 더 원본 hash를 확인합니다. 처음 확인하고 오래 작업한 사이 원본이 또 바뀌는 경우를 줄이기 위한 것입니다. 완전한 OS 수준 비교-교체 잠금은 아니므로 마지막 검사 이후의 외부 변경 가능성까지 0이라고 주장하지 않습니다.

주의: 현재 round-trip은 재가져오기와 유효성 검사를 수행하지만 모든 필드의 구조적 동등성을 비교하지 않습니다. 파서는 알 수 없는 최상위 키를 건너뛸 수 있습니다. 따라서 ‘다시 읽기 성공’과 ‘원본의 의미가 전부 보존됨’은 다른 검증입니다.

## 05 / ‘파서가 없다’는 클래스명만 보면 안 된다

`ScenarioSourceParser.cs`에는 주입 누락 시 명확한 오류를 반환하는 `MissingYamlScenarioSourceParser`가 있습니다. 그러나 실제 `ScenarioSourceYamlParser`는 `ScenarioSourceExporter.cs`에 구현되어 있고 SourceSync·Reimport·SaveTarget 경로에서 사용됩니다.

근거 · `Assets/_Game/Scripts/Scenario/Editor/ScenarioSourceExporter.cs` · 1131–1150행

```csharp
public sealed class ScenarioSourceYamlParser : IScenarioSourceParser
{
    private const int TopLevelIndent = 0;
    private const int SectionIndent = 2;
    private const int NestedIndent = 4;

    public ScenarioSourceParseResult Parse(string sourceText, string sourcePath)
    {
        var result = new ScenarioSourceParseResult();
        if (string.IsNullOrWhiteSpace(sourceText))
        {
            result.Validation.AddError(
                "scenario.yaml.source.required",
                "Scenario YAML source text is required.",
                sourcePath);
            return result;
        }

        List<YamlLine> lines = ReadLines(sourceText);
        var document = new ScenarioSourceDocument();
```
이 파서는 프로젝트 제작 형식에 맞춘 구현입니다. 범용 YAML 전체 문법이나 YamlDotNet 사용을 클래스 이름만 보고 보장할 수 없습니다. **interface 정의 → 실제 생성자 주입 → 호출 경로**를 따라가야 구현 여부를 정확히 판단할 수 있습니다.

## 06 / 실습과 예상 결과 — 이번에는 미실행

- [ ] 위 Dirty 표를 `SequenceEditCommandStackTests.cs`의 실제 상태 변화와 대조한다.
- [ ] 블록 여러 개 이동을 Undo/Redo한 뒤 순서·ID·선택이 돌아오는지 확인한다.
- [ ] 편집창을 열어 둔 상태에서 임시 복사본의 원문을 외부 수정한다. 예상: 무조건 덮기보다 충돌 결과.
- [ ] 미지원 필드를 추가해 오류 또는 누락 여부를 확인한다. 모든 필드의 보존까지 보장되는지는 별도로 검증한다.

읽을 테스트: `Scenario/Tests/Editor/SequenceEditCommandStackTests.cs`, `SequenceMakerCommandPropertyTests.cs`, `SequenceSaveCoordinatorTests.cs`.

면접 질문: **Unity Undo만 쓰지 않고 별도 명령 모델을 둔 이유는? 저장 상태를 bool 하나로 관리하면 Undo 뒤 어떤 문제가 생기나? hash 검사는 잠금과 어떻게 다른가?**
