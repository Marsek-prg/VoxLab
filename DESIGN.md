---
name: VoxLab
description: Russian-language portable desktop voice workbench with a compact field-recorder character.
colors:
  bg: "#101211"
  surface: "#191c1a"
  raised: "#222623"
  hover: "#2b312b"
  line: "#333a34"
  text: "#edf0e9"
  muted: "#b0b9ae"
  faint: "#869280"
  accent: "#c5f277"
  accent-ink: "#1d2810"
  warning: "#ffc48d"
  danger: "#ff9b94"
  selected-surface: "#252e20"
  active-nav: "#2b3523"
  preset-sage: "#c5d7a3"
  preset-sand: "#e3c79b"
  preset-rose: "#d8aebc"
  preset-sky: "#a5c9da"
  preset-peach: "#e8b794"
typography:
  headline:
    fontFamily: "Manrope, 'Segoe UI', sans-serif"
    fontSize: "31px"
    fontWeight: 650
    lineHeight: 1.22
    letterSpacing: "-1px"
  title:
    fontFamily: "Manrope, 'Segoe UI', sans-serif"
    fontSize: "17px"
    fontWeight: 650
    lineHeight: 1.4
  module-title:
    fontFamily: "Manrope, 'Segoe UI', sans-serif"
    fontSize: "14px"
    fontWeight: 650
  body:
    fontFamily: "Manrope, 'Segoe UI', sans-serif"
    fontSize: "13px"
    lineHeight: 1.55
  action:
    fontFamily: "Manrope, 'Segoe UI', sans-serif"
    fontSize: "12px"
    fontWeight: 650
  label:
    fontFamily: "Manrope, 'Segoe UI', sans-serif"
    fontSize: "11px"
  filter:
    fontFamily: "Manrope, 'Segoe UI', sans-serif"
    fontSize: "10px"
    fontWeight: 600
  readout:
    fontFamily: "Consolas, monospace"
    fontSize: "11px"
rounded:
  track: "3px"
  chip: "7px"
  field: "8px"
  navigation: "9px"
  button: "10px"
  card: "12px"
  panel: "14px"
  overlay: "15px"
  circle: "50%"
spacing:
  s6: "6px"
  s8: "8px"
  s10: "10px"
  s12: "12px"
  s16: "16px"
  s18: "18px"
  s20: "20px"
  s22: "22px"
  s24: "24px"
  s28: "28px"
  s30: "30px"
  s32: "32px"
components:
  button-primary:
    backgroundColor: "{colors.accent}"
    textColor: "{colors.accent-ink}"
    typography: "{typography.action}"
    rounded: "{rounded.button}"
    padding: "10px 16px"
  button-secondary:
    backgroundColor: "transparent"
    textColor: "{colors.text}"
    typography: "{typography.action}"
    rounded: "{rounded.button}"
    padding: "10px 16px"
  input-text:
    backgroundColor: "{colors.raised}"
    textColor: "{colors.text}"
    rounded: "{rounded.field}"
    padding: "12px"
    width: "100%"
  navigation-item:
    backgroundColor: "transparent"
    textColor: "{colors.muted}"
    rounded: "{rounded.navigation}"
    padding: "11px 12px"
    width: "100%"
  filter-chip:
    backgroundColor: "transparent"
    textColor: "{colors.muted}"
    typography: "{typography.filter}"
    rounded: "{rounded.chip}"
    padding: "7px 10px"
  preset-card:
    backgroundColor: "{colors.surface}"
    textColor: "{colors.text}"
    rounded: "{rounded.card}"
  parameter-slider:
    backgroundColor: "{colors.line}"
    rounded: "{rounded.track}"
    height: "4px"
    width: "100%"
  level-meter:
    backgroundColor: "#333b31"
    rounded: "{rounded.track}"
    height: "5px"
---

# Design System: VoxLab

## Overview

**Creative North Star: "Compact Field Recorder"**

VoxLab is a compact field recorder expressed as a Russian-language desktop audio workbench. Graphite surfaces, warm ivory text and signal green keep the live state, editable voice settings and output route readable during long sessions. Authored outline symbols distinguish presets without photos or raster artwork.

The central workspace scrolls independently of a permanent transport. Voices have a dedicated inspector; the other pages use the full workspace width. This document reconciles the earlier directional seed with the built WebView2 interface. The field-recorder world is an implementation choice, not a claim about additional user brand preferences.

Source of truth: `src/VoxLab.App/Web/style.css`, `index.html` and `app.js`, reconciled with the rendered desktop, compact and portable-routing views. The frontmatter records reusable values as implemented and replaces the seed's approximate spacing and radius statements. This was a code-led build, without a raster-composition approval stage.

**Key Characteristics:**

- Graphite tonal layers and thin boundaries, with restrained depth.
- Manrope for Cyrillic interface text; Consolas for instrument values.
- Signal green for action and active processing; peach and salmon for exceptional states.
- Persistent transport, visible routes and an inspector tied to the selected preset.

## Colors

A near-black green-tinted workbench supports warm light text, a concentrated lime accent and muted pastel symbols. Frontmatter values are normative.

### Primary

- **Signal Green** (`accent`): primary action, active navigation text, live signal, sliders, selected preset boundary and check.
- **Green Ink** (`accent-ink`): text on the bright primary button and the enabled switch thumb.
- **Selected Moss** (`selected-surface`) and **Navigation Moss** (`active-nav`): selection backgrounds. Selected art and limiter notes reuse Selected Moss.

### Secondary

- **Warm Peach** (`warning`): routing cautions and mute/bypass emphasis.
- **Soft Salmon** (`danger`): recording, destructive preset actions and overloaded meters. This replaces the seed's generalized orange recording/overload description.
- **Preset Sage, Sand, Rose, Sky and Peach** (`preset-*`): character-symbol hues, distinct from semantic status.

### Neutral

- **Graphite** (`bg`), **Panel Graphite** (`surface`) and **Raised Graphite** (`raised`): application, contained surfaces and fields.
- **Hover Graphite** (`hover`) and **Seam Grey** (`line`): small-control hover and thin boundaries.
- **Warm Ivory** (`text`): main text and selected category background.
- **Sage Grey** (`muted`) and **Quiet Sage** (`faint`): supporting text and auxiliary hints or scales.

**The Instrument State Rule.** Use signal green for the primary action, active processing and selection. Pair status color with text, an icon, a mark or a control state.

The sidecar's generated tonal ramps are swatch previews only, not additional application tokens.

## Typography

**Interface Font:** locally bundled variable Manrope (weights 200–800), with Segoe UI and sans-serif fallbacks.
**Readout Font:** Consolas, with a monospace fallback.

Manrope carries Russian copy and compact controls. Headings use moderate weight and tight tracking. Consolas distinguishes instrument values; it also appears in build metadata, shortcut keys and the timer.

### Hierarchy

- **Headline:** the frontmatter headline role, reduced to 28 px at widths up to 1200 px and 26 px up to 750 px.
- **Title:** the title role for sections; the module-title role for compact effects headings.
- **Body:** the body role is the global base. Explanatory copy commonly uses 12 px with 1.7–1.8 line height; inspector copy uses 11 px with 1.75 line height.
- **Action and label:** buttons use the action role; parameter labels use the label role. Desktop navigation uses 12 px at weight 600.
- **Filter:** compact category labels use the filter role.
- **Readout:** parameter values and signal metrics use the readout role. The 50 px tabular recording timer is a distinct instrument display, not a general heading token.

**The Readable Instrument Rule.** Keep names and explanatory copy in Manrope. Use Consolas for measurements and compact technical values.

The source does not use a mathematical type scale. Tiny scale ticks and build metadata are auxiliary text, not sizes to inherit for new body copy.

## Layout

The desktop shell fills the viewport with a 202 px navigation rail, flexible workspace and 304 px inspector. The header is 70 px high and permanent transport 88 px. The inspector appears only on the voices page. Workspace and inspector scroll independently.

Default workspace padding is 30 px above and horizontally, 28 px below. Presets use three columns with 11 px gaps. The rack uses three columns with 18 px gaps; routing and help use two columns with 22 px gaps. Frontmatter spacing values are recurring measurements, not a mandatory single-unit grid.

- **1500 px and above:** navigation is 218 px, inspector 326 px; workspace padding is 34 px vertically and 36 px horizontally; presets have four columns.
- **1200 px and below:** navigation is 174 px, inspector 270 px; workspace padding is 24 px vertically and 22 px horizontally; the rack has two columns. Transport text labels hide while their controls remain.
- **1010 px and below:** navigation contracts to a 66 px icon rail and inspector to 255 px; workspace padding is 22 px vertically and 20 px horizontally. Presets retain three columns.
- **751–1010 px:** compact navigation exposes a tooltip on hover and keyboard focus without clipping it at the rail edge.
- **750 px and below:** the voices shell uses a 58 px rail, no inspector, a 60 px header and a 75 px transport. Presets have two columns; rack, routing and help have one. Transport meters hide while start and record remain. These are stylesheet adaptations, not a separate mobile-product claim.

## Elevation & Depth

Persistent surfaces are flat. Tonal differences, single-pixel boundaries and moss fills establish hierarchy. The inspector and transport have their own source surface shades; neither floats above the workspace. No gradients or decorative glows are implemented.

### Shadow Vocabulary

- **Slider thumb:** `0 1px 3px #0008`.
- **Navigation tooltip:** `0 5px 16px #0005`.
- **Toast:** `0 6px 24px #0005`.
- **Dialog:** `0 15px 60px #0007`, with a `#0009` backdrop.

**The Tonal Workbench Rule.** Separate persistent regions with tonal surfaces and thin borders. Reserve cast shadows for transient overlays and the small slider thumb.

Control and card transitions use 150 ms ease; meters use 65 ms linear transforms; toast entry uses 180 ms ease-out. Enabled pressed buttons shift down 1 px. Reduced-motion preferences disable animations and transitions.

## Shapes

Related, individually sized corner radii distinguish small controls, buttons, cards and panels. The frontmatter records their actual values. Borders are generally 1 px; status dots, slider thumbs and recording motifs are circular.

Authored SVGs use round caps and joins. General icons are 20 px with a 1.7 stroke; preset symbols are 35 px with a 1.35 stroke. The inspector enlarges the selected symbol without changing its drawing language.

## Components

### Primary button

Bright transport and confirmation action. Uses the primary tokens, a transparent 1 px border and minimum 40 px height. Hover becomes `#d7ff95`; enabled press shifts down 1 px. The desktop start variant has minimum height 45 px and minimum width 173 px. Its running state becomes a subdued green stop control.

### Secondary button

Quiet outlined import, reset, refresh and setup action. Shares the primary shape, type and padding. Its resting border uses Seam Grey; hover uses Raised Graphite and border `#515d50`. No shadow.

### Text input and device fields

Raised Graphite, Seam Grey border, 12 px text and minimum 42 px height. Text-field padding is 12 px; selects reserve extra right-hand indicator space. Placeholder text is Quiet Sage; caret is Signal Green. Disabled controls use opacity 0.42 and a not-allowed cursor. Search is a separate borderless treatment.

### Navigation

Outline symbols, text and optional monospaced counts. Active state uses Navigation Moss with Signal Green; inactive hover uses Panel Graphite with Warm Ivory text. The compact desktop rail retains accessible names and exposes an ivory tooltip with dark text on hover or focus.

### Category chip

A compact text filter with minimum height 32 px. Active state reverses to Warm Ivory with Graphite text. Inactive hover uses Raised Graphite. Each category button exposes its pressed state.

### Preset card

Bordered Panel Graphite with an authored symbol, name, description and independent favorite button. Default minimum height is 136 px. Hover uses `#202520` and border `#63705c`; selection uses Selected Moss, a green border and check. The main selector has an inner 11 px radius and 16 px 13 px 12 px padding. Its focus outline is inset by 4 px so the clipped card does not obscure it.

### Parameter slider

A label and monospaced value sit above the narrow track. The round green thumb is 12 px, with a 2 px dark border; hover lightens it and scales it to 1.15. Focus has an 8 px outline offset. Each slider carries an associated label, current value text and units; repeated views edit the same parameter.

### Level meter

A compact label/readout above the narrow transport track. Measured signal fill scales from the left; overload becomes Soft Salmon. Text and numeric readings preserve meaning beyond color. Idle state displays silence, not decorative motion.

Focusable buttons, links, inputs, selects and summaries use a 2 px Signal Green outline with a default 4 px offset; the card and slider exceptions are stated above. Grid, category, inspector and rack re-renders preserve or restore keyboard focus. The sidecar contains self-contained examples of these eight component families.

## Do's and Don'ts

### Do:

- **Do** retain the visible transport while the workspace or inspector scrolls.
- **Do** preserve Russian labels and the bundled Manrope font with Cyrillic support.
- **Do** pair status color with readable text, a mark or the relevant semantic control state.
- **Do** preserve keyboard focus when preset, category, inspector or rack content is re-rendered.
- **Do** retain accessible names and the implemented hover/focus tooltip in compact navigation.
- **Do** display real signal data and explicit paused or preview states.

### Don't:

- **Don't** turn pastel preset-symbol colors into additional primary action colors.
- **Don't** apply card shadows, gradients or decorative glow to the flat workbench surfaces.
- **Don't** replace authored SVG symbols with character glyphs or icon fonts.
- **Don't** infer an 8 px-only spacing grid or a universal control/panel radius from this build.
- **Don't** make decorative or simulated waveforms look like live microphone activity.
