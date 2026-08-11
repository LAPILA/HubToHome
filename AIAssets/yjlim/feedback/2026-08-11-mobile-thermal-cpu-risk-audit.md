# 모바일 발열·CPU 정적 감사

## 범위와 전제

- 이번 결과는 코드와 프로젝트 설정을 기준으로 한 정적 감사다.
- 실제 기기 발열, CPU/GPU 병목, 프레임 유지 시간은 아직 측정하지 않았다.
- 3+3 후열은 `GameObject.SetActive(false)` 상태라 대기 중 `Update`, 물리, Animator CPU 비용은 발생하지 않는다. 대신 캐릭터 3개의 메모리는 전투 시작부터 유지한다.

## 우선순위

### P0 — 모바일 프레임 상한 정책

- 기본값은 60 FPS지만 설정 범위가 30~240 FPS이고 VSync 기본값은 꺼져 있다.
- Android에서도 같은 설정 행과 상한을 사용하므로 사용자가 90~240 FPS를 저장할 수 있다.
- 고주사율을 허용하면 게임 로직이 가벼워도 CPU/GPU가 가능한 만큼 계속 일해 발열과 배터리 소모가 커질 수 있다.
- 권장: 모바일은 30/60만 노출하고 기본 60, 절전 옵션 30으로 제한한다. PC 범위와 모바일 범위를 분리한다.

근거:

- `GameConfigManager.cs`: `DefaultTargetFps = 60`, `Application.targetFrameRate` 적용
- `GameConfigPolicy.cs`: 허용 범위 30~240
- `ConfigPanelUI.cs`: System 설정에서 Target FPS를 30씩 증감

### P0 — CharacterGhostTrail 생성량과 수명 누수

- 주요 플레이어·적 Prefab에 모두 붙어 있다.
- Player Prefab의 생성 간격은 0.01초라 활성 중 초당 최대 100개 SpriteRenderer/DOTween 작업을 만든다. 수명 0.4초 기준 동시에 약 40개 잔상이 살아 있을 수 있다.
- DOTween 기본 재활용이 꺼져 있어 짧은 Tween 생성이 GC 압력을 만들 수 있다.
- `Awake()`가 캐릭터마다 별도 루트 `GameObject`를 Scene root에 만들고, 캐릭터 파괴 시 이 루트를 정리하는 `OnDestroy()`가 없다.
- 3+3 후열도 Instantiate 직후 Awake를 거치므로 비활성 대기 전에 빈 GhostPool root를 만든다. 전투 반복 시 root와 생성된 잔상이 남을 수 있다.
- 권장: 풀을 캐릭터 자식으로 두거나 `OnDestroy()`에서 명시 정리하고, 비사용 중 컴포넌트를 disable한다. Player 간격은 0.03~0.05초부터 시각 비교하고 동시 잔상 상한을 둔다.

### P1 — 오버월드 적의 FixedUpdate/Animator 중복 작업

- 적 하나마다 50 Hz `FixedUpdate` 순찰과 매 프레임 `LateUpdate` 정렬을 수행한다.
- 이동 애니메이션 갱신에서 `EnemyCharacter.SetOverworldMoving()`이 Animator 파라미터 존재 여부를 매번 3회 검색한 뒤 값을 쓰고, `OverworldEnemy`가 같은 파라미터를 다시 쓴다.
- Animator Prefab의 Culling Mode가 `Always Animate`라 화면 밖 적도 Animator를 계속 평가한다.
- 적 수가 늘어날수록 비용이 선형 증가한다.
- 권장: Animator 파라미터 존재 여부를 Awake/Setup에서 캐시하고 중복 Set을 한 소유자로 합친다. 화면 밖 적의 Animator culling과 순찰 sleep 정책을 적용한다. 정렬은 Y가 바뀔 때만 갱신한다.

### P1 — 모바일 URP의 상시 GPU 기능

- Mobile RP는 Render Scale 0.8과 SRP Batcher를 사용해 기본 방향은 좋다.
- 반면 HDR, Main Light Shadow, 추가 광원 Per Pixel 4개, Gameplay Camera Post Processing이 켜져 있다.
- 현재 2D 게임 화면에서 이 기능이 항상 필요한지는 확인되지 않았다. 불필요하면 메모리 대역폭과 GPU 시간을 계속 사용한다.
- 권장: Mobile Low 프로필에서 HDR/그림자/Post Processing을 끄고, 연출이 필요한 장면만 별도 품질 또는 카메라/Volume 정책으로 켠다. 먼저 GPU Profiler와 RenderDoc/Frame Debugger로 효과별 비용을 측정한다.

### P1 — 공용 ObjectPool의 무제한 유지와 첫 사용 예열

- VFX 종류를 처음 쓸 때 기본 10개를 한 번에 Instantiate한다. 전투 중 첫 사용 프레임에 스파이크가 생길 수 있다.
- 풀 상한과 축소 정책이 없고 `DontDestroyOnLoad`라 세션 동안 사용한 모든 VFX 인스턴스를 유지한다.
- 키가 asset ID가 아니라 `prefab.name`이라 같은 이름의 다른 Prefab이 충돌할 수 있다.
- `CharacterVFX.Play()`는 매 Spawn마다 `GetComponentsInChildren<AudioSource>(true)` 배열을 만들고, 풀에서 재사용할 때마다 volume multiplier를 다시 곱한다.
- 권장: Prefab instance ID 기반 키, 종류별 prewarm 수, 최대 보관 수, Scene/전투 종료 trim 정책을 둔다. AudioSource 목록과 원본 볼륨은 pooled component에서 1회 캐시한다.

### P2 — 상시 또는 반복 폴링

- `InteractionSystem`: 이동 가능 중 매 프레임 NonAlloc OverlapBox 1회와 맞은 Collider의 `GetComponent<IInteractable>()`를 수행한다. GC는 억제했지만 모바일에서 플레이어 정지 중에도 계속 돈다.
- `UIResolutionRefreshService`: 매 프레임 화면 크기 비교 자체는 작지만 Scene 로드마다 모든 비활성 TMP까지 탐색하고 Mesh/Canvas를 강제 재생성한다. 지속 발열보다 로딩 hitch 후보다.
- `DialogueUI`: 타이핑 중 매 프레임 Typewriter 속도를 다시 적용한다. 설정 변경 이벤트 또는 노드 시작 시 적용으로 줄일 수 있다.
- `VFXAutoDespawn`: 활성 VFX마다 매 프레임 전체 ParticleSystem hierarchy의 `IsAlive(true)`를 검사한다. 많은 VFX가 동시에 켜질 때 측정 대상이다.
- `PeriodicHazardController`: 위험물 하나마다 매 프레임 주기를 계산한다. 개수가 적으면 무시 가능하지만 대량 배치 전에는 중앙 tick 또는 다음 전환 시각 기반으로 바꾸는 편이 안전하다.

## 현재 문제로 보지 않는 항목

- 전투 UI, 설정 UI, 상점, 대화 입력의 대부분 `Update()`는 화면이 닫혀 있거나 상태가 아니면 즉시 반환한다.
- `CheatManager`는 `UNITY_EDITOR` 전용이라 모바일 빌드에 들어가지 않는다.
- `PerformanceMonitor`는 현재 Scene/Prefab 참조가 없어 자동으로 실행되지 않는다.
- 3+3 후열 자체는 비활성 상태라 대기 CPU 비용은 없으며, 문제는 추가 메모리와 GhostTrail Awake side effect다.

## 권장 실행 순서

1. 모바일 FPS 옵션을 30/60으로 제한한다.
2. CharacterGhostTrail 수명 누수·생성 간격·동시 상한을 수정한다.
3. 오버월드 적 Animator 파라미터 캐시, 중복 갱신 제거, offscreen culling을 적용한다.
4. ObjectPool에 종류별 예열/상한/trim 정책을 추가한다.
5. 실제 Android 기기에서 GPU 옵션을 A/B 측정한 뒤 Mobile RP를 줄인다.

## 기기 검증 기준

- Android IL2CPP Development Build + Autoconnect Profiler
- 동일 기기, 동일 밝기, 충전 분리, 60 FPS와 30 FPS 각각 10분
- 구간: 오버월드 적 밀집 Room 5분, 3+3 전투와 잔상/VFX 반복 5분
- 기록: CPU Main Thread/Render Thread, GPU frame time, GC Alloc/frame, Batches/SetPass, 메모리, 평균 FPS와 1% low
- 열 센서 값은 기기 API가 제공할 때만 기록하고, 없으면 시작/종료 표면 온도와 thermal throttling 발생 시점을 별도 기록한다.
