# AudioManager CS0246: 잘못된 GUID 수정

## 확인된 원인

`AudioManager.cs(65,22)`의 `CombatHitAudioPolicy`는 파일과 클래스가 존재하지만 Unity 컴파일 대상에서는 빠져 있었다. `Editor.log`1499–1500에 해당 `.meta`의 GUID가 무효라 대응 스크립트를 무시했다는 기록이 있다. GUID 길이를 코드로 측정하면32가 아니라33이다. namespace/using/asmdef 문제는 아니었다.

전수 검사 결과 `_Game` 메타1,550개 중 아래 두 파일만 같은 문제였다.

- `Assets/_Game/Scripts/Core/Runtime/CombatHitAudioPolicy.cs.meta`
- `Assets/_Game/Scripts/UI/Tests/Editor/BattleImpactHudTests.cs.meta`

두 GUID의 끝 한 글자를 제거해32자리로 교정했다. 기존 유효 GUID와 충돌하지 않으며 잘못된 원문 GUID는 해당meta 외 직렬화 자산 참조에 사용되지 않았다. 두 타입은 일반 C# 정책/테스트 클래스다. 음향 처리·씬·Prefab·패키지·어셈블리 구성은 바꾸지 않았다.

## 검증

- 실제 파일을 읽는 메타 검사: 수정 전 위2개에서 실패, 수정 후1,550개 전체32자리 소문자hex/유일성/UTF8 검사 통과.
- 명중음 생산 정책236관리형 단언 통과.
- 전체 CLI Runtime444/Editor368소스 오류0, 경고225/0.
- 수정 뒤 실제 Unity `Assembly-CSharp.rsp`에도 `CombatHitAudioPolicy.cs`가 포함된 것을 읽기 전용 확인했다. Editor.log에는 이후 어셈블리 후처리 진행이 기록됐다. 자동 감지는 있었으나 강제 Refresh/Reimport/Play/씬 저장을 호출하지 않았다.
- `git diff --check` 통과. 커밋/푸시 없음.

## 하네스 교훈 · 2026-10-23

실패한 검증은 새 `.cs/.meta` 존재 여부와 Roslyn CLI 컴파일만 확인한 것이다. Roslyn은 Unity `.meta`를 해석하지 않으므로 Unity가 파일을 무시하는 상태에서도 통과할 수 있다. 성공한 절차는 Editor.log의 실제 오류 → Unity `.rsp` 누락 확인 → GUID 길이/문자/중복 검사 → 메타만 교정 → 검사 재통과/실제 `.rsp` 포함 확인이다. GUID는 육안으로 길이를 판단하지 않는다.

재사용 검사: `Library/CodexChecks/CombatHitAudio/CheckMeta.ps1`(로컬 추적 제외), `Check.ps1`, `Chapter01Foundation/Compile.ps1`. 컴파일 하네스가 있더라도 메타 검사를 별도로 선행해야 한다.

PowerShell 검사 구현 시 파일경로는 `-LiteralPath` 또는 `[IO.File]::ReadAllBytes`를 사용한다. `[GameBootstrap]` 같은 파일명은 `-Path`에서 와일드카드로 해석된다. 또한 `$matches`는 `-match/-notmatch`가 덮는 자동 변수이므로 정규식 결과를 담는 로컬 변수에는 `$taskGuidMatches`처럼 별도 이름을 쓴다. 검사 스크립트의 오류와 실제 생산 자산의 실패를 구분한다.
