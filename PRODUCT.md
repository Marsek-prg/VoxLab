# VoxLab

<!-- impeccable:product-schema 1 -->

## Platform

Windows desktop, native WASAPI audio with an embedded WebView2 interface.

## Stack

Implementation choice delegated by the request to choose the simpler routing approach: C#/.NET 10, WPF host, local HTML/CSS/JavaScript. No remote UI, cloud inference or required GPU.

## Users

Russian-speaking PC user. Confirmed equal priorities: conversations, games and recording.

## Product Purpose

Transform a live microphone into character voices, route the result to a selectable playback endpoint, and record processed audio locally. External applications receive the voice through an installed virtual audio cable.

## Capabilities and Constraints

High, low, robotic, echo, clown, childlike and feminine character presets; editable DSP parameters, compressor, gate and other voice tools. GitHub only for source hosting and research, owner marsek-prg. Deliver an executable plus editable source. A kernel virtual microphone driver is outside this application; use existing signed virtual cable drivers. No promise of AI voice identity replacement or zero latency.

Confirmed follow-up: the application must be portable, with no app installation. Bundle the .NET runtime and keep preferences and WebView2 cache in a Data folder beside the executable. Provide an integrated optional virtual-cable setup assistant, clearly distinguishing the portable app from the one-time Windows driver installation.

Confirmed follow-up: minimize system load and perform a final bug check on the deliverable. Measure CPU and memory for the complete app, test audio lifecycle and recording failures, and disclose the limits of local validation rather than claiming universal production certification.

## Product Principles

- The microphone only runs after an explicit start action.
- Presets must translate into real editable parameters.
- Routing, signal state and overloads are visible.
- Work offline after installation; save settings and recordings locally.
- Useful keyboard controls and readable Russian labels.

## Open decisions

VoxLab is a working product name selected for this implementation. Visual direction and engineering defaults are implementation assumptions, not additional user preferences.
