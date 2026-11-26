# UI Style

The interface is the field optics of a mortal observer inside a living ecosystem. It does not decorate the world and does not crown the player.

Applies to every view: HUD, inventory, world labels, terminal, skill graph, settings and world list. Read together with `PHILOSOPHY.md`; code rules stay in `docs/OPTIMIZATION.md` and `docs/SECURITY.md`.

## 1. Formula

- Who looks: an ordinary creature, human or external AI, trying to understand a world it does not control.
- What it sees: tundra, taiga, loam, sand, fog, stone walls, hundreds of autonomous creatures.
- What the UI is: thin geodesic and biological optics over the world: 1 px contours, viewfinder corner brackets, dashed leader lines, grids, monospaced telemetry, flat vector diagrams.
- Spirit, not copy: the precision of a field instrument, the honest readability of a systems simulator, a HUD that dissolves into the atmosphere.

## 2. Principles

1. **The world comes first; the UI vanishes at rest.** A value in its normal state (full stamina, no threat, no open window) is hidden or dimmed. The area around the body and the direction of motion stay free of fixed panels. An open window gets a backdrop so sand, snow or sky never eat its numbers.
2. **No crown.** No gold frames, crests, vignettes or accents that make the player the centre (`PHILOSOPHY.md` sections 2 and 4). An inspector takes an entity or colony and reads the player and a wild creature the same way. The HUD gives the player no perception a creature lacks.
3. **Honest complexity.** A modifier or a codex law is shown as its calculation chain, never as a vague buff.
4. **Contour over fake volume.** No bevels, gloss, drop shadows, glow or blur (`PHILOSOPHY.md` section 5). Volume belongs to the stone of the world, not to buttons.
5. **Colour is a signal.** Earthy neutral base; accents are muted mineral pigments of one brightness. One screen has one main accent: the active selection, a critical deficit, or the edited node. Two tokens may share a hue only if they never meet on one screen.
6. **Tools are part of the world.** Terminal, codex output and the skill graph use the same tokens, grid and brackets as the inventory; none looks like a debug overlay.
7. **Cheap UI means more life.** A closed panel or a culled label costs nothing per frame. Text is rewritten only when its value changes. Telemetry refreshes at most every 0.1–0.25 s (the skill graph uses 250 ms).
8. **World text is hostile.** LLM descriptions, NPC speech, names, speech tokens and Rive output are rendered with rich text off or with tags escaped, have a length limit and never break the layout (`docs/SECURITY.md` section 11).
9. **Intent only.** A button sends a request; the server decides. A refusal is shown calmly with its reason and no local guess (`docs/SECURITY.md` section 3).

## 3. Signature elements

- Viewfinder brackets inside inventory slots: the main graphic mark of the game.
- Functional zones of the inventory separated by the colour of their corner brackets, without heavy headers.
- Vector graphs through `Painter2D` with pooled elements, as in `SkillGraphCanvas` and `SkillNodeElement`.

## 4. Tokens

Source of truth: `Assets/Resources/UI/DesignTokens.uss`. A UI Toolkit sheet uses `var(--ravine-…)` and never a literal colour.

- UI Toolkit: the panel loads the tokens sheet before its own sheet (`SkillGraphPanel.BuildTree`).
- `Painter2D` cannot read USS variables: `SkillGraphPalette` mirrors the graph tokens; change both in one commit.
- uGUI: colours are `[SerializeField]` fields whose values are taken from the token table below.

| Group | Tokens |
|---|---|
| Base | `--ravine-bg-backdrop`, `--ravine-surface-panel`, `--ravine-surface-elevated`, `--ravine-surface-slot`, `--ravine-surface-hover`, `--ravine-surface-canvas` |
| Lines | `--ravine-border-subtle`, `--ravine-border-frame`, `--ravine-reticle-bracket`, `--ravine-reticle-active` |
| Ink | `--ravine-ink-primary`, `--ravine-ink-secondary`, `--ravine-ink-muted` |
| Inventory zones | `--ravine-zone-equip`, `--ravine-zone-pouch`, `--ravine-zone-craft`, `--ravine-zone-bag`, `--ravine-zone-analysis` |
| Body telemetry | `--ravine-stat-vitality`, `--ravine-stat-energy`, `--ravine-stat-thermal`, `--ravine-stat-viral` |
| Signals | `--ravine-signal-refusal` |
| Skill graph | `--ravine-graph-*`, `--ravine-node-*` |

- `--ravine-ink-muted` is for hints and inactive lines only, never for numbers.
- Forbidden palettes: SaaS dark blue (`#0f172a`, `#1e293b` and neighbours), neon purple or cyan borders.

## 5. Geometry

- Grid module 4 px: 4, 8, 12, 16, 24, 32.
- `border-radius: 0` for windows, slots, buttons, bars and menus. Allowed exceptions: a chamfer or a 45° rhombus for skill markers; round graph nodes, because their progress ring is an arc.
- Lines: 1 px for grids, dividers and frames; 2 px for active brackets. Bracket arm: 6 px on a slot, 10 px on a window.
- Text padding: at least 12 px in narrow inspectors and menus, 16 px in reading panels (LLM, codex, terminal). Text never touches a border.
- HUD safe margin: at least 16 px from the screen edge at the reference resolution.
- Icons: one pixel scale per view (32 or 48 px base), 6–8 px from the slot edge so they do not touch the brackets.
- Pixel art: point filtering, no compression, no mixed pixel densities in one view.

## 6. Typography

Two families with fixed roles:

- Monospace with Cyrillic and tabular digits (JetBrains Mono, IBM Plex Mono or Iosevka): stacks, coordinates, `100 / 100`, terminal, graph nodes and formulas, section titles.
- Neutral grotesque (Inter, IBM Plex Sans or PT Sans): item descriptions, codex articles, NPC speech, settings.

| Role | Size | Style |
|---|---|---|
| Window or section title | 13–14 px | monospace, bold, upper case, 1 px letter spacing |
| Reading text | 14–15 px | sans, line height 1.45 |
| Telemetry, stacks | 12–13 px | monospace, 1 px dark underlay over icons |
| World labels | 11–12 px | monospace, one line |

## 7. Motion and sound

- Motion is dry and instrumental, like a relay or a lens focusing. Only LitMotion; bounce and elastic eases are forbidden; no scale animation on uGUI hierarchies.
- Hover or focus: 60–90 ms, `Ease.OutQuad`.
- Context indicator in or out: 100–140 ms, `Ease.OutCubic`.
- Window open: 120–160 ms, backdrop fade plus a 4 px shift.
- Sound goes through `UISoundSystem`: hover is a short quiet tick, confirm a dry double click, refusal a low dull tone, never a sharp beep.

## 8. Technology map

| Layer | Technology | Code |
|---|---|---|
| HUD, inventory | uGUI, 9-slice | `UIInventory`, `UIInventorySlot`, `UIDragger`, `WorldEditFeedback` |
| World labels | world-space uGUI | `EntityView` |
| Graphs and inspectors | UI Toolkit runtime, `Painter2D` | `SkillGraphPanel`, `SkillGraphCanvas`, `SkillNodeElement` |
| Terminal | uGUI | `docs/map/terminal.md` |
| Main menu | live scene plus a thin overlay | `WorldManagerUI`, `SettingsUI` |

- A modal or a text input switches input maps through `ActionMapController`; closing restores the previous map.

## 9. Checklist for a UI change

- No literal colour in a USS file; new colours become tokens first.
- No shadow, glow, blur, gradient, bevel, rounded card, emoji icon or heroic frame.
- Padding, margin and sizes on the 4 px grid; text padding per section 5.
- Hidden view costs nothing; visible text is rewritten only on change.
- Untrusted text: rich text off or escaped, length limited.
- Gameplay mutation: intent sent to the server, refusal displayed.
- The same view works for the player and for any creature.
