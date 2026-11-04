# Third-party components

VoxLab's own code is MIT licensed. Included dependencies retain their respective licenses.

| Component | Version | License | Source |
| --- | --- | --- | --- |
| NAudio, NAudio.Core, NAudio.Wasapi and associated NAudio packages | 2.2.1 | MIT | https://github.com/naudio/NAudio/tree/v2.2.1 |
| SoundTouch.Net | 2.3.2 | LGPL-2.1-or-later | https://github.com/owoudenberg/soundtouch.net/tree/98e5b8fd2f8efed0ddf7c8f66b435bfb231659dc |
| Microsoft WebView2 SDK | 1.0.3650.58 | Microsoft WebView2 SDK license | https://github.com/MicrosoftEdge/WebView2Samples |
| .NET / Windows Desktop Runtime | 10.0.13 | MIT and third-party notices | https://github.com/dotnet/runtime |
| Manrope | bundled variable font | SIL Open Font License 1.1 | https://github.com/google/fonts/tree/main/ofl/manrope |
| VB-CABLE Basic original ZIP | Driver Pack 45 | VB-Audio Donationware terms inside original archive | https://www.vb-cable.com |

Full license texts are in `licenses/`, including the self-contained .NET runtime's license, third-party notices and Windows Desktop Runtime license, copied from their original runtime packages.

## SoundTouch.Net source and replacement

SoundTouch is originally by Olli Parviainen; the C# port is by Olaf Woudenberg. VoxLab uses the unmodified SoundTouch.Net 2.3.2 assembly as a separate dynamically loaded DLL; it is neither merged nor trimmed into the executable. The complete corresponding library source is distributed in `licenses/SoundTouch.Net-2.3.2-source.zip`, including its build files and license. The same source is available at the GitHub commit linked above.

You may modify and rebuild the library and replace `SoundTouch.Net.dll` in the application folder (`App/SoundTouch.Net.dll` in portable 0.1.1) with a compatible build. VoxLab imposes no restriction on reverse engineering for debugging modifications to this LGPL library. See `licenses/SoundTouch-LGPL-2.1.txt` for the full terms. The application source and build scripts are included in the project distribution to permit rebuilding against modified dependencies.

## Font and artwork

Manrope is bundled locally, without runtime requests to a font service. Geometric icons and the VoxLab application mark are authored as part of this project. There is no bundled model, cloned voice, stock photography or third-party character artwork.

## VB-CABLE Basic independent package

VB-CABLE is by VB-Audio / Vincent Burel. Origin: https://www.vb-cable.com. This is **Donationware**; voluntary participation and donations to its author are welcome. The original `VBCABLE_Driver_Pack45.zip` is redistributed AS IS without modification. Its full license is in `readme.txt` inside the archive. VoxLab's MIT license does not apply to this package.

VoxLab does not integrate the driver's installation into its own setup procedure: there is no app installer, and the only related action opens the archive's location. Users independently extract and run the original publisher's installer. See `Drivers/VB-CABLE/README.txt` for provenance, verification and checksum.
