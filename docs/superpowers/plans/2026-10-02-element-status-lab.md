# Element Status Lab Implementation Plan

> **For agentic workers:** Use the available collaboration agents for isolated implementation tasks and review. Follow the approved spec; do not enter Unity Play or commit without user request.

**Goal:** BunnySlime BattleLab에서 색이 다른 위젤 3명과 역할별 6스킬로 다섯 상태를 바로 시험한다.

**Architecture:** 기존 CharacterBase/SkillContext/전투 서비스에 좁은 연결을 추가한다. 상태 수치는 actor별 ElementalStatusState, 제작 수치는 ElementalStatusProfile, 스킬 부여는 SkillData/SkillContext가 소유한다. 기존 Scene과 Scenario는 유지한다.

**Tech Stack:** Unity 6, C#, 기존 ScriptableObject와 TMP, Odin, DOTween.

## Chunk 1: 런타임과 제작 계약

- [x] CharacterBase의 피해/턴/정리 연결과 신규 누적 상태 순수 코어·프로필 구현. 일반 StatusEffect는 유지.
- [x] SkillData 부여 목록/발동 계수, SkillContext 타격 연결, 상태 소비/정화 블록 구현. 기존 모든 직접 스킬 타격을 연결.
- [x] Frozen Z/X/C 입력 차단 및 행동 완료/스킵 구분. 소수 축적/면역/50타/취약/파쇄/재귀 방지 검사 추가.

## Chunk 2: 실험실과 표시

- [x] 기존 lab front/support/striker 데이터·카탈로그를 재사용, 새 역할별 6스킬 및 적 상태 공격 데이터를 Content/Skills에 제작.
- [x] 기본 3명/전멸 실습 6명 분리, 세션 복구 소유권 보존. 색상 변경과 첫 화면 파티 확인.
- [x] CharacterData/EnemyData 프로필 참조, 전투 캐릭터 tint와 상태 HUD/설명 색 키워드 연결. 상태 피해 숫자 연결.

## Chunk 3: 검증과 인수인계

- [x] 새 소스를 포함한 Runtime/Editor dotnet build. 마지막 빌드는 갱신된 csproj로 임시 include 없이 오류0/기존 경고2.
- [x] 수치 회귀 검사 작성, 자산 ID/GUID/3×6스킬·카탈로그 참조 정적 검사 및 변경 범위 diff 검토. NUnit 실행은 안 함.
- [x] AIAssets 변경 기록·feedback 및 관련 규칙 갱신. 남은 수동 확인을 명시.

남은 사용자 확인: Unity Play/NUnit, 640×480 가독성, 방어/회피/파쇄/50타 연출, 반복 진입·파티 복구. 구현 완료와 실제 플레이 검증은 구분합니다.

작업 중 발견한 API 차이는 spec/계획에 기록하며 기존 직렬화 값을 재번호화하지 않는다. 사용자 승인 없는 커밋/푸시는 생략한다.
