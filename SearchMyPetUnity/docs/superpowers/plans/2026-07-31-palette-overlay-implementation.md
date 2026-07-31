# Palette Overlay Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Keep the palette, capture, and pose controls visible while toggling the paint toolbar and selected-tool settings above the bottom black bar.

**Architecture:** Reuse the existing scene-owned `Paint Quick Controls`, `Paint Toolbar`, and `Paint Tool Options` objects. `CharacterColorPalette` owns the toggle state; `WallPlacementValidation.unity` is the single UI authoring source with `SearchMyPetAppUI` fully unpacked.

**Tech Stack:** Unity 6.5, C#, Unity UI (`RectTransform`, `Button`), NUnit EditMode tests, Unity MCP.

## Global Constraints

- Keep `Paint Quick Controls` visible while paint tools are open.
- The palette button toggles the complete paint-tool overlay.
- The top `<` button always returns directly to the map screen.
- Preserve the user's current camera-screen styling and keep `Paint Quick Controls` at `(0,75)`.
- Do not change painting, capture, pose, or map-tab implementations.
- Remove `SearchMyPetAppUI.prefab` and `PaintUiPrefabStyler`; the Scene is the only UI source.

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

### Superseded: Prefab-Based Layout (Do Not Execute)

> Replaced after live Scene verification showed prefab overrides were the cause of the mismatch. The user approved making the Scene the single source of truth.

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

---

### Task 2: Make the Scene UI the Single Source of Truth

**Files:**
- Modify: `Assets/Scenes/WallPlacementValidation.unity`
- Delete: `Assets/Prefabs/SearchMyPetAppUI.prefab`
- Delete: `Assets/Prefabs/SearchMyPetAppUI.prefab.meta`
- Delete: `Assets/Editor/PaintUiPrefabStyler.cs`
- Delete: `Assets/Editor/PaintUiPrefabStyler.cs.meta`
- Test: `Assets/Tests/Editor/CharacterColorPaletteTests.cs`

**Interfaces:**
- Consumes: the scene-owned object `SearchMyPetAppUI/Character Paint UI/Safe Area` and Task 1's `ToggleToolMenu()` behavior.
- Produces: an unpacked `SearchMyPetAppUI` whose quick controls stay at `(0,75)`, toolbar is at `(0,159)`, and tool options are at `(0,241)`.

- [ ] **Step 1: Replace prefab contract tests with a failing Scene contract**

Add these imports and constant to `CharacterColorPaletteTests.cs`:

```csharp
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

private const string ScenePath = "Assets/Scenes/WallPlacementValidation.unity";
```

Replace `EditableUiPrefab_ContainsTabsAndPaintControls` with `SceneUi_IsUnpackedAndStacksPaintPanels`. Load the scene only when it is not already open, locate the `SearchMyPetAppUI` root, and assert:

```csharp
Assert.That(PrefabUtility.GetPrefabInstanceStatus(appUi), Is.EqualTo(PrefabInstanceStatus.NotAPrefab));
Assert.That(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/SearchMyPetAppUI.prefab"), Is.Null);
Assert.That(AssetDatabase.LoadAssetAtPath<MonoScript>("Assets/Editor/PaintUiPrefabStyler.cs"), Is.Null);

var safeArea = appUi.transform.Find("Character Paint UI/Safe Area");
var quickControls = (RectTransform)safeArea.Find("Paint Quick Controls");
var toolbar = (RectTransform)safeArea.Find("Paint Toolbar");
var toolOptions = (RectTransform)safeArea.Find("Paint Tool Options");
Assert.That(quickControls.anchoredPosition, Is.EqualTo(new Vector2(0f, 75f)));
Assert.That(toolbar.anchoredPosition, Is.EqualTo(new Vector2(0f, 159f)));
Assert.That(toolOptions.anchoredPosition, Is.EqualTo(new Vector2(0f, 241f)));
Assert.That(toolbar.gameObject.activeSelf, Is.False);
Assert.That(toolOptions.gameObject.activeSelf, Is.False);
```

Keep the existing hierarchy, palette icon, back label, scrim, and tool-button assertions, but evaluate them from `appUi` instead of a prefab asset.

- [ ] **Step 2: Make the binding test clone the Scene UI**

Rename `EditableUiPrefab_BindsWithoutCreatingDuplicateUi` to `SceneUi_BindsWithoutCreatingDuplicateUi`. Use the same Scene helper to obtain `appUi`, then replace prefab loading/instantiation with:

```csharp
var instance = Object.Instantiate(appUi, canvasObject.transform);
instance.name = "SearchMyPetAppUI";
```

Close the Scene in `finally` only when the helper opened it for the test. Keep all Task 1 toggle and map navigation assertions unchanged.

- [ ] **Step 3: Run the two focused tests and verify RED**

Run through Unity MCP:

```json
{"mode":"EditMode","test_names":["SearchMyPet.AR.Tests.CharacterColorPaletteTests.SceneUi_IsUnpackedAndStacksPaintPanels","SearchMyPet.AR.Tests.CharacterColorPaletteTests.SceneUi_BindsWithoutCreatingDuplicateUi"],"include_failed_tests":true}
```

Expected: FAIL because the Scene object is still a connected prefab instance and the prefab/styler assets still exist.

- [ ] **Step 4: Unpack and reposition only the Scene UI**

Execute this C# in the Unity Editor:

```csharp
var scene = UnityEngine.SceneManagement.SceneManager.GetSceneByPath("Assets/Scenes/WallPlacementValidation.unity");
if (!scene.IsValid() || !scene.isLoaded)
{
    scene = UnityEditor.SceneManagement.EditorSceneManager.OpenScene(
        "Assets/Scenes/WallPlacementValidation.unity",
        UnityEditor.SceneManagement.OpenSceneMode.Single);
}

GameObject appUi = null;
foreach (var root in scene.GetRootGameObjects())
{
    if (root.name == "SearchMyPetAppUI")
    {
        appUi = root;
        break;
    }
}

if (appUi == null) throw new System.InvalidOperationException("SearchMyPetAppUI not found");
if (PrefabUtility.GetPrefabInstanceStatus(appUi) != PrefabInstanceStatus.NotAPrefab)
{
    PrefabUtility.UnpackPrefabInstance(appUi, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
}

var safeArea = appUi.transform.Find("Character Paint UI/Safe Area");
((RectTransform)safeArea.Find("Paint Quick Controls")).anchoredPosition = new Vector2(0f, 75f);
((RectTransform)safeArea.Find("Paint Toolbar")).anchoredPosition = new Vector2(0f, 159f);
((RectTransform)safeArea.Find("Paint Tool Options")).anchoredPosition = new Vector2(0f, 241f);
EditorSceneManager.MarkSceneDirty(scene);
EditorSceneManager.SaveScene(scene);
return "Scene UI unpacked and stacked";
```

- [ ] **Step 5: Delete the obsolete prefab and styler through Unity**

After the Scene save succeeds, delete exactly these assets with `AssetDatabase.DeleteAsset`:

```csharp
AssetDatabase.DeleteAsset("Assets/Prefabs/SearchMyPetAppUI.prefab");
AssetDatabase.DeleteAsset("Assets/Editor/PaintUiPrefabStyler.cs");
AssetDatabase.SaveAssets();
return "Obsolete UI assets deleted";
```

Unity deletes each matching `.meta` file. Do not delete the `Assets/Prefabs` folder because it still contains the character prefab.

- [ ] **Step 6: Run GREEN tests and the full EditMode suite**

Run the two focused tests from Step 3, then:

```json
{"mode":"EditMode","include_failed_tests":true}
```

Expected: both focused tests PASS and all EditMode tests PASS with zero failures.

- [ ] **Step 7: Verify the current Unity Scene visually**

Enter Play mode and initialize `CharacterColorPalette` with a temporary character if necessary. Click the palette button, then select the palette tool.

Confirm:

- quick controls remain at their existing position and stay visible;
- toolbar appears 8 px above the quick controls;
- tool options appear 6 px above the toolbar;
- the palette button closes both upper panels on its second click;
- `<` returns directly to the map.

Stop Play mode after verification. Do not launch Xcode.

- [ ] **Step 8: Commit the Scene-owned UI change**

```powershell
git add -A -- Assets/Scenes/WallPlacementValidation.unity Assets/Prefabs/SearchMyPetAppUI.prefab Assets/Editor/PaintUiPrefabStyler.cs Assets/Tests/Editor/CharacterColorPaletteTests.cs
git commit -m "Make scene UI the authoring source"
```
