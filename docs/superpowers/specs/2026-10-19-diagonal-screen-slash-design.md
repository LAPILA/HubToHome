# 실험실 사선 화면 참격

사용자 승인: 실시간 사선 분할 방식, 성능·발열 부담 최소화 우선. 토끼 전투 실험실의 위젤에게 스킬1개를 추가한다. 본편 DB/저장/시나리오 문법은 변경하지 않는다.

기존 이동→준비→공격→Action_Damage 뒤에 표시 전용 Action_ScreenSplit 블록을 실행한다. 양쪽 이동22기준픽셀(전체검정틈44),0.18초 확대→1초 유지→0.3초 복원(총1.48초)이다. 확대/복원은 cubic smoothstep이고 Begin(initialStrength:0)으로 최초 최대폭 번쩍임을 막는다. ExpandDuration/HoldDuration을 명시하고 기존Duration은 복원 시간으로 보존한다. 잔상은 별도 캡처가 아니라 실시간 분할 유지다. 틈 내부는 최종 검정 마스크로 유지하고 바깥 양쪽 경계에 흰 외곽선1.25픽셀을 그린다. 최대폭 도달 전후0.16초씩의 두께 펄스(.65강조, 최고2.0625픽셀)를 적용하며 색상/알파/두께/강조량은 데이터로 편집한다. LineColor/LineWidthPixels 직렬화명은 유지한다. HUD/QTE/대사·전투 판정·물체 위치는 바꾸지 않는다.

URP17.3 RenderGraph의 게임 카메라 색상만 단일 전면 셰이더 패스로 읽어 임시 색상 타깃에 쓴 뒤 cameraColor를 교체한다. 명시적 화면 복사 패스를 추가하지 않는다. 추가 카메라·스크린샷·블러·깊이/노멀/모션벡터·노이즈 텍스처는 사용하지 않는다. 효과 비활성 시 패스 등록/중간 타깃 요구/재질 생성은 하지 않는다. 최초 사용 때만 재질을 만들고 반복 사용한다. 실험실 Renderer2D에만 기능을 등록한다.

화면 소유권은 카메라별 토큰이다. 새 효과가 같은 카메라를 대체하면 이전 토큰의 정리가 새 효과를 지우지 않는다. 블록은 pause에 맞춰 시계를 멈추고 finally에서 자기 토큰을 반납한다. 소유자 파괴/비활성/장면 이탈은 렌더 조회와 장면 이벤트에서 정리한다. RenderGraph가 프레임 임시 타깃을 관리하고 재질은 RendererFeature.Dispose에서 해제한다. 효과 실패는 기존 피해를 되돌리거나 턴을 잠그지 않는다.

구현 검토에서 확인한 경계: Renderer2D는 requiresIntermediateTexture를 소비하지 않으므로 활성 패스만 Color 입력을 요청한다. 이2D 경로는 별도 CopyColor 없이 중간 attachment를 만든다. Shader를 feature가 직접 참조하므로 .mat 자산은 만들지 않는다. 마지막 대상을 죽인 뒤에도 표시하려고 SkillActionBlock.CanRunWithoutLivingTargets(false 기본, ScreenSplit만true)를 일반/적/기존/시나리오 실행기에 동일 적용한다. 일반 피해/이동 블록의 중단 규칙은 유지한다. Turn QTE 스킬 콜백은 현재 action scope 참조까지 검사해 Abort/Exit/새 행동에서 무효화한다. 시나리오 문법·Action ID는 그대로다.

Odin에서 각도·이동 폭(기준픽셀)·비틀림·시간·절단선 색/두께를 조절한다. Point 샘플링으로 블러는 방지하되 회전/왜곡 중 원본 픽셀의 완전한1:1 대응은 보장하지 않는다. 실제 기기 측정 없이 무발열·무프레임드롭을 선언하지 않는다.

연결은 새 SkillData, 실험실 front.asset의7번째 DefaultSkill, 콘텐츠 카탈로그, 샘플 복원 Builder다. 원소 테스트18개는 보존하고 연출 스킬1개를 별도 검증한다. 현재 프로젝트는 RenderGraph Compatibility Mode=0이며 이 설정을 임의 변경하지 않는다. Unity Play/Refresh/씬 저장/커밋/푸시는 하지 않는다.
