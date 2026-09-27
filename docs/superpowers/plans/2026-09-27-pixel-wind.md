# Pixel Wind Implementation Plan

> 아래 초기/Tilemap 밝기 파동 설계는 사용자 피드백으로 폐기되었다. 현재 구현은 [PixelFoliage UV 변형](../../../Assets/_Game/Presentation/PixelFoliage/README.md)이며, PixelFoliageWind/새 머티리얼을 BunnySlimeBattleLab Grid/Tilemap에 직접 연결했다. 구 PixelWind 파일 15개는 백업 후 삭제했다. 아래는 실패/변경 이력 보존용이며 다시 구현할 계획이 아니다.

**Goal:** 정수 픽셀 단위로 잎 덩어리가 흔들리는 선택 적용형 URP 2D 셰이더.

**Architecture:** UV/Transform 대신 GPU에서 스프라이트 전체를 월드 XY로 평행 이동한다. SpriteRenderer별 설정 컴포넌트가 픽셀 크기·위상·강도를 MaterialPropertyBlock으로 전달하고 컬링 범위를 확보한다. 줄기와 잎은 별도 SpriteRenderer로 둔다.

**Tech Stack:** Unity 6000.3.8f1, URP 17.3.0, HLSL, C#, Odin Inspector.

사용자 요청에 따라 별도 승인 대기·에이전트·자동 커밋·Unity Play/강제 Refresh는 하지 않는다.

## 작업

- [x] `Assets/_Game/Presentation/PixelWind/PixelWindLit.shader`: Lit/Normals/Forward 패스에 동일 변위, 기존 URP 조명 함수 재사용.
- [x] 같은 폴더 `PixelWindCommon.hlsl`: 정수 양자화, 두 주기, 좌우 편향. 매 프레임 텍스처 수정 없음.
- [x] `Assets/_Game/Scripts/VFX/Runtime/PixelWindSprite.cs`: 활성화/명시적 갱신/스프라이트 변경 시에만 속성 및 bounds 설정, 해제 시 정리. 다른 속성 블록 값 보존.
- [x] 같은 셰이더 폴더 약한/강한 바람 머티리얼 2개, 사용법 및 모든 meta 작성. 기존 자산 대체 없음.
- [x] C# 컴파일 및 자산 GUID/셰이더 참조 정적 검사. Unity에서 셰이더 컴파일·실제 출력은 별도 확인 대상으로 명시.
- [x] 일일 기록·인수인계·색인 갱신.

## 수동 확인 기준

Point/무압축/정수 배율의 환경 스프라이트에 머티리얼과 PixelWindSprite를 적용한다. Play에서 1~3픽셀 수평 이동, 서로 다른 위상, 2D Light 반응, 화면 가장자리 컬링, 컴포넌트 비활성화 시 정지, 재활성화 시 복귀를 확인한다. 줄기·Collider·Transform은 이동하지 않아야 한다.

## 후속 Tilemap 지원

- [x] 실제 실험실 Grid/Tilemap의 머티리얼 단독 연결과 연결형 바닥 아트를 확인한다.
- [x] `PixelWindTilemap.cs`/meta를 추가한다. 기본 바닥 밝기 파동 / 선택 장식 레이어 이동. Chunk 모드 유지, 전용 MPB 갱신과 컬링 여유 복구.
- [x] 공유 HLSL 및 Lit/Forward에 표면 밝기 파동을 추가하고 SpriteRenderer 기본 동작을 유지한다.
- [x] CLI 컴파일·정적 계약을 확인하고 제작자 README/인수인계/일일 기록을 갱신한다.

Unity 수동 확인은 실제 Tilemap에 PixelWindTilemap을 붙여 기본 모드의 밝기 변화, 이동 모드의 레이어 이동, 해제 시 원래 모습과 청크 설정 복구를 확인한다. 씬 자동 변경/Play 실행은 하지 않는다.
