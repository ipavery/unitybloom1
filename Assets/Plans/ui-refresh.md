# Project Overview
- **Game Title**: Smithery of Fluorescence (derived from scene title "Smithery of Fluorescence")
- **High-Level Concept**: A creative particle editor and spline physics sandbox where players design, manipulate, and animate beautiful glowing particle flows along bezier splines. The application integrates real-time audio analysis, curve editing, and rich customization, allowing players to build and save fluorescent designs.
- **Players**: Single player (creative sandbox editor)
- **Inspiration / Reference Games**: Particle sandbox simulators, Unity Editor, creative spline editors.
- **Tone / Art Direction**: Minimalist, professional, sleek, dark-mode workspace. The UI is clean, neutral, and grayscale, allowing the vibrant, glowing, fluorescent colors of the splines and particles to pop dramatically and serve as the main focus.
- **Target Platform**: Standalone PC (Windows 64-bit)
- **Screen Orientation / Resolution**: Landscape 1920x1080
- **Render Pipeline**: Built-in Render Pipeline (Standard)

# Game Mechanics
## Core Gameplay Loop
- Players create and draw complex bezier splines on-screen.
- They configure particle emitters, lerp properties, physics settings, and music reactivity for individual splines.
- They preview and animate the colorful flowing particles in real-time, syncing them to audio channels.
- They save and load their creative designs into persistent save files.

## Controls and Input Methods
- **Mouse Left-Click**: Draw control points, select curves, drag handles.
- **UI Interaction**: Keyboard/mouse interaction with UI menus, input fields, buttons, and dropdowns.
- **Hotkeys**: Undo/Redo actions, Hide UI overlay to focus on the artwork.

# UI
The UI is a dark-mode, grayscale workspace panel system modeled after high-end editor software (like the Unity Editor or Blender). The layout consists of:
- **Left Panel (Editor Tools)**: Clean vertical bar holding main drawing tools, now using professional icons.
- **Right Panel (Curve Editor)**: Advanced curve manipulation, color selection, and spline configuration.
- **Bottom Panel (Timeline Scroll View)**: Spline instance management, lerp speeds, and dynamic timeline controls.
- **Top Panel / Corner (System Controls)**: Save/Load menu, Undo/Redo controls, and Hide UI.

### Grayscale Color Palette
- **Primary Panel Background**: Solid/sliced dark gray (`#1E1E1E` or `RGBA(0.12, 0.12, 0.12, 1.0)`)
- **Secondary Panel Background / Headers**: Slightly lighter dark gray (`#2A2D32` or `RGBA(0.16, 0.18, 0.20, 1.0)`)
- **Normal Button Top Face**: Crisp neutral gray (`#3C3F41` or `RGBA(0.24, 0.25, 0.26, 1.0)`)
- **Hovered Button Top Face**: Lighter gray highlight (`#4C5052` or `RGBA(0.30, 0.31, 0.32, 1.0)`)
- **Pressed Button Top Face**: Deep gray indented (`#2D2F30` or `RGBA(0.18, 0.18, 0.19, 1.0)`)
- **Button Shadow / Bevel Base**: Dark contrast shadow (`#151515` or `RGBA(0.08, 0.08, 0.08, 1.0)`)
- **Text & Icons**: Pure white or soft light gray (`#D2D2D2` or `RGBA(0.82, 0.82, 0.82, 1.0)`) for maximum readability.

### Satisfying 3D-Style Buttons
Buttons use a physical dual-layer structure to provide a highly tactical, satisfying click response:
1. **The Base Shadow (Parent)**: Acts as the bottom bevel of the button. It is colored dark gray (`#151515`) and is static.
2. **The Button Face (Child)**: Contains the main button image and its child elements (Icon/Text). It is offset upwards by default.
3. **The Physical Press Animation**: A modular C# component (`UIButton3D`) shifts the child Button Face downward on click, making it align with the shadow base. On release, it springs back. This gives an incredibly physical, juicy 3D clicking feel.

# Key Asset & Context

### Identified Icons in `Assets/Icons/`
We will reconfigure these icons to be `Sprite (2D and UI)` and map them to their corresponding buttons:
1. `undo_24dp_1F1F1F_FILL0_wght400_GRAD0_opsz24.png` -> `UndoButton`
2. `redo_24dp_1F1F1F_FILL0_wght400_GRAD0_opsz24.png` -> `RedoButton`
3. `graph_4_24dp_1F1F1F_FILL0_wght400_GRAD0_opsz24.png` -> `DrawSplineButton` (Draw Curve)
4. `delete_24dp_1F1F1F_FILL0_wght400_GRAD0_opsz24.png` -> `DeleteButton` (Delete Curve, Delete Points, Save Slot Delete)
5. `colors_24dp_1F1F1F_FILL0_wght400_GRAD0_opsz24.png` -> `ColorPickerButton1` & `ColorPickerButton2`
6. `menu_24dp_1F1F1F_FILL0_wght400_GRAD0_opsz24.png` -> `HideUIButton` (Hide UI Menu)
7. `stylus_note_24dp_1F1F1F_FILL0_wght400_GRAD0_opsz24.png` -> `duplicatebutton` (Copy) and Save Slot Rename/Copy

### New Script: `UIButton3D.cs`
A lightweight, modular event-handler script that physicalizes button presses:
```csharp
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[RequireComponent(typeof(Button))]
public class UIButton3D : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerEnterHandler, IPointerExitHandler
{
    public RectTransform buttonFace;
    public float clickOffset = 4f;

    private Vector2 originalPosition;
    private bool isPressed = false;
    private bool isHovering = false;

    void Start()
    {
        if (buttonFace == null)
        {
            var faceTransform = transform.Find("ButtonFace");
            if (faceTransform != null)
                buttonFace = faceTransform.GetComponent<RectTransform>();
            else
                buttonFace = GetComponent<RectTransform>();
        }
        
        if (buttonFace != null)
        {
            originalPosition = buttonFace.anchoredPosition;
        }
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (buttonFace == null) return;
        isPressed = true;
        buttonFace.anchoredPosition = originalPosition + new Vector2(0f, -clickOffset);
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (buttonFace == null) return;
        isPressed = false;
        buttonFace.anchoredPosition = originalPosition;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        isHovering = true;
        if (isPressed && buttonFace != null)
        {
            buttonFace.anchoredPosition = originalPosition + new Vector2(0f, -clickOffset);
        }
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        isHovering = false;
        if (buttonFace != null)
        {
            buttonFace.anchoredPosition = originalPosition;
        }
    }

    void OnDisable()
    {
        isPressed = false;
        isHovering = false;
        if (buttonFace != null)
        {
            buttonFace.anchoredPosition = originalPosition;
        }
    }
}
```

# Implementation Steps

## Step 1: Configure Icon Asset Importers
- **Description**: Locate all texture files in `Assets/Icons/` and change their Texture Type to `Sprite (2D and UI)`. Set `Sprite Mode` to `Single`, click Apply. This allows the icons to be assigned as UI sprites.
- **Assigned role**: developer
- **Dependencies**: None
- **Parallelizable**: No

## Step 2: Create `UIButton3D.cs` Script
- **Description**: Create the modular 3D button physical displacement script in `Assets/Scripts/UI/UIButton3D.cs` to handle pointer clicks and physical vertical offsets.
- **Assigned role**: developer
- **Dependencies**: None
- **Parallelizable**: Yes

## Step 3: Re-style Buttons & Hierarchy in `bloom_particles_success.unity`
- **Description**: 
  - For each button in the active scene:
    1. Set the Button component transition to `Color Tint`.
    2. Create a child GameObject named `ButtonFace` inside the button, carrying its own Image component (with sprite set to `grad_1024cubenew`, colored `#3C3F41`). Anchor it to stretch with 0 margins, and offset its position slightly upwards (e.g. `PosY = 4`).
    3. Change the parent Button's Image color to `#151515` (this forms the 3D depth shadow).
    4. Attach the `UIButton3D` component to the parent Button and assign the `ButtonFace` as `buttonFace`.
    5. Set the Button component's `Target Graphic` to the child `ButtonFace`'s Image component. Configure the transition colors to match the Grayscale palette.
- **Assigned role**: developer
- **Dependencies**: Step 2
- **Parallelizable**: No

## Step 4: Implement Icon-Only Buttons
- **Description**: Convert targeted scene buttons from words to icons:
  - **UndoButton**: Remove the "Undo" text component. Add an Image child named `Icon` with sprite `undo` and color `#D2D2D2` (centered, 24x24).
  - **RedoButton**: Remove text. Add `redo` icon (centered, 24x24).
  - **HideUIButton**: Remove text. Add `menu` icon (centered, 24x24).
  - **DrawSplineButton**: Remove text. Add `graph_4` icon (centered, 48x48).
  - **DeleteButton** (Delete Points): Remove text. Add `delete` icon (centered, 24x24).
  - **DeleteButton** (Delete Curve): Remove text. Add `delete` icon (centered, 24x24).
  - **duplicatebutton**: Remove text. Add `stylus_note` icon (centered, 24x24).
  - **ColorPickerButton1**: Remove text. Add `colors` icon (centered, 24x24). Add a tiny text in the corner displaying "1".
  - **ColorPickerButton2**: Remove text. Add `colors` icon (centered, 24x24). Add a tiny text in the corner displaying "2".
- **Assigned role**: developer
- **Dependencies**: Step 1, Step 3
- **Parallelizable**: No

## Step 5: Refresh UI Panel Colors & Typography (Grayscale Theme)
- **Description**: Update the panels and texts in the scene to form a coherent, professional workspace:
  - Set `HIdeUIContainer` image color to solid `#1E1E1E`.
  - Set `CurveEditorContainer` image color to solid `#1E1E1E` with headers styled in `#2A2D32`.
  - Set `EditorToolsUIRoot` image color to solid `#1E1E1E`.
  - Set `SaveLoadContainer` image color to solid `#1E1E1E`.
  - Set `LoadPanel` image color to solid `#1E1E1E` (with scroll view background `#151515`).
  - Set `ConfirmDeletePanel` and `RenamePanel` to solid `#1E1E1E`.
  - Set the scroll bar handles and backgrounds to match dark gray tones (`#2D2D2D` and `#151515`).
  - Set all TMPro texts (titles, button labels, dropdown items) to standard light-gray/white `#E0E0E0` or `#FFFFFF` and ensure they use the professional `LiberationSans SDF` font with clean, un-bolded sizes.
- **Assigned role**: developer
- **Dependencies**: Step 3
- **Parallelizable**: No

## Step 6: Apply the 3D Grayscale Refresh to Prefabs
- **Description**: Apply the same styling transformations to key UI prefabs in `Assets/Prefabs/` so that runtime-spawned elements match seamlessly:
  - **`SaveSlotPanelPrefab3.prefab`**:
    - Style container in `#252525`.
    - `RenameButton`: Convert to icon-only with `stylus_note` icon. Style as 3D Button (dark base `#151515`, face `#3C3F41`).
    - `CopyButton`: Convert to icon-only with `stylus_note` icon. Style as 3D Button.
    - `DeleteButton`: Convert to icon-only with `delete` icon. Style as 3D Button.
    - `LoadButton`: Keep as dynamic savefile text, but style as 3D Button.
  - **`ColorPickerRoot.prefab`**:
    - Color picker container background -> `#1E1E1E`.
    - `ConfirmButton` & `CancelButton` -> Style as 3D Buttons (keep text but style in gray).
  - **`RenamePanel.prefab`**:
    - Container background -> `#1E1E1E`.
    - Input field and confirm/cancel buttons -> Grayscale styling.
- **Assigned role**: developer
- **Dependencies**: Step 1, Step 2
- **Parallelizable**: No

# Verification & Testing
1. **Manual Visual Check**: Open the scene `bloom_particles_success.unity` and ensure all panels, headers, scroll views, and static buttons have uniform grayscale coloring and high contrast.
2. **Tactile Feel Test**: Click every button in the scene (Undo, Redo, ColorPicker, Connect, Draw Spline, Delete, etc.) to verify the `UIButton3D` effect works perfectly—the button face should physically shift down on click and spring back on release.
3. **Icon Verification**: Verify that the icon-only buttons are centered, clean, and easily understandable. Verify the color picker buttons have "1" and "2" indicators in the corners.
4. **Dynamic Spawn Test**: Enter Play Mode, open the Save/Load menu, and create a save slot. Verify that the spawned slot (from `SaveSlotPanelPrefab3`) displays perfectly, and that the slot's Rename, Copy, and Delete buttons are icon-based 3D buttons.
5. **UI Hiding Test**: Click the Hide UI button (hamburger icon) and ensure it hides the UI and changes back appropriately.
6. **No Breaking Logic**: Check the Unity Console to ensure there are no errors or warnings, verifying that no scripts or field references were broken during the UI hierarchy modifications.
