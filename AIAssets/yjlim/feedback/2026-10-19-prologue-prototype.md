# 임시 프롤로그 · 새 게임부터 백색체 공개까지

## 시작하는 곳

`Assets/_Game/Content/Maps/Development/Regions/Title/00_TitleScene.unity`에서 새 게임을 선택한다. `01_IntroScene` 직접 시작도 기존 GameBootstrap 프리팹을 연결해 지원한다. `Region_Train`만 직접 열면 오프닝 완료 플래그가 없으므로 프롤로그 사건은 시작하지 않는다.

빌드의 첫 씬은 타이틀, 새 게임의 다음 씬은 기존 IntroScene이다. 열차 씬 Region_Train을 빌드 목록에 추가했으며 다른 씬은 제거하지 않았다.

## 현재 재생 순서

최신 대본은 사용자가 제공한 문구를 그대로 반영했다. '소문들', '새로운 에너지', 끊긴 구조 신호의 직접 인용, 돌아오지 않은 첫 원정대, '얼어붙은 북쪽' 표현을 유지한다. 수정 포인트 해설은 게임 본문에 넣지 않았다. 40px/2줄 배치를 위해 각 컷을4노드로 나눴으며 문장 자체는 바꾸지 않았다. 중앙 자막은 **그리고... 현재.**다. 창가/사고 독백과 재생 시간·전체 화면·음성 설정은 그대로다.

1. 국가 연결 그림: 두 나라가 오랫동안 철도로 연결되어 사람·물건·소식을 주고받았다는 내레이션.
2. B제국 그림: 새로운 에너지와 급격한 과학 발전, 활발한 교류.
3. 편지 그림: “긴급 구조를 요청한다. 이 신호를 듣는다면…”이라는 끊긴 구조 요청, 이후 연락 두절.
4. 열차 그림: 1차 원정대가 일 년째 돌아오지 않음 → 2차 원정대가 같은 철길을 따라 출발. 그림이 사라진 검정 화면 중앙에 **그리고... 현재.**를 초당4글자로 표시한다. 1.4초 페이드인과 함께 진행하며 전체 표시 후 최소1.2초 유지하고1초 페이드아웃 후 자동 진행한다. 전체 표시 시간은 기존 최소3.8초와 타이핑+유지 시간 중 긴 쪽이다.
5. 창가 애니메이션 반복: 위젤이 창밖 안개와 이전 원정대를 생각하는 독백.
6. 암전 중 Region_Train / all.trainq0 / default에 도착, 객차 탐색.
7. 공개 후 **실제 탐색 가능 시간 3초**가 지나면 큰 흔들림·어둠·점멸 → 위젤의 후미 쪽 소리 독백.
8. 방 기준 오른쪽 X6.5에 진입하면 암전 → Prologue_going 2.4초 1회 → Monster 그림 1.25초 페이드인.
9. Monster 그림은 Z/패드 확인까지 유지한다. 확인 후 객차 탐색으로 돌아가는 임시 종점이다. 전투·탈선은 아직 연결하지 않았다.

B제국은 사용자가 지정한 임시 국가명이다. 별도 정치/적대 설정을 추가하지 않았다. 본문은40px(기존20px의2배)·중앙 정렬이며, 560×128 영역 안에서2줄·줄당13자 이내로 나눴다. 다른 NPC/전투 대화의 공용 글꼴 크기는 변경하지 않았다. 기존 대화처럼 Z는 타이핑 완료/진행, X는 선형 대사 묶음 건너뛰기다. 이름 입력은 더 이상 호출하지 않고 주인공 표시 이름은 위젤로 고정했다. 과거 저장의 playerName 필드와 NameInputUI 타입은 데이터 호환을 위해 남긴다.

## 내용 바꾸는 곳

콘텐츠 루트: `Assets/_Game/Content/Maps/Regions/ALL/Data/Prologue`

| 대상 | 수정 위치 |
|---|---|
| 4컷 순서·창가 반복·그림 페이드 | Prologue_Opening.asset → 페이지 |
| 후미 이동 1회·Monster 공개 | Prologue_RearReveal.asset → 페이지 |
| 본문·화자·번역키 | Dialogue 폴더의 기존 DialogueData 형식 에셋6개 |
| 3초 사건 켜기/끄기·시간·후미 진입 X | Room_ALL_TrainQ0.prefab 루트 → PrologueTrainSequence |
| 다음 씬·방·스폰·입장 페이드 | 01_IntroScene → IntroManger → IntroManager |
| 그림 중심/본문 영역 | IllustratedStoryPlayer.CreateView / DialogueUI.CreateNarrationCopy |
| 주행·정전·발걸음 음원/음량/페이드 | Prologue_TrainAudio.asset |
| 주행음 시작/걷기 페이지 | IntroManager → 주행음 시작 페이지5 / PrologueTrainSequence → 발걸음 페이지0 |
| 초반 본문 고정 속도 | Prologue_Opening.asset → 내레이션 초당 글자 수10 |
| 현재 중앙 자막 속도/여유 시간 | Prologue_Opening.asset →4번 페이지 → 중앙 자막 초당 글자 수4 / 완성 후 유지1.2초 |
| 주요 컷신 일반 대사 고정 속도 | DialogueUI → 대사 속도 → 시네마틱 초당 글자 수14 |

페이지의 **정지 그림** 또는 **스프라이트 애니메이션**을 연결한다. 애니메이션은 둘 다 있을 때 우선한다. 원본 캔버스는 Aseprite의 실제 크기(현재128×96)다. 초반4컷은 픽셀 확대3배·위쪽 정렬을 유지한다. 창가 반복 장면도 **전체 화면**을 끄고3배(384×288), 화면 중심보다60px 위에 표시한다. 후미 이동/Monster2개 페이지만 **전체 화면**을 켜640×480을 채운다(원본5배). 이동하는 배경 때문에 프레임마다 Sprite 폭/pivot이 달라져도 같은 원본 캔버스와 배율을 유지한다. Image에 Animator만 붙이는 방식은 사용하지 않는다.

### 대사 소리·글자 효과 조절

- 프롤로그 본문은 초당10글자 고정이다. `DialogueStyle.Cinematic` 또는 원래 `GameState.Cutscene`에서 시작한 일반 대화도 초당14글자로 고정한다. 설정창의 TextSpeed와2.5배 증폭 경로를 거치지 않는다. 일반 NPC/메뉴 확인창은 기존 설정 속도를 유지한다. Z즉시표시·X선형 스킵은 설정과 별개인 직접 입력이므로 유지한다.
- `DialogueUI`는 중요 대화용 `TypingDelaysByCharacter`를 패널별로 한 번 만들어 재사용한다. 일반 글자 간격은 `1 / 초당 글자 수`, 중간 구두점0.2초/긴 구두점0.4초이며 연속 구두점의 마지막만 길게 기다린다. 공유 원본 설정은 수정하지 않고 일반 대화/패널 종료에는 원래 참조로 복구한다. 선택지의 다음 대화도 매 노드 정책을 재설정한다.
- 패키지의 ShowText가 배율을1로 초기화하므로, 본문을 넣은 즉시 속도를 다시 적용한다. 기존1·2프레임 지연 재적용 코루틴은 제거했다. 외부 플러그인 소스는 수정하지 않았다.
- 기본 내레이션: DialogueCanvas.prefab의 DialogueUI 기본 피치0.85 / 기본 음량0.25. 기존 SFX 믹서와 설정 음량을 그대로 따른다.
- 초기4컷: Prologue_Opening.asset의 각 페이지 **글자 소리 끄기**를 켰다. 타입라이터는 유지하고 글자 효과음만0으로 처리한다. 로컬 대화 패널에 페이지별로 적용하므로 창가 위젤 대사부터는 기존 화자 음량으로 복구하며, 공용 화자/프리팹 음량은 바꾸지 않는다.
- 위젤: Speaker_Wizzel.asset의 VoicePitch0.9 / VoiceVolume0.28. 기존 기본 blip을 사용하며 새 음원을 만들지 않았다. 다른 화자는 VoiceVolume 기본1로 기존 음량을 유지한다.
- Text Animator 공유 설정의 기본 `incr` 효과가 글자 크기를 변화시켰다. 공유 설정이 localSettings보다 우선하므로 이전 SetAppearancesActive(false)가 실효가 없었다. DialogueTextAnimationPolicy는 실제 사용 설정을 로컬 복사한 뒤 등장/퇴장 효과와 기본태그를 끈다. 타이핑·음성 이벤트와 Behaviors는 유지하며 외부 플러그인/공유 에셋 원본은 수정하지 않는다.

초기화된 Text Animator도 ForceDatabaseRefresh → SetText(Refresh) → RefreshSettings 경로에서 새 로컬 설정 참조를 적용함을 현재 패키지 소스/Core DLL 읽기로 확인했다. 단순 Inspector 값 교체만 한 것이 아니며 원본 공유 설정은 보존한다. 실제 Unity 화면·청음 확인은 별도다.

### 열차 효과음

공통 조절 자산은 `Data/Prologue/Prologue_TrainAudio.asset`이다. IntroScene과 Q0 방이 같은 프로필을 참조한다. 원본 음원은 `Content/Audio/PIXABAY/SFX`에 그대로 둔다.

| 음원 | 재생 구간 | 기본 음량 |
|---|---|---|
| Sample/Two empire.wav | 초기4컷 설명. 첫 페이지0.8초fade-in, 현재 자막 시작에1.2초fade-out | 0.8 |
| freesound_community-inside-the-old-train-66350 | 창가부터 반복. 인트로→객차 씬에서도 재생 위치 유지. 후미 걷기 시작부터1.8초 페이드아웃 | 0.85 |
| faespencer-power-down-486988 | 임시 사고의 화면 점멸 시작에1회 | 0.22 |
| universfield-heavy-walking-footsteps-352771 | 후미 걷기 애니메이션의2.4초 동안1회, 마지막0.12초 페이드아웃 | 0.35 |

주행음 fade-in은0.6초다. Two empire는53.333초/평균-27.7dBFS이며 초반 내레이션 BGM으로 반복한다. 파워다운 원본은8.064초/평균-10.4dBFS, 주행음은16.344초/평균-30.5dBFS로 원본 크기 차이가 커 각각 다르게 낮췄다. 발걸음은5.112초짜리 연속 녹음이며 매 걸음마다 전체 파일을 겹쳐 재생하지 않는다. 현재2.4초 구간에는 약3발이 들어간다. 수치는 파일 분석 기반 시작값이며 실제 게임 청음으로 최종 조정해야 한다.

초반 BGM은 IntroManager가 기존 AudioManager의 BgmPlaybackSnapshot/요청 버전으로 소유한다. 직접 Play에서도 MapSettings.Start가 끝난 뒤 시작하고, 종료 페이지4에서 정상 fade-out한다. 그 전 실패/취소만 동일 매니저·요청 버전·음원을 소유한 경우 이전 음악을 복원한다. 다른 맵이나 연출의 더 최신 요청은 건드리지 않는다.

`PrologueTrainAudioSession`은 기존 AudioManager 아래 전용2D AudioSource2개만 사용한다. 기존 SFX 믹서 그룹에 연결하며 공용 BGM/환경음/대사 소스를 교체하거나 효과음 설정 음량을 중복 곱하지 않는다. 맵 진입의 MapSettings가 공용 환경음을 정리해도 이 주행음은 유지된다. 초기 삽화 중 음원을 미리 준비하고, 일시정지/설정에서는 연출음·페이드 시간을 멈춘다(주행음 자체는 음악처럼 유지).

사고 취소는 자기 효과음만 정리한다. 후미 연출 실패는 발걸음을 정리하고 주행음을 복구한다. 방 비활성화/타이틀 등 허용 씬 이탈/AudioManager 파괴에는 소유한 소스와 Tween만 정리한다. 새 저장 필드·전역 싱글턴·공용 오디오 API 변경은 없다.

3초 자동 시작을 끄면 사고가 자동 진행되지 않는다. 향후 실제 NPC/시나리오 이벤트로 대체할 때 `prologue.train.incident.completed`를 성공 시 설정하면 기존 후미 진입 흐름을 이어 쓸 수 있다. 현재 Q0의 오른쪽에는 문 이동 컴포넌트가 없으므로 기존 사용자 삭제 상태를 복구하지 않았다.

대사는 PROLOGUE_* 키21개, 중앙 자막 키1개와 한국어 기본 문자열로 작성했다. 한영일중 번역은 이번에 작성하지 않았으며 기존 현지화 기본 문자열 대체를 사용한다. 기존 번역 CSV를 덮어쓰지 않았다.

## 코드 소유권

- IntroManager: 오프닝 시작·고정 이름·열차 도착 데이터 설정. 실패 시 도착 정보 롤백. 원본 DialogueData를 실행 중 수정하지 않는다.
- IllustratedStoryAsset/Player: 삽화 표시 전용 Presentation. 별도 전투/대화/시나리오 시스템이 아니다. 기존 대화 매니저의 타입라이터·음성·입력·번역을 재사용한다.
- DialogueManager.TryStartDialogueOnPanel / DialogueUI.CreateNarrationCopy: 기존 시네마틱 패널을 로컬 복제하고 기존 필드/전투 패널 배치는 보존한다. 복제본은 상위 스토리 Canvas만 사용하여 공유 Canvas 정렬을 바꾸지 않는다.
- PrologueTrainSequence: 임시 지연과 위치 진입만 소유한다. 실제 Light2D·TimeScale을 변경하지 않는다. 표시 전용 어둠을 DOTween으로 제어하고 카메라 흔들림은 기존 Cinemachine API/강도 설정을 따른다.
- 진행 플래그: prologue.opening.completed / prologue.train.incident.completed / prologue.train.reveal.completed. 기존 GlobalDataManager 플래그에 포함된다. 새 저장 스키마나 별도 저장 파일은 없다. 이번 임시 연결이 자동 저장을 추가하지는 않는다.

그림은 640×480 FixedViewport Overlay800으로 표시하며 광원/볼륨 영향을 받지 않는다. 열차 어둠700은 스토리/기존 대사 아래다. Monster Importer의 Filter Mode만 Point로 바꾸고 원본 그림·Aseprite·클립은 수정하지 않았다. 숨김 SpriteRenderer와 SampleAnimation은 제거했다. 추가 카메라/RenderTexture/셰이더가 없다.

### 프레임 캐시와 오류 수정

사용자가 최초 창가 재생에서 `illustrated_story_animation_sprite_missing`을 보고했다. 클립 참조/첫 키/바인딩 경로는 정상이나 이전 재생 대상은 Animator가 없는 SpriteRenderer뿐이었다. 원본 두 클립은 비 Legacy ObjectReferenceKeyframe 애니메이션이며, [Unity6.3 AnimationClip 문서](https://docs.unity3d.com/6000.3/Documentation/ScriptReference/AnimationClip.html)는 해당 키의 Animator 재생 지원을 명시한다. 엔진을 실행해 원인을 변수별 재현하지는 않았으므로 API 조건을 확정 원인으로 단언하지 않는다.

프로젝트의 기존 자원 UI와 같은 사전 프레임 추출 방식으로 바꿨다. `IllustratedStoryPage.Frames`에 원본 Sprite/시작 시각을 저장하고 `ResolveAnimationSprite`가 시간별 프레임을 선택한다. start5키/0.5초 반복, going8키/2.4초 단발이다. 동일 마지막 Sprite 유지 키는 압축하지만 총길이는 원본 클립 길이를 유지한다. UI 프레임의 pivot·원본 캔버스 마스크 계산은 유지한다.

원본 클립을 바꾸면 Story 에셋의 **원본 클립에서 UI 프레임 갱신** 버튼으로 캐시를 갱신한다. 클립 참조 변경/누락 캐시는 Editor 지연 갱신하며 자동 저장하지 않는다. 명시적 버튼만 Undo와 저장을 수행한다. 추출 전체가 성공한 다음에만 캐시를 일괄 교체한다. 런타임은 Editor API/원본 곡선 조회/숨김 오브젝트를 사용하지 않는다.

## 취소와 복구

대사 세대 ID, 스토리 재생 세대, 소유 Tween/Overlay를 각각 추적한다. 룸 비활성화·중단 시 자기 재생만 취소하고 파괴된 Unity 객체를 검사한다. 이미 다른 소유자가 게임 상태를 바꿨다면 덮어쓰지 않는다. 완료 플래그는 성공 때만 기록한다. 후미 연출 실패는 구역 안에서도1초 후 재시도할 수 있다. 설정/일시정지 동안 애니메이션·점멸·대기 시간은 진행하지 않는다.

## 검증과 남은 수동 확인

- Runtime418소스/393참조 오류0·경고222, Editor354소스/422참조 오류0·경고0로 CLI 컴파일. 음향 프로필 직렬화 참조2개는 Inspector에서 연결되며 CS0649 경고2개가 추가됐다.
- PrologueContentTests31사례, DialogueTypingTimingTests5사례, DialogueTextAnimationPolicyTests6사례 및 이름 입력 호환 검사2사례. 고정 속도·일반 대화 복구·원본 타이밍 보존, 중앙 자막 진행/일시정지/유지 시간·수치 검증과 기존 콘텐츠 연결을 포함한다. 테스트는 **소스/컴파일 확인만** 수행했으며 NUnit 엔진 실행 결과가 아니다.
- PNG4컷은 실제108×68 슬라이스/fileID 순서를 확인했다. 현재 Import Artifact를 binary2text로 확인한 원본 클립은 start0.5초 반복, going2.4초 반복이며 둘 다 SpriteRenderer.m_Sprite/path="" 바인딩이다. going은 플레이어에서 반복을 제한한다.
- Unity Play/Refresh/Reimport/열린 씬 저장은 실행하지 않았다. 실제 글꼴 줄바꿈, UI 프레임 재생, 모바일 안전 영역, 흔들림 체감은 사용자가 새 게임으로 확인해야 한다. 성능 측정이나 화면 완벽 동작을 확정하지 않는다.
- 브랜치 gameplayEdit. 커밋/푸시 없음. 기존 사용자 타일·상자·콜라이더·Player Order3 변경과 삭제 자산을 보존했다.

## 하네스 교훈

2026-10-19 / Text Animator 속도: ShowText 전에 SetTypewriterSpeed만 적용하거나1·2프레임 뒤 재적용하면 최초 표시와 이후 속도가 달라질 수 있다. 현재 패키지 소스·DLL에서 ShowText의 startup배율1복원을 확인했다. 중요 연출은 원본 timing을 수정하지 않는 패널별 고정글자간격과 배율1을 쓰고 ShowText 직후 즉시 적용한다. 일반 대화 복귀·선택지 분기·복사본 정리를 회귀소스로 검사하고 CLI컴파일했다. 엔진재생·설정최고속도에서실제화면확인은미실시다. 다른패키지버전은같은초기화경로인지먼저확인한다.

2026-10-19 / Text Animator 설정: localSettings를 바꾸는 API 호출만 확인하면 sharedSettings 우선순위를 놓칠 수 있다. 실제 Settings 공급 경로와 이미초기화된 RuntimeAnimator의캐시갱신까지 따라가야 한다. 이번에는 공유설정의 'incr'를 확인하고 로컬복사/공유분리/RefreshSettings재적용 경로를 수정했다. 소스·DLL·회귀소스·CLI컴파일을 확인했으며 실제 엔진 화면은 미검증이다. 외부설정원본을변경해다른사용처를함께바꾸는방법은피한다.

2026-10-19 / Aseprite UI 재생: 이전 접근은 클립의 m_Sprite/path/fileID가 정상이고 CLI 컴파일이 된다는 근거로 Animator 없는 GO의 런타임 SampleAnimation까지 가능하다고 판단했다. 사용자 실행에서 첫Sprite null 오류가 났다. 바인딩 자료는 대상 경로 검증이지 네이티브 런타임 재생 검증이 아니다. 기존프로젝트의 Sprite/Time 캐시로 API의존을 제거했고 원본키참조·시간·반복/단발경계 테스트소스·CLI 컴파일을 확인했다. 실제 엔진재생 성공은 아직 미검증이다. 향후 같은조건에서는 프레임캐시또는실제Animator기반재생을 선택하고, 정적바인딩확인을실행검증으로표현하지않는다. 원본클립·코드·회귀검사위치는 이 문서 위 항목과 PrologueContentTests에 있다.

2026-10-19 / 로컬 패치: 한 apply_patch 안에서 동일 파일 Delete+Add 조합은 이 환경에서 `multiple operations target`으로 거절됐다. 전체 교체가 필요해도 해당 파일을 명시적인 Update 패치로 처리하면 성공한다. 검증은 결과 원문·컴파일·diff 확인으로 한다. Windows rg는 경로 인자에 `SceneLoader*` 같은 와일드카드를 넣으면 OS123 오류가 나므로 디렉터리와 `-g 'SceneLoader*.cs'`를 분리한다. 글로벌 yjlim 메모리 경로가 이 장치에 없어 다른 사용자 프로필을 만들지 않고 이 프로젝트 기록에 남겼다.
