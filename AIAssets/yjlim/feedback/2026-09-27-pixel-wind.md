# 픽셀 풀 바람 — UV 변형으로 재구현

브랜치 `codex/gameplayEdit`. 커밋/푸시 없음. **이전 PixelWind/밝기 파동/레이어 전체 이동 구현은 폐기했다. 현재 구현은 PixelFoliage다.**

## 적용 완료 위치

### 최종 정리: 7종 시작값 / 위쪽 고정

- PixelFoliageWind Inspector에 잔디 3종, 꽃/세워진 풀, 덤불/나뭇잎, 매달린 천/덩굴, 몽환 흔들림을 추가. 드롭다운 선택 후 적용 버튼을 누를 때만 기존 수치를 변경한다. 자동 로딩/OnEnable에서 프리셋을 재적용하지 않는다.
- 엇박자 버튼은 해당 컴포넌트의 위상만 바꾸며 Unity 전역 Random 상태를 사용하지 않는다. Editor Undo/Redo와 prefab override 기록을 지원한다.
- _pinFromTop / _PinFromTop 기본값 false/0. 기존 _rootPin과 저장 필드는 보존. 공통 HLSL의 높이 가중치를 선택적으로 뒤집어 위쪽 고정을 지원한다. 기본값에서는 종전 수식과 동일하다.
- 기존 씬/타일/프리팹/공유 머티리얼 자산 변경 없음. 새 프레임 루프·물리·렌더 타깃·자동 장식 생성 없음. 프리셋별 수치는 README에서 한 표로 관리한다.
- 바닥 Tilemap은 잔디 3종, 장식 SpriteRenderer는 나머지 시작값을 권장. 실제 아트/Play 시각 검증 없이 프리셋 품질이 확정되었다고 주장하지 않는다. 물·불·접촉·옆쪽 고정은 별도 범위.
- 변경 파일: PixelFoliageWind.cs, PixelFoliageLit.shader, PixelFoliageUV.hlsl, README.md와 AIAssets 기록. [범위/검사 계획](../../../docs/superpowers/plans/2026-09-27-pixel-foliage-variations.md).
- 검증: CLI Editor 빌드 오류 0·기존 경고 2. 프리셋 7개 범위/기본 동작 수식 유지/16·32·64·128px 고정점 수식/3패스 공통 UV 정적 확인. 실제 Unity 화면과 Undo UI는 실행하지 않음. CPU Update/LateUpdate/FixedUpdate 추가 없음.

### 후속 수정: 아무것도 움직이지 않음

확인한 원인은 셰이더 수식이 아니라 **동일 Tilemap의 grass.png/wall.png 혼용**이다. 실제 저장 씬의 420칸과 Sprite 참조표를 대조했을 때 잔디 252칸·벽 168칸이 있었고, PixelFoliageWind.Refresh의 단일 텍스처 검사에서 Fail → DisableEffect → _FoliageLayout=0으로 빠진다. 앞선 검증은 잔디 시트만 확인하고 실제 사용 Sprite 전체를 확인하지 못했다.

- 기존 Grid/Tilemap(50306929, Tilemap 50306932)은 잔디 252칸만 유지하고 기존 바람 연결/수치를 보존.
- Grid/Tilemap_Walls(50306940, Transform 50306941, Renderer 50306942, Tilemap 50306943)에 벽 168칸을 옮김. Transform/정렬은 원래와 같고 URP 기본 Sprite-Lit-Default 머티리얼만 사용. 바람 컴포넌트는 없음.
- 타일 배열의 Asset/Sprite 인덱스를 재매핑하고 모든 RefCount를 재계산. 좌표·Tile GUID·Sprite fileID/GUID·색/변환 인덱스·타일 플래그를 분리 전후 420개 전체 대조해 일치 확인. 기존 오브젝트는 원래 Tilemap 데이터와 Grid 자식 목록만 변경.
- PixelFoliageWind의 혼합 텍스처 오류에 실제 텍스처 이름과 레이어 분리 방법 표시. Shader/HLSL/머티리얼 수식 변경 없음.
- CLI 빌드 오류 0, 기존 ConfigPanelUI 경고 2. 저장 씬 기준 실패 조건 재현(텍스처 2개) → 수정 후 잔디 레이어 텍스처 1개 통과. 실제 GPU/Play 검증은 하지 않음.
- 수정 전 씬을 `Temp/BunnySlimeLab-before-foliage-fix-*.zip`에 백업. 강제 Refresh/Reimport/열린 씬 저장/Play 진입/커밋/푸시 없음. 열려 있는 씬의 미저장 작업은 보존한 뒤 디스크 변경 반영 필요.

하네스 교훈: 개별 아트 규격/수식 검사만으로 컴포넌트 성공 경로를 보장하지 않는다. 실제 씬의 사용 Sprite 목록과 모든 초기화 guard를 함께 검증해야 한다. YAML 참조표 검사 정규식은 `^  m_...`처럼 줄 시작과 들여쓰기를 함께 고정해야 하위 `m_Data`를 새 섹션으로 오인하지 않는다. 검사 첫 시도의 범위 오류를 이렇게 수정한 뒤 전체 타일 대조가 통과했다.

`Assets/_Game/Content/Maps/Development/BunnySlimeBattleLab/Scenes/BunnySlimeBattleLab.unity`의 Grid/Tilemap.

- Tilemap GameObject 50306929에 PixelFoliageWind(50306933) 연결.
- TilemapRenderer 50306931의 Material을 Mat_PixelFoliage_Grass로 교체.
- 좌우 2px, 상하 0px, 속도 0.45, 공간 차이 1.1, 밑동 고정 0, 경계 고정 폭 2px.
- 기존 잔디 그림, 32×32 슬라이스, 32 PPU, 타일 배치, Transform, Collider, 카메라, Chunk 모드는 변경하지 않았다.
- 기존에 변경되어 있던 실험실 씬/Shared 아트 이동은 보존했다. 씬 파일 전체를 재생성하지 않았다.

[제작자 사용법](../../../Assets/_Game/Presentation/PixelFoliage/README.md) · [당일 기록](../../2026-09-27-update.md) · [색인](../../index.md)

## 변경 파일

- `Assets/_Game/Presentation/PixelFoliage/`: PixelFoliageLit.shader, PixelFoliageUV.hlsl, Mat_PixelFoliage_Grass.mat, README.md와 meta.
- `Assets/_Game/Scripts/VFX/Runtime/PixelFoliageWind.cs`와 meta.
- 위 실험실 씬의 컴포넌트 1개/머티리얼 1개, 문서.
- 이전 `Presentation/PixelWind/`, PixelWindSprite.cs, PixelWindTilemap.cs 및 meta **15개 삭제**. 삭제 전 `Temp/PixelWind-before-rebuild-36a96f11d9fa4e2a9ab4000794fee84e.zip`에 압축 보관. Temp는 Git 추적 대상이 아니므로 장기 보관 보장은 없다.

## 구현 원리 및 참고

[aarthificial/pixelgraphics](https://github.com/aarthificial/pixelgraphics)의 [셰이더 문서](https://github.com/aarthificial/pixelgraphics/blob/master/Documentation~/shaders.md), grass_pixelart_shader.shadergraph와 [참고 영상](https://www.youtube.com/watch?v=ecYWvfMoRIM)을 확인했다. 원본은 일반 버전의 정점 변위와 픽셀 버전의 UV 변위를 구분하며, 원본 픽셀 버전은 SpriteRenderer 전용이다.

원본 ShaderGraph/패키지를 설치·복사하지 않고 UV 변위 접근을 프로젝트 URP 17.3에 맞게 구현했다. 프래그먼트에서 원본 texel 좌표를 구하고 변위를 정수 반올림한 뒤 해당 픽셀 중심을 읽는다. 밝기를 조절하지 않고 실제 그림의 위치를 바꾼다. Lit/Normals/Forward 모두 같은 UV를 사용한다. 경계 픽셀은 고정하고 원래 타일 사각형 밖을 샘플하지 않는다.

현재 타일맵은 한 원본 텍스처에 동일 크기로 정렬된 타일/동일 PPU/패킹 없음 조건이다. 조건이 맞지 않으면 잘못된 이웃 스프라이트를 읽는 대신 Inspector 상태에 이유를 표시하고 비활성화한다. 일반 스프라이트는 투명 여백과 Full Rect가 필요할 수 있다. 원본 영상의 접촉/충격파 Velocity Buffer는 이번 바람 요청 범위에서 제외했다.

## 검증 및 남은 확인

- 새 코드 포함 CLI Editor 빌드 오류 0, 기존 ConfigPanelUI 경고 2.
- 3패스 UV 함수 호출, 새로운 6 GUID/meta, 씬의 새 스크립트/머티리얼 연결 및 기존 PixelWind 코드/GUID 참조 0 확인.
- 수식 참조 검사(실제 GPU 실행 아님): 32×32에서 t=0 이동 픽셀 801개, t=0과 0.65초 사이 샘플 위치 변경 457개, 최대 이동 2px, 소수 픽셀 이동 0, 경계 이동 0, 타일 외부 읽기 0.
- Unity Play/강제 Refresh/Reimport/실제 GPU 출력/기기 성능 검증은 하지 않았다. CLI C# 빌드는 HLSL을 컴파일하지 않는다.
- git diff --check는 사용자가 변경한 다른 프리팹 오버라이드의 기존 `value: ` 공백을 보고했다. 이번 연결 수정과 무관하여 정리하지 않았다.
- 열려 있는 Unity 씬에 저장하지 않은 변경이 있으면, 해당 작업을 보존하면서 디스크 변경을 반영해야 한다.

## 하네스 교훈

2026-09-27 / 환경 바람 셰이더:
- 실패: 요청한 픽셀 움직임을 밝기 파동으로 대체하고 사용자가 적용하던 Tilemap 경로를 처음부터 확인하지 않음. 컴파일 통과와 시각적 목표 충족은 별개다.
- 수정: 실제 32×32 잔디 시트와 Tilemap 참조, 제공된 원본 자료를 확인하고 UV 정수 변위로 교체. 파일만 제공하지 않고 요청한 씬에 연결.
- 검증 기준: 시간에 따라 샘플 픽셀 좌표가 변하고 색 자체를 곱하지 않는지, 재질/컴포넌트가 실제 대상에 연결되는지 확인. 최종 Unity 화면은 별도 확인으로 구분.
- 도구: PowerShell의 GitHub 직접 요청은 연결 거부. 연결된 GitHub 읽기 도구로 tree/license/ShaderGraph를 조회했다. web 도구에서 영상 열기가 실패했으나 브라우저에서 재생 장면 및 공식 자막을 확인했다.
- rg에 Windows 경로 와일드카드를 직접 넘기면 오류 123. 실제 디렉터리와 `-g` 파일 패턴으로 검색한다.
- 전역 yjlim 메모리 경로는 허용된 쓰기 범위 밖이므로 이 프로젝트 문서에 기록했다.
