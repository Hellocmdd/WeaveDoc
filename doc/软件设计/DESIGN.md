
# WeaveDoc UI Design System & Layout Guidelines (High-Fidelity Calibrated)

> **⚠️ Hard Constraint:** This application is a high-density, IDE-style local academic desktop workbench. It must completely dissolve all relaxed Web SaaS card-flow layouts, returning entirely to a tight tree-structured layout optimized for maximum information throughput.

## 1. Design Philosophy & Vibe

- **Core Identity:** A local-first, privacy-focused academic workspace.
- **Visual Vibe:** Minimalist, highly dense information architecture, academic rigor.
- **Design Principles:** Content first, UI recedes. No unnecessary whitespace. High information density.

## 2. Global Layout Anatomy (Base: 1440x900px)

- **Title Bar:** 32px height (Native OS style).
- **Menu Bar:** 28px height.
- **Toolbar:** 40px height. Contains max 7 icon-only buttons (16x16px icons).
- **Main Workspace (Flex: 1):**
  - **Left Split:** PDF Viewer (Default 35% width).
  - **Center Split:** Markdown Editor fills the remaining split width (about 65% when the context panel is collapsed; auto-fill after reserving the 280px context panel when expanded).
  - **Right Context Panel:** 280px strictly fixed width (Collapsible). Tall and narrow, horizontal scaling is strictly prohibited. Contains Tabs (AI, Literature, Timeline).
- **Status Bar:** 24px height at the absolute bottom. Left-aligned document/sync text, micro-indicators for AI/Memory on the right. It is visible by default and can be hidden from View.

## 3. Spacing & Grid System

- **Base Grid:** 4px.
- **Micro-spacing:** 2px (dividers), 4px (tight grouping), 8px (inner padding/bubbles).
- **Macro-spacing:** 12px (panel padding), 16px (module gaps), 24px (dialog sections).
- **Strict Layout Constraints:** Row layout padding must be exactly 8px vertically and 12px horizontally.
- **Component Heights:** Buttons and single-line input fields must be exactly 32px in height. The AI multiline input box is 60px inside an 88px input area. List rows vary (e.g., 56px for literature, 64px for templates).

## 4. Color Palette Tokens (Unified — Reconciled with visual-assets.json v2.0)

A strict "Pure White Reading + Immersive Dark Code" dual-core mapping. All hex values are drawn from the unified neutral, accent, and semantic scales defined in `visual-assets.json`. This section defines the **Light Theme** mapping; the full Dark Theme mapping lives in visual-assets.json §themeMapping.dark.

> **Note on naming**: The CSS variable names below (e.g. `--primary`) reflect the "dual-core" design concept where the editor is an immersive dark zone. For actual Avalonia ResourceDictionary keys, refer to `visual-assets.json` §themeMapping.light — there, `Primary` means the interactive accent color (`blue-700`), and `EditorBackground` is the dark editor surface (`neutral-950`).

| CSS Variable             | Hex Value      | Source Token       | Calibration Description & Business Mapping                                                                                                                                                                                      |
| :----------------------- | :------------- | :----------------- | :------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ |
| `--background`         | `#FFFFFF`    | `neutral-0`      | **Main Workspace Background**: Used for the PDF reader canvas and standard modal backdrops. Ensures a pristine, paper-like reading experience.                                                                                   |
| `--foreground`         | `#1C2128`    | `neutral-l80`    | **Primary Text / Foreground**: Matte charcoal black. Significantly minimizes eye strain during long-form reading sessions.                                                                                                       |
| `--primary`            | `#0D1117`    | `neutral-950`    | **Immersive Dark Container**: Dedicated exclusively to the **Markdown Source Editor Background**. Creates a dual-core visual contrast of "reading on paper (light) vs. writing in code (dark)".                                  |
| `--primary-foreground` | `#E6EDF3`    | `neutral-100`    | **Dark Zone Text**: Used for source text, cursors, and syntax-highlighted code inside the Markdown editor.                                                                                                                       |
| `--secondary`          | `#EAEEF2`    | `neutral-l20`    | **Raised Panel / Secondary Surface**: Sidebar backgrounds, card containers, panel chrome.                                                                                                                                        |
| `--secondary-foreground` | `#1C2128`   | `neutral-l80`    | **Secondary Surface Text**: Text on raised panels and sidebar areas.                                                                                                                                                             |
| `--muted`              | `#F0F6FC`    | `neutral-50`     | **Muted Surface**: Very subtle background for metadata rows and inactive regions.                                                                                                                                                |
| `--muted-foreground`   | `#57606A`    | `neutral-l60`    | **Secondary / Muted Text**: Literature metadata (author, year), absolute timestamps in the snapshot timeline, and local LLM performance metrics.                                                                                 |
| `--accent`             | `#DDF4FF`    | `blue-50`        | **Lightweight Interaction / Hover**: Applied to literature tree-node hover states, list selections, and context menu highlights.                                                                                                 |
| `--accent-foreground`  | `#0969DA`    | `blue-600`       | **Interactive Active Text**: High-contrast scientific blue for active text states, links, and focused list-item items.                                                                                                           |
| `--destructive`        | `#CF222E`    | `red-700`        | **Destructive / Error Alert**: Signals OCR extraction failures, memory overflow indicators, and risky rollback confirmation actions.                                                                                             |
| `--destructive-foreground`| `#FFFFFF`  | `neutral-0`      | **On-Destructive Text**: White text/icons on destructive backgrounds.                                                                                                                                                            |
| `--border`             | `#D8DEE4`    | `neutral-l30`    | **Global Splitter / Divider**: 1px physical boundary lines separating workspace panes and menus. Eradicates clumsy, thick borders; relies entirely on micro-contrast lines.                                                      |
| `--input`              | `transparent`| —                 | **Input Chrome Canvas**: Outer input chrome remains transparent so controls do not become bulky gray blocks. Actual TextBox fill uses `--input-background` when a subtle field surface is needed.                                    |
| `--input-background`   | `#EAEEF2`    | `neutral-l20`    | **Input Fill Background**: Subtle fill for text inputs, combo boxes, and search fields.                                                                                                                                          |
| `--ring`               | `#0550AE`    | `blue-700`       | **Focus Ring**: Visible focus indicator for keyboard navigation.                                                                                                                                                                 |
| `--sidebar`            | `#F0F6FC`    | `neutral-50`     | **Sidebar Background**: Context panel and auxiliary panel surfaces.                                                                                                                                                              |
| `--sidebar-foreground` | `#1C2128`    | `neutral-l80`    | **Sidebar Text**: Primary text on sidebar surfaces.                                                                                                                                                                              |
| `--sidebar-accent`     | `#DDF4FF`    | `blue-50`        | **Sidebar Selection**: Selected item background in sidebar lists.                                                                                                                                                                |
| `--sidebar-border`     | `#D8DEE4`    | `neutral-l30`    | **Sidebar Divider**: Separator lines within sidebar panels.                                                                                                                                                                      |

### 4.1 Semantic Status Colors

| Token        | Hex       | Role                                                                 |
| :----------- | :-------- | :------------------------------------------------------------------- |
| `--success`  | `#1A7F37` | Success text/icons (light); PDF matched indicator; export complete    |
| `--warning`  | `#9A6700` | Warning text/icons (light); memory 70-80%; citation mismatch          |
| `--error`    | `#CF222E` | Error text/icons (light); memory >80%; destructive action warning     |

### 4.2 AI Status Indicator Colors (Fixed — not theme-dependent)

| State            | Color    | Animation        | Meaning                                |
| :--------------- | :------- | :--------------- | :------------------------------------- |
| `unconfigured` | `#888888` | None             | No model configured                    |
| `unloaded`     | `#F85149` | None             | Model configured but not loaded        |
| `loading`      | `#D29922` | Rotation          | Model loading with progress percentage |
| `idle`         | `#3FB950` | Static            | Model ready, waiting for input         |
| `inference`    | `#3FB950` | Breathing (pulse) | Model generating tokens                |

## 5. Geometry & Corner Radius Tokens (Sharp Desktop Edge)

Academic-grade productivity tools demand crisp, sharp geometric silhouettes. Excessive Web-style rounding is strictly prohibited. Token names are aligned with `visual-assets.json` §borderRadius.

- `radius-none` (`0px`): Outer window frames, MenuBar, StatusBar, split-pane dividers — flush to the edge.
- `radius-sm` (`2px`): Toolbar buttons, DataGrid row selectors, checkboxes, radio inputs, `CitationChip`, inline tags, text inputs.
- `radius-md` (`4px`): `ChatBubble` in the AI context panel, dropdown panels, floating OCR toolbar (`FloatingCodeBlock`), cards.
- `radius-lg` (`6px`): **[Maximum Allowed Rounded Edge]** Reserved solely for the wrapper shell of independent blocking modal dialogs (`ModalHost`), such as Export, Settings, Setup Wizard, and Help/About.

## 6. Typography Ladder & Vertical Metrics (Ultra-Dense Scale)

Forced override of looser Web typography conventions. Fixed line-heights are strictly enforced to preserve screen real estate for massive data throughput. Token names are aligned with `visual-assets.json` §typography.scale.

- **UI Shell Font Stack:** `Inter, system-ui, -apple-system, 'Segoe UI', sans-serif` (Avalonia: Segoe UI / Noto Sans)
- **Code & Citations Font Stack:** `'JetBrains Mono', 'Cascadia Code', 'Fira Code', 'Consolas', monospace`

| Typography Token       | Font Size | Fixed Line-Height | Font Weight         | Target Components / UI Scenes                                                                                          |
| :--------------------- | :-------- | :---------------- | :------------------ | :--------------------------------------------------------------------------------------------------------------------- |
| `Caption`            | `10px`  | `14px`          | `400 (Regular)`   | StatusBar indicators, micro tooltips, OCR confidence percentages, timestamps.                                          |
| `Body Dense`         | `11px`  | `16px`          | `400 (Regular)`   | Reference item counts, `CitationChip` indices, metadata rows, inactive tree-node states, sidebar list items.          |
| `Subheading`         | `12px`  | `16px`          | `600 (Semi-Bold)` | Sidebar Panel section labels (e.g., "REFERENCES · 14 items"), dialog group headers, category headings.                 |
| `Body Regular`       | `13px`  | `20px`          | `400 (Regular)`   | **The Core App Font Size**: Literature list-items, text inside ChatBubbles, configuration form labels, settings body.  |
| `Code Editor`        | `14px`  | `22px`          | `400 (Regular)`   | **Monospace Code Playground**: Markdown editor workspace contents and native LaTeX math script rendering blocks.       |
| `Panel Header`       | `11px`  | `12px`          | `700 (Bold)`      | Sidebar chrome labels (e.g., "EXPLORER"), activity bar captions, uppercase panel titles.                               |

## 7. Elevation & Spatial Shadow Tokens (Micro Shadow Hierarchy)

Desktop platforms require high-contrast physical layer differentiation rather than soft, breathing web blurs. We leverage tight-spread physical elevation modeling:

- `elevation/none` (`box-shadow: none`): All native split-view panels embedded within the main grid shell. Boundaries are dictated purely by 1px micro-border dividers.
- `elevation/low` (`0 1px 3px rgba(0,0,0,.16), 0 1px 2px rgba(0,0,0,.10)`): Main top menu dropdown wrappers, command bar overlay list popovers, and auto-complete bibliography prompts.
- `elevation/medium` (`0 4px 16px rgba(0,0,0,.28), 0 2px 6px rgba(0,0,0,.18)`): Reserved for elements that break the fixed grid and need a strict depth break: blocking `ModalHost` shells and the Formula Floating OCR Toolbar.

## 8. Key Component Behaviors

- **SplitPane Divider:** 2px vertical line, expands visually on hover for easier dragging.
- **Chat Bubbles:** User (right-aligned, light gray `#F3F3F3` background). AI (left-aligned, pure white `#FFFFFF` with a 3px `#0969DA` left border indicator).
- **Status Indicators:** Micro-dots (green `#3FB950` / amber `#D29922` / red `#F85149` / gray `#888888`) positioned next to text labels to indicate connection or AI model states.
- **Empty States:** A muted, large central icon accompanied by 1-2 lines of descriptive text and an optional primary action button.
