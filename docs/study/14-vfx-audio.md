# VFX 잔상 전조 바람과 오디오 수명

[학습 홈](README.md) · [전체 코드 색인](20-code-atlas.md) · [이 영역의 모든 파일](code/vfx.md)

연출은 눈에 보이는 효과뿐 아니라 **재사용·중단·복구 비용**까지 포함합니다. VFX 폴더와 Core의 풀·오디오를 연결해서 읽으면 좋은 학습 사례가 됩니다.

## 파일별 역할

| 파일 | 핵심 |
| --- | --- |
| VFX/Runtime/BattleTelegraphCue.cs | 전조 START 애니메이션, 타격 시계, 핑, 반환 |
| CharacterGhostTrail.cs | 이동 잔상 생성·정리 |
| CharacterVFX.cs | 캐릭터 효과 연결 |
| VFXAutoDespawn.cs | 반복 효과의 종료와 반환 |
| PixelFoliageWind.cs | 스프라이트·타일맵의 바람 셰이더 파라미터 |
| Core/Runtime/ObjectPoolManager.cs | 반복 객체의 풀 |
| Core/Runtime/AudioManager.cs | BGM·SFX·환경음 라우팅과 수명 |

## 전조는 별도 타이머와 경쟁하면 안 된다

BattleTelegraphCue는 방어의 남은 타격 시간을 SynchronizeToImpact로 받습니다. 프리팹의 START 애니메이션과 소리를 시작하고, 타격·취소·비활성화에서 소유한 상태를 정리합니다. 전조가 남는 버그는 재생 속도만의 문제가 아니라 누가 반환하는가의 문제이기도 합니다.

레이어는 캐릭터를 기준으로 뒤에 놓는 정책을 갖고, 제작한 원본 스케일을 보존합니다. 오래된 스케일 필드가 호환용으로 남아 있다고 새 튜닝 경로로 사용하지 마세요.

## 풀의 세 자료구조

ObjectPoolManager에는 prefab→PoolState Dictionary, instance→owner Dictionary, 사용 가능한 객체 Queue와 중복 반환 검사용 HashSet이 있습니다.

- Queue: 다음 재사용 객체를 꺼냅니다.
- HashSet: 같은 객체를 두 번 반환하지 않게 합니다.
- owner Dictionary: 다른 풀·다른 Manager의 객체를 섞지 않습니다.
- 보관 상한: 사용하지 않는 객체가 계속 쌓이는 것을 제한합니다.

꺼낸 객체는 위치·회전·부모·활성을 초기화합니다. 개별 효과는 색·타이머·Tween·Animator 상태까지 스스로 복구해야 합니다. 풀에 넣었다는 이유로 상태 초기화가 자동 해결되지는 않습니다.

## 바람과 MaterialPropertyBlock

PixelFoliageWind는 SpriteRenderer / TilemapRenderer를 구분하고 변경 이벤트에서 Refresh합니다. MaterialPropertyBlock으로 renderer별 값을 전달합니다. 매 개체마다 material을 복제하는 접근과 다른 비용 특성이 있습니다.

그러나 MaterialPropertyBlock을 쓴다고 무조건 배칭이나 SRP 성능이 좋아지는 것은 아닙니다. 현재 셰이더·URP 렌더러의 실제 드로우콜을 Frame Debugger에서 확인해야 합니다. 셰이더 자체는 Scripts 밖 자산이므로 이 색인의 C# 해설 범위와 구분합니다.

## 오디오도 범위가 있다

AudioManager는 두 BGM 소스를 통해 교차 전환하고 환경음 채널을 분리합니다. SuppressAmbience의 IDisposable 범위는 상점처럼 잠시 환경음을 숨길 때 사용합니다. 여러 소유자가 억제 중이면 하나가 끝났다는 이유로 모두 복원하면 안 됩니다.

## 공부 과제

- [ ] 같은 객체를 두 번 Despawn했을 때 Queue에 두 번 들어가면 생기는 현상을 설명한다.
- [ ] 이동 잔상과 공격 정지 자세의 잔상을 구분하는 조건을 찾는다.
- [ ] 전조를 타격 전에 취소하고 같은 풀 객체를 재사용했을 때 남으면 안 되는 값을 적는다.
- [ ] 환경음 억제 A/B 중 A만 끝났을 때 기대 상태를 그린다.

테스트: ObjectPoolManagerTests, CharacterGhostTrailTests, AudioManagerBgmStateTests, AudioManagerRoutingTests.
