# 사선 화면 참격 구현 계획

> **For agentic workers:** REQUIRED: Use executing-plans to implement this plan. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 토끼 전투 실험실에서 성능을 우선한 사선 화면 분할 참격을 직접 사용할 수 있게 한다.

**Architecture:** 기존 Action_Damage가 피해를 처리하고 뒤의 표시 블록이 카메라별 토큰을 임시 소유한다. 실험실 Renderer2D의 휴면 RendererFeature는 활성 효과가 있는 게임 카메라에만 RenderGraph1패스를 추가한다. 화면 표시와 전투/저장은 독립이다.

**Tech Stack:** Unity6/URP17.3 RenderGraph, HLSL 단일Point샘플, 기존 SkillActionBlock/Odin/카탈로그/실험실 Builder.

**후속 요청 반영:** 폭22씩/전체검정틈44, ExpandDuration0.18초+HoldDuration1초+기존Duration0.3초복원(총1.48초). 확대/복원 cubic smoothstep과0강도 시작으로 순간 최대폭 전환을 제거한다. 초기값 생략은 기존Begin1강도를 보존하고 SetProgress도 유지한다. 새필드의 음수/비유한값·합계overflow를 거부한다. 추가 캡처/패스 없이 hold 중 삼각함수 갱신을 생략한다. 선 합성 뒤 검정 마스크를 적용하고 샘플/테스트/사용법을 갱신한다.

## Chunk1: 표시와 연결

외곽선 후속 요청: 기존LineColor/LineWidthPixels를 틈 중앙선이 아닌 양쪽 바깥 band에 적용한다. 기본white/1.25px, 새OutlinePulseStrength=.65로 최대폭 순간 약2.06px 강조한다. 검정 gap 마스크는 최종 적용하고, 추가 텍스처/패스/블러는 만들지 않는다. 색상 알파는 불투명도이며 기존FlashIntensity를 따른다.
- [x] `Scripts/VFX/Runtime/ScreenSplitEffect.cs`: 유효성·envelope·카메라별 토큰·pause외부시계·장면/소유자 정리. 스냅샷만 렌더러에 제공한다.
- [x] `Scripts/VFX/Runtime/ScreenSplitRendererFeature.cs`, `Presentation/ScreenSplit/ScreenSplit.shader`: 비활성0패스, 활성1패스, 재질 지연생성/해제. Shader 직접참조로 .mat는 불필요. 2D는 활성 Color 입력만 요청하고 깊이/노멀/모션 prepass를 추가하지 않는다.
- [x] `Battle/Data/Action_ScreenSplit.cs`: 표시전용 블록, 한글Odin필드·고정시간보고·취소finally. 피해 실행은 유지하며 결정타 후 표시 블록 opt-in/강제중단 action scope 검사를 보강했다.
- [x] 새 `Content/Skills/Ally/BattleLab/diagonal_screen_slash.asset`, lab front7번째, 카탈로그·복원Builder·메이커헤더/분류에 연결한다.
- [x] 실험실 Renderer2D에만 feature subasset을 추가한다. 기존광원/Volume/카메라·씬은 변경하지 않는다.

## Chunk2: 검증과 마감
- [x] 소유권/교체/종료/입력값/envelope/swell/외곽선 관리형358검사와 렌더구조6검사 통과. 0강도시작/확대·복원단조성/경계연속성/두께펄스·색보존을 포함한다. 원소18개+연출1개/renderer참조/결정타·취소 회귀소스를 추가했다(Unity NUnit 미실행).
- [x] CLI 전체컴파일 Runtime423/Editor356소스 오류0(기존경고222/0), 신규GUID7개 유일/meta/UTF8/직렬화참조·범위diff 확인. 엔진/GPU/실기기는 미실행이다.
- [x] 사용법/원리/수정위치와성능한계 문서, CONTEXT/전투규칙/시나리오스킬계약/당일기록 업데이트. gameplayEdit, 커밋·푸시 없음.
