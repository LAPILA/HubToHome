# Telegraph 제작 애니메이션 연결

## 2026-09-28 후속 수정 — 재생 종료 뒤 남는 표시

- 원인: `SynchronizeToImpact`가 완료한 클립을 마지막 프레임에 고정했으며, 구형 DOTween도 마지막 밝은 상태로 유지했습니다. 전조 재생 시간보다 방어창이 길면 그동안 표시가 남았습니다.
- 수정: 실제 클립 길이와 남은 타격 시간 중 짧은 길이까지 재생한 뒤 Renderer를 즉시 숨깁니다. 구형 프리팹도 자기 트윈이 끝나면 숨기고 트윈을 정리합니다. 완료 뒤 중복 호출로 다시 표시되지 않습니다.
- 소유권: 표시는 먼저 끝나지만 풀 대여는 유지합니다. QTEManager의 정상 종료/취소/비활성화 경로에서 기존 `Release`로 한 번만 반환합니다. 조기 반환 후 오래된 QTE 참조가 다른 공격의 새 전조를 해제하는 문제를 피합니다. Release는 파괴가 지연되는 프레임에도 즉시 표시를 닫습니다.
- 판정 시계와 일시정지, 준비 프레임, Center/Sorting Order -1, 핑 1회, 다음 대여 시 처음부터 재생은 유지합니다. 패링·회피·C 성공 구간은 변경하지 않았습니다.
- 수정 파일: `BattleTelegraphCue.cs`, `QTEManagerDefensePipelineTests.cs`. 제작 파일의 길이를 0.235초로 고정하던 검사는 양수 길이 검사로 바꿨습니다. 사용자가 편집 중인 Aseprite와 프리팹·스킬 데이터는 변경하지 않았습니다.
- 검증: 실제 Cue 소스를 링크한 CLI 검사에서 종료 후 표시 등 5개 실패를 재현한 뒤 총 7개 통과. Unity/DOTween/풀은 대체 객체이므로 엔진 렌더링 검증은 아닙니다. Unity 회귀 검사는 추가·컴파일만 수행하며 Play/EditMode 실행은 하지 않습니다. 분리 실행기는 ignored `Temp/TelegraphCueChecks`에만 있습니다.

## 반영 내용

- `Telegraph.prefab`에 사용자가 넣은 Aseprite Animator와 핑 음원을 그대로 사용합니다. START는 5프레임·원본 합계 0.235초입니다. 단일 레이어여서 가져온 클립의 SpriteRenderer 경로는 루트입니다. 임포터 소스의 단일 레이어 생성 규칙을 확인했으며 프리팹 계층을 새로 만들지 않았습니다.
- 넓은 X 대응 구간에서는 첫 프레임을 흐리게 표시하고, 기존 정밀 대응 구간이 열릴 때 START와 핑을 한 번 시작합니다. 짧은 창은 애니메이션만 압축합니다. 방어/회피/특수반격 판정과 데미지 규칙은 변경하지 않았습니다.
- 기존 확대·반짝임 DOTween은 Animator가 없는 전조의 호환 경로에만 남겼으며 Inspector에서는 숨깁니다. 제작 애니메이션에는 추가 확대/45도 회전을 적용하지 않습니다. 크기가 작다는 후속 피드백에 따라 프리팹 Scale을 기존 PNG 프리팹 기준인 3으로 복원했습니다. C 반격의 청록색 구분은 유지합니다.
- QTEManager의 남은 타격 시간으로 프레임을 평가합니다. 별도 Update 시계가 없으므로 일시정지/슬로모션과 분리되지 않습니다. 재생 완료 시 숨기고, 방어창 종료의 풀 반환 시 복원/다음 사용 시 처음부터 시작합니다. 같은 전조의 Emphasize 중복 호출은 재생/소리를 반복하지 않습니다.
- 전조는 공격자의 `Pivots/Center`에 표시하고 World Offset은 0입니다. 기존 스킬 9개(방어 블록 12개), QTE 기본값, 스킬 기본값과 실험실 생성기도 Center로 맞췄습니다. 공격자와 같은 Sorting Layer에서 Order - 1입니다. 호출 횟수는 그대로입니다.

## 음원 연결

| 위치 | 사용 음원 |
| --- | --- |
| 전조 시작 | 프리팹의 기존 `SFX_Vefects_Magic_Impact_02.wav` 유지 |
| 전투 탭/목록/대상 이동 | `CUSTOM/SFX/ButtonMove.wav` 추가 |
| 전투 메뉴/대상 확정 | `CUSTOM/SFX/ButtonSelect.wav` 추가 |
| 전투 메뉴/대상 취소 | `CUSTOM/SFX/Cancle.wav` 추가 |
| 피격 / 전투 진입 | 기존 `Hit.wav` / `MeetEnemy.wav` 연결 유지 |
| 일반 선택 / 대사 음성 | 기존 `Select.wav` / `DefaultVoice.wav` 연결 유지 |

UI 자동 갱신이나 선택이 움직이지 않은 경우에는 이동음을 내지 않습니다. 비활성 명령 확정도 무음입니다. UI 효과음은 AudioManager의 UI Source, 전조음은 SFX Source를 사용합니다. 새 AudioSource는 만들지 않았습니다.

## 나중에 수정할 위치

1. 전조 모양/프레임: `Assets/_Game/Presentation/Custom_VFX/Sprites/Telegraph.aseprite`의 START 태그.
2. 전조 크기/위치/음량: `Telegraph.prefab`의 Scale / BattleTelegraphCue의 World Offset / Volume.
3. 핑 교체: 같은 컴포넌트의 Ping Clip. Aseprite 태그를 변경하면 `재생 클립`도 해당 태그 이름으로 변경합니다. 현재 값은 `START`이며 예전 `Base Layer.START` 직렬화 값도 읽을 수 있습니다.
4. 전투 메뉴 음원: `SeamlessBattleHost.prefab`의 BattleMenuUI → 효과음 3칸.

## 검증과 제한

후속 수정: 이전 코드는 Awake에서 Animator 속도를 0으로 만들고, 매 프레임 `Animator.Play + Update(0)` 및 현재 state 길이를 사용했습니다. 따라서 전투 시계 호출이 없는 단독 프리팹 확인은 그대로 정지했습니다. 전투 중 정지 증상의 세부 엔진 원인은 로그/화면 재현만으로 확인하지 못했습니다. 지금은 실제 `AnimationClip.length`를 읽고 클립을 직접 SampleAnimation하여 정지된 state 초기화·길이에 의존하지 않습니다. 이때만 Animator의 자동 평가를 비활성화하고 풀 반환 시 원상복구합니다. 단독 프리팹의 Animator 속도는 건드리지 않습니다.

- 최종 Runtime/Editor CLI 컴파일 오류 0, 기존 ConfigPanelUI 경고 2개. 최초 전체 빌드에서는 외부 Febucci 패키지 경고 1개도 있었습니다.
- Telegraph 5개/공용 호스트 521개 YAML 객체의 중복·누락 내부 참조 검사 통과. 연결한 외부 GUID가 실제 자산으로 해결되는 것을 확인했습니다.
- Aseprite 바이너리의 START 태그·프레임 시간을 읽었으며 그림 파일/임포트 설정은 수정하지 않았습니다.
- 애니메이션 바인딩/방어 시계 고정·단축/재사용과 UI 음원 계약 검사를 **컴파일만** 했습니다. 후속으로 Animator 속도 0에서도 클립 진행, 단독 프리팹 비정지, Center 스킬 계약 검사를 보강했습니다. Unity Play/EditMode 테스트·Refresh·재임포트·씬 저장은 하지 않았습니다. 실제 화면/청음은 미검증입니다.
- 브랜치 `codex/gameplayEdit`, 커밋/푸시 없음. 사용자 변경 및 앞선 HUD 작업은 보존했습니다.

[작업 기록](../../2026-09-26-update.md) · [실험실 사용법](../../../docs/bunny-slime-battle-lab.md) · [인덱스](../../index.md)
