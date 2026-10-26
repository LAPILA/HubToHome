# 프롤로그 오른쪽 동선·즉시 확인 Implementation Plan

> **For agentic workers:** 사용자의 기존 승인 흐름 수정이다. 파일 소유권을 나눠 구현하고 독립 검토한다. 커밋/푸시/Play/엔진 NUnit/사용자 저장 I/O는 하지 않는다.

**Goal:** 첫 수리칸에서 시작해 공용칸→사건칸→제어칸으로 오른쪽 진행하고, 전용 프롤로그 대사는 Z 한 번마다 다음 문장으로 넘긴다.

**Architecture:** 기존 Room ID/enum/완료 플래그/그림은 보존한다. 문6개와 Spawn/재개 위치를 순방향으로 재연결하고 봉쇄 후 강제 역이동을 제거한다. DialogueData의 기본 false인 즉시표시 옵션만 전용 대사에 적용해 일반 NPC 기본 동작을 유지한다. 선택지/페이지/새 대사 첫 프레임 입력 방어는 보존한다.

**Tech Stack:** 기존 Unity6, C#, uGUI/TMP, DOTween/Odin, 기존 manifest 기반 좁은 자산 패치.

## Chunk 1 · 동선

- [x] 실제 문/재개 위치로 왼쪽 진행과 봉쇄 후 강제 복귀를 검출한다.
- [x] RoomFactory/Controller.Presentation/Progress를 수정한다. Repair→Common→Rear→Control, 모든 전진문 x>0/도착 x<0, 봉쇄 후 현재 Rear 유지, 완료 저장의 안전 재개도 같은 방향이다.
- [x] 전용 RoutingPatch로 실제 프리팹/프로필만 수정한다. 기존 타일/상자·다른 Room 구조·저장 플래그는 보존한다. 전체 생성기는 실행하지 않는다.

## Chunk 2 · 확인 입력

- [x] DialogueData 즉시표시 옵션과 Manager/UI 연결. 전용 프롤로그27개만 true, 일반 대사 기본 false. 타입라이터 대기 없이 Z1회당 페이지/문장1단계, 선택지/첫프레임 guard 유지.
- [x] 팩토리의 새 생성 기본값과 실제27개 대사 참조를 함께 갱신한다. 기관사 최초/반복 안내의 왼쪽→오른쪽은4언어를 동일하게 수정한다.
- [x] 전체 CLI/관리형 회귀/native 읽기 검사, manifest/메타/기존 Scene·타일·상자 보존 확인. actual Play는 별도 미검증으로 남긴다.
- [x] AIAssets/제작 안내/관련 문서 동선 갱신 및 독립 검토.

최종: Runtime463/Editor382 CLI 오류0, 대화 관리형24사례62단언, 진행14검사+2,048조합, native6문/12재개/27즉시본문 확인. 메타1,680개/생성기록166해시·Q0기존 타일·상자/두 Scene 보존 확인. 실제 Play·사용자 저장 I/O·커밋·푸시는 하지 않았다.

## 후속 오류 수정 · 2026-10-24

- [x] 사용자 로그의 ReadObjectThreaded169회를 추가한 일반 에셋 검사 경로로 추적. Scene2개를 해당 로더에서 제외하고 Scene dirty/manifest 보호는 유지한다.
- [x] RoutingPatch의 대사·정의·Prefab은 실제 변경 시에만 저장한다. 전체 생성기/그래픽 설정은 변경하지 않는다.
- [x] 실제 검사 메서드 관리형 회귀9사례27단언 RED→GREEN, 전체 CLI 오류0, 콘텐츠166해시/두 Scene 보존·diff 공백 검사 통과.
- [ ] Unity 엔진에서 경고 재발과 D3D12 fence 대기 오류를 확인한다. 이번에는 native 패치·Refresh·Play·재시작을 수행하지 않았으며 D3D12 원인은 미확정이다.
