# 기능별 코드 따라 읽기 Implementation Plan

> For agentic workers: execute the approved documentation plan with executing-plans. No game changes, Unity execution, commits or pushes.

**Goal:** 기존 Notion 학습실에 실제 호출 흐름과 코드 근거를 갖춘 심화 해설 자료실을 추가한다.

**Architecture:** 기존 입문/색인은 그대로 둔다. 새 심화 장은 단일 기능 흐름을 책임지며 색인에서 연결한다. 발췌는 소스 줄 범위로 생성하고 해시로 검증한다.

**Tech Stack:** C# 소스 읽기, UTF-8 Markdown, Node 검증/패키징, PowerShell ZIP, 로그인된 Notion의 네이티브 가져오기.

## Chunk 1 — 해설과 근거

공통 완료 규격: 현상/파일 → 호출 흐름 → 실제 발췌 해설 → 상태 추적 → 자료구조/선택 이유 → 실패 경계 → 실습/예상 결과 → 면접 질문. 사실과 설계 해석을 구분한다. 실습은 미실행이면 예상 결과로 표시한다. 근거는 HEAD가 아니라 미커밋 변경을 포함한 현재 작업 트리다.

- [x] `docs/study/deep/README.md`와 `00-reading-tools.md`: 읽는 순서, 문법/Unity 수명, 검증 경계.
- [x] `01-save-settings.md`: 저장 진입, 예약 자동저장, 검증 후 파일 교체, 설정 분리.
- [x] `02-battle-turn-defense.md`: 속도 일정, 대상 선택, 방어 입력과 수명, 후열 전환.
- [x] `03-skill-status.md`: 블록 타임라인, QTE, 자원, 속성 상태의 소유권.
- [x] `04-stats-equipment-shop.md`: 스탯 투영, 장비 소유 수량, 상점 롤백.
- [x] `05-world-interaction.md`: 탐색 후보, 상호작용, 방 후보 검증과 교체.
- [x] `06-dialogue-scenario.md`: 대사 대기/복원, Action 실행과 취소 배출.
- [x] `07-editor-undo-save.md`: UI Toolkit 메이커, 명령 기반 Undo, 충돌 감지 저장.
- [x] `08-ui-camera-effects.md`: 피해 합계, 이벤트 해제와 카메라 권한의 대표 흐름.
- [x] `09-pool-text.md`: 풀 자료구조와 텍스트 줄바꿈의 대표 흐름. 큰 장을 분리한다.
- [x] 각 장의 실제 소스/함수/발췌를 확인한다. 현재 구현에 없는 호출은 설명하지 않는다.

## Chunk 2 — 검증과 게시

- [x] `docs/study/build-deep-import.cjs`: 발췌 마커를 실제 코드로 치환하고 경로/펜스/링크/해시를 검사한다. 출력은 `Library/CodexChecks/StudyDeepImport`만 사용한다.
- [x] UTF-8 템플릿 `docs/study/deep/source/*.md.in`은 apply_patch로 작성하고 읽기용 `docs/study/deep/*.md`는 기계적으로 생성한다. 근거는 `docs/study/deep/source-evidence.json`에 문서/발췌 ID, 소스 경로, 시작/끝 줄, 파일 SHA-256, 발췌 SHA-256으로 기록한다.
- [x] 생성 산출물에 빈 장, 미해결 마커, 깨진 링크가 0개인지 확인한다. 실패하면 게시하지 않는다.
- [x] Unity/Git 루트에서 `node docs/study/build-deep-import.cjs` 실행 후 `node docs/study/build-deep-import.cjs --check`를 실행한다. UTF-8/범위/인용 불일치/해시 변경/링크 오류는 비정상 종료한다. PowerShell `Compress-Archive -LiteralPath`로 package 폴더를 `Library/CodexChecks/StudyDeepImport/HubToHome-CodeWalkthrough-20261012.zip`으로 묶는다. 게시 직전 --check를 다시 실행한다.
- [x] 새 심화 문서만 ZIP으로 가져온다. 기존 62개 문서/4개 CSV는 재가져오지 않는다.
- [x] 모든 새 페이지별 본문과 해당 표/코드/다이어그램을 화면에서 검증한다. 루트 `3f6641f6190180bb8567ff9caf83e5a9`의 자식으로 배치한다. 기존 본문/공유 권한은 보존하고 홈페이지에는 새 링크만 추가한다.
- [x] `docs/study/notion-publication.json`에 새 페이지/검증 결과를 추가한다.
- [x] `AIAssets/2026-10-12-update.md` 및 `AIAssets/index.md`에 범위와 한계를 기록한다. `git diff --check`와 문서 생성 검증을 실행한다.

검증 실패 시 원인을 수정한 뒤 다시 확인한다. Notion 응답이 불명확하면 재게시하기 전에 현재 페이지를 읽어 중복을 방지한다.
