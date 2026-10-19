# 저스트가드 배경 충격파

사용자 승인 범위: 토끼 전투 실험실의 Z 저스트가드 성공 때 약한 원형 파장. 캐릭터 앞의 기존 스파크가 아니라 방어자의 Center에서 시작하며 Background 정렬 레이어만 왜곡한다. 일반 가드/회피/C 반격·UI·판정·피해·AP·기존 전조는 변경하지 않는다.

## 표시와 격리

현재 URP17.3 Renderer2D는 LayerBatch 내부에 공개 사용자 주입점을 제공하지 않는다. 상시 CameraSortingLayerTexture 복사나 추가 카메라/Renderer 전환 대신 활성 약0.3초에만 두 패스를 등록한다.

1. BeforeRenderingPostProcessing에서 카메라 색상과 같은 크기 R8/Point 마스크를 만든다. Background 범위를 흰색, 그 위 정렬 레이어들을 검정으로 같은 패스의 두 RendererList로 기록한다. 현재 Lab의 프레임 스프라이트·Tilemap 알파/flip을 따르며 투명영역은 남긴다. 원본 재질 keyword가 overrideMaterial로 전달되지 않는 GPU SpriteSkin 및 사용자 셰이더의 정점 변형은 이번 범위에서 지원하지 않는다.
2. 원본 UV와 파장으로 변위한 UV가 둘 다 배경 마스크에 허용될 때만 현재 카메라 색상을 왜곡한다. 그 외는 원본 유지. 새 cameraColor로 교체하고 기존 후처리를 계속한다. 픽셀 정렬을 위해 마스크를 임의로 다운샘플하지 않는다.

BeforePP이므로 캐릭터 Bloom을 따로 왜곡하지 않는다. 실루엣 근처에서 원본으로 돌아가는 보수적인 보호 영역이 남을 수 있다. 배경을 별도 카메라로 완전히 재합성하는 구조는 아니다. 실제 알파/타일맵/카메라 UV 정렬은 Unity 수동 확인 대상이다.

## 시간과 소유권

PlayerController.ConfirmDefenseSuccess의 Parry 분기 한 곳만 요청한다. CharacterBase.GetPivot(Center)의 충돌 당시 월드 좌표를 저장하고, 렌더할 때 투영해 카메라 이동을 반영한다. 전면 VFX의 FrontMiddle 피벗은 재사용하지 않는다.

Feature가 렌더한 게임 Base 카메라와 프로필을 등록하고 그 카메라에만 요청한다. 카메라별 최신 파장1개, 같은 소유자/프레임 중복은 무시한다. 만료/소유자 비활성/전투 이탈/장면 이동/Feature Dispose에서 정리한다. 렌더 조회에서만 unscaled delta를 프레임당 한 번 소비하고 BattleDelay pause 중에는 시간을 멈춘다. 코루틴/턴을 기다리게 하지 않는다.

프로필 기본값: 0.30초, 반경16→110기준픽셀, 고리폭14, 변위1.25기준픽셀, 약한 청백색 테두리(alpha0.04). 기존 ScreenShake/FlashIntensity 설정을 따르고 값은 요청 시 복사한다. 가벼운 signed polynomial 파형으로 안/밖 방향을 바꾸며 별도 노이즈/블러/삼각함수 텍스처는 없다.

## 연결과 검증

실험실 Renderer2D에만 새 Feature와 profile/shader 참조를 추가한다. 본편 기본 Renderer0/카메라/장면/프리팹/머티리얼·패키지 소스는 보존한다. 다른 씬에서는 Feature가 없으면 조용히 표시를 생략한다. 셰이더와 재질은 직접 참조/지연생성/재사용/Dispose하며 비활성 추가 GPU패스와 복사는0이다. 활성2패스 및 임시 마스크 비용은 숨기지 않는다.

검증은 C# Runtime/Editor 전체 CLI, HLSL 분리 구문 검사, 시간/마스크 정책/중복/만료/소유권 회귀소스·관리형 검사, GUID/meta/직렬화 참조/UTF8/diff다. UnityPlay/Refresh/씬저장/실기기·GPU·발열은 미실행으로 기록한다. 커밋·푸시는 하지 않는다.
