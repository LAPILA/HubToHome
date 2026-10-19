# 필드 메뉴 수정 위치

가방·장비·동료/성장·수집 일지의 실제 메뉴 코드입니다. 샘플 파티나 가짜 재화를 생성하지 않습니다.

| 수정할 내용 | 파일 |
|---|---|
| 열기/닫기, 페이지·현재 동료, 실제 서비스 호출 | OverworldMenuUI.cs |
| 창 크기·색상·목록·설명·동료 카드 | FieldMenuView.cs / FieldMenuWidgets.cs |
| 능력치 배분·스킬 트리 표시 | FieldMenuGrowthView.cs |
| 방향키 포커스·테두리 | FieldMenuNavigation.cs |
| 메뉴/설정 한정 클릭·터치·끌기·휠 | FieldMenuTouchInput.cs |
| 숫자 편집과 ± 길게 누르기 | FieldMenuNumberInput.cs / FieldMenuButtonRelay.cs |
| 정수 배율·안전 영역·숫자 키보드 회피 | FieldMenuViewport.cs |
| 한 번 캡처하는 흐린 배경·자원 정리 | FieldMenuBackdrop.cs |

설정창 자체는 상위 폴더 ConfigPanelUI.cs, 게임 규칙은 Characters/Items/Core 서비스에 있습니다. 셰이더는 Presentation/UI/Shaders, 빌드 포함용 material은 _Game/Resources/HubToHome/FieldMenuBackdropBlur.mat입니다.

기존 스크립트 meta/GUID·프리팹 참조를 유지했습니다. 테스트는 UI/Tests/Editor, 개발용 콘텐츠는 Content/Maps/Development에서 관리합니다. 기술적인 이름이나 폴더명은 AI 제작 여부의 증거가 아닙니다. 원본 아트·음원 출처와 개발 기록을 보존하며 이번 정리는 기록 은폐 작업이 아닙니다.

상세 사용법: AIAssets/yjlim/feedback/2026-10-19-field-menu-navigation.md (저장소 루트 기준).
