# AIAssets Index

`AIAssets` is the durable collaboration memory for HubToHome. It is read by humans and AI agents before and after meaningful work.

## Required Use

Before work:

1. Read this file.
2. Read `context-briefing.md`.
3. Read `architecture.md`.
4. Read `todo.md`.
5. Read the latest `YYYY-MM-DD-update.md`.
6. Read any relevant feedback or patchnote under `yjlim/`.
7. If touching scenario YAML, Action Sequences, Battle Scenario Data, Encounter Definitions, Action Catalog entries, generated scenario ScriptableObjects, or the scenario editor, read `../.agents/skills/hubtohome-scenario-authoring/SKILL.md`.

After work:

1. Update or create `YYYY-MM-DD-update.md`.
2. Add a human-readable document under `yjlim/feedback/` when the work is analysis, review, architecture, diagnosis, or handoff.
3. Add a human-readable document under `yjlim/Patchnote/` when the work is a patch-note style summary.
4. Update `CONTEXT.md` if terminology changed.
5. Update `RuleFileforAI/` if coding or system-ownership rules changed.
6. Update `../.agents/skills/hubtohome-scenario-authoring/` if scenario authoring rules, YAML fields, actions, validation, editor UX, import/export, or runtime adapters changed.

## Update Note Template

Use this structure for `AIAssets/YYYY-MM-DD-update.md`.

```md
# YYYY-MM-DD 업데이트

> 작업 단계: analyze / plan / implement / verify
> 목적: 한 문장으로 작업 목적

## [변경]

- 변경한 내용
- 관련 파일

## [의도]

- 왜 이 작업이 필요했는지
- 기존 구조와 어떤 관계인지

## [검증]

- 실행한 테스트, 빌드, 정적 검사, 수동 확인
- 실행하지 못한 검증과 이유

## [위험 / 후속]

- 남은 위험
- 다음 작업자가 이어볼 지점
```

## Current Documents

- `architecture.md`: current architecture notes.
- `context-briefing.md`: next-session briefing and system observations.
- `todo.md`: central task and risk list.
- `milestones.md`: broad milestone tracking.
- `../docs/ai-collaboration-guidelines.md`: shared human/AI workflow for conflict prevention and handoff.
- `../.agents/skills/hubtohome-scenario-authoring/`: shared AI skill for scenario authoring and sync rules.
- `yjlim/feedback/`: readable analysis and review documents.
- `yjlim/Patchnote/`: readable patch-note summaries.
