# VoxLab visual system

Russian-language desktop audio workbench for long sessions in games, calls and recording.

## Direction

Seven grounded starting points considered: recording console, broadcast desk, guitar pedalboard, radio dial, performance sampler, mastering analyzer, and a compact field recorder. The seed assigned the seventh: **field recorder**. Its transport is always visible, the waveform is an actual instrument, and physical routes remain legible. No remote catalog was contacted after the network failure because research is constrained to GitHub.

Graphite surfaces, warm ivory labels, signal-green active processing, restrained orange for recording and overloads. A wide central preset browser with distinct authored line symbols, a calm inspector rail, and a permanent transport footer. The workspace should feel like a small useful studio instrument, not a marketing website. Code-first implementation selected to serve the executable-app request directly.

## Tokens

- Background #101211; panels #191c1a; raised surfaces #222623; lines #333a34.
- Foreground #edf0e9; secondary #b0b9ae; accent #c5f277; warning #ffc48d; danger #ff9b94.
- Manrope variable, self-hosted, including Cyrillic. Consolas for dB/ms/Hz readouts only.
- 8 px spacing base; 12 px control radius; 16 px panel radius. No gradients or decorative glow.
- 40 px controls, 2 px visible focus, semantic disabled/pressed states; reduced-motion support.

## Behavior

Real microphone and output controls on the routing page; start/pause, mute, bypass and monitor always reachable. Live sliders publish immutable parameter snapshots. Preset selection displays and edits the same settings actually used by the engine. No fake audio data in the shipped app. Browser preview explicitly says the audio engine is unavailable.
