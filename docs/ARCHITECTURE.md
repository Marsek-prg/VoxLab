# Architecture

## Boundaries

- `VoxLab.Core`: immutable parameter validation, 24 canonical presets, streaming mono DSP. Depends only on SoundTouch.Net.
- `VoxLab.App`: native WPF lifecycle, WASAPI, bounded audio queues, recording, local preferences, hotkeys, tray and a restricted WebView2 bridge.
- `VoxLab.App/Web`: local interface with no build framework or remote resources. Browser-only mode is explicitly a non-audio preview.
- `VoxLab.Tests`: executable deterministic DSP tests without an additional testing framework.

## Signal path

Native endpoint PCM16/24/32 or IEEE float → selected channel / mono downmix → smoothed input gain → high-pass → hysteresis gate → bass/mid/treble & tone EQ → split-band de-esser → RMS soft-knee compressor → SoundTouch pitch (when nonzero) → ring modulation → saturation → optional cascaded radio band + in-band saturation → sample/bit crusher → tremolo → feedback flanger → dual delay chorus → echo → Schroeder reverb → smoothed output gain → instantaneous-attack/releasing limiter (0.95 ceiling) → mute → independent output and monitor queues; processed WAV recording taps before monitor gain.

The pitch stage changes pitch without changing intended sample duration. Its block output is bursty, so output queues prebuffer. Input and output endpoints have independent clocks. A half-second bounded queue drops oldest data if full instead of accumulating unbounded latency. Underruns zero-fill and re-prime; counters are surfaced to the user. This is bounded recovery, not a precision drift-compensating asynchronous resampler.

## Threading

Audio capture performs decoding and DSP on the WASAPI capture callback thread. UI changes publish immutable validated settings using `Volatile.Write`; the processor consumes snapshots at block boundaries. The short processing lock protects recording handoff and UI telemetry. Buffer locks protect independent WASAPI rendering consumers. Main and monitor outputs have their own queues and WASAPI format adaptation.

Recording copies PCM to pooled buffers and sends them to a bounded channel read by a background disk writer. Queue exhaustion stops recording with an error rather than blocking the audio callback. WAV headers finalize on stop. Classic WAV recordings are capped before the 4 GiB format limit.

Meters are sent at ~16 Hz only while audio runs and the window is visible; idle meters update with state changes. Hidden/minimized windows stop meter traffic and request WebView2 suspension. The waveform is actual processed samples; there is no synthetic running animation. UI updates and COM device lifecycle stay outside DSP parameter mutation. Endpoint failures stop capture/playback and surface recovery instructions. A lifecycle lock serializes start/stop/dispose, while a per-data-directory mutex prevents two instances from overwriting the same profile.

## Persistence and security boundary

Portable 0.1.1 keeps only `VoxLab.exe`, a short readme, `App` and (after first launch) `Data` at the top level. The SDK's `CreateAppHost` task embeds the relative managed assembly path `App/VoxLab.dll` in the normal Windows GUI apphost. Libraries, runtime, Web assets, drivers and notices stay in `App`; there is no extra launcher process or startup extraction. `AppContext.BaseDirectory` points to `App`, while the data root follows `Environment.ProcessPath` for VoxLab.exe. Direct `dotnet` development runs fall back to the assembly directory. Relative command-line paths still resolve against the caller's working directory.

Settings are written atomically to `Data/profile.json` beside the executable. The WebView2 user-data folder and default recording directory also live under `Data`, keeping the application portable. Import size and counts are bounded; all numerical DSP parameters are clamped and NaN/Infinity are sanitized. User strings render as text or escaped HTML. The WebView can navigate only to the mapped `https://voxlab.local` origin; new windows and web permissions are denied. A CSP prevents external scripts/resources. Bridge commands are an explicit allowlist; no command executes user-supplied shell text. File destinations are chosen through Windows dialogs.

The app has no telemetry, automatic updater, login, model downloader, microphone autostart or persistent background service. The native host captures audio; browser microphone permission is unnecessary. `--data-dir` isolates automated tests from real user preferences.

The original VB-CABLE ZIP is shipped as an independent, unmodified package. The application only opens its location in Explorer; it never launches its installer, requests elevation for it, or accepts its terms. A Windows driver installation is separate from the portable application and persists beyond its deletion.

## Limits and future work

No model inference, independent formant manipulation, neural noise suppression, VST hosting or kernel audio driver. Nonzero pitch changes and bypass switching can introduce short transients; exact latency varies by endpoint. Quality improvements could include per-preset loudness calibration on speech, click-free crossfades between complete processor instances, an adaptive asynchronous output resampler, and a separately licensed denoising model.

Do not advertise a latency number as a physical measurement until a loopback round-trip measurement has actually been made.

## Updates and configurable shortcuts (0.2.0)

`Hotkeys.cs` validates saved actions, key ranges, modifiers and duplicate chords. `RegisterHotKey` uses MOD_NOREPEAT. Capture temporarily releases OS registrations; blur/Escape restores them. Dispatch maps registration IDs to immutable bindings. JSON profiles without bindings receive the original defaults; an intentionally empty list remains empty.

`UpdateService` reads the public GitHub stable release only on explicit user action. It bounds and validates manifests, safe relative paths, sizes and SHA-256, refuses reparse ancestors, and downloads only required content-addressed components. A verified payload and JSON plan are staged under Data/Updates. The separate Windows PowerShell helper acquires an exclusive installation lock, acknowledges readiness, waits for the originating PID/start time, backs up and atomically replaces changed files, rolling back applied changes on failure. No secrets, elevation, driver installation or background scheduler. Tests cover service requests through an injected transport and actual helper transactions on Windows PowerShell 5.1.
