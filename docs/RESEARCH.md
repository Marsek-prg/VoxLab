# GitHub research and decisions

Research used GitHub repositories. No GitLab repository or page was visited. Libraries below were reviewed at their GitHub sources; binary dependencies are restored from NuGet.

| Source | Relevant observation | VoxLab decision |
| --- | --- | --- |
| [w-okada/voice-changer](https://github.com/w-okada/voice-changer) | Realtime voice conversion, shortcuts and buffer visualization; neural engines have additional model/runtime requirements. | Keep immediate CPU DSP presets, global hotkeys and visible buffering diagnostics. Neural conversion is not implied or bundled. |
| [wwmm/easyeffects](https://github.com/wwmm/easyeffects) | A broad voice-processing chain includes gate, compressor, de-esser, equalizer, pitch and spatial effects. | Offer a focused editable voice chain with clear units and consistent preset parameters. Do not port its Linux/PipeWire backend. |
| [Zeit-Loop/VoiceShift](https://github.com/Zeit-Loop/VoiceShift) | Windows WASAPI, bounded transport, DSP presets and explicit development limits. | Use WASAPI with bounded queues, channel conversion, failure reporting and honest latency estimates. |
| [naudio/NAudio v2.2.1](https://github.com/naudio/NAudio/tree/v2.2.1) | Windows capture/playback, PCM formats, shared-mode resampling, WAV writing. | Pin the stable 2.2.1 API and use it directly for native I/O. |
| [owoudenberg/soundtouch.net](https://github.com/owoudenberg/soundtouch.net) | Managed time-domain pitch shifting independent of tempo; LGPL license. | Use 2.3.2 as a separate replaceable DLL, include its full corresponding source and license. |

These are references and component sources, not copied application templates. VoxLab's UI, parameter model, custom presets, routing layer, lifecycle and effects implementation were authored for this project. Presets are hand-tuned starting points, not perceptually validated replacements for another person's voice.

## Why an external virtual cable

A playback endpoint cannot become an OS microphone solely through a user-mode WPF app. A separately installed signed audio driver provides the paired render/capture endpoints. This avoids a custom kernel driver, test-signing and security setting changes. The application works with installed virtual cables; their drivers and licenses remain separate.
