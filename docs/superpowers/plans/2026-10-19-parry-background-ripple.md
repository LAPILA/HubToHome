# 저스트가드 배경 충격파 구현 계획

> **For agentic workers:** 기존 승인 범위를 분담해 실행하고 마지막에 별도 읽기전용 검토를 받는다. 커밋/푸시는 사용자 요청 전 금지.

**Goal:** 토끼 실험실 Z 저스트가드 충돌 때 방어자 중심으로 Background만 약하게 일렁이게 한다.

**Architecture:** 공용 성공표시 훅→카메라별 최신 파장 상태→BeforePP BG 허용마스크/UV왜곡. UI/캐릭터/전투 판정과 분리한다.

**Tech Stack:** Unity6/URP17.3 RenderGraph, RendererList/SortingLayerRange, Core2D mask shader, Odin 프로필, 기존 PlayerController/캐릭터 Center/BattleDelay.

## Chunk1: 상태·트리거

- [x] `VFX/Data/ParryBackgroundRippleProfile.cs` +meta: 유한값·범위·Odin 설정/기본 0.3s·16→110px·폭14·변위1.25·테두리alpha0.04.
- [x] `VFX/Runtime/ParryBackgroundRipple.cs` +meta: Register/Unregister camera/profile/feature owner, TryPlay(player,worldCenter), TryGetFrame, 최신1개/동일프레임중복방지/프레임별unscaled clock/pause/scene/owner cleanup. Frame은 OriginWorld/RadiusPixels/WidthPixels/AmplitudePixels/RingColor만 제공한다.
- [x] `Overworld/Runtime/PlayerController.cs`: ConfirmDefenseSuccess의 Parry에만 Center 기반 요청 한 줄. 대미지·보상·기존VFX/피벗/C는 유지한다.
- [x] 상태/프로필 회귀소스 추가. 일반가드·회피·C와 중복 요청을 구분한다.

## Chunk2: 배경 마스크·왜곡

- [x] `VFX/Runtime/ParryBackgroundRippleRendererFeature.cs` +meta: Lab에만 등록, 게임 Base카메라 등록, 활성 때만 재질/2패스. Background white→상위 nonBG black RendererList를 한 마스크패스에서 렌더한다. 색상과 같은 크기 Point R8(미지원은RGBA8), depth/prepass/copyback 없음.
- [x] `Presentation/BackgroundRipple/BackgroundRippleMask.shader`: Core2D frame sprite/Tilemap alpha/flip으로 mask값만 기록. GPU SpriteSkin/커스텀 정점 변형은 미지원으로 명시하고 패키지는 수정하지 않는다.
- [x] `Presentation/BackgroundRipple/BackgroundRipple.shader`: 화면과 worldCenter투영 기반 원형고리, 양방향약변위·감쇠. dest/source BG 둘다허용할때만 왜곡/링색, 나머지원본. BeforePP cameraColor교체.
- [x] 프로필 asset와Lab RendererFeature subasset/직접Shader참조 연결. 기존슬롯0/ScreenSplit/씬/캐릭터재질은보존한다.

## Chunk3: 검증·기록

- [x] `Library/CodexChecks/Chapter01Foundation/Compile.ps1 -Target All`: Runtime/Editor errors0 (426/357소스, 기존경고222/0).
- [x] HLSL 분리 compile, 상태/마스크수식 관리형검사, asset references/GUID/meta/UTF8/diff검사. UnityPlay/강제Refresh/NUnit/실기기는하지않는다.
- [x] 읽기전용검토로캐릭터보호/알파·PPC좌표/리소스/취소를확인하고범위내보완한다. GPU SpriteSkin지원약속제거, 나머지추가문제없음.
- [x] `AIAssets/yjlim/feedback/2026-10-19-parry-background-ripple.md` 사용법/성능한계, 당일기록/CONTEXT/전투규칙·색인/계획현행화. 본편맵은Feature연결전까지표시생략됨을명시한다.
