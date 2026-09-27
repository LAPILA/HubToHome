# 위젤 압력 자원 구현 기록

사용자 승인: 강화 조건 충족 시 스킬별 지정량만 소모. 전투 시작 0, 퍼펙트 패링 +1, 최대 4. 조건 미달은 기본 스킬 실행을 막지 않는다. 커밋/Play 실행/서브 에이전트 없이 현재 작업 공간에서 진행한다.

구조: 캐릭터 DB는 자원 정의만 참조하고 현재 수치는 각 PlayerCharacter의 전투 전용 상태가 소유한다. 스킬 시작 시 조건·소모·강화 배율을 한 번 확정하고 QTE/타격별 배율과 분리한다. 완료 획득은 정상 완료 시 한 번만 적용한다. 기본 턴제와 시나리오 스킬 실행이 같은 SkillContext API를 사용한다.

- [x] 자원 정의/인스턴스 상태/스킬 정책, 전투 시작·종료 초기화
- [x] 패링 획득, 일반/시나리오 스킬 실행, 모든 피해 블록의 강화 배율
- [x] ActorLargePortrait 자원 UI 및 step0~4 원본 프레임 연결, 스킬 메이커 편집
- [x] 위젤 표시명/샘플 연결, 컴파일/정적 검사, 인수인계

완료 검증: 런타임/Editor 빌드 오류 0, 기존 ConfigPanelUI 경고 2. 캐릭터 7개/스킬 9개/단계 5개 정적 연결 검사와 이번 변경 범위 diff 검사 통과. 회귀 테스트 코드는 추가하되 Unity 테스트/Play는 실행하지 않음. [인수인계](../../AIAssets/yjlim/feedback/2026-09-27-wizel-pressure.md).

대상: Characters/Data 및 Runtime, Battle/Data와 기존 전투 서비스, BattleUIController, SkillMakerWindow, WizzelDB와 실험실 위젤 데이터/스킬. 압력 수치는 세이브나 CharacterData 자산에 쓰지 않는다. 후열 비활성화/모듈 전환에서는 유지하고 전투 경계에서만 초기화한다.

원본 Aseprite 확인: 64×64, step0~4 태그, 현재 각 1프레임/0.1초. 실제 Unity 임포트 산출물에서 Sprite/AnimationClip fileID를 확인해 참조한다. 프레임을 추가했을 때 정의 Inspector의 원본 애니메이션 프레임 갱신으로 UI용 프레임을 다시 굽는다. 런타임 AssetDatabase/RenderTexture/전체화면 애니메이터는 사용하지 않는다.
