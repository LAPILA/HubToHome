# 오버월드 6인 동행 실험실 Implementation Plan

> **For agentic workers:** REQUIRED: Use superpowers:subagent-driven-development (if subagents available) or superpowers:executing-plans to implement this plan. Steps use checkbox (`- [x]`) syntax for tracking.

**Goal:** 독립 개발 씬과 장애물 Room3개에서 위젤+5명 추종, 관람 대화와 안전 재합류를 바로 시험한다.

**Architecture:** 기존 Room/Party/Dialogue/Camera를 재사용한다. 개발 세션은 임시 파티·저장 차단, 관람 컴포넌트는 지정 동료와 자기 대화/이동, Editor 생성기는 신규 자산만 소유한다. 설정 에셋의 Odin 리더/동료 목록이 편집 진입점이며 본편 DB/스키마/전투는 유지한다.

**Tech Stack:** Unity6/URP2D, C#, DOTween, Odin, 기존 Localization/Room/GlobalData/SaveManager.

승인 설계: `docs/superpowers/specs/2026-10-26-overworld-follower-lab-design.md`. 사용자가 현재 작업공간에서 구현을 요청했고 브랜치gameplayEdit를 유지한다. commit/push 없음. subagent-driven-development/finishing-a-development-branch 스킬은 현재 제공 목록에 없으므로 사용 가능한 executing-plans 절차와 독립 담당/검토를 사용한다. 추가 실행 승인은 이미 받았다.

## Chunk 1: 런타임과 콘텐츠 수직 연결

### Task 1 — 진행 저장 차단 scope

Files: `Assets/_Game/Scripts/Core/Runtime/SaveManager.cs`, 새 `Assets/_Game/Scripts/Core/Runtime/ProgressWriteBlockRegistry.cs`, `Assets/_Game/Scripts/Core/Tests/Editor/ProgressWriteBlockTests.cs` 및 meta.

- [x] 순수 registry 검사: 중첩2개 중1개해제 여전히차단, 중복Dispose 무효, Reset뒤옛lease가새차단을해제하지않음. 실제 구현 소스를 링크해 결과를 확인한다.
- [x] Registry의 `Acquire(): IDisposable`, `IsBlocked`, `Reset()`을 세대별로 구현한다. SaveManager는 `AcquireProgressWriteBlock(): IDisposable`, `AreProgressWritesBlocked`를 노출하고 SubsystemRegistration에서 초기화한다.
- [x] `CanSaveCurrentProgress`, `TrySave`, `TrySaveProgress`, `RequestAutoSave`, `TryDelete`를 차단한다. 자동요청은false반환/예약안함, 직접write/delete는NotAllowed로Storage접근전에실패. 읽기/설정/정상세션은유지한다.
- [x] 생산registry 관리형 검사와 SaveManager 연결검사를 실행한다. 유효요청 차단시 실제 저장소호출0/해제후정상, pending요청이차단뒤나중에실행되지않는지 확인한다.

### Task 2 — 편집 가능한 Lab 정의·임시 파티 수명

Files: 새 `Assets/_Game/Scripts/Overworld/Development/OverworldFollowerLabDefinition.cs`, `OverworldFollowerLabSession.cs`, `Assets/_Game/Scripts/Overworld/Tests/Editor/OverworldFollowerLabSessionTests.cs` 및 meta.

- [x] Definition은 `CharacterData Leader`, `List<CharacterData> Companions`, 초기Room/Area 참조를 제공한다. 리더필수,동료0~5/null거부 검증을 먼저 검사한다. Odin한글목록 추가·삭제·순서변경/InlineEditor/검증안내를 제공한다.
- [x] Session은 실행순서-11000에서 단독새Play와기존Global세션부재를검사한다. GameBootstrap초기화완료후 빈상태Snapshot→저장차단획득→목록으로SaveData작성→FromSaveData→리더바인딩→Configure(leader,5)로시작한다. 실제보이는인원은목록수다.
- [x] GlobalParty객체/각member참조와실행세대를기록한다. 초기실패/disable/destroy에는자기소유일때만빈상태복구,다른로드/새게임에는복구없이관람종료/저장차단해제. 중복dispose/꺼졌다켜짐과Unity fake-null을검사한다.
- [x] Session은Room전환수명보다상위Scene에둔다. 저장지점/보상/자동저장트리거는붙이지않는다. 새GameContentCatalog5개참조는Editor생성태스크가소유한다.
- [x] 관리형/Editor단위검사에서1·3·6명,빈리더/null/초과,임시파티복구,다른파티미덮어쓰기,부분초기화실패,scope해제를검증한다.

### Task 3 — 재사용 관람/대화/합류

Files: 새 `Assets/_Game/Scripts/Overworld/Runtime/Party/OverworldFollowerObservation.cs`, `OverworldFollowerObservationInteraction.cs`, 필요 시 같은 폴더의 순수 상태/경로 보조 파일, `Assets/_Game/Scripts/Overworld/Tests/Editor/OverworldFollowerObservationTests.cs` 및 meta.

- [x] 기존Follower/Dialogue/Input/Room공개API를읽고 소유권/선택취소/경로실패검사를먼저작성한다. Inspector참조필드는slot1..5,optionalMissing=true,출발영역,관람경유점/복귀경유점,DialogueData,localizedprompt/choice/failure키,이동시간,장애물LayerMask를둔다.
- [x] 관람 시작은Room활성/도착reset후 해당Follower가출발영역을통과하면1회 실행한다. 이전/현재선분을사용해빠른이동에서도놓치지않는다. slot미존재시해당관람만휴면. Actor현재점→관람경로를캐시한리더몸체크기로검증하고TryBeginMove(rejoin:false)를호출한다.
- [x] 프록시는Room소유IInteractable이며Hold중Actor실제좌표에위치한다. 이동중OFF. 기존DialogueData정상완료/닫힘→TryStartLocalizedChoicePrompt. 같이가자만복귀,취소/계속보기는Hold. 문구/화자하드코딩없음.
- [x] 합류경로는현재Actor→작성복귀경유점→리더현재위치. 마지막구간도충돌검사. 자기입력/상태scope로리더를고정하고기존TryBeginMove의rejoin을사용한다. 완료/실패/취소에자기대화/command만정리하며다른상태를덮지않는다.
- [x] 출발/복귀중단후현재위치프록시재활성,Room상실/다른파티/다른이동명령은종료. 동료1명합류후방이동과대화종료프레임방해제를회귀검사한다. 매프레임전역탐색/LINQ/새배열금지.
- [x] 재사용컴포넌트에는Lab/맵이름분기를넣지않는다. 좁은새조회API가필요하면Follower기존수명을유지하고해당회귀검사를추가한다.

### Task 4 — 안전한 Editor 생성과 실제 신규 자산

Files: 새 `Assets/_Game/Scripts/Overworld/Editor/FollowerLab/OverworldFollowerLabBuilder.cs` 및 필요시 `...DataBuilder.cs`/`...RoomBuilder.cs`, `Content/Maps/Development/Regions/FollowerLab/**`, `Assets/Resources/LocalizationTable.csv`, 기존GameContentCatalog(신규5참조만), `Assets/_Game/Scripts/Overworld/Tests/Editor/OverworldFollowerLabIntegrationTests.cs`.

- [x] 새경로존재/dirty대상거부와명시저장/활성Scene복구가있는생성기를작성한다. 상시메뉴추가/전체SaveAssets/BuildSettings수정없음. 기존본편/개발Room은참조만한다.
- [x] 새CharacterData5개를위젤원본복제하여ID/색/표시명과필드표시원본(Player_Base)만변경한다. Definition에리더+동료5목록을연결하고기존카탈로그중복검사후추가한다. 모든새파일meta32hex유일성확인.
- [x] Room1은상자섬/ㄱS U통로,Room2는벤치/전시물/2관람지점,Room3는기둥/순환/갈림길을배치한다. 기본타일Point/32PPU·발피벗Y정렬·Collider분리. 통로최소폭은실제리더Collider보다넓게한다.
- [x] Scene에기존GameBootstrap/GameplayCameraRig/Player/RoomContainer/MapTransitionService/RegionEntryCoordinator와LabSession을연결한다. 기본Room1/6인으로Play가능하게한다. 1↔2↔3,3↔1문/입구ID/도착방향/카메라Bounds를연결한다.
- [x] DialogueData/화자/반복안내·선택/실패키를한영일중으로연결한다. 번역표기존BOM/모든기존키보존. 큰디버그HUD없음.
- [x] 에디터실행상태를조회하고사용자dirty씬을저장하지않는경로로명시생성기를실행한다. 실제3Room/Scene/DB/대화참조와Editor검사를확인한다. Unity연결불가시새생성기를제공하는것만으로‘테스트맵완성’이라표현하지않고남은상태를보고한다.

### Task 5 — 통합 검증·인수 문서

- [x] `pwsh -NoProfile -File Library/CodexChecks/Chapter01Foundation/Compile.ps1 -Target All` → Runtime/Editor오류0. 기존경고와신규경고구분.
- [x] `pwsh -NoProfile -File Library/CodexChecks/PartyFollow/Trail/Check.ps1`, `Controller/Check.ps1`, `Sequence/Check.ps1` → 기존회귀유지. 새registry/session/observation의관리형실제코드검사도실행한다.
- [x] 신규자산명/ID/Room왕복/Spawn/카탈로그/언어/meta/UTF8/소유diff검사. 동료삭제로slot4/5누락시정상,출발·복귀경로가실제장애물에막히지않는지확인한다.
- [x] 실제UnityPlay에서6인행렬/L자경로/Room전환/두명관람/개별합류/입력복구/세션해제를확인했다. 자산계약을포함한Native2검사통과. 일반대화와선택입력전체·급반전수동플레이·GPU화면·모바일FPS는미검증으로남긴다. 무할당코드검사와실제프레임측정을혼동하지않는다.
- [x] `docs/overworld-party-follow-guide.md`, `AIAssets/yjlim/feedback/2026-10-26-overworld-follower-lab.md`, 당일기록/index/briefing/todo,관련overworld/core규칙을실제변경범위로갱신한다. 구현결과에따라설계/체크리스트도갱신한다.
- [x] 최종별도검토에서동료추종/파티소유/저장누출/취소/직렬화/반복프레임비용을점검하고수정한다. 커밋·푸시하지않는다.

## 실행 결과와 설계 조정

- 실제 자산 생성 및 카탈로그 최종 등록 완료. 생성기와 관련 복구 메서드는 기존 자산을 재생성하지 않는다.
- 저장7사례40단언, 세션16사례71단언, 관람24사례87단언 관리형 검사 통과. Runtime/Editor 컴파일 오류0.
- Unity 실험실 통합을 포함한 신규 NUnit42/42통과. 결과는 Library/CodexChecks/FollowerLab-verified.xml 및 최종 인수 기록에 보관한다.
- GlobalDataManager 이벤트 확장 대신 세션 내부 최대6개 참조 비교를 사용한다. 타파티는 다음Update에서 감지하며 감지 전 저장은 차단한다.
- TDD 순서 자체를 완료 근거로 삼지 않는다. 기록한 결과는 실행한 생산 코드 검사·컴파일·Unity 결과로 한정한다.
- 남은 수동 검수: 전 출입구를 직접 걸어서 왕복, Z/X 대화 입력 감각, 시각적 색/가독성, Domain Reload 비활성 에디터 반복 Play, 모바일 실기기 프로파일링.

