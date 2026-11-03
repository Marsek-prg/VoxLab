# Third-party components

VoxLab's own code is MIT licensed. Included dependencies retain their respective licenses.

| Component | Version | License | Source |
| --- | --- | --- | --- |
| NAudio, NAudio.Core, NAudio.Wasapi and associated NAudio packages | 2.2.1 | MIT | https://github.com/naudio/NAudio/tree/v2.2.1 |
| SoundTouch.Net | 2.3.2 | LGPL-2.1-or-later | https://github.com/owoudenberg/soundtouch.net/tree/98e5b8fd2f8efed0ddf7c8f66b435bfb231659dc |
| Microsoft WebView2 SDK | 1.0.3650.58 | Microsoft WebView2 SDK license | https://github.com/MicrosoftEdge/WebView2Samples |
| .NET / Windows Desktop Runtime | 10.0.9 | MIT and third-party notices | https://github.com/dotnet/runtime |
| Manrope | bundled variable font | SIL Open Font License 1.1 | https://github.com/google/fonts/tree/main/ofl/manrope |

Full license texts are in `licenses/`. The self-contained runtime's own license and third-party notices are also included by the .NET publisher.

## SoundTouch.Net source and replacement

SoundTouch is originally by Olli Parviainen; the C# port is by Olaf Woudenberg. VoxLab uses the unmodified SoundTouch.Net 2.3.2 assembly as a separate dynamically loaded DLL; it is neither merged nor trimmed into the executable. The complete corresponding library source is distributed in `licenses/SoundTouch.Net-2.3.2-source.zip`, including its build files and license. The same source is available at the GitHub commit linked above.

You may modify and rebuild the library and replace `SoundTouch.Net.dll` in the application folder with a compatible build. VoxLab imposes no restriction on reverse engineering for debugging modifications to this LGPL library. See `licenses/SoundTouch-LGPL-2.1.txt` for the full terms. The application source and build scripts are included in the project distribution to permit rebuilding against modified dependencies.

## Font and artwork

Manrope is bundled locally, without runtime requests to a font service. Geometric icons and the VoxLab application mark are authored as part of this project. There is no bundled model, cloned voice, stock photography, audio cable installer or third-party character artwork.
