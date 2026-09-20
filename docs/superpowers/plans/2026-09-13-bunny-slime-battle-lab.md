# BunnySlime 종합 전투 샘플 구현

## 목표와 범위

사용자가 지정한 공격하지 않는 BunnySlime 더미와 기존 TestMap은 보존한다. 독립 개발씬에서 원본 그림을 재사용한 전투 샘플을 제공한다. 새 전투 시스템을 만들기보다 기존 공용 Host, GameInput, 스킬 블록, Scenario Source, 대사·카메라·DOTween을 조합한다.

구성: 종합 전투 / 가드·회피 연습 / 연계 반격 연습 / 3+3 후열 진입 확인. 샘플 파티 6명은 현재 있는 Player_Base 외형을 공유하고 이름·색·스킬 역할만 구분한다. 회복 아이템과 기초 장비, 일반·다단·전체·투사체·상태이상·특수반격 공격, 오프닝·HP 페이즈·종료 대사를 포함한다. 아직 미구현인 다른 전투 모듈이나 장비 패시브까지 구현됐다고 표시하지 않는다.

## 안전 경계

- 생성·열기는 Edit Mode 메뉴 한 곳에서만 명시적으로 실행한다. 실행 중 씬 교체/자동 생성/자동 Play/Refresh/전체 QA 재생성은 하지 않는다.
- 생성 자산은 `Assets/_Game/Content/Maps/Development/BunnySlimeBattleLab`에 모은다. 기존 자산은 복제하거나 참조만 한다. 기존 생성 결과의 사용자 편집은 재생성으로 덮지 않는다.
- 시나리오 YAML을 원본으로 두고 기존 importer/validator로 Runtime Asset을 생성한다.
- 독립 fresh Play 전용이며 저장점·자동저장·로드 진입점을 만들지 않는다. 원본 파티·카탈로그의 데이터 구조를 덮어쓰지 않는다.
- Unity 실행/Play/테스트는 하지 않고 정적 컴파일·소스 계약 검증만 한다. 생성기 실행/실기 화면은 사용자 확인 대상이다.

## 담당과 구현 순서

- [x] Root: 샘플 데이터 계약, 스킬·대사·아이템/장비 제작, 시나리오 YAML, 생성기 통합·정적 검증·인계.
- [x] Enemy 작업: 샘플 순차 AI, 기존 EnemyCharacter 스킬 선택 확장 지점, 원본 기반 전용 prefab/Animator 생성.
- [x] Entry 작업: 공용 프리팹 기반 독립 씬 생성, 샘플 시작/재도전 UI와 파티 초기화, 세이브 경계.
- [x] Cross review: 참조·실제 지원 블록/트리거·취소/결과 흐름·기본 수치 대조.

시나리오 원본은 기존 경로 정책에 맞춰 `Assets/_Game/Content/Scenarios/Source/Battle/BunnySlimeBattleLab`에 둔다. Runtime 생성물만 LabRoot에 모은다. 실제 생성/Play는 사용자 실행 대상으로 남긴다. 상세 인계: `docs/bunny-slime-battle-lab.md`.

원본에 Attack/Telegraph trigger가 없고 BunnySlimeCharacter는 항상 Wait하므로 단순 SkillList 추가로 끝내지 않는다. 상태이상은 현재 방어와 무관한 독립 기믹으로 명확히 고지하거나 아군 공격/자기 버프로 시연한다. 투사체의 실제 비행과 방어 판정 시각이 어긋나지 않도록 기존 블록 실행 계약에 맞춘다.
