# 저장 데이터 버전·원자적 기록·복구 인수인계

## 구현 결과

- 저장 JSON에 `schemaVersion`을 추가하고 현재 버전을 1로 정의했다.
- 버전 필드가 없는 기존 JSON은 legacy v0으로 판정해 v1로 마이그레이션한다.
- 현재 버전보다 높은 저장은 추측해 읽지 않고 명확히 거절한다.
- 본 파일 직접 덮어쓰기를 제거하고 같은 폴더의 임시 파일을 검증한 뒤 원자 교체한다.
- 두 번째 저장부터 직전 정상본을 `.bak`으로 유지한다.
- 손상된 본 파일은 `.corrupt`로 격리하고 기존 정상 백업을 보존한다.
- 로드는 Primary, Backup, Temporary 순서로 유효한 후보를 선택한다.
- 기존 `SaveManager.Save/Load/Delete/Exists/HasAnySave` 호출은 유지했다.
- `Exists`는 이제 파일 존재가 아니라 실제 로드 가능한 후보가 있는지 판단한다.

## 슬롯 파일

슬롯 0을 예로 들면 저장 폴더에 다음 파일을 사용한다.

- `save_slot_0.json`: 현재 정상본
- `save_slot_0.json.bak`: 직전 정상본
- `save_slot_0.json.tmp`: 기록과 read-back 검증 중인 후보
- `save_slot_0.json.corrupt`: 발견된 손상 본 파일

저장 중 정상 종료되면 `.tmp`는 남지 않는다. 강제 종료로 `.tmp`만 남은 첫 저장은
Primary와 Backup이 없을 때 복구 후보로 읽는다. Backup 또는 Temporary를 읽어도
로드 시 파일을 자동으로 다시 쓰지는 않는다. 다음 정상 Save가 새 Primary를 만든다.

## 저장 대상

Codec 왕복 검증에 포함된 현재 저장 도메인은 다음과 같다.

- Scene, Room ID, SpawnPoint, 위치와 방향
- 파티 ID, 능력치, 레벨, 경험치, 장착 스킬
- 인벤토리, 돈, 이벤트 플래그
- Encounter Memory의 만남 횟수, 처치 여부, 확인한 전투 beat
- Battle Scenario가 없는 오버월드 적의 독립 처치 상태
- 플레이어 이름, 저장 시각, 플레이 시간

`PendingBattleScenario`, 진행 중 QTE와 같은 전투 진입·세션 임시 상태는 저장하지 않는다.

## 개발 진단창

`Hub To Home > Save > Diagnostics`에서 수동 슬롯 1~3과 자동 슬롯을 확인한다.

각 슬롯은 다음 정보를 표시한다.

- 실제 사용 가능한 로드 소스
- Primary, Backup, Temporary의 존재·유효성·스키마 버전
- 손상 격리 파일 존재 여부
- 저장 시각과 복구 원인

`새로 고침`과 창 열기는 읽기 전용이다. `삭제`와 `전체 초기화`는 확인 대화상자 이후
해당 슬롯의 Primary, Backup, Temporary, Corrupt 파일을 함께 삭제한다.

## 개발 규칙

- 런타임 저장 파일을 `File.WriteAllText`로 직접 쓰지 않는다.
- 데이터 형식 변경은 `SaveData`와 `SaveDataCodec` migration을 함께 수정한다.
- 실제 파일 기록과 복구는 `AtomicSaveStorage`를 통한다.
- 저장 후보를 읽을 때 Newtonsoft를 직접 호출하지 않고 Codec 결과를 사용한다.
- 새 영속 상태는 `GlobalDataManager.ToSaveData/FromSaveData` 왕복 테스트를 추가한다.
- 미래 버전을 현재 버전으로 강제 하향하지 않는다.
- 복구 로드는 자동 저장을 유발하지 않는다.

## 검증 결과

- SaveData Codec: 5/5
- Atomic Save Storage: 9/9
- SaveManager 호환 Facade: 3/3
- Save Diagnostics Window: 4/4
- 기존 GlobalData/Overworld Enemy/Encounter Memory 저장 회귀: 11/11
- Inventory 소비와 Battle Reward/Progression 연계: 6/6
- Unity 전체 EditMode: 776/776
- Project Content Validation: 오류 0건, 기존 선택 아트 경고 10건
- `Assets/_Game` Prefab 59개, 하위 오브젝트 738개: Missing Script 0건
- TestMap SHA256:
  `D456DEC931BA4C14E101A031B07880391958B0E9B65A84DE1E88F61ED1340164`

## 이번 범위에서 제외

- 타이틀 이어하기의 실제 Scene 복원 흐름
- 스토리 분기별 세이브 슬롯 정책
- 저장 슬롯 선택·미리보기 UI
- 전투 도중 저장과 QTE 세션 복원
- 클라우드 저장과 플랫폼 동기화
