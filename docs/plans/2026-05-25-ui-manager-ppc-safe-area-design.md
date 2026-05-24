# UIManager PPC Safe Area Design

## Goal

Make UIManager support panels that must stay inside the Pixel Perfect Camera visible area, without changing battle UI or other fullscreen UI by accident.

## Decision

Use a panel-level opt-in model. UIManager owns the rule that specific panels can be fitted to the Pixel Perfect Camera safe area. For now, only `OverWorldPanel` opts in.

## Architecture

- `UIManager` keeps its existing panel registration and stack behavior.
- `OverWorldPanel` is registered as a Pixel Perfect safe-area panel.
- When an opted-in panel is registered or opened, UIManager ensures that panel has a runtime safe-area fitter.
- The fitter creates a `[PPC Safe Area]` RectTransform under the panel's Canvas root, moves direct UI children under it, and sizes that root to the current PPC reference aspect.
- Existing panel scripts keep their serialized references. Moving the hierarchy at runtime does not change the referenced `RectTransform` or `CanvasGroup` objects.

## Scope

- Apply to `OverWorldPanel` only.
- Do not change scene files.
- Do not change `BattleUIController` behavior.
- Do not globally call `UIRuntimeGuard.NormalizeCanvas()` for this problem.

## Validation

- At 640x480, the safe area should be the full canvas.
- At 16:9 or 4K, the safe area should be centered and keep a 640:480 aspect.
- Top and bottom overworld menu bars should stretch only across the safe area, not the whole monitor aspect.
- Build should pass with only existing warnings.
