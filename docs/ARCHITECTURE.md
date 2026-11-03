# Architecture

## Boundaries

- `VoxLab.Core`: immutable parameter validation, 16 canonical presets, streaming mono DSP. Depends only on SoundTouch.Net.
- `VoxLab.App`: native WPF lifecycle, WASAPI, bounded audio queues, recording, local preferences, hotkeys, tray and a restricted WebView2 bridge.
- `VoxLab.App/Web`: local interface with no build framework or remote resources. Browser-only mode is explicitly a non-audio preview.
- `VoxLab.Tests`: executable deterministic DSP tests without an additional testing framework.

## Signal path

Native endpoint PCM16/24/32 or IEEE float → selected channel / mono downmix → smoothed input gain → high-pass → hysteresis gate → bass/mid/treble & tone EQ → split-band de-esser → RMS soft-knee compressor → SoundTouch pitch (when nonzero) → ring modulation → saturation → optional radio filters → dual delay chorus → echo → Schroeder reverb → smoothed output gain → instantaneous-attack/releasing limiter (0.95 ceiling) → mute → independent output and monitor queues; processed WAV recording taps before monitor gain.

The pitch stage changes pitch without changing intended sample duration. Its block output is bursty, so output queues prebuffer. Input and output endpoints have independent clocks. A half-second bounded queue drops oldest data if full instead of accumulating unbounded latency. Underruns zero-fill and re-prime; counters are surfaced to the user. This is bounded recovery, not a precision drift-compensating asynchronous resampler.

## Threading

Audio capture performs decoding and DSP on the WASAPI capture callback thread. UI changes publish immutable validated settings using `Volatile.Write`; the processor consumes snapshots at block boundaries. The short processing lock protects recording handoff and UI telemetry. Buffer locks protect independent WASAPI rendering consumers. Main and monitor outputs have their own queues and WASAPI format adaptation.

Recording copies PCM to pooled buffers and sends them to a bounded channel read by a background disk writer. Queue exhaustion stops recording with an error rather than blocking the audio callback. WAV headers finalize on stop. Classic WAV recordings are capped before the 4 GiB format limit.

Meters are sent at ~16 Hz. The waveform is actual processed samples; there is no synthetic running animation. UI updates and COM device lifecycle stay outside DSP parameter mutation. Endpoint failures stop capture/playback and surface recovery instructions.

## Persistence and security boundary

Settings are written atomically to `%APPDATA%/VoxLab/profile.json`. Import size and counts are bounded; all numerical DSP parameters are clamped and NaN/Infinity are sanitized. User strings render as text or escaped HTML. The WebView can navigate only to the mapped `https://voxlab.local` origin; new windows and web permissions are denied. A CSP prevents external scripts/resources. Bridge commands are an explicit allowlist; no command executes user-supplied shell text. File destinations are chosen through Windows dialogs.

The app has no telemetry, automatic updater, login, model downloader, microphone autostart or persistent background service. The native host captures audio; browser microphone permission is unnecessary. `--data-dir` isolates automated tests from real user preferences.

## Limits and future work

No model inference, independent formant manipulation, neural noise suppression, VST hosting or kernel audio driver. Nonzero pitch changes and bypass switching can introduce short transients; exact latency varies by endpoint. Quality improvements could include per-preset loudness calibration on speech, click-free crossfades between complete processor instances, an adaptive asynchronous output resampler, and a separately licensed denoising model.

Do not advertise a latency number as a physical measurement until a loopback round-trip measurement has actually been made.
