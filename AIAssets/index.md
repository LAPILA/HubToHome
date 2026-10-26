# AIAssets Index

전투 공통 피드백(2026-10-26): [판정·연타 억제·지속 방어·AP 예산·공격 QTE 자동·누적 강화·아이템 연계·보스 HP](yjlim/feedback/2026-10-26-combat-feedback-foundation.md). 특정 적 분기 없이 적용하며 평타/스킬 구성/전조 말풍선은 보류했습니다. 본편 저장·성장·원본 스킬은 보존합니다. [실험실 사용법](../docs/bunny-slime-battle-lab.md).

C 반격 후속 수정(2026-10-26): [후퇴·대기 뒤 끝나던 오류](yjlim/feedback/2026-10-26-zev-sample-seal-counter.md). 실제 DOTween.dll에서 콜백 없는 Interval 전용 Sequence의 Startup실패를 재현했고, 대기만 floatTween으로교체했다. 기존대체모델보강후4실패→0실패/10사례108단언,전체CLI오류0. UnityPlay는미검증이다.

최신 샘플(2026-10-26): [ZEV만 표시·대사 교체·2행동마다3스킬 잠금·C 정수2배 포커스/돌파](yjlim/feedback/2026-10-26-zev-sample-seal-counter.md). 토끼 데이터/본편/저장은 보존하며 회색·입력거부·종료해제를 같은 임시 상태로 연결했다. 실제 Unity Play는 미검증이다.

현재 전투 기본 정책(2026-10-26): [모든 적 공격의 이동 중 실시간 대응](yjlim/feedback/2026-10-26-global-enemy-attack-flow.md). 정지 준비/감속/접점 고정을 폐기하고 일반·투사체·연쇄·C/광역·시나리오 호출에 연결했습니다. 아래의 과거 opt-in/가드 정지 안내보다 이 정책이 우선합니다.

전투 피드백 마무리(2026-10-26): [턴 안내 제거·ZEV 이름/반응 속도 조정·실루엣 생명주기 오류·무문자 QTE 피드백](yjlim/feedback/2026-10-26-battle-feedback-polish.md). 턴 시스템/탈진은 유지하며 난무 후반0.24초·성공 흰색/실패 빨강 흔들림을 적용했습니다. 실제 Unity 화면·입력 체감은 미검증입니다.

제브 사슬 광란(2026-10-26): [왕복10·공중2·혼합8·최종C검격·1턴탈진](yjlim/feedback/2026-10-26-zev-chain-frenzy.md).2페이즈 첫 일반 공격 완료 뒤 다음 자기 행동에서1회 사용하며 기존 스킬5종/본편DB는 유지합니다. 재사용 단계·후속 패턴·휴식 턴·VFX 수명·조정 위치와 관리형/엔진 검증 한계를 구분했습니다.

최신 전투 후속(2026-10-26): [사슬2배·개별 반전/음량·제브 일반4종·상단 고정3줄 순차 기록](yjlim/feedback/2026-10-26-battle-chain-rolling-followup.md). Z는 현재 줄 완성/다음 줄 진행이며 표시 크기·속도는 공용 프리팹과 프로필에서 조절합니다. 이전 즉시 전체 표시 정책은 대체했습니다.

제브 사슬2페이즈(2026-10-26): [HP1800/50%전환·적 화면 관통·4모서리 사슬·검정 전투 대사창](yjlim/feedback/2026-10-26-zev-chain-phase.md). Source/Runtime/17대사4언어를 연결했고 사슬은 선택적 표시 프로필로 재사용한다. 명시 배우 포커스가 전체구도 보정에 눌리던 경로도 수정했다. [실험실 사용법](../docs/bunny-slime-battle-lab.md).

단독 실험실 최신(2026-10-26): [컷인 제거·실제 크기 실루엣·명중 점멸·Z 재준비·화면 관통](yjlim/feedback/2026-10-26-battle-accent-pierce.md). 기본 위젤1명·토끼/제브 두 입구·위젤10/제브5 사본은 유지한다. 컷인8개 제거,3스킬 화면 끝 관통, 반복 Z 기본0.18초 재준비와 배우 옆 표시를 연결했다. 본편 DB/스킬/Scene/Prefab/저장은 보존한다. [실행·재사용 가이드](../docs/bunny-slime-battle-lab.md) · [이전 구성 기록](yjlim/feedback/2026-10-25-solo-battle-showcase.md).

최신 전투 기록(2026-10-25): [상단 행동·실제 HP/AP·상태 결과 요약](yjlim/feedback/2026-10-25-battle-combat-log.md). 피격/저체력 자동 말풍선을 제거하고 행동별 합산 결과를 기존 중앙창에 표시한다. 기존 상태의 부여·해제·행동 불가 이유만 안내하며 새 스턴은 추가하지 않는다. [검증·한계](2026-10-25-update.md).

전투 안내 확인(2026-10-24): [중앙 상단 Z 전체 표시·즉시 넘기기](yjlim/feedback/2026-10-24-battle-narration-confirm.md). 글자 출력/유지 중에도 새 입력을 받고, 첫 확인은 전체 표시·다음 확인은 추가 대기 없이 종료한다. 자동 안내의 중복 유지 시간을 제거하고 주 대화·짧은 말풍선은 보존한다.

최신 프롤로그(2026-10-24): [교체형 튜토리얼 · 제작/검수 안내](yjlim/feedback/2026-10-24-prologue-tutorial.md). 창가 원래 대사→기관사·위젤 자동 접근 대화→수리→선택 탐색→후미 전투→후퇴·구조→봉쇄→단독 감속·탈선→완료 저장을 기존 시스템으로 연결한다. 객차 문은 접촉 이동이다. NPC/대사/아트는 샘플 참조이고 새 문구는 한·영·일·중으로 분리했다. 검증 수단과 미확인 범위는 [당일 기록](2026-10-24-update.md)에 구분한다. 아래10/19 임시3초 사건은 튜토리얼 Scene에서 대체된다.

최신 코드 검수(2026-10-23): [저장 복구·X 취소·대화 패널 손실·선제공격 중단·도착 문 범위](yjlim/feedback/2026-10-23-runtime-safety-audit.md). 확인된 실행 경계만 수정하고 기존 아트/씬/프리팹/DB/외부 플러그인·웹사이트는 보존했다. 저장 스키마와 전투 판정은 변경하지 않았다. 관리형 검사와 Unity 엔진 미실행 범위를 분리해 기록했다.

최신 픽셀 안정화(2026-10-23): [화면 배율 펌핑 수정·고정 전투 배율·정수 렌즈 규칙](yjlim/feedback/2026-10-23-camera-pixel-stability.md). 매 이동마다 자동 줌하지 않고 기본7.5/원본1배를 전투 동안 유지한다. native PP/최종XY/Brain 후 렌즈까지 보정하며 엄격 모드의 타격 줌·카메라 회전은 억제한다. CLI447/370오류0·관리형52사례/73,689단언·메타1,555개 통과. Unity/GPU/실기기 미검증. [이전 HUD 가림 수정](yjlim/feedback/2026-10-23-battle-camera-safe-viewport.md)의 Lens4·적응형 줌은 이번 정책으로 대체한다.

최신 컴파일 복구(2026-10-23): [AudioManager 누락 타입: 33자리 메타 GUID 교정](yjlim/feedback/2026-10-23-unity-meta-import-fix.md). 메타2개만 수정,1,550개 GUID검사/전체CLI오류0 및 실제Unity컴파일목록포함 확인. C# 음향 동작은 변경하지 않았다.

최신 적 참격 후속(2026-10-23): [적 중심 준비·타격 후 짧은 돌파·취소 복구](yjlim/feedback/2026-10-23-enemy-melee-pass.md). 제브 빙섬/연쇄 참격 두 스킬만 선택 적용하고 나머지 일반 명중 피드백은 보존했다. 판정 창/피해·HUD는 그대로다. Runtime444/Editor368 CLI 오류0, 실제 경로871·데이터41관리형 검사 통과. Unity 화면 체감은 미검증이다.

최신 전투 피드백(2026-10-23): [준비–명중 카메라 컷·즉시 피격·HP/상태/음향 조정](yjlim/feedback/2026-10-23-battle-impact-cuts.md). 캐릭터 정지는 기본0으로 바꾸고 구도 준비0.05초/명중0.025진입·0.025유지·0.075복귀를 연결했다. 방향 반동/적 처치 강조/HP 잔여바/상태 전환/명중음4보이스를 통합했다. Runtime440/Editor366 CLI 오류0, 생산 피격201·카메라10,027·오디오236·상태16,398 관리형 단언 통과. Unity 체감/GPU/기기는 미검증이다.

초기 명중 피드백(후속 수치 대체됨): [직접·상태 구분과 수명](yjlim/feedback/2026-10-23-damage-impact.md). 감전 추가 피해의 직접 피격 취소와 순수0/음수 피해를 방어했다. 초기 정지 기본값은 위 최신 카메라 중심 구성으로 대체했다.

최신 동료 동행(2026-10-23): [제작·설정·시퀀스 사용법](../docs/overworld-party-follow-guide.md) · [경로 버퍼·취소·맵 이동 학습](../docs/study/deep/10-overworld-party-follow.md). 실제 파티 기본3/설정 최대6명 표시, 실제 경로 추종과 연출용 고정·이동·숨김·재합류를 연결했다. Runtime436/Editor362 CLI 오류0, 관리형11,194+110+86검사 통과. Unity 재생/실기기는 미검증. 기존 Google Docs11장에 제작 절차를 추가했으며 Notion은 사용자 수동 ZIP2개로 학습2문서·운영1문서를 게시하고 브라우저에서 계층·실제 본문·서식·링크를 확인했다. API404는 별도 미해결이다. [운영실 반영 현황](../docs/overworld-party-follow-production-update.md) · [전체 기록](2026-10-23-update.md).

최신 통합(2026-10-23): [날씨 샘플·창가 시작·피격 표시 사용법](yjlim/feedback/2026-10-23-weather-opening-hit.md). 방 자식 Prefab에 그림3개를 교체하는 원형 안개/2D 착지 비, 국가4컷·현재 자막/이름UI 잔재 제거, 직접 피격 공통 경로를 연결했다(피격 수치는 위 후속 정리 참조). 기존 저장 구조는 유지하고 메모리 저장33검사와 전체 CLI 오류0을 확인했다. 씬 자동 날씨 배치·Unity/GPU/실기기 검사는 하지 않았다. [전체 변경·검증·하네스 교훈](2026-10-23-update.md).

최신 패링 연출(2026-10-19): [저스트가드 · 배경만 약한 충격파](yjlim/feedback/2026-10-19-parry-background-ripple.md). 실험실 Z 성공 시 Center 기준0.3초, Background 정렬레이어만 왜곡하고 캐릭터/HUD를 보존합니다. Profile에서 강도/반경/고리색 편집, 휴면GPU0패스·활성2패스. C#/D3D분리문법/관리형검사 통과, Unity/GPU/실기기는 미검증입니다.

최신 전투 샘플(2026-10-19): [단층 베기 · 사선 화면 절단](yjlim/feedback/2026-10-19-diagonal-screen-slash.md). 토끼 실험실 첫 위젤7번째/AP12, 검정틈44기준픽셀·0.18초확대→1초유지→0.3초복원. 0강도에서 부드럽게 열고 닫으며 별도 캡처 없이 활성1효과패스/Point1샘플입니다. Overlay HUD/본편DB/저장은 유지, GPU·실기기발열은 미검증입니다.

최신 메뉴 정리(2026-10-19): [작은 창·방향키/클릭·설정 모달·배경 흐림](yjlim/feedback/2026-10-19-field-menu-navigation.md). 출력640×480/창592×416, Q/E필수해제, 메뉴·설정 한정 포인터, OS 숫자키보드 회피. 전용코드10개는 `UI/Runtime/FieldMenu`로 모으고 meta해시를 보존했다. CLI 오류0/순수수치31검사 통과, Unity화면·실기기는 미검증.

최신 프롤로그(2026-10-19): [새 게임 → 4컷 → 창가 → 열차 → 백색체 공개](yjlim/feedback/2026-10-19-prologue-prototype.md). 이름 입력 제거, 원본 애니메이션 반복/1회 구분, 임시3초 사건·후미 진입 연결. 대사·시간·플래그 편집 위치와 실제 Unity 재생 미검증 범위를 함께 기록했다.

최신 정렬 수정(2026-10-19): [플레이어 Order3 고정·상자 Y축 가림](yjlim/feedback/2026-10-19-player-sorting.md). 플레이어 Y×100 계산을 제거하고 기본3 고정, 상자 바닥SortingGroup3 및 실제사용2DRenderer의CustomAxisY 적용. 사용자 콜라이더·위치·그림은 보존하고 기존 전투 임시전경은 유지.

최신 상자 구현(2026-10-19): [기차 과자 상자 · 복제/설정 위치](yjlim/feedback/2026-10-19-item-chest.md). 기존 아이템 마커에 열린 외형 유지 옵션, 전체 수량 지급/즉시 완료/로드 외형 복원 추가. Q0 시작점 오른쪽에 과자×1 샘플과 재사용 Prefab을 연결했다. 기존 아트·타일·씬·저장 형식은 보존. [제작 가이드](../docs/content-maker-guide.md#아이템-상자-배치하기).

최신 UI 수정(2026-10-19): [고정 UI 조명·후처리 분리](yjlim/feedback/2026-10-19-ui-light-isolation.md). 화면 UI를 Overlay로 통일하고 640×480/정수 배율/게임 출력 경계를 보존했다. 창별 정렬·모바일 안전 영역·타이틀 월드 그림은 유지, 월드 말풍선·전조 VFX와 조명 자산은 변경하지 않았다. Runtime/Editor CLI 컴파일 오류0, Unity 화면은 미검증.

열차 현황 정정(2026-10-19): [현재 Q0 조명·편집 위치](yjlim/feedback/2026-10-18-train-cars.md). 사용자가 Q1~3와 장식을 삭제한 상태를 유지하고 Lamp100→0.3/Room Ambient0.6, 열차 카메라만 후처리OFF, 삭제 방 참조3개 정리. 현재7사례 계약 검사로 갱신했으며 실제 화면은 미검증. [당일 기록](2026-10-19-update.md).

최신 열차 제작(2026-10-18): [ALL 열차 4객차 · 편집 위치와 이동 연결](yjlim/feedback/2026-10-18-train-cars.md). 기존 Q0 보존, 객실·휴게·수하물·관측칸과 좌우6문, 지역 방4개 등록, 씬 한정 2D 조명/카메라 Renderer 연결. 원본 아트와 기존 역 이동은 유지. 정적 검사/CLI 컴파일 통과, Unity 재생·기기 성능은 미검증.

편집기 해결 확인(2026-10-18): [Tile Palette 격자·선택 테두리 미표시 — SSAO](yjlim/feedback/2026-10-18-tile-palette-ssao.md). PC_Renderer의 SSAO 해제 후 사용자 복구 확인. 원본 이미지/Library/패키지 변경 없이 해결했으며 이전 Tilemap 참조 예외와는 구분한다.

최신 메뉴 구현(2026-10-18): [실제 Unity 필드 메뉴 · 사용법과 수정 위치](yjlim/feedback/2026-10-18-field-menu.md). 가방/장비/동료/수집 일지, 실제 스킬 트리와 능력치 일괄 배분, 모바일 전용 터치·기기 안전 영역을 기존 데이터/세이브에 연결했다. 기존 씬/프리팹 재저장 없음. [당일 변경·검증 기록](2026-10-18-update.md). Unity 화면·실기기 터치/줄바꿈은 미실행이다.

이전 화면 보정(2026-10-16): [픽셀 UI 정수 배율 · 당시 연속 줌](yjlim/feedback/2026-10-16-pixel-output.md). 여백 허용·UI 정수 배율은 유지하고 일반 카메라 연속 줌은10/23 엄격 픽셀 규칙으로 대체했다. 기준 해상도 미만 출력의 축소는 무손실이 아니다.

최신 구현(2026-10-16): [챕터 1 기반 규칙 · 제작 연결과 검증 범위](yjlim/feedback/2026-10-16-chapter01-foundation.md). 공용 EXP/레벨·개인 투자, 이벤트 합류, 방어65%, 아이템·장비, 수동 저장 휴식/일반 적 재등장, 보스 재도전, 일지·입력·대화 스킵을 기존 시스템에 연결했다. [당일 변경·검증 기록](2026-10-16-update.md). 콘텐츠 미정 사항과 Unity/실기기 미검증은 문서에서 분리한다.

최신 범위 정정(2026-10-13): [챕터 1 · 미완성 기능과 전체 기획 결정 목록](yjlim/feedback/2026-10-13-chapter01-feature-inventory.md). 위젤+동료2명 기준, 우선순위 없이 구현 공백/기획 데이터/통합 검증/선택 확장을 구분했다. 동료 합류, 장비 지급, 아이템 효과, 저장 회복, 번역·입력 연결을 확인했다. 게임 코드 변경 없음.

최신 기획 후보 점검(2026-10-13): [본편 기능 기획 우선순위](yjlim/feedback/2026-10-13-design-priorities.md). 동료별 고유 자원, 성장·스킬 구성, 장비 효과, 편성, 보상·회복과 입력 안내를 미구현/기반 있음/정책 확인으로 구분했다. 게임 코드와 자산은 변경하지 않았다.

최신 심화 학습 자료(2026-10-12): [코드 따라 읽기 · 로컬 원본](../docs/study/deep/README.md) · [Notion 심화 색인](https://app.notion.com/p/3f7641f619018140b840faafd0b4128c). 해설 10편, 실제 코드 37곳, 호출 흐름·상태 추적·설계 선택·실패 경계·실습을 추가했다. 기존 학습실과 게임 코드는 보존했다. [검증과 하네스 교훈](yjlim/feedback/2026-10-12-code-walkthrough.md) · [당일 기록](2026-10-12-update.md).

최신 공부·포트폴리오 자료(2026-10-11): [기능별 학습실과 전체 코드 색인](../docs/study/README.md) · [Notion 개발 학습실](https://app.notion.com/p/3f6641f6190180bb8567ff9caf83e5a9). 자체 C# 686개와 전체 폴더, 자료구조·알고리즘·패턴, 공부 과제, 면접 사례, 개발 기록을 분리했다. 사용자 로그인 후 정확한 목적지에 게시했으며 [게시 결과·검증·하네스 교훈](yjlim/feedback/2026-10-11-code-study-portal.md)에 상태를 남겼다.

최신 설정·저장·장비 정리(2026-10-11): [저장 지점과 자동 저장 연결 사용법](yjlim/feedback/2026-10-11-settings-save-equipment.md). 수동 3칸/자동 1칸, 설정 지연 기록, 장비 수량·상한 보호. [검증과 변경 파일](2026-10-11-update.md).

최신 자체 코드 정리(2026-10-09): [미사용 스크립트 6개 삭제와 중복 처리 통합](yjlim/feedback/2026-10-09-own-scripts-cleanup.md). 구현 변경은 Scripts와 meta에 한정했습니다. C# 691→685개, 순감소 499줄. CLI 컴파일 오류 0개, YAML 전후 비교 3,844개 통과. Unity 재생 검사는 하지 않았습니다. [삭제·유지 근거와 복구 위치](2026-10-09-update.md).

최신 전투 메인 대화(2026-10-08): [서브메뉴 대화 표시·제작/조절 위치](yjlim/feedback/2026-10-08-battle-submenu-dialogue.md). 기존 DialogueData/화자/시나리오를 재사용하고 목록만 가린 뒤 복구합니다. 짧은 말풍선은 변경하지 않았습니다. [코드/검증 기록](2026-10-08-update.md).

최신 전투 기본 흐름(2026-10-07): [매 타격 전조·전멸 후 선택·장비/저항·저장 경계](yjlim/feedback/2026-10-07-combat-foundation.md). 후열은 계속 싸우기 승인 후 진입, 포기는 게임오버. 추가 씬/프리팹 연결 없음. [코드/검증 기록](2026-10-07-update.md).

최신 안정화: [대사 입력 누수·후열 교체 중단·피해 숫자 재사용 정리](yjlim/feedback/2026-10-05-battle-presentation-cleanup.md). 기존 전투 규칙·자산 유지, CLI 컴파일/관리형 검사 확인. Unity 재생 및 글자 선명도는 수동 확인 대상입니다.

최신 전투 표현: [말풍선 텍스트/도트 아이콘/혼합 · 전열0.5초 페이드/후열 화면 밖 진입](yjlim/feedback/2026-10-02-speech-and-party-wave.md). 추가 프리팹 연결 없음, 기존 승계 규칙 유지. 글자 깨짐은 화면 증상 확인 대기이며 실제 Unity 재생 검증은 수동입니다.

최신 피해 표시: [대상·속성·행동별 누적 숫자](yjlim/feedback/2026-10-02-damage-totals.md). 연타는 한 숫자에 합산하고 물리/화염/냉기/전기/중독/출혈을 세로 정렬합니다. 피해 계산/기존 QTE는 유지, 자산 연결 변경 없음. 실제 Unity 화면은 수동 확인 대상입니다.

최신 제브: [냉기·감전5스킬·개별 접근·보정 랜덤](yjlim/feedback/2026-10-02-zev-cold-electric.md). 실험실 수치와 공유 스킬을 갱신했으며 본편 DB 능력치·씬·시나리오는 보존했습니다. 실제 체감은 수동 확인 대상입니다.

최신 속성 실험실: [위젤 3색 파티·18스킬·5종 누적 상태](yjlim/feedback/2026-10-02-element-status-lab.md) · [바로 테스트하는 순서](../docs/bunny-slime-battle-lab.md). 기존 토끼 통합 전투 사용, 후열 전멸 실습은 3+3 유지. 본편 위젤·ZEV·씬·시나리오 자산은 보존합니다. Unity Play/NUnit 실행은 수동 확인 대상으로 남깁니다.

후속 3:1 수정: 공용 프리팹의 InteractionSystem 중복 Awake가 추가 동료를 삭제하던 문제와, PlayerController.Start가 전투 좌표를 필드 좌표로 덮던 문제를 수정했습니다. 토끼/ZEV 모두 전열3 대 적1이며 월드 수동 배치는 필요 없습니다. [원인·재현·검증](2026-10-02-update.md).

최신 진입 연출: [심리스 전투 — 경고·짧은 줌·사선 절단·상하 개방·HUD 등장](yjlim/feedback/2026-10-02-battle-entry-reference.md). 후속 요청으로 약 2초/줌 0.5초, 완전 암전 뒤 캐릭터·NPC·UI·카메라 준비로 수정했습니다. 공용 Host 값과 조우별 켜기/끄기 제공, 전용 BattleScene 유지. Runtime/Editor 컴파일 오류 0, Unity Play는 미실행입니다.

최신 제작 문서: [Google Docs 콘텐츠 제작 가이드 갱신](yjlim/feedback/2026-10-02-content-guide-refresh.md) · [원본 가이드 열기](https://docs.google.com/document/d/1kY_qJeiVUwpHJNuCOCk_ac3LsDe85bAiR0iBLx3Py2E/edit). 기존 14장/18표를 보존하고 스킬 13종·전조·압력·상점·캐릭터 데이터 안내를 현행화했습니다. PDF 22쪽 검토 및 표 경로 넘침 수정 완료. [검증 기록](2026-10-02-update.md).

최신 로컬 스킬 안내(2026-10-02): [복제·블록·누적 상태·압력 강화·DB 연결](yjlim/feedback/2026-09-28-skill-block-authoring.md). 상태 실험실과 제브의 개별 접근/보정 랜덤을 반영했습니다. 원격 Google Docs는 위 갱신 시점 기준이며 이번 로컬 변경은 아직 동기화하지 않았습니다.

최신 이펙트 수정: [전조 재생 완료 시 숨김·방어창 종료 시 풀 반환](yjlim/feedback/2026-09-26-telegraph-animation.md). 마지막 프레임 잔류를 제거하고 판정 시간은 유지했습니다. 제작 중인 Aseprite/프리팹은 변경하지 않았습니다. [당일 검증 기록](2026-09-28-update.md).

최신 전투 규칙: [속도 기반 턴 — 최종 규칙·편집 위치·턴 큐 연출](yjlim/feedback/2026-09-28-speed-turns.md) · [2026-09-28 변경/검증 기록](2026-09-28-update.md). SPD는 순서와 행동 빈도에 반영됩니다. 자연 만료를 예고에 미리 반영해 이미 표시한 연속 턴이 만료 뒤 사라지는 오류를 수정했습니다. 순수 검사 19개 통과(100조합/4,800행동), Unity 연결 검사는 컴파일만 확인했습니다.

최종 정리: **증기 가속 샘플 스킬과 DB·카탈로그 연결은 삭제**, 스킬 기획은 보류합니다. 공용 속도 상태·턴 계산·DOTween 재정렬은 유지합니다. 원작 내부 공식 복제가 아닌 프로젝트의 누적 준비량 방식이며, 사용자 PUSH 요청에 따라 현재 브랜치에 반영합니다.

환경 연출: [픽셀 풀 바람 — UV 픽셀 변형·실험실 적용·미동작 원인 수정](yjlim/feedback/2026-09-27-pixel-wind.md). 기존 PixelWind/밝기 파동은 삭제했습니다. 혼합 텍스처 검사로 꺼지던 원인을 수정해 BunnySlimeBattleLab의 Grid/Tilemap(잔디 252칸·바람)과 Tilemap_Walls(벽 168칸·정적)를 분리했습니다.

바람 제작 편의: [7가지 시작값·위/아래 고정·엇박자 조절·아트 분리 기준](../Assets/_Game/Presentation/PixelFoliage/README.md). 프리셋은 명시적으로 적용할 때만 기존 값을 바꿉니다.

고유 자원 및 표시: [위젤 압력 — 데이터·스킬 메이커·단계 애니메이션 수정 위치](yjlim/feedback/2026-09-27-wizel-pressure.md). 오른쪽 ActorLargePortrait는 선택 아군의 자원 표시입니다. 후속: [ZEV 스킬 이동 데이터 미세 조정](yjlim/feedback/2026-09-27-zev-movement-tuning.md) · [C 압력·피해 숫자·대사 위치 및 레터박스 앞 표시](yjlim/feedback/2026-09-27-counter-popups-dialogue.md).

`AIAssets` 루트의 오래된 개별 정리 문서는 `AIAssets/yjlim/` 스타일로 재정리했습니다.

## 먼저 볼 문서

최신 통합: [2026-09-27 토끼 슬라임·ZEV 전투 실험실](yjlim/feedback/2026-09-27-battle-lab-consolidation.md) · [당일 검증/변경 기록](2026-09-27-update.md). 종합/가드·회피/C 입구를 통합했고, ZEV 원본 5스킬·전조를 연결했습니다. 임시 스킬도 Content/Skills로 통합했으며 3+3 전멸 실습은 별도로 유지합니다.

최신 버그 수정: [빈 공격 안내 프레임·확인 입력 중복](yjlim/feedback/2026-09-26-battle-narration-input-fix.md). 빈 내레이션은 창을 켜지 않으며, 버튼 콜백에도 입력 소비/활성화 프레임 가드를 적용합니다.

전투 시인성: [이동 중에만 잔상 표시](yjlim/feedback/2026-09-27-movement-only-ghosts.md). 판정: [2026-09-26 근접 첫 전조부터 패링·즉시 입력 반응](yjlim/feedback/2026-09-26-melee-parry-cue.md).

최신 스킬: [2026-09-26 압력 난무 독립 QTE·공중 회전 베기](yjlim/feedback/2026-09-26-independent-barrage-aerial-skill.md). 난무는 0.1초 간격 50타, Z/X/C는 독립 주기입니다. 360도 회전은 별도 공중 스킬의 카메라에만 적용하며 캐릭터 자체는 회전시키지 않습니다.

교전 연출: [짧은 카메라 전환·고정 HUD](yjlim/feedback/2026-09-26-battle-camera-beats-rapid-skill.md). 상시 드리프트/호흡 줌 없음. [앞선 동적 연출](yjlim/feedback/2026-09-26-dynamic-battle-presentation.md)의 대사/UI 반응/C 후퇴 반격과 [중앙 교전](yjlim/feedback/2026-09-26-central-duel-presentation.md)의 캐릭터 배치는 유지합니다.

최근 전투 작업: [2026-09-20 중앙 궁극기·피격 대상 전진·지상 복귀](2026-09-20-update.md) · [사용자 확인 및 미결정 사항](yjlim/feedback/2026-09-20-battle-timing.md) · [실험실 사용법](../docs/bunny-slime-battle-lab.md).

최신 UI 수정: [2026-09-26 목업 기준 전투 HUD 개편](yjlim/feedback/2026-09-26-battle-hud.md) · [작업/검증 기록](2026-09-26-update.md). 이전: [파괴된 Image 트윈 수명 정리](yjlim/feedback/2026-09-20-battle-ui-lifetime.md).

최신 전조 연결: [2026-09-26 Telegraph Aseprite 애니메이션·전투 메뉴 SFX](yjlim/feedback/2026-09-26-telegraph-animation.md).

최신 화면 샘플: [2026-09-26 토끼 실험실 2D 조명·Volume 조절법](yjlim/feedback/2026-09-26-bunny-lab-lighting.md).

1. `yjlim/README.md` - 현재 문서 구조와 읽는 순서
2. `yjlim/feedback/2026-06-19-work-summary.md` - 지금까지 한 것 / 안 한 것 / 더 해야 할 것 종합 정리
3. `yjlim/TODO.md` - 다음 작업 체크리스트
4. `yjlim/Patchnote/2026-06-19-aiassets-reorganization.md` - 이번 문서 정리 패치노트
5. 최신 `YYYY-MM-DD-update.md` - 당일 작업 update note
6. [프로젝트 학습 자료](../docs/learning/README.md) - 실제 코드 기반 C#·알고리즘·자료구조·Unity·전투·시나리오·제작 도구·패턴·실습

## 운영 규칙

- 분석/리뷰/아키텍처/인수인계 문서는 `yjlim/feedback/`에 둡니다.
- 패치노트형 요약은 `yjlim/Patchnote/`에 둡니다.
- 루트 문서는 중복된 장문 기록을 쌓지 않고 `yjlim/` 문서로 안내하는 얇은 진입점으로 유지합니다.
- 시나리오 파이프라인 작업은 `.agents/skills/hubtohome-scenario-authoring/` 규칙을 먼저 확인합니다.
