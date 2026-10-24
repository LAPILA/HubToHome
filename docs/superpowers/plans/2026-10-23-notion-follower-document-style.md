# Notion 동료 동행 문서 디자인 Implementation Plan

> **For agentic workers:** REQUIRED: Use superpowers:subagent-driven-development (if subagents available) or superpowers:executing-plans to implement this plan. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 기존 내용을 보존하면서 신규 동료 동행 Notion 문서 3개를 읽기 좋은 매뉴얼로 정리한다.

**Architecture:** 원격 Notion 기본 요소만 편집한다. 본문은 유지하고 페이지별 구분색·커버·요약·목차를 적용한 뒤 실제 저장 여부를 확인한다. 브라우저 제어는 한 작업자가 소유하고 독립 검토만 위임한다.

**Tech Stack:** Notion, 인증된 브라우저 UI, CUA.

## Chunk 1: 승인 범위의 디자인 정리

**Files:** 디자인 명세와 이 계획 외 게임 파일 변경 없음. 완료 기록은 `AIAssets/2026-10-23-update.md`, `docs/study/notion-publication.json`의 이번 디자인 이력에만 한정한다.

- [x] 사용자 디자인 승인과 독립 디자인 검토.
- [x] 제작 사용법에 청록 계열 커버·아이콘·도입부와 제목 강조 적용. 새 제목 `동료 동행 · 제작 매뉴얼`, 아쿠아 낡은 페인트/green book open, 본문H1·인용 green.
- [x] 코드 학습에 보라 계열, 운영 현황에 금색 계열 적용. 새 제목 `동료 동행 · 코드 학습` / `동료 동행 · 구현·검증 현황`, 그라데이션5 보라/purple angle brackets solidus 및 무광 에이징 코퍼/yellow clipboard, 본문H1 purple/orange·인용 purple/yellow.
- [x] UI가 지원하면 기존 제목 기반 목차 추가. 네이티브 자동목차3개, 본문H1을 포함한 제목 링크13/12/8개 확인. 본문 날짜·옛 제목은 보존.
- [x] 새로고침 후 저장·계층·코드·표·링크·미실행 체크리스트 보존 확인. 부모 묶음에서 새 제목으로 재열기 후3페이지 저장 화면 확인, 운영 미실행8항목 모두 false 유지. 기존ID·계층·부모 묶음 제목·계정·권한·공유 불변. full-width는 미검증이며 성공으로 기록하지 않음.
- [x] 실제 결과 스크린샷3개 저장, 게시 기록에 적용 범위만 추가. [당일 후속 기록](../../../AIAssets/2026-10-23-update.md) · [게시 JSON](../../study/notion-publication.json)의 `overworldPartyFollow.visualDesign`과 `notion-follower-style/{guide,code-learning,operations}.jpg` 참조. 커밋·푸시하지 않음.
