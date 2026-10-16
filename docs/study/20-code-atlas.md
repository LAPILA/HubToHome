# 전체 Scripts 폴더와 코드 색인

[학습 홈](README.md)

기준일 2026-10-11. 현재 작업 트리의 `Assets/_Game/Scripts` 전부를 파일 단위로 색인했습니다. 외부 Plugins·Packages는 제외합니다. **전체 파일 색인과 모든 줄의 정밀 검토는 다릅니다.** 기능 해설은 실제 핵심 경로와 작업 기록에 근거합니다.

## 범위

- 자체 C# **686개**. 테스트로 분류한 소스 **204개** 포함.
- Scripts 루트 포함 **96개 디렉터리**, 하위 폴더 95개.
- C#이 아닌 제작 UI 파일 **4개**: USS 3개, UXML 1개. Unity .meta는 보존 대상이지만 코드 해설 목록에서 제외.
- 분류는 경로·파일 이름 기준입니다. 테스트 파일 수는 테스트 케이스 수나 통과 수가 아닙니다.

## 영역별 진입점

| 영역 | C# 수 | 학습 문서 | 전체 파일 |
| --- | --- | --- | --- |
| Core | 40 | [기능 해설](02-core-save-input.md) | [파일 색인](code/core.md) |
| Battle | 65 | [기능 해설](03-battle-turn-defense.md) | [파일 색인](code/battle.md) |
| Characters | 36 | [기능 해설](05-characters.md) | [파일 색인](code/characters.md) |
| Items | 12 | [기능 해설](06-items-shops.md) | [파일 색인](code/items.md) |
| Overworld | 127 | [기능 해설](07-overworld.md) | [파일 색인](code/overworld.md) |
| Dialogue | 13 | [기능 해설](08-dialogue.md) | [파일 색인](code/dialogue.md) |
| Scenario | 239 | [기능 해설](09-scenario-runtime.md) | [파일 색인](code/scenario.md) |
| Editor | 66 | [기능 해설](11-editor-tools.md) | [파일 색인](code/editor.md) |
| UI | 69 | [기능 해설](12-ui.md) | [파일 색인](code/ui.md) |
| Camera | 11 | [기능 해설](13-camera.md) | [파일 색인](code/camera.md) |
| VFX | 6 | [기능 해설](14-vfx-audio.md) | [파일 색인](code/vfx.md) |
| Shared | 2 | [기능 해설](15-shared.md) | [파일 색인](code/shared.md) |

## 폴더 전체 목록

직접 파일 수는 해당 폴더 바로 아래 C#만 셉니다. 하위 포함 수는 자식까지 포함하므로 행끼리 더하면 중복 집계됩니다.

| 폴더 | 직접 C# | 하위 포함 C# |
| --- | ---: | ---: |
| `(Scripts 루트)` | 0 | 686 |
| `Battle` | 0 | 65 |
| `Battle/Data` | 11 | 11 |
| `Battle/Development` | 5 | 5 |
| `Battle/Runtime` | 8 | 29 |
| `Battle/Runtime/Presentation` | 5 | 5 |
| `Battle/Runtime/Services` | 16 | 16 |
| `Battle/Tests` | 0 | 20 |
| `Battle/Tests/Editor` | 20 | 20 |
| `Camera` | 0 | 11 |
| `Camera/Data` | 1 | 1 |
| `Camera/Runtime` | 6 | 6 |
| `Camera/Tests` | 0 | 4 |
| `Camera/Tests/Editor` | 4 | 4 |
| `Characters` | 0 | 36 |
| `Characters/Data` | 10 | 10 |
| `Characters/Runtime` | 16 | 16 |
| `Characters/Tests` | 0 | 10 |
| `Characters/Tests/Editor` | 10 | 10 |
| `Core` | 0 | 40 |
| `Core/Data` | 1 | 1 |
| `Core/Debug` | 2 | 2 |
| `Core/Editor` | 2 | 2 |
| `Core/Runtime` | 20 | 20 |
| `Core/Tests` | 0 | 15 |
| `Core/Tests/Editor` | 15 | 15 |
| `Dialogue` | 0 | 13 |
| `Dialogue/Runtime` | 9 | 9 |
| `Dialogue/Tests` | 0 | 4 |
| `Dialogue/Tests/Editor` | 4 | 4 |
| `Editor` | 4 | 66 |
| `Editor/BattleLab` | 8 | 8 |
| `Editor/ContentMaker` | 5 | 17 |
| `Editor/ContentMaker/Dialogue` | 5 | 5 |
| `Editor/ContentMaker/Maps` | 4 | 4 |
| `Editor/ContentMaker/Markers` | 3 | 3 |
| `Editor/ContentValidation` | 14 | 14 |
| `Editor/SkillMaker` | 3 | 3 |
| `Editor/Tests` | 20 | 20 |
| `Items` | 0 | 12 |
| `Items/Data` | 3 | 3 |
| `Items/Runtime` | 7 | 7 |
| `Items/Tests` | 0 | 2 |
| `Items/Tests/Editor` | 2 | 2 |
| `Overworld` | 0 | 127 |
| `Overworld/AreaMarkers` | 0 | 26 |
| `Overworld/AreaMarkers/Editor` | 6 | 6 |
| `Overworld/AreaMarkers/Runtime` | 20 | 20 |
| `Overworld/Editor` | 4 | 19 |
| `Overworld/Editor/ShowcaseStation` | 5 | 5 |
| `Overworld/Editor/TravelTrain` | 9 | 9 |
| `Overworld/Editor/WideField` | 1 | 1 |
| `Overworld/Runtime` | 14 | 52 |
| `Overworld/Runtime/Cinematics` | 7 | 7 |
| `Overworld/Runtime/Hazards` | 2 | 2 |
| `Overworld/Runtime/Map` | 14 | 14 |
| `Overworld/Runtime/Puzzles` | 4 | 4 |
| `Overworld/Runtime/State` | 2 | 2 |
| `Overworld/Runtime/Travel` | 9 | 9 |
| `Overworld/Tests` | 0 | 30 |
| `Overworld/Tests/Editor` | 30 | 30 |
| `Scenario` | 0 | 239 |
| `Scenario/Data` | 26 | 26 |
| `Scenario/Editor` | 23 | 63 |
| `Scenario/Editor/Preview` | 1 | 1 |
| `Scenario/Editor/Samples` | 0 | 0 |
| `Scenario/Editor/SequenceMaker` | 12 | 39 |
| `Scenario/Editor/SequenceMaker/Diagnostics` | 1 | 1 |
| `Scenario/Editor/SequenceMaker/Documents` | 2 | 2 |
| `Scenario/Editor/SequenceMaker/Flow` | 6 | 6 |
| `Scenario/Editor/SequenceMaker/Input` | 1 | 1 |
| `Scenario/Editor/SequenceMaker/Inspector` | 4 | 4 |
| `Scenario/Editor/SequenceMaker/Library` | 4 | 4 |
| `Scenario/Editor/SequenceMaker/Playback` | 3 | 3 |
| `Scenario/Editor/SequenceMaker/Rules` | 6 | 6 |
| `Scenario/Runtime` | 25 | 77 |
| `Scenario/Runtime/Adapters` | 15 | 15 |
| `Scenario/Runtime/Battle` | 14 | 14 |
| `Scenario/Runtime/Encounter` | 2 | 2 |
| `Scenario/Runtime/Presentation` | 14 | 14 |
| `Scenario/Runtime/Preview` | 7 | 7 |
| `Scenario/Tests` | 0 | 73 |
| `Scenario/Tests/Editor` | 73 | 73 |
| `Shared` | 0 | 2 |
| `Shared/Runtime` | 0 | 1 |
| `Shared/Runtime/Text` | 1 | 1 |
| `Shared/Tests` | 0 | 1 |
| `Shared/Tests/Editor` | 1 | 1 |
| `UI` | 0 | 69 |
| `UI/Runtime` | 51 | 51 |
| `UI/Tests` | 0 | 18 |
| `UI/Tests/Editor` | 18 | 18 |
| `VFX` | 0 | 6 |
| `VFX/Runtime` | 5 | 5 |
| `VFX/Tests` | 0 | 1 |
| `VFX/Tests/Editor` | 1 | 1 |

## 제작 화면 리소스

- [ContentMakerWindow.uss](../../Assets/_Game/Scripts/Editor/ContentMaker/ContentMakerWindow.uss)
- [SkillMakerWindow.uss](../../Assets/_Game/Scripts/Editor/SkillMaker/SkillMakerWindow.uss)
- [SequenceMakerWindow.uxml](../../Assets/_Game/Scripts/Scenario/Editor/SequenceMaker/SequenceMakerWindow.uxml)
- [SequenceMakerWindow.uss](../../Assets/_Game/Scripts/Scenario/Editor/SequenceMaker/SequenceMakerWindow.uss)

UXML은 UI 구조, USS는 스타일입니다. C# 창 로직과 함께 읽되 게임 플레이 데이터로 취급하지 않습니다.

## 검색용 데이터

[code-inventory.csv](code-inventory.csv)는 686개 파일, [folder-inventory.csv](folder-inventory.csv)는 전체 폴더를 담습니다. UTF-8로 읽습니다. 이 목록에 있다는 이유로 파일을 삭제하거나 이동하지 마세요. Unity 직렬화 참조와 GUID 검사가 별도로 필요합니다.

