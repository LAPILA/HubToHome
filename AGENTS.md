# AGENTS.md — yjlim Global Codex Instructions

This file provides global guidance to Codex. Project-specific rules are loaded from the nearest repository `AGENTS.md`; when working under `D:\DL`, the DL project rules take precedence.

## 사용자 기본값

- 사용자: `yjlim`
- 기본 응답 언어: 한국어
- 소통 수준: C#/Unity 시니어 개발자와 대화하듯이 한다. 단순 구현 나열보다 구조, 영향 범위, 아키텍처 판단을 우선한다.
- 답변 스타일: 존댓말, 간결한 답변, 과한 감정 표현 금지. 느낌표·이모지·감탄사 반복을 사용하지 않는다.
- 표현 규칙: 사용자가 알아듣기 어려운 영어 표현을 불필요하게 쓰지 않는다. `caveat` 같은 단어는 `주의점`, `단서`, `전제`처럼 한국어로 풀어 쓴다.
- 최우선 원칙: 모르는 것을 추측하지 않는다. 확인된 사실만 말하고, 확인할 수 없거나 판단이 불확실하면 모른다고 밝히고 필요한 질문을 한다.

## DL 프로젝트 진입 규칙

`D:\DL` 또는 그 하위에서 작업할 때는 아래 파일들을 프로젝트 메모리와 지침으로 간주한다.

- 루트 지침: `D:\DL\AGENTS.md`
- 브랜치 지침: `D:\DL\KR\Trunk\AGENTS.md`
- 원문 전체 마이그레이션 메모리: `D:\DL\.codex\DL_PROJECT_MEMORY.md`
- 글로벌 원문 사본: `C:\Users\yjlim\.codex\memories\DL_CODEX_MIGRATION.md`
- 원본 핸드오버: `D:\DL\CODEX_MIGRATION.md`

DL 작업에서는 `D:\DL\AGENTS.md`와 더 가까운 `AGENTS.md`의 규칙을 따른다. 세부 내용이 누락되었거나 충돌하면 원문 마이그레이션 메모리와 현재 파일 상태를 확인하고, 그래도 불확실하면 사용자에게 묻는다.

### DL Obsidian/Architecture 세션 규칙

DL 작업에서 Obsidian Wiki는 선택 참고자료가 아니라 영구 프로젝트 메모리다. 매 세션 다음을 반드시 지킨다.

1. 작업 전 `C:\Users\yjlim\Documents\Obsidian Vault\DL\index.md`를 읽고 관련 도메인 지식을 확인한다.
2. 아키텍처, 기술 방향, 레거시 오답 노트, 상급자 핸드오버, 온보딩, 관리 판단이 관련되면 `Architecture/_index.md`와 `Architecture/Roadmap/Architecture - Roadmap.md`를 함께 읽는다.
3. 작업 중 가치 있는 분석, 결정, 미확정 질문, 패턴, 제약은 위키에 기록한다.
4. 작업 후 관련 `_index.md`, 루트 `index.md`, `log.md`를 갱신한다.
5. 위키 운영 규칙이 필요하면 `D:\DL\docs\obsidian-wiki.md`를 확인하고, 규칙 자체가 바뀌면 해당 문서도 갱신한다.

### DL Obsidian Synapse 규칙

- Obsidian은 단순 문서 저장소가 아니라 프로젝트 뇌의 노드 그래프다.
- 모든 작업 전후에 관련 노드, 상위 `_index.md`, 루트 `index.md`, `Tasks/index.md`, `log.md` 연결 상태를 확인한다.
- 새 사실, 결정, 미확정 질문은 채팅에만 남기지 않고 관련 노드에 기록한다.
- 새 폴더는 `_index.md`를 만들고, 새 문서는 관련 노드 양쪽에 wikilink를 연결한다.
- 작업 후 broken wikilink, 고립 노드, `_index.md` 누락을 검증한다.

## 보안과 인증 정보

- Jira/Confluence 등 인증 토큰은 채팅에 재인용하지 않는다.
- 인증은 지침에 적힌 토큰 값을 복사해 쓰지 말고, 프로젝트가 지정한 로컬 config 파일과 API 스크립트를 사용한다.
- 로컬 파일에 이미 존재하는 민감 정보는 필요한 범위에서만 읽고, 최종 답변에 노출하지 않는다.
- 권한/그룹 구성원 열거는 민감 작업으로 간주한다. 사용자가 해당 조회를 명시적으로 요청하거나 승인하지 않는 한 실행하지 않는다.
- 금지 예시는 `net localgroup administrators`, `net localgroup docker-users`, `Get-LocalGroupMember`, `whoami /groups`, `lusrmgr.msc`, 로컬/도메인 관리자 그룹 구성원 조회, 현재 사용자의 권한 그룹 열거다.
- Docker, WSL, Hyper-V, 서비스 문제를 진단할 때도 권한/그룹 구성원 목록을 우회적으로 확인하지 않는다. 필요한 경우 먼저 사용자에게 이유와 실행할 정확한 명령을 설명하고 승인받는다.
- 민감 작업 여부가 애매하면 실행하지 말고, 조회 없이 가능한 대안 진단부터 수행한다.

## Codex 운영 원칙

- 한글 파일을 읽거나 쓰기 전에 인코딩을 먼저 확인한다.
- 파일 수정 전에는 변경 대상과 이유를 짧게 설명한다.
- 독립적인 읽기·탐색 명령은 병렬 실행을 우선한다.
- Git은 보조 수단일 수 있다. 프로젝트가 SVN 기반이면 SVN 규칙을 우선한다.
- 사용자가 명시적으로 커밋을 요청하지 않으면 커밋하지 않는다.

## Harness Lessons / 실패-성공 기록

- 어떤 세션에서든 특정 작업의 첫 접근이 실패했고 다른 접근으로 성공했다면, 그 순간을 재사용 가능한 하네스 교훈으로 취급한다.
- 같은 실수를 반복하지 않도록 작업 종료 전 지속 메모리에 기록한다. 전역 기록 위치는 `C:\Users\yjlim\.codex\memories\HARNESS_LESSONS.md`다.
- DL 작업에서는 전역 기록과 함께 `C:\Users\yjlim\Documents\Obsidian Vault\DL\Codex\Operations\Codex - Harness Lessons.md` 또는 더 구체적인 관련 노드에도 반영한다.
- 기록 항목은 날짜, 작업 맥락, 실패한 접근, 관찰된 증상/오류, 성공한 접근, 적용 조건, 검증 방법, 관련 문서/파일 링크를 포함한다.
- 실패 원인이 확정되지 않았으면 사실과 추정을 분리해서 쓴다. 인증 토큰, 서버 비밀번호, 개인키, 권한/그룹 구성원 같은 민감 정보는 기록하지 않는다.
- 일회성 네트워크 흔들림처럼 재사용 가능한 절차가 없는 실패는 기록 대상이 아니다. 단, 우회 절차나 안정적인 재시도 조건이 확인되면 기록한다.
- Unity MCP, C# LSP/MCP, RAG, Browser, Atlassian, Docker/WSL, 서버 연결, 빌드/테스트, UTF/BOM/CP949 인코딩, 파일 잠금, 경로/권한 문제는 우선 기록 후보로 본다.
- 동일 계열 작업을 다시 시작할 때는 관련 하네스 교훈을 먼저 확인하고, 이미 실패로 기록된 접근을 반복하지 않는다.

## DL Encoding / 한글 문서 운영 규칙 (2026-05-13)

- 한글이 깨져 보이는 주원인은 대개 파일 손상이 아니라 PowerShell 콘솔 출력 코드페이지 또는 기본 디코딩 불일치다.
- 한글 파일은 읽기 전 UTF-8 유효성과 BOM 여부를 확인한다.
- Markdown/Obsidian 문서는 UTF-8 without BOM으로 유지한다.
- PowerShell 읽기는 `Get-Content -Encoding UTF8`, 쓰기는 `[System.Text.UTF8Encoding]::new($false)`를 명시한다.
- 콘솔 출력이 깨져 보였다는 이유만으로 원본 파일이 깨졌다고 판단하지 않는다.
- 상세 운영 노드: `C:\Users\yjlim\Documents\Obsidian Vault\DL\Codex\Operations\Codex - Encoding Policy.md`.
