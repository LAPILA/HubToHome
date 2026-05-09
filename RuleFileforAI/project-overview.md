# HubToHome 프로젝트 개요

## 컨셉
- 2D 탑다운 탐색 + 2.5D 연출형 턴제 RPG.
- Undertale/Deltarune의 대화 감성, 키보드 조작, 선택/플래그 구조를 참고한다.
- Limbus Company식 전투 카메라와 타격 연출을 참고한다.
- 적 턴에는 패링/회피/점프 QTE로 반응형 방어를 구현한다.

## 기술 스택
- Unity 6000.3.8f1, URP.
- DOTween, Odin Inspector, Febucci Text Animator, Cinemachine, TextMesh Pro 사용.

## 주요 폴더
```text
Assets/_Game/
├── Core        # 전역 상태, 저장, 씬, 오디오, 풀링
├── Dialogue    # DialogueData, DialogueManager, SpeakerData
├── UI          # 대화/전투/타이틀/입력 UI
├── Overworld   # 이동, 상호작용, 트리거
├── Battle      # 전투 흐름, 상태, 포지션, 스킬
├── Characters  # 플레이어/적/상태이상/데이터
├── Items       # 아이템 데이터/인벤토리
├── Vfx         # 캐릭터/VFX 보조
└── Scenes      # 씬 파일
```

## 현재 큰 흐름
- 타이틀/인트로 → 이름 입력 → 오버월드 → 대화/트리거 → 심리스 전투 또는 BattleScene → 결과 복귀.
- 데이터 보존은 `GlobalDataManager`를 중심으로 한다.
- 조작 가능 여부는 `GameStateManager`를 기준으로 판단한다.