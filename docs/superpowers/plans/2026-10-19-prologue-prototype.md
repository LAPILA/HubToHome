# 임시 프롤로그 구현 계획

사용자 요청: 새 게임부터 열차 후미의 백색체 이미지 공개까지 연결한다. 이름 입력은 사용하지 않는다. 기존 열차·대사·입력·저장 상태를 재사용하며 전투/탈선은 이번 범위에 포함하지 않는다.

## 구성

1. 타이틀 새 게임 → 기존 IntroScene. 고정 이름 위젤, 4컷 삽화와 내레이션, 현재 시점 창가 애니메이션.
2. Region_Train / all.trainq0 / default로 암전 중 도착 준비. 공개 후 탐색 시작.
3. 임시 3초 타이머 → 흔들림·어둠/점멸 → 위젤 독백. 타이머는 인스펙터에서 해제 가능.
4. 독백 뒤 객차 오른쪽으로 이동 → 암전 → 후미 이동 애니메이션 1회 → Monster 정지 그림 천천히 공개.
5. Monster 화면 확인 후 객차 탐색 복귀. 후속 콘텐츠 연결 전까지의 임시 종료점.

## 소유권과 데이터

- IllustratedStoryAsset/Player는 삽화 표시 Presentation 전용이다. 기존 Action Sequence 문법이나 시나리오 Runtime Asset을 임의 생성하지 않는다.
- 대본은 기존 DialogueData/현지화 키를 사용하고, DialogueManager/DialogueUI의 타이핑·입력·취소를 재사용한다.
- IntroManager는 도착 정보와 시작 흐름만, PrologueTrainSequence는 지연/오른쪽 진입/완료 플래그만 소유한다.
- 그림과 애니메이션은 제공된 원본 참조를 사용한다. Aseprite SpriteRenderer 클립은 숨김 샘플러 → Image로 투영하여 프레임별 크기/pivot을 보존한다.
- UI는 640×480 FixedViewport Overlay. 광원/볼륨 영향 없음. 별도 RenderTexture/상시 카메라 추가 없음.
- 성공 시만 플래그 확정. 취소/룸 비활성화 시 소유 대사·Tween·Overlay 정리 및 소유한 게임 상태만 복원.

## 검증

- 원본 그림 4컷 순서, Aseprite 현재 클립 참조/fileID/길이 확인.
- CLI Runtime/Editor 컴파일, 자산 참조·연결·프레임/재생 정책 정적 검사.
- 테스트 코드 추가. Unity Play/Refresh/Reimport는 실행하지 않으며 실제 시각 검증은 사용자가 수행한다.
- 기존 dirty 파일/사용자 편집·삭제는 보존. 커밋/푸시하지 않는다.

## 완료 기록

구현·직렬화 연결·CLI Runtime/Editor 컴파일·콘텐츠 정적 검사 완료. Unity 재생과 엔진 테스트는 실행하지 않았다. 상세 변경 파일·조절 위치·수동 확인 항목은 AIAssets/yjlim/feedback/2026-10-19-prologue-prototype.md에 기록했다.

## 사용자 후속 개정

- [x] 본문40px·중앙 정렬·2줄 편집, B제국/SOS/원정 내레이션 개정.
- [x] 마지막 삽화 뒤 그림 없는 중앙 자막 페이지를 분리하고 천천히 자동 전환.
- [x] 보고된 최초Sprite누락 경로 제거: 숨김GO+SampleAnimation 대신 원본Sprite/Time캐시를 직접 재생. 기존자원UI의베이크패턴재사용.
- [x] 캐시누락검증·Editor명시갱신·원본클립과캐시비교 및 반복/단발/자막회귀소스 추가.
- [x] Runtime/Editor CLI컴파일·데이터정적검사. 실제UnityPlay/엔진NUnit은 수행하지 않음.

## 표시·음성 후속 요청

- [x] 현재 자막 뒤 창가/후미/Monster3개 페이지만 FullScreen. 앞선4컷과640×480 FixedViewport/정수 배율 정책 보존.
- [x] 기존 소리 재사용, 내레이션0.85/.25·위젤0.9/.28로 피치/음량 감소. 믹서 설정 중복 적용 없음.
- [x] Text Animator 공유 설정 우선순위에 맞게 실제 대사 등장/퇴장 효과 비활성화. 타이핑·음성 보존.
- [x] CLI 컴파일·자산/회귀 소스 검증 및 기록. 실제 Unity 시각·청음 테스트는 수행하지 않음.

## 열차 음향 연결 계획

**목표:** 제공된 주행·전원 종료·발걸음3개 음원을 현재 프롤로그에 연결한다.

**구조:** 기존 AudioManager 아래 프롤로그 전용 세션과2개2D AudioSource를 둔다. 새 전역 싱글턴이나 공용 환경음 교체는 하지 않는다. 같은 주행 소스를 창가→씬 전환→객차 탐색까지 유지한다. IllustratedStoryPlayer의 페이지 시작 알림으로 설정된 창가/후미 걷기 페이지에 연결한다.

**데이터:** 공용 PrologueTrainAudioProfile에 음원/음량/페이드 값을 둔다. IntroManager와 Q0 PrologueTrainSequence에 같은 프로필을 연결한다. 시작값은 주행0.85, 파워다운0.22, 발걸음0.35, 주행 fade-in0.6초/fade-out1.8초, 발걸음 fade-out0.12초다.

- [x] PrologueTrainAudioProfile과 음원 참조 자산 추가. 원본 mp3/Importer는 보존한다.
- [x] 전용 세션 구현: 소스/mixer 재사용 경로, 선행 로드, 중복 시작 방지, 씬 범위 이탈·취소 정리, 연출음/페이드 일시정지.
- [x] 창가 페이지 시작에 주행음 시작, 열차 이동 성공에는 세션을 넘기고 실패/취소에는 종료. 목적 씬 활성화 뒤 준비 실패/시간초과도 음향은 종료한다.
- [x] 사고 시작에 파워다운1회. 후미 걷기 시작에 주행 fade-out + 발걸음1회, 원본 애니메이션 길이 후 짧게 종료. 실패에는 주행 복구.
- [x] 콘텐츠 회귀2사례 추가·수명 코드 검토, CLI Runtime·Editor 컴파일, 직렬화 참조 검사. 실제 Unity Play/엔진NUnit/청음은 수행하지 않는다.
- [x] 가이드·일일 기록·CONTEXT 갱신. 커밋/푸시는 요청 범위가 아니므로 수행하지 않는다.

## 초기 BGM·중요 대화 고정 속도

- [x] 첫4컷에 Two empire.wav를 기존BGM믹서/API로 연결. 음량0.8/시작0.8초/현재 자막에서1.2초fade-out. 씬Start순서경쟁회피, 음원준비, 버전기반취소복원.
- [x] 삽화본문10cps·시네마틱 또는 Cutscene소유대화14cps로 설정속도에서분리. 매노드/선택지분기정책갱신, 일반대화는원본timing·설정복구. 공유플러그인자산불변.
- [x] 중앙자막4cps/maxVisibleCharacters 진행, 완료후1.2초유지, 설정/Paused정지, 최소표시시간과타이핑총시간비교.
- [x] ShowText직후즉시속도적용,2프레임재적용코루틴제거. 기존직접Z/X스킵은유지.
- [x] 콘텐츠31사례/속도소유권5사례소스추가·갱신, Runtime418/Editor354 CLI컴파일오류0. 실제Unity재생·청음·엔진NUnit미실시.
