# Palette Overlay Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Keep the palette, capture, and pose controls visible while toggling the paint toolbar and selected-tool settings above the bottom black bar.

**Architecture:** Reuse the existing `Paint Quick Controls`, `Paint Toolbar`, and `Paint Tool Options` objects. `CharacterColorPalette` owns the toggle state; the prefab owns the fixed iPhone-sized authoring layout, and `PaintUiPrefabStyler` records the same coordinates without being executed over the user's manual styling.

**Tech Stack:** Unity 6.5, C#, Unity UI (`RectTransform`, `Button`), NUnit EditMode tests, Unity MCP.

## Global Constraints

- Keep `Paint Quick Controls` visible while paint tools are open.
- The palette button toggles the complete paint-tool overlay.
- The top `<` button always returns directly to the map screen.
- Preserve the user's current camera-screen styling and manual prefab edits.
- Do not change painting, capture, pose, or map-tab implementations.
- Do not run `SearchMyPet/Style Paint UI` against the current prefab.

---

### Task 1: Palette Toggle and Back Navigation

**Files:**
- Modify: `Assets/Scripts/AR/CharacterColorPalette.cs:274-314`
- Test: `Assets/Tests/Editor/CharacterColorPaletteTests.cs:159-205`

**Interfaces:**
- Consumes: `CharacterColorPalette.ToolsOpen`, `OpenToolMenu()`, `CloseToolMenu()`, and `AppTabController.SelectTab(AppTab)`.
- Produces: `public void ToggleToolMenu()`; the palette button invokes it, and `NavigateBack()` always selects `AppTab.Map`.

- [ ] **Step 1: Change the binding test to describe the required toggle**

Replace the palette/back interaction portion of `EditableUiPrefab_BindsWithoutCreatingDuplicateUi` with:

```csharp
var paletteButton = safeArea.Find("Paint Quick Controls/Color Palette Button").GetComponent<Button>();
Assert.That(safeArea.Find("Paint Quick Controls").gameObject.activeSelf, Is.True);

paletteButton.onClick.Invoke();
Assert.That(palette.ToolsOpen, Is.True);
Assert.That(safeArea.Find("Paint Quick Controls").gameObject.activeSelf, Is.True);
Assert.That(safeArea.Find("Paint Toolbar").gameObject.activeSelf, Is.True);
Assert.That(safeArea.Find("Paint Top Bar").gameObject.activeSelf, Is.True);

paletteButton.onClick.Invoke();
Assert.That(palette.ToolsOpen, Is.False);
Assert.That(safeArea.Find("Paint Quick Controls").gameObject.activeSelf, Is.True);
Assert.That(safeArea.Find("Paint Toolbar").gameObject.activeSelf, Is.False);

paletteButton.onClick.Invoke();
safeArea.Find("Paint Top Bar/완료").GetComponent<Button>().onClick.Invoke();
Assert.That(tabs.ActiveTab, Is.EqualTo(AppTab.Map));
Assert.That(instance.transform.Find("Map Fallback").gameObject.activeSelf, Is.True);
Assert.That(instance.transform.Find("Character Paint UI").gameObject.activeSelf, Is.False);
```

Keep the existing character rebinding assertions after this block, but reuse the `paletteButton` local instead of declaring it again.

- [ ] **Step 2: Run the focused EditMode test and verify it fails**

Run through Unity MCP:

```json
{"mode":"EditMode","test_names":["SearchMyPet.AR.Tests.CharacterColorPaletteTests.EditableUiPrefab_BindsWithoutCreatingDuplicateUi"],"include_failed_tests":true}
```

Expected: FAIL because opening tools currently hides `Paint Quick Controls`, clicking the palette twice does not close tools, and the first back click only closes tools.

- [ ] **Step 3: Implement the smallest toggle and navigation change**

Update `BindQuickControls` and add the toggle method:

```csharp
private void BindQuickControls(Transform safeArea)
{
    quickControls = safeArea?.Find("Paint Quick Controls")?.gameObject;
    var button = quickControls?.transform.Find("Color Palette Button")?.GetComponent<Button>();
    button?.onClick.RemoveListener(OpenToolMenu);
    button?.onClick.RemoveListener(ToggleToolMenu);
    button?.onClick.AddListener(ToggleToolMenu);
    quickColorSwatch = quickControls?.transform.Find("Color Palette Button/Selected Color")?.GetComponent<Image>();
}

public void ToggleToolMenu()
{
    if (ToolsOpen)
    {
        CloseToolMenu();
        return;
    }

    OpenToolMenu();
}
```

Keep quick controls active in both menu states and make back navigation unconditional:

```csharp
public void OpenToolMenu()
{
    ToolsOpen = true;
    quickControls?.SetActive(true);
    toolbar?.SetActive(true);
    topBar?.SetActive(true);
    ShowTool(Tool.Brush);
}

public void NavigateBack()
{
    IsPainting = false;
    ToolsOpen = false;
    paintUi?.SetActive(false);
    SetCameraUiVisible(false);
    appTabs ??= FindAnyObjectByType<AppTabController>();
    appTabs?.SelectTab(AppTab.Map);
}
```

Leave `CloseToolMenu()` responsible for hiding `Paint Toolbar` and `Paint Tool Options` while keeping `Paint Quick Controls` visible.

- [ ] **Step 4: Run the focused test and verify it passes**

Run the same Unity MCP EditMode test.

Expected: PASS with one palette click opening the overlay, the second closing it, and `<` selecting the map even while tools are open.

- [ ] **Step 5: Commit the interaction change**

```powershell
git add -- Assets/Scripts/AR/CharacterColorPalette.cs Assets/Tests/Editor/CharacterColorPaletteTests.cs
git commit -m "Add palette overlay toggle"
```

---

### Task 2: Stack the Existing Panels Above the Bottom Bar

**Files:**
- Modify: `Assets/Prefabs/SearchMyPetAppUI.prefab`
- Modify: `Assets/Editor/PaintUiPrefabStyler.cs:36-45`
- Test: `Assets/Tests/Editor/CharacterColorPaletteTests.cs:121-157`

**Interfaces:**
- Consumes: the existing bottom-anchored `RectTransform` objects named `Paint Quick Controls`, `Paint Toolbar`, and `Paint Tool Options`.
- Produces: fixed authoring positions `(0,20)`, `(0,104)`, and `(0,186)` respectively; sizes remain `(358,76)`, `(358,76)`, and `(358,110)`.

- [ ] **Step 1: Add failing prefab layout assertions**

Add to `EditableUiPrefab_ContainsTabsAndPaintControls` after the active-state assertions:

```csharp
var quickControls = (RectTransform)prefab.transform.Find("Character Paint UI/Safe Area/Paint Quick Controls");
var toolbar = (RectTransform)prefab.transform.Find("Character Paint UI/Safe Area/Paint Toolbar");
var toolOptions = (RectTransform)prefab.transform.Find("Character Paint UI/Safe Area/Paint Tool Options");
Assert.That(quickControls.anchoredPosition, Is.EqualTo(new Vector2(0f, 20f)));
Assert.That(toolbar.anchoredPosition, Is.EqualTo(new Vector2(0f, 104f)));
Assert.That(toolOptions.anchoredPosition, Is.EqualTo(new Vector2(0f, 186f)));
```

- [ ] **Step 2: Run the focused prefab test and verify it fails**

Run through Unity MCP:

```json
{"mode":"EditMode","test_names":["SearchMyPet.AR.Tests.CharacterColorPaletteTests.EditableUiPrefab_ContainsTabsAndPaintControls"],"include_failed_tests":true}
```

Expected: FAIL because `Paint Toolbar` is currently at `(0,20)` and `Paint Tool Options` is at `(0,102)`.

- [ ] **Step 3: Record the new coordinates in the repeatable styler**

Change only these two calls in `PaintUiPrefabStyler.StylePaintUi()`:

```csharp
SetRect((RectTransform)toolbar, new Vector2(0f, 104f), new Vector2(358f, 76f));
SetRect((RectTransform)context, new Vector2(0f, 186f), new Vector2(358f, 110f));
```

Do not execute the menu command; it recreates `Paint Quick Controls` and could overwrite current manual edits.

- [ ] **Step 4: Move only the two existing prefab panels through Unity MCP**

Execute this C# in the Unity Editor:

```csharp
var path = "Assets/Prefabs/SearchMyPetAppUI.prefab";
var root = PrefabUtility.LoadPrefabContents(path);
try
{
    var safeArea = root.transform.Find("Character Paint UI/Safe Area");
    var toolbar = (RectTransform)safeArea.Find("Paint Toolbar");
    var toolOptions = (RectTransform)safeArea.Find("Paint Tool Options");
    toolbar.anchoredPosition = new Vector2(0f, 104f);
    toolOptions.anchoredPosition = new Vector2(0f, 186f);
    PrefabUtility.SaveAsPrefabAsset(root, path);
}
finally
{
    PrefabUtility.UnloadPrefabContents(root);
}
return "Palette overlay panels repositioned";
```

Expected: existing children, colors, sprites, and sizes remain unchanged.

- [ ] **Step 5: Run the focused prefab test and all EditMode tests**

First run the focused prefab test from Step 2, then run:

```json
{"mode":"EditMode","include_failed_tests":true}
```

Expected: focused test PASS; all 56 EditMode tests PASS with zero failures.

- [ ] **Step 6: Verify the visual stack in Unity**

Enter Play mode, place or assign a character so `Character Paint UI` is active, and click the palette button.

Confirm:

- the black bottom bar and all three quick controls remain visible;
- the five-tool toolbar starts 8 px above the quick-control panel;
- the selected tool options start 6 px above the toolbar;
- clicking palette again hides both upper panels;
- clicking `<` returns directly to the map.

Stop Play mode after verification. Do not launch Xcode.

- [ ] **Step 7: Commit the layout change**

```powershell
git add -- Assets/Prefabs/SearchMyPetAppUI.prefab Assets/Editor/PaintUiPrefabStyler.cs Assets/Tests/Editor/CharacterColorPaletteTests.cs
git commit -m "Stack paint tools above camera controls"
```
