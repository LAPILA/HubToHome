# Tile Palette 격자·선택 테두리 미표시 — SSAO

2026-10-18 · Unity 6000.3.8f1 · 사용자 해결 확인 완료

## 증상과 실패한 접근

GrassWall 팔레트에서 붓 선택·팔레트 편집 OFF·Grid/Gizmos ON 상태인데도 격자선과 선택한 칸의 흰 테두리가 모두 보이지 않았다. 먼저 안내한 연필 OFF/붓 선택은 해결하지 못했다. Active Target이 비어 있는 점도 팔레트 자체의 집기·표시를 막는 조건은 아니었다.

## 확인과 성공한 접근

`GrassWall.prefab`의 셀 크기(1,1,0), 간격0, 원점·배율1은 정상이다. 설치된 Tilemap 패키지에서 격자 표시와 집기 외곽선 조건을 확인했고, 두 표시를 동시에 숨기는 현상을 도구 선택만으로 설명할 수 없었다.

Graphics/현재 PC 품질이 참조하는 `Assets/RenderSettings/PC_RPAsset.asset`의 기본 렌더러는 `PC_Renderer.asset`이다. 해당 렌더러의 `ScreenSpaceAmbientOcclusion`이 활성화돼 있었다. [Unity 공식 동일 이슈](https://issuetracker.unity3d.com/issues/the-tilemap-grid-in-the-tile-palette-is-not-visible-when-screen-space-ambient-occlusion-is-enabled), [동일 증상의 직접 보고와 SSAO 전환 결과](https://discussions.unity.com/t/tile-palette-tab-doesnt-show-grid-or-selection-gizmos/1605521/14)를 확인했다.

사용자가 `PC_Renderer.asset → Renderer Features → Screen Space Ambient Occlusion` 활성 체크를 해제한 뒤 **"되네 감사"**로 복구를 확인했다. 에이전트는 렌더러·프로젝트 코드·패키지·원본 이미지·Library를 수정하지 않았다.

## 재사용 절차와 주의점

1. 격자 버튼 ON과 브러시 집기 여부를 확인한다. 선택 테두리만 없는 현상과 격자/테두리가 함께 없는 현상을 구분한다.
2. 실제 Graphics/Quality가 참조하는 기본 Renderer를 확인한다. 다른 Renderer나 실행 카메라의 2D Renderer override만 검사하면 에디터 팔레트 문제를 놓칠 수 있다.
3. 동일 URP 증상이면 SSAO 활성 체크만 한 변수로 끄고 팔레트 격자/흰 테두리 복구를 확인한다. 기능을 삭제하거나 파이프라인 전체를 교체하지 않는다.
4. SSAO를 다시 켜면 재발 가능성이 있다. SSAO의 화면 기반 음영은 꺼지므로 게임 시각 효과 유지가 필요하면 별도 편집기 대응을 검토한다. 임의 에디터 업데이트/PackageCache 패치는 하지 않는다.

검증은 사용자 화면 복구 확인이며 자동 Unity 재생/기기 테스트가 아니다. 앞서 발생한 `GridPaintPaletteClipboard.DoDragPerform → Tilemap.tileAnchor` MissingReferenceException은 다른 미리보기 참조 문제이므로 이번 SSAO 해결로 함께 고쳐졌다고 해석하지 않는다.

## 하네스 교훈

날짜: 2026-10-18. 작업 맥락: Unity Tile Palette 표시 이상. 실패: 일반 사용법(편집 OFF/붓 선택)을 원인으로 먼저 추정했으나 사용자의 후속 스크린샷에서 해당 설정이 이미 정상임을 확인했다. 성공: 팔레트 YAML/패키지 표시 조건과 Unity 공식 이슈를 함께 확인하고 실제 활성 PC 렌더러의 SSAO를 한 변수로 비교했다. 적용 조건: URP에서 격자·선택 테두리가 함께 안 보이지만 타일 이미지는 보이는 경우. 검증: SSAO OFF 후 사용자 복구 확인. 이 머신에는 지정된 yjlim 전역 메모리 파일이 없어 저장소의 이 노드에 기록했다.
