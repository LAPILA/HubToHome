# Trigger rules are catalog-driven and Action Sequences orchestrate finite flow

## Status

Accepted on 2026-07-12.

## Context

The first Battle Event Rule implementation uses a fixed enum and event-specific fields. Adding a new event requires changes across data, parser, writer, evaluator, editor, and tests. This conflicts with the project's requirement that new encounter, module, save-memory, overworld, and minigame situations can be composed without expanding a central switch.

At the same time, making Action Sequences a general visual programming language would move continuous gameplay, per-frame input, physics, and AI into an editor grammar that non-programmers cannot reason about safely.

## Decision

- New Trigger Rules use stable Scenario Event IDs, typed payloads, catalog-backed Conditions, explicit timing, explicit repeat scope, and a target Action Sequence.
- Event and Condition metadata use deterministic YAML-backed Trigger Library sources.
- Domain systems remain responsible for emitting typed events through adapters. No unrestricted global string event bus is introduced.
- Existing fixed Battle Event data migrates through compatibility mapping until source and runtime assets are verified.
- Action Sequences support finite orchestration: order, parallel policies, conditions, choices, bounded repetition, waits with completion semantics, typed sequence calls, and named outcomes.
- Continuous gameplay loops remain inside Game Modules.
- Arbitrary expressions, unbounded loops, per-frame polling, and recursive sequence calls are rejected.

## Consequences

- Trigger Rule data, source sync, validation, runtime evaluation, and editor UI require a coordinated migration.
- New domain events still require a typed publisher and adapter, but new combinations of existing events and Conditions do not require a new central enum member.
- Sequence inputs and parameter bindings require typed validation and discoverable value sources.
- Game Module outcomes become the normal bridge from continuous gameplay back into scenario orchestration.
- Rule simulation and execution diagnostics can explain which Condition matched and which timing checkpoint is pending.
