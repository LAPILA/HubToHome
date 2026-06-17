# Scenario Authoring Editor UX Design

> Date: 2026-06-18
> Phase: implement
> Owner: Codex

## Goal

Make the Korean Scenario Authoring Editor comfortable for a human scenario author who needs to read, adjust, validate, save, and reimport battle scenario flow without directly editing Unity serialized YAML or memorizing action IDs.

## Context

The runtime architecture is now validated for:

- `Scenario Source` YAML as authoring truth.
- `Battle Scenario Data` as Unity runtime asset.
- `Battle Event Rule` as `when`.
- `Action Sequence` as `do`.
- `Action Catalog` as discoverable action grammar.
- `Action Director` and adapters as execution path.

The remaining bottleneck is human authoring. The current `ScenarioAuthoringWindow` can already show overview/rules/sequences, preview YAML, validate source, export source, reimport runtime assets, and perform basic action list edits. Its main usability weakness is that the user must still understand IDs and JSON-like parameter blobs.

## UX Direction

Use a compact production-tool layout, not a marketing or decorative page.

The editor should feel like a scenario flow board:

1. **Left column: Flow Map**
   - Overview facts.
   - Rules shown as `when -> do`.
   - Sequence list.

2. **Center column: Sequence Timeline**
   - Actions shown as stable rows.
   - Parallel children shown indented.
   - Row badges show validation near the problem.
   - Row selection drives the inspector.

3. **Right column: Action Inspector**
   - Korean action name, ID, category, summary.
   - Catalog-driven parameter fields.
   - Fallback parameter editor from current JSON when catalog metadata is incomplete.
   - Advanced raw JSON editor behind a foldout.

4. **Bottom or side utility area: Source and Sync**
   - YAML preview remains available.
   - Buttons distinguish:
     - validate source
     - save source
     - reimport runtime asset
     - save and reimport

## Design Decisions

### Keep UI Toolkit

The current editor is already built directly with UI Toolkit C# APIs. Continue with UI Toolkit rather than IMGUI or a separate UXML/USS split for this phase.

Reason:

- Existing window behavior is all in one file.
- The first improvement is interaction-heavy rather than visual-only.
- UI Toolkit supports stable rows, split views, scroll views, and dynamic parameter forms well enough.

### Keep YAML As Source Of Truth

Human light edits update the runtime `ActionSequenceAsset` object first, then the editor writes YAML through `ScenarioSourceYamlExportCommand`. Reimport uses `ScenarioSourceRuntimeAssetReimportCommand`.

The editor must continue to use existing safe sync commands rather than creating a second write path.

### Do Not Require Complete Catalog Metadata Yet

The sample catalog currently has useful Korean labels but almost no parameter definitions. The inspector therefore needs two modes:

- Catalog-driven fields when `ActionCatalogEntry.Parameters` exists.
- JSON-key fallback fields when metadata is absent.

This lets the editor become useful immediately while future catalog work deepens the parameter schema.

### Row-Level Validation First

The editor should still have a validation summary, but the most important messages must appear next to the exact rule/action row when possible.

This matches the authoring workflow: the user fixes the row they are looking at, not a detached log panel.

## First Implementation Slice

The first implementation should add:

- Three-panel layout: flow map, sequence timeline, action inspector.
- Selectable action rows.
- Inspector for selected action.
- Parameter editing through catalog metadata or current JSON fallback.
- Advanced raw JSON foldout with apply button.
- `저장 및 반영` button that performs source save then runtime reimport.
- Clear Korean status text for success/failure.
- Tests for pure helper behavior:
  - action picker label grouping
  - JSON parameter read/write helper
  - fallback parameter key extraction

## Non-Goals

- No scene edits.
- No prefab edits.
- No full YAML text editor with syntax highlighting yet.
- No drag-and-drop timeline in the first pass.
- No complete visual redesign using external packages.
- No change to runtime adapter grammar.

## Validation

Required:

- `ScenarioSourceSyncTests`
- `ZevScenarioCloneVerticalSliceTests`
- `dotnet build HubToHome.sln --no-restore`
- Unity script validation for `ScenarioAuthoringWindow.cs`

Nice to have:

- Open the window with Unity MCP if available and check console for compile errors.

## Follow-Up

After this slice:

- Populate production `ActionCatalogAsset` parameter metadata.
- Add rule inspector for editing `when` fields.
- Add dialogue/audio/module ID pickers from scenario registries.
- Add drag-and-drop reorder after row selection and parameter editing are stable.
- Add a compact visual graph preview for rule-to-sequence references.
