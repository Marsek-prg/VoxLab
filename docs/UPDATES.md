# Portable updates

The app checks `Marsek-prg/VoxLab` public GitHub Releases on demand. There is no sign-in, background service or scheduled network check. GitHub sees normal release/download requests; microphone audio is never sent.

The release contains the full portable ZIP plus `VoxLab-update.json` and three content-addressed ZIP components: `app`, `runtime`, `drivers`. Files in a component are downloaded together, but only files with changed size/SHA-256 are staged and replaced. This is not a binary delta protocol. The archive of VB-CABLE remains separate and its driver is never installed by the updater.

The manifest declares every file path, size, hash and component. Only `VoxLab.exe`, the root readme and paths under `App/` are accepted. Data, traversal, absolute paths, alternate streams, reserved device names, duplicate/case-colliding paths, and reparse ancestors are rejected. ZIP entries must match the manifest. HTTPS release assets must belong to the same GitHub repository/tag. Hashes detect corruption; they are not an independent publisher signature.

## Installation and recovery

1. Finish the current WAV recording, then choose **Install and restart**.
2. The local PowerShell helper locks `App/.update.lock`, acknowledges startup and waits for the exact originating process to exit. It does not kill processes or elevate.
3. All payload files are verified again. Changed existing files are copied into `Data/Updates/<id>/backup/` before any replacements.
4. File replacements are atomic individually. If any replacement fails, applied replacements are reversed using the backup; newly created files are removed. Unknown/obsolete files are not pruned.
5. `Data/Updates/last-result.json` records the outcome. The app reopens with its original Data path. If rollback itself fails, automatic relaunch is skipped; close anything holding the files and restore that backup manually, or extract the full portable ZIP over the folder.

Power loss or forced process termination can interrupt a transaction across multiple files. Keep the backup until the new version has started successfully. The standard full portable ZIP can always be extracted over the installation while VoxLab is closed; it contains no Data folder. Copying the backup's contents back into the portable root restores replaced files. Data is never part of the replacement plan.

Backups and staged files remain in Data/Updates for recovery. After confirming the updated version works, you may remove completed update subfolders while VoxLab is closed. Keep `profile.json`, `Recordings`, custom presets and other Data files. If the folder is read-only, move the whole portable folder to a writable location.

## Building a release

```powershell
./build.ps1 -Test -Publish
./tests/Test-Updater.ps1
python scripts/package_release.py artifacts/VoxLab-win-x64 artifacts/release 0.2.0
```

Upload all generated files to the stable `v0.2.0` GitHub Release. Create/upload a draft first, then publish it after all assets are present. Keep version values in Directory.Build.props, UpdateService, UI and the package command consistent. Preserve filenames and hashes. Never include Data, credentials or a developer profile in a release.
