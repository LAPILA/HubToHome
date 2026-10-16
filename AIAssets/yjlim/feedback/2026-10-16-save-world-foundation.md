# 챕터 1 저장·월드·전투 직전 재도전

2026-10-16 사용자 확정 규칙에 따른 구현 기록. Unity Play/강제 새로고침/씬·프리팹 재저장은 하지 않았다.

## 저장과 휴식

`SaveManager.TrySaveProgress`의 수동 슬롯 0~2는 `GlobalDataManager.ToSaveData()`가 만든 독립 복사본을 사용한다. `ManualSaveRestPolicy`가 장비·투자를 반영한 최대 HP/AP로 회복하고 `RespawnOnManualSave=true`인 필드 적의 `IsDefeated`만 해제한다. 파일 원자 저장이 성공해야 `ApplyManualSaveRest`와 필드 표시 복귀를 실행한다. 취소·파일 기록 실패에는 현재 파티·필드 적을 바꾸지 않는다.

자동 슬롯 99에는 휴식 효과가 없다. EncounterMemory의 승리 이력·한 번 본 대사와 상자/이벤트 플래그는 수동 휴식으로 초기화하지 않는다. 이전 저장의 리스폰 속성은 false로 보존하며, 실제 필드 적을 로드할 때 현재 적 데이터로 정책을 등록한다.

## 적 분류와 필드 재등장

`EnemyData.Rank`: Normal / Elite / Boss. `EnemyData.Kind`: Human / Monster / WhiteEntity. 두 축은 별개다. 새 필드가 없던 기존 데이터는 Normal/Monster 기본값을 사용하므로 본편 보스·엘리트 데이터는 제작자가 등급을 지정해야 한다. 이름을 보고 보스라고 추정하지 않는다.

`OverworldEnemy`는 주 적과 동반 적 모두 일반 등급인 접촉 조우만 휴식 재등장 대상으로 등록한다. 패배 때문에 자신을 비활성화한 적만 원래 위치로 복구하며, 전투 복제체와 스토리 비활성 개체는 켜지 않는다. 비활성 부모 방은 비활성 그대로 둔다. 재등장 가능한 일반 적은 전용 전투 진입 때 `destroyAfterTouch`로 파괴하지 않는다.

## 재도전

`BattleEncounterService`는 보스 포함 또는 도주 불가 조우의 진입 직전에 파티·돈·인벤토리·장비·플래그·조우 기억을 메모리 스냅샷으로 만든다. 실제 전투 시작 transaction이 성공해야 `BattleRetryCheckpoint`에 확정한다. 일반전 진입 성공은 이전 체크포인트를 버린다. 진행 중 전투 상태를 디스크에 저장하지 않는다.

게임오버에서 체크포인트가 있으면 `전투 직전부터 재도전`, 없으면 기존 최근 저장 복구를 제공한다. 재도전은 원래 필드/방 상태 복원 후 같은 조우를 다시 시작한다. 원본 소스는 씬 재로드로 파괴되므로 `IEncounterRetrySource.RetryEncounterId`로 새 소스를 찾는다. 동일 ID가 둘 이상이거나 소스가 없으면 임의의 대상을 선택하지 않고 실패를 표시한다.

새 게임/일반 불러오기에는 `ClearUnlessRestoring()`, 승리·도주에는 `CompleteEncounter(outcome)`를 호출한다. 패배에는 체크포인트를 유지한다. 재도전 복원 자체가 호출하는 `FromSaveData`에서는 체크포인트를 지우지 않는다.

## 진행 이벤트 자동 저장

`SceneActionSequenceTrigger`는 성공 결과에서 완료 플래그 기록 후 자동 저장을 예약한다. 실패/취소에는 저장하지 않는다. 반복 연출 전용인 경우 `autoSaveOnComplete`를 끌 수 있다. 체크포인트로 필드 씬을 복원 중일 때는 씬 진입 연출을 재생하지 않는다.

일반 NPC 대화/저장 확인창은 자동으로 진행 이벤트 취급하지 않는다. `ProgressSaveTrigger`의 명시적 완료 이벤트 연결은 유지한다. 보스 승리 자동 저장 요청과 결과 체크포인트 해제는 BattleManager에 통합했다. 실제 파일 기록은 대화·결과창·전투·씬 전환이 모두 끝난 안정된 탐색 경계에서 수행한다. 최종 적용 범위는 [통합 인수인계](2026-10-16-chapter01-foundation.md)를 따른다.

## 회귀 검사와 미검증

- 추가: `ManualSaveRestPolicyTests` 3개 — HP/AP 회복, 일반적만 초기화, 복사본 격리.
- 추가: `SaveManagerRestTransactionTests` 2개 — 실제 원자 저장 성공/실패에 따른 런타임 커밋 여부.
- 추가: `BattleRetryCheckpointTests` 3개 — 등급/종별 독립, 패배 유지/승리 해제, 없는 체크포인트 거부.
- 위 NUnit 테스트는 작성했으며 Unity에서 실행하지 않았다. 통합 CLI 컴파일/검사 결과는 루트 작업의 당일 update에 기록한다.
- 1차 통합 Roslyn 컴파일: 최신 Runtime 소스 404개·참조 393개 오류 0, Editor/테스트 소스 342개·참조 422개 오류 0. Runtime 경고 225개는 Unity 직렬화 필드 CS0649 및 Newtonsoft netstandard 참조 CS1701 중심이며, 상세 로그를 보존했다. Editor 경고 0. 이후 통합 변경의 최종 재검증은 당일 update를 따른다.
- 수동 확인: 일반 적 처치→저장 취소/실패/성공, 방 재방문, 보스에서 아이템 소비 후 패배→재도전, 전용/심리스 조우 재진입, 중복 ID 실패 안내, 이벤트 완료·취소 자동 저장 구분.

## 하네스 교훈

- Windows rg 경로 인수에 `GlobalDataManager*` 같은 glob을 직접 넘기면 Win32 경로 오류가 발생했다. 디렉터리 인수를 주고 `-g 'GlobalDataManager*.cs'`를 사용하면 같은 검색이 정상 동작한다.
- 처음부터 예상 폴더를 추측하기보다 `rg --files Assets/_Game/Scripts | rg '<클래스명>'`으로 실제 소유 경로를 찾는다. SceneActionSequenceTrigger는 Scenario 폴더가 아니라 Overworld/Runtime/Cinematics에 있었다.
- `dotnet`이 PATH에 없고 생성 csproj의 NuGet 산출물이 없어도 SDK는 `C:/Program Files/dotnet`에 설치되어 있었다. `Library/CodexChecks/Chapter01Foundation/Compile.ps1`은 생성 csproj의 define/참조를 읽고 최신 Scripts 소스를 디렉터리에서 수집하여 SDK Roslyn으로 별도 ignored DLL을 만든다. Unity/생성 csproj/ScriptAssemblies는 변경하지 않는다. Editor/비Editor 경로 분리와 외부 ProjectReference의 기존 DLL 존재를 검사하고, Editor는 방금 만든 Runtime DLL을 참조한다. 이것은 컴파일 검사이며 Unity 네이티브 실행·NUnit 실행 검사가 아니다.
