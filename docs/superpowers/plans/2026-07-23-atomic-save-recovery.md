# Atomic Save Recovery Implementation Plan

> **For agentic workers:** REQUIRED: Use superpowers:subagent-driven-development (if subagents available) or superpowers:executing-plans to implement this plan. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 기존 저장 API를 유지하면서 스키마 버전, 검증된 원자 교체, 직전 정상 백업 복구, Editor 슬롯 진단 도구를 추가한다.

**Architecture:** `SaveDataCodec`가 버전 판정·마이그레이션·정규화를 소유하고, `AtomicSaveStorage`가 파일 후보와 교체 순서를 소유한다. `SaveManager`는 기존 호출 호환 Facade로 남으며 Editor 창은 공개 검사 결과만 읽는다.

**Tech Stack:** Unity 6, C#, Newtonsoft.Json/JObject, System.IO durable flush/File.Replace, NUnit EditMode tests, Unity Editor IMGUI

**Design:** `docs/superpowers/specs/2026-07-23-atomic-save-recovery-design.md`

---

## 파일 구조

### 새 파일

- `Assets/_Game/Scripts/Core/Runtime/SaveDataCodec.cs`
  - 현재 스키마 버전, Decode 결과, v0 마이그레이션, 누락 필드 정규화를 소유한다.
- `Assets/_Game/Scripts/Core/Runtime/AtomicSaveStorage.cs`
  - 슬롯 경로, 파일 시스템 경계, 원자 저장, 후보별 로드, 검사 결과를 소유한다.
- `Assets/_Game/Scripts/Core/Editor/SaveDiagnosticsWindow.cs`
  - 수동 슬롯·자동 슬롯 상태와 명시적 삭제 명령을 제공한다.
- `Assets/_Game/Scripts/Core/Tests/Editor/SaveDataCodecTests.cs`
  - legacy, 미래 버전, 누락 필드와 전체 도메인 왕복을 검증한다.
- `Assets/_Game/Scripts/Core/Tests/Editor/AtomicSaveStorageTests.cs`
  - 첫 저장, 백업, 손상 복구, 임시 파일 복구, 실패 원자성을 검증한다.
- `Assets/_Game/Scripts/Core/Tests/Editor/SaveManagerCompatibilityTests.cs`
  - 기존 Facade의 슬롯 검사·삭제와 상세 결과 연결을 검증한다.
- `AIAssets/yjlim/feedback/2026-07-23-atomic-save-recovery.md`
  - 저장 형식, 복구 순서, 개발 진단창 사용법을 기록한다.

### 수정 파일

- `Assets/_Game/Scripts/Core/Runtime/SaveData.cs`
  - `schemaVersion`을 추가한다.
- `Assets/_Game/Scripts/Core/Runtime/SaveManager.cs`
  - 직접 파일 I/O를 제거하고 `AtomicSaveStorage` Facade가 된다.
- `AIAssets/2026-07-23-update.md`
  - 구현과 검증 결과를 기록한다.
- `RuleFileforAI/core.clinerules`
  - 존재할 경우 저장 파일 직접 쓰기 금지와 공용 저장소 사용 규칙을 추가한다.

---

## Chunk 1: 스키마와 마이그레이션

### Task 1: SaveData 스키마 판정과 legacy 마이그레이션

**Files:**
- Modify: `Assets/_Game/Scripts/Core/Runtime/SaveData.cs`
- Create: `Assets/_Game/Scripts/Core/Runtime/SaveDataCodec.cs`
- Create: `Assets/_Game/Scripts/Core/Tests/Editor/SaveDataCodecTests.cs`

- [ ] **Step 1: 버전 필드 없는 legacy JSON 실패 테스트 작성**

```csharp
[Test]
public void Decode_LegacyJsonWithoutVersion_MigratesToCurrentVersion()
{
    const string json = "{\"currentScene\":\"TestMap\",\"InventoryDict\":{\"potion\":2}}";

    SaveDecodeResult result = new SaveDataCodec().Decode(json);

    Assert.That(result.Success, Is.True);
    Assert.That(result.SourceVersion, Is.Zero);
    Assert.That(result.WasMigrated, Is.True);
    Assert.That(result.Data.schemaVersion, Is.EqualTo(SaveSchema.CurrentVersion));
    Assert.That(result.Data.InventoryDict["potion"], Is.EqualTo(2));
}
```

- [ ] **Step 2: 누락 컬렉션과 미래 버전 실패 테스트 작성**

```csharp
[Test]
public void Decode_MissingCollections_NormalizesDefaults()
{
    SaveDecodeResult result = new SaveDataCodec().Decode(
        "{\"currentScene\":\"TestMap\",\"PartyData\":null,\"InventoryDict\":null}");

    Assert.That(result.Success, Is.True);
    Assert.That(result.Data.PartyData, Is.Not.Null);
    Assert.That(result.Data.InventoryDict, Is.Not.Null);
    Assert.That(result.Data.EncounterMemory, Is.Not.Null);
    Assert.That(result.Data.OverworldEnemies, Is.Not.Null);
}

[Test]
public void Decode_FutureVersion_IsRejectedExplicitly()
{
    string json = "{\"schemaVersion\":" + (SaveSchema.CurrentVersion + 1) + ",\"currentScene\":\"TestMap\"}";

    SaveDecodeResult result = new SaveDataCodec().Decode(json);

    Assert.That(result.Success, Is.False);
    Assert.That(result.Failure, Is.EqualTo(SaveDecodeFailure.UnsupportedFutureVersion));
}
```

- [ ] **Step 3: Core Codec 테스트 실행해 타입 미정의 실패 확인**

Run:

```powershell
Invoke-RestMethod -Uri 'http://127.0.0.1:8090/command' -Method Post -ContentType 'application/json' -Body '{"command":"run_tests","params":{"mode":"EditMode","filter":"SaveDataCodecTests"}}'
```

Expected: `SaveDataCodec`, `SaveSchema`, `schemaVersion` 미정의로 Compile 또는 Test FAIL.

- [ ] **Step 4: 스키마와 Decode 결과 모델 구현**

```csharp
public static class SaveSchema
{
    public const int LegacyVersion = 0;
    public const int CurrentVersion = 1;
}

public enum SaveDecodeFailure
{
    None,
    EmptyContent,
    InvalidJson,
    InvalidRoot,
    UnsupportedVersion,
    UnsupportedFutureVersion
}

public sealed class SaveDecodeResult
{
    public bool Success { get; private set; }
    public SaveData Data { get; private set; }
    public int SourceVersion { get; private set; }
    public bool WasMigrated { get; private set; }
    public SaveDecodeFailure Failure { get; private set; }
    public string Message { get; private set; }
}
```

- [ ] **Step 5: JObject 기반 버전 판정과 v0 → v1 migration 구현**

버전 속성 누락을 field initializer와 혼동하지 않도록 `JObject`에서
`schemaVersion` 토큰을 먼저 읽는다. 버전 0은 현재 `SaveData` 필드 이름을 그대로
Deserialize한 뒤 현재 버전으로 올린다.

- [ ] **Step 6: 누락 필드 정규화 구현**

문자열, `PartyData`, `InventoryDict`, `eventFlags`, `EncounterMemory`,
`OverworldEnemies`, `EquippedSkillIDs`, `SeenBeatIds`를 null이 아닌 값으로 정리한다.
시나리오 없는 적은 Dictionary key를 fallback EnemyId로 유지한다.

- [ ] **Step 7: 전체 도메인 Encode/Decode 왕복 테스트 추가**

Room ID, SpawnPoint, Money, Inventory, Party progression, Encounter Memory,
Overworld Enemy를 한 SaveData에 넣고 값이 보존되는지 확인한다.

- [ ] **Step 8: Codec 테스트 실행**

Expected: `SaveDataCodecTests` 전부 PASS.

- [ ] **Step 9: 독립 커밋**

```bash
git add Assets/_Game/Scripts/Core/Runtime/SaveData.cs Assets/_Game/Scripts/Core/Runtime/SaveDataCodec.cs Assets/_Game/Scripts/Core/Tests/Editor/SaveDataCodecTests.cs
git commit -m "feat: add versioned save data codec"
```

---

## Chunk 2: 원자 저장과 복구

### Task 2: 파일 시스템 경계와 슬롯 경로

**Files:**
- Create: `Assets/_Game/Scripts/Core/Runtime/AtomicSaveStorage.cs`
- Create: `Assets/_Game/Scripts/Core/Tests/Editor/AtomicSaveStorageTests.cs`

- [ ] **Step 1: 슬롯 경로 계약 테스트 작성**

```csharp
[Test]
public void GetPaths_UsesSameDirectoryForAtomicCandidates()
{
    var storage = CreateStorage();

    SaveSlotPaths paths = storage.GetPaths(2);

    Assert.That(paths.PrimaryPath, Does.EndWith("save_slot_2.json"));
    Assert.That(paths.BackupPath, Is.EqualTo(paths.PrimaryPath + ".bak"));
    Assert.That(paths.TemporaryPath, Is.EqualTo(paths.PrimaryPath + ".tmp"));
    Assert.That(paths.CorruptPath, Is.EqualTo(paths.PrimaryPath + ".corrupt"));
}
```

- [ ] **Step 2: 첫 저장과 두 번째 저장 백업 테스트 작성**

```csharp
[Test]
public void Save_SecondCommitKeepsPreviousValidSnapshotAsBackup()
{
    AtomicSaveStorage storage = CreateStorage();
    Assert.That(storage.Save(CreateData("first"), 0).Success, Is.True);
    Assert.That(storage.Save(CreateData("second"), 0).Success, Is.True);

    SaveSlotPaths paths = storage.GetPaths(0);
    Assert.That(Decode(paths.PrimaryPath).Data.playerName, Is.EqualTo("second"));
    Assert.That(Decode(paths.BackupPath).Data.playerName, Is.EqualTo("first"));
    Assert.That(File.Exists(paths.TemporaryPath), Is.False);
}
```

- [ ] **Step 3: 저장 테스트 실행해 타입 미정의 실패 확인**

- [ ] **Step 4: `ISaveFileSystem`과 실제 구현 작성**

실제 쓰기는 `FileStream`과 UTF-8 no BOM `StreamWriter`를 사용하고
writer Flush 후 `FileStream.Flush(true)`를 호출한다. Replace, Move, Delete는
예외를 숨기지 않고 storage 결과로 변환한다.

- [ ] **Step 5: 첫 저장과 정상 본 파일 교체 구현**

`.tmp` 기록 → read-back Decode → 정상 primary가 있으면
`ReplaceFile(temp, primary, backup)` → 없으면 `MoveFile(temp, primary)` 순서를 구현한다.

- [ ] **Step 6: 테스트 실행**

Expected: 경로, 첫 저장, 두 번째 백업 테스트 PASS.

### Task 3: 손상·중단·실패 복구

**Files:**
- Modify: `Assets/_Game/Scripts/Core/Runtime/AtomicSaveStorage.cs`
- Modify: `Assets/_Game/Scripts/Core/Tests/Editor/AtomicSaveStorageTests.cs`

- [ ] **Step 1: 손상 primary에서 backup 로드 테스트 작성**

```csharp
[Test]
public void Load_CorruptPrimary_UsesPreviousValidBackup()
{
    AtomicSaveStorage storage = CreateStorage();
    storage.Save(CreateData("first"), 0);
    storage.Save(CreateData("second"), 0);
    File.WriteAllText(storage.GetPaths(0).PrimaryPath, "{broken");

    SaveLoadResult result = storage.Load(0);

    Assert.That(result.Success, Is.True);
    Assert.That(result.Source, Is.EqualTo(SaveLoadSource.Backup));
    Assert.That(result.Data.playerName, Is.EqualTo("first"));
}
```

- [ ] **Step 2: 첫 저장 중단의 temp 복구 테스트 작성**

본 파일과 백업 없이 `.tmp`에 유효한 current JSON만 둔 뒤
`SaveLoadSource.Temporary`로 읽는지 확인한다.

- [ ] **Step 3: Replace 실패 원자성 테스트 작성**

`FaultInjectingSaveFileSystem`이 Replace에서 예외를 던지게 하고,
저장 결과는 실패하지만 기존 primary가 여전히 Decode되는지 확인한다.

- [ ] **Step 4: 손상 primary 저장 시 backup 보존 테스트 작성**

primary를 손상시키고 backup은 정상으로 둔 상태에서 새 Save를 실행한다.
새 primary가 저장되고 기존 정상 backup 내용이 바뀌지 않으며 손상 primary가
`.corrupt`에 격리되는지 확인한다.

- [ ] **Step 5: 후보별 Decode와 로드 순서 구현**

Primary → Backup → Temporary 순서로 읽고, 실패 후보 메시지를 합친다.
미지원 미래 버전 primary도 backup fallback 대상으로 처리한다.

- [ ] **Step 6: 손상 본 파일 격리와 정상 백업 보존 구현**

primary가 존재하지만 Decode 실패면 기존 `.corrupt`를 제거하고 primary를 이동한다.
backup은 교체하지 않고 temp를 primary로 이동한다.

- [ ] **Step 7: Delete가 관련 파일 전체를 제거하는 테스트와 구현**

- [ ] **Step 8: Atomic storage 테스트 실행**

Expected: 신규 저장·복구 테스트 전부 PASS.

- [ ] **Step 9: 독립 커밋**

```bash
git add Assets/_Game/Scripts/Core/Runtime/AtomicSaveStorage.cs Assets/_Game/Scripts/Core/Tests/Editor/AtomicSaveStorageTests.cs
git commit -m "feat: add atomic save storage recovery"
```

---

## Chunk 3: 기존 API 연결과 개발 도구

### Task 4: SaveManager 호환 Facade

**Files:**
- Modify: `Assets/_Game/Scripts/Core/Runtime/SaveManager.cs`
- Create: `Assets/_Game/Scripts/Core/Tests/Editor/SaveManagerCompatibilityTests.cs`

- [ ] **Step 1: null·음수 슬롯 실패 계약 테스트 작성**

상세 API가 예외 대신 false와 오류 메시지를 반환하고 기존 void API도 예외를 던지지 않는지 확인한다.

- [ ] **Step 2: 기존 API 보존 Reflection 테스트 작성**

`Save(SaveData,int)`, `Load(int)`, `Delete(int)`, `Exists(int)`, `HasAnySave()`의
public signature가 존재하는지 확인한다.

- [ ] **Step 3: `SaveManager`를 storage Facade로 교체**

- `Save`는 `TrySave`를 호출하고 성공·실패를 로깅한다.
- `Load`는 `TryLoad`를 호출하고 backup/temp 복구 시 Warning을 남긴다.
- `Delete`, `Exists`, `HasAnySave`는 storage를 사용한다.
- `InspectSlot`과 `SaveDirectoryPath`를 Editor 창에 제공한다.

- [ ] **Step 4: 저장 시각과 current schema 설정**

`SaveManager` 진입 시 `saveTime`과 `schemaVersion`을 설정하되 codec도 current schema를 강제한다.

- [ ] **Step 5: SaveManager와 기존 Core 저장 테스트 실행**

Expected: 신규 호환 테스트, `OverworldEnemySaveTests`,
`GlobalDataManagerSaveCompatibilityTests`, `EncounterMemorySaveTests` PASS.

- [ ] **Step 6: 독립 커밋**

```bash
git add Assets/_Game/Scripts/Core/Runtime/SaveManager.cs Assets/_Game/Scripts/Core/Tests/Editor/SaveManagerCompatibilityTests.cs
git commit -m "refactor: route save manager through atomic storage"
```

### Task 5: Save Diagnostics EditorWindow

**Files:**
- Create: `Assets/_Game/Scripts/Core/Editor/SaveDiagnosticsWindow.cs`
- Modify: `Assets/_Game/Scripts/Core/Tests/Editor/SaveManagerCompatibilityTests.cs`

- [ ] **Step 1: 표시 행 생성 순서 테스트 작성**

수동 슬롯 0, 1, 2 다음 자동 슬롯 99가 결정적 순서로 만들어지는 순수 helper를 검증한다.

- [ ] **Step 2: Diagnostics 창 구현**

상단에 `새로 고침`, `저장 폴더 열기`, `전체 초기화`를 둔다.
각 슬롯 행에는 파일 후보 존재 여부, loadable source, schema, save time, 메시지와
`삭제` 버튼을 표시한다.

- [ ] **Step 3: 파괴적 명령 확인과 갱신 구현**

슬롯 삭제와 전체 초기화는 `EditorUtility.DisplayDialog` 확인 뒤 실행한다.
창을 열거나 새로 고칠 때는 파일을 수정하지 않는다.

- [ ] **Step 4: Editor 테스트와 컴파일 확인**

- [ ] **Step 5: 독립 커밋**

```bash
git add Assets/_Game/Scripts/Core/Editor/SaveDiagnosticsWindow.cs Assets/_Game/Scripts/Core/Tests/Editor/SaveManagerCompatibilityTests.cs
git commit -m "feat: add save diagnostics window"
```

---

## Chunk 4: 문서·검증·Jira

### Task 6: 개발 규칙과 인수인계

**Files:**
- Create: `AIAssets/yjlim/feedback/2026-07-23-atomic-save-recovery.md`
- Modify: `AIAssets/2026-07-23-update.md`
- Modify if present: `RuleFileforAI/core.clinerules`

- [ ] **Step 1: 파일 형식과 복구 순서 문서화**

- [ ] **Step 2: Diagnostics 창 사용법과 삭제 주의점 작성**

- [ ] **Step 3: AI 규칙에 직접 `File.WriteAllText` 저장 금지와 공용 storage 사용 원칙 추가**

- [ ] **Step 4: 문서 커밋**

```bash
git add AIAssets/yjlim/feedback/2026-07-23-atomic-save-recovery.md AIAssets/2026-07-23-update.md RuleFileforAI/core.clinerules
git commit -m "docs: document atomic save recovery"
```

### Task 7: 전체 검증과 Jira 인수인계

**Files:**
- No code changes expected

- [ ] **Step 1: Unity Refresh와 Compile 완료 확인**

- [ ] **Step 2: 저장 전용 EditMode 테스트 실행**

Expected: Codec, Atomic storage, SaveManager compatibility 전부 PASS.

- [ ] **Step 3: 관련 회귀 테스트 실행**

Expected: Overworld Enemy, Encounter Memory, Inventory, Reward progression PASS.

- [ ] **Step 4: 전체 EditMode 테스트 실행**

Expected: 기존 755개와 신규 테스트 전부 PASS.

- [ ] **Step 5: Project Content Validation 실행**

Expected: Error 0, 기존 선택 아트 Warning만 허용.

- [ ] **Step 6: first-party Prefab Missing Script 검사**

Expected: Missing Script 0.

- [ ] **Step 7: diff와 사용자 Scene 해시 확인**

```powershell
git diff --check
Get-FileHash -LiteralPath 'Assets/_Game/Content/Maps/Development/TestMap/TestMap.unity' -Algorithm SHA256
git status --short
```

Expected: TestMap SHA256
`D456DEC931BA4C14E101A031B07880391958B0E9B65A84DE1E88F61ED1340164` 유지.

- [ ] **Step 8: HUBTOHOME-62에 구현·복구 시나리오·검증 댓글 작성**

- [ ] **Step 9: HUBTOHOME-62를 `검토 중`으로 전환**

- [ ] **Step 10: 최종 커밋과 작업 트리 확인**

```bash
git log --oneline -10
git status --short --branch
```
