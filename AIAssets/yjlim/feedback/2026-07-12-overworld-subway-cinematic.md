# 오버월드 지하철 시작 연출

## 만들어진 흐름

`Title -> Intro -> OverworldScene` 뒤, SceneLoader가 화면을 검게 유지하는 동안 지하철 카메라와 기차 시작 위치를 먼저 준비한다. 화면이 밝아지면 지하철이 좌측에서 우측으로 이동하고 카메라가 함께 이동/줌인한다. 끝에서 검게 페이드한 뒤 시네마틱 카메라를 해제하고, 다시 밝아지면 기존 Player/ZEV 게임플레이 화면으로 돌아온다.

Player와 ZEV는 숨기거나 비활성화하지 않는다. 카메라가 오프스테이지 좌표의 연출만 바라보기 때문에 일반 게임플레이 대상은 그대로 유지된다.

## 제작 위치

- 원본 YAML: `Assets/_Game/Content/Scenarios/Source/Overworld/overworld_intro_subway.sequence.yaml`
- 런타임 시퀀스: `Assets/_Game/Content/Scenarios/Runtime/Overworld/overworld_intro_subway.asset`
- 카메라/기차 이동 데이터: `Assets/_Game/Content/Cinematics/Overworld/overworld_intro_subway_arrival.asset`
- 액션 카탈로그: `Assets/_Game/Content/Scenarios/ActionCatalogs/OverworldCinematicActionCatalog.asset`
- 씬 오브젝트: `OverworldCinematicStage_Subway`, `OverworldIntroSequenceTrigger`
- 재생성 메뉴: `HubToHome > 시나리오 > 샘플 > 오버월드 지하철 인트로 생성 또는 갱신`

## 시퀀스 메이커 사용

`HubToHome > 시나리오 > 시퀀스 메이커`를 열고 **독립 Action Sequence**에 `overworld_intro_subway` 에셋을, Action Catalog에 `OverworldCinematicActionCatalog`을 선택한다.

타임라인에는 네 액션이 보인다.

1. 지하철 도착 샷 재생
2. 검은 화면 페이드
3. 시네마틱 카메라 해제
4. 오버월드 화면 페이드 인

순서 변경, 액션 삽입, 파라미터 수정 뒤에는 `저장 및 반영`을 사용한다. YAML이 먼저 검증되고, 오류가 없을 때만 런타임 에셋을 바꾼다.

## 저장 동작

성공한 뒤 `GlobalDataManager.eventFlags`에 `overworld.intro.subway.completed = 1`이 저장된다. 같은 저장을 다시 불러오거나 다시 진입하면 연출은 건너뛴다. 전투 상태나 연출 중간 진행은 저장하지 않는다.

## 확인 필요

자동 테스트는 YAML/에셋/액션 계약을 통과했다. Play Mode에서 `HubToHome > 시나리오 > 샘플 > 오버월드 지하철 인트로 재생 테스트`를 실행해 SceneLoader 재진입, 기차 샷, Cutscene -> Exploration 복귀, Player/ZEV 최종 구도를 캡처로 확인했다.

실제 이름 입력을 거치는 Title -> Intro -> Overworld 첫 진입과 저장 슬롯을 다시 불러오는 전체 사용자 흐름은 한 번 수동 확인하면 된다.
