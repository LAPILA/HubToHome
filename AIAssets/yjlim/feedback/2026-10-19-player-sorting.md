# 플레이어 Order3 고정 · 상자 앞뒤 가림

## 원인과 최종 방향

처음 증상은 플레이어가 상자보다 항상 앞에 보이는 것이었습니다. 양쪽은 Characters 레이어였지만 PlayerController는 `base - Round(y*100)`으로 Order를 매번 바꾸고 상자는2로 고정되어 있었습니다. 현재 Q0 상자는 사용자 편집으로 Y=-1.5입니다. 플레이어가 상자보다 위인 Y=-1이어도 order101>상자2이므로 앞에 표시되는 설정을 확인했습니다.

이후 사용자가 **플레이어 Order3 고정**을 명시했습니다. 따라서 상자에도 큰 Y기반Order를 붙이는 안은 폐기하고 기본 Unity 깊이 정렬로 정리했습니다.

- 플레이어: 기본/탐색 Order3, SpriteSortPoint.Pivot. Awake와 탐색 갱신에서55 같은 Y기반 값으로 바꾸지 않습니다.
- 상자: 루트에 SortingGroup(Characters/order3). 그룹 루트가 바닥 기준이며 Visual의 사용자 내부 Order와 위치 Y0.75는 보존합니다.
- 실제 2D Renderer: CustomAxis(0,1,0). 같은 레이어·같은 Order 내에서 아래쪽이 앞으로 정렬됩니다.
- 전투: 기존 임시 foreground boost5000은 별도 연출이라 유지하며 종료 시 기본3으로 복원합니다. 전투 중까지3만 허용하면 기존 배경 앞 표시 계약이 깨지므로 이 처리는 제거하지 않았습니다.

## 수정 위치

| 파일 | 변경 |
|---|---|
| `Assets/_Game/Scripts/Overworld/Runtime/PlayerController.cs` | 기본3/Pivot/탐색Y계산제거,전투boost유지 |
| `Assets/_Game/Content/Characters/Prefabs/Player/Player_Base.prefab` | SpriteRenderer Order3/Pivot |
| `Assets/_Game/Content/Maps/Regions/ALL/Prefabs/Props/Prop_Chest_TrainSnack.prefab` | 바닥root SortingGroup3 추가 |
| `Assets/_Game/Content/Maps/Development/BunnySlimeBattleLab/Presentation/BunnySlimeLab_Renderer2D.asset` | Default0→CustomAxis3,기존Y축유지 |

위 Renderer2D는 RP의 slot1이며 열차와 실험실이 사용합니다. 3D PC_Renderer, RPAsset 목록, 카메라, UI, 배경/타일, 기존 적의 Order 코드는 변경하지 않았습니다. 상자의 사용자 수정 콜라이더/Room 위치/아트는 보존합니다. 이번 진단 중 새로 만들던 미완료 WorldPropYSort 스크립트·테스트/meta4개는 사용자 요청 변경에 맞춰 제거했습니다. 사용자 파일을 삭제한 것이 아닙니다.

## 소품 제작 규칙

다른 소품도 플레이어와 깊이를 바꾸려면 Characters/Order3을 맞추고 바닥 위치의 루트에 SortingGroup을 사용합니다. 오더가 다르면 Y보다 오더가 우선합니다. 그룹 내 자식 Order는 부분 그림 간 순서이며 외부 우선순위와 다릅니다. 기존 샘플의 ApplyStaticYSort(Y×100베이크)와 새 동일 Order 규칙을 중복 사용하지 않습니다.

## 검증 범위

실제 화면 재현 대신 직렬화 Order와 실제 Player 계산으로 앞/뒤 모두 앞이 되는 설정 신호를 확인했습니다. Unity Play/Refresh/열린 씬 저장/엔진 NUnit은 실행하지 않습니다. CLI 컴파일/테스트 소스/자산 계약 최종 결과는 [당일 기록](../../2026-10-19-update.md)을 따릅니다. 실제 앞뒤 전환·카메라 연출 중 가림은 Unity 수동 확인 대상입니다.

최종 CLI는 Runtime413/Editor351소스 오류0입니다. PlayerSortingOrderTests5사례는 Awake3/Pivot, 다른Y좌표3개에서3고정, 전투5003/3복원을 포함하며 엔진 실행은 하지 않았습니다. 정적 고정3/상자그룹3/Renderer CustomAxisY 계약도 확인했습니다.

URP패키지 `Runtime/2D/Passes/Utility/RendererLighting.cs:GetTransparencySortingMode`는 Default일 때 카메라가 Orthographic인지로 모드를 결정합니다. ProjectSettings CustomAxis만 설정해도 해당 Renderer2D에는 적용되지 않으므로 로컬 RendererData에 축을 명시했습니다. 패키지 원본은 수정하지 않았습니다.

## 하네스 교훈 · 2026-10-19

Windows에서 `rg Library/PackageCache/com.unity.render-pipelines.universal*`처럼 입력 경로에 wildcard를 넣은 최초 조회는 오류123으로 실패했습니다. `rg Library/PackageCache -g '*.cs'`로 파일을 찾은 후 확인된 정확한 디렉터리를 사용하니 조회가 정상 동작했습니다. 동일 문제 기록이 있어도 반복될 수 있으므로 도구 호출 전 입력 경로에 wildcard가 없는지 확인합니다. 증상 검증은 관련 패키지 실제 함수 위치를 직접 읽고 RendererData 값과 대조하는 방식입니다.

[상자 제작](2026-10-19-item-chest.md) · [제작 가이드](../../../docs/content-maker-guide.md#상자와-플레이어-앞뒤-가림)
