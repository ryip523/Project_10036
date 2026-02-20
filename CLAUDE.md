# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

"美麗新香港" — a Unity 3D interactive Hong Kong district map + visual novel built with Universal Render Pipeline (URP). Players explore a 3D map of Hong Kong's 18 districts, select a district to reveal available episodes, and launch Cantonese/English visual novel scenes with typewriter dialogue, character sprites, and dynamic speaker highlighting.

**Unity version:** 6000.3.9f1
**Render pipeline:** URP 2D (Renderer2D)
**Input system:** New Input System (com.unity.inputsystem 1.18.0)

## Unity-Specific Workflow

This project is controlled through **MCPForUnity** — use the MCP tools (`manage_scene`, `manage_gameobject`, `manage_components`, `manage_script`, etc.) to interact with the Unity Editor directly rather than only editing files on disk.

After creating or modifying any `.cs` script, always:
1. Call `refresh_unity` with `compile: "request"` and `wait_for_ready: true`
2. Call `read_console` to check for compilation errors before proceeding

To run the game: `manage_editor` with `action: "play"`.

**Important**: When adding Canvas child GameObjects via MCP tools that involve reparenting, always reset `localScale` to `(1, 1, 1)` after reparenting. Unity preserves world scale during reparent which inflates localScale when the Canvas has scale ≠ 1.

## Font System

Two TMP font assets are built by **Tools → Setup CJK Font** (`Assets/Editor/CJKFontSetup.cs`):

| Asset | Source | Purpose |
|---|---|---|
| `PingFang_TMP.asset` | `PingFang.ttc` (77 MB) | Default font assigned to all TMP components by the setup script |
| `LXGWWenKaiTC_TMP.asset` | `LXGWWenKaiTC-Medium.ttf` | UI font — title neon glow + all panel/dialogue text (assigned at runtime) |

Both font atlases contain the **same full character set**: ASCII, UI symbols, all 18 district names, title, and all dialogue text from `Assets/Dialogue/*.txt`. The setup script assigns PingFang to all TMP components in scenes and prefabs, then assigns WenKai to `DistrictMapManager.titleFont` and `VisualNovelManager.font`. Both scripts override TMP fonts at runtime in `Start()` using their respective font fields.

**Re-run Tools → Setup CJK Font** whenever new Chinese characters are added (new dialogue, new UI strings, new district names). Add new hardcoded UI characters to the `CollectCharacters()` string in `CJKFontSetup.cs`. The `GameScenes[]` array must include any new scenes. **Must exit play mode before running** — the script uses `EditorSceneManager` which fails during play.

## Architecture

### Scene Flow

`MainMenu` (build index 0) → title typewriter → click to start → blur transition → map with auto-selected 中西區 → click district → side panel → select episode → click GO → BGM fades (0.5s) → `Angel` (build index 1) → staged entrance → dialogue → end or back button → `MainMenu`.

### MainMenu Scene (`Assets/Scenes/MainMenu.unity`)

3D district map with Screen Space overlay UI (1536×1024 reference, Scale with Screen Size):
```
Main Camera              (perspective, angled top-down view)
Directional Light
EventSystem
HKDistrictMap            (imported FBX, parent of 18 district meshes)
  +-- central_western    (MeshCollider + DistrictInteractable)
  +-- eastern, southern, wan_chai, ...  (18 total)
Canvas (Screen Space - Overlay)
  +-- TitleText          (TMP - "美麗新香港", neon glow effect)
  +-- HoverLabel         (TMP - tooltip follows cursor)
  +-- SidePanel          (Image + Mask, slides in from right)
      +-- PanelDistrictName  (TMP - district Chinese name, font size 40)
      +-- SceneList          (VerticalLayoutGroup + RectMask2D for clipping)
      +-- ComingSoonLabel    (TMP - "即將推出")
      +-- CloseButton        (top-right red "X", 40×40, transparent bg)
      +-- GoButton           (bottom green bar, "開始", hidden until selection)
  +-- BackButton         (top-left "< 返回", hidden during title, returns to title screen)
DistrictMapManager       (manages raycast hover/click + panel + scene selection)
```

**SidePanel layout**: 480×700, anchored right-center, pivot (1, 0.5). Slide constants: `PanelHiddenX=560`, `PanelVisibleX=-40` (negative = inset from right edge with pivot at right).

Each district mesh lives on the **"District" layer (layer 8)**, used by `DistrictMapManager` for raycast filtering.

### Angel Scene (`Assets/Scenes/Angel.unity`)

```
Canvas
├── Background          (Image – full-screen bg color)
├── PortraitLeft        (Image – 和記 character, dim when not speaking)
├── PortraitRight       (Image – 外國人 character, dim when not speaking)
└── DialogueBox         (Image – bottom strip)
    ├── NamePlate       (Image – speaker-color coded)
    │   ├── NameText    (TextMeshProUGUI – speaker name)
    │   └── NameUnderline (Image – color underline)
    ├── DialogueText    (TextMeshProUGUI – typewriter text)
    └── ContinueHint    (TextMeshProUGUI – "▼  點擊繼續" / "— END —  ▼ 返回選單")
VisualNovelManager      (standalone GameObject, font field → WenKai)
```

**Created at runtime by VisualNovelManager**: BackButton (top-left "< 返回"), EventSystem + InputSystemUIInputModule (if missing), GraphicRaycaster on Canvas (if missing), and confirm panel modal on back button click.

### Scripts

#### `DistrictMapManager.cs`

Main controller for the MainMenu scene. Key systems:
- **Start screen**: 1s delay → title typewriter (0.5s/char) → "點擊任意位置開始" hint → click to dismiss → blur transition → map appears → auto-selects 中西區
- **Back to title**: `ReturnToTitle()` reverses the start transition without restarting BGM
- **Hover**: Raycasts against layer 8, updates tooltip, triggers `DistrictInteractable` lift animation
- **Side panel**: Click district → `OpenPanel()` populates episode list from `DistrictData` → click episode → `SelectSceneButton()` highlights it and shows GoButton → click GO → `LoadSceneWithFade()` (fades BGM over 0.5s)
- **Font override**: `Start()` assigns `titleFont` (WenKai) to all panel TMP components; scene button labels and GoButton text also get WenKai at runtime

#### `VisualNovelManager.cs`

Runtime script for Angel scene. All UI refs found by `GameObject.Find()` in `Awake()`. Font field assigns WenKai to nameText, dialogueText, continueHint in `Start()`. Key behaviours:
- **Staged entrance**: 0.5s buffer → fade in portraits (0.6s) → 0.5s pause → show dialogue box → start first line
- `ParseDialogue()` – reads `dialogueFile` (TextAsset), splits on `|`, skips `#` comments
- `TypewriterEffect(text)` – coroutine; one char per `typewriterSpeed` seconds
- Input: Enter, Space, or left-click to advance; skips if pointer is over a Button (`IsPointerOverButton()` via EventSystem raycast); if typing: skip to full text; if ended: return to MainMenu
- **Back button**: Created programmatically with confirm panel ("確認返回選單?" / "確認" / "取消"). Blocks dialogue advancement while confirm panel is open.
- **Audio**: Loads clips from `Resources/DialogueClips/{lineNumber:D2}` per dialogue line

#### `DistrictData.cs`

Static dictionary: `district_id` (mesh name) → `DistrictDef { nameZH, scenes[] }`. Only `central_western` currently has episodes (`Angel` scene). All other 17 districts show "即將推出".

#### `DistrictInteractable.cs`

MonoBehaviour on each district mesh. Handles hover lift animation (Y axis, LiftHeight=5, LiftSpeed=8) and material swapping (default/hover/selected).

## Adding Content

### New Dialogue

Edit `Assets/Dialogue/WakiDialogue.txt` (UTF-8, `speaker|text` format, `#` for comments):
```
和記|新的台詞...
外國人|New line of dialogue...
```
Audio clips go in `Assets/Resources/DialogueClips/` named `01.mp3`, `02.mp3`, etc. matching line numbers.

### New Episodes / Scenes

1. Create the new scene and add it to Build Settings (`Tools/Setup Build Settings` or manually)
2. Add a `SceneEntry` to the district's array in `DistrictData.cs`
3. Add the scene path to `GameScenes[]` in `CJKFontSetup.cs`
4. Re-run **Tools → Setup CJK Font** if new Chinese characters were introduced

## Known Harmless Warnings

On play mode entry, Unity may log "referenced script (Unknown) on this Behaviour is missing!" messages. These are transient domain-reload artifacts and do not affect runtime behaviour.

## Key Packages

| Package | Purpose |
|---|---|
| `com.unity.inputsystem` | New Input System (mouse/keyboard) |
| `com.unity.render-pipelines.universal` | URP 2D renderer |
| `com.unity.2d.*` | Sprites, animation, tilemaps |
| `com.coplaydev.unity-mcp` | MCP bridge for Claude Code tooling |
| `com.unity.textmeshpro` | TMP (bundled in ugui) |
