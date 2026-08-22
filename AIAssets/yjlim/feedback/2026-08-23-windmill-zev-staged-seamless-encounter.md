# Windmill Wizzel-ZEV 대화와 필수 심리스 전투

## 결과

Windmill Exterior에 배치된 ZEV와 상호작용하면 아래 순서로 진행됩니다.

1. Wizzel/ZEV 전투 전 대화 6문장
2. 두 캐릭터가 서로 가까이 이동
3. 양쪽 일반 공격 애니메이션과 기존 공격 VFX 재생
4. 전투 직전 대화 2문장
5. 같은 맵의 SeamlessBattleHost로 전투 시작

이 조우는 도주할 수 없습니다. Run 버튼은 사라지지 않고 회색으로 남으며, 클릭과 직접 실행 요청이 모두 차단됩니다.

## 대사·표정 수정 위치

- 전투 전 대사:
  `Assets/_Game/Content/Maps/Regions/Chapter01/Data/Dialogue/Dialogue_Wizzel_ZEV_PreClash.asset`
- 공격 뒤 대사:
  `Assets/_Game/Content/Maps/Regions/Chapter01/Data/Dialogue/Dialogue_Wizzel_ZEV_PostClash.asset`
- Wizzel 화자/표정 연결:
  `Assets/_Game/Content/Dialogue/Data/Speakers/Speaker_Wizzel.asset`
- ZEV 화자:
  `Assets/_Game/Content/Dialogue/Data/Speakers/Speaker_ZEV.asset`
- Wizzel 얼굴 원본:
  `Assets/_Game/Content/Art/Characters/Player/Wizzel/`

표정 종류는 Normal, Happy, Confused, Angry 네 가지입니다. 대사는 DialogueData의 Nodes 순서대로 수정하면 됩니다.

## 맵 연결 위치

`Assets/_Game/Content/Maps/Regions/Chapter01/Prefabs/Rooms/Room_Chapter01_WindmillExterior.prefab`

이 Prefab의 ZEV 인스턴스에 전투 전/후 DialogueData와 필수 전투 설정이 연결되어 있습니다. 공용 `ZEV_Prefab`을 직접 바꾸지 않았으므로 다른 맵의 ZEV에는 영향이 없습니다.

## 연출 시간 조정

Windmill ZEV 인스턴스의 `DialogueBattleNPC`에서 다음 값을 조절합니다.

- `Staged Approach Stop Distance`: 서로 멈추는 거리
- `Staged Approach Duration`: 접근 시간
- `Staged Attack Hold Duration`: 공격 뒤 대기 시간

`Use Staged Encounter`, `Require Seamless Battle Host`, `Defeat On Victory`는 이 조우에서 켜 둡니다. `Allow Escape`는 꺼 둡니다.

## Choice Root

Dialogue Canvas의 Cinematic/Overworld Choice Root는 모두 Y 0을 사용합니다. Prefab에서 위치를 바꿔도 런타임 코드가 다시 덮어쓰므로, 이후 공통 위치를 변경할 때는 `DialogueUI._choiceAnchoredPosition`과 두 DialogueUI Prefab 값을 함께 수정해야 합니다.

## 검증 결과

- Wizzel/ZEV 콘텐츠와 Windmill 연결: 8/8
- Choice Root 배치: 3/3
- 대화·조우·전투 도주 정책 집중 회귀: 39/39
- 필수 심리스 전투 Run 버튼 통합 검증: 1/1

수동 Play 확인은 남아 있습니다.

## Initial Room 오류가 다시 보일 때

`[RoomContainer] 유효하지 않은 RoomDefinition입니다.`가 표시되면 먼저 `Room_Chapter01_WindmillExterior.prefab`에 Git 충돌 마커가 남아 있지 않은지 확인합니다. 이번 오류는 Scene의 Initial Room 설정이 아니라 깨진 Prefab import 때문에 Definition의 Room Prefab 참조가 null로 해석된 경우였습니다. 현재 Prefab은 Wizzel-ZEV 설정과 `SeamlessBattleHost`를 보존한 정상 병합본으로 복구되어 있습니다.
