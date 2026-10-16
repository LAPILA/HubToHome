# 본편 제작을 위한 기능 기획 우선순위

현재는 기본 전투와 저장 기능을 더 늘리기보다 동료별 전투 역할, 성장 선택, 장비 효과, 탐색 중 자원 소모를 연결하는 기획이 우선이다. 아래의 기획 질문은 새 승인 사항이 아니며, 현재 구현이 없는 항목과 본편 규칙을 확인할 항목을 구분한다.

## 우선 기획할 기능

| 순위 | 기능 | 현재 상태 | 결정할 내용 |
| --- | --- | --- | --- |
| 1 | 동료별 전투 역할과 고유 자원 | 본편 AllyDB에는 WizzelDB가 있으며 BattleResources에는 WizelPressure가 있다. 위젤 외 고유 규칙은 CONTEXT에서도 미구현으로 명시한다. 실험실 복제 파티를 본편 동료 구현으로 세지 않는다. | 동료마다 잘하는 상황, 약점, 자원 획득·소비 조건, 처음 배우는 스킬, 다른 동료와의 조합. |
| 2 | 성장과 스킬 구성 | 경험치·레벨·능력치 투자·스킬 트리·장착·초기화 경로가 있다. 현재 기본 성장 프로필은 레벨당 능력치 3점/스킬 1점이고 스킬 트리는 최대 6개 장착이다. | 자동 성장과 투자 중 어디에 무게를 둘지, 챕터별 목표 레벨, 습득 방식, 장착 수, 초기화 비용·장소. 기존 값을 유지할지도 결정에 포함한다. |
| 3 | 장비 특수 효과 | 능력치·저항 보정, 수량과 장착 제한은 구현되어 있다. PassiveEffectID와 EquipReactionDialogueID는 실행되지 않는 예약 필드다. | 수치 위주 장비인지 조건부 효과 장비인지, 발동 조건·중첩·횟수 제한, 무기 교체와 강화 중 어느 방향인지. 현재 슬롯은 무기/장신구2/머리/몸/신발이다. |
| 4 | 전투 밖 파티 편성 | 전열 3명과 후열 승계, 전멸 후 계속/포기 선택은 있다. 현재 오버월드 메뉴는 Item/Equip/Power/Config이며 전용 편성 화면은 확인되지 않았다. | 출전 순서 고정 여부, 편성 가능한 장소, 주인공 고정 여부, 후열의 의미. 전투 중 수동 교대를 추가한다는 뜻은 아니다. |
| 5 | 보상과 회복 경제 | 승리 EXP·돈·드롭, 상점 거래·회복 서비스가 있다. BattleRewardService는 현재 파티의 모든 유효 멤버에게 같은 EXP를 지급한다. | 일반전/보스 보상량, 물약 가격과 회복량, 휴식·저장 지점 회복, 전투 간 HP/AP 유지, 후열·사망자 EXP 정책 유지 여부. |
| 6 | 전투 정보 확인 | 상태 누적 아이콘·숫자와 스킬 설명은 있다. 전용 적 분석/상태 상세 화면은 이번 검색 범위에서 확인되지 않았다. | 무엇을 언제 어떤 키로 보여줄지, 약점·저항 공개 범위, 적 공격 중 열 수 있는지, 설명 중 시간 정지 여부. |
| 7 | 모바일과 패드 조작 표시 | GameInput에 패드 대응 바인딩이 있다. BattleInputHintView는 동작별 KeyboardText와 단일 Image를 사용하며 자동 기기별 표시 전환은 확인되지 않는다. 자체 Scripts에서 터치 전용 조작 화면도 확인되지 않았다. | 가상 버튼·메뉴 이동 방식, 버튼 배치와 크기, 기기 전환 표시, 홀드 방어 및 연타 접근성. |

## 이후 선택할 확장

열차는 노선·정류장·해금 플래그·이동을 다루는 구조가 있다. 열차에서 휴식·편성·동료 대화·장비 관리 중 무엇을 제공할지는 별도 기획이다. 기본 이동 기능이 없다는 뜻은 아니다.

특수 전투 `aim_shooter`는 모듈 수명과 전투 규칙의 일부가 있으나 완성된 입력·투사체·전용 UI 플레이 루프는 아니다. 격자 전투를 포함한 별도 전투 방식은 챕터에서 필요한 경우에만 구체화한다. 현재 턴제 전투를 마감하기 위해 필수로 추가할 항목은 아니다.

## 다시 만들 필요가 없는 기반

Z 방어/저스트가드, X 회피, C 특수 반격, 속도 기반 턴 예고, 누적 상태 5종, 후열 승계 선택, 수동 3슬롯/자동 저장, 최근 저장 재시도, 성장·스킬 장착·상점 거래의 코드 경로는 존재한다. 구현 존재는 실제 Unity/기기 통합 검증 완료와 다르다.

2026-06-19 TODO에 남은 Continue 미구현 등의 항목은 현행 구현을 반영하지 못하므로 그대로 미완성 목록으로 복사하지 않는다. 현재 GameOverUI는 GameLoadCoordinator.LoadMostRecent를 호출한다.

## 확인 근거

- [EquipmentData](../../../Assets/_Game/Scripts/Characters/Data/EquipmentData.cs): 실제 슬롯, 능력치 보정, 실행되지 않는 예약 효과 필드.
- [기본 성장 수치](../../../Assets/_Game/Content/Characters/Growth/GrowthBalance_Default.asset)와 [현재 스킬 트리](../../../Assets/_Game/Content/Characters/SkillTrees/Player_SkillTree.asset).
- [SkillTreeProgressionService](../../../Assets/_Game/Scripts/Characters/Runtime/SkillTreeProgressionService.cs): 해금·장착 제한·초기화.
- [BattleRewardService](../../../Assets/_Game/Scripts/Battle/Runtime/Services/BattleRewardService.cs): 돈·드롭·전체 파티 EXP 지급.
- [OverworldMenuUI](../../../Assets/_Game/Scripts/UI/Runtime/OverworldMenuUI.cs): 현재 메뉴 범위.
- [BattleInputHintView](../../../Assets/_Game/Scripts/UI/Runtime/BattleInputHintView.cs): 입력 안내 모델.
- [GameOverUI](../../../Assets/_Game/Scripts/UI/Runtime/GameOverUI.cs): 최근 저장 재시도.
- [TrainStopDefinition](../../../Assets/_Game/Scripts/Overworld/Runtime/Travel/TrainStopDefinition.cs): 정류장 해금과 도착 정보.
- [프로젝트 용어와 구현 경계](../../../CONTEXT.md): 위젤 고유 자원과 aim_shooter 범위.

## 점검 범위

2026-10-13, `codex/gameplayEdit`의 현재 작업 트리와 데이터 정의를 읽었다. 코드 수정, Unity 실행, 컴파일, 자산 갱신, Notion 수정, 커밋, push는 하지 않았다. 기능의 존재와 공개 계약을 확인한 점검이며 전체 코드 버그 감사는 아니다.
