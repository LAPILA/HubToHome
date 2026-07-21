# HUB TO HOME 맵·씬 폴더 정리 설계

## 목표

- 기획자와 맵 제작자가 `Assets/_Game/Content/Maps` 한 곳에서 플레이 가능한 지역을 찾는다.
- 지역 씬, 해당 지역 전용 Prefab, 재질, 제작 메모를 하나의 지역 패키지로 묶는다.
- 타이틀과 전투처럼 맵이 아닌 실행 흐름용 씬은 `Assets/_Game/Scenes`에 남긴다.
- Unity `.meta` GUID와 기존 씬 이름을 보존해 Serialized Reference와 런타임 전환을 깨뜨리지 않는다.
- 외부 에셋과 아트 원본은 이동하지 않는다.

## 최종 구조

```text
Assets/_Game/
├─ Scenes/
│  ├─ Frontend/
│  │  ├─ 00_TitleScene.unity
│  │  └─ 01_IntroScene.unity
│  └─ Battle/
│     └─ BattleScene.unity
└─ Content/Maps/
   ├─ README_MapAuthoring.md
   ├─ Shared/
   │  ├─ Generated/
   │  ├─ Markers/
   │  ├─ Sprites/
   │  └─ Tilemaps/
   ├─ Development/
   │  └─ TestMap/
   │     ├─ TestMap.unity
   │     ├─ Prefabs/
   │     └─ README_TestMap_QA.md
   └─ Regions/
      ├─ PrologueSubway/
      │  └─ Scenes/
      │     └─ OverworldScene.unity
      └─ MapFieldStarter/
         ├─ Scenes/
         ├─ Prefabs/
         ├─ Materials/
         └─ Notes/
```

## 분류 규칙

- `Scenes`: 타이틀, 로딩, 전용 전투 등 지역 콘텐츠가 아닌 애플리케이션 흐름용 씬.
- `Maps/Regions`: 출시 게임에서 플레이하는 지역. 지역 폴더 안에 씬과 지역 전용 자산을 함께 둔다.
- `Maps/Development`: QA, 기능 검증, 스프라이트 크기 비교용 맵. 출시 포함 여부를 별도로 관리한다.
- `Maps/Shared`: 두 지역 이상에서 재사용하는 맵 마커, 타일, 스프라이트, 생성 보조 리소스.
- 아트 원본, 캐릭터, 스킬, 대화, 시나리오 데이터는 기존 도메인 폴더에 유지한다.

## 이동 원칙

1. 에셋과 같은 이름의 `.meta` 파일을 반드시 함께 이동한다.
2. `.unity` 파일명과 GUID는 유지한다.
3. Build Settings, Editor 도구의 상수 경로, 테스트, Markdown 가이드를 새 경로로 갱신한다.
4. 씬 이름 기반 런타임 값(`BattleScene`, `OverworldScene`, `TestMap`)은 이번 작업에서 변경하지 않는다.
5. 현재 작업 트리의 수정 내용은 보존하며 폴더 정리와 무관한 파일은 건드리지 않는다.

## 검증

- 이전 경로를 가리키는 텍스트 참조가 남지 않았는지 `rg`로 검사한다.
- 모든 씬 GUID가 이동 전과 동일한지 확인한다.
- Unity 배치 모드 컴파일과 관련 EditMode 테스트를 실행한다.
- Build Settings의 활성 씬이 모두 실제 파일을 가리키는지 검사한다.
- 빈 이전 폴더와 고아 `.meta`가 남지 않았는지 확인한다.

## 의도적으로 제외하는 범위

- 픽셀 아트 파일 자체의 재분류나 수정
- 씬 내용, 연출, 게임플레이 로직 변경
- 씬 파일명 변경과 기존 저장 데이터 마이그레이션
- 외부 패키지 및 Asset Store 에셋 폴더 정리
