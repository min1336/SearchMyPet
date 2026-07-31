# Palette Overlay Design

## Goal

Keep the current camera controls visible while exposing character-paint tools above the bottom black bar.

## Layout

- The bottom black bar and its palette, capture, and pose buttons remain visible at all times.
- Tapping the palette button shows the brush, size, texture, palette, and eyedropper toolbar immediately above the black bar.
- The selected tool's settings panel appears immediately above the toolbar.
- The selected tool keeps the existing lime outline treatment.
- The top `<` button always returns to the map screen.

## Interaction

- The palette button toggles the complete paint-tool overlay open and closed.
- Opening the overlay does not disable or hide capture and pose controls.
- Choosing a tool updates the settings panel without moving the bottom controls.
- Closing the overlay hides both the toolbar and its settings panel.

## Implementation Boundary

- `WallPlacementValidation.unity` is the single authoring source for `SearchMyPetAppUI`.
- Fully unpack the scene's `SearchMyPetAppUI` instance so Scene edits are not overridden by a prefab.
- Reuse the scene's `Paint Quick Controls`, `Paint Toolbar`, and `Paint Tool Options`.
- Change `CharacterColorPalette` so opening tools no longer hides `Paint Quick Controls`.
- Keep the current quick controls at `(0,75)` and place only the toolbar and settings panel above them.
- Delete `SearchMyPetAppUI.prefab` and `PaintUiPrefabStyler`; neither remains a source of UI truth.
- Do not change `AppTabController`, the map screen, character painting logic, capture logic, or pose logic.

## Verification

- Editor test: palette clicks toggle the toolbar and settings panel while quick controls stay active.
- Editor test: the top back button still selects the map tab.
- Scene contract test verifies the unpacked UI hierarchy and positions.
- Unity visual check at the current iPhone-sized canvas confirms the overlay stacks above the black bar without overlap.
