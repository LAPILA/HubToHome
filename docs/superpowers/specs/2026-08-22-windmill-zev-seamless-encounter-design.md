# Windmill ZEV 심리스 조우 설계

## 목표

Chapter 1 Windmill 외부 룸에 배치된 ZEV와 상호작용하면 아래 순서를 한 번의 조우 흐름으로 실행한다.

1. 위젤과 ZEV의 첫 대화
2. 대화 종료 후 두 배우가 서로에게 접근
3. 양쪽이 기존 일반 공격 애니메이션과 효과를 동시에 재생
4. 짧은 후속 대화
5. 현재 룸의 `SeamlessBattleHost`를 이용해 ZEV 전투 시작

동시에 Dialogue 선택지 영역은 런타임과 Prefab 모두 Y=0을 기준으로 사용하고, 위젤의 네 표정 초상화를 대화 데이터에 등록한다.

## 확인한 선택지

### 1. `DialogueBattleNPC`의 선택형 단계 조우 — 채택

기존 컴포넌트에 기본 비활성인 단계 조우 설정을 추가한다. Windmill의 ZEV 인스턴스에서만 활성화하며, 다른 ZEV와 기존 선택지 전투 동작에는 영향을 주지 않는다. 기존 대화·애니메이션·전투 진입 API를 그대로 재사용하므로 변경 범위가 가장 작다.

### 2. 범용 Action Sequence 확장 — 보류

`overworld.actor.move`, `overworld.actor.attack`, `encounter.start`를 새 액션으로 추가하면 재사용성은 높다. 하지만 액션 카탈로그, 실행기, 대상 해석기, 미리보기와 문서를 함께 확장해야 하므로 이번 샘플에는 과도하다.

### 3. Windmill 전용 연출 스크립트 — 제외

빠르게 만들 수 있지만 인물 이름과 대사 흐름이 코드에 박히고 다른 조우에 재사용할 수 없다. 콘텐츠 데이터와 런타임 책임이 섞이므로 사용하지 않는다.

## 런타임 구조

`DialogueBattleNPC`는 기존 일반 대화/선택지 전투 경로를 유지한다. 선택형 단계 조우가 켜진 인스턴스만 다음 흐름을 사용한다.

```text
Interact
  -> 첫 DialogueData 재생
  -> GameState.Cutscene 획득 및 플레이어 이동 정지
  -> Player/ZEV 위치를 안전 거리까지 동시에 보간
  -> 서로 바라보기
  -> 기존 Attack 트리거와 기본 공격 효과 동시 재생
  -> 후속 DialogueData 재생
  -> BattleEncounterService.StartEncounter(... useDedicatedBattleScene: false)
  -> 현재 SeamlessBattleHost에서 전투 시작
```

단계 조우 설정에는 후속 대화, 접근 정지 거리, 접근 시간, 공격 후 대기 시간, 심리스 호스트 필수 여부만 둔다. 대사와 캐릭터 고유 이름은 코드에 넣지 않는다.

새 입력은 흐름이 끝날 때까지 차단한다. 재상호작용과 선공도 거부한다. 두 대화는 `DialogueManager.TryStartDialogue(..., onCancelled, out playbackGeneration)`으로 시작하고, 이 컴포넌트가 시작한 재생 세대를 기억한다. 비활성화될 때에는 자신의 세대만 `CancelDialogue(playbackGeneration)`로 취소한다.

이동 Tween은 오브젝트 수명에 연결한다. 대화 시작 실패, 대화 취소, 비활성화 또는 심리스 전투 시작 실패 시 Tween을 제거하고 두 배우의 연출 전 위치, 게임 상태와 진행 플래그를 복구한다.

심리스 전투는 `EnemyData.Prefab`으로 별도의 전투용 ZEV를 만들므로, 전투 요청 직전에 룸의 원본 ZEV에 속한 Renderer와 Collider 상태를 캡처한 뒤 숨긴다. 전투 시작이 거절되면 즉시 복구한다. 전투 종료 콜백에서는 다음 정책을 사용한다.

- 승리: 원본 ZEV를 숨긴 채 유지하고 상호작용을 막는다. 안정적인 조우 ID `chapter01.windmill.zev_duel`을 처치 완료 상태로 기록한다.
- 도주·중단: 원본 ZEV의 위치, Renderer, Collider와 상호작용을 연출 전 상태로 복구한다.
- 파티 전멸: Game Over 흐름이 소유하며 원본을 임의 복구하지 않는다.

결과를 구분하기 위해 `DialogueBattleNPC`는 기존 `IEncounterSource`와 함께 `IEncounterOutcomeSource`를 구현한다. 기존 bool 콜백은 호환을 위해 유지하고 `true`는 `Victory`, `false`는 `Escaped`로 변환해 결과형 메서드에 위임한다.

강제 중단은 기존 소스에 알리지 않는 현재 계약을 보존한다. 새 opt-in `IEncounterAbortSource.OnEncounterAborted(PlayerController)`를 추가하고 `DialogueBattleNPC`만 구현한다. `BattleManager.AbortSeamlessBattle()`는 기존 `CompleteSeamlessBattleCleanup(Unknown, false)`를 유지하되, 정리 전에 활성 소스가 이 인터페이스를 구현한 경우에만 중단 콜백을 한 번 보낸다. `AreaTrigger`, `OverworldEnemyMarker`와 기존 테스트 프로브에는 새 콜백이 가지 않으므로 레거시 동작은 바뀌지 않는다.

`DialogueBattleNPC`에는 선택적 조우 ID override와 승리 시 제거 여부를 둔다. Windmill 인스턴스는 위 ID와 승리 제거를 사용한다. `Awake`/`OnEnable` 및 상호작용 직전에 `GlobalDataManager.TryGetOverworldEnemyState`를 확인해 이미 처치된 조우면 원본 ZEV를 숨기고 상호작용을 막는다. 따라서 룸 재생성이나 저장 불러오기 후에도 ZEV가 다시 나타나지 않는다.

## 콘텐츠 데이터

### 위젤 초상화

`Assets/_Game/Content/Art/Characters/Player/Wizzel/대화얼굴/`의 네 PNG를 Sprite/Point/무압축/밉맵 끔으로 설정하고 다음 감정에 연결한다.

| 파일 | 감정 |
|---|---|
| `wizzel_normal.png` | `Normal` |
| `wizzel_happy.png` | `Happy` |
| `wizzel_confuse.png` | 새 `Confused` |
| `wizzel_angry.png` | `Angry` |

`Confused`는 기존 직렬화 값을 보존하도록 `EmotionType` 마지막에 추가한다. `SpeakerData`의 표정 Dictionary가 저장 후에도 유지되도록 Odin 직렬화가 가능한 ScriptableObject 기반을 사용한다.

Speaker는 위젤과 ZEV를 각각 만든다. ZEV는 기존 EnemyData 초상화를 `Normal`에 재사용한다.

### 대화

Chapter 1 지역 전용 폴더에 첫 대화 6노드와 후속 대화 2노드를 별도 `DialogueData`로 만든다. 선택지는 넣지 않는다.

| 순서 | 데이터 | Speaker | Emotion | LocalizationKey | DefaultText |
|---:|---|---|---|---|---|
| 1 | 첫 대화 | 위젤 | `Normal` | `chapter01.windmill.zev_duel.pre.001` | 굳이 싸워야 하는 건가요...? |
| 2 | 첫 대화 | ZEV | `Normal` | `chapter01.windmill.zev_duel.pre.002` | 어쩔 수 없습니다만, 의뢰인의 요청입니다. |
| 3 | 첫 대화 | 위젤 | `Confused` | `chapter01.windmill.zev_duel.pre.003` | 굳이 의뢰라고 싸워야 할 필요는 없잖아요! |
| 4 | 첫 대화 | 위젤 | `Happy` | `chapter01.windmill.zev_duel.pre.004` | 그냥 적당히 못 찾은 척 넘어가면 되죠! |
| 5 | 첫 대화 | ZEV | `Normal` | `chapter01.windmill.zev_duel.pre.005` | 저희 용병단의 운영 원칙에 어긋납니다. 죄송하지만... |
| 6 | 첫 대화 | 위젤 | `Angry` | `chapter01.windmill.zev_duel.pre.006` | 그럼 싸워야겠네요. |
| 7 | 후속 대화 | ZEV | `Normal` | `chapter01.windmill.zev_duel.post.001` | 너무 약하시군요... |
| 8 | 후속 대화 | 위젤 | `Normal` | `chapter01.windmill.zev_duel.post.002` | 으윽... |

Windmill 룸 안의 ZEV 중첩 인스턴스에만 첫 대화, 후속 대화, 단계 조우 옵션과 ZEV 적 데이터를 연결한다. 전역 `ZEV_Prefab`과 기존 `SampleDialogue.asset`은 수정하지 않는다.

## Choice Root

`DialogueUI` 기본 `_choiceAnchoredPosition`, `DialogueCanvas.prefab`의 Overworld/Cinematic 두 UI 값과 두 ChoiceRoot RectTransform, `DialogueManager.prefab`의 중첩 override를 모두 Y=0으로 통일한다. 이렇게 해야 Inspector에서 0으로 보여도 `PrepareChoiceUI()`가 다시 120으로 덮어쓰는 문제가 사라진다.

## 심리스 전투 조건

Windmill 룸에 활성 `SeamlessBattleHost`와 `BattleManager`가 있어야 한다. 전투 호출 직전에 `SeamlessBattleHost.Instance`가 존재하고 `IsRuntimeReady(out error)`가 성공하는지 검사한다. 실패하면 `BattleEncounterService.StartEncounter`를 호출하지 않아 전용 BattleScene으로 조용히 대체되지 않게 하고, 구체적인 오류를 남긴 뒤 Cutscene 상태와 배우를 복구한다.

이 ZEV 조우는 필수 전투로 설정한다. 조우 컨텍스트의 `AllowEscape` 기본값은 기존 전투 호환을 위해 `true`이고, Windmill ZEV 인스턴스만 `false`를 전달한다. `BattleMenuUI`의 도주 버튼은 `SetActive(false)`로 숨기지 않는다. 기존 `SetRunEnabled(false)` 경로를 사용해 버튼을 회색으로 표시하고 `interactable=false`로 만들어 마우스·키보드·패드 모두 선택할 수 없게 한다. UI 외부에서 `PlayerMenuAction.Run`이 직접 전달되어도 BattleManager가 다시 거부한다.

## 검증

- Choice Root의 직렬화 위치와 런타임 재배치가 모두 `(0, 0)`인지 검사
- `SpeakerData` 저장·재로드 후 네 표정이 유지되는지 검사
- 대화 자산의 노드 수, Speaker, 감정, 기본 문구를 검사
- 단계 조우가 꺼진 기존 `DialogueBattleNPC` 동작이 유지되는지 검사
- 단계 조우에서 첫 대화 → 접근 → 공격 → 후속 대화 → 전투 순서와 재진입 방지를 검사
- 실패·대화 취소·비활성화 시 소유한 대화 세대만 취소되고 Tween, 위치와 GameState가 복구되는지 검사
- 전투 시작 중 원본 ZEV가 숨겨지고 승리 시 숨김 유지, 도주·중단 시 원상 복구되는지 검사
- 결과형 콜백이 Victory/Escaped/PartyDefeated/Unknown을 보존하고 기존 bool 콜백이 호환되는지 검사
- 기존 소스는 강제 심리스 중단 통지를 받지 않는 현재 회귀 테스트를 유지하고, `IEncounterAbortSource`를 구현한 Windmill 조우만 중단 콜백을 한 번 받아 원본을 복구하는지 검사
- 저장된 `chapter01.windmill.zev_duel` 처치 상태를 룸 재진입 시 적용하는지 검사
- `SeamlessBattleHost.IsRuntimeReady`가 실패할 때 전용 BattleScene으로 대체되지 않는지 검사
- 필수 전투에서는 도주 버튼이 활성 상태로 남아 있으나 회색·비상호작용 상태이고, 직접 Run 명령도 전투를 종료하지 않는지 검사
- 일반 조우는 기본 `AllowEscape=true`를 유지하는지 검사
- Windmill ZEV 인스턴스가 지역 전용 대화와 ZEV 적 데이터, 심리스 필수 설정을 참조하는지 검사
- Runtime과 Editor 어셈블리 컴파일 후 관련 EditMode/PlayMode 테스트 실행

## 제외 범위

- 범용 오버월드 Action Sequence 문법 추가
- 전역 ZEV 프리팹의 행동 변경
- 초상화 원본 픽셀 수정
- 전투 밸런스, 보상, ZEV 스킬 변경
- Windmill 룸의 다른 사용자 배치 수정
