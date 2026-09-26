# 근접 전조와 패링 시작점 일치

## 요청·확인

근접 공격은 Telegraph 프리팹이 보이는 순간부터 Z 패링을 허용하고 입력 반응을 보강한다. 기존 Unity 실행 테스트 금지, 서브 에이전트 금지, 커밋/푸시 금지를 유지한다.

정적 재현: 기본 전투 보정의 준비 전조는 타격 0.34초 전, Z 성공 구간은 0.24초 전이다. 따라서 타격 0.30초 전에 전조를 보고 누른 Z는 거부되고 유지 시 일반 방어만 된다. `BattleInput_DodgePreparationPrecedesJustGuardPing` 검사도 이 기존 동작을 명시한다. 또한 ExecuteParry는 판정 전 0.22초 대기만 만들므로 입력 성공을 즉시 알아보기 어렵다. Unity에서 화면 현상을 직접 재현한 것은 아니다.

## 구현 결정과 순서

진단·브레인스토밍·구현 계획·Unity 스킬 기준으로 표시와 판정이 같은 정책을 사용하게 한다. 사용자 요청이 정한 범위로 바로 구현하며 별도 설계 승인/커밋/에이전트 절차는 생략한다.

- [x] DefenseJudgementPolicyTests에 근접 첫 전조 입력·조기 입력 후 재시도·난이도/짧은 공격·원거리/C 유지 경계 검사 추가.
- [x] DefenseJudgementPolicy.WithBattleAssistance에 근접 옵션 추가: Z 최소 0.40초, 더 넓은 X 준비 구간/기존 저스트 구간 보존. 근접 Z 성공과 준비/강조 신호의 시작점을 일치시킨다.
- [x] QTEManager.StartBattleDefenseWindow의 useMeleeParryAssistance 기본값 true. Action_Projectile은 ExecuteImpact(isProjectile:true)를 통해 완화를 끄고, 기존 원거리/광역 fallback도 명시적으로 끈다. 일반 QTE 동작/스킬 데이터 직렬화는 변경하지 않는다.
- [x] PlayerController의 수락된 근접 Z 입력에만 약한 로컬 색 반응. 실제 패링 애니메이션/VFX/AP 보상은 기존 충돌 프레임에서 한 번 실행한다. 전조 전 새 입력은 소모하지 않고, 미리 유지한 Z는 일반 가드다.
- [x] CLI 컴파일·diff/UTF-8 정적 검증, 전투 규칙/사용법 갱신. Unity 테스트는 코드 추가/컴파일만 하고 실행하지 않는다.

피해를 과거로 되돌리거나 공격 타이밍을 조기 종료하지 않는다. 카메라 흔들림/전역 히트스톱/상시 화면 점멸은 추가하지 않는다. 완화 구간도 공격 전체 길이로 제한하며 기존 0.06초 늦은 입력 유예와 타격별 입력 소유권은 그대로다.

## 변경 파일·검증

- 런타임: `DefenseJudgementPolicy.cs`, `QTEManager.cs`, `SkillActionBlocks.cs`, `BattleTurnQteModuleControllerService.cs`, `PlayerController.cs`.
- 검사 코드: `DefenseJudgementPolicyTests.cs`, `QTEManagerDefensePipelineTests.cs`. 후자는 실제 StartBattleDefenseWindow 첫 프레임에서 입력/전조는 수락하되 결과/적 QTE 패널은 아직 발생하지 않는 계약을 추가한다. 원거리 옵션 false와 비교한다.
- 문서: `CONTEXT.md`, `RuleFileforAI/battle.clinerules`, `docs/bunny-slime-battle-lab.md`, AIAssets 일지/인덱스/본 문서.
- 기존 기본값 사례는 0.34초 전 준비 신호와 0.24초 Z를 분리했으나, 새 근접 보정은 준비/강조/Z 모두 0.40초가 된다. 0.6초 X 설정은 Z와 표시도 0.6초로 보존한다. Counterable에는 적용하지 않는다.
- Runtime+Editor CLI 빌드 오류 0 / 기존 ConfigPanelUI 미할당 필드 경고 2. UTF-8 및 diff 정적 검사. Unity Play/EditMode 테스트·Refresh·재임포트·씬 저장 명령은 실행하지 않았다. 실제 화면 체감과 기기 입력 지연은 사용자 확인이 필요하다.
- 씬/프리팹/음원/캐릭터 DB 수정 없음. 전조의 기존 START 클립과 핑 음원을 사용하며 추가 연결은 없다. 단, 예고 전용 다음-턴 텔레그래프 애니메이션 자체를 방어 입력창으로 바꾸지는 않았다.

브랜치 `codex/gameplayEdit`. 앞선 중앙 교전 변경을 보존하며 커밋/푸시는 하지 않았다.
